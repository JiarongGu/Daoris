#!/usr/bin/env node
/**
 * The census of every test unit: its reach and its subject, for every suite (design `docs/2026-10-24-ci-scope-design.md` §3),
 * read from the sources so the lane table's hand rows (`REACH` in the merge tool) are a check.
 *
 * ## The units
 *
 * A **unit** is what a runner selects: a .NET class of a declared `dotnet test` gate (each half, each test project of a
 * solution gate), a file of a test set `daoris.gates.json` declares (`tests`, `tools/test-sets.mjs`: a `node --test` file, a vitest
 * file, a Playwright spec), a rehearsal. Each has:
 *
 * - **its reach**. .NET: the Process census unchanged (`censusOf`, the area grain, one level of naming, **not a closure**);
 *   a fast half's filter is read from its command, and a solution gate's test projects each count. TypeScript: a closure over
 *   the repository: every relative import and literal `import(…)` followed wherever it leads, every string that names a
 *   tracked file or folder (from the root or the file), every basename string with an extension that names tracked files,
 *   `import.meta.glob`'s patterns, and `// reaches: <path, folder, glob or *> — <why>` lines for an edge no string names (a census's
 *   listing, `declaresRead`); a computed `import(…)` reaches its whole suite. A rehearsal: its tool's import closure.
 * - **its subject**: the module its own file sits in (§3.1): a test project's first folder, or the project at its root; the
 *   CLI's `test/<module>` is `src/<module>`'s; the page's tests sit beside their sources; a tool-test names its tool. A git-half
 *   class at its project's root carries `// subject: <module> — <why>`.
 *
 * ## The checks (`check`)
 *
 * 1. **Inventory**: with `--reports <dir>`, every class a trx there ran is a unit of the census of the tree it ran on
 *    (`--commit`), and every unit of a half the trx covers ran, so a class the census lacks or a runner that lost one is said.
 * 2. **Placement**: every tracked file is in a module, a lane, or the laneless list.
 * 3. **Hand rows**: each fixture row of `REACH` (a twin's table) names exactly the gates whose units the census finds reading it.
 * 4. **Subjects**: a git-half class at its project's root declares its subject.
 * 5. **Whole-suite files**: every file that acts on a suite's units unnamed is on `tools/test-map.json`'s list, which names no gone file.
 * 6. **Expected edges** are the tool-tests' fixtures, one per edge kind.
 * 7. **Skips**: a statically skipped test's reason begins with a row open in `TASKS.md`.
 * 8. **Check steps**: each step of the `cli` gate declares every path its command's code reads (`tools/verify-checks/reads.mjs`).
 *
 *   node tools/test-map.mjs census [<suite>…]            every unit with its subject and the size of its reach
 *   node tools/test-map.mjs reach <from> <to> [<suite>…]  the units the paths changed between two commits reach
 *   node tools/test-map.mjs check [--reports <dir> --commit <sha>]     (`--check` alike: the gate)
 *
 * Exit codes: 0 answered or clean · 1 a check failed · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, posix } from 'node:path';
import { isMain } from './fsx.mjs';
import { globToRegExp } from './merge/lanes-file.ts';
import { censusOf, halfOf, pathReach } from './process-reach.mjs';
import { classHitByAreas } from './process-reach/census.mjs';
import { tsFacts, resolveImport } from './test-map/ts-facts.mjs';
import { declaredSets, filesBySet, scopeOf, testSets } from './test-sets.mjs';
import { webReach } from './test-map/web-reach.mjs';
import { stepReadFailures } from './verify-checks/reads.mjs';
import { CHECK_STEPS } from './verify-checks/steps.mjs';
export { literalsOf, tsFacts } from './test-map/ts-facts.mjs';

// ---------------------------------------------------------------------------------------------------
// The tree

/** What the census reads of a commit's tree: sources, projects, settings, configs and tests. */
const READS = (path) => /^(src|tools|examples)\//.test(path) && /\.(cs|csproj|props|targets|runsettings|ts|tsx|mts|mjs|js|json)$/.test(path)
  && !/\/(node_modules|bin|obj|dist|wwwroot)\//.test(path) && !/package-lock\.json$/.test(path)
  || ['daoris.gates.json', 'daoris.lanes.json', 'package.json', 'TASKS.md'].includes(path);

/**
 * The repository's tracked files at `commit` (the working tree's when null) and a reader of each, as the Process census's
 * `repository` reads them, over every suite's sources: a commit's files by one `git cat-file --batch`.
 */
export function treeOf(root, commit = null) {
  const listed = spawnSync('git', commit ? ['ls-tree', '-r', '-z', commit] : ['ls-files', '-z'], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  if (listed.status !== 0) throw new Error(`git could not list ${commit ?? 'the tree'}: ${String(listed.stderr).trim()}`);
  if (!commit) {
    const files = listed.stdout.split('\0').filter(Boolean);
    const cache = new Map();
    const read = (file) => {
      if (!cache.has(file)) cache.set(file, existsSync(join(root, file)) ? readFileSync(join(root, file), 'utf8') : '');
      return cache.get(file);
    };
    return { files, read };
  }
  const blobs = new Map();
  for (const entry of listed.stdout.split('\0').filter(Boolean)) {
    const parts = /^\d+ (\w+) ([0-9a-f]+)\t(.*)$/s.exec(entry);
    if (parts && parts[1] === 'blob') blobs.set(parts[3], parts[2]);
  }
  const wanted = [...blobs].filter(([path]) => READS(path));
  const batch = spawnSync('git', ['cat-file', '--batch'], { cwd: root, windowsHide: true, input: `${wanted.map(([, sha]) => sha).join('\n')}\n`, maxBuffer: 2 * 1024 * 1024 * 1024 });
  if (batch.status !== 0) throw new Error(`git could not read ${commit}'s files: ${String(batch.stderr).trim()}`);
  const texts = new Map();
  let at = 0;
  for (const [path] of wanted) {
    const eol = batch.stdout.indexOf(10, at);
    const size = Number(/ (\d+)$/.exec(batch.stdout.subarray(at, eol).toString('utf8'))?.[1] ?? 0);
    texts.set(path, batch.stdout.subarray(eol + 1, eol + 1 + size).toString('utf8'));
    at = eol + 1 + size + 1;
  }
  return { files: [...blobs.keys()], read: (file) => texts.get(file) ?? '' };
}

// ---------------------------------------------------------------------------------------------------
// The suites

export function globRegExp(glob) {
  if (!GLOBS.has(glob)) GLOBS.set(glob, globToRegExp(glob));
  return GLOBS.get(glob);
}
const GLOBS = new Map();

/**
 * The test sets this checkout declares (`daoris.gates.json`'s `tests`): each its suite, its runner, the patterns of its files and
 * the files that act on every unit unnamed (`whole`). A tree's census reads the tree's own declaration, this one where the tree
 * declares none (a fixture's tree). A rehearsal is a unit of its own, its tool.
 */
export const TS_SUITES = declaredSets();

/** The sets whose files are units: a tree's declared ones (this checkout's where it declares none), less the picks of another set. */
const unitSets = (declaration) => (declaration.tests ? testSets(declaration) : TS_SUITES).filter((set) => !set.of);

/** The rehearsals, each a unit: its gate and its tool. */
export const REHEARSALS = Object.freeze([
  { suite: 'rehearse', file: 'tools/release-rehearsal.mjs' },
  { suite: 'rehearse-family', file: 'tools/family-rehearsal.mjs' },
  { suite: 'deployment', file: 'tools/deployment-rehearsal.mjs' },
]);

/**
 * The .NET suites of the declared gates: each `dotnet test` gate's test projects, with the filter its command or its run
 * settings give. A solution's folder holds several test projects, each a half of the gate.
 */
export function dotnetSuites(gates, files) {
  const suites = [];
  for (const gate of gates) {
    const words = gate.run.trim().match(/"[^"]*"|'[^']*'|\S+/g)?.map((word) => word.replace(/^["']|["']$/g, '')) ?? [];
    if (words[0] !== 'dotnet' || words[1] !== 'test' || !words[2]) continue;
    const target = words[2].split('\\').join('/').replace(/\/$/, '').replace(/\/[^/]+\.csproj$/i, '');
    const process = halfOf(gate.run);
    const filterAt = words.indexOf('--filter');
    const filter = filterAt >= 0 ? words[filterAt + 1] : null;
    const projects = files.filter((file) => file.startsWith(`${target}/`) && /\.Tests\.csproj$/.test(file)).map((file) => posix.dirname(file));
    for (const project of [...new Set(projects)]) {
      suites.push({ suite: gate.name, project, settings: process?.settings ?? null, filter: process ? null : filter, process: Boolean(process) });
    }
  }
  return suites;
}

// ---------------------------------------------------------------------------------------------------
// Reading TypeScript

const CODE = /\.(ts|tsx|mts|mjs|js)$/;

/**
 * The reach of TypeScript units over a tree: for each file, the closure of what it imports, and every file, folder and glob
 * the closure's strings name. Memoised per file, so a suite of four hundred units reads each source once.
 */
export function tsReader(files, read) {
  const tracked = new Set(files);
  const folders = new Set();
  const byName = new Map();
  for (const file of files) {
    const parts = file.split('/');
    for (let i = 1; i < parts.length; i += 1) folders.add(parts.slice(0, i).join('/'));
    const name = parts.at(-1);
    if (!byName.has(name)) byName.set(name, []);
    byName.get(name).push(file);
  }
  const page = webReach(files, read, (from, spec) => resolveImport(from, spec, tracked));
  const facts = new Map();
  const factsOf = (file) => {
    if (!facts.has(file)) facts.set(file, tsFacts(read(file)));
    return facts.get(file);
  };
  /** What one file's own strings, globs and declarations reach, and its resolved imports. */
  const own = new Map();
  const ownOf = (file) => {
    if (own.has(file)) return own.get(file);
    const fact = factsOf(file);
    const reach = { files: new Set(), folders: new Set(), globs: [], wide: [], imports: [], declared: [] };
    for (const edge of fact.edges) {
      const resolved = edge.typeOnly || edge.reexport || edge.names?.length === 0 ? null : resolveImport(file, edge.spec, tracked);
      if (!resolved) continue;
      const through = page.through(file, resolved, edge.names);
      reach.imports.push(...through.imports);
      through.files.forEach((each) => reach.files.add(each));
      reach.globs.push(...through.globs);
    }
    if (fact.computed) reach.wide.push(`${file} imports a module it computes`);
    // A file that names paths as data (the merge tool's lane table) declares it: its strings are a table, not reads.
    const data = /^\s*\/\/\s*test-map:\s*strings name paths as data\b/m.test(read(file));
    if (!data) page.catalogues(file, fact.strings).forEach((each) => reach.files.add(each));
    for (const value of data ? [] : fact.strings) {
      if (page.dotIsNoFolder(file, value)) continue;
      const clean = value.replace(/^\.\//, '').replace(/\/+$/, '');
      const candidates = value.startsWith('.') ? [posix.normalize(posix.join(posix.dirname(file), value)).replace(/\/+$/, '')] : [clean];
      for (const candidate of candidates) {
        if (tracked.has(candidate)) reach.files.add(candidate);
        else if (candidate.includes('/') && folders.has(candidate)) reach.folders.add(candidate);
      }
      if (/^[\w][\w.-]*\.[A-Za-z]{1,6}$/.test(value) && byName.has(value) && byName.get(value).length <= 50) {
        for (const each of byName.get(value)) reach.files.add(each);
      }
    }
    for (const pattern of fact.globs) {
      const absolute = pattern.startsWith('/') ? pattern.slice(1) : posix.normalize(posix.join(posix.dirname(file), pattern));
      if (!page.keyedGlob(file, absolute)) reach.globs.push(absolute);
    }
    for (const { target, why } of fact.reaches) {
      if (target !== '*') reach.declared.push(target.replace(/\/+$/, ''));
      if (target === '*') reach.wide.push(`${file} declares it reaches its whole suite: ${why}`);
      else if (tracked.has(target)) reach.files.add(target);
      else if (folders.has(target.replace(/\/+$/, ''))) reach.folders.add(target.replace(/\/+$/, ''));
      else reach.globs.push(target);
    }
    own.set(file, reach);
    return reach;
  };
  /** A unit's reach: the closure of its imports, each file's own reach joined. */
  const reachOf = (unit) => {
    const queue = page.through(unit, unit, null).imports;
    const closure = new Set(queue);
    const reach = { files: new Set(queue), folders: new Set(), globs: [], wide: [], declared: [] };
    while (queue.length) {
      const file = queue.pop();
      if (!CODE.test(file)) continue;
      const mine = ownOf(file);
      for (const each of mine.imports) if (!closure.has(each)) {
        closure.add(each);
        reach.files.add(each);
        queue.push(each);
      }
      for (const each of mine.files) reach.files.add(each);
      for (const each of mine.folders) reach.folders.add(each);
      reach.globs.push(...mine.globs);
      reach.wide.push(...mine.wide);
      reach.declared.push(...mine.declared);
    }
    const declared = [...new Set(reach.declared)].sort();
    return { files: [...reach.files].sort(), folders: [...reach.folders].sort(), globs: [...new Set(reach.globs)], wide: reach.wide, declared };
  };
  /** The files within `depth` reads of `unit`, each at its fewest: itself 0, what it imports or names 1… (no folder or glob). */
  const hopsFrom = (unit, depth) => {
    const found = new Map([[unit, 0]]);
    let frontier = [unit];
    for (let hops = 1; hops <= depth; hops += 1) {
      const next = [];
      for (const file of frontier) {
        if (!CODE.test(file)) continue;
        const mine = ownOf(file);
        for (const each of [...mine.imports, ...mine.files]) if (!found.has(each)) {
          found.set(each, hops);
          next.push(each);
        }
      }
      frontier = next;
    }
    return found;
  };
  return { reachOf, factsOf, hopsFrom };
}

/** Whether a TypeScript unit's reach holds `path`. */
export function tsReaches(reach, path) {
  if (reach.within && !declaredHolds(reach.within, path)) return false;
  if (reach.wide.length) return true;
  if (reach.files.includes(path)) return true;
  if (reach.folders.some((folder) => path.startsWith(`${folder}/`))) return true;
  return reach.globs.some((glob) => globRegExp(glob).test(path));
}

/** Whether a unit's `// reaches:` lines declare `path`, one read from it (`unitHops`), and its reach even where that is wide. */
export const declaresRead = (reach, path) => (!reach?.within || declaredHolds(reach.within, path)) && (reach?.declared ?? [])
  .some((target) => target === path || path.startsWith(`${target}/`) || (/[*?{[]/.test(target) && globRegExp(target).test(path)));

/** Whether a gate's declared `paths` hold `path`: an entry ending in `/` is a folder, any other a file, as the declaration writes them. */
const declaredHolds = (paths, path) => paths.some((entry) => (entry.endsWith('/') ? path.startsWith(entry) : path === entry));

// ---------------------------------------------------------------------------------------------------
// Subjects

/** The module a TypeScript unit's own file sits in. */
export function tsSubject(file) {
  const cli = /^src\/Daoris\.Cli\/test\/([^/]+)\//.exec(file);
  if (cli) return `src/Daoris.Cli/src/${cli[1]}`;
  if (file.startsWith('src/Daoris.Cli/test/')) return 'src/Daoris.Cli';
  if (/^src\/Daoris\.Cli\/tool-test\/merge-branch-[^/]+\.(test|e2e)\.ts$/.test(file)) return 'tools/merge-branch.mjs';
  if (/^src\/Daoris\.Cli\/tool-test\/merge-queue-[^/]+\.e2e\.ts$/.test(file)) return 'tools/merge-queue.mjs';
  const merge = /^(tools\/merge\/.+)\.test\.ts$/.exec(file);
  if (merge) return `${merge[1]}.ts`;
  if (/^tools\/merge\/.+\.e2e\.ts$/.test(file)) return 'tools/merge-branch.mjs';
  const tool = /^src\/Daoris\.Cli\/tool-test\/(.+)\.(?:test|e2e)\.ts$/.exec(file);
  if (tool) return `tools/${tool[1]}.mjs`;
  const web = /^src\/Daoris\.Web\/src\/([^/]+)\//.exec(file);
  if (web) return `src/Daoris.Web/src/${web[1]}`;
  if (file.startsWith('src/Daoris.Web/')) return 'src/Daoris.Web';
  const ownTool = /^(tools\/.+)\.test\.mjs$/.exec(file);
  return ownTool ? `${ownTool[1]}.mjs` : file;
}

/** A .NET class's own file: the one that declares it (its first, where several do). */
function ownFile(each, read) {
  const name = each.name.split('+').at(-1);
  const declares = new RegExp(`\\b(?:class|record|struct)\\s+${name}\\b`);
  return each.files.find((file) => declares.test(read(file))) ?? each.files[0];
}

export const needsOf = (text) => [...new Set([...text.matchAll(/Trait\(\s*Category\.Needs\s*,\s*Needed\.(\w+)\s*\)/g)].map((found) => found[1].toLowerCase()))];

/** A .NET class's subject: its project's first folder its own file sits in, or the project at its root; and a declared one. */
export function dotnetSubject(project, file, text = '') {
  const declared = /^\s*\/\/\s*subject:\s*(\S+)/m.exec(text);
  const rest = file.slice(project.length + 1).split('/');
  return { subject: rest.length > 1 ? `${project}/${rest[0]}` : project, atRoot: rest.length === 1, declared: declared?.[1] ?? null };
}

// ---------------------------------------------------------------------------------------------------
// The census

const VIRTUAL_SETTINGS = '<virtual>.runsettings';

/** The census of every suite over a tree (`files`, `read`): each suite's units with their reach and subject. */
export function census(files, read, { suites = null } = {}) {
  const declaration = JSON.parse(read('daoris.gates.json') || '{"gates":[]}');
  const gates = declaration.gates ?? [];
  const wanted = (suite) => !suites || suites.includes(suite);
  const units = [];
  const problems = [];
  const dotnet = [];
  for (const suite of dotnetSuites(gates, files).filter((each) => wanted(each.suite))) {
    // A fast half's filter is its command's; a solution gate with none holds every test (a condition on a name always holds).
    const settings = suite.settings ?? VIRTUAL_SETTINGS;
    const xml = `<RunSettings><RunConfiguration><TestCaseFilter>${(suite.filter ?? 'FullyQualifiedName!=none').replace(/&/g, '&amp;')}</TestCaseFilter></RunConfiguration></RunSettings>`;
    const seen = suite.settings ? files : [...files, VIRTUAL_SETTINGS];
    const reader = (file) => (file === VIRTUAL_SETTINGS ? xml : read(file));
    const half = { project: suite.project, settings };
    const result = censusOf(half, seen, reader);
    for (const problem of result.problems) problems.push(`${suite.suite} (${suite.project}): ${problem}`);
    dotnet.push({ ...suite, census: result });
    for (const each of result.classes) {
      const file = ownFile(each, read);
      const subject = dotnetSubject(suite.project, file, read(file));
      units.push({
        suite: suite.suite, kind: 'dotnet', unit: each.full, name: each.name, file, project: suite.project, process: suite.process,
        subject: subject.declared ?? subject.subject, atRoot: subject.atRoot, declaredSubject: subject.declared,
        reach: { areas: each.reach, reaches: each.reaches ?? [] }, strings: each.strings ?? '',
        settings: suite.settings, needs: needsOf(read(file)),
      });
    }
  }
  const ts = tsReader(files, read);
  const sets = unitSets(declaration);
  const held = filesBySet(sets, files);
  for (const set of sets.filter((each) => wanted(each.suite))) {
    // A bounded set's units are reached only within its gate's declared paths; a gate that declares none bounds nothing.
    const within = scopeOf(set, gates);
    const setup = set.setup && files.includes(set.setup) ? ts.reachOf(set.setup) : null;
    for (const file of held.get(set.name)) {
      const own = ts.reachOf(file);
      const reach = setup ? { files: [...new Set([...own.files, ...setup.files])].sort(), folders: [...new Set([...own.folders, ...setup.folders])].sort(), globs: [...new Set([...own.globs, ...setup.globs])], wide: [...own.wide, ...setup.wide] } : own;
      const where = set.runner === 'node' ? { cwd: set.cwd } : {};
      units.push({ suite: set.suite, kind: set.runner, unit: file, name: posix.basename(file), file, ...where, subject: tsSubject(file), reach: within ? { ...reach, within } : reach });
    }
  }
  for (const rehearsal of REHEARSALS.filter((each) => wanted(each.suite))) {
    if (!files.includes(rehearsal.file)) continue;
    units.push({ suite: rehearsal.suite, kind: 'rehearsal', unit: rehearsal.file, name: posix.basename(rehearsal.file), file: rehearsal.file, subject: rehearsal.file, reach: ts.reachOf(rehearsal.file) });
  }
  return { units, dotnet, problems, ts };
}

/** Whether a unit of the census reaches `path`, with why: a TypeScript unit by its closure, a .NET class by the Process census's `pathReach`. */
export function unitReaches(unit, path, map) {
  if (unit.kind === 'dotnet') {
    const suite = map.dotnet.find((each) => each.suite === unit.suite && each.project === unit.project);
    return suite ? classReaches(suite.census, unit.unit, path) : false;
  }
  return tsReaches(unit.reach, path);
}

const classCache = new WeakMap();
function classReaches(result, full, path) {
  let byPath = classCache.get(result);
  if (!byPath) classCache.set(result, (byPath = new Map()));
  if (!byPath.has(path)) byPath.set(path, reachedBy(result, path));
  const reached = byPath.get(path);
  return reached === '*' || reached.has(full);
}

/** The classes of a half one path reaches: every class for a whole-half path, as `classesReached` reads it. */
function reachedBy(result, path) {
  const placed = pathReach(path, result);
  if (placed.whole) return '*';
  if (placed.none) return new Set();
  if (placed.classes) return new Set(placed.classes);
  return new Set(result.classes.filter((each) => classHitByAreas(each, placed, result)).map((each) => each.full));
}

/** The fewest reads between `unit` and `paths` within `depth`: a TypeScript unit's imports, a .NET class 1 from a file declaring a type it names. */
export function unitHops(unit, paths, map, depth = 2) {
  if (unit.kind === 'dotnet') {
    const census = map.dotnet.find((each) => each.suite === unit.suite && each.project === unit.project)?.census;
    const sourceFiles = census?.classes.find((each) => each.full === unit.unit)?.sourceFiles ?? [];
    const counts = paths.map((path) => (path === unit.file ? 0 : sourceFiles.includes(path) ? 1 : undefined)).filter((each) => each !== undefined);
    return counts.length ? Math.min(...counts) : undefined;
  }
  const within = map.ts.hopsFrom(unit.file, depth);
  const counts = paths.map((path) => within.get(path) ?? (declaresRead(unit.reach, path) ? 1 : undefined)).filter((each) => each !== undefined);
  return counts.length ? Math.min(...counts) : undefined;
}

/** The units of the census `paths` reach, each with the first path that reaches it. */
export function unitsReached(paths, map, { suites = null } = {}) {
  const out = [];
  for (const unit of map.units) {
    if (suites && !suites.includes(unit.suite)) continue;
    const by = paths.find((path) => unitReaches(unit, path, map));
    if (by) out.push({ ...unit, by });
  }
  return out;
}

// ---------------------------------------------------------------------------------------------------
// The checks

/** The files that act on a suite's units unnamed, as this tree holds them, each with why. */
export function wholeSuiteFiles(files, map) {
  const found = new Map();
  for (const suite of map.dotnet) {
    for (const [path, file] of Object.entries(suite.census.files ?? {})) if (file.unnamed) found.set(path, 'acts on every test unnamed (a global using, an assembly attribute, a module initializer)');
    for (const file of files) {
      if (file.startsWith(`${suite.project}/`) && /\.csproj$/.test(file)) found.set(file, "a test project's own project file");
    }
    if (suite.settings && files.includes(suite.settings)) found.set(suite.settings, "a half's run settings");
  }
  for (const file of files) if (/^src\/[^/]+\/Directory\.(Build|Packages)\.(props|targets)$/.test(file)) found.set(file, 'every project of its folder builds with it');
  for (const suite of TS_SUITES) for (const file of suite.whole) if (files.includes(file)) found.set(file, `configures every unit of ${suite.suite}`);
  return found;
}

/** The modules of §3.1 a path sits in, or null. */
export function moduleOf(path, sourceProjects) {
  const cli = /^src\/Daoris\.Cli\/src\/([^/]+)\//.exec(path);
  if (cli) return `src/Daoris.Cli/src/${cli[1]}`;
  if (path.startsWith('src/Daoris.Cli/src/')) return 'src/Daoris.Cli/src';
  const web = /^src\/Daoris\.Web\/src\/([^/]+)\//.exec(path);
  if (web) return `src/Daoris.Web/src/${web[1]}`;
  if (/^tools\/[^/]+$/.test(path)) return path;
  for (const project of sourceProjects) {
    if (!path.startsWith(`${project}/`)) continue;
    const rest = path.slice(project.length + 1).split('/');
    return rest.length > 1 ? `${project}/${rest[0]}` : project;
  }
  return null;
}

/** The fixture rows of a lane table: each rule whose paths are all a twin's table, a fixture some suite's units read. */
export const fixtureRows = (reach) => reach.filter((rule) => rule.paths && rule.paths.every((path) => /\/fixtures\/[^*]+$|^tools\/orient-index-fixtures\//.test(path)));

/** The gates a unit's suite is: a .NET class's half's gate, a TypeScript unit's suite. */
const gateOfUnit = (unit) => unit.suite;

/**
 * The census's checks over a tree. `lanes`, `reach` and `baseline` are the merge tool's; `tasks` the backlog's text; `listed`
 * `tools/test-map.json`'s whole-suite list; `reports`, a trx's classes by gate, for the inventory.
 */
export function checks(files, read, map, { lanes = [], laneless = [], union = [], laneMatcher, reach = [], baseline = [], tasks = '', listed = {}, reports = null }) {
  const failures = [];
  // 1. Inventory.
  if (reports) {
    for (const [gate, { ran, kept = new Set() }] of Object.entries(reports)) {
      const units = new Set(map.units.filter((unit) => unit.suite === gate).map((unit) => unit.unit));
      const missing = [...ran].filter((name) => !units.has(name));
      const unrun = [...units].filter((name) => !ran.has(name) && !kept.has(name));
      if (missing.length) failures.push(`inventory: ${gate} ran ${missing.length} class(es) the census lacks: ${missing.slice(0, 5).join(', ')}`);
      if (unrun.length) failures.push(`inventory: the census holds ${unrun.length} class(es) of ${gate} its report did not run: ${unrun.slice(0, 5).join(', ')}`);
    }
  }
  // 2. Placement.
  const sourceProjects = [...new Set(files.filter((file) => /\.csproj$/.test(file) && !/\.Tests\.csproj$/.test(file)).map((file) => posix.dirname(file)))];
  if (laneMatcher) {
    const owned = lanes.map((lane) => laneMatcher(lane.paths));
    const free = laneMatcher([...laneless, ...union]);
    const unplaced = files.filter((file) => !moduleOf(file, sourceProjects) && !owned.some((match) => match(file)) && !free(file));
    if (unplaced.length) failures.push(`placement: ${unplaced.length} tracked file(s) in no module, no lane and not laneless: ${unplaced.slice(0, 5).join(', ')}`);
  }
  // 3. Hand rows.
  for (const row of fixtureRows(reach)) {
    const paths = row.paths.flatMap((path) => (path.includes('*') ? files.filter((file) => globRegExp(path).test(file)) : [path]));
    const derived = new Set();
    for (const path of paths) {
      const name = posix.basename(path);
      const stem = name.replace(/\.[^.]+$/, '');
      for (const unit of map.units) {
        const reads = unit.kind === 'dotnet'
          ? new RegExp(`(?:^|[^A-Za-z0-9_.-])(?:${name.replace(/[.]/g, '\\.')}|${stem.replace(/[.]/g, '\\.')})(?:$|[^A-Za-z0-9_-])`).test(unit.strings)
          // A bounded unit (the end-to-end tier's) reads a fixture for the lane table only where its gate's declared paths hold it.
          : (!unit.reach.within || declaredHolds(unit.reach.within, path))
            && (unit.reach.files.includes(path) || unit.reach.folders.includes(posix.dirname(path)) || unit.reach.globs.some((glob) => globRegExp(glob).test(path)));
        if (reads) derived.add(gateOfUnit(unit));
      }
    }
    const rowGates = new Set((row.gates === '*' ? [] : row.gates).filter((gate) => !baseline.includes(gate)));
    for (const gate of baseline) derived.delete(gate);
    const omitted = [...derived].filter((gate) => !rowGates.has(gate));
    const unread = [...rowGates].filter((gate) => !derived.has(gate));
    if (omitted.length || unread.length) {
      failures.push(`hand row ${row.paths[0]}: ${omitted.length ? `the census finds readers in ${omitted.join(', ')}, which the row omits` : ''}${omitted.length && unread.length ? '; ' : ''}${unread.length ? `the row names ${unread.join(', ')}, where the census finds no reader` : ''}`);
    }
  }
  // 4. Subjects.
  for (const unit of map.units.filter((each) => each.suite === 'driver-process-git' && each.atRoot && !each.declaredSubject)) {
    failures.push(`subject: ${unit.name} sits at its project's root and declares no subject (// subject: <module> — <why>)`);
  }
  // 5. Whole-suite files.
  const found = wholeSuiteFiles(files, map);
  for (const [path, why] of found) if (!(path in listed)) failures.push(`whole-suite: ${path} ${why}, and tools/test-map.json does not list it`);
  for (const path of Object.keys(listed)) if (!files.includes(path)) failures.push(`whole-suite: tools/test-map.json lists ${path}, which this tree does not hold`);
  // 7. Skips.
  const open = new Set([...String(tasks).matchAll(/^\s*- \[ \] \*\*([A-Z][A-Z0-9-]*\d[\w-]*)\b/gm)].map((match) => match[1]));
  for (const unit of map.units) {
    const text = read(unit.file);
    const reasons = unit.kind === 'dotnet'
      ? [...text.matchAll(/\bSkip\s*=\s*"([^"]*)"/g)].filter((match) => /\[\s*(?:Fact|Theory)[^\]]*$/.test(text.slice(Math.max(0, match.index - 200), match.index))).map((match) => match[1])
      : [...text.matchAll(/\b(?:test|it|describe)\.skip\(\s*['"`]([^'"`]*)/g)].map((match) => match[1]);
    for (const reason of reasons) {
      const row = /^([A-Z][A-Z0-9-]*\d[\w-]*):/.exec(reason)?.[1];
      if (!row || !open.has(row)) failures.push(`skip: ${unit.file} skips a test for "${reason.slice(0, 60)}", which names no row open in TASKS.md`);
    }
  }
  return failures;
}

// ---------------------------------------------------------------------------------------------------
// Recall's structural judgement

/**
 * Whether a commit's own change, its diff against its first parent, reaches each named unit (a class's simple or full name, a
 * test file's path or name) by the census of that commit's tree, pinned at this tool: *selected* where it does (a .NET fast
 * half by any path the lane table sends its gate, since §4.2 runs a reached fast half whole), *missed* where not, *unscoped*
 * for a direct commit, and the unit not found said apart.
 */
export async function judgeStructurally(root, commit, units) {
  const { readLanes, pathsReaching } = await import('./merge-branch.mjs');
  const parents = spawnSync('git', ['rev-list', '--parents', '-n', '1', commit.sha], { cwd: root, windowsHide: true, encoding: 'utf8' }).stdout.trim().split(' ').slice(1);
  const diff = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', `${commit.sha}^1`, commit.sha], { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const paths = diff.stdout.split('\0').filter(Boolean);
  const tree = treeOf(root, commit.sha);
  const map = census(tree.files, tree.read);
  const lanes = readLanes(root)?.lanes ?? [];
  const reached = [];
  for (const name of units) {
    const matches = map.units.filter((unit) => unit.name === name || unit.unit === name || unit.unit.endsWith(`.${name}`) || unit.file.endsWith(`/${name}`));
    if (!matches.length) {
      reached.push({ unit: name, found: false });
      continue;
    }
    const hit = matches.some((unit) => {
      if (unit.kind === 'dotnet' && !unit.process) return pathsReaching(unit.suite, paths, { lanes }).length > 0;
      const via = unit.kind === 'dotnet' ? pathsReaching(unit.suite, paths, { lanes }) : paths;
      return via.some((path) => unitReaches(unit, path, map));
    });
    reached.push({ unit: name, found: true, reached: hit });
  }
  const found = reached.filter((each) => each.found);
  const verdict = !found.length ? 'unfound' : parents.length < 2 ? 'unscoped' : found.every((each) => each.reached) ? 'selected' : 'missed';
  return { verdict, reached, paths: paths.length };
}

// ---------------------------------------------------------------------------------------------------
// The tool

/** A trx's classes, by their full names. */
function trxClasses(trx) {
  const classes = new Set();
  for (const [, className] of trx.matchAll(/<TestMethod\b[^>]*\bclassName="([^"]+)"/g)) classes.add(className.replace(/&amp;/g, '&'));
  return classes;
}

/**
 * A trx's classes with a test that did not execute (a skip), by full name. A proof that needs a machine's own tool
 * (an sshd) skips where the machine lacks it and reports green; the check names each such class so the skip is seen.
 */
export function trxSkipped(trx) {
  const classOf = new Map();
  for (const [, id, body] of trx.matchAll(/<UnitTest\b[^>]*\bid="([^"]+)"[^>]*>([\s\S]*?)<\/UnitTest>/g)) {
    const name = /<TestMethod\b[^>]*\bclassName="([^"]+)"/.exec(body)?.[1];
    if (name) classOf.set(id, name.replace(/&amp;/g, '&'));
  }
  const skipped = new Set();
  for (const [tag] of trx.matchAll(/<UnitTestResult\b[^>]*>/g)) {
    if (!/\boutcome="NotExecuted"/.test(tag)) continue;
    const id = /\btestId="([^"]+)"/.exec(tag)?.[1];
    if (id && classOf.has(id)) skipped.add(classOf.get(id));
  }
  return [...skipped].sort();
}

const withCensus = (tree) => ({ ...tree, map: census(tree.files, tree.read) });

async function main(argv) {
  const [verb, ...rest] = argv;
  const root = process.cwd();
  const option = (name) => (rest.includes(name) ? rest[rest.indexOf(name) + 1] : null);
  if (verb === 'census') {
    const { files, read } = treeOf(root, option('--commit'));
    const suites = rest.filter((word) => !word.startsWith('--') && word !== option('--commit'));
    const map = census(files, read, { suites: suites.length ? suites : null });
    const bySuite = new Map();
    for (const unit of map.units) bySuite.set(unit.suite, [...(bySuite.get(unit.suite) ?? []), unit]);
    for (const [suite, units] of bySuite) {
      console.log(`${suite}: ${units.length} units`);
      for (const unit of units) {
        const size = unit.kind === 'dotnet' ? `${unit.reach.areas.length ? unit.reach.areas.map((area) => posix.basename(area)).join(', ') : '(no reach: every change)'}` : `${unit.reach.files.length} files, ${unit.reach.folders.length} folders, ${unit.reach.globs.length} globs${unit.reach.wide.length ? `, its whole suite: ${unit.reach.wide[0]}` : ''}`;
        console.log(`  ${unit.kind === 'dotnet' ? unit.name : unit.file} [${unit.subject}]: ${size}`);
      }
    }
    for (const problem of map.problems) console.log(`problem: ${problem}`);
    return 0;
  }
  if (verb === 'reach') {
    const [from, to, ...suites] = rest;
    if (!from || !to) throw new Error('usage: node tools/test-map.mjs reach <from> <to> [<suite>…]');
    const diff = spawnSync('git', ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', from, to], { cwd: root, windowsHide: true, encoding: 'utf8' });
    if (diff.status !== 0) throw new Error(`git could not compare ${from} and ${to}: ${String(diff.stderr).trim()}`);
    const paths = diff.stdout.split('\0').filter(Boolean);
    const { files, read } = treeOf(root, to);
    const map = census(files, read, { suites: suites.length ? suites : null });
    const reached = unitsReached(paths, map);
    const bySuite = new Map();
    for (const unit of reached) bySuite.set(unit.suite, [...(bySuite.get(unit.suite) ?? []), unit]);
    for (const suite of new Set(map.units.map((unit) => unit.suite))) {
      const units = bySuite.get(suite) ?? [];
      const total = map.units.filter((unit) => unit.suite === suite).length;
      console.log(`${suite}: ${units.length} of ${total} units`);
      for (const unit of units.slice(0, 40)) console.log(`  ${unit.kind === 'dotnet' ? unit.name : unit.file} (${unit.by})`);
      if (units.length > 40) console.log(`  … and ${units.length - 40} more`);
    }
    return 0;
  }
  // `--check` is the gate's spelling, as the merge tool reads a check's (`gateKind`).
  if (verb === 'check' || verb === '--check') {
    const commit = option('--commit');
    const { files, read, map } = commit ? withCensus(treeOf(root, commit)) : (await import('./merge-branch/full-set.mjs')).workingCensus(root);
    const { readLanes, laneMatcher, unionRecords, REACH, BASELINE } = await import('./merge-branch.mjs');
    const lanesRead = commit ? null : readLanes(root);
    const listedFile = join(root, 'tools/test-map.json');
    const listed = existsSync(listedFile) ? JSON.parse(readFileSync(listedFile, 'utf8')).wholeSuite ?? {} : {};
    let reports = null;
    const dir = option('--reports');
    if (dir) {
      reports = {};
      for (const file of readdirSync(dir).filter((each) => each.endsWith('.trx'))) {
        const gate = file.replace(/\.trx$/, '');
        // A half run by reach keeps classes out by its own run settings (`reachSettings`): those do not run, by design.
        const settings = join(dir, `${gate}.process.runsettings`);
        const kept = new Set(existsSync(settings) ? [...readFileSync(settings, 'utf8').matchAll(/FullyQualifiedName!~([^&<)]+?)\\?\.(?=&|<|\))/g)].map((match) => match[1].replace(/\\(.)/g, '$1')) : []);
        const trx = readFileSync(join(dir, file), 'utf8');
        reports[gate] = { ran: trxClasses(trx), kept };
        const skipped = trxSkipped(trx);
        if (skipped.length) console.log(`test-map: ${gate} skipped on this machine, so these proofs did not run: ${skipped.join(', ')}`);
      }
    }
    const failures = checks(files, read, map, {
      lanes: lanesRead?.lanes ?? [], laneless: lanesRead?.laneless ?? [], union: lanesRead ? unionRecords(root) : [], laneMatcher: lanesRead ? laneMatcher : null,
      reach: REACH, baseline: BASELINE, tasks: read('TASKS.md'), listed, reports,
    });
    for (const problem of map.problems) failures.push(`census: ${problem}`);
    failures.push(...stepReadFailures(CHECK_STEPS, { files, read, reader: tsReader(files, read) }));
    const suites = new Set(map.units.map((unit) => unit.suite));
    if (!failures.length) {
      console.log(`test-map: ${map.units.length} units in ${suites.size} suites; every check holds${reports ? `, the inventory against ${Object.keys(reports).length} report(s) among them` : ''}`);
      return 0;
    }
    for (const failure of failures) console.log(`test-map: ${failure}`);
    return 1;
  }
  console.error('usage: node tools/test-map.mjs census [<suite>…] | reach <from> <to> [<suite>…] | check [--reports <dir> --commit <sha>]');
  return 2;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`test-map: ${error.stack ?? error.message}`);
      process.exitCode = 2;
    },
  );
}
