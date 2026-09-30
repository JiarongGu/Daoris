// Links, and links held as text (LAYOUT3; D117 §5.4, the last table).
//
// D3 stands: materialization is always a real file. What this adds is the other side of it — a link
// ALREADY at a path Daoris writes. Writing beside and renaming replaces the link with a file holding a
// copy of what it pointed at, and git records the link turned into a file. On a checkout without links
// (`core.symlinks=false`) the link is a text file holding its target, and appending to that text is
// worse: git reads the edit as the link's new target, broken for every other contributor.
//
// 🔴 **Refused, never written through, and never guessed at** — D59's "never guess at a boundary" in
// another shape. On the other side of the guess is how the repository is laid out for its other
// contributors, so the refusal names both answers and leaves the choice to the repository.

import type { LinkProblem } from './types.ts';
import { existsSync, lstatSync, readFileSync } from 'node:fs';
import { join, posix } from 'node:path';

/** What a path is on disk, without following it. */
type Kind = 'link' | 'folder' | 'file' | 'none';

function kindOf(root: string, rel: string): Kind {
  try {
    const stat = lstatSync(join(root, rel));
    // A junction on Windows reports as a link here, which is what it is for this question.
    if (stat.isSymbolicLink()) return 'link';
    return stat.isDirectory() ? 'folder' : 'file';
  } catch {
    return 'none';
  }
}

/** The single token a link held as text is: no whitespace, not prose, not an import, not markup. */
function token(root: string, rel: string): string | null {
  let text: string;
  try {
    text = readFileSync(join(root, rel), 'utf8').replace(/^﻿/, '').trim();
  } catch {
    return null;
  }
  if (!text || text.length > 255 || /\s/.test(text) || /^[@#<>!\[\-*|`]/.test(text)) return null;
  return text;
}

/**
 * Whether an instruction file is a link checked out as text: its whole content a relative path to
 * something beside it.
 *
 * @remarks
 * A probable, and refusing on a probable is safe (D117 §5.4): the reference's `CLAUDE.md` is the nine
 * bytes `AGENTS.md`, which names its partner whether or not the partner exists yet, and a folder link
 * reads `../.agents/skills`. A one-word file that names nothing beside it is prose, however short.
 */
export function heldAsText(root: string, rel: string): boolean {
  const held = token(root, rel);
  if (held === null) return false;
  const name = posix.basename(rel);
  const partner = name === 'CLAUDE.md' ? 'AGENTS.md' : name === 'AGENTS.md' ? 'CLAUDE.md' : null;
  if (held === partner || /^\.\.?\//.test(held)) return true;
  return existsSync(join(root, posix.dirname(rel), held));
}

/** Every folder above a repository-relative path, outermost first — never the repository root itself. */
const above = (rel: string): string[] => {
  const parts = rel.split('/');
  return parts.slice(0, -1).map((_, at) => parts.slice(0, at + 1).join('/'));
};

/**
 * The paths Daoris would write that are not a plain file or folder.
 *
 * @param files Files Daoris writes: each one, and every folder above it, must not be a link.
 * @param folders Folders Daoris writes into: not a link, and not a file where the folder must go.
 * @param pairs Instruction files (`AGENTS.md`, `CLAUDE.md`, a room's pair): not a link, and not one held as text.
 */
export function linkProblems(
  root: string,
  { files = [], folders = [], pairs = [] }:
  { files?: readonly string[]; folders?: readonly string[]; pairs?: readonly string[] },
): LinkProblem[] {
  const found = new Map<string, LinkProblem>();
  const seen = new Map<string, Kind>();
  const look = (rel: string): Kind => {
    if (!seen.has(rel)) seen.set(rel, kindOf(root, rel));
    return seen.get(rel)!;
  };
  const flag = (path: string, kind: LinkProblem['kind']) => {
    if (!found.has(path)) found.set(path, { path, kind });
  };
  const folderAt = (rel: string) => {
    const kind = look(rel);
    if (kind === 'link') flag(rel, 'link');
    else if (kind === 'file') flag(rel, token(root, rel) !== null && /[./]/.test(token(root, rel)!) ? 'text' : 'file');
  };

  for (const rel of [...files, ...folders, ...pairs]) for (const up of above(rel)) folderAt(up);
  for (const rel of folders) folderAt(rel);
  for (const rel of [...files, ...pairs]) if (look(rel) === 'link') flag(rel, 'link');
  for (const rel of pairs) if (look(rel) === 'file' && heldAsText(root, rel)) flag(rel, 'text');

  return [...found.values()].sort((a, b) => a.path.localeCompare(b.path));
}

/** What a person is told about one, in the words D117 §5.4 gives it. */
export function describeLink(problem: LinkProblem): string {
  switch (problem.kind) {
    case 'link':
      return `${problem.path} is a link. daoris never writes through one: writing beside and renaming would `
        + 'replace it with a copy of what it points at, and git would record the link turned into a file. '
        + 'A real file (for CLAUDE.md, one holding @AGENTS.md) or a folder of mirrors works on every checkout; '
        + "the choice is this repository's";
    case 'text':
      return `${problem.path} looks like a link checked out as text (core.symlinks=false); an agent that reads `
        + 'it reads a path. A real file holding @AGENTS.md, or a folder of mirrors, works on every checkout — '
        + "the choice is this repository's";
    case 'file':
      return `${problem.path} is a file where daoris writes a folder. Move it aside; what goes there is `
        + "this repository's choice";
  }
}

/** The one-line form, for `--dry-run` and `check`. */
export function shortLink(problem: LinkProblem): string {
  return problem.kind === 'link' ? 'a link: daoris never writes through one'
    : problem.kind === 'text' ? 'looks like a link checked out as text (core.symlinks=false)'
      : 'a file where daoris writes a folder';
}
