import { readFolder } from './folder.ts';
import { readDeclaration, type Overlap, type Reading } from './reading.ts';
import type { Declaration, Split } from './split.ts';

/**
 * The proof a conversion owes: the folder and the home, read back with nothing from the old file, resolve to it member
 * for member. Every gate's commands and other members, its expanded reach, the set holding each tracked test file and the gates' order
 * are compared; a difference is named with what the old file says and what the folder says, and stops the conversion. Sets that hold
 * one file today are not a difference of the conversion but a thing the repository must resolve, so they are named apart.
 */

type Obj = Record<string, unknown>;

export type DifferenceKind = 'folder' | 'gate' | 'order' | 'reach' | 'set' | 'member';

export interface Difference {
  kind: DifferenceKind;
  /** What differs: a gate, a file, a member by its path. */
  subject: string;
  /** What the old file says. */
  was: string;
  /** What the folder and the home say. */
  now: string;
}

export interface Proof {
  differences: Difference[];
  /** Files two sets hold today, which the folder's format refuses until the repository resolves them. */
  overlaps: Overlap[];
  gates: number;
  testFiles: number;
  /** Each gate's expanded reach in the old file, null where it names no paths. */
  reaches: Record<string, string[] | null>;
}

const isObject = (value: unknown): value is Obj => value !== null && typeof value === 'object' && !Array.isArray(value);
const shown = (value: unknown): string => {
  if (value === undefined) return '(absent)';
  const text = JSON.stringify(value);
  return text.length > 120 ? `${text.slice(0, 117)}...` : text;
};
const named = (value: unknown): value is Obj & { name: string } => isObject(value) && typeof value.name === 'string';

/** The members that differ between two values, by path: a list of named members is compared by name, any other list as a whole. */
function members(was: unknown, now: unknown, path: string, out: Difference[]): void {
  if (JSON.stringify(was) === JSON.stringify(now)) return;
  const at = (key: string): string => (path ? `${path}.${key}` : key);
  if (isObject(was) && isObject(now)) {
    for (const key of [...new Set([...Object.keys(was), ...Object.keys(now)])].sort()) members(was[key], now[key], at(key), out);
  } else if (Array.isArray(was) && Array.isArray(now) && was.every(named) && now.every(named)) {
    const byName = (list: readonly (Obj & { name: string })[]) => new Map(list.map((each) => [each.name, each]));
    const before = byName(was);
    const after = byName(now);
    for (const name of [...new Set([...before.keys(), ...after.keys()])]) {
      const subject = `${path}[${name}]`;
      if (!after.has(name)) out.push({ kind: path === 'gates' ? 'gate' : 'member', subject, was: 'declared', now: '(absent)' });
      else if (!before.has(name)) out.push({ kind: path === 'gates' ? 'gate' : 'member', subject, was: '(absent)', now: 'declared' });
      else members(before.get(name), after.get(name), subject, out);
    }
  } else out.push({ kind: 'member', subject: path, was: shown(was), now: shown(now) });
}

/** What a reading of the old file lets through to a gate's reach: the files only one side reaches, each named. */
function reachDifferences(was: Reading, now: Reading): Difference[] {
  const out: Difference[] = [];
  for (const name of was.order.filter((each) => now.order.includes(each))) {
    const before = was.reach[name] ?? null;
    const after = now.reach[name] ?? null;
    if (before === null && after === null) continue;
    if (before === null || after === null) {
      out.push({ kind: 'reach', subject: name, was: before === null ? 'every change' : `${before.length} files`, now: after === null ? 'every change' : `${after.length} files` });
      continue;
    }
    const lost = before.filter((file) => !after.includes(file));
    const gained = after.filter((file) => !before.includes(file));
    if (lost.length || gained.length) out.push({ kind: 'reach', subject: name, was: lost.join(', '), now: gained.join(', ') });
  }
  return out;
}

/**
 * Proves that `split`, written from `old`, resolves to `old` over `files`. `old` is the declaration as read; the folder and the
 * home are read back into the same shape and the two compared, so a tampered or mistaken conversion is named, never trusted.
 */
export function proveSame(old: Declaration, split: Split, files: readonly string[]): Proof {
  const { declaration, refusals } = readFolder(split.folder, split.home);
  const expected = structuredClone(old);
  for (const path of split.product) {
    const [section, member] = path.split('.') as [string, string];
    if (isObject(expected[section])) delete (expected[section] as Obj)[member];
  }

  const was = readDeclaration(expected, files);
  const now = readDeclaration(declaration, files);
  const differences: Difference[] = refusals.map((reason) => ({ kind: 'folder', subject: 'daoris/', was: '', now: reason }));

  const memberDifferences: Difference[] = [];
  members(expected, declaration, '', memberDifferences);
  differences.push(...memberDifferences.filter((each) => each.kind === 'gate'));

  if (was.order.length === now.order.length && was.order.every((name) => now.order.includes(name)) && was.order.join(', ') !== now.order.join(', ')) {
    differences.push({ kind: 'order', subject: 'gates', was: was.order.join(', '), now: now.order.join(', ') });
  }
  differences.push(...reachDifferences(was, now));

  const overlapped = new Set([...was.overlaps, ...now.overlaps].map((each) => each.file));
  for (const file of [...new Set([...Object.keys(was.holder), ...Object.keys(now.holder)])].sort()) {
    if (overlapped.has(file) || was.holder[file] === now.holder[file]) continue;
    differences.push({ kind: 'set', subject: file, was: was.holder[file] ?? '(no set)', now: now.holder[file] ?? '(no set)' });
  }
  differences.push(...memberDifferences.filter((each) => each.kind !== 'gate'));

  return { differences, overlaps: was.overlaps, gates: was.order.length, testFiles: Object.keys(was.holder).length, reaches: was.reach };
}
