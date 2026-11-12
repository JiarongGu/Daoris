/**
 * The census's answers as the key of a reached class needs them (`tools/merge/class-reuse.ts`): which tree files reach a unit, and
 * the digest of each as a result can see it. A path the census cannot place (`whole`) is a class's input only where it builds the
 * class: its test project's own files, a source project's root files where the class names an area of that project, and the build
 * files (`.props`, `.targets`) of a folder above the test project; a root file, a document or a tool outside every project is not.
 */
import { createHash } from 'node:crypto';
import { posix } from 'node:path';
import { TS_SUITES, tsReaches, unitReaches } from '../test-map.mjs';
import { pathReach } from '../process-reach.mjs';
import { propertiesSetTrue, isMsbuildFile, resultText } from '../merge/wiring.ts';

const BUILD_FILE = /^(Directory\.(Build|Packages)\.(props|targets)|Directory\.Build\.rsp|global\.json|nuget\.config|[^/]*\.(props|targets))$/i;
const WIDE_ROOTS = /^(src|tools)\//;

/** The key context of `map` over the tree whose files are `files`, read by `read`, under `toolchain` and the gates' declarations. */
export function reuseContext({ map, files, read, toolchain, gates }) {
  const setTrue = propertiesSetTrue(files.filter(isMsbuildFile).map(read));
  const digests = new Map();
  const digestOf = (path) => {
    if (!digests.has(path)) digests.set(path, createHash('sha1').update(resultText(path, read(path), setTrue)).digest('hex'));
    return digests.get(path);
  };
  const suiteOf = new Map(map.dotnet.map((each) => [`${each.suite}|${each.project}`, each]));
  const classOf = new Map();
  for (const each of map.dotnet) for (const cls of each.census.classes) classOf.set(`${each.suite}|${each.project}|${cls.full}`, cls);
  const unitOf = new Map(map.units.map((each) => [`${each.suite}|${each.unit}`, each]));
  const placements = new Map();
  const placed = (suite, path) => {
    const memo = placements.get(suite) ?? new Map();
    placements.set(suite, memo);
    if (!memo.has(path)) memo.set(path, pathReach(path, suite.census));
    return memo.get(path);
  };

  const dotnetReaches = (unit, path) => {
    const suite = suiteOf.get(`${unit.suite}|${unit.project}`);
    const found = placed(suite, path);
    if (found.none) return false;
    if (!found.whole) return unitReaches(unit, path, map);
    if (path.startsWith(`${unit.project}/`)) return true;
    const source = suite.census.sources.find((folder) => path.startsWith(`${folder}/`));
    if (source) {
      const reach = classOf.get(`${unit.suite}|${unit.project}|${unit.unit}`)?.reach ?? [];
      return !reach.length || reach.includes('*') || reach.some((area) => area === source || area.startsWith(`${source}/`));
    }
    const folder = posix.dirname(path);
    return BUILD_FILE.test(posix.basename(path)) && (folder === '.' || unit.project.startsWith(`${folder}/`));
  };
  const wholeOf = (suite) => TS_SUITES.find((each) => each.suite === suite)?.whole ?? [];
  const tsUnitReaches = (unit, path) => {
    if (path === unit.file || wholeOf(unit.suite).includes(path)) return true;
    if (unit.reach.wide.length) return WIDE_ROOTS.test(path);
    return tsReaches(unit.reach, path);
  };
  const reaches = (unit, path) => (unit.kind === 'dotnet' ? dotnetReaches(unit, path) : tsUnitReaches(unit, path));

  const candidates = new Map();
  const declarations = new Map(gates.map((gate) => [gate.name, JSON.stringify([gate.run, gate.memoryGb ?? null, gate.clock ?? false])]));
  return {
    reaches: (unit, path) => reaches(unitOf.get(`${unit.suite}|${unit.unit}`) ?? unit, path),
    candidates: (unit) => {
      const full = unitOf.get(`${unit.suite}|${unit.unit}`) ?? unit;
      const group = full.kind === 'dotnet' ? `${full.suite}|${full.project}` : full.suite;
      if (!candidates.has(group)) {
        candidates.set(group, full.kind === 'dotnet' ? files.filter((path) => {
          const suite = suiteOf.get(`${full.suite}|${full.project}`);
          const found = placed(suite, path);
          return !found.none && (!found.whole || path.startsWith(`${full.project}/`) || suite.census.sources.some((folder) => path.startsWith(`${folder}/`))
            || (BUILD_FILE.test(posix.basename(path)) && (posix.dirname(path) === '.' || full.project.startsWith(`${posix.dirname(path)}/`))));
        }) : files);
      }
      return candidates.get(group);
    },
    digestOf,
    declaration: (suite) => declarations.get(suite) ?? '',
    toolchain,
  };
}
