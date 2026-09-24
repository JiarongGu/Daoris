// The harness's trust flag — asked, and written only on the person's word (D73).
//
// Claude Code ignores a folder's own `permissions.allow` until a person has accepted that folder in the
// account's `.claude.json` (DEPLOY1; both doors, measured). The flag IS the person's grant, so nothing
// here runs at adoption, sync or spawn: `daoris agent trust … --yes` is the terminal's door, and the
// desktop's confirmation of a hold the driver is showing is the screen's.
//
// The CLI's twin of the driver's `ClaudeTrust`. The two share no code — the FILE is the contract — so
// the same cases are asserted on both sides:
//
//   1. A folder is matched in any spelling Windows gives it: case, separator, a trailing one.
//   2. Never recorded is NOT trusted (the harness prompts on first visit); no file, or one this build
//      cannot read, is unknown — for the question.
//   3. For a WRITE, unreadable is a refusal: the file is left exactly as it was.
//   4. One flag moves. An existing entry is updated in place under the key the harness wrote; a new
//      one takes the form Claude Code writes today (forward slashes).
//
// It edits one file and nothing else: no spawn, no network. The file is Claude Code's own, under the
// account's configuration home — the only file outside the Daoris home any command writes, and only
// because a person named the folder.

import { existsSync, readFileSync } from 'node:fs';
import { DaorisError } from './errors.ts';
import { writeTextAtomic } from './fsx.ts';

/** The harness's own config, in the account's configuration home. The driver's `ClaudeTrust.FileName`. */
export const TRUST_FILE = '.claude.json';

/** What a grant did: the key it wrote under, whether the file moved, and whether a re-read says yes. */
export interface TrustGrant {
  key: string;
  changed: boolean;
  verified: boolean;
}

/** Windows spells one folder several ways, and all of them name the same place. */
function normalise(path: string): string {
  return path.replaceAll('/', '\\').replace(/\\+$/, '').toLowerCase();
}

/** The key a folder the harness never recorded is written under: forward slashes, no trailing one. */
export function trustKey(folder: string): string {
  return folder.replaceAll('\\', '/').replace(/\/+$/, '');
}

type Projects = Record<string, unknown>;

/** The file's projects, or null for no file, unreadable JSON, or a shape this build does not know. */
function read(file: string): { root: Record<string, unknown>; projects: Projects } | null {
  if (!existsSync(file)) return null;
  let root: unknown;
  try {
    root = JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
  if (!root || typeof root !== 'object' || Array.isArray(root)) return null;
  const projects = (root as Record<string, unknown>).projects;
  if (!projects || typeof projects !== 'object' || Array.isArray(projects)) return null;
  return { root: root as Record<string, unknown>, projects: projects as Projects };
}

function keyOf(projects: Projects, folder: string): string | undefined {
  const wanted = normalise(folder);
  return Object.keys(projects).find((key) => normalise(key) === wanted);
}

/** `true` accepted, `false` definitely not (never recorded counts), `null` unknown. */
export function isTrusted(file: string, folder: string): boolean | null {
  const held = read(file);
  if (!held) return null;
  const key = keyOf(held.projects, folder);
  if (key === undefined) return false;
  const entry = held.projects[key];
  return !!entry && typeof entry === 'object' && (entry as Record<string, unknown>).hasTrustDialogAccepted === true;
}

/**
 * Set `hasTrustDialogAccepted` for `folder` in `file`, and nothing else in it.
 *
 * @remarks
 * 🔴 The harness rewrites this file itself, whole, whenever it saves its state — so a Claude Code
 * already running under this account may save a copy it read before this grant, and undo it. That is
 * not silent: the driver re-reads the file before every start, so a lost grant shows as the same hold
 * again. `verified` is the re-read at the moment of writing, not a promise about later.
 *
 * Numbers round-trip through `JSON.parse`: an integer beyond 2^53 would lose precision. The fields
 * Claude Code keeps there are counters, costs and durations, none near it.
 */
export function grantTrust(file: string, folder: string): TrustGrant {
  let original: string | null = null;
  let root: Record<string, unknown>;
  let projects: Projects;

  if (existsSync(file)) {
    original = readFileSync(file, 'utf8');
    const held = read(file);
    if (held) {
      ({ root, projects } = held);
    } else {
      // The one shape that is not a refusal: a readable object with no `projects` yet.
      let parsed: unknown;
      try { parsed = JSON.parse(original); } catch { throw unreadable(file); }
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed) || 'projects' in parsed) {
        throw unreadable(file);
      }
      root = parsed as Record<string, unknown>;
      projects = {};
      root.projects = projects;
    }
  } else {
    projects = {};
    root = { projects };
  }

  let key = keyOf(projects, folder);
  if (key !== undefined) {
    const entry = projects[key];
    if (!entry || typeof entry !== 'object' || Array.isArray(entry)) throw unreadable(file);
    if ((entry as Record<string, unknown>).hasTrustDialogAccepted === true) {
      return { key, changed: false, verified: isTrusted(file, folder) === true };
    }
    (entry as Record<string, unknown>).hasTrustDialogAccepted = true;
  } else {
    key = trustKey(folder);
    projects[key] = { hasTrustDialogAccepted: true };
  }

  // The harness's own formatting: two-space indent, LF, and the trailing newline it had.
  let text = JSON.stringify(root, null, 2);
  if (original === null || original.endsWith('\n')) text += '\n';
  writeTextAtomic(file, text);

  return { key, changed: true, verified: isTrusted(file, folder) === true };
}

function unreadable(file: string): DaorisError {
  return new DaorisError(
    `\`${file}\` is not a file this build can read, so nothing was written to it — accept the trust `
    + 'prompt by running `claude` in that folder instead.');
}
