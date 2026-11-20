import { laneMatcher } from '../merge/lanes-file.ts';
import { PACKAGE_ROOTS } from '../merge/packages.ts';
import { blank } from '../partials.mjs';
import { readProjects } from './projects.mjs';

/**
 * A lane's boundary, the package it is to become: the references across it both ways, per type, as a ratchet that only falls, for
 * whichever lane of `daoris.lanes.json` the list or the argument names. Inside is the lane's C# sources but those under a package
 * root, which its build holds to its ports; outside is every other source of a production project (a test project's names follow the
 * code they test). A type is a class, record, struct, interface or enum declared on one side, by its PascalCase name; one declared on
 * both is set aside. A reference is the name as an identifier no dot qualifies (a namespace's segment, a member and a nested type's
 * own name are not read, its outer type is), comments, strings and character literals blanked: the count is the text's, not the
 * compiler's. `inbound` is how often the outside names each inside type, `outbound` how often the inside names each outside type.
 */
const DECLARED = /\b(?:record\s+(?:class|struct)|class|record|struct|interface|enum)\s+([A-Z]\w*)/g;
const NAMED = /(?<![.\w])[A-Z]\w*/g;
const TEST_PROJECT = /(\.Tests|\.Rehearsals|\.Testing)$|^Daoris\.Testing$/;

/** The lane `id` of the lanes file the tree holds, or a thrown error naming what is missing. */
export function laneOf(tree, id) {
  let map;
  try {
    map = JSON.parse(tree.read('daoris.lanes.json'));
  } catch {
    throw new Error('the boundary census reads daoris.lanes.json, which the tree does not hold readable');
  }
  const lane = (map.lanes ?? []).find((each) => each.id === id);
  if (!lane) throw new Error(`no lane '${id}' in daoris.lanes.json: the boundary census counts a declared lane`);
  return lane;
}

const sorted = (counts) => Object.fromEntries(Object.entries(counts).sort(([a], [b]) => (a < b ? -1 : 1)));

/** The references across lane `id`'s boundary in the tree, both ways, per type, and the names set aside as declared on both sides. */
export function scanBoundary(tree, id) {
  const inLane = laneMatcher(laneOf(tree, id).paths);
  const production = readProjects(tree).filter((project) => !TEST_PROJECT.test(project.name)).map((project) => `${project.folder}/`);
  const inside = [];
  const outside = [];
  for (const path of tree.files) {
    if (!path.endsWith('.cs') || /(^|\/)(bin|obj)\//.test(path)) continue;
    if (inLane(path)) {
      if (!PACKAGE_ROOTS.some((root) => path.startsWith(`${root}/`))) inside.push(path);
    } else if (production.some((folder) => path.startsWith(folder))) outside.push(path);
  }
  const code = new Map([...inside, ...outside].map((path) => [path, blank(tree.read(path))]));
  const declaredIn = (paths) => new Set(paths.flatMap((path) => [...code.get(path).matchAll(DECLARED)].map((match) => match[1])));
  const own = declaredIn(inside);
  const others = declaredIn(outside);
  const shared = [...own].filter((name) => others.has(name)).sort();
  for (const name of shared) {
    own.delete(name);
    others.delete(name);
  }
  const count = (paths, names) => {
    const found = {};
    for (const path of paths) for (const [word] of code.get(path).matchAll(NAMED)) if (names.has(word)) found[word] = (found[word] ?? 0) + 1;
    return sorted(found);
  };
  return { inbound: count(outside, own), outbound: count(inside, others), shared, files: { inside: inside.length, outside: outside.length } };
}

/** The ratchet's breaches for each lane `list` names: a type named more often across the boundary than its count, or one that fell. */
export function checkBoundaries(tree, list = {}) {
  const problems = [];
  for (const [id, listed] of Object.entries(list)) {
    let found;
    try {
      found = scanBoundary(tree, id);
    } catch (error) {
      problems.push(`${id}: ${error.message}`);
      continue;
    }
    for (const [way, across] of [['inbound', 'the outside names its type'], ['outbound', 'it names the outside type']]) {
      const held = listed[way] ?? {};
      for (const [name, count] of Object.entries(found[way])) {
        if (count > (held[name] ?? 0)) problems.push(`lane ${id}: ${across} ${name} ${count} times, over the ratchet's ${held[name] ?? 0}: reach it through a port of the boundary instead`);
      }
      for (const [name, count] of Object.entries(held)) {
        if ((found[way][name] ?? 0) < count) problems.push(`lane ${id}: ${across} ${name} ${found[way][name] ?? 0} times, under the ratchet's ${count}: lower the list (node tools/layers-census.mjs --shrink)`);
      }
    }
  }
  return problems;
}

/** The lines that say a lane's counts today, for a reader and the row's proof. */
export function sayBoundary(id, found, top = 8) {
  const total = (counts) => Object.values(counts).reduce((sum, each) => sum + each, 0);
  const heaviest = (counts) => Object.entries(counts).sort(([a, x], [b, y]) => y - x || (a < b ? -1 : 1)).slice(0, top).map(([name, n]) => `${name} ${n}`).join(', ');
  return [
    `layers-census: lane ${id}: ${found.files.inside} sources inside, ${found.files.outside} production sources outside; ${found.shared.length} names declared on both sides set aside`,
    `  inbound: ${total(found.inbound)} references to ${Object.keys(found.inbound).length} of its types${total(found.inbound) ? ` (${heaviest(found.inbound)})` : ''}`,
    `  outbound: ${total(found.outbound)} references to ${Object.keys(found.outbound).length} types outside${total(found.outbound) ? ` (${heaviest(found.outbound)})` : ''}`,
  ];
}
