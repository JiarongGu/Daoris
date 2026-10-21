import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join, posix, relative } from 'node:path';
import { cliRoot, ROWS, SOURCES, sourcePaths } from './_sources.ts';

/**
 * The structure gate for the CLI's sources (STRUCT5d, D166; the code-structure design §5), the desktop modules' and the
 * service core's gate in TypeScript. Its map is `fixtures/structure.json`. Each breach names the file, the line (0 for the
 * file as a whole) and the rule:
 * - `top`: a file at `src/`'s top is the entry (`cli.ts`: `bin/daoris.mjs` runs it, built as `dist/cli.js`) or one the map's
 *   `root` list names with the module it is to move to, and every name on that list is still at the top, so it only shrinks.
 * - `module`: a file in a folder is in a declared module's folder, and no `Application`, `Infrastructure` or `Domain` folder
 *   is at any depth: a module keeps its files at its top, or in scope folders named for what they hold.
 * - `name`: a source outside the command rows' `cli/` has a name no other there has, since a reader finds it by its name
 *   (`_sources.ts`); a row shares its verb's name with the module that handles it, and is read by its path.
 * - `entry`: no source imports the entry, and only the entry imports a command row: `cli.ts` composes the rows' table.
 * - `mirror`: a test folder is a module's or `fixtures`, and a test the map's `tests` lists sits under its module's folder.
 * - `map`: the map names declared modules and files that are there, and each module has a file or one listed to move there.
 * The rules are held on sources made in memory, one case each, and then run over the CLI's own tree.
 */

interface StructureMap {
  /** The module folders under `src/`. */
  modules: string[];
  /** The files that stay at `src/`'s top. */
  entry: string[];
  /** Each file still at `src/`'s top that is to move, with its module: the list only shrinks. */
  root: Record<string, string>;
  /** Each test that mirrors its module, with the module. */
  tests: Record<string, string>;
}

type Source = readonly [path: string, text: string];

const LAYERS = ['Application', 'Infrastructure', 'Domain'];
const TEST_SUPPORT = ['fixtures'];
// `from './x.ts'`, a bare `import './x.ts'` and a dynamic `import('./x.ts')`, as dogfood.test.ts walks them.
const IMPORT = /(?:\bfrom|\bimport)\s*\(?\s*['"](\.{1,2}\/[\w./-]+\.ts)['"]/g;
const COMMENT = /^\s*(?:\/\/|\*|\/\*)/;

function readMap(json: string): StructureMap {
  const raw = JSON.parse(json) as Partial<StructureMap>;
  return { modules: raw.modules ?? [], entry: raw.entry ?? [], root: raw.root ?? {}, tests: raw.tests ?? {} };
}

function checkStructure(map: StructureMap, sources: readonly Source[], tests: readonly string[] = []): string[] {
  const breaches: [file: string, line: number, rule: string, why: string][] = [];
  const add = (file: string, line: number, rule: string, why: string): void => { breaches.push([file, line, rule, why]); };
  const paths = sources.map(([path]) => path);
  const listed = Object.values(map.root);

  for (const [file, module] of Object.entries(map.root)) {
    if (!map.modules.includes(module)) add('structure.json', 0, 'map', `${file} is mapped to ${module}, which is no module of the map`);
    if (!paths.includes(file)) add(file, 0, 'top', "structure.json lists it at the sources' top, where it is not: a move takes its entry off the list");
  }
  for (const file of map.entry.filter((entry) => !paths.includes(entry))) {
    add('structure.json', 0, 'map', `${file} is the entry, and is not at the sources' top`);
  }
  for (const module of map.modules) {
    if (!paths.some((path) => path.startsWith(`${module}/`)) && !listed.includes(module)) {
      add('structure.json', 0, 'map', `${module} is a module of the map with no file in its folder and none listed to move there`);
    }
  }

  const named = new Map<string, string>();
  for (const [path, text] of sources) {
    const parts = path.split('/');
    if (parts.length === 1) {
      if (!map.entry.includes(path) && !(path in map.root)) {
        add(path, 1, 'top', "a new file at the sources' top: put it in its module's folder (structure.json's modules), not here");
      }
    } else if (!map.modules.includes(parts[0]!)) {
      add(path, 1, 'module', `the folder ${parts[0]} is no module's: a source goes in its module's folder (structure.json's modules)`);
    } else {
      const layer = parts.slice(1, -1).find((part) => LAYERS.includes(part));
      if (layer) add(path, 1, 'module', `${parts[0]} holds no ${layer} folder: a module keeps its files at its top, or in scope folders named for what they hold`);
    }

    if (!path.startsWith(ROWS)) {
      const name = parts.at(-1)!;
      const first = named.get(name);
      if (first) add(path, 1, 'name', `${first} has its name: a reader finds a source by its name (test/_sources.ts), so a name is one file outside ${ROWS}`);
      else named.set(name, path);
    }

    text.split('\n').forEach((line, index) => {
      if (COMMENT.test(line)) return;
      for (const match of line.matchAll(IMPORT)) {
        const target = posix.join(posix.dirname(path), match[1]!);
        if (map.entry.includes(target)) {
          add(path, index + 1, 'entry', `imports the entry ${target}: the entry composes the commands, and nothing imports it`);
        } else if (target.startsWith(ROWS) && !map.entry.includes(path)) {
          add(path, index + 1, 'entry', `imports the command row ${target}: only the entry imports a row`);
        }
      }
    });
  }

  for (const path of tests) {
    const top = path.split('/')[0]!;
    if (path.includes('/') && !map.modules.includes(top) && !TEST_SUPPORT.includes(top)) {
      add(path, 0, 'mirror', `the folder ${top} is no module's: a test folder is a module's, or fixtures`);
    }
  }
  for (const [name, module] of Object.entries(map.tests)) {
    if (!map.modules.includes(module)) add('structure.json', 0, 'map', `${name} is a test mapped to ${module}, which is no module of the map`);
    const found = tests.filter((path) => path.split('/').at(-1) === name);
    if (found.length === 0) {
      add('structure.json', 0, 'map', `${name} is listed under ${module}, and no such test is in the tests' tree: a delete takes its entry off the list`);
    }
    for (const path of found.filter((each) => !each.includes('/') || each.split('/')[0] !== module)) {
      const where = path.includes('/') ? `under ${path.split('/')[0]}` : 'at the top of the tests';
      add(path, 0, 'mirror', `sits ${where}, and the map lists it under ${module}: a test mirrors its module's folder`);
    }
  }

  return breaches
    .sort((a, b) => a[0].localeCompare(b[0], 'en') || a[1] - b[1] || a[2].localeCompare(b[2], 'en') || a[3].localeCompare(b[3], 'en'))
    .map(([file, line, rule, why]) => `${file}:${line} [${rule}] ${why}`);
}

const MAP: StructureMap = { modules: ['cli', 'doctrine', 'platform'], entry: ['cli.ts'], root: { 'drift.ts': 'doctrine' }, tests: {} };
const UNMOVED: Source[] = [['cli.ts', "import { command } from './cli/sync.ts';\n"], ['drift.ts', ''], ['cli/sync.ts', ''], ['platform/fsx.ts', '']];
const check = (...sources: Source[]): string[] => checkStructure(MAP, [...UNMOVED, ...sources]);

test("a file at the sources' top is the entry or one the list names, and every name on the list is still at the top", () => {
  assert.deepEqual(check(), []);
  assert.deepEqual(check(['fresh.ts', '']),
    ["fresh.ts:1 [top] a new file at the sources' top: put it in its module's folder (structure.json's modules), not here"]);
  // A move takes its entry off the list, or the list says a file is where it is not.
  assert.deepEqual(checkStructure(MAP, [['cli.ts', ''], ['cli/sync.ts', ''], ['doctrine/drift.ts', ''], ['platform/fsx.ts', '']]),
    ["drift.ts:0 [top] structure.json lists it at the sources' top, where it is not: a move takes its entry off the list"]);
});

test('a file in a folder is in a module’s folder, and no module keeps a layer folder', () => {
  assert.deepEqual(check(['platform/home.ts', ''], ['doctrine/tiers/region.ts', '']), []);
  assert.deepEqual(check(
    ['gardens/rose.ts', ''], ['platform/Application/clock.ts', ''], ['platform/deep/Domain/wire.ts', ''], ['doctrine/Infrastructure/http.ts', ''],
  ), [
    'doctrine/Infrastructure/http.ts:1 [module] doctrine holds no Infrastructure folder: a module keeps its files at its top, or in scope folders named for what they hold',
    "gardens/rose.ts:1 [module] the folder gardens is no module's: a source goes in its module's folder (structure.json's modules)",
    'platform/Application/clock.ts:1 [module] platform holds no Application folder: a module keeps its files at its top, or in scope folders named for what they hold',
    'platform/deep/Domain/wire.ts:1 [module] platform holds no Domain folder: a module keeps its files at its top, or in scope folders named for what they hold',
  ]);
});

test('a source outside the command rows has a name no other has, and a row may share its verb’s name', () => {
  assert.deepEqual(check(['doctrine/sync.ts', '']), []);
  assert.deepEqual(check(['doctrine/fsx.ts', '']),
    ['doctrine/fsx.ts:1 [name] platform/fsx.ts has its name: a reader finds a source by its name (test/_sources.ts), so a name is one file outside cli/']);
});

test('nothing imports the entry, and only the entry imports a command row', () => {
  assert.deepEqual(check(['platform/home.ts', "import { x } from '../drift.ts';\n// import { y } from '../cli.ts';\n"]), []);
  assert.deepEqual(check(
    ['platform/home.ts', "import { x } from './fsx.ts';\nimport { packageRoot } from '../cli.ts';\n"],
    ['cli/check.ts', "const { command } = await import('./sync.ts');\n"],
  ), [
    'cli/check.ts:1 [entry] imports the command row cli/sync.ts: only the entry imports a row',
    'platform/home.ts:2 [entry] imports the entry cli.ts: the entry composes the commands, and nothing imports it',
  ]);
});

test('a test folder is a module’s or fixtures, and a test the map lists sits under its module', () => {
  const mirror = (tests: string[], listed: Record<string, string> = {}): string[] =>
    checkStructure({ ...MAP, tests: listed }, UNMOVED, tests);
  assert.deepEqual(mirror(['sync.test.ts', 'fixtures/a.json', 'platform/fsx.test.ts', 'doctrine/deep/drift.test.ts'], { 'drift.test.ts': 'doctrine' }), []);
  assert.deepEqual(mirror(['gardens/rose.test.ts', 'fsx.test.ts', 'doctrine/home.test.ts'], { 'fsx.test.ts': 'platform', 'home.test.ts': 'platform' }), [
    'doctrine/home.test.ts:0 [mirror] sits under doctrine, and the map lists it under platform: a test mirrors its module\'s folder',
    'fsx.test.ts:0 [mirror] sits at the top of the tests, and the map lists it under platform: a test mirrors its module\'s folder',
    "gardens/rose.test.ts:0 [mirror] the folder gardens is no module's: a test folder is a module's, or fixtures",
  ]);
});

test('the map names declared modules and files that are there', () => {
  assert.deepEqual(checkStructure({ ...MAP, root: { 'drift.ts': 'gardens' } }, [...UNMOVED, ['doctrine/region.ts', '']]),
    ['structure.json:0 [map] drift.ts is mapped to gardens, which is no module of the map']);
  assert.deepEqual(checkStructure({ ...MAP, modules: [...MAP.modules, 'gardens'], entry: ['cli.ts', 'main.ts'], tests: { 'gone.test.ts': 'platform' } }, UNMOVED), [
    'structure.json:0 [map] gardens is a module of the map with no file in its folder and none listed to move there',
    "structure.json:0 [map] gone.test.ts is listed under platform, and no such test is in the tests' tree: a delete takes its entry off the list",
    "structure.json:0 [map] main.ts is the entry, and is not at the sources' top",
  ]);
});

const mapFile = join(cliRoot, 'test', 'fixtures', 'structure.json');

// PUSH1f2b (D194 point 7): `driver` and `workflows` left with the driver family's TypeScript, the family Daoris's driver program's whole.
test("the map names the CLI's modules", () => {
  assert.deepEqual(readMap(readFileSync(mapFile, 'utf8')).modules,
    ['cli', 'platform', 'doctrine', 'layout', 'accounts', 'agents', 'tools', 'service', 'plugins', 'browser']);
});

test("the CLI's own tree keeps its structure", () => {
  const map = readMap(readFileSync(mapFile, 'utf8'));
  const sources = sourcePaths().map((path): Source => [path, readFileSync(join(SOURCES, path), 'utf8')]);
  const testsFolder = join(cliRoot, 'test');
  const tests = readdirSync(testsFolder, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => relative(testsFolder, join(entry.parentPath, entry.name)).split('\\').join('/'));

  assert.ok(sources.length >= 80, `the gate read only ${sources.length} sources: its walk no longer reaches the sources' tree`);
  assert.ok(tests.length >= 100, `the gate read only ${tests.length} tests: its walk no longer reaches the tests' tree`);
  assert.deepEqual(checkStructure(map, sources, tests), [], "each breaks the CLI's structure (the code-structure design §5)");
});
