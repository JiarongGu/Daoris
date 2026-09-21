// A minimal Agent Client Protocol (ACP) client for the DSH1 probes: JSON-RPC 2.0, newline-delimited,
// over the agent's stdio. It drives one session end to end and logs EVERY frame in both directions
// verbatim to a JSONL file, because the frames are the evidence — what the driver and the timeline
// would see is exactly this, nothing more.
//
// usage: node client.mjs --cmd <exe> [--args <json array>] --cwd <abs dir> --prompt <text>
//                        --log <frames.jsonl> [--permission allow|reject] [--timeout <ms>]

import { spawn } from 'node:child_process'
import { appendFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'

const args = Object.fromEntries(
  process.argv.slice(2).reduce((acc, token, i, all) => {
    if (token.startsWith('--')) acc.push([token.slice(2), all[i + 1] === undefined || all[i + 1].startsWith('--') ? '' : all[i + 1]])
    return acc
  }, []),
)
const cmd = args.cmd
// `|`-separated rather than JSON: Windows PowerShell strips embedded double quotes on the way into a
// native process, which ate a JSON array on the first run.
const cmdArgs = args.args ? args.args.split('|') : []
const cwd = args.cwd
const prompt = args.prompt ?? 'say hello'
const permission = args.permission ?? 'reject'
const timeoutMs = Number(args.timeout ?? 120000)
const logPath = args.log
if (logPath) mkdirSync(dirname(logPath), { recursive: true })

const started = Date.now()
const log = (record) => {
  const line = JSON.stringify({ ms: Date.now() - started, ...record })
  if (logPath) appendFileSync(logPath, line + '\n')
  return line
}
const say = (text) => console.log(`[${String(Date.now() - started).padStart(6)}ms] ${text}`)

const child = spawn(cmd, cmdArgs, { cwd, stdio: ['pipe', 'pipe', 'pipe'], env: process.env, shell: false })
log({ dir: 'meta', event: 'spawned', cmd, args: cmdArgs, pid: child.pid })

let nextId = 1
const pending = new Map()
function send(method, params) {
  const id = nextId++
  const frame = { jsonrpc: '2.0', id, method, params }
  log({ dir: 'out', frame })
  child.stdin.write(JSON.stringify(frame) + '\n')
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject, method }))
}
function notify(method, params) {
  const frame = { jsonrpc: '2.0', method, params }
  log({ dir: 'out', frame })
  child.stdin.write(JSON.stringify(frame) + '\n')
}
function respond(id, result) {
  const frame = { jsonrpc: '2.0', id, result }
  log({ dir: 'out', frame })
  child.stdin.write(JSON.stringify(frame) + '\n')
}
function respondError(id, code, message) {
  const frame = { jsonrpc: '2.0', id, error: { code, message } }
  log({ dir: 'out', frame })
  child.stdin.write(JSON.stringify(frame) + '\n')
}

const updates = []
let buffer = ''
child.stdout.on('data', (data) => {
  buffer += data.toString('utf8')
  let idx
  while ((idx = buffer.indexOf('\n')) >= 0) {
    const line = buffer.slice(0, idx).trim()
    buffer = buffer.slice(idx + 1)
    if (!line) continue
    let frame
    try {
      frame = JSON.parse(line)
    } catch {
      log({ dir: 'in', nonJson: line })
      say(`stdout non-JSON: ${line.slice(0, 200)}`)
      continue
    }
    log({ dir: 'in', frame })
    if (frame.id !== undefined && frame.method === undefined) {
      const p = pending.get(frame.id)
      if (p) {
        pending.delete(frame.id)
        if (frame.error) p.reject(new Error(`${p.method}: ${JSON.stringify(frame.error)}`))
        else p.resolve(frame.result)
      }
      continue
    }
    if (frame.method === 'session/update') {
      const u = frame.params?.update
      updates.push(u)
      const kind = u?.sessionUpdate
      const detail = kind === 'tool_call' ? `${u.title} ${JSON.stringify(u.rawInput ?? '').slice(0, 120)}`
        : kind === 'tool_call_update' ? `${u.toolCallId} -> ${u.status}`
        : kind === 'agent_message_chunk' || kind === 'agent_thought_chunk' ? JSON.stringify(u.content?.text ?? u.content).slice(0, 120)
        : JSON.stringify(u).slice(0, 160)
      say(`update ${kind}: ${detail}`)
      continue
    }
    if (frame.method === 'session/request_permission') {
      const options = frame.params?.options ?? []
      say(`PERMISSION requested for ${JSON.stringify(frame.params?.toolCall).slice(0, 200)} options=${options.map((o) => `${o.optionId}:${o.kind}`).join(',')}`)
      const wanted = permission === 'allow' ? ['allow_once', 'allow_always'] : ['reject_once', 'reject_always']
      const chosen = options.find((o) => wanted.includes(o.kind)) ?? options[0]
      if (chosen) respond(frame.id, { outcome: { outcome: 'selected', optionId: chosen.optionId } })
      else respond(frame.id, { outcome: { outcome: 'cancelled' } })
      continue
    }
    if (frame.id !== undefined && frame.method) {
      say(`unhandled agent request ${frame.method}`)
      respondError(frame.id, -32601, `client does not implement ${frame.method}`)
      continue
    }
    say(`notification ${frame.method}`)
  }
})
child.stderr.on('data', (data) => {
  for (const line of data.toString('utf8').split(/\r?\n/)) {
    if (!line.trim()) continue
    log({ dir: 'err', line })
    say(`stderr: ${line.slice(0, 200)}`)
  }
})
child.on('exit', (code, signal) => {
  log({ dir: 'meta', event: 'exit', code, signal })
  say(`child exited code=${code} signal=${signal}`)
})

const watchdog = setTimeout(() => {
  say('TIMEOUT — cancelling and killing')
  log({ dir: 'meta', event: 'timeout' })
  try { child.kill() } catch {}
  process.exit(3)
}, timeoutMs)

try {
  const init = await send('initialize', {
    protocolVersion: 1,
    clientCapabilities: { fs: { readTextFile: false, writeTextFile: false }, terminal: false },
    clientInfo: { name: 'daoris-dsh1-probe', version: '0.0.0' },
  })
  say(`initialize -> protocolVersion=${init.protocolVersion} agentCapabilities=${JSON.stringify(init.agentCapabilities).slice(0, 300)}`)

  say(`initialize authMethods=${JSON.stringify(init.authMethods ?? null).slice(0, 300)} agentInfo=${JSON.stringify(init.agentInfo ?? null)}`)

  const session = await send('session/new', { cwd, mcpServers: [] })
  say(`session/new -> ${session.sessionId} models=${JSON.stringify(session.models ?? session.configOptions ?? null).slice(0, 300)}`)

  let result = { stopReason: 'not-prompted' }
  if (args['no-prompt'] === undefined) {
    const t0 = Date.now()
    result = await send('session/prompt', { sessionId: session.sessionId, prompt: [{ type: 'text', text: prompt }] })
    say(`session/prompt -> stopReason=${result.stopReason} after ${Date.now() - t0}ms, ${updates.length} updates`)
  } else {
    say('no prompt sent (--no-prompt): handshake and session creation only')
  }

  try {
    const list = await send('session/list', {})
    say(`session/list -> ${JSON.stringify(list).slice(0, 300)}`)
  } catch (e) {
    say(`session/list failed: ${e.message.slice(0, 200)}`)
  }

  try {
    const closed = await send('session/close', { sessionId: session.sessionId })
    say(`session/close -> ${JSON.stringify(closed).slice(0, 200)}`)
  } catch (e) {
    say(`session/close failed: ${e.message.slice(0, 200)}`)
  }

  const summary = {}
  for (const u of updates) summary[u?.sessionUpdate ?? 'unknown'] = (summary[u?.sessionUpdate ?? 'unknown'] ?? 0) + 1
  say(`update vocabulary seen: ${JSON.stringify(summary)}`)
  log({ dir: 'meta', event: 'summary', stopReason: result.stopReason, updates: summary })
} catch (e) {
  say(`ERROR ${e.message}`)
  log({ dir: 'meta', event: 'error', message: e.message })
} finally {
  clearTimeout(watchdog)
  child.stdin.end()
  setTimeout(() => { try { child.kill() } catch {} ; process.exit(0) }, 8000)
}
