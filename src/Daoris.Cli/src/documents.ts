// The development documents a repository declares: roles bound to paths (DOC3; D122,
// `docs/2026-10-01-development-documents-design.md` §2.7–§2.8).
//
// The canon names its records by their job (the backlog, the decisions, the fix log) and never by their
// path, which is right for doctrine and costs every session in every adopter a search. A repository
// declares where each one is in its manifest's `documents`; `sync` renders that as the region's *Where
// things are* table, and `check` holds the table and the paths to the facts. Nouns, so the manifest (D26).
//
// 🔴 **A repository that declares nothing sees no change.** No table, no report, and a region byte for
// byte what it was, so no adopter and no example needs a re-sync until it declares.
//
// The service's `RepositoryScanner` reads the same field for the declared decisions, fixes and archive
// (DOC5): the manifest-reading table in `documents-manifest.test.ts` is the one its reader matches.

import type { DeclaredDocument, Harness, LinkProblem } from './types.ts';
import { readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { declared, escapes, within } from './layout.ts';
import { linkProblems } from './links.ts';
import { DaorisError } from './errors.ts';

/** How a role is bound: to a path the repository chooses, or only to a ceiling on a file the layout names. */
type Binding = 'path' | 'ceiling';

/**
 * The roles the manifest may declare, in the order everything lists them, each with its job in the
 * CLI's fixed words, which are the canon's (`development-documents`, its roles table).
 *
 * @remarks
 * A closed set, refused by name when unknown, as an unknown harness is (D23). `brief` and `room` take a
 * ceiling and no path: the brief is the repository's own part of the root instruction file, outside the
 * region, and the rooms are `rooms`'. `knowledge` and `skill` are roles too, and are not declared here:
 * the index already lists them from the target, which is where the descriptor keeps them.
 */
export const ROLES: readonly { role: string; binding: Binding; job: string }[] = [
  { role: 'brief', binding: 'ceiling', job: 'what this is, the constraint every change serves, and where everything else is' },
  { role: 'room', binding: 'ceiling', job: "one folder's conventions, traps and checks" },
  { role: 'router', binding: 'path', job: 'every document, its kind and its standing' },
  { role: 'decisions', binding: 'path', job: 'numbered decisions, each with why and what it rejected' },
  { role: 'backlog', binding: 'path', job: 'open work only, each row naming its contract and its proof' },
  { role: 'archive', binding: 'path', job: 'finished work, each with its date and outcome' },
  { role: 'fixes', binding: 'path', job: 'root cause, fix and verification per non-trivial defect' },
  { role: 'changelog', binding: 'path', job: 'what a user of a release sees changed' },
  { role: 'glossary', binding: 'path', job: "the names people and code use for this repository's things" },
  { role: 'gates', binding: 'path', job: 'the checks, their exact commands, and the work a session may do without asking' },
];

/** The roles the index lists from the descriptor's tiers, so nothing is declared for them. */
const TIER_ROLES: Record<string, string> = { knowledge: 'knowledge', skill: 'skills' };

/** The roles whose absence `check` reports: the records the canon's rules name most (D122 §2.8). */
export const RECORD_ROLES = ['backlog', 'decisions'] as const;

const FIELDS = new Set(['path', 'words']);

/**
 * `documents`, checked at the edge (D122 §2.7, D18): what every command reads, before a single path is
 * planned. Refused rather than corrected, as a room is: a role nobody knows, a path that leaves the
 * repository, or a role declared twice is a manifest saying something this tool cannot honour, and
 * honouring half of it would render a table the repository did not write.
 *
 * @param raw The parsed field. Absent, null or empty is none.
 * @param text The manifest's text, for the one refusal parsing hides: JSON keeps the last of two keys
 * with the same name, silently.
 * @returns The declared roles in `ROLES`' order, each path spelled as a declared path is.
 */
export function checkDocuments(raw: unknown, text: string, target: string, harness: Harness): DeclaredDocument[] {
  if (raw === undefined || raw === null) return [];
  if (typeof raw !== 'object' || Array.isArray(raw)) {
    throw new DaorisError(
      `daoris.json declares documents as ${JSON.stringify(raw)} — it is a map from a role to its path: `
      + '{ "decisions": "docs/DECISIONS.md", "backlog": { "path": "TASKS.md", "words": 5000 } }');
  }
  const twice = duplicateMembers(text);
  if (twice !== null) {
    throw new DaorisError(
      `daoris.json declares ${twice} twice — JSON keeps the last one silently, and daoris will not guess `
      + 'which is meant. Keep one');
  }

  const known = ROLES.map((row) => row.role).join(', ');
  const found = new Map<string, DeclaredDocument>();
  for (const [role, value] of Object.entries(raw as Record<string, unknown>)) {
    const name = `daoris.json's documents.${role}`;
    const tier = TIER_ROLES[role];
    if (tier !== undefined) {
      const dir = harness.tiers[tier]?.dir ?? tier;
      throw new DaorisError(
        `daoris.json's documents names '${role}', which the index lists from ${target === '' ? '' : `${target}/`}${dir}/ `
        + '— it is not declared here');
    }
    const spec = ROLES.find((row) => row.role === role);
    if (!spec) throw new DaorisError(`daoris.json's documents names '${role}', which is not a role daoris knows — the roles are ${known}`);

    const shape = spec.binding === 'path'
      ? '{ "path": "<path>", "words": <ceiling> }'
      : '{ "words": <ceiling> }';
    if (spec.binding === 'ceiling' && (typeof value === 'string' || (isObject(value) && 'path' in value))) {
      throw new DaorisError(
        `${name} takes a ceiling and no path — ${role === 'brief'
          ? "the brief is this repository's own part of the root instruction file, outside the region"
          : "the rooms are daoris.json's rooms, each with its own instruction file"}: ${shape}`);
    }
    if (typeof value === 'string') {
      found.set(role, { role, path: checkPath(name, value, target, harness), words: null });
      continue;
    }
    if (!isObject(value)) {
      throw new DaorisError(`${name} is ${JSON.stringify(value)} — a document is its path, or ${shape}`);
    }
    const extra = Object.keys(value).find((key) => !FIELDS.has(key));
    if (extra !== undefined) throw new DaorisError(`${name} has '${extra}', which is not a field — a document is its path, or ${shape}`);
    if (spec.binding === 'path' && typeof value.path !== 'string') {
      throw new DaorisError(`${name} has no path — a document is its path, or ${shape}`);
    }
    const words = value.words;
    if (words !== undefined && !(typeof words === 'number' && Number.isInteger(words) && words > 0)) {
      throw new DaorisError(`${name} has words ${JSON.stringify(words)} — a ceiling is a whole number of words above zero`);
    }
    found.set(role, {
      role,
      path: spec.binding === 'path' ? checkPath(name, value.path as string, target, harness) : null,
      words: words === undefined ? null : words as number,
    });
  }
  return ROLES.flatMap((row) => (found.has(row.role) ? [found.get(row.role)!] : []));
}

const isObject = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

/** A declared path, or a refusal (D18; the design's §2.7): inside the repository, below its root, and clear of Daoris's roots. */
function checkPath(name: string, path: string, target: string, harness: Harness): string {
  const normal = declared(path);
  if (escapes(normal)) throw new DaorisError(`${name} '${path}' leaves the repository — a document is a file or folder inside it`);
  if (normal === '' || normal === '.') throw new DaorisError(`${name} is the repository's root — a document is a file or folder inside it`);
  if (target !== '' && within(normal, target)) {
    throw new DaorisError(`${name} '${path}' sits inside ${target}, where daoris writes the on-demand tiers`);
  }
  if (harness.mirror && within(normal, harness.mirror.root)) {
    throw new DaorisError(`${name} '${path}' sits inside ${harness.mirror.root}, where daoris writes the mirror for ${harness.mirror.reader}`);
  }
  return normal;
}

/**
 * The first member `documents` holds twice, or `documents` itself twice at the top, or null.
 *
 * @remarks
 * `JSON.parse` keeps the last of two keys with one name and says nothing, so the only place a role
 * declared twice can be seen is the text. A scanner of already-valid JSON, so it needs no error paths:
 * the parse that ran first would have refused anything malformed.
 */
export function duplicateMembers(text: string): string | null {
  let at = 0;
  const space = () => { while (at < text.length && /\s/.test(text[at]!)) at += 1; };
  const string = (): string => {
    let out = '';
    at += 1;
    while (text[at] !== '"') {
      if (text[at] === '\\') {
        const next = text[at + 1]!;
        if (next === 'u') {
          out += String.fromCharCode(Number.parseInt(text.slice(at + 2, at + 6), 16));
          at += 6;
        } else {
          out += ({ n: '\n', t: '\t', r: '\r', b: '\b', f: '\f' } as Record<string, string>)[next] ?? next;
          at += 2;
        }
        continue;
      }
      out += text[at];
      at += 1;
    }
    at += 1;
    return out;
  };
  /** The keys of the object at `at`, in order and with repeats, when `collect`; the value skipped either way. */
  const value = (collect = false): string[] => {
    space();
    const opening = text[at];
    if (opening === '"') {
      string();
      return [];
    }
    if (opening === '{' || opening === '[') {
      const closing = opening === '{' ? '}' : ']';
      const keys: string[] = [];
      at += 1;
      space();
      if (text[at] === closing) {
        at += 1;
        return keys;
      }
      for (;;) {
        space();
        if (opening === '{') {
          keys.push(string());
          space();
          at += 1; // the colon
        }
        value();
        space();
        const separator = text[at];
        at += 1;
        if (separator === closing) return collect ? keys : [];
      }
    }
    while (at < text.length && !/[\s,\]}]/.test(text[at]!)) at += 1;
    return [];
  };

  space();
  if (text[at] !== '{') return null;
  at += 1;
  let seen = false;
  for (;;) {
    space();
    if (text[at] === '}') return null;
    const key = string();
    space();
    at += 1; // the colon
    if (key === 'documents') {
      if (seen) return 'documents';
      seen = true;
      const roles = value(true);
      const repeated = roles.find((role, index) => roles.indexOf(role) !== index);
      if (repeated !== undefined) return `documents.${repeated}`;
    } else {
      value();
    }
    space();
    if (text[at] === ',') at += 1;
    else return null;
  }
}

/** The declared roles that carry a path: the rows of the region's table. */
export const declaredPaths = (documents: readonly DeclaredDocument[]): { role: string; path: string }[] =>
  documents.flatMap((doc) => (doc.path === null ? [] : [{ role: doc.role, path: doc.path }]));

/** A role's job, in the CLI's fixed words. */
export const jobOf = (role: string): string => ROLES.find((row) => row.role === role)?.job ?? '';

/** Whitespace-separated tokens: `tools/doc-budgets.mjs`'s count, duplicated deliberately (twins). */
export const wordCount = (text: string): number => text.split(/\s+/).filter(Boolean).length;

/** A declared document that is a link, a link held as text, or below a file where a folder must be. */
export interface DocumentLink extends LinkProblem {
  role: string;
  /** The declared path; `path` is the part of it that is the problem, the same or a folder above. */
  declared: string;
}

/**
 * The declared paths that are not a plain file or folder (D117 §5.4's link table, for a path Daoris
 * points sessions at rather than writes). Never read through: what the table names, every session reads.
 */
export function documentLinks(root: string, documents: readonly DeclaredDocument[]): DocumentLink[] {
  return declaredPaths(documents).flatMap(({ role, path }) =>
    linkProblems(root, { pairs: [path] }).map((problem) => ({ ...problem, role, declared: path })));
}

/** One line about a document link, for `check` and `--dry-run`. */
export function shortDocumentLink(link: DocumentLink): string {
  const at = link.path === link.declared ? '' : `${link.path} `;
  switch (link.kind) {
    case 'link': return `${at ? `${at}is ` : ''}a link: every session sent there reads through it`;
    case 'text': return `${at}looks like a link checked out as text (core.symlinks=false)`;
    case 'file': return `${at ? `${at}is ` : ''}a file where a folder must be`;
  }
}

/** The whole sentence, for `sync`'s refusal. */
export function describeDocumentLink(link: DocumentLink): string {
  const head = `${link.declared}, declared as ${link.role},`;
  const where = link.path === link.declared ? '' : ` (at ${link.path})`;
  switch (link.kind) {
    case 'link':
      return `${head} is a link${where}: every session the region sends there reads through it, and a checkout `
        + "without links reads a path. Declare the file it points at, or make it a file — the choice is this repository's";
    case 'text':
      return `${head} looks like a link checked out as text (core.symlinks=false)${where}: every session the region `
        + "sends there reads a path. Declare the file itself, or make it one — the choice is this repository's";
    case 'file':
      return `${head} sits below a file where a folder must be${where}. Move it aside, or declare where the `
        + "document really is — the choice is this repository's";
  }
}

/** Whether a declared path is there at all: a file or a folder, followed or not. */
export function present(root: string, path: string): 'file' | 'folder' | null {
  try {
    const stat = statSync(join(root, path));
    return stat.isFile() ? 'file' : stat.isDirectory() ? 'folder' : null;
  } catch {
    return null;
  }
}

/**
 * Where a repository usually keeps each record, by conventional names (D122 §2.7): what `init` and
 * `analyze` name as candidates. Matched without regard to case, files before folders, and the
 * repository's own readme never: it is the project's front page, not a router of its documents.
 */
const CANDIDATES: readonly { role: string; files?: readonly string[]; folders?: readonly string[] }[] = [
  { role: 'router', files: ['docs/README.md', 'docs/INDEX.md', 'doc/README.md', 'doc/INDEX.md'] },
  {
    role: 'decisions',
    files: ['DECISIONS.md', 'docs/DECISIONS.md', 'doc/DECISIONS.md'],
    folders: ['adr', 'docs/adr', 'doc/adr', 'decisions', 'docs/decisions', 'docs/architecture/decisions'],
  },
  { role: 'backlog', files: ['TASKS.md', 'TODO.md', 'BACKLOG.md', 'docs/TASKS.md', 'docs/TODO.md', 'docs/BACKLOG.md'] },
  { role: 'archive', files: ['TASK-ARCHIVE.md', 'TASKS-ARCHIVE.md', 'docs/task-archive.md', 'docs/tasks-archive.md'] },
  { role: 'fixes', files: ['FIX-LOG.md', 'FIXLOG.md', 'FIXES.md', 'docs/FIX-LOG.md', 'docs/FIXLOG.md', 'docs/FIXES.md'] },
  { role: 'changelog', files: ['CHANGELOG.md', 'CHANGES.md', 'HISTORY.md', 'docs/CHANGELOG.md'] },
  { role: 'glossary', files: ['GLOSSARY.md', 'docs/GLOSSARY.md'] },
  { role: 'gates', files: ['daoris.gates.json'] },
];

/** A conventional path as this repository spells it, matched one segment at a time without regard to case. */
function caseless(root: string, rel: string): string | null {
  let found = '';
  for (const segment of rel.split('/')) {
    let names: string[];
    try {
      names = readdirSync(join(root, found));
    } catch {
      return null;
    }
    const name = names.find((entry) => entry.toLowerCase() === segment.toLowerCase());
    if (name === undefined) return null;
    found = found === '' ? name : `${found}/${name}`;
  }
  return found;
}

/**
 * The candidates, as `init` and `analyze` print them. Named and never written: declaring is the
 * repository's act, done by its own session in a set-up, and a guess written into the manifest would
 * read as a declaration nobody made.
 */
export function sayCandidates(candidates: readonly { role: string; path: string }[], write: (line: string) => void): void {
  if (!candidates.length) return;
  write('');
  write('  records this repo seems to keep — declare the ones it does in daoris.json\'s "documents" (D122),');
  write('  and sync lists them in the region:');
  for (const { role, path } of candidates) write(`    ${role.padEnd(12)}${path}`);
}

/** The records this repository seems to keep, by role. Reads names only, and writes nothing. */
export function candidateDocuments(root: string): { role: string; path: string }[] {
  const found: { role: string; path: string }[] = [];
  for (const { role, files = [], folders = [] } of CANDIDATES) {
    for (const [paths, kind] of [[files, 'file'], [folders, 'folder']] as const) {
      for (const conventional of paths) {
        const path = caseless(root, conventional);
        if (path !== null && present(root, path) === kind && !found.some((row) => row.path === path)) {
          found.push({ role, path });
        }
      }
    }
  }
  return found;
}
