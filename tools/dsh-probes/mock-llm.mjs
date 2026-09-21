// A scripted OpenAI-compatible chat-completions server for the DSH1 probes.
//
// It is NOT a model. Each accepted request consumes the next step of a plan: either a tool call
// (the first advertised tool whose name matches a pattern, with the given arguments) or a final
// text. Every request body is logged verbatim-enough (model, tool roster, message tail) so the
// evidence note can say what the harness actually sent. No key is involved anywhere; the tier
// is "scripted", and the note says so (D24).
//
// usage: node mock-llm.mjs --port <n> --plan <plan.json> --log <requests.jsonl>

import { createServer } from 'node:http'
import { readFileSync, appendFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, token, i, all) => {
    if (token.startsWith('--')) acc.push([token.slice(2), all[i + 1]])
    return acc
  }, []),
)
const port = Number(args.port ?? 8765)
const plan = JSON.parse(readFileSync(args.plan, 'utf8'))
const logPath = args.log
if (logPath) mkdirSync(dirname(logPath), { recursive: true })

let cursor = 0
const log = (record) => {
  const line = JSON.stringify({ t: new Date().toISOString(), ...record })
  console.log(line)
  if (logPath) appendFileSync(logPath, line + '\n')
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = []
    req.on('data', (c) => chunks.push(c))
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    req.on('error', reject)
  })
}

function sse(res, payload) {
  res.write(`data: ${typeof payload === 'string' ? payload : JSON.stringify(payload)}\n\n`)
}

function chunk(model, delta, finish = null, extra = {}) {
  return {
    id: 'chatcmpl-mock',
    object: 'chat.completion.chunk',
    created: Math.floor(Date.now() / 1000),
    model,
    choices: [{ index: 0, delta, finish_reason: finish }],
    ...extra,
  }
}

function pickTool(tools, pattern) {
  const re = new RegExp(pattern)
  for (const t of tools ?? []) {
    const name = t?.function?.name ?? t?.name
    if (name && re.test(name)) return name
  }
  return undefined
}

const server = createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', 'http://mock.invalid')
  if (req.method === 'GET' && url.pathname.endsWith('/models')) {
    res.writeHead(200, { 'content-type': 'application/json' })
    res.end(JSON.stringify({ object: 'list', data: [{ id: 'mock-model', object: 'model' }] }))
    return
  }
  if (req.method === 'POST' && url.pathname.endsWith('/messages')) {
    // The official DeepSeek route speaks a Messages protocol. This server does not answer it; it
    // only records which request fields ride along (the session-log uploader is default-on).
    let body = {}
    try { body = JSON.parse(await readBody(req)) } catch {}
    const sizes = Object.fromEntries(Object.entries(body).map(([k, v]) => [k, Buffer.byteLength(JSON.stringify(v ?? null), 'utf8')]))
    log({ kind: 'messages-route', path: url.pathname, fieldSizes: sizes, sessionLogKeys: body.dsh_session_log ? Object.keys(body.dsh_session_log) : null, headers: { 'user-agent': req.headers['user-agent'], 'x-api-key': req.headers['x-api-key'] ? 'present' : 'absent', authorization: req.headers.authorization ? 'present' : 'absent' } })
    res.writeHead(500, { 'content-type': 'application/json' })
    res.end(JSON.stringify({ type: 'error', error: { type: 'api_error', message: 'mock: messages protocol not scripted' } }))
    return
  }
  if (req.method !== 'POST' || !url.pathname.endsWith('/chat/completions')) {
    log({ kind: 'unexpected', method: req.method, path: url.pathname })
    res.writeHead(404).end()
    return
  }
  let body
  try {
    body = JSON.parse(await readBody(req))
  } catch (e) {
    res.writeHead(400).end()
    return
  }
  const tools = (body.tools ?? []).map((t) => t?.function?.name ?? t?.name)
  if (tools.length === 0) {
    // An auxiliary request (session title, compaction summary): no tools are advertised. It is
    // answered with a fixed line and does NOT consume a plan step — the plan is for the agent loop.
    const model0 = body.model ?? 'mock-model'
    log({ kind: 'auxiliary', path: url.pathname, model: model0, messages: body.messages?.length, lastPreview: String(body.messages?.at(-1)?.content ?? '').slice(0, 160) })
    res.writeHead(200, { 'content-type': 'text/event-stream; charset=utf-8', 'cache-control': 'no-cache', connection: 'keep-alive' })
    sse(res, chunk(model0, { role: 'assistant', content: 'probe session' }))
    sse(res, chunk(model0, {}, 'stop', { usage: { prompt_tokens: 5, completion_tokens: 2 } }))
    sse(res, '[DONE]')
    res.end()
    return
  }
  const n = ++cursor
  const step = plan.steps[Math.min(n - 1, plan.steps.length - 1)]
  const last = body.messages?.at(-1)
  const lastPreview = typeof last?.content === 'string' ? last.content.slice(0, 400) : JSON.stringify(last?.content ?? null).slice(0, 400)
  const model = body.model ?? 'mock-model'
  // Everything the harness sends BESIDE the model input is a disclosure question: which extra
  // request fields ride along, and how big they are (the session-log uploader is default-on).
  const known = new Set(['model', 'messages', 'tools', 'stream', 'stream_options', 'max_tokens', 'temperature', 'tool_choice', 'thinking', 'reasoning_effort'])
  const extraFields = Object.fromEntries(
    Object.entries(body).filter(([k]) => !known.has(k)).map(([k, v]) => [k, Buffer.byteLength(JSON.stringify(v ?? null), 'utf8')]),
  )
  log({
    kind: 'request', n, path: url.pathname, model, stream: body.stream, messages: body.messages?.length,
    tools, lastRole: last?.role, lastToolCallId: last?.tool_call_id, lastPreview,
    system: body.messages?.[0]?.role === 'system' ? String(body.messages[0].content).length : 0,
    extraFields, headers: { authorization: req.headers.authorization ? 'present' : 'absent', 'user-agent': req.headers['user-agent'] },
    step: step.tool ? `tool:${step.tool.match}` : 'text',
  })

  res.writeHead(200, { 'content-type': 'text/event-stream; charset=utf-8', 'cache-control': 'no-cache', connection: 'keep-alive' })
  res.flushHeaders()

  if (step.tool) {
    const name = pickTool(body.tools, step.tool.match)
    if (!name) {
      const text = `mock: no advertised tool matches /${step.tool.match}/ — roster: ${tools.join(', ')}`
      log({ kind: 'no-tool-match', n, wanted: step.tool.match, roster: tools })
      sse(res, chunk(model, { role: 'assistant', content: text }))
      sse(res, chunk(model, {}, 'stop', { usage: { prompt_tokens: 10, completion_tokens: 10 } }))
      sse(res, '[DONE]')
      res.end()
      return
    }
    const argumentsJson = JSON.stringify(step.tool.args)
    log({ kind: 'tool-call', n, tool: name, arguments: argumentsJson })
    sse(res, chunk(model, { role: 'assistant', content: '' }))
    sse(res, chunk(model, {
      tool_calls: [{ index: 0, id: `call-${n}`, type: 'function', function: { name, arguments: argumentsJson } }],
    }))
    sse(res, chunk(model, {}, 'tool_calls', { usage: { prompt_tokens: 10, completion_tokens: 5 } }))
    sse(res, '[DONE]')
    res.end()
    return
  }

  const text = step.text ?? 'done'
  log({ kind: 'text', n, text })
  sse(res, chunk(model, { role: 'assistant', content: text }))
  sse(res, chunk(model, {}, 'stop', { usage: { prompt_tokens: 10, completion_tokens: 3 } }))
  sse(res, '[DONE]')
  res.end()
})

server.listen(port, '127.0.0.1', () => {
  log({ kind: 'ready', baseURL: `http://127.0.0.1:${port}/v1`, steps: plan.steps.length })
})
