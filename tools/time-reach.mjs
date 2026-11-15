/**
 * Whether a merge's changed paths reach a test the time ratchet found over its limit, by the census of test units the merge's
 * reached checks use (`tools/test-map.mjs`, through `keyContextOf`): one reach, not a second one by file names. A test over its
 * limit that no changed path reaches is not the merge's: it is slow on this machine under this load, and the merge neither made
 * it slower nor can make it faster, so the ratchet names it and fails nothing. A test a path reaches is the merge's, judged as
 * before after it is measured again alone (`tools/time-ratchet.mjs`).
 *
 * A record's key names its unit: a `file::name` key (Vitest, the node reporter) is the census's unit of that file, a dotnet key
 * `Namespace.Class.Method(args)` the census's class `Namespace.Class`. A test the census holds no unit for is reached: an unknown
 * is judged, never excused.
 */

/** The census unit of the test `key` among `units` (a map from unit name to unit), or null. */
export function unitOfKey(key, units) {
  if (key.includes('::')) return units.get(key.split('::')[0].replace(/\\/g, '/')) ?? null;
  const parts = key.split('(')[0].split('.');
  for (let end = parts.length - 1; end > 0; end -= 1) {
    const found = units.get(parts.slice(0, end).join('.'));
    if (found) return found;
  }
  return null;
}

/** `found` (key -> { ms, limit }) split into the tests `changed` reaches and those it does not, by `world` ({ units, reaches(unit, path) }). */
export function splitByReach(found, changed, world) {
  const mine = {};
  const others = {};
  for (const [key, over] of Object.entries(found)) {
    const unit = unitOfKey(key, world.units);
    const reached = unit === null || changed.some((path) => world.reaches(unit, path));
    (reached ? mine : others)[key] = over;
  }
  return { mine, others };
}

/** The names of the census units the tests of `found` are in: what is run again alone to measure them. */
export function unitsOf(found, world) {
  const names = new Set();
  for (const key of Object.keys(found)) {
    const unit = unitOfKey(key, world.units);
    if (unit) names.add(unit.unit);
  }
  return [...names].sort();
}

/**
 * The over-limit tests of a merge judged by reach: the records scanned (`scan()` -> { over, seen }); where any is over its limit the
 * census is read (`world()`), the tests `changed` reaches are measured again alone (`measure(unitNames)` runs their classes and
 * writes newer records) and the records are scanned again, so a reached test is judged by its time alone. Tests nothing reaches
 * are `others`. With nothing over there is no census and no run.
 */
export async function judgeByReach({ scan, changed, world, measure }) {
  const first = scan();
  if (!Object.keys(first.over).length) return { seen: first.seen, mine: {}, others: {}, measured: [] };
  const loaded = await world();
  const units = unitsOf(splitByReach(first.over, changed, loaded).mine, loaded);
  if (!units.length) return { seen: first.seen, ...splitByReach(first.over, changed, loaded), measured: [] };
  measure(units);
  const again = scan();
  return { seen: again.seen, ...splitByReach(again.over, changed, loaded), measured: units };
}

/** The census of the tree at `root` as a world: its units by name and its reach of a unit by a path. */
export async function loadWorld(root) {
  const { keyContextOf } = await import('./merge-branch/full-set.mjs');
  const { map, context } = keyContextOf(root);
  return { units: new Map(map.units.map((unit) => [unit.unit, unit])), reaches: (unit, path) => context.reaches(unit, path) };
}
