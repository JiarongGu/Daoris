#!/usr/bin/env node
/**
 * What each dispatched subagent's context cost, read from the harness's own transcripts (D160).
 *
 *   node tools/subagent-usage.mjs <transcripts-dir> [--since YYYY-MM-DD] [--json <file>]
 *
 * `<transcripts-dir>` is the folder where the harness keeps this repository's sessions, one folder per session, with
 * each subagent's transcript under `<session>/subagents/`. The path is the machine's, so it is passed in and never
 * written here.
 *
 * ## Why
 *
 * Every turn re-reads the whole context, so a branch's cost is its context summed over its turns. Before D160 the
 * median branch took 57 turns and grew 200K tokens orienting before its first edit, then carried that load through
 * every turn after (`docs/2026-10-10-subagent-load-evidence.md`). This reports the numbers the change is judged by:
 * the context at the first turn (the fixed startup), at the first edit of the work (the orientation load), and at
 * the end, by agent type and model, so a scout-and-worker run can be compared with the runs before it.
 *
 * `weighted` counts each kind of token at its ratio to an uncached input token (a cache write 1.25, a cache read 0.1,
 * an output 5). It compares runs on one model. It is not a price, and runs on two models are compared by their
 * columns, not by it (D57 claims no price).
 */
import { existsSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { isMain } from './fsx.mjs';

export const RATIO = Object.freeze({ input: 1, cacheWrite: 1.25, cacheRead: 0.1, output: 5 });

// A write under scratch is a probe, not the work: the load is measured at the first edit of a tracked file.
const SCRATCH = /(^|[\\/])(local|_fixtures)[\\/]/;

// A file written through the shell instead of the edit tools (file-tool-discipline): an in-place stream edit, or a
// program's own write. A worker that edited this way showed no first edit at all (D160's SUBLOAD1c note). A search
// for those words, and a write into scratch or temp, edit no work, so the count is an estimate that errs low.
// A redirect into a source or record file counts too (`cat >> A.test.tsx <<EOF` appended a test, `>> FIX-LOG.md` an
// entry; D160's second SUBLOAD1c note). A shell redirect stands after a space, so `=>x.id` is no redirect; `2>&1` and
// `> /dev/null` write no file, and a log written beside the work is no edit of it.
const SHELL_EDIT = /\bsed\s+(-\w*\s+)*-i|\bperl\s+-\w*i|\.write\(|writeFileSync|\b(Set|Add)-Content\b|\bOut-File\b|\s>>?\s*[\w./\\-]+\.(ts|tsx|js|mjs|cjs|cs|json|md|css|html|ya?ml)\b/;
const NOT_WORK = /local[\\/]scratch|_fixtures|[\\/]tmp\b|\$TMP|\$env:TEMP|[\\/]Temp[\\/]/i;
const SEARCH = /^\s*(cd\s+\S+\s*(&&|;)\s*)?(grep|rg|git\s+grep|Select-String)\b/;

const context = u => (u.input_tokens ?? 0) + (u.cache_creation_input_tokens ?? 0) + (u.cache_read_input_tokens ?? 0);

const isEdit = block => (block.name === 'Edit' || block.name === 'Write') && !SCRATCH.test(String(block.input?.file_path ?? ''));
const isShellEdit = block => {
  if (block.name !== 'Bash' && block.name !== 'PowerShell') return false;
  const command = String(block.input?.command ?? '');
  return SHELL_EDIT.test(command) && !NOT_WORK.test(command) && !SEARCH.test(command);
};

/** One transcript's text (JSON lines) to its turns, its contexts and its token totals. */
export function readTranscript(text) {
  const usage = new Map();
  const order = [];
  let model = '';
  let firstEdit = -1;
  const shellEdits = new Set();
  for (const line of text.split('\n')) {
    if (!line.includes('"assistant"')) continue;
    let entry;
    try { entry = JSON.parse(line); } catch { continue; }
    const message = entry.message;
    if (entry.type !== 'assistant' || !message?.usage || !message.id) continue;
    // The harness writes a message of its own when a run stops (a usage limit): no model read it, so it is no turn.
    if (message.model === '<synthetic>') continue;
    // A streamed message is written once per block, each line repeating its usage: one turn, counted once.
    if (!usage.has(message.id)) order.push(message.id);
    usage.set(message.id, message.usage);
    model = message.model || model;
    for (const block of Array.isArray(message.content) ? message.content : []) {
      if (block.type !== 'tool_use') continue;
      const shell = isShellEdit(block);
      // A streamed message repeats its blocks: a shell edit is counted once, by its id.
      if (shell) shellEdits.add(block.id);
      if (firstEdit < 0 && (shell || isEdit(block))) firstEdit = order.length - 1;
    }
  }
  const turns = order.map(id => usage.get(id));
  const sum = key => turns.reduce((n, u) => n + (u[key] ?? 0), 0);
  const edit = firstEdit < 0 ? turns.length - 1 : firstEdit;
  // Each turn's cache read, split by what it re-read: the first turn's context (the startup), what orienting added
  // by the first edit (read while orienting, then carried), and what the work added after it.
  const startup = turns.length ? context(turns[0]) : 0;
  const load = turns.length ? context(turns[edit]) - startup : 0;
  const reads = { startup: 0, orientation: 0, carried: 0, work: 0 };
  turns.forEach((u, i) => {
    const read = u.cache_read_input_tokens ?? 0;
    const fixed = Math.min(read, startup);
    reads.startup += fixed;
    if (i <= edit) { reads.orientation += read - fixed; return; }
    const carried = Math.min(read - fixed, load);
    reads.carried += carried;
    reads.work += read - fixed - carried;
  });
  const totals = {
    input: sum('input_tokens'),
    cacheCreate: sum('cache_creation_input_tokens'),
    cacheRead: sum('cache_read_input_tokens'),
    output: sum('output_tokens'),
  };
  return {
    model,
    turns: turns.length,
    ...totals,
    weighted: RATIO.input * totals.input + RATIO.cacheWrite * totals.cacheCreate
      + RATIO.cacheRead * totals.cacheRead + RATIO.output * totals.output,
    reads,
    shellEdits: shellEdits.size,
    firstContext: startup,
    preEditTurns: firstEdit < 0 ? turns.length : firstEdit,
    editContext: turns.length ? context(turns[edit]) : 0,
    lastContext: turns.length ? context(turns.at(-1)) : 0,
    maxContext: Math.max(0, ...turns.map(context)),
  };
}

/** Every subagent transcript under `dir`, with what its `.meta.json` says, written on or after `since`. */
export function collect(dir, { since = new Date(0) } = {}) {
  const agents = [];
  for (const session of readdirSync(dir)) {
    const folder = join(dir, session, 'subagents');
    if (!existsSync(folder)) continue;
    for (const name of readdirSync(folder)) {
      if (!name.endsWith('.jsonl')) continue;
      const file = join(folder, name);
      const written = statSync(file).mtime;
      if (written < since) continue;
      const metaFile = file.replace(/\.jsonl$/, '.meta.json');
      const meta = existsSync(metaFile) ? JSON.parse(readFileSync(metaFile, 'utf8')) : {};
      const agent = readTranscript(readFileSync(file, 'utf8'));
      if (agent.turns === 0) continue;
      agents.push({
        session, agent: basename(name, '.jsonl'), type: meta.agentType ?? 'unknown', description: meta.description ?? '',
        written: written.toISOString().slice(0, 10), ...agent,
      });
    }
  }
  return agents;
}

const median = values => [...values].sort((a, b) => a - b)[Math.floor(values.length / 2)];
const spread = values => ({ median: median(values), max: Math.max(...values) });

/** The agents grouped by type and model, heaviest group first. */
export function summarize(agents) {
  const total = agents.reduce((n, a) => n + a.weighted, 0) || 1;
  const groups = new Map();
  for (const a of agents) {
    const key = `${a.type}\u0000${a.model}`;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(a);
  }
  return [...groups.values()].map(list => {
    const weighted = list.reduce((n, a) => n + a.weighted, 0) || 1;
    const part = pick => list.reduce((n, a) => n + pick(a), 0) / weighted;
    return {
      type: list[0].type,
      model: list[0].model,
      agents: list.length,
      turns: spread(list.map(a => a.turns)),
      preEditTurns: spread(list.map(a => a.preEditTurns)),
      firstContext: spread(list.map(a => a.firstContext)),
      editContext: spread(list.map(a => a.editContext)),
      lastContext: spread(list.map(a => a.lastContext)),
      weighted,
      share: weighted / total,
      shellEdits: list.reduce((n, a) => n + a.shellEdits, 0),
      shellEditors: list.filter(a => a.shellEdits > 0).length,
      // Where the group's weighted went: the four kinds of re-reading, the cache writes, the output and fresh input.
      split: {
        startup: part(a => RATIO.cacheRead * a.reads.startup),
        orientation: part(a => RATIO.cacheRead * a.reads.orientation),
        carried: part(a => RATIO.cacheRead * a.reads.carried),
        work: part(a => RATIO.cacheRead * a.reads.work),
        writes: part(a => RATIO.cacheWrite * a.cacheCreate),
        output: part(a => RATIO.output * a.output),
        input: part(a => RATIO.input * a.input),
      },
    };
  }).sort((a, b) => b.weighted - a.weighted);
}

const thousands = n => `${Math.round(n / 1000).toLocaleString('en-US')}K`;

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const option = name => (args.includes(name) ? args[args.indexOf(name) + 1] : undefined);
  const dir = args[0];
  if (!dir || dir.startsWith('--') || !existsSync(dir)) {
    console.error('usage: node tools/subagent-usage.mjs <transcripts-dir> [--since YYYY-MM-DD] [--json <file>]');
    process.exit(2);
  }
  const since = option('--since') ? new Date(option('--since')) : new Date(0);
  const agents = collect(dir, { since });
  const json = option('--json');
  if (json) writeFileSync(json, JSON.stringify(agents, null, 1));
  console.log(`${agents.length} subagents since ${since.toISOString().slice(0, 10)}; medians, with the largest in brackets`);
  for (const g of summarize(agents)) {
    console.log(`${g.type} on ${g.model || 'unknown'}: ${g.agents} agents, ${(g.share * 100).toFixed(0)}% of weighted`);
    console.log(`  turns ${g.turns.median} (${g.turns.max}), before the first edit ${g.preEditTurns.median}`);
    console.log(`  context: start ${thousands(g.firstContext.median)}, first edit ${thousands(g.editContext.median)}, `
      + `end ${thousands(g.lastContext.median)} (${thousands(g.lastContext.max)})`);
    const percent = v => `${(v * 100).toFixed(1)}%`;
    const s = g.split;
    console.log(`  weighted went to: startup ${percent(s.startup)}, orienting ${percent(s.orientation)}, `
      + `orientation carried ${percent(s.carried)}, the work's reading ${percent(s.work)}, `
      + `cache writes ${percent(s.writes)}, output ${percent(s.output)}`);
    if (g.shellEdits > 0) console.log(`  edits through the shell: ${g.shellEdits}, by ${g.shellEditors} of ${g.agents} agents`);
  }
}
