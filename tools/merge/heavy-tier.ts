/**
 * The two tiers of a reached run. A merge runs the fast tier: the unit classes its paths reach, and the end-to-end specs nearest the
 * change (`nearEndToEnd`). The heavy tier is every class that starts real processes or a real git (a suite whose gate runs with the
 * Process settings), the rehearsals' classes and the other end-to-end specs; a merge defers them, and the run after the landing
 * (`heavy-after.ts`) runs them on main's new tip. A builder's own checks run both.
 */
import { NEAR_HOPS, nearness } from './reached.ts';

/** Which classes a reached run runs: the merge's fast ones, the heavy ones the landing owes, or every one a builder asks for. */
export type Tier = 'fast' | 'heavy' | 'all';

export const TIERS: readonly Tier[] = Object.freeze(['fast', 'heavy', 'all']);

/** The tier named by a flag's value, or null where it names none. */
export const tierNamed = (value: string | null): Tier | null => TIERS.find((tier) => tier === value) ?? null;

/** A suite of the heavy tier: its gate runs with the Process settings, it is a rehearsals project's, or an end-to-end tier. */
export const isHeavySuite = (suite: string, processSuites: ReadonlySet<string>): boolean => processSuites.has(suite) || suite.endsWith('-rehearsals') || suite.endsWith('-e2e');

export const isHeavyUnit = (unit: { suite: string }, processSuites: ReadonlySet<string>): boolean => isHeavySuite(unit.suite, processSuites);

/**
 * How many end-to-end specs the fast tier runs, the nearest the change. A spec seeds a scratch repository and runs the tool in it, a
 * minute or two, and the specs of one run start side by side, so three cost a merge about the longest of them. Three hold the two
 * specs that would have stopped MAINRED1's end-to-end reds: the nearest of fifteen within two reads of a tier's move, and of thirteen
 * of a census scope's.
 */
export const E2E_FAST = 3;

/** What the fast tier reads of a change to choose its end-to-end specs: the changed paths and each spec's fewest reads from them. */
export type Nearness<U> = { changed: readonly string[]; hopsOf: (unit: U) => number | undefined; bound?: number };

/** A file's name without its folder and its kinds: `reached.e2e.ts` and `reached.ts` are both `reached`. */
const stemOf = (path: string): string => (path.split('/').at(-1) ?? '').replace(/(\.(e2e|test|spec))?\.[^.]+$/, '');

/**
 * The end-to-end specs of `units` the fast tier runs: those within `NEAR_HOPS` reads of a changed path (its own file 0, what it imports
 * or names 1), the change's own first (a spec named as a changed file is), then the fewest reads, then named for a changed file's
 * prefix or in its folder, then the census's order; at most `bound`.
 */
export function nearEndToEnd<U extends { suite: string; file?: string }>(units: readonly U[], { changed, hopsOf, bound = E2E_FAST }: Nearness<U>): U[] {
  const stems = new Set(changed.map(stemOf));
  return units.filter((unit) => unit.suite.endsWith('-e2e'))
    .map((unit, index) => ({ unit, index, hops: hopsOf(unit), own: stems.has(stemOf(unit.file ?? '')) ? 0 : 1, near: nearness({ file: unit.file ?? '' }, changed) }))
    .filter((each): each is typeof each & { hops: number } => each.hops !== undefined && each.hops <= NEAR_HOPS)
    .sort((a, b) => a.own - b.own || a.hops - b.hops || b.near - a.near || a.index - b.index)
    .slice(0, bound).map((each) => each.unit);
}

/** The units a tier runs, and (of the fast tier) the heavy ones it hands to the run after the landing; `near` lets it run the nearest end-to-end specs. */
export function splitTier<U extends { suite: string; file?: string }>(
  units: readonly U[], tier: Tier, processSuites: ReadonlySet<string>, near?: Nearness<U>,
): { kept: U[]; deferred: U[] } {
  if (tier === 'all') return { kept: [...units], deferred: [] };
  const heavy = units.filter((unit) => isHeavyUnit(unit, processSuites));
  if (tier === 'heavy') return { kept: heavy, deferred: [] };
  const early = new Set(near ? nearEndToEnd(heavy, near) : []);
  return { kept: units.filter((unit) => !heavy.includes(unit) || early.has(unit)), deferred: heavy.filter((unit) => !early.has(unit)) };
}

/**
 * The lines a merge and `--plan` say of the heavy classes the fast tier hands to the landing's run, by suite, and the end-to-end specs
 * among them by name; none for none.
 */
export function deferredLines(deferred: readonly { suite: string; file?: string }[]): string[] {
  if (deferred.length === 0) return [];
  const by = new Map<string, number>();
  for (const unit of deferred) by.set(unit.suite, (by.get(unit.suite) ?? 0) + 1);
  const parts = [...by].map(([suite, count]) => `${suite} ${count}`).join(', ');
  const lines = [`reached: ${deferred.length} heavy-tier classes deferred to the run after the landing (${parts})`];
  const specs = deferred.filter((unit) => unit.suite.endsWith('-e2e')).map((unit) => unit.file ?? unit.suite);
  if (specs.length) lines.push(`reached: end-to-end specs deferred, past the ${E2E_FAST} nearest the change: ${specs.slice(0, 8).join(', ')}${specs.length > 8 ? ` and ${specs.length - 8} more` : ''}`);
  return lines;
}
