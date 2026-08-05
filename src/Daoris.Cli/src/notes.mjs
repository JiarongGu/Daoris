import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from './fsx.mjs';

const CANON_CHANGELOG = 'CHANGELOG.md';

/** Numeric, so 0.10.0 sorts after 0.9.0 — string comparison gets that backwards. */
export function compareVersions(a, b) {
  const left = a.split('.').map(Number);
  const right = b.split('.').map(Number);
  for (let i = 0; i < Math.max(left.length, right.length); i += 1) {
    const diff = (left[i] ?? 0) - (right[i] ?? 0);
    if (diff) return diff;
  }
  return 0;
}

/**
 * Why the canon changed, for a consumer sitting on an older version.
 *
 * `status` and `sync` can already say WHICH files moved, computed from the lock.
 * They cannot say whether it matters — that is a sentence only the author of the
 * change can write, so the canon carries it and ships it in the package. No
 * network, and nothing here can fail a build: a canon with no changelog simply
 * has nothing to add.
 */
export function notesBetween(canonRoot, from, to) {
  const file = join(canonRoot, CANON_CHANGELOG);
  if (!existsSync(file)) return [];

  const sections = [];
  let current = null;
  for (const line of readText(file).split('\n')) {
    const heading = /^##\s+(\d+\.\d+\.\d+)\s*$/.exec(line);
    if (heading) {
      current = { version: heading[1], lines: [] };
      sections.push(current);
    } else if (current) {
      current.lines.push(line);
    }
  }

  return sections
    .filter((s) => compareVersions(s.version, from) > 0 && compareVersions(s.version, to) <= 0)
    .sort((a, b) => compareVersions(a.version, b.version))
    .map((s) => ({ version: s.version, body: s.lines.join('\n').trim() }));
}
