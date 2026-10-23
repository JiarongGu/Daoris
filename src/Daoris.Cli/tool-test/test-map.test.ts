import { test } from 'node:test';
import assert from 'node:assert/strict';

/**
 * CISCOPE1c (D207, design §3): the census of every test unit, `tools/test-map.mjs`. Its TypeScript reach is held here to one
 * fixture per edge kind (§3.4 check 6): an import closure into a tool, a catalogue glob with a member added, a twin's basename,
 * a source read as text, a folder scan with a member added, another project's words, a generated output declared, a computed
 * import read wide, and a table named as data. Its checks are held to a hand row, a whole-suite file and a skip. The .NET
 * reach is PROCREACH1's census, held by `process-reach.test.ts`. Run by the `tool-tests` gate.
 */

interface Reach { files: string[]; folders: string[]; globs: string[]; wide: string[] }
interface Unit { suite: string; kind: string; unit: string; file: string; subject: string; reach: Reach }
interface Map { units: Unit[]; dotnet: unknown[]; problems: string[] }

const tool = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../tools/test-map.mjs') as {
  literalsOf: (text: string) => string[];
  globRegExp: (glob: string) => RegExp;
  census: (files: string[], read: (file: string) => string) => Map;
  tsReaches: (reach: Reach, path: string) => boolean;
  tsSubject: (file: string) => string;
  dotnetSubject: (project: string, file: string, text?: string) => { subject: string; atRoot: boolean; declared: string | null };
  checks: (files: string[], read: (file: string) => string, map: Map, options: Record<string, unknown>) => string[];
};

const TREE: Record<string, string> = {
  'daoris.gates.json': '{"gates":[]}',
  'tools/fsx.mjs': 'export const isMain = () => false;\n',
  'tools/thing.mjs': "import { isMain } from './fsx.mjs';\nexport const thing = 1;\n",
  'src/Daoris.Cli/tool-test/thing.test.ts': "const thing = await import(\n  // a seam\n  '../../../tools/thing.mjs');\n",
  'src/Daoris.Web/src/locales/index.ts': "export const all = import.meta.glob('./*/*.json', { eager: true });\n",
  'src/Daoris.Web/src/locales/en/common.json': '{}',
  'src/Daoris.Web/src/locales/index.test.ts': "import { all } from './index';\n",
  'src/Daoris.Web/src/test/driverSource.ts': "export const read = (name: string) => name;\nexport const notes = read('NoteCodes.cs');\n",
  'src/Daoris.Web/src/note/note.test.ts': "import { notes } from '../test/driverSource';\n",
  'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/NoteCodes.cs': 'class NoteCodes {}',
  'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/table.json': '[]',
  'src/Daoris.Web/src/table/table.test.ts': "const rows = readFileSync(join(root, `${'..'}`, 'table.json'));\n",
  'src/Daoris.Cli/test/text.test.ts': "const source = readFileSync('src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/NoteCodes.cs', 'utf8');\n",
  'src/Daoris.Cli/test/doctrine/scan.test.ts': "for (const file of readdirSync(join(root, 'docs/decisions'))) check(file);\n",
  'docs/decisions/D1.md': '# D1',
  'src/Daoris.Cli/test/words.test.ts': "const words = read('src/Daoris.Service');\n",
  'src/Daoris.Service/Daoris.Service.Core/Thing.cs': 'class Thing {}',
  'src/Daoris.Cli/test/built.test.ts': '// reaches: src/Daoris.Service/Daoris.Service.Http/wwwroot/** — the page builds into the host it serves\nimport x from "y";\n',
  'src/Daoris.Cli/test/computed.test.ts': 'const name = pick();\nawait import(name);\n',
  'tools/table.mjs': "// test-map: strings name paths as data\nexport const ROWS = ['docs/decisions/D1.md'];\n",
  'src/Daoris.Cli/test/table.test.ts': "import { ROWS } from '../../../tools/table.mjs';\n",
  'TASKS.md': '# Tasks\n\n- [ ] **OPEN1** — open.\n',
};

const treeOf = (extra: Record<string, string> = {}) => {
  const all = { ...TREE, ...extra };
  return { files: Object.keys(all), read: (file: string) => all[file] ?? '' };
};
const unitOf = (map: Map, file: string) => {
  const unit = map.units.find((each) => each.file === file);
  assert.ok(unit, `${file} is a unit`);
  return unit;
};

test('the literals of a source: quotes, a template and its holes, never a comment', () => {
  assert.deepEqual(tool.literalsOf("const a = 'x'; // 'not'\nconst b = `a${'b' + `c${1}d`}e`; /* \"no\" */ const c = \"y\";"), ['x', 'a', 'b', 'c', 'd', 'e', 'y']);
});

test('a glob of the lanes grammar, nested braces among it', () => {
  assert.ok(tool.globRegExp('src/{a,b/**/*.{ts,tsx}}').test('src/b/x/y.tsx'));
  assert.ok(!tool.globRegExp('src/*.ts').test('src/a/b.ts'));
});

test('every edge kind reaches its unit, and a member added to a glob or a scanned folder reaches it too', () => {
  const { files, read } = treeOf({ 'src/Daoris.Web/src/locales/zh/common.json': '{}', 'docs/decisions/D2.md': '# D2' });
  const map = tool.census(files, read);
  const reaches = (unit: string, path: string) => tool.tsReaches(unitOf(map, unit).reach, path);
  assert.ok(reaches('src/Daoris.Cli/tool-test/thing.test.ts', 'tools/fsx.mjs'), 'an import closure into a tool, through a literal dynamic import');
  assert.ok(reaches('src/Daoris.Web/src/locales/index.test.ts', 'src/Daoris.Web/src/locales/zh/common.json'), 'a catalogue glob, its member added');
  assert.ok(reaches('src/Daoris.Web/src/note/note.test.ts', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/NoteCodes.cs'), "a twin's basename through the file it imports");
  assert.ok(reaches('src/Daoris.Web/src/table/table.test.ts', 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/table.json'), "a fixture's basename");
  assert.ok(reaches('src/Daoris.Cli/test/text.test.ts', 'src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/NoteCodes.cs'), 'a source read as text');
  assert.ok(reaches('src/Daoris.Cli/test/doctrine/scan.test.ts', 'docs/decisions/D2.md'), 'a folder scan, its member added');
  assert.ok(reaches('src/Daoris.Cli/test/words.test.ts', 'src/Daoris.Service/Daoris.Service.Core/Thing.cs'), "another project's words");
  assert.ok(reaches('src/Daoris.Cli/test/built.test.ts', 'src/Daoris.Service/Daoris.Service.Http/wwwroot/index.html'), 'a generated output, declared');
  assert.ok(reaches('src/Daoris.Cli/test/computed.test.ts', 'anything/at/all.txt'), 'a computed import reaches its whole suite');
  assert.ok(!reaches('src/Daoris.Cli/test/table.test.ts', 'docs/decisions/D1.md'), 'a table named as data is not read');
  assert.ok(!reaches('src/Daoris.Cli/test/text.test.ts', 'src/Daoris.Service/Daoris.Service.Core/Thing.cs'), 'and nothing else');
});

test("a unit's subject is the module its own file sits in", () => {
  assert.equal(tool.tsSubject('src/Daoris.Cli/test/doctrine/scan.test.ts'), 'src/Daoris.Cli/src/doctrine');
  assert.equal(tool.tsSubject('src/Daoris.Cli/test/words.test.ts'), 'src/Daoris.Cli');
  assert.equal(tool.tsSubject('src/Daoris.Cli/tool-test/merge-branch.test.ts'), 'tools/merge-branch.mjs');
  assert.equal(tool.tsSubject('src/Daoris.Web/src/note/note.test.ts'), 'src/Daoris.Web/src/note');
  const project = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests';
  assert.deepEqual(tool.dotnetSubject(project, `${project}/Trees/LandTests.cs`), { subject: `${project}/Trees`, atRoot: false, declared: null });
  assert.equal(tool.dotnetSubject(project, `${project}/HandOffTests.cs`, '// subject: src/X/Trees — why\nclass HandOffTests {}').declared, 'src/X/Trees');
});

test('the checks: a hand row held to its readers, a whole-suite file listed, a skip naming an open row', () => {
  // Spelt in pieces, so this file's own census reads no skip in it.
  const skip = ['test', 'skip'].join('.');
  const { files, read } = treeOf({ 'src/Daoris.Web/vite.config.ts': 'export default {}', 'src/Daoris.Web/src/skip/skip.test.ts': `${skip}('CLOSED1: waits', () => {});\n${skip}('OPEN1: waits', () => {});\n` });
  const map = tool.census(files, read);
  const failures = tool.checks(files, read, map, {
    reach: [{ paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/table.json'], gates: ['driver', 'web-unit'] }],
    baseline: ['cli'], tasks: read('TASKS.md'), listed: { 'tools/gone.json': 'stale' },
  });
  assert.ok(failures.some((line) => /hand row .*table\.json: the row names driver, where the census finds no reader/.test(line)), failures.join('\n'));
  assert.ok(!failures.some((line) => /web-unit, which the row omits/.test(line)));
  assert.ok(failures.some((line) => /whole-suite: src\/Daoris\.Web\/vite\.config\.ts/.test(line)));
  assert.ok(failures.some((line) => /whole-suite: tools\/test-map\.json lists tools\/gone\.json/.test(line)));
  assert.ok(failures.some((line) => /skip: .*CLOSED1: waits/.test(line)));
  assert.ok(!failures.some((line) => /OPEN1: waits/.test(line)));
  const held = tool.checks(files, read, map, {
    reach: [{ paths: ['src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/fixtures/table.json'], gates: ['web-unit'] }],
    baseline: ['cli'], tasks: '- [ ] **CLOSED1** — open again.\n- [ ] **OPEN1** — open.', listed: { 'src/Daoris.Web/vite.config.ts': 'the config' },
  });
  assert.deepEqual(held, []);
});
