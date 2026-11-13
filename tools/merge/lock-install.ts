/**
 * The npm installs a run owes before its gates. A merge whose `package-lock.json` moved leaves the folder's `node_modules`
 * installed from the lockfile before it, and a gate then runs without a package the merged lockfile added. Each folder whose
 * lockfile moved installs once (`npm ci`), whichever gate starts first, and every gate waits for it.
 */
import { DEPENDENCY_FOLDERS } from '../merge-branch/dependencies.mjs';

/** A folder's lockfile as git names it from the tree's root. */
const lockfileOf = (folder: string): string => (folder === '.' ? 'package-lock.json' : `${folder}/package-lock.json`);

/** The folders of `folders` whose `package-lock.json` is among the `changed` paths, in the folders' order. */
export function movedLockfiles(changed: readonly string[], folders: readonly string[] = DEPENDENCY_FOLDERS): string[] {
  const paths = new Set(changed.map((path) => path.replaceAll('\\', '/')));
  return folders.filter((folder) => paths.has(lockfileOf(folder)));
}

const listed = (folders: readonly string[]): string => folders.join(' and ');

/** The line the plan says of the installs: those owed, and those already from the moved lockfile; null when none moved. */
export function installLine(owed: readonly string[], current: readonly string[]): string | null {
  if (!owed.length && !current.length) return null;
  if (!owed.length) return `package-lock.json moved in ${listed(current)}, already installed from it: no npm ci before the gates`;
  const already = current.length ? `; ${listed(current)} ${current.length === 1 ? 'is' : 'are'} already installed from its own` : '';
  return `npm ci in ${listed(owed)} before the first gate: package-lock.json moved there${already}`;
}

export type LockSeams = {
  /** The paths the tree changed against the commit before the merge. */
  changed: (where: string) => string[];
  /** Whether a folder is installed from its lockfile as it is now. */
  current: (where: string, folder: string) => boolean;
  install: (where: string, folder: string) => Promise<{ ok: boolean; note: string }>;
  say: (line: string) => void;
};

const messageOf = (error: unknown): string => (error instanceof Error ? error.message : String(error));

/** The installs of one run, by tree: `ready` answers null once each is done, or the failure every gate reports in its place. */
export class LockInstalls {
  readonly #seams: LockSeams;
  readonly #trees = new Map<string, Promise<string | null>>();

  constructor(seams: LockSeams) {
    this.#seams = seams;
  }

  ready(where: string): Promise<string | null> {
    const kept = this.#trees.get(where);
    if (kept) return kept;
    const started = this.#install(where);
    this.#trees.set(where, started);
    return started;
  }

  async #install(where: string): Promise<string | null> {
    let moved: string[];
    try {
      moved = movedLockfiles(this.#seams.changed(where));
    } catch (error) {
      this.#seams.say(`the merge's changed paths did not read (${messageOf(error)}): no npm ci before the gates`);
      return null;
    }
    const current = moved.filter((folder) => this.#seams.current(where, folder));
    const owed = moved.filter((folder) => !current.includes(folder));
    const line = installLine(owed, current);
    if (line) this.#seams.say(line);
    for (const folder of owed) {
      let done: { ok: boolean; note: string };
      try {
        done = await this.#seams.install(where, folder);
      } catch (error) {
        done = { ok: false, note: messageOf(error) };
      }
      if (!done.ok) return `npm ci failed in ${folder} (${done.note}), so no gate ran on the merged lockfile`;
    }
    return null;
  }
}
