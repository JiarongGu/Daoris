/**
 * A model endpoint on loopback for keyless harness proofs: no account, no credential, no model. It speaks the two wires the
 * harnesses call, Anthropic's Messages (`/v1/messages`) and OpenAI's Responses (`/v1/responses`), each streamed, and logs every
 * call's input so a proof reads where a word landed from what the model is sent next.
 *
 * The first call of a turn whose last user words carry `TOOL` is answered with one shell call that sleeps; one whose words carry
 * `STREAM` with a slow stream of text; every other call with a short reply. A tool's result never starts the sleep again.
 */
import { createServer } from 'node:http';

const SLEEP_SECONDS = 8;
const STREAM_CHUNKS = 24;
const STREAM_GAP_MS = 150;

/** The text of a Messages or Responses input item, its blocks joined. */
function textOf(content) {
  if (typeof content === 'string') return content;
  if (!Array.isArray(content)) return '';
  return content
    .map((block) => (typeof block === 'string' ? block : block?.text ?? (typeof block?.content === 'string' ? block.content : textOf(block?.content))))
    .filter(Boolean)
    .join('\n');
}

/** What a call asks for: `tool`, `stream` or `reply`, read from its last user message, never from a tool's result. */
export function askedFor(items) {
  const last = items.at(-1);
  if (!last) return 'reply';
  const blocks = Array.isArray(last.content) ? last.content : [];
  const fromTool = last.type === 'function_call_output' || blocks.some((block) => block?.type === 'tool_result');
  if (fromTool) return 'reply';
  const text = textOf(last.content);
  if (/\bTOOL\b/.test(text)) return 'tool';
  if (/\bSTREAM\b/.test(text)) return 'stream';
  return 'reply';
}

const sse = (res, event, data) => res.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function anthropic(res, kind, n) {
  res.writeHead(200, { 'content-type': 'text/event-stream', 'cache-control': 'no-cache' });
  const id = `msg_fake_${n}`;
  sse(res, 'message_start', {
    type: 'message_start',
    message: { id, type: 'message', role: 'assistant', model: 'fake-model', content: [], stop_reason: null, stop_sequence: null, usage: { input_tokens: 10, output_tokens: 1 } },
  });
  if (kind === 'tool') {
    sse(res, 'content_block_start', { type: 'content_block_start', index: 0, content_block: { type: 'tool_use', id: `toolu_fake_${n}`, name: 'Bash', input: {} } });
    sse(res, 'content_block_delta', { type: 'content_block_delta', index: 0, delta: { type: 'input_json_delta', partial_json: JSON.stringify({ command: `sleep ${SLEEP_SECONDS}`, description: 'wait' }) } });
    sse(res, 'content_block_stop', { type: 'content_block_stop', index: 0 });
    sse(res, 'message_delta', { type: 'message_delta', delta: { stop_reason: 'tool_use', stop_sequence: null }, usage: { output_tokens: 5 } });
  } else {
    sse(res, 'content_block_start', { type: 'content_block_start', index: 0, content_block: { type: 'text', text: '' } });
    const chunks = kind === 'stream' ? STREAM_CHUNKS : 1;
    for (let at = 0; at < chunks; at += 1) {
      sse(res, 'content_block_delta', { type: 'content_block_delta', index: 0, delta: { type: 'text_delta', text: kind === 'stream' ? `word${at} ` : `reply ${n}` } });
      if (kind === 'stream') await pause(STREAM_GAP_MS);
    }
    sse(res, 'content_block_stop', { type: 'content_block_stop', index: 0 });
    sse(res, 'message_delta', { type: 'message_delta', delta: { stop_reason: 'end_turn', stop_sequence: null }, usage: { output_tokens: 5 } });
  }
  sse(res, 'message_stop', { type: 'message_stop' });
  res.end();
}

async function responses(res, kind, n) {
  res.writeHead(200, { 'content-type': 'text/event-stream', 'cache-control': 'no-cache' });
  const id = `resp_fake_${n}`;
  const response = { id, object: 'response', status: 'in_progress', model: 'fake-model', output: [] };
  sse(res, 'response.created', { type: 'response.created', response });
  let item;
  if (kind === 'tool') {
    const command = process.platform === 'win32'
      ? ['powershell.exe', '-NoProfile', '-Command', `Start-Sleep -Seconds ${SLEEP_SECONDS}`]
      : ['sleep', String(SLEEP_SECONDS)];
    item = { type: 'function_call', id: `fc_fake_${n}`, call_id: `call_fake_${n}`, name: 'shell', arguments: JSON.stringify({ command }), status: 'completed' };
    sse(res, 'response.output_item.added', { type: 'response.output_item.added', output_index: 0, item: { ...item, arguments: '' } });
  } else {
    const chunks = kind === 'stream' ? STREAM_CHUNKS : 1;
    const words = [];
    item = { type: 'message', id: `msg_fake_${n}`, role: 'assistant', status: 'in_progress', content: [] };
    sse(res, 'response.output_item.added', { type: 'response.output_item.added', output_index: 0, item });
    for (let at = 0; at < chunks; at += 1) {
      const delta = kind === 'stream' ? `word${at} ` : `reply ${n}`;
      words.push(delta);
      sse(res, 'response.output_text.delta', { type: 'response.output_text.delta', item_id: item.id, output_index: 0, content_index: 0, delta });
      if (kind === 'stream') await pause(STREAM_GAP_MS);
    }
    item = { ...item, status: 'completed', content: [{ type: 'output_text', text: words.join(''), annotations: [] }] };
  }
  sse(res, 'response.output_item.done', { type: 'response.output_item.done', output_index: 0, item });
  sse(res, 'response.completed', {
    type: 'response.completed',
    response: { ...response, status: 'completed', output: [item], usage: { input_tokens: 10, output_tokens: 5, total_tokens: 15, input_tokens_details: { cached_tokens: 0 }, output_tokens_details: { reasoning_tokens: 0 } } },
  });
  res.end();
}

/**
 * Serves on an ephemeral loopback port until `close()`. `calls` holds each model call as `{ at, wire, kind, input }`, `input`
 * the last user words and every tool result it carried, in the order they arrived.
 */
export async function startFakeModel() {
  const calls = [];
  let n = 0;
  const server = createServer((req, res) => {
    const chunks = [];
    req.on('data', (chunk) => chunks.push(chunk));
    req.on('end', () => {
      let body = {};
      try {
        body = JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
      } catch {
        body = {};
      }
      const path = (req.url ?? '').split('?')[0];
      if (path.endsWith('/count_tokens')) {
        res.writeHead(200, { 'content-type': 'application/json' });
        res.end(JSON.stringify({ input_tokens: 10 }));
        return;
      }
      if (path.endsWith('/messages') || path.endsWith('/responses')) {
        n += 1;
        const wire = path.endsWith('/messages') ? 'messages' : 'responses';
        const items = wire === 'messages' ? body.messages ?? [] : Array.isArray(body.input) ? body.input : [];
        const kind = askedFor(items);
        calls.push({ at: Date.now(), wire, kind, input: items.map((item) => textOf(item.content ?? item.output)).filter(Boolean) });
        if (body.stream === false) {
          res.writeHead(200, { 'content-type': 'application/json' });
          res.end(JSON.stringify(wire === 'messages'
            ? { id: `msg_fake_${n}`, type: 'message', role: 'assistant', model: 'fake-model', content: [{ type: 'text', text: 'ok' }], stop_reason: 'end_turn', usage: { input_tokens: 1, output_tokens: 1 } }
            : { id: `resp_fake_${n}`, object: 'response', status: 'completed', output: [] }));
          return;
        }
        (wire === 'messages' ? anthropic : responses)(res, kind, n).catch(() => res.destroy());
        return;
      }
      res.writeHead(200, { 'content-type': 'application/json' });
      res.end('{}');
    });
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const { port } = server.address();
  return {
    url: `http://127.0.0.1:${port}`,
    calls,
    close: () => new Promise((resolve) => server.close(() => resolve())),
  };
}
