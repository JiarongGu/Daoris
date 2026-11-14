/**
 * The two tiers of a reached run. A merge runs the fast tier: the unit classes its paths reach. The heavy tier is every
 * class that starts real processes or a real git (a suite whose gate runs with the Process settings) and the rehearsals' classes;
 * a merge defers them, and the run after the landing (`heavy-after.ts`) runs them on main's new tip. A builder's own checks run both.
 */

/** Which classes a reached run runs: the merge's fast ones, the heavy ones the landing owes, or every one a builder asks for. */
export type Tier = 'fast' | 'heavy' | 'all';

export const TIERS: readonly Tier[] = Object.freeze(['fast', 'heavy', 'all']);

/** The tier named by a flag's value, or null where it names none. */
export const tierNamed = (value: string | null): Tier | null => TIERS.find((tier) => tier === value) ?? null;

/** A class of the heavy tier: its suite's gate runs with the Process settings, or the suite is a rehearsals project's. */
export const isHeavyUnit = (unit: { suite: string }, processSuites: ReadonlySet<string>): boolean => processSuites.has(unit.suite) || unit.suite.endsWith('-rehearsals');

/** The units a tier runs, and (of the fast tier) the heavy ones it hands to the run after the landing. */
export function splitTier<U extends { suite: string }>(units: readonly U[], tier: Tier, processSuites: ReadonlySet<string>): { kept: U[]; deferred: U[] } {
  if (tier === 'all') return { kept: [...units], deferred: [] };
  const heavy = units.filter((unit) => isHeavyUnit(unit, processSuites));
  if (tier === 'heavy') return { kept: heavy, deferred: [] };
  return { kept: units.filter((unit) => !heavy.includes(unit)), deferred: heavy };
}

/** The line a merge and `--plan` say of the heavy classes the fast tier hands to the landing's run, by suite; none for none. */
export function deferredLines(deferred: readonly { suite: string }[]): string[] {
  if (deferred.length === 0) return [];
  const by = new Map<string, number>();
  for (const unit of deferred) by.set(unit.suite, (by.get(unit.suite) ?? 0) + 1);
  const parts = [...by].map(([suite, count]) => `${suite} ${count}`).join(', ');
  return [`reached: ${deferred.length} heavy-tier classes deferred to the run after the landing (${parts})`];
}
