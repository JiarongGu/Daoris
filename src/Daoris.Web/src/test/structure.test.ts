import { readdirSync, readFileSync } from 'node:fs';
import { join, posix, relative } from 'node:path';
import { describe, expect, it } from 'vitest';

/**
 * The structure gate for the page's sources (STRUCT5c, D166; the code-structure design §5), the CLI's gate in vitest. Its
 * map is `structure.json` beside it. Each breach names the file, the line (0 for the file as a whole) and the rule:
 * - `top`: a file at `src/`'s top is on the map's `entry` list (`main.tsx`, which `index.html` runs, the stylesheet it and
 *   Storybook load, and Vite's ambient types) or one the map's `root` list names with the module it is to move to, and
 *   every name on that list is still at the top, so it only shrinks.
 * - `module`: a folder at `src/`'s top is a declared module's, and no `Application`, `Infrastructure` or `Domain` folder is
 *   at any depth: a module keeps its files at its top, or in scope folders named for what they hold.
 * - `width`: a module's top holds at most twenty sources (a test or a story is no source), or the module is on the map's
 *   `wide` list, the folders that held more when the gate came, each to be cut into scopes by a row of its own.
 * - `entry`: no source but an entry imports an entry: the entries compose the page, and nothing composes them.
 * - `mirror`: a test (`name.test.ts`, `name.part.test.tsx`) or a story (`name.stories.tsx`) whose subject `name.ts(x)` is a
 *   source of the page sits in a folder that holds a source of that name: the tests sit beside their sources. One of its
 *   own folder's that a source elsewhere shares a name with is on the map's `loose` list, with why.
 * - `map`: the map names declared modules and files that are there, each module has a file or one listed to move there,
 *   a `wide` module is still over the width, and a `loose` test is there and still apart from its namesake.
 * The rules are held on sources made in memory, one case each, and then run over the page's own tree.
 */

interface StructureMap {
  /** The module folders under `src/`. */
  modules: string[];
  /** The files that stay at `src/`'s top. */
  entry: string[];
  /** The modules whose top holds more sources than the width, until a row cuts each into scopes. */
  wide: string[];
  /** Each file still at `src/`'s top that is to move, with its module: the list only shrinks. */
  root: Record<string, string>;
  /** Each test or story named as a source elsewhere is, that is its own folder's, with why. */
  loose: Record<string, string>;
}

type Source = readonly [path: string, text: string];

/** The most sources a module's top holds (the code-structure design's ~20-file rule, as the CLI's modules keep). */
const WIDTH = 20;
const LAYERS = ['Application', 'Infrastructure', 'Domain'];
const SCRIPT = /\.(?:ts|tsx)$/;
const SUPPORT = /\.(?:test|stories)\.tsx?$/;
// A line's relative import: `import … from './x'`, a bare `import './x.css'`, `export … from`, the `} from` that closes a
// long import, a test's `vi.mock('./x')`, and a dynamic `import('./x')` after `=`, `(` or `return`. Each starts its line
// or follows code, so a source that holds an import inside a string, as this gate's own cases do, is not read as one.
const SPEC = String.raw`['"](\.{1,2}\/[^'"?]+)(?:\?[^'"]*)?['"]`;
const IMPORTS = [
  new RegExp(String.raw`^\s*import\s+(?:[^'"]*?\sfrom\s+)?${SPEC}`),
  new RegExp(String.raw`^\s*export\s+[^'"]*?\sfrom\s+${SPEC}`),
  new RegExp(String.raw`^\s*\}\s*from\s+${SPEC}`),
  new RegExp(String.raw`^\s*(?:await\s+)?vi\.(?:mock|doMock)\s*\(\s*${SPEC}`),
  new RegExp(String.raw`(?:=|\(|\breturn)\s*(?:await\s+)?import\s*\(\s*${SPEC}`, 'g'),
];
const COMMENT = /^\s*(?:\/\/|\*|\/\*)/;

function readMap(json: string): StructureMap {
  const raw = JSON.parse(json) as Partial<StructureMap>;
  return { modules: raw.modules ?? [], entry: raw.entry ?? [], wide: raw.wide ?? [], root: raw.root ?? {}, loose: raw.loose ?? {} };
}

/** What an import names, as Vite finds it: the path itself, or with `.ts`, `.tsx` or an `index` added. */
function target(paths: ReadonlySet<string>, from: string, spec: string): string {
  const base = posix.join(posix.dirname(from), spec);
  return [base, `${base}.ts`, `${base}.tsx`, `${base}/index.ts`, `${base}/index.tsx`].find((each) => paths.has(each)) ?? base;
}

/** A test's or a story's subject: its name up to the first dot, `QuestsView` for `QuestsView.held.test.tsx`. */
const subjectOf = (name: string): string => name.slice(0, name.indexOf('.'));

function checkStructure(map: StructureMap, sources: readonly Source[]): string[] {
  const breaches: [file: string, line: number, rule: string, why: string][] = [];
  const add = (file: string, line: number, rule: string, why: string): void => { breaches.push([file, line, rule, why]); };
  const paths = new Set(sources.map(([path]) => path));
  const listed = Object.values(map.root);
  const folderOf = (path: string): string => (path.includes('/') ? path.slice(0, path.lastIndexOf('/')) : '');
  const nameOf = (path: string): string => path.slice(path.lastIndexOf('/') + 1);
  const isSource = (path: string): boolean => SCRIPT.test(path) && !SUPPORT.test(path);

  for (const [file, module] of Object.entries(map.root)) {
    if (!map.modules.includes(module)) add('structure.json', 0, 'map', `${file} is mapped to ${module}, which is no module of the map`);
    if (!paths.has(file)) add(file, 0, 'top', "structure.json lists it at the sources' top, where it is not: a move takes its entry off the list");
  }
  for (const file of map.entry.filter((entry) => !paths.has(entry))) {
    add('structure.json', 0, 'map', `${file} is an entry, and is not at the sources' top`);
  }
  for (const module of map.modules) {
    if (![...paths].some((path) => path.startsWith(`${module}/`)) && !listed.includes(module)) {
      add('structure.json', 0, 'map', `${module} is a module of the map with no file in its folder and none listed to move there`);
    }
  }

  const width = new Map<string, number>();
  for (const [path] of sources) {
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
      if (parts.length === 2 && isSource(path)) width.set(parts[0]!, (width.get(parts[0]!) ?? 0) + 1);
    }
  }
  for (const [module, count] of [...width].filter(([, count]) => count > WIDTH)) {
    if (!map.wide.includes(module)) {
      add(`${module}/`, 0, 'width', `holds ${count} sources at its top, over ${WIDTH}: cut it into scope folders named for what they hold`);
    }
  }
  for (const module of map.wide) {
    if (!map.modules.includes(module)) add('structure.json', 0, 'map', `${module} is wide, and is no module of the map`);
    else if ((width.get(module) ?? 0) <= WIDTH) {
      add('structure.json', 0, 'map', `${module} is wide, and holds ${width.get(module) ?? 0} sources at its top: its entry comes off the list`);
    }
  }

  const named = new Map<string, Set<string>>();
  for (const path of [...paths].filter(isSource)) {
    const name = nameOf(path).replace(SCRIPT, '');
    named.set(name, (named.get(name) ?? new Set()).add(folderOf(path)));
  }

  for (const [path, text] of sources) {
    if (!SCRIPT.test(path)) continue;
    if (!map.entry.includes(path)) {
      text.split('\n').forEach((line, index) => {
        if (COMMENT.test(line)) return;
        const specs = IMPORTS.flatMap((pattern) => (pattern.global ? [...line.matchAll(pattern)] : [pattern.exec(line)]))
          .flatMap((match) => (match ? [match[1]!] : []));
        for (const spec of specs) {
          const imported = target(paths, path, spec);
          if (map.entry.includes(imported)) {
            add(path, index + 1, 'entry', `imports the entry ${imported}: the entries compose the page, and nothing composes them`);
          }
        }
      });
    }
    // A file the map still lists at the top is placed by its entry, and is held to its subject once it moves.
    if (SUPPORT.test(path) && !(path in map.root)) {
      const homes = named.get(subjectOf(nameOf(path)));
      const apart = homes !== undefined && !homes.has(folderOf(path));
      if (apart && !(path in map.loose)) {
        const where = [...homes].map((home) => home || "the sources' top").sort().join(', ');
        add(path, 0, 'mirror', `its subject ${subjectOf(nameOf(path))} is in ${where}: a test or a story sits beside its source`);
      } else if (!apart && path in map.loose) {
        add('structure.json', 0, 'map', `${path} is loose, and sits beside its subject or is named for none: its entry comes off the list`);
      }
    }
  }
  for (const path of Object.keys(map.loose).filter((each) => !paths.has(each))) {
    add('structure.json', 0, 'map', `${path} is loose, and is not there: a move or a delete takes its entry off the list`);
  }

  return breaches
    .sort((a, b) => a[0].localeCompare(b[0], 'en') || a[1] - b[1] || a[2].localeCompare(b[2], 'en') || a[3].localeCompare(b[3], 'en'))
    .map(([file, line, rule, why]) => `${file}:${line} [${rule}] ${why}`);
}

const MAP: StructureMap = {
  modules: ['app', 'data', 'quests', 'test'], entry: ['main.tsx', 'tokens.css'], wide: [], root: { 'api.ts': 'data' }, loose: {},
};
const UNMOVED: Source[] = [
  ['main.tsx', "import { App } from './app/App';\nimport './tokens.css';\n"], ['tokens.css', ''], ['api.ts', ''],
  ['app/App.tsx', "import { api } from '../api';\n"], ['quests/QuestsView.tsx', ''], ['test/setup.tsx', ''],
];
const check = (...sources: Source[]): string[] => checkStructure(MAP, [...UNMOVED, ...sources]);
const many = (folder: string, count: number): Source[] => Array.from({ length: count }, (_, at): Source => [`${folder}/part${at}.ts`, '']);

describe("the page's structure (STRUCT5c, D166)", () => {
  it("keeps a file at the sources' top on the entry list or the move list, and every name on that list still at the top", () => {
    expect(check()).toEqual([]);
    expect(check(['fresh.ts', ''])).toEqual(
      ["fresh.ts:1 [top] a new file at the sources' top: put it in its module's folder (structure.json's modules), not here"]);
    // A move takes its entry off the list, or the list says a file is where it is not.
    expect(checkStructure(MAP, [...UNMOVED.filter(([path]) => path !== 'api.ts'), ['data/api.ts', '']])).toEqual(
      ["api.ts:0 [top] structure.json lists it at the sources' top, where it is not: a move takes its entry off the list"]);
  });

  it("keeps a file in a folder in a module's folder, and no module keeps a layer folder", () => {
    expect(check(['data/queries.ts', ''], ['quests/history/QuestHistory.tsx', ''])).toEqual([]);
    expect(check(['gardens/rose.ts', ''], ['data/Application/clock.ts', ''], ['quests/deep/Domain/wire.ts', ''])).toEqual([
      'data/Application/clock.ts:1 [module] data holds no Application folder: a module keeps its files at its top, or in scope folders named for what they hold',
      "gardens/rose.ts:1 [module] the folder gardens is no module's: a source goes in its module's folder (structure.json's modules)",
      'quests/deep/Domain/wire.ts:1 [module] quests holds no Domain folder: a module keeps its files at its top, or in scope folders named for what they hold',
    ]);
  });

  it("keeps a module's top to twenty sources, its tests, stories and scope folders aside, unless the map lists it wide", () => {
    const tests = Array.from({ length: 5 }, (_, at): Source => [`data/part${at}.test.ts`, '']);
    expect(check(...many('data', 20), ...tests, ['data/part0.stories.tsx', ''], ...many('data/scope', 5))).toEqual([]);
    expect(check(...many('data', 21))).toEqual(
      ['data/:0 [width] holds 21 sources at its top, over 20: cut it into scope folders named for what they hold']);
    expect(checkStructure({ ...MAP, wide: ['data'] }, [...UNMOVED, ...many('data', 21)])).toEqual([]);
    // A wide module cut to the width, or one the map does not declare, takes its entry off the list.
    expect(checkStructure({ ...MAP, wide: ['quests', 'gardens'] }, UNMOVED)).toEqual([
      'structure.json:0 [map] gardens is wide, and is no module of the map',
      'structure.json:0 [map] quests is wide, and holds 1 sources at its top: its entry comes off the list',
    ]);
  });

  it('lets no source but an entry import an entry', () => {
    expect(check(['data/queries.ts', "// import { x } from '../main';\nimport { y } from '../api';\n"])).toEqual([]);
    expect(check(
      ['data/queries.ts', "import { api } from './scope';\nimport '../tokens.css';\n"],
      ['quests/QuestsView.test.tsx', "vi.mock('../main.tsx', () => ({}));\n"],
    )).toEqual([
      'data/queries.ts:2 [entry] imports the entry tokens.css: the entries compose the page, and nothing composes them',
      'quests/QuestsView.test.tsx:1 [entry] imports the entry main.tsx: the entries compose the page, and nothing composes them',
    ]);
  });

  it('keeps a test or a story beside the source it is named for, and leaves one named for no source where it is', () => {
    expect(check(
      ['quests/QuestsView.test.tsx', ''], ['quests/QuestsView.held.test.tsx', ''], ['app/App.stories.tsx', ''],
      ['app/menuBar.test.tsx', ''], ['test/structure.test.ts', ''],
    )).toEqual([]);
    // A test the map still lists at the top is placed by its entry until it moves.
    expect(checkStructure({ ...MAP, root: { ...MAP.root, 'QuestsView.test.tsx': 'quests' } }, [...UNMOVED, ['QuestsView.test.tsx', '']]))
      .toEqual([]);
    // A test of its own folder's that a source elsewhere shares a name with is listed loose, and the list holds no stale entry.
    const loose = { 'app/QuestsView.test.tsx': "the frame's", 'quests/QuestsView.test.tsx': 'beside it', 'app/gone.test.tsx': 'gone' };
    expect(checkStructure({ ...MAP, loose }, [...UNMOVED, ['app/QuestsView.test.tsx', ''], ['quests/QuestsView.test.tsx', '']])).toEqual([
      'structure.json:0 [map] app/gone.test.tsx is loose, and is not there: a move or a delete takes its entry off the list',
      'structure.json:0 [map] quests/QuestsView.test.tsx is loose, and sits beside its subject or is named for none: its entry comes off the list',
    ]);
    expect(check(['app/QuestsView.held.test.tsx', ''], ['quests/App.stories.tsx', ''], ['data/api.test.ts', ''])).toEqual([
      "app/QuestsView.held.test.tsx:0 [mirror] its subject QuestsView is in quests: a test or a story sits beside its source",
      "data/api.test.ts:0 [mirror] its subject api is in the sources' top: a test or a story sits beside its source",
      'quests/App.stories.tsx:0 [mirror] its subject App is in app: a test or a story sits beside its source',
    ]);
  });

  it('holds the map to declared modules and files that are there', () => {
    expect(checkStructure({ ...MAP, root: { 'api.ts': 'gardens' } }, UNMOVED)).toEqual([
      'structure.json:0 [map] api.ts is mapped to gardens, which is no module of the map',
      'structure.json:0 [map] data is a module of the map with no file in its folder and none listed to move there',
    ]);
    expect(checkStructure({ ...MAP, modules: [...MAP.modules, 'gardens'], entry: ['main.tsx', 'tokens.css', 'vite-env.d.ts'] }, UNMOVED)).toEqual([
      'structure.json:0 [map] gardens is a module of the map with no file in its folder and none listed to move there',
      "structure.json:0 [map] vite-env.d.ts is an entry, and is not at the sources' top",
    ]);
  });

  it("holds the page's own tree", () => {
    const folder = join(process.cwd(), 'src');
    const map = readMap(readFileSync(join(folder, 'test', 'structure.json'), 'utf8'));
    const sources = readdirSync(folder, { recursive: true, withFileTypes: true })
      .filter((entry) => entry.isFile())
      .map((entry): Source => {
        const path = relative(folder, join(entry.parentPath, entry.name)).split('\\').join('/');
        return [path, SCRIPT.test(path) ? readFileSync(join(folder, path), 'utf8') : ''];
      });

    const scripts = sources.filter(([path]) => SCRIPT.test(path));
    expect(scripts.filter(([path]) => !SUPPORT.test(path)).length, "the gate's walk no longer reaches the sources").toBeGreaterThanOrEqual(350);
    expect(scripts.filter(([path]) => SUPPORT.test(path)).length, "the gate's walk no longer reaches the tests and stories").toBeGreaterThanOrEqual(400);
    expect(checkStructure(map, sources), "each breaks the page's structure (the code-structure design §5)").toEqual([]);
  });
});
