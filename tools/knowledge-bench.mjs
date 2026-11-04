#!/usr/bin/env node
/**
 * The knowledge bench (KNOW3): how well each knowledge design lets a real agent session find the document
 * that governs a task.
 *
 * ## Why
 *
 * D128 proposes moving the knowledge list out of the always-loaded region into a generated index read on
 * demand, and KNOW2 (D129) reviews the designs from the makers' documentation. Neither is a measurement. The
 * first real set-up made the region 53 KB with 169 documents, and real sessions at the owner's install found
 * knowledge mostly by searching. This bench puts one corpus behind six designs, asks the same questions of a
 * real headless session in each, and reads the stream for what the session opened and when.
 *
 * ## What it runs
 *
 * - **The corpus** is this repository's own `docs/*.md` (the router and the three append-only records left
 *   out: the router is itself an index, and the records are read by lookup) and `canon/core/knowledge/*.md`.
 *   Each design document's frontmatter is written from its row in `docs/README.md`, the way a set-up session
 *   would write it; the canon's keep their own.
 * - **The designs** are one fixture each under `--root`: A, the table in the always-loaded `AGENTS.md`; B, a
 *   generated `INDEX.md` that `AGENTS.md` points to (D128); C, every document a skill; E, search-first with no
 *   index; G, a project prompt hook that prints a local BM25 ranking; 0, the files and no guidance. The brief is
 *   one text in all six, and only each design's own part differs.
 * - **The isolation.** The harness reads instruction files from every folder above the one it runs in, so the
 *   root is refused when one sits above it, and the fixtures belong under the OS temp folder: generated test
 *   material that must not inherit this repository's own doctrine. Each run loads project settings only, no MCP
 *   server, an exact tool set, hook events in the stream, and nothing persisted. A canary turn per design reads
 *   the stream's `init` event and its hook events, and the bench stops if any of it is not as built.
 *
 * Runs are serial, never parallel, and the bench stops at the first sign of a rate or spend limit, keeping what
 * it measured. The raw streams and the scores go to `local/scratch/knowledge-bench/` (gitignored).
 *
 *   node tools/knowledge-bench.mjs run --root <dir under the OS temp folder> [--pilot | --tasks T1,T5]
 *        [--designs 0,A,B,C,E,G] [--label <name>] [--max-turns 20]
 *   node tools/knowledge-bench.mjs report [--label <name>]
 *   node tools/knowledge-bench.mjs rank "<question>"     the push design's ranking, with no model
 *
 * The helpers are tested by `node --test tools/knowledge-bench.test.mjs`. The results are
 * `docs/2026-10-02-knowledge-bench-results.md`.
 */
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import { join, parse, resolve } from 'node:path';
import { isMain, spawnGit, writeAtomic } from './fsx.mjs';
import { alwaysLoaded, fixtureFiles, loadCorpus, project } from './knowledge-bench/corpus.mjs';
import { CANARY_PROMPT, DESIGNS, PILOT_TASKS, REPO, TASKS, taskPrompt } from './knowledge-bench/design.mjs';
import { aggregate, docNameOf, scoreRun } from './knowledge-bench/scoring.mjs';
import { assertIsolatedRoot, claudeArgs, isHook, isolationProblems, limitReached, parseStream } from './knowledge-bench/isolation.mjs';
import { formatRanking, rankBm25, searchable } from './knowledge-bench/search.mjs';
export { ALLOWED_TOOLS, DISALLOWED_TOOLS, WINDOW_CEILING, BRIEF, DESIGNS, TASKS, PILOT_TASKS, taskPrompt, CANARY_PROMPT } from './knowledge-bench/design.mjs';
export { parseRouterRows, frontmatterFor, renderDocument, loadCorpus, knowledgePath, fixtureFiles, alwaysLoaded, project, projectIndex } from './knowledge-bench/corpus.mjs';
export { readFrontmatter, stem, tokenize, rankBm25, formatRanking, hookSource } from './knowledge-bench/search.mjs';
export { assertIsolatedRoot, claudeArgs, parseStream, isolationProblems, limitReached } from './knowledge-bench/isolation.mjs';
export { docNameOf, toolCalls, targetOf, scoreRun, aggregate } from './knowledge-bench/scoring.mjs';

const MARKER = '.knowledge-bench';

/** Write a design's fixture as a fresh git repository with one commit, so the harness sees a clean tree. */
export function buildFixture(dir, design, corpus) {
  if (existsSync(dir)) {
    if (!existsSync(join(dir, MARKER))) throw new Error(`${dir} exists and is not a fixture of this bench; refusing to replace it`);
    rmSync(dir, { recursive: true, force: true });
  }
  mkdirSync(dir, { recursive: true });
  for (const [path, text] of fixtureFiles(design, corpus)) writeAtomic(join(dir, path), text);
  writeAtomic(join(dir, MARKER), `knowledge bench fixture, design ${design}\n`);
  const git = (...args) => {
    const r = spawnGit(['-c', 'core.autocrlf=false', '-c', 'user.name=knowledge-bench', '-c', 'user.email=bench@example.invalid', ...args],
      { cwd: dir, encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`git ${args[0]} in ${dir}: ${r.stderr}`);
  };
  git('init', '-q', '-b', 'main');
  git('add', '-A');
  git('commit', '-q', '-m', 'The documents');
}

/** The child's environment: this session's own variables out, so the run is not a child of it nor shares its effort. */
function childEnv() {
  return Object.fromEntries(Object.entries(process.env).filter(([k]) => !/^CLAUDE/i.test(k)));
}

/** Run one session and keep its whole stream. Resolves `{ stdout, stderr, code, wallMs }`. */
function runClaude(claude, cwd, args, timeoutMs) {
  return new Promise((done) => {
    const t0 = Date.now();
    const child = spawn(claude, args, { cwd, env: childEnv(), stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    let stdout = '';
    let stderr = '';
    child.stdout.setEncoding('utf8').on('data', (c) => { stdout += c; });
    child.stderr.setEncoding('utf8').on('data', (c) => { stderr += c; });
    const timer = setTimeout(() => child.kill(), timeoutMs);
    child.on('close', (code) => { clearTimeout(timer); done({ stdout, stderr, code, wallMs: Date.now() - t0 }); });
    child.on('error', (err) => { clearTimeout(timer); done({ stdout, stderr: `${stderr}${err.message}`, code: -1, wallMs: Date.now() - t0 }); });
  });
}

function argOf(argv, flag, fallback) {
  const i = argv.indexOf(flag);
  return i >= 0 && argv[i + 1] !== undefined ? argv[i + 1] : fallback;
}

const pct = (x) => `${Math.round(x * 100)}%`;
const kb = (n) => `${(n / 1000).toFixed(1)} KB`;

/** The per-design table and the per-task matrix, as markdown, from the run records and the fixtures' bytes. */
export function renderReport(records, corpus) {
  const order = Object.keys(DESIGNS).filter((d) => records.some((r) => r.design === d));
  const rows = aggregate(records).sort((a, b) => order.indexOf(a.design) - order.indexOf(b.design));
  const out = ['| Design | Hits | Hit rate | Median calls to the target | Median calls to the read | Median searches before it | Wrong reads per run | Tokens per hit | List price per hit | Median turns | Median wall | Always loaded | At 169 documents |',
    '|---|---|---|---|---|---|---|---|---|---|---|---|---|'];
  for (const r of rows) {
    const files = fixtureFiles(r.design, corpus);
    const loaded = alwaysLoaded(r.design, files, corpus);
    const at169 = project(r.design, files, corpus, 169);
    out.push(`| ${r.design}: ${DESIGNS[r.design]} | ${r.hits}/${r.runs} | ${pct(r.hitRate)} | ${r.medianCallsToTarget ?? '—'} | ${r.medianCallsToHit ?? '—'} | ${r.medianSearchesBeforeHit ?? '—'} | ${r.meanWrongReads.toFixed(1)} | ${r.tokensPerHit?.toLocaleString('en-US') ?? '—'} | ${r.costPerHit === null ? '—' : `$${r.costPerHit.toFixed(3)}`} | ${r.medianTurns ?? '—'} | ${((r.medianWallMs ?? 0) / 1000).toFixed(0)} s | ${kb(loaded.total)} | ${kb(at169)} |`);
  }
  const tasks = TASKS.filter((t) => records.some((r) => r.task === t.id));
  out.push('', `| Task | ${order.join(' | ')} |`, `|---|${order.map(() => '---').join('|')}|`);
  for (const t of tasks) {
    const cellsOf = order.map((d) => {
      const r = records.find((x) => x.design === d && x.task === t.id);
      if (!r) return '';
      return r.hit ? `read at call ${r.hitAt}` : `miss (${r.calls} calls)`;
    });
    out.push(`| ${t.id} | ${cellsOf.join(' | ')} |`);
  }
  return `${out.join('\n')}\n`;
}

/** The run records of a label, each scored again from its kept stream, so a scoring change reaches old runs. */
function readRecords(outDir) {
  if (!existsSync(outDir)) return [];
  return readdirSync(outDir).filter((f) => /^[0A-Z]-T\d+\.json$/.test(f)).map((f) => {
    const record = JSON.parse(readFileSync(join(outDir, f), 'utf8'));
    const stream = join(outDir, f.replace(/\.json$/, '.jsonl'));
    return existsSync(stream) ? { ...record, ...scoreRun(parseStream(readFileSync(stream, 'utf8')), record.truth) } : record;
  });
}

async function main(argv) {
  const command = argv[0];
  const label = argOf(argv, '--label', 'run');
  const outDir = join(REPO, 'local', 'scratch', 'knowledge-bench', label);
  if (command === 'rank') {
    const corpus = loadCorpus();
    process.stdout.write(formatRanking(rankBm25(searchable(corpus), taskPrompt(argv[1] ?? '')).slice(0, 5)) || 'nothing ranked\n');
    return 0;
  }
  if (command === 'report') {
    const corpus = loadCorpus();
    process.stdout.write(renderReport(readRecords(outDir), corpus));
    return 0;
  }
  if (command !== 'run') {
    process.stderr.write('usage: knowledge-bench.mjs run --root <dir> [--pilot | --tasks T1,T5] [--designs 0,A,B,C,E,G] [--label <name>] [--max-turns 20]\n'
      + '       knowledge-bench.mjs report [--label <name>]\n       knowledge-bench.mjs rank "<question>"\n');
    return 2;
  }
  const root = argOf(argv, '--root');
  try { assertIsolatedRoot(root); } catch (err) { process.stderr.write(`${err.message}\n`); return 2; }
  const designs = argOf(argv, '--designs', Object.keys(DESIGNS).join(',')).split(',');
  const taskIds = argv.includes('--pilot') ? PILOT_TASKS : argOf(argv, '--tasks', TASKS.map((t) => t.id).join(',')).split(',');
  const tasks = TASKS.filter((t) => taskIds.includes(t.id));
  const maxTurns = Number(argOf(argv, '--max-turns', '20'));
  const claude = argOf(argv, '--claude', 'claude');
  const corpus = loadCorpus();
  const corpusNames = corpus.map((d) => d.name);
  mkdirSync(outDir, { recursive: true });
  writeAtomic(join(outDir, 'corpus.json'), `${JSON.stringify(corpus.map(({ name, applies_when, enforces, title, source }) => ({ name, applies_when, enforces, title, source })), null, 2)}\n`);
  console.log(`corpus: ${corpus.length} documents (${corpus.filter((d) => d.source === 'canon').length} canon); designs ${designs.join(' ')}; tasks ${tasks.map((t) => t.id).join(' ')}`);

  // Fixtures and canaries. The control's skills are the harness's own, which every other design is held to.
  let baselineSkills = null;
  const baselineFile = join(outDir, 'baseline-skills.json');
  if (existsSync(baselineFile)) baselineSkills = JSON.parse(readFileSync(baselineFile, 'utf8'));
  for (const design of ['0', ...designs.filter((d) => d !== '0')]) {
    const dir = join(resolve(root), design);
    const canaryFile = join(outDir, `canary-${design}.json`);
    if (existsSync(canaryFile) && existsSync(join(dir, MARKER))) continue;
    buildFixture(dir, design, corpus);
    const run = await runClaude(claude, dir, claudeArgs(CANARY_PROMPT, { maxTurns: 2 }), 300_000);
    writeAtomic(join(outDir, `canary-${design}.jsonl`), run.stdout);
    const events = parseStream(run.stdout);
    const limit = limitReached(events);
    if (limit) { console.log(`STOPPED at the canary of ${design}: ${limit}`); return 3; }
    const init = events.find((e) => e.type === 'system' && e.subtype === 'init');
    if (design === '0') {
      baselineSkills = (init?.skills ?? []).filter((s) => !s.includes(':'));
      writeAtomic(baselineFile, `${JSON.stringify(baselineSkills)}\n`);
    }
    const problems = isolationProblems(events, { design, baselineSkills, corpusNames, fixtureDir: dir });
    const memory = init?.memory_paths?.auto;
    if (memory && existsSync(join(memory, 'MEMORY.md'))) problems.push(`an auto-memory index exists for the fixture: ${memory}`);
    const answer = events.find((e) => e.type === 'result')?.result ?? '';
    writeAtomic(canaryFile, `${JSON.stringify({ design, code: run.code, stderr: run.stderr.slice(0, 2000), problems, init, answer,
      hooks: events.filter(isHook) }, null, 2)}\n`);
    console.log(`canary ${design}: ${problems.length ? `NOT ISOLATED\n  ${problems.join('\n  ')}` : 'isolated'} (model ${init?.model}, harness ${init?.claude_code_version})`);
    if (problems.length || !init) return 1;
  }

  // The runs: task by task, the design order turned each time so no design always runs first or last.
  let i = 0;
  for (const task of tasks) {
    const order = designs.map((_, k) => designs[(k + i) % designs.length]);
    i += 1;
    for (const design of order) {
      const record = join(outDir, `${design}-${task.id}.json`);
      if (existsSync(record)) continue;
      const dir = join(resolve(root), design);
      const run = await runClaude(claude, dir, claudeArgs(taskPrompt(task.question), { maxTurns }), 900_000);
      writeAtomic(join(outDir, `${design}-${task.id}.jsonl`), run.stdout);
      const events = parseStream(run.stdout);
      const limit = limitReached(events);
      const score = scoreRun(events, task.truth);
      const problems = isolationProblems(events, { design, baselineSkills, corpusNames, fixtureDir: dir });
      const pushRank = rankBm25(searchable(corpus), taskPrompt(task.question)).findIndex((r) => task.truth.includes(docNameOf(r.path))) + 1 || null;
      writeAtomic(record, `${JSON.stringify({ design, task: task.id, truth: task.truth, ...score, wallMs: run.wallMs, code: run.code,
        stderr: run.stderr.slice(0, 2000), isolation: problems, pushRank }, null, 2)}\n`);
      console.log(`${design} ${task.id}: ${score.hit ? `hit at call ${score.hitAt}` : 'miss'}; ${score.calls} calls, ${score.turns} turns, ${(run.wallMs / 1000).toFixed(0)} s${problems.length ? `; ISOLATION: ${problems.join('; ')}` : ''}`);
      if (limit) { console.log(`STOPPED after ${design} ${task.id}: ${limit}`); return 3; }
      if (problems.length) return 1;
    }
  }
  const report = renderReport(readRecords(outDir), corpus);
  writeAtomic(join(outDir, 'report.md'), report);
  process.stdout.write(report);
  return 0;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => { process.exitCode = code; }, (err) => {
    process.stderr.write(`${err.stack ?? err}\n`);
    process.exitCode = 2;
  });
}
