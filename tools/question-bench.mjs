#!/usr/bin/env node
/**
 * The question bench (KNOWUSE2): would a review of a question, before it reaches the person, class it as the person
 * did?
 *
 * ## Why
 *
 * D135 §6: beside each item a park or a closing note brings, a review shows what may already answer it, and a model's
 * review never answers in the person's place until a replay over recorded questions shows it classes them as a person
 * did. The knowledge-use evidence (`docs/2026-10-03-knowledge-use-evidence.md` §4) classed 46 such questions: K a
 * knowledge document answers it, R the repository's documents require asking, B the ticket, the code or a look settles
 * it, O only the person, D drift or Daoris's own mechanics. This bench replays a cases file of such questions against a
 * repository's knowledge folder, through two tiers, and scores each against those classes.
 *
 * ## The tiers
 *
 * - **The floor needs no model** (D24): a BM25 word search, by the knowledge bench's tokenizer, over every markdown
 *   line of the knowledge folder and the ask's words, each line with its neighbours. It shows the three best lines
 *   that share two words of the question at least, never two beside each other, labelled *words only*. It points at
 *   text and never answers: it cannot say a question needs the person.
 * - **The model tier is the deployment's**: a command line it names (`--harness`, or `QUESTION_BENCH_HARNESS`), run
 *   through the platform's shell in the knowledge folder, handed one check per question on its standard input: the
 *   question, the ask's words by line and the floor's hits. Its reply ends with one line: `SOURCE: <file>:<line>
 *   "<quote>"`, `NEEDS THE PERSON: <why>` or `A READING: <what it rests on>`. A source counts only where its quote is in
 *   the file it names. No model is named here, and the bench's own tests run it only against a stub.
 *
 * ## The score
 *
 * Per tier: the K and B items found with the source the person's record names; the O items left to the person; and
 * every item the tier answered in the person's place wrongly, the number that must be zero before a review is shown
 * (D135 §6). An answer is a source or a reading. It is wrong on an O or an R, which are the person's to give, and a
 * source is wrong on any class unless it is the record's own (a quote not in its file, another document, the drifted
 * one). Each item says which tier classed it, and a tier that did not run, or a check that failed, is said, never
 * counted as zero (D24). The score is a report, never a gate (D54).
 *
 * ## The cases file
 *
 *   { "knowledge": "<folder, beside the file>", "asks": { "<key>": "<the ask's words>" },
 *     "cases": [{ "id", "where": "park|close", "class": "K|R|B|O|D", "ask": "<key>", "question",
 *                 "answer": { "source": "<a knowledge file, or ask>", "quote": "<its words>" } }] }
 *
 * A K names its answer; an O or an R names none. The tracked fixture is constructed (`question-bench-fixtures/`); a
 * recorded set and the repository's documents are private and stay untracked. Records and the report go to
 * `local/scratch/question-bench/<label>/` (gitignored); a second run asks again only the checks that failed.
 *
 *   node tools/question-bench.mjs run [--cases <file>] [--knowledge <dir>] [--harness "<command line>"] [--cwd <dir>]
 *        [--label <name>] [--timeout <seconds>] [--stop-after <failed checks in a row>] [--fresh]
 *
 * The helpers are tested by `node --test tools/question-bench.test.mjs`. What it found is D135's KNOWUSE2 note.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { tokenize } from './knowledge-bench.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..');

/** The constructed fixture, run when no cases file is named. */
export const DEFAULT_CASES = join(HERE, 'question-bench-fixtures', 'cases.json');

export const CLASSES = ['K', 'R', 'B', 'O', 'D'];
const WHERE = ['park', 'close'];

/** The floor shows at most this many lines, one per source. */
export const FLOOR_LIMIT = 3;

/** A line must share this many distinct words of the question to be shown: one shared word is noise. */
export const MIN_SHARED = 2;

/** How far a pointer may sit from the record's quote and still be the same source, in lines. */
export const SLACK = 2;

/** An O or an R is the person's to give: any answer to one is an answer in their place. */
const PERSONS_OWN = new Set(['O', 'R']);

// ---------------------------------------------------------------------------------------------------------
// The cases and the sources

/** The cases file, checked: `{ about, knowledge, asks, cases }`, the knowledge folder resolved against `base`. */
export function readCases(text, { base }) {
  let data;
  try { data = JSON.parse(text); } catch (err) { throw new Error(`the cases file is not JSON: ${err.message}`); }
  const asks = data?.asks && typeof data.asks === 'object' ? data.asks : {};
  const cases = Array.isArray(data?.cases) ? data.cases : [];
  if (!cases.length) throw new Error('the cases file has no cases');
  const problems = [];
  const seen = new Set();
  for (const c of cases) {
    const id = String(c?.id ?? '');
    if (!/^[\w.-]+$/.test(id)) problems.push(`case id "${id}" is not a simple name (letters, digits, dot, dash)`);
    if (seen.has(id)) problems.push(`case id ${id} is repeated`);
    seen.add(id);
    if (!WHERE.includes(c.where)) problems.push(`case ${id}: where ${c.where} is not park or close`);
    if (!CLASSES.includes(c.class)) problems.push(`case ${id}: class ${c.class} is not one of ${CLASSES.join(', ')}`);
    if (typeof asks[c.ask] !== 'string') problems.push(`case ${id}: ask ${c.ask} is not in asks`);
    if (typeof c.question !== 'string' || !c.question.trim()) problems.push(`case ${id}: no question`);
    if (c.answer !== undefined && (typeof c.answer?.source !== 'string' || !c.answer.source
      || (c.answer.quote !== undefined && typeof c.answer.quote !== 'string'))) {
      problems.push(`case ${id}: an answer is { "source", "quote" }`);
    }
    if (c.class === 'K' && !c.answer) problems.push(`case ${id} is a K but names no answer: a K names the document that answers it`);
    if (PERSONS_OWN.has(c.class) && c.answer) problems.push(`case ${id}: an O or R item names no answer, being the person's to give`);
  }
  if (problems.length) throw new Error(`the cases file cannot be scored:\n- ${problems.join('\n- ')}`);
  return {
    about: data.about ?? '',
    knowledge: typeof data.knowledge === 'string' ? resolve(base, data.knowledge) : null,
    asks,
    cases: cases.map((c) => ({
      id: String(c.id), where: c.where, class: c.class, ask: c.ask, question: c.question,
      ...(c.answer ? { answer: { source: c.answer.source, ...(c.answer.quote !== undefined ? { quote: c.answer.quote } : {}) } } : {}),
    })),
  };
}

const linesOf = (text) => String(text ?? '').replace(/^﻿/, '').replace(/\r\n?/g, '\n').split('\n');

/** Every markdown file under `dir`, by its `/`-separated path there, as lines: `[{ path, lines }]`, sorted. */
export function loadKnowledge(dir) {
  const out = [];
  const walk = (at) => {
    for (const entry of readdirSync(join(dir, at), { withFileTypes: true })) {
      const path = at ? `${at}/${entry.name}` : entry.name;
      if (entry.isDirectory()) { if (!entry.name.startsWith('.')) walk(path); } else if (entry.name.endsWith('.md')) {
        out.push({ path, lines: linesOf(readFileSync(join(dir, path), 'utf8')) });
      }
    }
  };
  walk('');
  return out.sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));
}

/** The ask's words as a source, cited as `ask:<line>`. */
export const askDoc = (text) => ({ path: 'ask', lines: linesOf(text) });

/** Text compared by its words: case, spacing, emphasis and code marks, and curly quotes set aside. */
function norm(text) {
  return String(text).toLowerCase()
    .replace(/[‘’ʼ]/g, "'").replace(/[“”]/g, '"')
    .replace(/[*`_]/g, '')
    .replace(/\s+/g, ' ').trim();
}

/** How far apart an elided quote's pieces may sit, in characters. */
const ELISION_SPAN = 600;

/**
 * Where a quote stands in a source: every `{ start, end }`, 1-based lines, matched by its words across lines. A quote
 * may elide with `…` or `...`, its pieces then found in order.
 */
export function locateQuote(doc, quote) {
  const pieces = String(quote ?? '').split(/\s*(?:…|\.\.\.)\s*/).map(norm).filter(Boolean);
  if (!pieces.length) return [];
  let joined = '';
  const starts = [];
  doc.lines.forEach((line, i) => {
    const n = norm(line);
    if (!n) return;
    starts.push({ at: joined.length, line: i + 1 });
    joined += `${n} `;
  });
  const lineAt = (offset) => {
    let found = starts[0]?.line ?? 1;
    for (const s of starts) { if (s.at > offset) break; found = s.line; }
    return found;
  };
  const out = [];
  for (let from = joined.indexOf(pieces[0]); from >= 0; from = joined.indexOf(pieces[0], from + 1)) {
    let end = from + pieces[0].length;
    let whole = true;
    for (const piece of pieces.slice(1)) {
      const next = joined.indexOf(piece, end);
      if (next < 0 || next - end > ELISION_SPAN) { whole = false; break; }
      end = next + piece.length;
    }
    if (whole) out.push({ start: lineAt(from), end: lineAt(end - 1) });
  }
  return out;
}

/**
 * The source a name means: the file with that path, else the one file whose path and the name end alike (a bare file
 * name, or a path a harness wrote in full or under its folder). `{ doc }` or `{ problem }`.
 */
export function resolveSource(name, docs) {
  const wanted = String(name ?? '').trim().replace(/\\/g, '/').replace(/^\.\//, '');
  const exact = docs.find((d) => d.path === wanted);
  if (exact) return { doc: exact };
  const alike = docs.filter((d) => d.path !== 'ask' && (wanted.endsWith(`/${d.path}`) || d.path.endsWith(`/${wanted}`)));
  if (alike.length === 1) return { doc: alike[0] };
  if (alike.length > 1) {
    const longest = Math.max(...alike.map((d) => d.path.length));
    const best = alike.filter((d) => d.path.length === longest && wanted.endsWith(`/${d.path}`));
    if (best.length === 1) return { doc: best[0] };
    return { problem: `${wanted} matches two files or more: ${alike.map((d) => d.path).join(', ')}` };
  }
  return { problem: `${wanted}: no such source` };
}

/**
 * The cases made ready to score: each with its ask as a source and the lines its answer stands at (`expected`), and
 * every answer that is not in its source named in `problems`, so nothing is scored against a record that moved.
 */
export function prepare(read, knowledge) {
  const problems = [];
  const cases = read.cases.map((c) => {
    const ask = askDoc(read.asks[c.ask]);
    let expected = null;
    if (c.answer) {
      const r = resolveSource(c.answer.source, [...knowledge, ask]);
      if (r.problem) problems.push(`${c.id}: its answer's source ${r.problem}`);
      else if (c.answer.quote === undefined) expected = { path: r.doc.path, ranges: null };
      else {
        const ranges = locateQuote(r.doc, c.answer.quote);
        if (ranges.length) expected = { path: r.doc.path, ranges };
        else problems.push(`${c.id}: its answer's quote is not in ${r.doc.path}`);
      }
    }
    return { ...c, askDoc: ask, expected };
  });
  return { knowledge, cases, problems };
}

/** Whether a pointer `{ path, line, end? }` stands where the record's answer does. */
function pointerRight(pointer, expected) {
  if (!expected || pointer.path !== expected.path) return false;
  if (!expected.ranges) return true;
  const end = pointer.end ?? pointer.line;
  return expected.ranges.some((r) => end >= r.start - SLACK && pointer.line <= r.end + SLACK);
}

// ---------------------------------------------------------------------------------------------------------
// The floor: words only

/**
 * Every line of the sources that carries a word, as a passage of it and its neighbours, the line itself counted
 * twice so the line a passage is named by is the one that matched.
 */
export function indexPassages(docs) {
  const passages = [];
  const df = new Map();
  for (const doc of docs) {
    const tokens = doc.lines.map((l) => tokenize(l));
    tokens.forEach((own, i) => {
      if (!own.length) return;
      const words = [...own, ...own, ...(tokens[i - 1] ?? []), ...(tokens[i + 1] ?? [])];
      const tf = new Map();
      for (const w of words) tf.set(w, (tf.get(w) ?? 0) + 1);
      for (const w of tf.keys()) df.set(w, (df.get(w) ?? 0) + 1);
      passages.push({ path: doc.path, line: i + 1, text: doc.lines[i].trim(), tf, length: words.length });
    });
  }
  return { passages, df };
}

const clip = (text, max) => (text.length <= max ? text : `${text.slice(0, max - 1)}…`);

/**
 * What the words of a question meet: the {@link FLOOR_LIMIT} best lines by BM25, each sharing {@link MIN_SHARED} of
 * its words at least, no two within {@link SLACK} lines of each other in one source. `extra` is searched with the
 * index (the ask's words).
 */
export function floorHits(question, index, extra = [], k1 = 1.2, b = 0.75) {
  const more = indexPassages(extra);
  const passages = [...index.passages, ...more.passages];
  if (!passages.length) return [];
  const df = (t) => (index.df.get(t) ?? 0) + (more.df.get(t) ?? 0);
  const avg = passages.reduce((n, p) => n + p.length, 0) / passages.length;
  const terms = [...new Set(tokenize(question))];
  const scored = [];
  for (const p of passages) {
    const shared = terms.filter((t) => p.tf.has(t));
    if (shared.length < MIN_SHARED) continue;
    let score = 0;
    for (const t of shared) {
      const f = p.tf.get(t);
      const n = df(t);
      score += Math.log(1 + (passages.length - n + 0.5) / (n + 0.5)) * ((f * (k1 + 1)) / (f + k1 * (1 - b + (b * p.length) / avg)));
    }
    scored.push({ path: p.path, line: p.line, text: clip(p.text, 200), shared, score });
  }
  scored.sort((x, y) => y.score - x.score || (x.path < y.path ? -1 : x.path > y.path ? 1 : x.line - y.line));
  // A ticket is one long source, so the best lines are taken wherever they stand, but a line beside one already shown
  // is the same place and is folded into it (KNOWUSE2: one line per source lost the ticket's line to its neighbours).
  const shown = [];
  for (const h of scored) {
    if (shown.length >= FLOOR_LIMIT) break;
    if (shown.some((s) => s.path === h.path && Math.abs(s.line - h.line) <= SLACK)) continue;
    shown.push(h);
  }
  return shown.map((h) => ({ ...h, score: Math.round(h.score * 100) / 100 }));
}

// ---------------------------------------------------------------------------------------------------------
// The model tier: the deployment's command

/** The check one question is handed: what to look at, and the three ways its reply may end. */
export function checkPrompt({ question, where, askDoc: ask, hits, knowledgeDir }) {
  const askLines = ask.lines.map((l, i) => (l.trim() ? `ask:${i + 1}  ${l}` : null)).filter(Boolean);
  return [
    'You are checking one question a coding session put to the person it works for, before the person sees it. Say',
    'whether something already answers it. Read what you need; change nothing.',
    '',
    `The repository's knowledge is the markdown under: ${knowledgeDir}`,
    '',
    'The ask the session worked from, by line (cite a line as ask:<line>):',
    ...(askLines.length ? askLines : ['(no words)']),
    '',
    'A word search found these lines (words only: they may not apply):',
    ...(hits.length ? hits.map((h) => `- ${h.path}:${h.line}  ${h.text}`) : ['- nothing']),
    '',
    `The question, as the session put it in ${where === 'park' ? 'a park (it stopped to ask)' : 'a closing note'}:`,
    question,
    '',
    'End your reply with exactly one of these lines:',
    'SOURCE: <file>:<line> "<words quoted exactly from that line>"',
    '  when a knowledge file (its path under the folder above) or the ask (ask:<line>) already answers it, so the',
    '  person need not.',
    'NEEDS THE PERSON: <why>',
    '  when only the person can give it: a sign-in, a go-ahead for an act outside the repository or on a production',
    '  system, a preference nothing records, or an agreement a repository document requires before a change.',
    'A READING: <the reading, and what it rests on>',
    '  when the sources lean one way without settling it: the session should take that reading and say so, not ask.',
    '',
    "A person's words quoted in a document, a closed note or a commit are someone's reading, not the person's answer,",
    'unless the ask above holds them.',
    '',
  ].join('\n');
}

const VERDICT = /^[\s>*-]*\**\s*(SOURCE|NEEDS THE PERSON|A READING)\s*\**\s*:\s*\**\s*(.*)$/i;

/**
 * A reply, by its last verdict line: `{ verdict: 'source', path, line, quote }`, `{ verdict: 'person' | 'reading',
 * words }`, or `{ verdict: null }` when it ends in none of the three.
 */
export function parseReply(text) {
  const lines = String(text ?? '').split(/\r?\n/);
  for (let i = lines.length - 1; i >= 0; i -= 1) {
    const m = VERDICT.exec(lines[i]);
    if (!m) continue;
    const kind = m[1].toUpperCase();
    const rest = m[2].trim();
    if (kind === 'NEEDS THE PERSON') return { verdict: 'person', words: rest };
    if (kind === 'A READING') return { verdict: 'reading', words: rest };
    const open = rest.search(/["“]/);
    const close = Math.max(rest.lastIndexOf('"'), rest.lastIndexOf('”'));
    const quote = open >= 0 && close > open ? rest.slice(open + 1, close) : null;
    const locator = (open >= 0 ? rest.slice(0, open) : rest).replace(/`/g, '').replace(/[\s—–:-]+$/, '').trim();
    const at = /^(.*?)(?::(\d+)(?:[-–]\d+)?)?$/.exec(locator);
    return { verdict: 'source', path: at[1].trim(), line: at[2] ? Number(at[2]) : null, quote };
  }
  return { verdict: null };
}

/**
 * A source checked against the files: founded only where its quote stands in the file it names, and then placed at
 * the quote's line nearest the one it gave. `{ founded: true, path, line, end }` or `{ founded: false, path, why }`.
 */
export function judgeSource(reply, docs) {
  const r = resolveSource(reply.path, docs);
  if (r.problem) return { founded: false, path: reply.path, why: `its source ${r.problem}` };
  if (!reply.quote) return { founded: false, path: r.doc.path, why: 'it quotes nothing' };
  const ranges = locateQuote(r.doc, reply.quote);
  if (!ranges.length) return { founded: false, path: r.doc.path, why: `the quote is not in ${r.doc.path}` };
  const near = reply.line ?? ranges[0].start;
  const best = ranges.reduce((x, y) => (Math.abs(y.start - near) < Math.abs(x.start - near) ? y : x));
  return { founded: true, path: r.doc.path, line: best.start, end: best.end };
}

/** Why a kept check gave no reply to read, or null when it ran to the end. */
function runFailure(record) {
  if (record.error) return `the run failed: ${record.error}`;
  if (record.timedOut) return 'the run timed out';
  if (record.code !== 0) return `the run failed: exit ${record.code}`;
  return null;
}

/** The process tree of a check that ran too long. */
function stopTree(child) {
  if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
  else {
    try { process.kill(-child.pid, 'SIGKILL'); } catch { child.kill('SIGKILL'); }
  }
}

/**
 * The child's environment: the bench's own, so the deployment's account is the one its harness reads, without the
 * two variables that mark a process as inside a running session, since the check is a session of its own.
 */
function childEnv() {
  const env = { ...process.env };
  delete env.CLAUDECODE;
  delete env.CLAUDE_CODE_ENTRYPOINT;
  return env;
}

/**
 * Run one check: the deployment's command line through the platform's shell, as typed at its prompt, in `cwd`, the
 * prompt on its standard input. Resolves `{ stdout, stderr, code, timedOut, wallMs, error? }`.
 */
export function runCheck(commandLine, prompt, { cwd, timeoutMs = 300_000 } = {}) {
  return new Promise((done) => {
    const t0 = Date.now();
    let child;
    try {
      child = spawn(commandLine, {
        shell: true, cwd, env: childEnv(), stdio: ['pipe', 'pipe', 'pipe'], windowsHide: true,
        detached: process.platform !== 'win32',
      });
    } catch (err) {
      done({ stdout: '', stderr: '', code: null, timedOut: false, wallMs: 0, error: err.message });
      return;
    }
    let stdout = '';
    let stderr = '';
    let timedOut = false;
    let error;
    child.stdout.setEncoding('utf8').on('data', (c) => { stdout += c; });
    child.stderr.setEncoding('utf8').on('data', (c) => { stderr += c; });
    child.stdin.on('error', () => { /* a command that never reads its input */ });
    child.stdin.end(prompt);
    let settled = false;
    const finish = (code) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      done({ stdout, stderr, code: timedOut ? null : code, timedOut, wallMs: Date.now() - t0, ...(error ? { error } : {}) });
    };
    const timer = setTimeout(() => { timedOut = true; stopTree(child); }, timeoutMs);
    child.on('error', (err) => { error = err.message; });
    child.on('close', finish);
    // A check that timed out ends when its shell does: a program the shell started as the kill landed may still hold
    // the output pipes, and waiting on them would wait out the program (KNOWUSE2).
    child.on('exit', (code) => {
      if (!timedOut) return;
      child.stdout.destroy();
      child.stderr.destroy();
      finish(code);
    });
  });
}

// ---------------------------------------------------------------------------------------------------------
// The score

/** One item scored: the floor's hits, each marked when it is the record's source; the model's verdict, judged. */
export function scoreCase(c, hits, record, knowledge) {
  const marked = hits.map((h) => ({ ...h, right: pointerRight(h, c.expected) }));
  const floor = { hits: marked, right: marked.some((h) => h.right), wrong: false };
  let model = null;
  if (record) {
    const failed = runFailure(record);
    const reply = failed ? { verdict: null } : parseReply(record.stdout);
    const own = PERSONS_OWN.has(c.class);
    if (!reply.verdict) model = { verdict: null, why: failed ?? 'no verdict line in the reply', right: false, wrong: false };
    else if (reply.verdict === 'source') {
      const judged = judgeSource(reply, [...knowledge, c.askDoc]);
      const right = judged.founded && pointerRight(judged, c.expected);
      model = { verdict: 'source', judged, right, wrong: own || !right, ...(judged.why ? { why: judged.why } : {}) };
    } else model = { verdict: reply.verdict, words: reply.words, right: false, wrong: own && reply.verdict === 'reading' };
  }
  const classedBy = model?.verdict ? 'model' : hits.length ? 'words only' : 'none';
  return { id: c.id, where: c.where, class: c.class, named: Boolean(c.expected), floor, model, classedBy };
}

/** The tiers' numbers. A model tier with no kept check is `null`: it did not run, which is not zero. */
export function summarize(scored) {
  const kbItems = scored.filter((s) => s.class === 'K' || s.class === 'B');
  const os = scored.filter((s) => s.class === 'O');
  const named = kbItems.filter((s) => s.named).length;
  const checked = scored.filter((s) => s.model);
  const verdicts = (v) => checked.filter((s) => s.model.verdict === v).length;
  const byClass = {};
  for (const k of CLASSES) {
    const mine = scored.filter((s) => s.class === k);
    byClass[k] = {
      items: mine.length,
      floorPointed: mine.filter((s) => s.floor.hits.length).length,
      floorRight: mine.filter((s) => s.floor.right).length,
      source: mine.filter((s) => s.model?.verdict === 'source').length,
      reading: mine.filter((s) => s.model?.verdict === 'reading').length,
      person: mine.filter((s) => s.model?.verdict === 'person').length,
      none: mine.filter((s) => s.model && !s.model.verdict).length,
      modelRight: mine.filter((s) => s.model?.right).length,
      modelWrong: mine.filter((s) => s.model?.wrong).length,
    };
  }
  return {
    cases: scored.length,
    where: { park: scored.filter((s) => s.where === 'park').length, close: scored.filter((s) => s.where === 'close').length },
    floor: {
      label: 'words only', answers: false,
      kb: { right: kbItems.filter((s) => s.floor.right).length, total: kbItems.length, named },
      oLeft: os.length, oTotal: os.length, wrong: 0,
      pointed: scored.filter((s) => s.floor.hits.length).length,
    },
    model: checked.length ? {
      checked: checked.length,
      kb: { right: kbItems.filter((s) => s.model?.right).length, total: kbItems.length, named },
      oLeft: os.filter((s) => s.model?.verdict === 'person').length, oTotal: os.length,
      wrong: checked.filter((s) => s.model.wrong).length,
      noVerdict: checked.filter((s) => !s.model.verdict).length,
      verdicts: { source: verdicts('source'), reading: verdicts('reading'), person: verdicts('person') },
    } : null,
    byClass,
    classedBy: {
      model: scored.filter((s) => s.classedBy === 'model').length,
      'words only': scored.filter((s) => s.classedBy === 'words only').length,
      none: scored.filter((s) => s.classedBy === 'none').length,
    },
  };
}

const cell = (text) => String(text).replace(/\|/g, '\\|');

function modelCell(m) {
  if (!m) return 'not run';
  if (!m.verdict) return `no verdict: ${m.why}`;
  if (m.verdict === 'person') return 'needs the person';
  if (m.verdict === 'reading') return 'a reading';
  if (!m.judged.founded) return `source ${m.judged.path} (unfounded: ${m.why})`;
  return `source ${m.judged.path}:${m.judged.line} (${m.right ? 'right' : "not the record's source"})`;
}

/** The report: the tiers' numbers, per class, and per item which tier classed it. No question's words are in it. */
export function renderReport({ label, scored, summary, knowledgeCount, commands = [], stopped = null }) {
  const s = summary;
  const count = (k) => s.byClass[k].items;
  const m = s.model;
  const tier = !commands.length && !m ? 'The model tier: not run.'
    : `The model tier: ${commands.map((c) => `\`${c}\``).join(', ') || 'kept checks'}, ${m?.checked ?? 0} of ${s.cases} checked${stopped ? `; stopped after ${stopped}` : ''}.`;
  const out = [
    `# Question bench: ${label}`,
    '',
    `${s.cases} recorded questions (${CLASSES.map((k) => `${k} ${count(k)}`).join(', ')}; ${s.where.park} in parks, ${s.where.close} in closing notes) against ${knowledgeCount} knowledge documents.`,
    '',
    "The floor is a word search over the knowledge and the ask's words, *words only*: it points at a line and never answers.",
    tier,
    '',
    "| Tier | K and B found with the right source | O left to the person | Answered in the person's place, wrongly | No verdict |",
    '|---|---|---|---|---|',
    `| Floor, words only | ${s.floor.kb.right} of ${s.floor.kb.total} (${s.floor.kb.named} name a source) | ${s.floor.oLeft} of ${s.floor.oTotal}: it never answers | 0: it never answers | — |`,
    m ? `| Model | ${m.kb.right} of ${m.kb.total} | ${m.oLeft} of ${m.oTotal} | ${m.wrong} | ${m.noVerdict} |` : '| Model | not run | not run | not run | not run |',
    '',
    "Nothing is shown to the person as an answer while the model's wrong answers are above zero (D135 §6).",
    '',
    '| Class | Items | Floor pointed | Floor right | Model: source | A reading | Needs the person | No verdict | Right | Wrong |',
    '|---|---|---|---|---|---|---|---|---|---|',
    ...CLASSES.map((k) => {
      const c = s.byClass[k];
      const mc = m ? [c.source, c.reading, c.person, c.none, c.modelRight, c.modelWrong] : Array(6).fill('—');
      return `| ${k} | ${c.items} | ${c.floorPointed} | ${c.floorRight} | ${mc.join(' | ')} |`;
    }),
    '',
    `Classed by: the model ${s.classedBy.model}, words only ${s.classedBy['words only']}, none ${s.classedBy.none}.`,
    '',
    '| Item | Where | Class | Floor, words only | Model | Classed by | Wrong |',
    '|---|---|---|---|---|---|---|',
    ...scored.map((x) => {
      const floor = x.floor.hits.map((h) => `${h.path}:${h.line}${h.right ? ' (right)' : ''}`).join(', ') || '—';
      return `| ${cell(x.id)} | ${x.where} | ${x.class} | ${cell(floor)} | ${cell(modelCell(x.model))} | ${x.classedBy} | ${x.model?.wrong ? 'wrong' : ''} |`;
    }),
    '',
  ];
  return out.join('\n');
}

// ---------------------------------------------------------------------------------------------------------
// The runner

function writeAtomic(path, text) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(`${path}.tmp`, text);
  renameSync(`${path}.tmp`, path);
}

const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));

/**
 * Replay a cases file: the floor for every case, the model tier for every case without a kept check that ran to the
 * end (when a harness is named), then the score from every kept check. Resolves `{ code, summary, scored, report,
 * problems }`: 0 scored, 2 a cases file or folder that cannot be scored (nothing run), 3 stopped after `stopAfter`
 * failed checks in a row, keeping what it measured.
 */
export async function runBench({
  casesFile = DEFAULT_CASES, knowledge, harness, cwd, outDir, label = 'run', timeoutMs = 300_000, stopAfter = 3,
  fresh = false, log = (line) => console.log(line),
}) {
  let read;
  try { read = readCases(readFileSync(casesFile, 'utf8'), { base: dirname(resolve(casesFile)) }); } catch (err) {
    return { code: 2, problems: [err.message] };
  }
  const knowledgeDir = knowledge ? resolve(knowledge) : read.knowledge;
  if (!knowledgeDir || !existsSync(knowledgeDir)) {
    return { code: 2, problems: [`no knowledge folder: ${knowledgeDir ?? 'name one with --knowledge or the cases file\'s "knowledge"'}`] };
  }
  const docs = loadKnowledge(knowledgeDir);
  const p = prepare(read, docs);
  if (p.problems.length) return { code: 2, problems: p.problems };

  const index = indexPassages(docs);
  const floors = new Map(p.cases.map((c) => [c.id, floorHits(c.question, index, [c.askDoc])]));
  writeAtomic(join(outDir, 'floor.json'), `${JSON.stringify({ knowledge: knowledgeDir,
    cases: p.cases.map((c) => ({ id: c.id, hits: floors.get(c.id) })) }, null, 2)}\n`);

  const modelDir = join(outDir, 'model');
  if (fresh) rmSync(modelDir, { recursive: true, force: true });
  let code = 0;
  let stopped = null;
  if (harness) {
    let failures = 0;
    for (const c of p.cases) {
      const file = join(modelDir, `${c.id}.json`);
      if (existsSync(file) && !runFailure(readJson(file))) continue;
      const prompt = checkPrompt({ question: c.question, where: c.where, askDoc: c.askDoc, hits: floors.get(c.id), knowledgeDir });
      const at = cwd ? resolve(cwd) : knowledgeDir;
      const run = await runCheck(harness, prompt, { cwd: at, timeoutMs });
      const record = { id: c.id, command: harness, cwd: at, prompt, ...run, stderr: run.stderr.slice(-2000) };
      writeAtomic(file, `${JSON.stringify(record, null, 2)}\n`);
      const failed = runFailure(record);
      log(`checked ${c.id}: ${failed ?? `${parseReply(record.stdout).verdict ?? 'no verdict'}`} (${(run.wallMs / 1000).toFixed(0)} s)`);
      failures = failed ? failures + 1 : 0;
      if (failures >= stopAfter) { stopped = `${failures} failed checks in a row`; code = 3; break; }
    }
  }

  const records = new Map();
  if (existsSync(modelDir)) {
    for (const c of p.cases) {
      const file = join(modelDir, `${c.id}.json`);
      if (existsSync(file)) records.set(c.id, readJson(file));
    }
  }
  const scored = p.cases.map((c) => scoreCase(c, floors.get(c.id), records.get(c.id) ?? null, docs));
  const summary = summarize(scored);
  const commands = [...new Set([...records.values()].map((r) => r.command))];
  const report = renderReport({ label, scored, summary, knowledgeCount: docs.length, commands, stopped });
  writeAtomic(join(outDir, 'report.md'), report);
  writeAtomic(join(outDir, 'results.json'), `${JSON.stringify({ summary, scored }, null, 2)}\n`);
  return { code, summary, scored, report, problems: [] };
}

function argOf(argv, flag, fallback) {
  const i = argv.indexOf(flag);
  return i >= 0 && argv[i + 1] !== undefined ? argv[i + 1] : fallback;
}

const USAGE = 'usage: question-bench.mjs run [--cases <file>] [--knowledge <dir>] [--harness "<command line>"] [--cwd <dir>]\n'
  + '         [--label <name>] [--timeout <seconds>] [--stop-after <n>] [--fresh]\n'
  + '       the harness may come from QUESTION_BENCH_HARNESS; with none, only the floor runs.\n';

async function main(argv) {
  const [command, ...rest] = argv;
  if (command !== 'run') { process.stderr.write(USAGE); return 2; }
  const label = argOf(rest, '--label', 'run');
  if (!/^[\w.-]+$/.test(label)) { process.stderr.write(`--label ${label}: letters, digits, dot and dash only\n`); return 2; }
  const result = await runBench({
    casesFile: resolve(argOf(rest, '--cases', DEFAULT_CASES)),
    knowledge: argOf(rest, '--knowledge'),
    harness: argOf(rest, '--harness', process.env.QUESTION_BENCH_HARNESS) || undefined,
    cwd: argOf(rest, '--cwd'),
    outDir: join(REPO, 'local', 'scratch', 'question-bench', label),
    label,
    timeoutMs: Number(argOf(rest, '--timeout', '300')) * 1000,
    stopAfter: Number(argOf(rest, '--stop-after', '3')),
    fresh: rest.includes('--fresh'),
  });
  if (result.problems.length) { process.stderr.write(`${result.problems.join('\n')}\n`); return 2; }
  process.stdout.write(result.report);
  return result.code;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => { process.exitCode = code; }, (err) => {
    process.stderr.write(`${err.stack ?? err}\n`);
    process.exitCode = 2;
  });
}
