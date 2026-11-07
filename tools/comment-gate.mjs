/**
 * Comments shrink where code is touched, and name no row, decision or history anywhere (the gate of the comment rules in the area document on code structure).
 *
 *   node tools/comment-gate.mjs                    the working tree against its merge-base with main (exit 0 clean, 1 breach, 2 tool error)
 *   node tools/comment-gate.mjs --base <ref>       against the merge-base with <ref> instead of main
 *   node tools/comment-gate.mjs --tip <ref>        the change from the merge-base to <ref> (a merge's diff) instead of the working tree
 *   node tools/comment-gate.mjs --root <dir>       the repository at <dir> instead of the one the tool sits in
 *   node tools/comment-gate.mjs --all              every source file of the work tree, held to the naming rule alone (no base, no share)
 *
 * For each source file the change touches (`.cs`, `.ts`, `.tsx`, `.mjs`, `.js`; generated and build output skipped; a `*.template`
 * file under `src/` is read as the source its extension before `.template` names):
 *
 * 1. The share of its non-blank lines that are comment lines does not rise. A comment line has nothing on it but a comment:
 *    a line comment, or a line of a block comment. A renamed file is held against its old self; a new file has no old
 *    share, so only the second rule holds it.
 * 2. No comment it adds names a backlog row id (capitals then digits), a decision number (the letter D and
 *    digits) or a history note (the words for what the code once did). The commit or the decision holds those. A comment is
 *    added where its trimmed text stands on the new side more often than on the old, so a moved or untouched comment is not.
 *
 * A comment on a code line counts for the second rule, not the first. Strings, templates, regular expressions and C#'s
 * verbatim and raw strings are not comments. A tool's or a standard's name that is capitals and digits (`UTF8`, `SHA256`) is no id.
 */
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execGit, isMain, repositoryFiles } from './fsx.mjs';

const SOURCE = /\.(cs|ts|tsx|mjs|js)$/;
const SKIPPED = /(^|\/)(bin|obj|node_modules|_fixtures|wwwroot|dist|generated|local)\//;
const MAIN = 'main';
const TEMPLATE = /^src\/.*\.template$/;

/** Whether `path` is source: a source file, or a template under `src/` whose extension before `.template` is a source one. */
export const isSource = (path) => SOURCE.test(TEMPLATE.test(path) ? path.slice(0, -'.template'.length) : path);

const ROW = /\b[A-Z]{2,}\d+[a-z]?\b/g;
const KNOWN = /^(?:UTF|SHA|MD|HTTP|HS|RS|ES|PS|AES|X|ISO|RFC|TLS|SSL|CRC|BASE|GB|PBKDF|SSH|IPV|WIN|MP|H|OS)\d+$/;
const DECISION = /\bD\d{1,4}\b/g;
const HISTORY = /\b(?:was|used to|formerly|previously)\b/gi;

/**
 * The comments of `src`, one entry per line that carries one: `{ line, text, whole, ranges }`, `text` the comment's words on that
 * line, `whole` true where nothing else is on the line and `ranges` the `[from, to)` columns the comment spans there.
 */
export function comments(src) {
  const found = [];
  const lines = src.split(/\r?\n/);
  const starts = [];
  let at = 0;
  for (const l of lines) {
    starts.push(at);
    at += l.length + 1;
  }
  const lineOf = (index) => {
    let lo = 0;
    let hi = starts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (starts[mid] <= index) lo = mid;
      else hi = mid - 1;
    }
    return lo;
  };
  const spans = [];
  const n = src.length;
  let last = '';
  let i = 0;
  const afterValue = () => /[\w)\]}$]$/.test(last) && !/\b(?:return|yield|await|case|else|of|in|typeof|void|throw)$/.test(last);
  const note = (from, to) => {
    for (const ch of src.slice(from, to)) if (!/\s/.test(ch)) last = (last + ch).slice(-12);
  };
  while (i < n) {
    const c = src[i];
    const d = src[i + 1];
    let end = -1;
    if (c === '/' && d === '/') {
      end = src.indexOf('\n', i);
      if (end < 0) end = n;
      spans.push([i, end]);
      i = end;
      continue;
    }
    if (c === '/' && d === '*') {
      end = src.indexOf('*/', i + 2);
      end = end < 0 ? n : end + 2;
      spans.push([i, end]);
      i = end;
      continue;
    }
    if (c === '"' && src.startsWith('"""', i)) {
      let q = 3;
      while (src[i + q] === '"') q++;
      end = src.indexOf('"'.repeat(q), i + q);
      end = end < 0 ? n : end + q;
    } else if (c === '"' || c === "'" || c === '`' || ((c === '$' || c === '@') && /^[$@]*"/.test(src.slice(i, i + 4)))) {
      let j = i;
      let verbatim = false;
      while (src[j] !== '"' && src[j] !== "'" && src[j] !== '`') {
        if (src[j] === '@') verbatim = true;
        j++;
      }
      const quote = src[j];
      if (quote === '"' && src.startsWith('"""', j)) {
        let q = 3;
        while (src[j + q] === '"') q++;
        end = src.indexOf('"'.repeat(q), j + q);
        end = end < 0 ? n : end + q;
      } else {
        end = j + 1;
        let depth = 0;
        while (end < n) {
          if (src[end] === '\\' && !verbatim) end += 2;
          else if (quote === '`' && src[end] === '$' && src[end + 1] === '{') {
            depth++;
            end += 2;
          } else if (quote === '`' && depth > 0 && src[end] === '}') {
            depth--;
            end++;
          } else if (src[end] === quote && depth === 0) {
            if (verbatim && src[end + 1] === quote) end += 2;
            else {
              end++;
              break;
            }
          } else if (src[end] === '\n' && quote !== '`' && !verbatim) break;
          else end++;
        }
      }
    } else if (c === '/' && !afterValue()) {
      end = i + 1;
      let inClass = false;
      while (end < n && src[end] !== '\n') {
        const ch = src[end];
        end += ch === '\\' ? 2 : 1;
        if (ch === '[') inClass = true;
        else if (ch === ']') inClass = false;
        else if (ch === '/' && !inClass) {
          while (/[a-z]/.test(src[end] ?? '')) end++;
          break;
        }
      }
    }
    if (end < 0) {
      note(i, i + 1);
      i++;
    } else {
      end = Math.min(end, n);
      note(i, end);
      last = 'x';
      i = end;
    }
  }
  const byLine = new Map();
  for (const [from, to] of spans) {
    for (let line = lineOf(from); line <= lineOf(Math.max(from, to - 1)); line++) {
      const lo = Math.max(from, starts[line]) - starts[line];
      const hi = Math.min(to, starts[line] + lines[line].length) - starts[line];
      const ranges = byLine.get(line) ?? [];
      ranges.push([lo, hi]);
      byLine.set(line, ranges);
    }
  }
  for (const [line, ranges] of [...byLine].sort(([a], [b]) => a - b)) {
    const text = lines[line];
    const words = ranges.map(([lo, hi]) => text.slice(lo, hi)).join(' ').replace(/\*+\//g, ' ').replace(/\/\*+|^\s*\*+|\/\/+/g, ' ').replace(/\s+/g, ' ').trim();
    const rest = ranges.reduceRight((s, [lo, hi]) => s.slice(0, lo) + s.slice(hi), text);
    found.push({ line: line + 1, text: words, whole: rest.trim() === '', ranges });
  }
  return found;
}

/** `{ comment, total }`: the comment lines and the non-blank lines of `src`. */
export function share(src) {
  const total = src.split(/\r?\n/).filter((l) => l.trim() !== '').length;
  return { comment: comments(src).filter((c) => c.whole).length, total };
}

/** The comments `after` adds to `before`: those whose text stands more often after than before. */
export function added(before, after) {
  const seen = new Map();
  for (const { text } of comments(before)) if (text) seen.set(text, (seen.get(text) ?? 0) + 1);
  const fresh = [];
  for (const c of comments(after)) {
    if (!c.text) continue;
    const left = seen.get(c.text) ?? 0;
    if (left > 0) seen.set(c.text, left - 1);
    else fresh.push(c);
  }
  return fresh;
}

/** What a comment's text names that a comment may not: `[kind, word]` pairs. */
export function namings(text) {
  const names = [];
  for (const m of text.matchAll(ROW)) if (!KNOWN.test(m[0])) names.push(['a row id', m[0]]);
  for (const m of text.matchAll(DECISION)) names.push(['a decision number', m[0]]);
  for (const m of text.matchAll(HISTORY)) names.push(['a history note', m[0]]);
  return names;
}

/** The breaches of one file: `before` is its old text, or null where it is new. */
export function judge(path, before, after) {
  const problems = [];
  if (before !== null) {
    const was = share(before);
    const now = share(after);
    if (now.comment * was.total > was.comment * now.total) {
      const pct = (s) => `${((100 * s.comment) / Math.max(1, s.total)).toFixed(1)}%`;
      problems.push(`${path}: comments rose from ${pct(was)} (${was.comment}/${was.total}) to ${pct(now)} (${now.comment}/${now.total}) of its lines: take comments out of this file, or add less`);
    }
  }
  for (const { line, text } of added(before ?? '', after)) {
    for (const [kind, word] of namings(text)) {
      problems.push(`${path}:${line}: a comment names ${kind} (${word}): the commit or the decision holds that, so say only why the code is as it is`);
    }
  }
  return problems;
}

const git = (root, args) => execGit(args, { cwd: root, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
const gitOrNull = (root, args) => {
  try {
    return git(root, args);
  } catch {
    return null;
  }
};

/** The files a change touches, `{ path, from, status }`, `from` the path they had at the base. */
function touched(root, base, tip) {
  const out = git(root, ['--no-optional-locks', 'diff', '-z', '--name-status', '--find-renames', base, ...(tip ? [tip] : [])]).split('\0').filter(Boolean);
  const files = [];
  for (let i = 0; i < out.length; ) {
    const status = out[i++];
    if (status[0] === 'R' || status[0] === 'C') {
      const from = out[i++];
      files.push({ path: out[i++], from, status: status[0] });
    } else {
      const path = out[i++];
      files.push({ path, from: path, status: status[0] });
    }
  }
  if (!tip) {
    for (const path of git(root, ['ls-files', '-z', '--others', '--exclude-standard']).split('\0').filter(Boolean)) {
      files.push({ path, from: path, status: 'A' });
    }
  }
  return files.filter((f) => f.status !== 'D' && isSource(f.path) && !SKIPPED.test(f.path));
}

/** The breaches of the change `base`..`tip` (the working tree where `tip` is null) in the repository at `root`. */
export function checkChange(root, base, tip = null) {
  const problems = [];
  let checked = 0;
  for (const file of touched(root, base, tip)) {
    const after = tip ? gitOrNull(root, ['show', `${tip}:${file.path}`]) : readWork(root, file.path);
    if (after === null) continue;
    const before = file.status === 'A' ? null : gitOrNull(root, ['show', `${base}:${file.from}`]);
    problems.push(...judge(file.path, before, after));
    checked++;
  }
  return { problems, checked };
}

/** The breaches of every source file of the work tree at `root`: each comment is held to the naming rule; the share rule is a change's alone. */
export function checkAll(root) {
  const problems = [];
  let checked = 0;
  for (const path of repositoryFiles(root)) {
    if (!isSource(path) || SKIPPED.test(path)) continue;
    problems.push(...judge(path, null, readFileSync(join(root, path), 'utf8')));
    checked++;
  }
  return { problems, checked };
}

const readWork = (root, path) => (existsSync(join(root, path)) ? readFileSync(join(root, path), 'utf8') : null);

function main(argv) {
  const option = (name) => {
    const at = argv.indexOf(name);
    return at >= 0 ? argv[at + 1] : null;
  };
  const root = option('--root') ?? dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    if (argv.includes('--all')) {
      const { problems, checked } = checkAll(root);
      for (const p of problems) console.error(`comment-gate: ${p}`);
      if (problems.length) return 1;
      console.log(`comment-gate: clean (all ${checked} source files)`);
      return 0;
    }
    const tip = option('--tip');
    const trunk = option('--base') ?? MAIN;
    const base = gitOrNull(root, ['merge-base', trunk, tip ?? 'HEAD'])?.trim();
    if (!base) throw new Error(`no merge-base between ${trunk} and ${tip ?? 'HEAD'}`);
    const { problems, checked } = checkChange(root, base, tip);
    for (const p of problems) console.error(`comment-gate: ${p}`);
    if (problems.length) return 1;
    console.log(`comment-gate: clean (${checked} changed source files)`);
    return 0;
  } catch (e) {
    console.error(`comment-gate: ${e.message}`);
    return 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
