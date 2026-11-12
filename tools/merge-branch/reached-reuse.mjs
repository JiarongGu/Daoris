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

/** The build files MSBuild and NuGet apply to every project below them; any other `.props` builds only the project that imports it. */
const AUTO_IMPORTED = /^(Directory\.(Build|Packages)\.(props|targets)|Directory\.Build\.rsp|global\.json|nuget\.config)$/i;
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

  // The build files a test project builds with: those applied to everything below them, and each a project of its closure imports.
  // Each file a project of the test project's closure imports, by name, with the folders of the projects that import it ('*' for one
  // applied to every project below it).
  const importers = new Map();
  const importsOf = (suite) => {
    if (!importers.has(suite.project)) {
      const folders = [suite.project, ...suite.census.sources];
      const ancestors = suite.project.split('/').map((_, at, parts) => parts.slice(0, at).join('/'));
      const imports = new Map();
      for (const path of files) {
        const folder = posix.dirname(path) === '.' ? '' : posix.dirname(path);
        const applied = AUTO_IMPORTED.test(posix.basename(path)) && ancestors.includes(folder);
        if (!applied && !(path.endsWith('.csproj') && folders.includes(folder))) continue;
        for (const found of read(path).matchAll(/<Import\b[^>]*\bProject\s*=\s*"([^"]+)"/g)) {
          const name = found[1].split(/[\\/]/).at(-1).toLowerCase();
          imports.set(name, new Set([...(imports.get(name) ?? []), applied ? '*' : folder]));
        }
      }
      importers.set(suite.project, imports);
    }
    return importers.get(suite.project);
  };
  /** Whether a build file builds a class of `suite` whose reach is `reach` (null: any class): one applied to its project's folder, or one a project it names imports. */
  const buildsProject = (suite, path, reach) => {
    const folder = posix.dirname(path);
    const name = posix.basename(path);
    if (AUTO_IMPORTED.test(name)) return folder === '.' || suite.project.startsWith(`${folder}/`);
    const by = importsOf(suite).get(name.toLowerCase());
    if (!by) return false;
    if (by.has('*') || by.has(suite.project) || reach === null) return true;
    return !reach.length || reach.includes('*') || [...by].some((source) => reach.some((area) => area === source || area.startsWith(`${source}/`)));
  };

  const dotnetReaches = (unit, path) => {
    const suite = suiteOf.get(`${unit.suite}|${unit.project}`);
    const found = placed(suite, path);
    if (found.none) return false;
    if (!found.whole) return unitReaches(unit, path, map);
    if (path.startsWith(`${unit.project}/`)) return true;
    const reach = classOf.get(`${unit.suite}|${unit.project}|${unit.unit}`)?.reach ?? [];
    const source = suite.census.sources.find((folder) => path.startsWith(`${folder}/`));
    if (source) return !reach.length || reach.includes('*') || reach.some((area) => area === source || area.startsWith(`${source}/`));
    return buildsProject(suite, path, reach);
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
            || buildsProject(suite, path, null));
        }) : files);
      }
      return candidates.get(group);
    },
    digestOf,
    declaration: (suite) => declarations.get(suite) ?? '',
    toolchain,
  };
}
