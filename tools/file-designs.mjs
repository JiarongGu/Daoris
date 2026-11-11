#!/usr/bin/env node
/**
 * Designs filed: a dated document moved from the documents folder's top into its archive folder with `git mv`, every
 * citation of it across the tree respelled in the same change, and its router row moved to the archive's router.
 *
 *   node tools/file-designs.mjs [--plan] [--root <dir>] [--unclaimed] [--skip-refused] [--expect-moved N]
 *                               [--expect-rewritten N] [docs/<name>.md ...]
 *
 * `--unclaimed` names every dated document at `docs/`'s top that neither the backlog, an area document nor the brief
 * cites: a design whose work is done. A citation is the document's file name in a tracked text file, with the path
 * before it:
 *
 * - one that reaches the document from where the citing file stands (`../docs/<name>`, a sibling's bare `<name>`), or
 *   from the tree's top (`docs/<name>`, a link ending in it), is respelled to reach it where it now stands;
 * - a bare name that reaches nothing from where it stands is a mention by name, which the move keeps true, and counted;
 * - any other (a path that reaches neither place, or one in a file the move may not edit: `daoris.lanes.json`,
 *   `daoris.lock`, `canon/`) is refused, and the whole run with it unless `--skip-refused` leaves that design where it is.
 *
 * A moved document's own relative links are respelled from its new folder. The router's row of a moved document (a row
 * whose first cell is the document's name) leaves `docs/README.md` for `docs/archive/README.md`, with its subject.
 * Nothing is written until the plan holds; after the run, no citation reaches a moved document's old place, else exit 1.
 *
 * `--plan` prints the plan and changes nothing. Exit 0 clean, 1 a policy failure (a refusal, a count that differs, a
 * citation left behind), 2 a tool error (usage, unreadable input, git failing).
 */
import { mkdirSync, readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { isMain, repositoryFiles, spawnGit, writeAtomic } from './fsx.mjs';

export const DOCS = 'docs';
export const ARCHIVE = 'docs/archive';
export const ROUTER = 'docs/README.md';
export const ARCHIVE_ROUTER = 'docs/archive/README.md';
const DATE = /\d{4}-\d{2}-\d{2}/;
const TEXT = /\.(?:md|mdx|txt|json|jsonc|ya?ml|toml|[cm]?[jt]sx?|cs|csproj|props|targets|xml|html|css|ps1|sh|cmd|razor|template)$/i;
const PATH_CHAR = /[A-Za-z0-9_.~/-]/;
const WORD_CHAR = /[A-Za-z0-9_]/;
const isProtected = (path) => path === 'daoris.lanes.json' || path === 'daoris.lock' || path.startsWith('canon/');

/** A failure of the filing's own rules, for exit 1. */
export class FilingRefused extends Error {}

const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** The dated documents at the documents folder's top, by path. */
export function datedAtTop(files) {
  return files.filter((file) => posix.dirname(file) === DOCS && file.endsWith('.md') && DATE.test(posix.basename(file)));
}

/** The files whose citation keeps a design open: the brief, the backlog and every area document a manifest declares. */
export function openReaders(files, manifest) {
  const documents = manifest?.documents ?? {};
  const pathOf = (role, fallback) => (typeof documents[role] === 'string' ? documents[role] : documents[role]?.path) ?? fallback;
  const backlog = pathOf('backlog', 'TASKS.md');
  const areas = pathOf('areas', 'docs/areas');
  return files.filter((file) => file === 'AGENTS.md' || file === backlog || (file.startsWith(`${areas}/`) && file.endsWith('.md')));
}

/** The dated documents at the top that no open reader cites. */
export function unclaimed(files, read, manifest) {
  const text = openReaders(files, manifest).map(read).join('\n');
  return datedAtTop(files).filter((file) => !text.includes(posix.basename(file)));
}

const lineAt = (text, index) => text.slice(0, index).split('\n').length;

/** The router's rows for the moved names, cut from its text: `{ text, rows: Map(name -> subject) }`. */
export function cutRouterRows(text, names) {
  const rows = new Map();
  const kept = text.split('\n').filter((line) => {
    const row = /^\|\s*`([^`]+)`\s*\|(.*)$/.exec(line);
    if (!row || !names.has(row[1])) return true;
    rows.set(row[1], row[2].replace(/\|\s*$/, '').split('|').map((cell) => cell.trim()).filter(Boolean).join('; '));
    return false;
  });
  return { text: kept.join('\n'), rows };
}

/** The archive router with a row added for each filed document, after its table's last row. */
export function addArchiveRows(text, entries) {
  const lines = text.split('\n');
  let last = -1;
  lines.forEach((line, i) => { if (line.startsWith('|')) last = i; });
  const rows = entries.map(({ name, subject }) => `| \`${name}\` | ${subject} | Filed: no open row or area cites it |`);
  if (last < 0) return `${text.replace(/\n*$/, '\n\n')}| Document | What it was | What replaced it |\n|---|---|---|\n${rows.join('\n')}\n`;
  lines.splice(last + 1, 0, ...rows);
  return lines.join('\n');
}

/**
 * Every citation of a dated top-level document in one file, respelled for the moves: `{ text, rewritten, byName,
 * refused }`. `moved` maps an old path to its new one; `tracked` is the tree's files.
 */
export function respellCitations(file, text, { moved, names, pattern }) {
  const newPlace = (path) => moved.get(path) ?? path;
  const fromDir = posix.dirname(file);
  const toDir = posix.dirname(newPlace(file));
  const citerMoved = moved.has(file);
  const refused = [];
  let rewritten = 0;
  let byName = 0;
  let out = '';
  let at = 0;
  for (const match of text.matchAll(pattern)) {
    const name = match[0];
    const end = match.index + name.length;
    if (end < text.length && WORD_CHAR.test(text[end])) continue;
    let start = match.index;
    while (start > 0 && PATH_CHAR.test(text[start - 1])) start--;
    const prefix = text.slice(start, match.index);
    if (prefix && !prefix.endsWith('/')) continue;
    const old = `${DOCS}/${name}`;
    const nameMoved = moved.has(old);
    const token = prefix + name;
    const resolved = token.startsWith('/') ? posix.normalize(token.slice(1)) : posix.normalize(posix.join(fromDir, token));
    let replacement = null;
    if (resolved === old) {
      if (nameMoved || citerMoved) {
        const target = newPlace(old);
        replacement = token.startsWith('/') ? `/${target}` : posix.relative(toDir, target);
        if (prefix.startsWith('./') && !replacement.startsWith('.')) replacement = `./${replacement}`;
      }
    } else if (nameMoved && (token === old || token.endsWith(`/${old}`))) {
      replacement = `${token.slice(0, token.length - old.length)}${newPlace(old)}`;
    } else if (!nameMoved || resolved === newPlace(old)) {
      continue;
    } else if (!prefix) {
      byName++;
    } else {
      refused.push({ file, line: lineAt(text, match.index), token, why: 'reaches neither its old place nor its new one' });
    }
    if (replacement === null || replacement === token) continue;
    if (isProtected(file)) {
      refused.push({ file, line: lineAt(text, match.index), token, why: 'in a file the move may not edit' });
      continue;
    }
    out += text.slice(at, start) + replacement;
    at = end;
    rewritten++;
  }
  return { text: out + text.slice(at), rewritten, byName, refused, names };
}

/** A moved document's relative links to anything not dated at the top, respelled from its new folder. */
export function respellLinks(file, text, { moved, tracked, folders, names }) {
  const fromDir = posix.dirname(file);
  const toDir = posix.dirname(moved.get(file) ?? file);
  let rewritten = 0;
  const out = text.replace(/\]\(([^)\s#]+)(#[^)\s]*)?\)/g, (whole, target, anchor = '') => {
    if (/^[a-z][a-z0-9+.-]*:/i.test(target) || target.startsWith('/') || names.has(posix.basename(target))) return whole;
    const resolved = posix.normalize(posix.join(fromDir, target));
    if (!tracked.has(resolved) && !folders.has(resolved)) return whole;
    const respelled = posix.relative(toDir, moved.get(resolved) ?? resolved) || '.';
    if (respelled === target) return whole;
    rewritten++;
    return `](${respelled}${anchor})`;
  });
  return { text: out, rewritten };
}

/** The plan for filing `paths`: `{ moved, edits, rewritten, byName, refused }`, nothing written. */
export function planFiling(files, read, paths) {
  const tracked = new Set(files);
  const moved = new Map(paths.map((path) => [path, `${ARCHIVE}/${posix.basename(path)}`]));
  for (const [from, to] of moved) {
    if (!tracked.has(from)) throw new FilingRefused(`${from}: not a tracked file`);
    if (posix.dirname(from) !== DOCS || !DATE.test(posix.basename(from))) throw new FilingRefused(`${from}: not a dated document at ${DOCS}/'s top`);
    if (tracked.has(to)) throw new FilingRefused(`${to}: already exists`);
  }
  const folders = new Set();
  for (const file of files) for (let dir = posix.dirname(file); dir !== '.'; dir = posix.dirname(dir)) folders.add(dir);
  const names = new Set(datedAtTop(files).map((path) => posix.basename(path)));
  const pattern = new RegExp([...names].sort((a, b) => b.length - a.length).map(escape).join('|'), 'g');
  const context = { moved, tracked, folders, names, pattern };

  const edits = new Map();
  const refused = [];
  let rewritten = 0;
  let byName = 0;
  const movedNames = new Set([...moved.keys()].map((path) => posix.basename(path)));
  const router = tracked.has(ROUTER) ? cutRouterRows(read(ROUTER), movedNames) : { text: null, rows: new Map() };
  for (const file of files) {
    if (!TEXT.test(file)) continue;
    let text = file === ROUTER ? router.text : read(file);
    const original = read(file);
    if (file === ARCHIVE_ROUTER) {
      text = addArchiveRows(text, [...moved.keys()].map((path) => {
        const name = posix.basename(path);
        const heading = /^#\s+(.+)$/m.exec(read(path))?.[1]?.trim() ?? name;
        return { name, subject: router.rows.get(name) ?? heading.replace(/\|/g, '/') };
      }));
    }
    const cited = respellCitations(file, text, context);
    refused.push(...cited.refused);
    rewritten += cited.rewritten;
    byName += cited.byName;
    text = cited.text;
    if (moved.has(file)) {
      const linked = respellLinks(file, text, context);
      rewritten += linked.rewritten;
      text = linked.text;
    }
    if (text !== original) edits.set(file, text);
  }
  if (!tracked.has(ARCHIVE_ROUTER) && moved.size) {
    throw new FilingRefused(`${ARCHIVE_ROUTER}: no archive router to take the rows`);
  }
  return { moved, edits, rewritten, byName, refused, routerRows: router.rows.size };
}

/** The plan with every refused design left where it is, planned again until nothing is refused. */
export function planSkippingRefused(files, read, paths) {
  let keep = [...paths];
  const skipped = [];
  for (;;) {
    const plan = planFiling(files, read, keep);
    if (!plan.refused.length) return { ...plan, skipped };
    const out = new Set(plan.refused.map((r) => `${DOCS}/${posix.basename(r.token)}`));
    skipped.push(...plan.refused);
    keep = keep.filter((path) => !out.has(path));
  }
}

function git(root, args, env) {
  const run = spawnGit(args, { cwd: root, encoding: 'utf8', windowsHide: true, env });
  if (run.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${(run.stderr || run.error?.message || '').trim()}`);
}

/** Runs the plan: `git mv` the documents, writes the respelled text, then checks the tree. Returns the failures. */
export function applyFiling(root, plan, { env = process.env } = {}) {
  mkdirSync(join(root, ARCHIVE), { recursive: true });
  const froms = [...plan.moved.keys()];
  for (let i = 0; i < froms.length; i += 50) git(root, ['mv', '--', ...froms.slice(i, i + 50), `${ARCHIVE}/`], env);
  for (const [file, text] of plan.edits) writeAtomic(join(root, plan.moved.get(file) ?? file), text);
  return verifyFiling(root, plan, { env });
}

/** Every citation in the tree that still reaches a moved document's old place. */
export function verifyFiling(root, plan, { env = process.env, read = (path) => readFileSync(join(root, path), 'utf8') } = {}) {
  const files = repositoryFiles(root, { env });
  const names = [...plan.moved.keys()].map((path) => posix.basename(path));
  if (!names.length) return [];
  const pattern = new RegExp(names.map(escape).join('|'), 'g');
  const failures = [];
  for (const file of files) {
    if (!TEXT.test(file)) continue;
    const text = read(file);
    for (const match of text.matchAll(pattern)) {
      let start = match.index;
      while (start > 0 && PATH_CHAR.test(text[start - 1])) start--;
      const token = text.slice(start, match.index + match[0].length);
      const old = `${DOCS}/${match[0]}`;
      const resolved = posix.normalize(posix.join(posix.dirname(file), token));
      if (resolved === old || token === old || token.endsWith(`/${old}`)) failures.push(`${file}:${lineAt(text, match.index)}: ${token} reaches ${old}`);
    }
  }
  return failures;
}

function parseArgs(argv) {
  const args = { paths: [], plan: false, unclaimed: false, skipRefused: false, root: process.cwd(), expect: {} };
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--plan') args.plan = true;
    else if (arg === '--unclaimed') args.unclaimed = true;
    else if (arg === '--skip-refused') args.skipRefused = true;
    else if (arg === '--root') args.root = argv[++i];
    else if (arg === '--expect-moved') args.expect.moved = Number(argv[++i]);
    else if (arg === '--expect-rewritten') args.expect.rewritten = Number(argv[++i]);
    else if (arg.startsWith('--')) throw new Error(`unknown flag ${arg}`);
    else args.paths.push(arg.replace(/\\/g, '/'));
  }
  if (!args.unclaimed && !args.paths.length) throw new Error('name a document, or --unclaimed');
  return args;
}

export function main(argv, { out = (line) => console.log(line), err = (line) => console.error(line), env = process.env } = {}) {
  let args;
  try {
    args = parseArgs(argv);
  } catch (error) {
    err(`file-designs: ${error.message}`);
    return 2;
  }
  try {
    const files = repositoryFiles(args.root, { env });
    const read = (path) => readFileSync(join(args.root, path), 'utf8');
    let manifest = null;
    try { manifest = JSON.parse(read('daoris.json')); } catch { manifest = null; }
    const paths = args.unclaimed ? [...new Set([...unclaimed(files, read, manifest), ...args.paths])] : args.paths;
    const plan = args.skipRefused ? planSkippingRefused(files, read, paths) : planFiling(files, read, paths);
    for (const r of plan.skipped ?? []) out(`left: ${r.file}:${r.line} ${r.token} (${r.why})`);
    if (plan.refused.length) {
      for (const r of plan.refused) err(`refused: ${r.file}:${r.line} ${r.token} (${r.why})`);
      return 1;
    }
    out(`file-designs: ${plan.moved.size} moved, ${plan.rewritten} citations respelled in ${plan.edits.size} files, `
      + `${plan.byName} by name kept, ${plan.routerRows} router rows moved`);
    for (const [key, value] of Object.entries(args.expect)) {
      const found = key === 'moved' ? plan.moved.size : plan.rewritten;
      if (found !== value) { err(`file-designs: expected ${value} ${key}, found ${found}`); return 1; }
    }
    if (args.plan) {
      for (const [from, to] of plan.moved) out(`  ${from} -> ${to}`);
      return 0;
    }
    const failures = applyFiling(args.root, plan, { env });
    for (const failure of failures) err(`left behind: ${failure}`);
    return failures.length ? 1 : 0;
  } catch (error) {
    err(`file-designs: ${error.message}`);
    return error instanceof FilingRefused ? 1 : 2;
  }
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));
