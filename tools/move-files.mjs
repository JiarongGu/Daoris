/**
 * MOVETOOL1: files moved with `git mv`, and every relative import of them respelled.
 *
 *   node tools/move-files.mjs [--plan] [--root <dir>] [--moves <file.json>] [--expect-moved N]
 *                             [--expect-rewritten N] [--expect-files N] [from=to ...]
 *
 * The CLI's and the page's restructures (STRUCT5c, STRUCT5d) each built a scratch script for this; this is the one that
 * stays. A move is `from=to` on the command line, or an entry of the JSON file: `{ "moves": [["a.ts", "b/a.ts"], ...],
 * "expect": { "moved": 1, "rewritten": 4, "files": 3 } }` (a bare array, or `{ "from": ..., "to": ... }` entries, work too).
 * A `from` that names a folder moves every tracked file under it. The root is the git work tree's top (default: the
 * current folder).
 *
 * Every relative specifier in the tree's JS and TS sources (`from '...'`, `import('...')`, `import '...'`,
 * `require('...')`, `vi.mock('...')`) is resolved before anything moves, to a tracked file. After the moves, a specifier
 * in a moved file, or naming a moved file, is respelled to reach the same file from where the importer now stands, in the
 * form it had: with the extension as written (`.ts`, or `.js` for a `.ts` target), without one, or as a folder's index.
 * One that did not resolve before is left alone and reported. Nothing is written until the counts hold.
 *
 * Counts: `--expect-moved`, `--expect-rewritten` (specifiers) and `--expect-files` (files whose text changed) assert what
 * the plan found; a mismatch stops before any change. After the run, every relative specifier that resolved before must
 * resolve to the same file, by its new place, from disk; a failure leaves the work for `git diff` and exits 1.
 *
 * `--plan` prints the plan and changes nothing. Exit 0 clean, 1 a policy failure (a bad move, a count that differs, an
 * import that does not resolve after), 2 a tool error (usage, unreadable input, git failing).
 */
import { mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { isMain, repositoryFiles, spawnGit, writeAtomic } from './fsx.mjs';

const SOURCE = /\.(?:[cm]?[jt]s|[jt]sx)$/;
const EXTENSIONS = ['.ts', '.tsx', '.mts', '.cts', '.js', '.mjs', '.cjs', '.jsx', '.json', '.css'];
const SPECIFIER = /(\b(?:from|import|require|mock|doMock|unmock|importActual)\s*\(?\s*)(['"])(\.\.?(?:\/[^'"\r\n]*)?)\2/g;
const JS_FOR = { '.ts': '.js', '.tsx': '.jsx', '.mts': '.mjs', '.cts': '.cjs' };
const JS_FOR_REVERSE = {};
for (const [real, js] of Object.entries(JS_FOR)) (JS_FOR_REVERSE[js] ??= []).push(real);

/** A failure of the move's own rules, for exit 1. */
export class MoveRefused extends Error {}

const extOf = (path) => posix.extname(path);
const withoutExt = (path) => path.slice(0, path.length - extOf(path).length);

/** The tracked file a specifier names from `importer`, and how it was spelled, or null. `files` is a Set of paths. */
export function resolveSpecifier(files, importer, specifier) {
  const joined = posix.join(posix.dirname(importer), specifier);
  if (files.has(joined)) return { target: joined, form: 'exact' };
  const ext = extOf(joined);
  if (JS_FOR_REVERSE[ext]) {
    for (const real of JS_FOR_REVERSE[ext]) {
      if (files.has(withoutExt(joined) + real)) return { target: withoutExt(joined) + real, form: 'js' };
    }
  }
  for (const e of EXTENSIONS) if (files.has(joined + e)) return { target: joined + e, form: 'bare' };
  for (const e of EXTENSIONS) if (files.has(`${joined}/index${e}`)) return { target: `${joined}/index${e}`, form: 'index' };
  return null;
}

/** The relative specifier from the folder of `from` to `path` (a path in the tree). */
function relative(from, path) {
  const value = posix.relative(posix.dirname(from), path) || '.';
  return value.startsWith('.') ? value : `./${value}`;
}

/** The specifier that reaches `newTarget` from `importer` in the form `form`. */
function respell(form, importer, newTarget) {
  if (form === 'exact') return relative(importer, newTarget);
  if (form === 'js') return relative(importer, withoutExt(newTarget) + (JS_FOR[extOf(newTarget)] ?? extOf(newTarget)));
  if (form === 'index' && posix.basename(newTarget).startsWith('index.')) return relative(importer, posix.join(posix.dirname(newTarget), 'index')).replace(/\/index$/, '');
  return relative(importer, withoutExt(newTarget));
}

/** The relative specifiers of a source's text, as `{ index, length, specifier }` of the text inside the quotes. */
export function specifiersOf(text) {
  const found = [];
  for (const match of text.matchAll(new RegExp(SPECIFIER.source, 'g'))) {
    const index = match.index + match[1].length + 1;
    found.push({ index, specifier: match[3] });
  }
  return found;
}

/** Normalizes the moves given in any of the accepted shapes to `{ moves: [[from, to]], expect }`. */
export function readMoves(input) {
  const doc = Array.isArray(input) ? { moves: input } : input;
  if (!doc || !Array.isArray(doc.moves)) throw new MoveRefused('the moves are an array, or an object with a "moves" array');
  const moves = doc.moves.map((entry) => {
    const pair = Array.isArray(entry) ? entry : [entry?.from, entry?.to];
    if (typeof pair[0] !== 'string' || typeof pair[1] !== 'string') throw new MoveRefused(`not a move: ${JSON.stringify(entry)}`);
    return [pair[0].replace(/\\/g, '/').replace(/\/+$/, ''), pair[1].replace(/\\/g, '/').replace(/\/+$/, '')];
  });
  return { moves, expect: doc.expect ?? {} };
}

/**
 * The plan, from the tracked tree and the moves: `map` (old path to new, every file), `edits` (the new text of each file
 * whose text changes, keyed by its OLD path), `rewritten` (specifiers changed), `unresolved` (relative specifiers that
 * named no tracked file before) and `expectations` (every resolved specifier, to check after).
 */
export function planMoves(root, input, { env = process.env, read = (path) => readFileSync(join(root, path), 'utf8') } = {}) {
  const { moves, expect } = readMoves(input);
  const tracked = repositoryFiles(root, { env });
  const files = new Set(tracked);
  const map = new Map();
  for (const [from, to] of moves) {
    const under = files.has(from) ? [from] : tracked.filter((path) => path.startsWith(`${from}/`));
    if (under.length === 0) throw new MoveRefused(`${from} is not a tracked file or folder`);
    for (const path of under) {
      const target = path === from && files.has(from) ? to : to + path.slice(from.length);
      if (map.has(path)) throw new MoveRefused(`${path} is moved twice`);
      map.set(path, target);
    }
  }
  const targets = new Map();
  for (const [from, to] of map) {
    if (targets.has(to)) throw new MoveRefused(`${targets.get(to)} and ${from} both move to ${to}`);
    targets.set(to, from);
    if (files.has(to) && !map.has(to)) throw new MoveRefused(`${to} exists and is not itself moved`);
  }
  const placed = (path) => map.get(path) ?? path;

  const edits = new Map();
  const unresolved = [];
  const expectations = [];
  let rewritten = 0;
  for (const file of tracked) {
    if (!SOURCE.test(file)) continue;
    const text = read(file);
    const now = placed(file);
    let out = '';
    let last = 0;
    for (const { index, specifier } of specifiersOf(text)) {
      const hit = resolveSpecifier(files, file, specifier);
      if (!hit) {
        unresolved.push(`${file}: ${specifier}`);
        continue;
      }
      const next = respell(hit.form, now, placed(hit.target));
      expectations.push({ importer: now, specifier: next, target: placed(hit.target), from: file, old: specifier });
      if (next === specifier) continue;
      out += text.slice(last, index) + next;
      last = index + specifier.length;
      rewritten += 1;
    }
    if (last > 0) edits.set(file, out + text.slice(last));
  }
  return { moves, map, edits, rewritten, unresolved, expectations, expect };
}

/** Throws MoveRefused when an asserted count differs from the plan's. */
export function assertCounts(plan, expect) {
  const counts = { moved: plan.map.size, rewritten: plan.rewritten, files: plan.edits.size };
  const wrong = Object.entries(expect)
    .filter(([name, value]) => name in counts && counts[name] !== value)
    .map(([name, value]) => `${name}: expected ${value}, found ${counts[name]}`);
  const unknown = Object.keys(expect).filter((name) => !(name in counts));
  if (unknown.length) throw new MoveRefused(`unknown count ${unknown.join(', ')} (moved, rewritten, files)`);
  if (wrong.length) throw new MoveRefused(`the counts differ: ${wrong.join('; ')}`);
}

/** The plan as lines for a person. */
export function describe(plan) {
  const lines = [...plan.map].map(([from, to]) => `move ${from} -> ${to}`);
  for (const file of plan.edits.keys()) lines.push(`edit ${plan.map.get(file) ?? file}`);
  for (const line of plan.unresolved) lines.push(`unresolved before, left alone: ${line}`);
  lines.push(`${plan.map.size} moved, ${plan.rewritten} specifiers rewritten in ${plan.edits.size} files`);
  return lines;
}

function git(root, args, env) {
  const run = spawnGit(args, { cwd: root, encoding: 'utf8', windowsHide: true, env });
  if (run.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${(run.stderr || run.error?.message || '').trim()}`);
}

/** Runs the plan: `git mv` each file, writes the changed text, then checks every import from disk. Returns the failures. */
export function applyPlan(root, plan, { env = process.env } = {}) {
  for (const [from, to] of plan.map) {
    mkdirSync(dirname(join(root, to)), { recursive: true });
    git(root, ['mv', '--', from, to], env);
  }
  for (const [file, text] of plan.edits) writeAtomic(join(root, plan.map.get(file) ?? file), text);
  return verifyPlan(root, plan, { env });
}

/** Re-reads the tree from disk; every specifier that resolved before must resolve to its target's new place. */
export function verifyPlan(root, plan, { env = process.env } = {}) {
  const files = new Set(repositoryFiles(root, { env }));
  const failures = [];
  for (const { importer, specifier, target, old, from } of plan.expectations) {
    const hit = files.has(importer) ? resolveSpecifier(files, importer, specifier) : null;
    if (!hit) failures.push(`${importer}: ${specifier} (was ${old} in ${from}) resolves to nothing`);
    else if (hit.target !== target) failures.push(`${importer}: ${specifier} resolves to ${hit.target}, not ${target}`);
  }
  return failures;
}

function parseArgs(argv) {
  const options = { plan: false, root: process.cwd(), movesFile: null, expect: {}, pairs: [] };
  const counts = { '--expect-moved': 'moved', '--expect-rewritten': 'rewritten', '--expect-files': 'files' };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--plan') options.plan = true;
    else if (arg === '--root') options.root = argv[++i];
    else if (arg === '--moves') options.movesFile = argv[++i];
    else if (counts[arg]) {
      const value = Number(argv[++i]);
      if (!Number.isInteger(value) || value < 0) throw new TypeError(`${arg} takes a count`);
      options.expect[counts[arg]] = value;
    } else if (!arg.startsWith('-') && arg.includes('=')) {
      const at = arg.indexOf('=');
      options.pairs.push([arg.slice(0, at), arg.slice(at + 1)]);
    } else throw new TypeError(`unknown argument ${arg}`);
  }
  if (!options.root) throw new TypeError('--root takes a folder');
  if (options.movesFile === null && options.pairs.length === 0) throw new TypeError('give moves as from=to, or --moves <file.json>');
  return options;
}

/** The tool as a function: returns the exit code, writing to `out` and `err`. */
export function main(argv, { out = (line) => console.log(line), err = (line) => console.error(line), env = process.env } = {}) {
  let options;
  let input;
  try {
    options = parseArgs(argv);
    input = options.movesFile === null ? { moves: [] } : JSON.parse(readFileSync(options.movesFile, 'utf8'));
  } catch (error) {
    err(`move-files: ${error.message}`);
    return 2;
  }
  try {
    const base = readMoves(input);
    const plan = planMoves(options.root, { moves: [...base.moves, ...options.pairs], expect: { ...base.expect, ...options.expect } }, { env });
    for (const line of describe(plan)) out(line);
    assertCounts(plan, plan.expect);
    if (options.plan) return 0;
    const failures = applyPlan(options.root, plan, { env });
    for (const failure of failures) err(`move-files: ${failure}`);
    return failures.length ? 1 : 0;
  } catch (error) {
    err(`move-files: ${error.message}`);
    return error instanceof MoveRefused ? 1 : 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
