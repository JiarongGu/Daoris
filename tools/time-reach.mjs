/**
 * Whose a test the time ratchet found over its limit is: the merge's where the merge adds or changes the test's file, or the test
 * ran over half slower than main's alone record of it (`tools/time-main.mjs`); else main's, inherited as a known red with its cause
 * and row, said and failing nothing. The merge's are judged after they are measured again alone (`tools/time-ratchet.mjs`).
 *
 * A record's key names its unit: a `file::name` key (Vitest, the node reporter) is the census's unit of that file, a dotnet key
 * `Namespace.Class.Method(args)` the census's class `Namespace.Class`, whose file the census names. A test of no known file is the
 * merge's: an unknown is judged, never excused.
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

/** How much slower than main's alone record a test the merge does not change must run to be the merge's. */
export const SLOWER = 1.5;

const fileOfKey = (key, units) => (key.includes('::') ? key.split('::')[0].replace(/\\/g, '/') : unitOfKey(key, units)?.file?.replace(/\\/g, '/') ?? null);

/**
 * `found` (key -> { ms, limit }) split into the merge's tests and those it inherits from main: the merge's where `changed` adds or
 * changes its file (`why: 'changed'`; one of no known file is judged, never excused), or where it ran over half slower than
 * `mainMs(key)`, main's alone record of it (`why: 'slower'`). Each carries `main`, that record, where there is one.
 */
export function splitByOwner(found, { changed, world, mainMs }) {
  const mine = {};
  const inherited = {};
  const paths = new Set(changed.map((path) => path.replace(/\\/g, '/')));
  for (const [key, over] of Object.entries(found)) {
    const file = fileOfKey(key, world.units);
    const record = mainMs(key);
    const main = typeof record === 'number' ? { main: record } : {};
    const why = file === null || paths.has(file) ? 'changed' : typeof record === 'number' && over.ms > record * SLOWER ? 'slower' : null;
    if (why) mine[key] = { ...over, why, ...main };
    else inherited[key] = { ...over, ...main };
  }
  return { mine, inherited };
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
 * The over-limit tests of a merge judged by owner: the records scanned (`scan()` -> { over, seen }); where any is over its limit the
 * census is read (`world()`), the merge's tests are measured again alone (`measure(unitNames)` runs their classes and writes newer
 * records) and the records are scanned again, so a test of the merge's is judged by its time alone. The rest are `inherited`. With
 * nothing over there is no census and no run.
 */
export async function judgeByOwner({ scan, changed, world, measure, mainMs }) {
  const first = scan();
  if (!Object.keys(first.over).length) return { seen: first.seen, mine: {}, inherited: {}, measured: [] };
  const loaded = await world();
  const split = (over) => splitByOwner(over, { changed, world: loaded, mainMs });
  const units = unitsOf(split(first.over).mine, loaded);
  if (!units.length) return { seen: first.seen, ...split(first.over), measured: [] };
  measure(units);
  const again = scan();
  return { seen: again.seen, ...split(again.over), measured: units };
}

/** The census of the tree at `root` as a world: its units by name and its reach of a unit by a path. */
export async function loadWorld(root) {
  const { keyContextOf } = await import('./merge-branch/full-set.mjs');
  const { map, context } = keyContextOf(root);
  return { units: new Map(map.units.map((unit) => [unit.unit, unit])), reaches: (unit, path) => context.reaches(unit, path) };
}
