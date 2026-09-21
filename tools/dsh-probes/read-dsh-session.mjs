// Decode a dsh session log (session.vN.jsonl[.zstd]) and print one line per event: seq, type, and a
// short payload summary. dsh appends one zstd FRAME per flush, so the file is a sequence of frames;
// Node's zstdDecompressSync stops at the first, so frames are split on the magic number first.
// usage: node read-session.mjs <path-to-session-file> [--full]
import { readFileSync } from 'node:fs'
import { zstdDecompressSync } from 'node:zlib'

const [file, flag] = process.argv.slice(2)
const full = flag === '--full'
let bytes = readFileSync(file)
if (file.endsWith('.zstd')) {
  const MAGIC = Buffer.from([0x28, 0xb5, 0x2f, 0xfd])
  const starts = []
  let at = bytes.indexOf(MAGIC)
  while (at >= 0) { starts.push(at); at = bytes.indexOf(MAGIC, at + 4) }
  const parts = starts.map((s, i) => zstdDecompressSync(bytes.subarray(s, starts[i + 1] ?? bytes.length)))
  console.log(`# ${starts.length} zstd frames`)
  bytes = Buffer.concat(parts)
}
const lines = bytes.toString('utf8').split('\n').filter((l) => l.trim())
console.log(`# ${lines.length} lines`)
for (const line of lines) {
  let e
  try { e = JSON.parse(line) } catch { console.log('non-json:', line.slice(0, 200)); continue }
  if (full) { console.log(JSON.stringify(e)); continue }
  const type = e.type ?? '?'
  const d = e.data ?? e
  let summary = ''
  switch (type) {
    case 'session': summary = JSON.stringify(e).slice(0, 240); break
    case 'user/message': summary = JSON.stringify(d.message?.content ?? d).slice(0, 200); break
    case 'system/message': summary = `len=${JSON.stringify(d).length}`; break
    case 'assistant/message': summary = JSON.stringify(d.message?.content?.map((b) => b.type === 'text' ? `text:${b.text}` : `${b.type}:${b.name ?? b.toolName ?? ''}`)).slice(0, 220); break
    case 'tool/call': summary = `${d.name} ${String(d.arguments).slice(0, 160)}`; break
    case 'tool/result': summary = JSON.stringify(d.message?.content?.[0]?.content?.map((b) => b.text ?? b.type)).slice(0, 240) + (d.message?.content?.[0]?.isError ? ' [isError]' : ''); break
    case 'turn/end': summary = JSON.stringify(d.reason); break
    default: summary = JSON.stringify(d).slice(0, 160)
  }
  console.log(`${String(e.seq ?? '').padStart(4)} ${type.padEnd(40)} ${(e.surfaceOp ?? '').padEnd(8)} ${summary}`)
}
