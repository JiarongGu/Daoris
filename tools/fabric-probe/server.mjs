/**
 * The fabric probe's stand-in MCP server (FABRIC-PROBE1; the C0 audit §2, `docs/2026-10-16-fabric-contract-audit.md`).
 *
 *   node tools/fabric-probe/server.mjs --log <file>
 *
 * A harness starts it from its MCP entry, over stdio, one JSON-RPC message a line. It offers four tools: `probe_read`
 * (a launcher's rules allow it), `probe_write` (denied), `probe_unlisted` (named by no rule) and `probe_wait` (it holds
 * a call open, for a kill to land on). Each answers a token no model would invent, `ochre-lattice-5287-<nonce>`, with a
 * nonce per process, so a reply that quotes it was read off this process.
 *
 * 🔴 Every message it receives, its own start, its input's end and its exit are appended to the log SYNCHRONOUSLY, one
 * JSON object a line, before anything answers: a process killed a moment later loses nothing it had already received.
 * The log is the evidence; what a model says it called is not.
 */
import { appendFileSync, mkdirSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import { dirname } from 'node:path';
import { isMain } from '../fsx.mjs';

/** The token's fixed part: the audit's, so a receipt reads the same either side of the rebuild. */
export const TOKEN = 'ochre-lattice-5287';

/** The variables a start line names, by value: who the process was started as, never where. */
export const NAMED = ['DAORIS_SESSION_ID', 'DAORIS_ASK_ID'];

/** The tools, as `tools/list` answers them. */
export const TOOLS = [
  { name: 'probe_read', description: 'Answer the probe token. Call this when asked to read the probe.' },
  { name: 'probe_write', description: 'Write through the probe. Its rules deny it.' },
  { name: 'probe_unlisted', description: 'A probe tool that no rule names.' },
  { name: 'probe_wait', description: 'Hold the call open for `seconds` (default 60), then answer the probe token.' },
].map((tool) => ({
  ...tool,
  inputSchema: {
    type: 'object',
    properties: {
      note: { type: 'string', description: 'Who is calling: root, child, grandchild, or a sibling’s name.' },
      ...(tool.name === 'probe_wait' ? { seconds: { type: 'number' } } : {}),
    },
  },
}));

/**
 * One line of the log, appended at once. A log that cannot be written is said on stderr, which is the harness's, and
 * the server goes on: stdout is the protocol and must stay clean.
 */
export function logLine(file, entry) {
  try {
    mkdirSync(dirname(file), { recursive: true });
    appendFileSync(file, `${JSON.stringify({ at: new Date().toISOString(), pid: process.pid, ...entry })}\n`);
  } catch (error) {
    process.stderr.write(`fabric-probe server: the log would not take a line: ${error.message}\n`);
  }
}

/**
 * The answer to one message, or null for a notification. `nonce` is this process's; `hold` waits `seconds` and is the
 * runner's, so a test answers at once.
 */
export async function answer(message, nonce, hold = (seconds) => new Promise((done) => setTimeout(done, seconds * 1000))) {
  if (message === null || typeof message !== 'object' || message.id === undefined) return null;
  const reply = (result) => ({ jsonrpc: '2.0', id: message.id, result });
  switch (message.method) {
    case 'initialize':
      return reply({
        protocolVersion: message.params?.protocolVersion ?? '2025-06-18',
        capabilities: { tools: {} },
        serverInfo: { name: 'fabric-probe', version: '1.0.0' },
      });
    case 'ping':
      return reply({});
    case 'tools/list':
      return reply({ tools: TOOLS });
    case 'tools/call': {
      const name = message.params?.name;
      if (!TOOLS.some((tool) => tool.name === name)) {
        return { jsonrpc: '2.0', id: message.id, error: { code: -32602, message: `no tool ${name}` } };
      }
      if (name === 'probe_wait') {
        const seconds = Number(message.params?.arguments?.seconds);
        await hold(Number.isFinite(seconds) && seconds >= 0 ? seconds : 60);
      }
      return reply({ content: [{ type: 'text', text: `${TOKEN}-${nonce} (${name})` }] });
    }
    default:
      return { jsonrpc: '2.0', id: message.id, error: { code: -32601, message: `method not found: ${message.method}` } };
  }
}

/** The server over this process's stdio, logging to `log`. */
export function serve(log, { input = process.stdin, output = process.stdout, env = process.env } = {}) {
  const nonce = randomBytes(3).toString('hex');
  const named = Object.fromEntries(NAMED.filter((name) => env[name] !== undefined).map((name) => [name, env[name]]));
  logLine(log, { event: 'start', ppid: process.ppid, nonce, env: named });
  process.on('exit', (code) => logLine(log, { event: 'exit', code }));

  let buffered = '';
  input.setEncoding('utf8');
  input.on('data', (chunk) => {
    buffered += chunk;
    let end;
    while ((end = buffered.indexOf('\n')) >= 0) {
      const line = buffered.slice(0, end).trim();
      buffered = buffered.slice(end + 1);
      if (!line) continue;
      let message;
      try {
        message = JSON.parse(line);
      } catch {
        logLine(log, { event: 'unreadable', line });
        continue;
      }
      logLine(log, { event: 'in', message });
      answer(message, nonce).then((reply) => {
        if (reply === null) return;
        logLine(log, { event: 'out', message: reply });
        output.write(`${JSON.stringify(reply)}\n`);
      });
    }
  });
  input.on('end', () => {
    logLine(log, { event: 'input-end' });
    process.exit(0);
  });
}

if (isMain(import.meta.url)) {
  const at = process.argv.indexOf('--log');
  const log = at > 0 ? process.argv[at + 1] : process.env.FABRIC_PROBE_LOG;
  if (!log) {
    process.stderr.write('usage: node tools/fabric-probe/server.mjs --log <file>\n');
    process.exit(2);
  }
  serve(log);
}
