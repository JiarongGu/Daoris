import { test } from 'node:test';
import assert from 'node:assert/strict';

/**
 * The census of every test unit, `tools/test-map.mjs`. Its TypeScript reach is held here to one
 * fixture per edge kind (design §3.4 check 6): an import closure into a tool, a catalogue glob with a member added, a twin's basename,
 * a source read as text, a folder scan with a member added, another project's words, a generated output declared, a computed
 * import read wide, and a table named as data. Its checks are held to a hand row, a whole-suite file and a skip. The .NET
 * reach is the process-reach census's, held by `process-reach.test.ts`. Run by the `tool-tests` gate.
 */

interface Reach { files: string[]; folders: string[]; globs: string[]; wide: string[] }
interface Unit { suite: string; kind: string; unit: string; file: string; cwd?: string; subject: string; reach: Reach }
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
  trxSkipped: (trx: string) => string[];
  unitHops: (unit: unknown, paths: string[], map: unknown, depth?: number) => number | undefined;
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

test("an end-to-end spec is reached only by a path its gate declares, so a tools change outside the merge tool reaches no e2e spec", () => {
  const gates = { gates: [{ name: 'tool-e2e', run: 'npm run tooltests:e2e', paths: ['daoris.gates.json', 'tools/merge-branch.mjs', 'tools/merge/', 'src/Daoris.Cli/tool-test/'] }] };
  const { files, read } = treeOf({
    'daoris.gates.json': JSON.stringify(gates),
    'tools/merge-branch.mjs': "import { isMain } from './fsx.mjs';\nimport { plan } from './merge/plan.ts';\nexport const merge = plan;\n",
    'tools/merge/plan.ts': 'export const plan = 1;\n',
    'src/Daoris.Cli/tool-test/merge-branch-flow.e2e.ts': "const tool = await import(\n  // a seam\n  '../../../tools/merge-branch.mjs');\n",
    'src/Daoris.Cli/tool-test/merge-branch-flow.test.ts': "const tool = await import(\n  // a seam\n  '../../../tools/merge-branch.mjs');\n",
  });
  const map = tool.census(files, read);
  const e2e = unitOf(map, 'src/Daoris.Cli/tool-test/merge-branch-flow.e2e.ts');
  const unit = unitOf(map, 'src/Daoris.Cli/tool-test/merge-branch-flow.test.ts');
  assert.equal(e2e.suite, 'tool-e2e', 'an e2e spec is a unit of the end-to-end tier');
  assert.equal(unit.suite, 'tool-tests', 'a test file beside it stays in the unit tier');
  assert.equal(map.units.filter((each) => each.file === e2e.file).length, 1, 'and in no other');
  assert.equal(tool.tsSubject(e2e.file), 'tools/merge-branch.mjs');
  assert.ok(tool.tsReaches(e2e.reach, 'tools/merge-branch.mjs'), "the merge tool's own file reaches it");
  assert.ok(tool.tsReaches(e2e.reach, 'tools/merge/plan.ts'), 'and a module of the merge tool it imports');
  assert.ok(!tool.tsReaches(e2e.reach, 'tools/fsx.mjs'), 'a tools change outside the merge tool reaches no e2e spec, though the tool imports it');
  assert.ok(tool.tsReaches(unit.reach, 'tools/fsx.mjs'), 'the unit tier is reached by its whole closure, as before');
});

test("a unit says how many reads lie between it and a changed file, so a bound can keep the change's own tests", () => {
  const work = 'src/Daoris.Web/src/work';
  const { files, read } = treeOf({
    [`${work}/rail/Row.tsx`]: 'export const Row = 1;\n',
    [`${work}/rail/Rail.tsx`]: "import { Row } from './Row';\nexport const Rail = Row;\n",
    [`${work}/rail/Rail.test.tsx`]: "import { Rail } from './Rail';\n",
    [`${work}/Frame.tsx`]: "import { Rail } from './rail/Rail';\nexport const Frame = Rail;\n",
    [`${work}/Page.tsx`]: "import { Frame } from './Frame';\nexport const Page = Frame;\n",
    [`${work}/Page.test.tsx`]: "import { Page } from './Page';\n",
  });
  const map = tool.census(files, read);
  const hops = (unit: string, path: string) => tool.unitHops(unitOf(map, unit), [path], map);
  assert.equal(hops(`${work}/rail/Rail.test.tsx`, `${work}/rail/Rail.test.tsx`), 0, 'its own file');
  assert.equal(hops(`${work}/rail/Rail.test.tsx`, `${work}/rail/Rail.tsx`), 1, 'what it imports');
  assert.equal(hops(`${work}/rail/Rail.test.tsx`, `${work}/rail/Row.tsx`), 2, 'what that imports');
  assert.equal(hops(`${work}/Page.test.tsx`, `${work}/rail/Row.tsx`), undefined, 'a unit further off is reached, not near');
  assert.equal(tool.unitHops(unitOf(map, `${work}/rail/Rail.test.tsx`), [`${work}/Page.tsx`, `${work}/rail/Row.tsx`], map), 2, 'the nearest of the changed paths');
  // A .NET class is one read from the source file that declares a type its files name, and no nearer to an area's other files.
  const dotnet = { dotnet: [{ suite: 'driver', project: 'T', census: { classes: [{ full: 'N.ATests', sourceFiles: ['S/A.cs'], reach: ['S'] }] } }] };
  const cls = { kind: 'dotnet', suite: 'driver', project: 'T', unit: 'N.ATests', file: 'T/ATests.cs' };
  assert.equal(tool.unitHops(cls, ['S/A.cs'], dotnet), 1);
  assert.equal(tool.unitHops(cls, ['S/B.cs'], dotnet), undefined);
  assert.equal(tool.unitHops(cls, ['T/ATests.cs'], dotnet), 0);
});

test("a page change reaches its domain's tests, through the bridge's barrel by the names each takes, and a catalogue its keys' readers", () => {
  const web = 'src/Daoris.Web/src';
  const { files, read } = treeOf({
    [`${web}/bridge/shell.ts`]: "// The barrel.\nexport * from './agents';\nexport * from './work';\n",
    [`${web}/bridge/call.ts`]: 'export const call = (what: string) => what;\n',
    [`${web}/bridge/agents.ts`]: "import { call } from './call';\nexport const useAgents = () => call('agents');\n",
    [`${web}/bridge/work.ts`]: "import { call } from './call';\nexport function useWork() { return call('work'); }\nexport type WorkRow = { id: string };\n",
    [`${web}/bridge/bridge.test.ts`]: "import * as shell from './shell';\n",
    [`${web}/agents/AgentsView.tsx`]: "import { useAgents } from '../bridge/shell';\nimport type { WorkRow } from '../bridge/shell';\nexport const AgentsView = (key: string) => t('agents.list.open') + key.split('.');\n",
    [`${web}/agents/Other.tsx`]: 'export const Other = 1;\n',
    [`${web}/agents/AgentsView.test.tsx`]: "import { AgentsView } from './AgentsView';\n",
    [`${web}/work/WorkView.tsx`]: "import { type WorkRow, useWork } from '../bridge/shell';\nexport const WorkView = (x: string) => t(`work.rail.${x}`);\n",
    [`${web}/work/WorkView.test.tsx`]: "import { WorkView } from './WorkView';\n",
    [`${web}/test/setup.tsx`]: "import '../locales/i18n';\n",
    [`${web}/locales/i18n.ts`]: "// test-map: reads the catalogues by key — its readers name their keys\nimport { all, languages } from './index';\n",
    [`${web}/locales/parity.test.ts`]: "import { all } from '.';\n",
    [`${web}/locales/en/agents.json`]: '{}',
    [`${web}/locales/zh/agents.json`]: '{}',
    [`${web}/locales/en/work.json`]: '{}',
    [`${web}/locales/en/work.rail.json`]: '{}',
  });
  const map = tool.census(files, read);
  const reaches = (unit: string, path: string) => tool.tsReaches(unitOf(map, `${web}/${unit}`).reach, `${web}/${path}`);
  assert.ok(reaches('agents/AgentsView.test.tsx', 'bridge/agents.ts'), 'a bridge domain whose names its page takes from the barrel');
  assert.ok(reaches('agents/AgentsView.test.tsx', 'bridge/call.ts'), "and what that domain imports");
  assert.ok(reaches('agents/AgentsView.test.tsx', 'bridge/shell.ts'), 'and the barrel itself');
  assert.ok(!reaches('agents/AgentsView.test.tsx', 'bridge/work.ts'), "not another domain of the barrel, its type taken alone");
  assert.ok(reaches('work/WorkView.test.tsx', 'bridge/work.ts') && !reaches('work/WorkView.test.tsx', 'bridge/agents.ts'), 'and the other way round');
  assert.ok(reaches('bridge/bridge.test.ts', 'bridge/agents.ts') && reaches('bridge/bridge.test.ts', 'bridge/work.ts'), 'a test taking the barrel whole reaches every domain');
  assert.ok(!reaches('agents/AgentsView.test.tsx', 'agents/Other.tsx'), "a key's dot names no folder");
  assert.ok(reaches('agents/AgentsView.test.tsx', 'locales/en/agents.json') && reaches('agents/AgentsView.test.tsx', 'locales/zh/agents.json'), 'a catalogue whose key its page names, in every language');
  assert.ok(!reaches('agents/AgentsView.test.tsx', 'locales/en/work.json'), 'not a catalogue it names no key of, though the i18n runtime loads every one');
  assert.ok(reaches('work/WorkView.test.tsx', 'locales/en/work.rail.json'), "a template's prefix names its area");
  assert.ok(!reaches('work/WorkView.test.tsx', 'locales/en/work.json'), 'and not the shorter area its keys do not live in');
  assert.ok(reaches('agents/AgentsView.test.tsx', 'locales/i18n.ts'), 'what the setup file loads is in every unit');
  assert.ok(reaches('locales/parity.test.ts', 'locales/en/work.json') && reaches('locales/parity.test.ts', 'locales/en/agents.json'), 'a parity suite reads every catalogue');
  assert.equal(tool.unitHops(unitOf(map, `${web}/agents/AgentsView.test.tsx`), [`${web}/locales/en/agents.json`], map), 2, "a catalogue is two reads from its key's page's test");
  assert.equal(tool.unitHops(unitOf(map, `${web}/locales/parity.test.ts`), [`${web}/locales/en/agents.json`], map), 1, 'and one from a suite reading them all');
});

test("a unit's subject is the module its own file sits in", () => {
  assert.equal(tool.tsSubject('src/Daoris.Cli/test/doctrine/scan.test.ts'), 'src/Daoris.Cli/src/doctrine');
  assert.equal(tool.tsSubject('src/Daoris.Cli/test/words.test.ts'), 'src/Daoris.Cli');
  assert.equal(tool.tsSubject('src/Daoris.Cli/tool-test/merge-branch-ci.test.ts'), 'tools/merge-branch.mjs');
  assert.equal(tool.tsSubject('tools/merge/flow.test.ts'), 'tools/merge/flow.ts');
  assert.equal(tool.tsSubject('tools/merge/merge.e2e.ts'), 'tools/merge-branch.mjs');
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

test('a report names each class with a test that did not execute (REVIEW2 T1)', () => {
  const trx = [
    '<TestRun><TestDefinitions>',
    '<UnitTest name="A" id="1"><Execution id="e"/><TestMethod codeBase="x" className="Ns.SshProofs" name="A"/></UnitTest>',
    '<UnitTest name="B" id="2"><TestMethod codeBase="x" className="Ns.Plain" name="B"/></UnitTest>',
    '</TestDefinitions><Results>',
    '<UnitTestResult testId="1" outcome="NotExecuted"/><UnitTestResult testId="2" outcome="Passed"/>',
    '</Results></TestRun>',
  ].join('\n');
  assert.deepEqual(tool.trxSkipped(trx), ['Ns.SshProofs']);
});

test("a tools test is a unit of the first declared set whose pattern holds it, run from the set's folder, a new file with no list edited", () => {
  const files: Record<string, string> = {
    'daoris.gates.json': JSON.stringify({
      gates: [{ name: 'cli-tests', run: 'npm run verify:tests' }, { name: 'tool-tests', run: 'npm run tooltests' }, { name: 'layers', run: 'node tools/test-sets.mjs run --gate layers' }],
      tests: { sets: [
        { name: 'layers', gate: 'layers', runner: 'node', files: ['tools/c.test.mjs'] },
        { name: 'tool-tests', gate: 'tool-tests', runner: 'node', cwd: 'src/Daoris.Cli', files: ['tools/b.test.mjs'] },
        { name: 'tools', gate: 'cli-tests', suite: 'cli', runner: 'node', files: ['tools/*.test.mjs'] },
      ] },
    }),
    'tools/a.test.mjs': '// reaches: src/**/*.cs — it lists every C# source\n', 'tools/b.test.mjs': '', 'tools/c.test.mjs': '', 'tools/brand-new.test.mjs': '', 'src/X.cs': '',
  };
  const read = (file: string) => files[file] ?? '';
  const map = tool.census(Object.keys(files), read);
  const placed = map.units.filter((each) => each.file.startsWith('tools/')).map((each) => ({ suite: each.suite, file: each.file, cwd: each.cwd }));
  assert.deepEqual(placed, [
    { suite: 'layers', file: 'tools/c.test.mjs', cwd: '.' }, { suite: 'tool-tests', file: 'tools/b.test.mjs', cwd: 'src/Daoris.Cli' },
    { suite: 'cli', file: 'tools/a.test.mjs', cwd: '.' }, { suite: 'cli', file: 'tools/brand-new.test.mjs', cwd: '.' },
  ]);
  const unit = map.units.find((each) => each.file === 'tools/a.test.mjs');
  assert.equal(unit?.subject, 'tools/a.mjs');
  assert.equal(tool.unitHops(unit, ['src/X.cs'], map), 1);
});
