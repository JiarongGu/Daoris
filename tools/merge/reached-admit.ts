/**
 * Which groups of a reached plan are heavy, and a group run in its place in the machine's admission. A builder's check and a merge's
 * gate share that admission (`tools/machine-admission.mjs`, one capacity), so a Process run, a rehearsal or a whole suite of
 * classes waits for its turn behind the others instead of loading the machine beside them. A light group runs at once.
 */
import type { ReachedGroup } from './reached.ts';

/** A group of this many classes or more builds and hosts as a whole suite does; fewer is a few fast classes. */
export const WHOLE_SUITE_CLASSES = 8;

/** The place in the machine's admission: `take` waits its turn and answers the hold (null where nothing is shared or the wait ended). */
export type Admission = { take: (options: { what: string }) => Promise<{ release: () => void } | null> };

/** By the group's tier and suite, never by a class's name: real processes (the Process settings), a rehearsal, or a whole suite. */
export function groupIsHeavy(group: ReachedGroup, processSuites: ReadonlySet<string>): boolean {
  if (group.runner === 'dotnet' && group.settings) return true;
  if (processSuites.has(group.suite) || group.suite.endsWith('-rehearsals')) return true;
  return group.units.length >= WHOLE_SUITE_CLASSES;
}

/** `fn` run holding the group's place where the group is heavy, let go as `fn` ends however it ends; a light group runs at once. */
export async function inPlace<T>(group: ReachedGroup, { admission, processSuites }: { admission: Admission | null; processSuites: ReadonlySet<string> }, fn: () => Promise<T>): Promise<T> {
  if (!admission || !groupIsHeavy(group, processSuites)) return fn();
  const hold = await admission.take({ what: `reached checks, ${group.suite} (${group.units.length} ${group.units.length === 1 ? 'class' : 'classes'})` });
  try {
    return await fn();
  } finally {
    hold?.release();
  }
}
