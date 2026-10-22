/**
 * REVIEWLOCK1: every `git status` and `git diff` the driver asks passes `--no-optional-locks` (FIX-LOG 2026-10-23, REVIEWLOCK1).
 *
 *   node tools/optional-locks.mjs        check the driver's sources (exit 0 clean, 1 breach, 2 tool error)
 *
 * A status, and a diff against a working tree, refresh the index's stat data and write it back where they can, taking
 * `index.lock` to do so: opening Review in a tree a session is writing in could then hold the lock the session's own `git add`
 * or `git commit` needs, and fail it. `--no-optional-locks` keeps the read a read. The census reads the driver's C# (tests,
 * `bin/` and `obj/` skipped) for each collection expression of git's arguments, `[ … ]`, whose command is `status` or `diff`:
 * git's own options before the command are skipped (`-c` and `-C` with their value), and the command is the first word that is
 * no option. One without `--no-optional-locks` before its command is a breach. An expression the census cannot read to its
 * command (a variable or a call before it) is not one, nor is a command only shown to a person (`GitRefs.Command([…])`).
 * A status or a diff that means to write the index marks its command's line `// optional-locks: <why>`, and the clean line
 * counts the marks. A word inside a comment is not one. There is no .NET build.
 */
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { blank } from './partials.mjs';

export const DRIVER = 'src/Daoris.Desktop/Daoris.Desktop.Driver';
const SKIPPED = new Set(['bin', 'obj', 'node_modules']);
const MARK = '// optional-locks:';
const FLAG = '--no-optional-locks';
const COMMANDS = new Set(['status', 'diff']);
const VALUED = new Set(['-c', '-C', '--git-dir', '--work-tree', '--namespace']);
const SHOWN = /\bGitRefs\s*\.\s*Command\s*\(\s*$/;

function* sources(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIPPED.has(entry.name)) yield* sources(full);
    } else if (entry.name.endsWith('.cs')) {
      yield full;
    }
  }
}

/** The index of the `]` closing the `[` at `open` in blanked `text`, or -1. */
function closing(text, open) {
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '[' || text[i] === '(' || text[i] === '{') depth++;
    else if ((text[i] === ']' || text[i] === ')' || text[i] === '}') && --depth === 0) return text[i] === ']' ? i : -1;
  }
  return -1;
}

/** The top-level elements of the expression between `from` and `to`: `{ index, raw }`, split at the commas blanked `text` shows. */
function elements(text, raw, from, to) {
  const found = [];
  let depth = 0;
  let start = from;
  for (let i = from; i <= to; i++) {
    const c = text[i];
    if (i === to || (c === ',' && depth === 0)) {
      // A comment beside an element, on its own line or after the comma before it, is no part of it.
      const piece = raw.slice(start, i).replace(/^\s*\/\/[^\n]*(?:\n|$)/gm, '').replace(/\/\*[\s\S]*?\*\//g, '').trim();
      if (piece) found.push({ index: raw.indexOf(piece, start), raw: piece });
      start = i + 1;
    } else if (c === '[' || c === '(' || c === '{') depth++;
    else if (c === ']' || c === ')' || c === '}') depth--;
  }
  return found;
}

/** A plain string literal's value, or null for anything else (a variable, a call, an interpolation). */
function literal(element) {
  const m = /^"((?:[^"\\]|\\.)*)"$/.exec(element);
  return m ? m[1] : null;
}

/**
 * Each git status or diff in blanked `text` (raw `raw` beside it) with no `--no-optional-locks` before its command:
 * `{ index, command }`, `index` at the command's word.
 */
export function unlocked(text, raw) {
  const found = [];
  for (let open = text.indexOf('['); open >= 0; open = text.indexOf('[', open + 1)) {
    const close = closing(text, open);
    if (close < 0) continue;
    // An indexer (`said["status"]`, `parts?[0]`) follows what it indexes; a collection expression follows no value.
    if (/[\w\])?!]$/.test(text.slice(0, open).trimEnd())) continue;
    if (SHOWN.test(text.slice(Math.max(0, open - 80), open))) continue;
    const words = elements(text, raw, open + 1, close);
    let flagged = false;
    for (let at = 0; at < words.length; at++) {
      const word = literal(words[at].raw);
      if (word === null) break;
      if (word === FLAG) flagged = true;
      if (VALUED.has(word)) {
        at++;
        continue;
      }
      if (word.startsWith('-')) continue;
      if (COMMANDS.has(word) && !flagged) found.push({ index: words[at].index, command: word });
      break;
    }
  }
  return found;
}

/** The driver's sources under `root`, each `{ rel, raw, text }` with `text` blanked of comments and strings. */
function read(root, dir) {
  return [...sources(join(root, dir))]
    .filter((file) => !file.slice(root.length + 1).replaceAll('\\', '/').split('/').some((part) => part.endsWith('.Tests')))
    .map((file) => {
      const raw = readFileSync(file, 'utf8');
      return { rel: file.slice(root.length + 1).replaceAll('\\', '/'), raw, text: blank(raw) };
    });
}

/** The breaches under `root` (`{ problems, marked }`): a git status or diff asked without `--no-optional-locks`. */
export function checkOptionalLocks(root, dir = DRIVER) {
  const problems = [];
  let marked = 0;
  for (const { rel, raw, text } of read(root, dir)) {
    const lines = raw.split('\n');
    for (const { index, command } of unlocked(text, raw)) {
      const line = text.slice(0, index).split('\n').length;
      if (lines[line - 1].includes(MARK)) {
        marked++;
        continue;
      }
      problems.push(`${rel}:${line}: git ${command} without ${FLAG}: pass it before the command, so the read takes no index.lock, or mark the line \`${MARK} <why it writes>\``);
    }
  }
  return { problems, marked };
}

function main() {
  const root = dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    const { problems, marked } = checkOptionalLocks(root);
    for (const p of problems) console.error(`optional-locks: ${p}`);
    if (problems.length) return 1;
    console.log(`optional-locks: clean (${marked} marked writes)`);
    return 0;
  } catch (e) {
    console.error(`optional-locks: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main();
