/**
 * The dependency tiers of a merge's `reached` gate. A unit class (no `Needs` trait) runs when a path reaches it. A class that
 * needs a real dependency (git, a process, an agent harness, a browser, the network, the filesystem) runs only when a changed
 * path is in the code that dependency's declaration names (`tiers.needs` in `daoris.gates.json`) and reaches the class; every
 * other such class is left to the nightly run and the stage, which run them whole, and the summary says how many.
 */

export type Tiers = Record<string, string[]>;

/** The declaration's tiers: each dependency with the paths of its code, or none where the gates file holds none. */
export function tiersOf(gatesText: string): Tiers {
  try {
    const parsed = JSON.parse(gatesText) as { tiers?: { needs?: Record<string, unknown> } };
    const needs = parsed.tiers?.needs ?? {};
    return Object.fromEntries(Object.entries(needs).map(([name, paths]) => [name, Array.isArray(paths) ? paths.filter((path): path is string => typeof path === 'string') : []]));
  } catch {
    return {};
  }
}

const inCode = (path: string, code: string[]): boolean => code.some((entry) => (entry.endsWith('/') ? path.startsWith(entry) : path === entry));

/** The changed paths that are the code of any of `needs`, or empty where none changed. */
export function changedCode(needs: string[], changed: string[], tiers: Tiers): string[] {
  return changed.filter((path) => needs.some((name) => inCode(path, tiers[name] ?? [])));
}

/** What a merge left to the night, by dependency: each count of the classes of that dependency not run. */
export function nightLines(left: Record<string, number>): string[] {
  const names = Object.keys(left).sort();
  if (names.length === 0) return [];
  const total = names.reduce((sum, name) => sum + (left[name] as number), 0);
  return [`reached: ${total} dependency classes left to the nightly run (--ci --night) and the stage: ${names.map((name) => `${name} ${left[name]}`).join(', ')}`];
}
