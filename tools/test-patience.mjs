/**
 * TESTPATIENCE3: the TypeScript tests wait on their signals by one shared patience (FIX-LOG 2026-10-28, TESTPATIENCE3).
 *
 *   node tools/test-patience.mjs          check the three test trees (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/test-patience.mjs --list   the same, then each file's count of waits on the patience and of waits kept
 *
 * A bound that waits on a signal (a stub's or a plugin's answer, a file or a process a child is to produce or end, a poll's
 * condition) elapses only when the code is wrong, so it costs a passing test nothing, and a few seconds of one's own failed
 * merge-queue gates beside a loaded machine. Such a wait reads `PATIENCE`, which `src/Daoris.Cli/test/_fixture.ts` exports,
 * as the desktop tests read `Poll.Patience` (TESTPATIENCE1-2). The census reads the CLI's tests, the tool-tests and the tools'
 * own tests (`tools/*.test.mjs`) for a fixed wait of a second or more and under the patience, in the shapes a wait is written:
 *
 *   - a key: `timeout`, `timeoutMs`, `waitMs`, `wait`, `within`, `limitMs`, `bound`, `boundMs`, `patience`, `deadline` or `start`
 *     given a number (`{ timeout: 30_000 }`, `wait = 10_000`), and a parameter's default `ms = 5000`;
 *   - a constant named for a wait (`STUB_WAIT_MS = 10_000`: WAIT, HANG, TIMEOUT, BOUND, PATIENCE, LIMIT or DEADLINE in its name);
 *   - the last argument of `setTimeout`, `sleep`, `pause`, `until`, `waitFor`, `within`, `treePids`, `stillRunning`,
 *     `bridgeCall`, `invokeInPage`, `delay` or `AbortSignal.timeout`;
 *   - a deadline, `Date.now() + 30_000`, and a loop `waited < 10_000`.
 *
 * A number is `5000`, `5_000` or a product of them (`2 * 60_000`). Under a second is no wait here: a poll's step, a staging
 * delay, a bound a row means to elapse. A site that stays short marks its line, or the line above it, `// patience: <kind> —
 * <why>`, the kind one of: `claim` (an elapsed-time bound that states what the test asserts), `staging` (a delay that sets a
 * scene, or a window in which nothing should happen) or `input` (a product input the test hands to the code it tests). A word
 * in a comment or a string is no wait. The clean line counts the waits on the patience and the marks.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PATIENCE } from '../src/Daoris.Cli/test/_fixture.ts';
import { isMain } from './fsx.mjs';
import { blankScript } from './optional-locks.mjs';

/** Where the tests are: each folder with what names a test file in it. */
export const TREES = [
  { dir: 'src/Daoris.Cli/test', keep: (name) => name.endsWith('.ts') },
  { dir: 'src/Daoris.Cli/tool-test', keep: (name) => name.endsWith('.ts') },
  { dir: 'tools', keep: (name) => name.endsWith('.test.mjs') },
];
const SKIPPED = new Set(['node_modules', 'bin', 'obj', 'fixtures']);
export const KINDS = ['claim', 'staging', 'input'];
const MARK = /\/\/\s*patience:\s*(claim|staging|input)\b\s*\S/;

const NUMBER = String.raw`\d[\d_]*(?:\s*\*\s*\d[\d_]*)*(?![\w.])(?!\s*[*/+\-]\s*[\w(])`;
const KEYS = 'timeout|timeoutMs|waitMs|wait|within|limitMs|bound|boundMs|patience|deadline|start';
const CALLS = 'setTimeout|sleep|pause|until|waitFor|within|treePids|stillRunning|bridgeCall|invokeInPage|delay|AbortSignal\\.timeout';
const SHAPES = [
  new RegExp(String.raw`(?<!\w)(?:${KEYS})\s*[:=]\s*(${NUMBER})`, 'g'),
  new RegExp(String.raw`(?<![.\w])ms\s*=\s*(${NUMBER})`, 'g'),
  new RegExp(String.raw`\b[A-Z0-9_]*(?:WAIT|HANG|TIMEOUT|BOUND|PATIENCE|LIMIT|DEADLINE)[A-Z0-9_]*\s*=\s*(${NUMBER})`, 'g'),
  new RegExp(String.raw`Date\.now\(\)\s*\+\s*(${NUMBER})`, 'g'),
  new RegExp(String.raw`\bwaited\s*<\s*(${NUMBER})`, 'g'),
];
const CALL = new RegExp(String.raw`\b(?:${CALLS})\s*\(`, 'g');
const WHOLE = new RegExp(String.raw`^(${NUMBER})$`);

/** The milliseconds a number's text says: `5_000` is 5000 and `2 * 60_000` is 120000. */
const value = (text) => text.split('*').reduce((product, part) => product * Number(part.trim().replaceAll('_', '')), 1);

/** The index of the `)` closing the `(` at `open` in blanked `text`, or -1. */
function closing(text, open) {
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '(' || text[i] === '[' || text[i] === '{') depth++;
    else if ((text[i] === ')' || text[i] === ']' || text[i] === '}') && --depth === 0) return text[i] === ')' ? i : -1;
  }
  return -1;
}

/** Where a call's last top-level argument starts and what it says. */
function lastArgument(text, open, close) {
  let depth = 0;
  let from = open + 1;
  for (let i = open + 1; i < close; i++) {
    const c = text[i];
    if (c === '(' || c === '[' || c === '{') depth++;
    else if (c === ')' || c === ']' || c === '}') depth--;
    else if (c === ',' && depth === 0) from = i + 1;
  }
  const raw = text.slice(from, close);
  return { at: from + raw.length - raw.trimStart().length, text: raw.trim() };
}

/** Each fixed wait in blanked `text`: `{ at, ms }`, `at` where its number starts. */
export function waits(text) {
  const found = [];
  for (const shape of SHAPES) {
    for (const m of text.matchAll(shape)) found.push({ at: m.index + m[0].lastIndexOf(m[1]), ms: value(m[1]) });
  }
  for (const m of text.matchAll(CALL)) {
    const open = m.index + m[0].length - 1;
    const close = closing(text, open);
    const last = close < 0 ? null : lastArgument(text, open, close);
    if (last && WHOLE.test(last.text)) found.push({ at: last.at, ms: value(last.text) });
  }
  return found;
}

function* sources(dir, keep) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* sources(full, keep);
    } else if (keep(entry.name)) {
      yield full;
    }
  }
}

/**
 * The breaches under `root`: `{ problems, marked, uses, files }`. `marked` counts the kept waits by kind, `uses` the places
 * that name `PATIENCE`, `files` each file's `{ uses, marked }`. `patience` is the bound a wait must reach.
 */
export function checkTestPatience(root, patience = PATIENCE) {
  const problems = [];
  const marked = Object.fromEntries(KINDS.map((kind) => [kind, 0]));
  const files = new Map();
  let uses = 0;
  for (const { dir, keep } of TREES) {
    for (const file of sources(join(root, dir), keep)) {
      const raw = readFileSync(file, 'utf8');
      const rel = file.slice(root.length + 1).replaceAll('\\', '/');
      const text = blankScript(raw);
      const lines = raw.split('\n');
      const seen = { uses: 0, marked: 0 };
      // The import and the definition name the patience without waiting on it.
      for (const line of text.split('\n')) if (!/^\s*(?:import\b|export const PATIENCE\b)/.test(line)) seen.uses += (line.match(/\bPATIENCE\b/g) ?? []).length;
      for (const { at, ms } of waits(text)) {
        if (ms < 1000 || ms >= patience) continue;
        const line = text.slice(0, at).split('\n').length;
        const kept = [lines[line - 1], /^\s*\/\//.test(lines[line - 2] ?? '') ? lines[line - 2] : ''].map((each) => MARK.exec(each ?? '')).find(Boolean);
        if (kept) {
          marked[kept[1]]++;
          seen.marked++;
        } else {
          problems.push(`${rel}:${line}: a fixed wait of ${ms} ms, under the shared patience of ${patience} ms: wait by PATIENCE (src/Daoris.Cli/test/_fixture.ts), or mark the line \`// patience: claim|staging|input — <why it stays>\``);
        }
      }
      uses += seen.uses;
      files.set(rel, seen);
    }
  }
  return { problems, marked, uses, files };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    if (!Number.isInteger(PATIENCE) || PATIENCE < 1000) throw new Error(`PATIENCE in src/Daoris.Cli/test/_fixture.ts is ${PATIENCE}, not a number of milliseconds`);
    const { problems, marked, uses, files } = checkTestPatience(root);
    for (const p of problems) console.error(`test-patience: ${p}`);
    if (problems.length) return 1;
    const kept = KINDS.map((kind) => `${marked[kind]} ${kind}`).join(', ');
    console.log(`test-patience: clean (${uses} waits on PATIENCE, ${PATIENCE} ms; ${kept} kept)`);
    if (process.argv.includes('--list')) {
      for (const [rel, seen] of [...files].filter(([, seen]) => seen.uses || seen.marked).sort(([a], [b]) => a.localeCompare(b))) {
        console.log(`  ${rel}: ${seen.uses} on PATIENCE, ${seen.marked} kept`);
      }
    }
    return 0;
  } catch (e) {
    console.error(`test-patience: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
