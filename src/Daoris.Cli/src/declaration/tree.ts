import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve } from 'node:path';

/**
 * The folders a walk of the disk never enters where no index says what is tracked: version control's and the dependencies'.
 * Build output and scratch folders are not skipped by name, since a repository may track a file in a folder called `bin`.
 */
const SKIPPED = new Set(['.git', 'node_modules']);

/** The git folder of a checkout: `.git` itself, or the folder a worktree's `.git` file points to; null where there is none. */
function gitFolder(root: string): string | null {
  const dot = join(root, '.git');
  if (!existsSync(dot)) return null;
  if (statSync(dot).isDirectory()) return dot;
  const pointer = /^gitdir:\s*(.+?)\s*$/m.exec(readFileSync(dot, 'utf8'));
  return pointer ? resolve(root, pointer[1] as string) : null;
}

/**
 * The paths the git index tracks, read from its file (versions 2 and 3: an entry is 62 bytes, two more when extended, then its path,
 * the whole padded to eight). The doctrine commands spawn nothing, so git is not asked. Null where the checkout has no index or a
 * version this does not read, and the disk is walked instead.
 */
function trackedFiles(root: string): string[] | null {
  const folder = gitFolder(root);
  if (folder === null || !existsSync(join(folder, 'index'))) return null;
  const bytes = readFileSync(join(folder, 'index'));
  if (bytes.length < 12 || bytes.toString('latin1', 0, 4) !== 'DIRC') return null;
  const version = bytes.readUInt32BE(4);
  if (version !== 2 && version !== 3) return null;
  const count = bytes.readUInt32BE(8);
  const found = new Set<string>();
  let at = 12;
  for (let entry = 0; entry < count; entry += 1) {
    const mode = bytes.readUInt32BE(at + 24);
    const flags = bytes.readUInt16BE(at + 60);
    const head = at + 62 + (version === 3 && (flags & 0x4000) ? 2 : 0);
    const end = (flags & 0xfff) === 0xfff ? bytes.indexOf(0, head) : head + (flags & 0xfff);
    if (mode !== 0o160000) found.add(bytes.toString('utf8', head, end));
    at += Math.ceil((head - at + (end - head) + 1) / 8) * 8;
  }
  return [...found];
}

/**
 * The repository's files, repository-relative with `/` between the parts, sorted: what git tracks where the checkout has an index,
 * else every file on the disk outside the skipped folders. The doctrine commands spawn nothing, so neither asks git.
 */
export function repositoryFiles(root: string): string[] {
  const tracked = trackedFiles(root);
  if (tracked !== null) return tracked.sort();
  const out: string[] = [];
  const walk = (folder: string, prefix: string): void => {
    for (const entry of readdirSync(join(root, folder), { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (!SKIPPED.has(entry.name)) walk(join(folder, entry.name), `${prefix}${entry.name}/`);
      } else out.push(`${prefix}${entry.name}`);
    }
  };
  walk('', '');
  return out.sort();
}

/** Whether `path` lies at or under `folder`. */
export const within = (folder: string, path: string): boolean => {
  const between = relative(folder, path);
  return between === '' || (!between.startsWith('..') && !isAbsolute(between));
};

/** The nearest existing folder at or above `path`. */
export function nearestExisting(path: string): string {
  let at = path;
  while (!existsSync(at)) at = dirname(at);
  return at;
}
