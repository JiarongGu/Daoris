/**
 * The census's answers as the key of a reached class needs them (`tools/merge/class-reuse.ts`): which tree files reach a unit, and
 * the digest of each as a result can see it. A path the census cannot place (`whole`) is a class's input only where it builds the
 * class: its test project's own files, a source project's root files where the class names an area of that project, and the build
 * files (`.props`, `.targets`) of a folder above the test project; a root file, a document or a tool outside every project is not.
 */
import { createHash } from 'node:crypto';
import { posix } from 'node:path';
import { propertiesSetTrue, isMsbuildFile, resultText } from './wiring.ts';
import type { KeyContext, Unit } from './class-reuse.ts';
import type { ReachedUnit } from './reached.ts';

/** The build files MSBuild and NuGet apply to every project below them; any other `.props` builds only the project that imports it. */
const AUTO_IMPORTED = /^(Directory\.(Build|Packages)\.(props|targets)|Directory\.Build\.rsp|global\.json|nuget\.config)$/i;
const WIDE_ROOTS = /^(src|tools)\//;

export type DotnetSuite = { suite: string; project: string; census: { classes: { full: string; reach: string[] }[]; sources: string[] } };

export type CensusUnit = ReachedUnit & { reach?: { wide: string[] }; subject?: string };

export type TestMap = { dotnet: DotnetSuite[]; units: CensusUnit[] };

/** The census's readers the key asks, given by the untyped scripts that own them. */
export type Readers = {
  tsSuites: readonly { suite: string; whole?: readonly string[] }[];
  tsReaches: (reach: unknown, path: string) => boolean;
  unitReaches: (unit: ReachedUnit, path: string, map: TestMap) => boolean;
  pathReach: (path: string, census: DotnetSuite['census']) => { none?: boolean; whole?: boolean };
};

/** The key context of `map` over the tree whose files are `files`, read by `read`, under `toolchain` and the gates' declarations. */
export function reuseContext({ map, files, read, toolchain, gates, readers }: {
  map: TestMap; files: readonly string[]; read: (path: string) => string; toolchain: string;
  gates: readonly { name: string; run: string; memoryGb?: number; clock?: boolean }[]; readers: Readers;
}): KeyContext {
  const setTrue = propertiesSetTrue(files.filter(isMsbuildFile).map(read));
  const digests = new Map<string, string>();
  const digestOf = (path: string): string => {
    let digest = digests.get(path);
    if (digest === undefined) {
      digest = createHash('sha1').update(resultText(path, read(path), setTrue)).digest('hex');
      digests.set(path, digest);
    }
    return digest;
  };
  const suiteOf = new Map(map.dotnet.map((each) => [`${each.suite}|${each.project}`, each]));
  const classOf = new Map<string, { full: string; reach: string[] }>();
  for (const each of map.dotnet) for (const cls of each.census.classes) classOf.set(`${each.suite}|${each.project}|${cls.full}`, cls);
  const unitOf = new Map(map.units.map((each) => [`${each.suite}|${each.unit}`, each]));
  const placements = new Map<DotnetSuite, Map<string, { none?: boolean; whole?: boolean }>>();
  const placed = (suite: DotnetSuite, path: string): { none?: boolean; whole?: boolean } => {
    const memo = placements.get(suite) ?? new Map();
    placements.set(suite, memo);
    let found = memo.get(path);
    if (!found) {
      found = readers.pathReach(path, suite.census);
      memo.set(path, found);
    }
    return found;
  };
  const suiteFor = (unit: ReachedUnit): DotnetSuite => suiteOf.get(`${unit.suite}|${unit.project}`) as DotnetSuite;

  // The build files a test project builds with: those applied to everything below them, and each a project of its closure imports.
  // Each file a project of the test project's closure imports, by name, with the folders of the projects that import it ('*' for one
  // applied to every project below it).
  const importers = new Map<string, Map<string, Set<string>>>();
  const importsOf = (suite: DotnetSuite): Map<string, Set<string>> => {
    let imports = importers.get(suite.project);
    if (!imports) {
      const folders = [suite.project, ...suite.census.sources];
      const ancestors = suite.project.split('/').map((_, at, parts) => parts.slice(0, at).join('/'));
      imports = new Map();
      for (const path of files) {
        const folder = posix.dirname(path) === '.' ? '' : posix.dirname(path);
        const applied = AUTO_IMPORTED.test(posix.basename(path)) && ancestors.includes(folder);
        if (!applied && !(path.endsWith('.csproj') && folders.includes(folder))) continue;
        for (const found of read(path).matchAll(/<Import\b[^>]*\bProject\s*=\s*"([^"]+)"/g)) {
          const name = (found[1] ?? '').split(/[\\/]/).at(-1)!.toLowerCase();
          imports.set(name, new Set([...(imports.get(name) ?? []), applied ? '*' : folder]));
        }
      }
      importers.set(suite.project, imports);
    }
    return imports;
  };
  /** Whether a build file builds a class of `suite` whose reach is `reach` (null: any class): one applied to its project's folder, or one a project it names imports. */
  const buildsProject = (suite: DotnetSuite, path: string, reach: string[] | null): boolean => {
    const folder = posix.dirname(path);
    const name = posix.basename(path);
    if (AUTO_IMPORTED.test(name)) return folder === '.' || suite.project.startsWith(`${folder}/`);
    const by = importsOf(suite).get(name.toLowerCase());
    if (!by) return false;
    if (by.has('*') || by.has(suite.project) || reach === null) return true;
    return !reach.length || reach.includes('*') || [...by].some((source) => reach.some((area) => area === source || area.startsWith(`${source}/`)));
  };

  const dotnetReaches = (unit: ReachedUnit, path: string): boolean => {
    const suite = suiteFor(unit);
    const found = placed(suite, path);
    if (found.none) return false;
    if (!found.whole) return readers.unitReaches(unit, path, map);
    if (path.startsWith(`${unit.project}/`)) return true;
    const reach = classOf.get(`${unit.suite}|${unit.project}|${unit.unit}`)?.reach ?? [];
    const source = suite.census.sources.find((folder) => path.startsWith(`${folder}/`));
    if (source) return !reach.length || reach.includes('*') || reach.some((area) => area === source || area.startsWith(`${source}/`));
    return buildsProject(suite, path, reach);
  };
  const wholeOf = (suite: string): readonly string[] => readers.tsSuites.find((each) => each.suite === suite)?.whole ?? [];
  const tsUnitReaches = (unit: CensusUnit, path: string): boolean => {
    if (path === unit.file || wholeOf(unit.suite).includes(path)) return true;
    // A unit whose reach the census cannot bound (it runs the tool whole) is reached by its subject's own files; a change to what
    // it only uses is the CI's and the night's to run it for, since it is the slowest of the tool's tests.
    if (unit.reach?.wide.length) return WIDE_ROOTS.test(path) && (path === unit.subject || path.startsWith(`${String(unit.subject).replace(/\.[^.]+$/, '')}/`));
    return readers.tsReaches(unit.reach, path);
  };
  const fullOf = (unit: Unit): CensusUnit => unitOf.get(`${unit.suite}|${unit.unit}`) ?? (unit as CensusUnit);
  const reaches = (unit: CensusUnit, path: string): boolean => (unit.kind === 'dotnet' ? dotnetReaches(unit, path) : tsUnitReaches(unit, path));

  const candidates = new Map<string, string[]>();
  // The run settings a command names are part of what it is: a change to the filter or the environment they hold is another run.
  const settingsOf = (run: string): string | null => {
    const path = /(?:--settings|-s)[ =:]+"?([^"\s]+)"?/.exec(run)?.[1]?.replaceAll('\\', '/');
    return path && files.includes(path) ? digestOf(path) : null;
  };
  const declarations = new Map(gates.map((gate) => [gate.name, JSON.stringify([gate.run, gate.memoryGb ?? null, gate.clock ?? false, settingsOf(gate.run)])]));
  return {
    reaches: (unit, path) => reaches(fullOf(unit), path),
    candidates: (unit) => {
      const full = fullOf(unit);
      const group = full.kind === 'dotnet' ? `${full.suite}|${full.project}` : full.suite;
      let found = candidates.get(group);
      if (!found) {
        found = full.kind === 'dotnet' ? files.filter((path) => {
          const suite = suiteFor(full);
          const at = placed(suite, path);
          return !at.none && (!at.whole || path.startsWith(`${full.project}/`) || suite.census.sources.some((folder) => path.startsWith(`${folder}/`))
            || buildsProject(suite, path, null));
        }) : [...files];
        candidates.set(group, found);
      }
      return found;
    },
    digestOf,
    declaration: (suite) => declarations.get(suite) ?? '',
    toolchain,
  };
}
