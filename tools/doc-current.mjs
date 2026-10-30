#!/usr/bin/env node
/**
 * KNOWFRESH1a-iii (D220; docs/2026-10-30-knowledge-freshness-design.md §3.2 and §3.4): the present records, read for what
 * the code no longer has.
 *
 *   node tools/doc-current.mjs                  report every declared present record's paths, names, routes and variables
 *   node tools/doc-current.mjs --since <base>   also list the records that quote a path or name the diff since <base> deleted or
 *                                               renamed, and show only the findings in the homes the diff touched
 *   node tools/doc-current.mjs --json           the report as data
 *
 * Reports and never fails: exit 0 clean or reporting, 2 on a tool error (a bad declaration, a base git cannot read). A pure
 * audit like doc-system.mjs; the declaration is `documents.present` in `daoris.json`: `homes` (a file, or a folder whose `.md`
 * files this repository owns, not those `daoris.lock` says the canon writes), `bases` (per home: folders its short paths
 * follow), `runtime` (roots a path under is machine state, never checked) and `allow` (a span with the reason it is let through).
 *
 * The rule, over each code span outside a fence, in a home read above its generated doctrine region (design §3.2):
 *  - A path: its `:line` is stripped and never range-checked. It is resolved exactly against the home's folder, the root, then each
 *    base; present if tracked, a tracked folder or ignored by git. A placeholder, a bare file name, a machine path, a path under
 *    a runtime root and a path whose first part is no top-level entry are unchecked, said as such and never as present. A moved
 *    file is never found by its name.
 *  - A name (PascalCase, camelCase or dotted): the stem of a tracked source file is a file reference. Otherwise each part, or its
 *    PascalCase form, must be an identifier of product source (`.cs .ts .tsx .mjs .js .ps1`, comments stripped, strings kept,
 *    JSON and other data out). One found only in test source is test-only, printed.
 *  - A route (`/api/...`) is read against `docs/index/routes.md`; a `DAORIS_*` variable against product source.
 * A finding that is none of the repository's to fix goes on the allow-list with its reason.
 */
import { readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fenced } from './doc-duplicates.mjs';
import { isMain, repositoryFiles, spawnGit } from './fsx.mjs';

const SOURCE = /\.(?:cs|ts|tsx|mjs|js|ps1)$/;
// A test source is a test file, a test project or folder, or a fixture: a name only there is test-only.
const TEST = /(?:\.(?:test|spec|stories)\.[a-z]+$|Tests?\.cs$|(?:^|\/)(?:[^/]*\.Tests|tests?|tool-test|e2e|__tests__|fixtures?|[^/]*-fixtures|_fixtures)\/)/;
/** The generated doctrine region begins here in a brief; the canon owns it, so it is never read (design §5 iii). */
const REGION = '<!-- daoris:rules';
const VERBS = '(?:GET|POST|PUT|PATCH|DELETE|HEAD)';
const BARE = /^\.?[\w.-]+\.(?:md|jsonc?|mts|cts|tsx?|mjs|cjs|jsx?|cs|csproj|slnx?|props|targets|ps1|psm1|sh|cmd|bat|py|ya?ml|toml|xml|html?|css|scss|svg|png|ico|txt|lock|exe|dll|log|db|sqlite|config|settings|snap)$|^\.[\w.-]+$/;
const LINE = /(?::\d+(?:-\d+)?)(?:,\s*:?\d+(?:-\d+)?)*$/;

/** `product` for a source file a name may live in, `test` for one a name is test-only in, else null. */
export function sourceKind(path) {
  if (!SOURCE.test(path)) return null;
  return TEST.test(path) ? 'test' : 'product';
}

/**
 * The source with its comments blanked and its strings kept (newlines stay), per language by `ext`: `cs`, `ts` (also `tsx`, `mjs`,
 * `js`) or `ps1`. A string is kept because a variable or a wire name read from one is present; a `//` inside one is no comment.
 * A regular-expression literal is not told from division, so a quote inside one ends at its line.
 */
export function stripComments(src, ext) {
  const ps = ext === 'ps1';
  const cs = ext === 'cs';
  let out = '';
  let i = 0;
  const n = src.length;
  const gap = (from, to) => src.slice(from, to).replace(/[^\r\n]/g, ' ');
  while (i < n) {
    const c = src[i];
    const d = src[i + 1];
    if (ps ? c === '#' : c === '/' && d === '/') {
      let j = src.indexOf('\n', i);
      if (j < 0) j = n;
      out += gap(i, j);
      i = j;
    } else if (ps ? c === '<' && d === '#' : c === '/' && d === '*') {
      let j = src.indexOf(ps ? '#>' : '*/', i + 2);
      j = j < 0 ? n : j + 2;
      out += gap(i, j);
      i = j;
    } else if (c === '"' || (cs && (c === '$' || c === '@') && /^[$@]*"/.test(src.slice(i, i + 4)))) {
      let j = i;
      let verbatim = false;
      while (src[j] !== '"') {
        if (src[j] === '@') verbatim = true;
        j++;
      }
      let quotes = 0;
      while (src[j + quotes] === '"') quotes++;
      let end;
      if (cs && quotes >= 3) {
        const close = '"'.repeat(quotes);
        end = src.indexOf(close, j + quotes);
        end = end < 0 ? n : end + quotes;
      } else {
        end = j + 1;
        while (end < n) {
          if (src[end] === '"' && verbatim && src[end + 1] === '"') end += 2;
          else if (src[end] === '"') {
            end++;
            break;
          } else if (src[end] === '\\' && !verbatim) end += 2;
          else if (src[end] === '\n' && !verbatim) break;
          else end++;
        }
      }
      out += src.slice(i, end);
      i = end;
    } else if (c === "'" && cs) {
      const end = d === '\\' ? src.indexOf("'", i + 3) + 1 || n : i + 3;
      out += src.slice(i, end);
      i = end;
    } else if (c === "'" || (c === '`' && !ps && !cs)) {
      // A quoted string ends at its line; a template literal runs to its closing mark.
      let end = i + 1;
      while (end < n) {
        if (src[end] === '\\') end += 2;
        else if (src[end] === c) {
          end++;
          break;
        } else if (src[end] === '\n' && c === "'") break;
        else end++;
      }
      out += src.slice(i, end);
      i = end;
    } else {
      out += c;
      i++;
    }
  }
  return out;
}

const extOf = (path) => path.slice(path.lastIndexOf('.') + 1).replace(/^(?:tsx|mjs|js)$/, 'ts');
const IDENTIFIER = /[A-Za-z_][A-Za-z0-9_]*/g;
const identifiersOf = (text, into) => {
  for (const [word] of text.matchAll(IDENTIFIER)) into.add(word);
  return into;
};

const mixed = (part) => /[a-z]/.test(part) && /[A-Z]/.test(part);
const pascal = (part) => part[0].toUpperCase() + part.slice(1);

/** The name a span says, as its dotted parts, or null where it is no name (a word, a constant, prose, a command). */
function nameOf(text) {
  const call = /^([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)(?:<[^<>`]*>)?(?:\(.*\))?$/.exec(text);
  if (!call) return null;
  const parts = call[1].split('.');
  return parts.some(mixed) ? { text: call[1], parts } : null;
}

function repositoryPath(value) {
  return typeof value === 'string' && value.trim() !== '' ? posix.normalize(value.replace(/\\/g, '/')).replace(/\/$/, '') : null;
}

/** The declared homes as files: a folder is the `.md` files under it that daoris.lock does not say the canon wrote. */
function homesOf(present, files, read, target) {
  const lockTargets = new Set();
  if (files.includes('daoris.lock')) {
    for (const entry of JSON.parse(read('daoris.lock')).entries ?? []) {
      lockTargets.add(posix.join(target ?? '', entry.target ?? ''));
    }
  }
  const homes = [];
  for (const declared of present.homes ?? []) {
    const path = repositoryPath(declared);
    if (path === null) throw new Error(`documents.present.homes has ${JSON.stringify(declared)}, which is no repository path`);
    if (files.includes(path)) {
      homes.push(path);
    } else if (declared.endsWith('/') && files.some((file) => file.startsWith(`${path}/`))) {
      homes.push(...files.filter((file) => file.startsWith(`${path}/`) && file.endsWith('.md') && !lockTargets.has(file)));
    } else {
      throw new Error(`documents.present.homes names ${declared}, which is no tracked file or folder`);
    }
  }
  return [...new Set(homes)].sort();
}

/** Every code span outside a fence, with its line: `{ line, text }`. A span broken over a line is not one. */
function spansOf(text) {
  const out = [];
  const lines = text.split(/\r?\n/);
  const region = lines.findIndex((line) => line.startsWith(REGION));
  const read = region < 0 ? lines : lines.slice(0, region);
  const inFence = fenced(read);
  for (const [index, line] of read.entries()) {
    if (inFence[index]) continue;
    for (const match of line.matchAll(/(?<!`)(`{1,2})(?!`)(.+?)(?<!`)\1(?!`)/g)) {
      const span = match[2].trim();
      if (span) out.push({ line: index + 1, text: span });
    }
  }
  return out;
}

/** The routes of `docs/index/routes.md`, as segment lists: a `{x}` segment is any. */
function routesOf(files, read) {
  if (!files.includes('docs/index/routes.md')) return [];
  const routes = [];
  for (const match of read('docs/index/routes.md').matchAll(new RegExp(`^\\|\\s*${VERBS}\\s+(/\\S+)\\s*\\|`, 'gm'))) {
    routes.push(match[1].replace(/[?#].*$/, '').replace(/\/$/, '').split('/'));
  }
  return routes;
}
const placeholderSegment = (segment) => /^(?:\{.*\}|<.*>|:\w+)$/.test(segment);
const sameRoute = (a, b) => a.length === b.length
  && a.every((segment, i) => segment === b[i] || placeholderSegment(segment) || placeholderSegment(b[i]));

/**
 * Audit the declared present records against the code. The caller supplies the inventory and the reads, and `ignored(paths)`
 * answering which of those paths git ignores, so a fixture needs no checkout. `diff` is `sinceDiff`'s answer, or null.
 */
export function auditCurrent({ manifest, files, read, ignored, diff = null }) {
  const present = manifest?.documents?.present;
  if (!present || typeof present !== 'object') throw new Error('daoris.json declares no documents.present (the homes the report reads)');
  const allow = present.allow ?? {};
  for (const [span, reason] of Object.entries(allow)) {
    if (typeof reason !== 'string' || reason.trim() === '') throw new Error(`documents.present.allow: ${span} has no reason`);
  }
  const runtime = (present.runtime ?? []).map((root) => root.replace(/\/?$/, '/'));
  const homes = homesOf(present, files, read, manifest.target);

  const tracked = new Set(files);
  const folders = new Set();
  const top = new Set();
  const stems = new Set();
  const product = new Set();
  const tests = new Set();
  for (const file of files) {
    top.add(file.split('/')[0]);
    for (let at = file.indexOf('/'); at >= 0; at = file.indexOf('/', at + 1)) folders.add(file.slice(0, at));
    const kind = sourceKind(file);
    if (kind === null) continue;
    stems.add(posix.basename(file).replace(/\.[^.]+$/, ''));
    identifiersOf(stripComments(read(file), extOf(file)), kind === 'product' ? product : tests);
  }
  const routes = routesOf(files, read);

  // The diff's deleted and renamed things, read once: the paths, the file stems, and the names no product source has left.
  const gone = new Map();
  const goneStems = new Map();
  const goneNames = new Set();
  if (diff) {
    for (const change of diff.changed) {
      const from = change.status === 'D' ? change.path : change.status === 'R' ? change.from : null;
      if (from === null) continue;
      const what = change.status === 'D' ? 'deleted' : `renamed to ${change.path}`;
      gone.set(from, what);
      const stem = posix.basename(from).replace(/\.[^.]+$/, '');
      if (SOURCE.test(from) && !stems.has(stem)) goneStems.set(stem, change.status === 'D' ? 'file deleted' : `file renamed to ${change.path}`);
    }
    for (const name of diff.removed) if (!product.has(name)) goneNames.add(name);
  }

  const counts = {
    spans: 0,
    allowed: 0,
    paths: { present: 0, missing: 0, unchecked: { placeholder: 0, bare: 0, runtime: 0, unanchored: 0 } },
    symbols: 0,
    symbolsBy: { file: 0, product: 0, testOnly: 0, absent: 0 },
    routes: { present: 0, unknown: 0 },
    variables: { present: 0, testOnly: 0, absent: 0 },
  };
  const findings = [];
  const quoting = [];
  const hit = new Set();
  const pending = [];
  const report = (home, line, span, kind, say) => {
    if (span in allow) {
      hit.add(span);
      counts.allowed++;
      return;
    }
    findings.push({ home, line, span, kind, say });
  };

  /** A name's parts held against product source, then test source; one the diff removed from product source is quoted. */
  const checkName = (home, line, span, parts, quoteRemoved = true) => {
    if (quoteRemoved && parts.some((part) => goneNames.has(part) || goneNames.has(pascal(part)))) {
      quoting.push({ home, line, span, what: 'removed from product source' });
    }
    const states = parts.map((part) => (product.has(part) || product.has(pascal(part)) ? 'product'
      : tests.has(part) || tests.has(pascal(part)) ? 'test' : 'absent'));
    if (states.includes('absent')) {
      counts.symbolsBy.absent++;
      report(home, line, span, 'symbol-absent', 'no product or test source has it');
    } else if (states.includes('test')) {
      counts.symbolsBy.testOnly++;
      report(home, line, span, 'symbol-test-only', 'only test source has it');
    } else {
      counts.symbolsBy.product++;
    }
  };

  for (const home of homes) {
    for (const { line, text } of spansOf(read(home))) {
      counts.spans++;
      const route = new RegExp(`^(?:${VERBS}\\s+)?(/api/[^\\s?#]*)(?:[?#].*)?$`).exec(text);
      if (route) {
        const segments = route[1].replace(/\/$/, '').split('/');
        if (routes.some((each) => sameRoute(each, segments))) counts.routes.present++;
        else {
          counts.routes.unknown++;
          report(home, line, text, 'route-unknown', 'no such route in docs/index/routes.md');
        }
        continue;
      }
      const variable = /^(?:\$env:|\$|%)?(DAORIS_[A-Z0-9_]*[A-Z0-9])%?(?:=\S*)?$/.exec(text);
      if (variable) {
        if (product.has(variable[1])) counts.variables.present++;
        else if (tests.has(variable[1])) {
          counts.variables.testOnly++;
          report(home, line, text, 'variable-test-only', 'only test source names it');
        } else {
          counts.variables.absent++;
          report(home, line, text, 'variable-absent', 'no source names it');
        }
        continue;
      }
      // `file.ext:Member` names a member of the file: the path is read, and the member as a name.
      const member = /^(.+\.[A-Za-z]+):([A-Za-z_]\w*)$/.exec(text.replace(LINE, ''));
      const path = member ? member[1] : text.replace(LINE, '');
      if (/\s/.test(path) || /:\/\//.test(path) || path.includes('\\')) continue;
      if (path.includes('/')) {
        const unchecked = (kind) => { counts.paths.unchecked[kind]++; };
        if (/^[~%$/]|^[A-Za-z]:/.test(path)) { unchecked('runtime'); continue; }
        if (/[<>{}*…|]|\.\.\.|YYYY/.test(path)) { unchecked('placeholder'); continue; }
        const wrapped = `/${path.replace(/\/$/, '')}/`;
        if (runtime.some((root) => wrapped.includes(`/${root}`))) { unchecked('runtime'); continue; }
        const given = path.replace(/^\.\//, '');
        const candidates = [...new Set([
          posix.normalize(posix.join(posix.dirname(home), given)),
          posix.normalize(given),
          ...[...(present.bases?.[home] ?? []), ...(present.bases?.['*'] ?? [])].map((base) => posix.normalize(posix.join(base, given))),
        ])].filter((candidate) => !candidate.startsWith('..')).map((candidate) => (given.endsWith('/') ? candidate.replace(/\/?$/, '/') : candidate));
        const exists = (candidate) => tracked.has(candidate) || folders.has(candidate.replace(/\/$/, ''));
        const quotes = candidates.map((candidate) => gone.get(candidate.replace(/\/$/, ''))).find(Boolean);
        if (quotes) quoting.push({ home, line, span: text, what: quotes });
        if (candidates.some(exists)) {
          counts.paths.present++;
          if (member) checkName(home, line, text, [member[2]]);
        } else {
          pending.push({ home, line, span: text, given, candidates, member: member?.[2] });
        }
        continue;
      }
      if (BARE.test(path)) { counts.paths.unchecked.bare++; continue; }
      const name = nameOf(text);
      if (!name) continue;
      counts.symbols++;
      if (stems.has(name.text)) {
        counts.symbolsBy.file++;
        continue;
      }
      const stemGone = goneStems.get(name.text);
      if (stemGone) quoting.push({ home, line, span: text, what: stemGone });
      checkName(home, line, text, name.parts, !stemGone);
    }
  }

  // A path no tracked file or folder answers: asked of git once, whether it is ignored, then anchored or not.
  // A folder that is not there is ignored by a `dir/` pattern only when asked as a folder, so each is asked both ways.
  const asked = [...new Set(pending.flatMap((each) => each.candidates.flatMap((candidate) => [candidate.replace(/\/$/, ''), candidate.replace(/\/?$/, '/')])))];
  const ignoredSet = asked.length ? ignored(asked) : new Set();
  for (const { home, line, span, given, candidates } of pending) {
    if (candidates.some((candidate) => ignoredSet.has(candidate) || ignoredSet.has(candidate.replace(/\/?$/, '/')))) counts.paths.present++;
    else if (!top.has(given.split('/')[0]) && !/^\.\.?\//.test(span)) counts.paths.unchecked.unanchored++;
    else {
      counts.paths.missing++;
      report(home, line, span, 'path-missing', 'no tracked file or folder, nor an ignored path');
    }
  }
  findings.sort((a, b) => (a.home < b.home ? -1 : a.home > b.home ? 1 : a.line - b.line));
  quoting.sort((a, b) => (a.home < b.home ? -1 : a.home > b.home ? 1 : a.line - b.line));
  const result = { homes, counts, findings, unusedAllow: Object.keys(allow).filter((span) => !hit.has(span)) };
  if (diff) {
    const touched = new Set(diff.changed.flatMap((change) => [change.path, change.from].filter(Boolean)));
    result.since = { touched: homes.filter((home) => touched.has(home)), quoting };
  }
  return result;
}

const gitArgs = (...args) => ['-c', 'core.quotepath=false', '--no-optional-locks', ...args];

/**
 * What the diff from `base` to the work tree deleted or renamed: `changed` as `{ status, path, from }` (`D`, `R` with its old path,
 * or the first letter of any other status) and `removed`, the identifiers on lines it removed from product source, which the
 * audit holds against what product source still has. A removed line that is a comment is not product source.
 */
export function sinceDiff(root, base, { env = process.env } = {}) {
  const options = { cwd: root, encoding: 'utf8', windowsHide: true, env, maxBuffer: 256 * 1024 * 1024 };
  const run = (args) => {
    const done = spawnGit(args, options);
    if (done.status !== 0) throw new Error(`git ${args.join(' ')} failed: ${done.stderr?.trim() ?? done.error?.message}`);
    return done.stdout;
  };
  const changed = [];
  const fields = run(gitArgs('diff', '--name-status', '-M', '-z', base)).split('\0');
  for (let at = 0; at < fields.length && fields[at]; ) {
    const status = fields[at][0];
    if (status === 'R' || status === 'C') {
      changed.push({ status, from: fields[at + 1], path: fields[at + 2] });
      at += 3;
    } else {
      changed.push({ status, path: fields[at + 1] });
      at += 2;
    }
  }
  const removed = new Set();
  let file = null;
  for (const line of run(gitArgs('diff', '-U0', '-M', base)).split('\n')) {
    if (line.startsWith('--- ')) file = line === '--- /dev/null' ? file : line.slice(6);
    else if (line.startsWith('+++ ')) file = line === '+++ /dev/null' ? file : line.slice(6);
    else if (line.startsWith('-') && file && sourceKind(file) === 'product') {
      const body = line.slice(1).trim();
      if (/^(?:\/\/|\/\*|\*|#)/.test(body)) continue;
      identifiersOf(stripComments(body, extOf(file)), removed);
    }
  }
  return { changed, removed };
}

/** Which of `paths` git ignores, asked once. */
function ignoredBy(root) {
  return (paths) => {
    const done = spawnGit(['check-ignore', '-z', '--stdin'], { cwd: root, encoding: 'utf8', input: `${paths.join('\0')}\0`, windowsHide: true });
    if (done.status !== 0 && done.status !== 1) throw new Error(`git check-ignore failed: ${done.stderr?.trim() ?? done.error?.message}`);
    return new Set(done.stdout.split('\0').filter(Boolean));
  };
}

function print(result, since) {
  const { counts } = result;
  const u = counts.paths.unchecked;
  console.log(`doc-current: ${result.homes.length} present homes, ${counts.spans} code spans (${counts.allowed} let through by the allow-list, in the counts below)`);
  console.log(`  paths    ${counts.paths.present} present, ${counts.paths.missing} missing, unchecked ${u.placeholder} placeholder, ${u.bare} bare name, ${u.runtime} runtime, ${u.unanchored} unanchored`);
  console.log(`  names    ${counts.symbols}: ${counts.symbolsBy.file} file reference, ${counts.symbolsBy.product} product, ${counts.symbolsBy.testOnly} test-only, ${counts.symbolsBy.absent} absent`);
  console.log(`  routes   ${counts.routes.present} present, ${counts.routes.unknown} unknown; variables ${counts.variables.present} present, ${counts.variables.testOnly} test-only, ${counts.variables.absent} absent`);
  const touched = since ? result.findings.filter((f) => result.since.touched.includes(f.home)) : result.findings;
  // A name only test source has is printed after the rest: a test's name in a record is often right, so it asks for a look, not a fix.
  const shown = [...touched.filter((f) => !f.kind.endsWith('test-only')), ...touched.filter((f) => f.kind.endsWith('test-only'))];
  if (since) {
    console.log(`since ${since}: ${result.since.touched.length} present home(s) touched, ${result.since.quoting.length} span(s) quote what the diff deleted or renamed`);
    for (const q of result.since.quoting) console.log(`${q.home}:${q.line}: quotes \`${q.span}\`, ${q.what}`);
  }
  for (const f of shown) console.log(`${f.home}:${f.line}: ${f.kind} \`${f.span}\`: ${f.say}`);
  for (const span of result.unusedAllow) console.log(`daoris.json: documents.present.allow names \`${span}\`, which no finding needs any more`);
  console.log(`doc-current: ${shown.length} finding(s)${since ? ' in the touched homes' : ''}; reports only, never fails`);
}

if (isMain(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    const json = args.includes('--json');
    const at = args.indexOf('--since');
    const base = at >= 0 ? args[at + 1] : null;
    const rest = args.filter((arg, i) => arg !== '--json' && arg !== '--since' && i !== at + 1);
    if (rest.length || (at >= 0 && !base)) throw new Error('usage: node tools/doc-current.mjs [--since <base>] [--json]');
    const root = dirname(dirname(fileURLToPath(import.meta.url)));
    const files = repositoryFiles(root);
    let diff = null;
    if (base) {
      const merged = spawnGit(gitArgs('merge-base', base, 'HEAD'), { cwd: root, encoding: 'utf8', windowsHide: true });
      const from = merged.status === 0 ? merged.stdout.trim() : base;
      diff = sinceDiff(root, from);
    }
    const result = auditCurrent({
      manifest: JSON.parse(readFileSync(join(root, 'daoris.json'), 'utf8')),
      files,
      read: (file) => readFileSync(join(root, file), 'utf8'),
      ignored: ignoredBy(root),
      diff,
    });
    if (json) console.log(JSON.stringify({ ...result, since: result.since && { ...result.since } }, null, 2));
    else print(result, base);
  } catch (error) {
    console.error(`doc-current: ${error.message}`);
    process.exitCode = 2;
  }
}
