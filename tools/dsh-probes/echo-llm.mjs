// An echo provider for instruction-file probes: it answers every request by quoting back the lines that
// begin `PROBE-LINE` from whatever the harness sent, or `NONE` when the request holds none.
//
// It is NOT a model. The answer is read off the request, so a quoted line proves the harness put that file's
// text in front of the model, and the log says where in the request it sat. Both wire formats a harness here
// speaks are served: chat completions (dsh's custom route) and Responses (codex). No key is involved; the
// tier is "scripted echo", and an evidence note says so.
//
// usage: node echo-llm.mjs --port <n> --log <requests.jsonl>

import { createServer } from 'node:http'
import { appendFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, token, i, all) => {
    if (token.startsWith('--')) acc.push([token.slice(2), all[i + 1]])
    return acc
  }, []),
)
const port = Number(args.port ?? 8766)
const logPath = args.log
if (logPath) mkdirSync(dirname(logPath), { recursive: true })

const log = (record) => {
  const line = JSON.stringify({ t: new Date().toISOString(), ...record })
  console.log(line)
  if (logPath) appendFileSync(logPath, line + '\n')
}

const readBody = (req) => new Promise((resolve, reject) => {
  const chunks = []
  req.on('data', (c) => chunks.push(c))
  req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
  req.on('error', reject)
})

/** Every `PROBE-LINE …` line in the request, with the path of the field that held it. */
export function probeLines(body) {
  const found = []
  const walk = (value, path) => {
    if (typeof value === 'string') {
      for (const m of value.matchAll(/^.*PROBE-LINE:.*$/gm)) found.push({ path, line: m[0].trim() })
    } else if (Array.isArray(value)) value.forEach((v, i) => walk(v, `${path}[${i}]`))
    else if (value && typeof value === 'object') for (const [k, v] of Object.entries(value)) walk(v, path ? `${path}.${k}` : k)
  }
  walk(body, '')
  return found
}

const answerOf = (found) => (found.length ? [...new Set(found.map((f) => f.line))].join('\n') : 'NONE')
const sse = (res, event, payload) => res.write(`${event ? `event: ${event}\n` : ''}data: ${typeof payload === 'string' ? payload : JSON.stringify(payload)}\n\n`)
const headers = { 'content-type': 'text/event-stream; charset=utf-8', 'cache-control': 'no-cache', connection: 'keep-alive' }

const server = createServer(async (req, res) => {
  const url = new URL(req.url ?? '/', 'http://echo.invalid')
  if (req.method === 'GET' && url.pathname.endsWith('/models')) {
    res.writeHead(200, { 'content-type': 'application/json' })
    res.end(JSON.stringify({ object: 'list', data: [{ id: 'echo-model', object: 'model' }], models: [] }))
    return
  }
  if (req.method !== 'POST') {
    log({ kind: 'unexpected', method: req.method, path: url.pathname })
    res.writeHead(404).end()
    return
  }
  let body
  try { body = JSON.parse(await readBody(req)) } catch { res.writeHead(400).end(); return }
  const found = probeLines(body)
  const text = answerOf(found)
  const model = body.model ?? 'echo-model'
  log({ kind: 'request', path: url.pathname, model, found, answered: text })

  if (url.pathname.endsWith('/responses')) {
    res.writeHead(200, headers)
    sse(res, 'response.created', { type: 'response.created', response: { id: 'resp-echo' } })
    sse(res, 'response.output_item.done', {
      type: 'response.output_item.done',
      item: { type: 'message', role: 'assistant', id: 'msg-echo', content: [{ type: 'output_text', text }] },
    })
    sse(res, 'response.completed', {
      type: 'response.completed',
      response: { id: 'resp-echo', usage: { input_tokens: 1, input_tokens_details: null, output_tokens: 1, output_tokens_details: null, total_tokens: 2 } },
    })
    res.end()
    return
  }
  if (url.pathname.endsWith('/chat/completions')) {
    const chunk = (delta, finish = null, extra = {}) => ({ id: 'chatcmpl-echo', object: 'chat.completion.chunk', created: Math.floor(Date.now() / 1000), model, choices: [{ index: 0, delta, finish_reason: finish }], ...extra })
    res.writeHead(200, headers)
    sse(res, null, chunk({ role: 'assistant', content: text }))
    sse(res, null, chunk({}, 'stop', { usage: { prompt_tokens: 1, completion_tokens: 1 } }))
    sse(res, null, '[DONE]')
    res.end()
    return
  }
  log({ kind: 'unexpected', method: req.method, path: url.pathname })
  res.writeHead(404).end()
})

server.listen(port, '127.0.0.1', () => log({ kind: 'ready', baseURL: `http://127.0.0.1:${port}/v1` }))
