/**
 * REVIEWLOCK1: every `git status` and `git diff` the driver asks passes `--no-optional-locks` (FIX-LOG 2026-10-23, REVIEWLOCK1).
 * QUEUELOCK1: so does every one the tools ask (FIX-LOG 2026-10-28, QUEUELOCK1).
 *
 *   node tools/optional-locks.mjs        check the driver's sources and the tools' (exit 0 clean, 1 breach, 2 tool error)
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
 *
 * QUEUELOCK1: the same census reads the tools' own scripts (`tools/*.mjs`, their tests skipped), where the merge queue's runner
 * asks git of the main checkout: a stale `index.lock` there held a green candidate's promotion for about 1 h 45 min, and the
 * runner's own clean-checkout look took optional locks. In a script an array literal is the expression, its strings in single,
 * double or plain template quotes, and `diff-index` and `diff-files` count beside `status` and `diff`: the commands that read a
 * tree and may write its index. A read of commits alone (`log`, `rev-parse`, `show`) is none.
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
export const TOOLS = 'tools';
const COMMANDS = new Set(['status', 'diff', 'diff-index', 'diff-files']);
const VALUED = new Set(['-c', '-C', '--git-dir', '--work-tree', '--namespace']);
// In a script an array after one of these words is a value, not an indexer.
const AHEAD = /\b(?:return|yield|await|case|else|of|in|typeof|void|throw)$/;
const SHOWN =/\bGitRefs\s*\.\s*Command\s*\(\s*$/;

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

/** A plain string literal's value, or null for anything else (a variable, a call, an interpolation); in a script `'…'` and a template with no `${` are plain too. */
function literal(element, script) {
  const m = /^"((?:[^"\\]|\\.)*)"$/.exec(element)
    ?? (script ? /^'((?:[^'\\]|\\.)*)'$/.exec(element) ?? /^`((?:[^`\\$]|\\.|\$(?!\{))*)`$/.exec(element) : null);
  return m ? m[1] : null;
}

/**
 * Each git status or diff in blanked `text` (raw `raw` beside it) with no `--no-optional-locks` before its command:
 * `{ index, command }`, `index` at the command's word. `script` reads a JavaScript source, not a C# one.
 */
export function unlocked(text, raw, script = false) {
  const found = [];
  for (let open = text.indexOf('['); open >= 0; open = text.indexOf('[', open + 1)) {
    const close = closing(text, open);
    if (close < 0) continue;
    // An indexer (`said["status"]`, `parts?[0]`) follows what it indexes; a collection expression follows no value.
    const before = text.slice(0, open).trimEnd();
    if (/[\w\])?!]$/.test(before) && !(script && AHEAD.test(before))) continue;
    if (script && /\bnew\s+Set\s*\($/.test(before)) continue;
    if (SHOWN.test(text.slice(Math.max(0, open - 80), open))) continue;
    const words = elements(text, raw, open + 1, close);
    let flagged = false;
    for (let at = 0; at < words.length; at++) {
      const word = literal(words[at].raw, script);
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

/**
 * `src` with its comments, strings, templates and regular expressions blanked to spaces (newlines kept), as `blank` does a C#
 * source: a script's `'status'` inside a quote or a `/\[status\]/` is no array. A `/` begins a regular expression after an
 * operator, a bracket or one of `AHEAD`'s words, and is a division after a value.
 */
export function blankScript(src) {
  let out = '';
  let i = 0;
  const n = src.length;
  const gap = (from, to) => src.slice(from, to).replace(/[^\r\n]/g, ' ');
  const afterValue = () => {
    const seen = out.trimEnd();
    return /[\w)\]}$]$/.test(seen) && !AHEAD.test(seen);
  };
  while (i < n) {
    const c = src[i];
    const d = src[i + 1];
    let end = -1;
    if (c === '/' && d === '/') {
      end = src.indexOf('\n', i);
      if (end < 0) end = n;
    } else if (c === '/' && d === '*') {
      end = src.indexOf('*/', i + 2);
      end = end < 0 ? n : end + 2;
    } else if (c === '"' || c === "'" || c === '`') {
      end = i + 1;
      let depth = 0;
      while (end < n) {
        if (src[end] === '\\') end += 2;
        else if (c === '`' && src[end] === '$' && src[end + 1] === '{') {
          depth++;
          end += 2;
        } else if (c === '`' && depth > 0 && src[end] === '}') {
          depth--;
          end++;
        } else if (src[end] === c && depth === 0) {
          end++;
          break;
        } else if (src[end] === '\n' && c !== '`') break;
        else end++;
      }
    } else if (c === '/' && !afterValue()) {
      end = i + 1;
      let inClass = false;
      while (end < n && src[end] !== '\n') {
        const at = src[end];
        end += at === '\\' ? 2 : 1;
        if (at === '[') inClass = true;
        else if (at === ']') inClass = false;
        else if (at === '/' && !inClass) {
          while (/[a-z]/.test(src[end] ?? '')) end++;
          break;
        }
      }
    }
    if (end < 0) {
      out += c;
      i++;
    } else {
      end = Math.min(end, n);
      out += gap(i, end);
      i = end;
    }
  }
  return out;
}

/** The files under `dir` that `keep` takes, as `{ rel, raw, text }` against `root`, `text` blanked of comments and strings by `blanker`. */
function readFiles(root, dir, keep, blanker) {
  return [...sources(join(root, dir), keep)].map((file) => {
    const raw = readFileSync(file, 'utf8');
    return { rel: file.slice(root.length + 1).replaceAll('\\', '/'), raw, text: blanker(raw) };
  });
}

/** The driver's sources under `root`, each `{ rel, raw, text }` with `text` blanked of comments and strings. */
function read(root, dir) {
  return readFiles(root, dir, (name) => name.endsWith('.cs'), blank)
    .filter(({ rel }) => !rel.split('/').some((part) => part.endsWith('.Tests')));
}

/** The tools' scripts under `root`, tests skipped, each `{ rel, raw, text }` as `read` gives them. */
function readTools(root, dir) {
  return readFiles(root, dir, (name) => name.endsWith('.mjs') && !name.endsWith('.test.mjs'), blankScript);
}

/** The breaches under `root` (`{ problems, marked }`): a git status or diff asked without `--no-optional-locks`. */
export function checkOptionalLocks(root, dir = DRIVER) {
  return breaches(read(root, dir), false);
}

/** The same census over the tools' scripts under `root`'s `dir` (QUEUELOCK1). */
export function checkToolLocks(root, dir = TOOLS) {
  return breaches(readTools(root, dir), true);
}

function breaches(files, script) {
  const problems = [];
  let marked = 0;
  for (const { rel, raw, text } of files) {
    const lines = raw.split('\n');
    for (const { index, command } of unlocked(text, raw, script)) {
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
    const driver = checkOptionalLocks(root);
    const tools = checkToolLocks(root);
    const problems = [...driver.problems, ...tools.problems];
    const marked = driver.marked + tools.marked;
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
