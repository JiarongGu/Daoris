// The line endings `init` pins for an adopter (INIT1; D25 and its note).
//
// `sync` writes BOM-less UTF-8 with LF, and the lock records a hash per document. The tool's own comparisons
// normalize on read (D25), but the bytes a checkout holds are git's answer, and without a `.gitattributes` git
// takes it from each machine's `core.autocrlf`: a Windows clone holds CRLF where `sync` wrote LF. So `init` writes
// the one line that settles it, where the repository has said nothing.
//
// 🔴 Never recorded in the lock, and never touched once it exists. A lock entry for a file the canon does not hold
// is a retirement in D19's table, so the next `sync` would delete it, and refuse it the moment the repository added
// a line of its own. Unrecorded, it is invisible to the tool (D5): written once, like the manifest, and the
// repository's own from then on. One it already has is read, to say whether it pins anything, and kept byte for byte.

import { lstatSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { normalize, writeTextAtomic } from './fsx.ts';

export const GITATTRIBUTES = '.gitattributes';

/** The line `init` writes: git decides what is text, and every text file is LF on every checkout. */
export const LINE_ENDINGS_RULE = '* text=auto eol=lf';

const CONTENT = [
  "# Line endings are pinned, so every checkout holds the LF that `daoris sync` writes whatever a machine's",
  "# core.autocrlf says. Written once by `daoris init`; the file is this repository's own.",
  LINE_ENDINGS_RULE,
  '',
].join('\n');

/**
 * What `init` does about line endings: create the file, or keep the repository's own. `pinned` says whether a kept
 * file pins LF for every file, and is null when what is there could not be read as a file.
 */
export type LineEndingsPlan =
  | { path: typeof GITATTRIBUTES; state: 'create'; content: string }
  | { path: typeof GITATTRIBUTES; state: 'kept'; pinned: boolean | null };

/** Anything at the path, a link to nothing included: whatever is there is the repository's. */
function present(abs: string): boolean {
  try {
    lstatSync(abs);
    return true;
  } catch {
    return false;
  }
}

/**
 * Whether a `.gitattributes` pins LF for every file: the last line for `*` that says anything about `eol` says
 * `eol=lf`. A narrower pattern (`*.md`) leaves some file `sync` writes to the machine, so it does not count.
 */
export function pinsLineEndings(text: string): boolean {
  let pinned = false;
  for (const line of normalize(text).split('\n')) {
    const words = line.trim().split(/\s+/);
    if (words[0] !== '*') continue;
    for (const word of words.slice(1)) {
      if (word === 'eol=lf') pinned = true;
      else if (word.startsWith('eol=') || word === '-eol' || word === '!eol') pinned = false;
    }
  }
  return pinned;
}

/** Reads the disk and writes nothing, so `init` can print it and a test can assert it. */
export function planLineEndings(root: string): LineEndingsPlan {
  const abs = join(root, GITATTRIBUTES);
  if (!present(abs)) return { path: GITATTRIBUTES, state: 'create', content: CONTENT };
  let pinned: boolean | null;
  try {
    pinned = pinsLineEndings(readFileSync(abs, 'utf8'));
  } catch {
    pinned = null;
  }
  return { path: GITATTRIBUTES, state: 'kept', pinned };
}

/** Writes what the plan creates; a kept file is never written. */
export function applyLineEndings(root: string, plan: LineEndingsPlan): void {
  if (plan.state === 'create') writeTextAtomic(join(root, plan.path), plan.content);
}
