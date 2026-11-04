#!/usr/bin/env node
/**
 * Where each dispatched subagent's hours went, read from the harness's own transcripts (CONSOLIDATE1).
 *
 *   node tools/subagent-time.mjs <transcripts-dir> [--since YYYY-MM-DDTHH:MM] [--type branch-worker] [--json <file>]
 *
 * `<transcripts-dir>` is the folder holding this repository's sessions, each subagent's transcript under
 * `<session>/subagents/`; the path is the machine's, so it is passed in and never written here.
 *
 * A tool call's time runs from the assistant message that asks it to the result that answers it; the model's time
 * runs from a result (or the prompt) to the next assistant message. A command is classed by what it runs, a read and
 * an edit by the path they touch, so the report says how much of a builder's day went to code, to the records around
 * it, and to builds, tests and waits. `subagent-usage.mjs` reports the tokens; this reports the clock.
 */
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { isMain } from './fsx.mjs';

/** The class of a path a read or an edit touches. */
export function pathClass(path = '') {
  const p = path.replaceAll('\\', '/');
  if (/\/docs\/index\//.test(p) || /docs\/index\//.test(p)) return 'generated';
  if (/(^|\/)(fixtures|golden)\//.test(p) || /\.(golden|account)\.json$/.test(p)) return 'fixtures';
  if (/(Tests?\/|\.test\.|\.spec\.|tool-test\/|\/test\/|e2e\/|measured\/)/.test(p)) return 'tests';
  if (/(^|\/)(TASKS\.md|CHANGELOG\.md)$|\/docs\/|\.claude\/|AGENTS\.md|README\.md$|\.md$/.test(p)) return 'records';
  if (/(^|\/)(src|tools|canon|examples)\//.test(p)) return 'code';
  return 'other';
}

/** The class of a shell command, by the first thing it runs that the report tells apart. */
export function commandClass(command = '') {
  const c = command.replace(/\s+/g, ' ');
  if (/\b(sleep|Start-Sleep|Wait-Process)\b/.test(c)) return 'wait';
  if (/\bnpm run verify\b|check:web-rules|i18n:check|names:check|merge-branch\.mjs|\bdaoris\.mjs check\b|doc-(shapes|duplicates|links|system)|optional-locks|test-map\.mjs|orient-index\.mjs --check/.test(c)) return 'verify';
  if (/\bdotnet test\b|\bvitest\b|\bnode --test\b|\bplaywright\b|\bnpm (run )?test\b|\bnpm run (test|e2e)/.test(c)) return 'test';
  if (/\bdotnet build\b|\btsc\b|\bnpm run (build|typecheck)\b|\bnpm (ci|install)\b|build-server/.test(c)) return 'build';
  if (/^\s*(cd [^&;]+(&&|;)\s*)?git\b|\bgit (merge|diff|log|status|show|add|commit|checkout|worktree|rev-parse|branch)\b/.test(c)) return 'git';
  if (/\b(grep|rg|find|Select-String|Get-ChildItem)\b/.test(c)) return 'search';
  if (/\b(cat|head|tail|sed -n|Get-Content|type)\b/.test(c)) return 'read';
  if (/\bnode tools\//.test(c)) return 'tools';
  return 'other';
}

/** One tool call's bucket: the activity the report sums it under. */
export function bucketOf(name, input = {}) {
  switch (name) {
    case 'Read': return `read:${pathClass(input.file_path)}`;
    case 'Edit': case 'Write': case 'NotebookEdit': return `edit:${pathClass(input.file_path)}`;
    case 'Grep': case 'Glob': return 'search';
    case 'Bash': case 'PowerShell': return `run:${commandClass(input.command)}`;
    case 'Monitor': case 'TaskOutput': case 'Wait': return 'run:wait';
    case 'Skill': case 'ToolSearch': return 'setup';
    default: return `tool:${name}`;
  }
}

/** The clock of one transcript's lines: total, the model's own, and each bucket's, in milliseconds, with call counts. */
export function clockOf(lines) {
  const asked = new Map();
  const buckets = {};
  const calls = {};
  let model = 0;
  let first = null;
  let last = null;
  let ready = null;
  for (const line of lines) {
    const at = line.timestamp ? Date.parse(line.timestamp) : NaN;
    if (Number.isNaN(at)) continue;
    first ??= at;
    last = at;
    const content = line.message?.content;
    if (line.type === 'assistant') {
      if (ready !== null && at >= ready) model += at - ready;
      ready = null;
      for (const part of Array.isArray(content) ? content : []) {
        if (part.type === 'tool_use') asked.set(part.id, { at, bucket: bucketOf(part.name, part.input) });
      }
    } else if (line.type === 'user') {
      let answered = false;
      for (const part of Array.isArray(content) ? content : []) {
        if (part.type !== 'tool_result') continue;
        const call = asked.get(part.tool_use_id);
        if (!call) continue;
        buckets[call.bucket] = (buckets[call.bucket] ?? 0) + Math.max(0, at - call.at);
        calls[call.bucket] = (calls[call.bucket] ?? 0) + 1;
        asked.delete(part.tool_use_id);
        answered = true;
      }
      if (answered || typeof content === 'string') ready = at;
    }
  }
  return { total: first === null ? 0 : last - first, model, buckets, calls, start: first };
}

function transcripts(root) {
  const found = [];
  for (const session of readdirSync(root, { withFileTypes: true })) {
    if (!session.isDirectory()) continue;
    const dir = join(root, session.name, 'subagents');
    if (!existsSync(dir)) continue;
    for (const file of readdirSync(dir)) {
      if (!file.endsWith('.jsonl')) continue;
      const metaFile = join(dir, file.replace(/\.jsonl$/, '.meta.json'));
      const meta = existsSync(metaFile) ? JSON.parse(readFileSync(metaFile, 'utf8')) : {};
      found.push({ file: join(dir, file), meta });
    }
  }
  return found;
}

const hours = (ms) => (ms / 3600000).toFixed(1);
const share = (ms, of) => (of ? `${Math.round((100 * ms) / of)}%` : '-');

function main() {
  const args = process.argv.slice(2);
  const root = args.find((a) => !a.startsWith('--'));
  const flag = (name) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
  if (!root || !existsSync(root)) {
    console.error('subagent-time: name the folder of the sessions transcripts');
    return 2;
  }
  const since = flag('--since') ? Date.parse(flag('--since')) : 0;
  const type = flag('--type');
  const rows = [];
  for (const { file, meta } of transcripts(root)) {
    if (type && meta.agentType !== type) continue;
    const lines = readFileSync(file, 'utf8').split('\n').filter(Boolean).map((l) => { try { return JSON.parse(l); } catch { return {}; } });
    const clock = clockOf(lines);
    if (!clock.start || clock.start < since || clock.total === 0) continue;
    rows.push({ agent: meta.description ?? file, type: meta.agentType, model: meta.model, ...clock });
  }
  if (flag('--json')) writeFileSync(flag('--json'), JSON.stringify(rows, null, 2));
  const total = rows.reduce((s, r) => s + r.total, 0);
  const sum = {};
  let model = 0;
  for (const r of rows) {
    model += r.model;
    for (const [b, ms] of Object.entries(r.buckets)) sum[b] = (sum[b] ?? 0) + ms;
  }
  console.log(`${rows.length} subagents, ${hours(total)} agent-hours`);
  console.log(`  the model thinking and writing: ${hours(model)} h (${share(model, total)})`);
  const counted = model + Object.values(sum).reduce((s, ms) => s + ms, 0);
  console.log(`  between calls (stalls, limits, API errors, waits outside a tool): ${hours(Math.max(0, total - counted))} h (${share(Math.max(0, total - counted), total)})`);
  for (const [b, ms] of Object.entries(sum).sort(([, x], [, y]) => y - x)) console.log(`  ${b.padEnd(18)} ${hours(ms).padStart(6)} h  ${share(ms, total).padStart(4)}`);
  console.log('\nthe longest, by agent:');
  for (const r of [...rows].sort((x, y) => y.total - x.total).slice(0, 12)) {
    const top = Object.entries(r.buckets).sort(([, x], [, y]) => y - x).slice(0, 3).map(([b, ms]) => `${b} ${share(ms, r.total)}`).join(', ');
    console.log(`  ${hours(r.total).padStart(5)} h  ${String(r.agent).slice(0, 52).padEnd(52)}  model ${share(r.model, r.total)}; ${top}`);
  }
  return 0;
}

if (isMain(import.meta.url)) process.exitCode = main();
