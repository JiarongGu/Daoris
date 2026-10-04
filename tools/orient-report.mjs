#!/usr/bin/env node
/**
 * What a session spent finding its way before its first edit, read from its transcript (ORIENT1d).
 *
 * ## Why
 *
 * ORIENT1 was measured by a scratch script over ten branches of 2026-10-04: 60 to 100 calls and 370 to 600 KB read
 * before the first edit, about a third of each branch, 278 shell greps and 117 shell dumps between them. The
 * orientation index (`tools/orient-index.mjs`) is meant to halve both. This is that script made a tool, so the
 * same numbers are read the same way after as before.
 *
 * ## What it reads
 *
 * A transcript is the harness's JSONL: one object a line, a tool call in an assistant message and its result in
 * the next user message. A line that does not parse is skipped. The first edit is the first `Edit`, `MultiEdit`,
 * `Write` or `NotebookEdit` of a file inside the session's working directory and outside its `local/`: a plan
 * written to a scratch folder changes nothing of the work. Bytes are the UTF-8 size of each result's text.
 *
 * A shell command is classed by its words, first match wins, in the scratch script's order so the numbers compare:
 * a search (grep, rg, Select-String), a dump (sed -n, cat, head, tail, Get-Content), a listing (ls, find,
 * Get-ChildItem), a git read, a build or test, or other. Decision files are counted as one row among the files
 * read most, as the measurement counted them. A call of the workspace's knowledge server (`mcp__daoris-knowledge__*`,
 * ORIENT1c) is an ask, counted in all and before the first edit, so whether branches ask it is read here too.
 *
 *   node tools/orient-report.mjs [--json] <transcript.jsonl>…
 *
 * Exit codes: 0 reported · 2 a usage error or a transcript it could not read (the rest are still reported).
 */
import { readFileSync } from 'node:fs';
import { basename } from 'node:path';
import { isMain } from './fsx.mjs';

const EDITS = new Set(['Edit', 'MultiEdit', 'Write', 'NotebookEdit']);
const SHELLS = new Set(['Bash', 'PowerShell']);
const KINDS = ['search', 'dump', 'list', 'git', 'build', 'other'];
const INDEX = 'docs/index/';
/** The workspace's knowledge server's tools, as the harness names them (ORIENT1c). */
const SERVER = 'mcp__daoris-knowledge__';
const asksOf = (calls) => calls.filter((call) => call.name.startsWith(SERVER)).length;

const kb = (bytes) => Math.round(bytes / 1024);
const slash = (path) => String(path ?? '').replace(/\\/g, '/');

/** A shell command's kind, by its words, the first that matches. */
export function shellKind(command) {
  const c = String(command);
  if (/\bgrep\b|\brg\b|\bSelect-String\b|\bfindstr\b/i.test(c)) return 'search';
  // PowerShell's aliases count only as a command's first word: `type` and `dir` are common words in arguments.
  if (/\bsed -n|\bcat\b|\bhead\b|\btail\b|\bGet-Content\b|(?:^|[;&|]\s*)(?:type|gc)\s/i.test(c)) return 'dump';
  if (/\bls\b|\bfind\b|\bGet-ChildItem\b|(?:^|[;&|]\s*)(?:dir|gci)\b/i.test(c)) return 'list';
  if (/git (log|show|diff|status|grep|blame)/.test(c)) return 'git';
  if (/dotnet|npm|node --test|vitest/.test(c)) return 'build';
  return 'other';
}

/** The size of a tool result's text: a string, or the text parts of a list of blocks. */
function resultBytes(content) {
  if (typeof content === 'string') return Buffer.byteLength(content, 'utf8');
  if (!Array.isArray(content)) return content == null ? 0 : Buffer.byteLength(JSON.stringify(content), 'utf8');
  return content.reduce((sum, block) => sum + Buffer.byteLength(block?.type === 'text' ? String(block.text ?? '') : JSON.stringify(block ?? ''), 'utf8'), 0);
}

/** A transcript's working directory and its tool calls in order, each with the size of what came back. */
export function readTranscript(text) {
  let cwd = null;
  const calls = [];
  const byId = new Map();
  for (const line of text.split('\n')) {
    if (!line.trim()) continue;
    let entry;
    try {
      entry = JSON.parse(line);
    } catch {
      continue;
    }
    if (cwd === null && typeof entry?.cwd === 'string') cwd = slash(entry.cwd);
    const content = entry?.message?.content;
    if (!Array.isArray(content)) continue;
    for (const block of content) {
      if (block?.type === 'tool_use') {
        const call = { id: block.id, name: String(block.name), input: block.input ?? {}, bytes: 0 };
        calls.push(call);
        byId.set(block.id, call);
      } else if (block?.type === 'tool_result' && byId.has(block.tool_use_id)) {
        byId.get(block.tool_use_id).bytes += resultBytes(block.content);
      }
    }
  }
  return { cwd, calls };
}

/** A path as the repository names it: relative to the session's directory, or to a worktree's top, `/`-separated. */
function relative(path, cwd) {
  const at = slash(path);
  const top = cwd ? cwd.replace(/\/+$/, '') : null;
  if (top && at.toLowerCase().startsWith(`${top.toLowerCase()}/`)) return at.slice(top.length + 1);
  const worktree = /\/\.claude\/worktrees\/[^/]+\/(.*)$/.exec(at);
  return worktree ? worktree[1] : at;
}

/** Whether a call edits the work: a file inside the session's directory and outside its `local/`. */
function editsWork(call, cwd) {
  if (!EDITS.has(call.name)) return false;
  const path = slash(call.input.file_path ?? call.input.notebook_path);
  if (!cwd) return true;
  const inside = path.toLowerCase().startsWith(`${cwd.replace(/\/+$/, '').toLowerCase()}/`);
  return inside && !relative(path, cwd).startsWith('local/');
}

/** What a session spent before its first edit. */
export function summarize({ cwd, calls }, label) {
  const first = calls.findIndex((call) => editsWork(call, cwd));
  const before = first < 0 ? calls : calls.slice(0, first);
  const byTool = {};
  const bytesByTool = {};
  const shell = Object.fromEntries(KINDS.map((kind) => [kind, 0]));
  const reads = [];
  for (const call of before) {
    byTool[call.name] = (byTool[call.name] ?? 0) + 1;
    bytesByTool[call.name] = (bytesByTool[call.name] ?? 0) + call.bytes;
    if (SHELLS.has(call.name)) shell[shellKind(call.input.command ?? '')] += 1;
    if (call.name === 'Read') {
      const whole = call.input.offset === undefined && call.input.limit === undefined;
      reads.push({ path: relative(call.input.file_path, cwd), bytes: call.bytes, whole });
    }
  }
  return {
    label,
    total: calls.length,
    before: before.length,
    byTool,
    bytes: before.reduce((sum, call) => sum + call.bytes, 0),
    bytesByTool,
    shell,
    reads,
    indexReads: reads.filter((read) => read.path.startsWith(INDEX)).length,
    // Whether it asked the knowledge server, in all and before its first edit (ORIENT1c).
    asks: asksOf(calls),
    asksBefore: asksOf(before),
    firstEdit: first < 0 ? null : { name: calls[first].name, path: relative(calls[first].input.file_path ?? calls[first].input.notebook_path, cwd) },
  };
}

const median = (values) => {
  const sorted = [...values].sort((a, b) => a - b);
  if (sorted.length === 0) return 0;
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[middle] : Math.round((sorted[middle - 1] + sorted[middle]) / 2);
};

/** The report: a paragraph per branch, the totals, and the files read most before the first edit. */
export function report(summaries, { top = 20 } = {}) {
  const out = [];
  for (const s of summaries) {
    const share = s.total ? Math.round((100 * s.before) / s.total) : 0;
    const edit = s.firstEdit ? `first edit: ${s.firstEdit.name} ${s.firstEdit.path}` : 'no edit';
    const tools = Object.entries(s.byTool).sort((a, b) => b[1] - a[1] || (a[0] < b[0] ? -1 : 1))
      .map(([name, n]) => `${name} ${n} (${kb(s.bytesByTool[name])} KB)`);
    const whole = s.reads.filter((read) => read.whole).length;
    out.push(
      `${s.label}: ${s.total} calls, ${s.before} before the first edit (${share}%), ${kb(s.bytes)} KB read before it; ${edit}`,
      `  by tool: ${tools.join(', ') || 'none'}`,
      `  reads ${s.reads.length} (${whole} whole, ${s.reads.length - whole} ranged), ${s.indexReads} of ${INDEX}; shell: ${KINDS.map((kind) => `${kind} ${s.shell[kind]}`).join(', ')}`,
      `  knowledge server: ${s.asks ?? 0} asks, ${s.asksBefore ?? 0} before the first edit`,
    );
  }
  const sum = (pick) => summaries.reduce((total, s) => total + pick(s), 0);
  out.push('', `all ${summaries.length}: ${sum((s) => s.before)} calls before the first edit (median ${median(summaries.map((s) => s.before))}), `
    + `${kb(sum((s) => s.bytes))} KB read before it (median ${kb(median(summaries.map((s) => s.bytes)))} KB); `
    + `shell search ${sum((s) => s.shell.search)}, dump ${sum((s) => s.shell.dump)}`);
  out.push(`knowledge server asked by ${summaries.filter((s) => (s.asks ?? 0) > 0).length} of ${summaries.length} branches, `
    + `${sum((s) => s.asks ?? 0)} asks in all`);
  const files = new Map();
  for (const s of summaries) {
    for (const read of s.reads) {
      const path = read.path.replace(/(^|\/)docs\/decisions\/D\d+\.md$/, '$1docs/decisions/D<n>.md');
      const row = files.get(path) ?? { count: 0, bytes: 0 };
      row.count += 1;
      row.bytes += read.bytes;
      files.set(path, row);
    }
  }
  const most = [...files].sort((a, b) => b[1].count - a[1].count || b[1].bytes - a[1].bytes || (a[0] < b[0] ? -1 : 1)).slice(0, top);
  if (most.length) {
    out.push('', 'files read most before the first edit (reads, KB, path):');
    for (const [path, row] of most) out.push(`${String(row.count).padStart(5)} ${String(kb(row.bytes)).padStart(5)} KB  ${path}`);
  }
  return `${out.join('\n')}\n`;
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const json = args.includes('--json');
  const files = args.filter((arg) => arg !== '--json');
  if (files.length === 0 || files.some((file) => file.startsWith('--'))) {
    console.error('usage: node tools/orient-report.mjs [--json] <transcript.jsonl>…');
    process.exit(2);
  }
  const summaries = [];
  const unread = [];
  for (const file of files) {
    let text;
    try {
      text = readFileSync(file, 'utf8');
    } catch (error) {
      unread.push(`${file}: ${error.code ?? error.message}`);
      continue;
    }
    summaries.push(summarize(readTranscript(text), basename(file).replace(/\.(jsonl|output|json)$/, '')));
  }
  if (summaries.length) process.stdout.write(json ? `${JSON.stringify(summaries, null, 2)}\n` : report(summaries));
  if (unread.length) {
    console.error(`orient-report: could not read ${unread.length} transcript(s):\n${unread.map((line) => `  ${line}`).join('\n')}`);
    process.exit(2);
  }
}
