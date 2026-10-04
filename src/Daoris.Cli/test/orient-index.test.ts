import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture } from './_fixture.ts';

// ORIENT1a and 1b: `tools/orient-index.mjs` writes `docs/index/` from the code and the decisions, so a session
// starts from a lookup rather than a search, and `--check` fails when the index no longer says what the tree does.
type Entry = { start: number; end: number; depth: number; name: string };
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const tool = await import('../../../tools/orient-index.mjs') as {
  INDEX: string;
  LARGE: number;
  outlineCSharp: (text: string) => Entry[];
  outlineScript: (text: string, path: string) => Entry[];
  outlineMarkdown: (text: string) => Entry[];
  continued: (lines: string[], language: 'cs' | 'ts') => { inside: boolean[]; clean: boolean };
  firstArgument: (text: string, at: number) => { text: string; start: number } | null;
  noteLabel: (line: string) => string | null;
  listFiles: (root: string) => string[];
  planIndex: (root: string, files?: string[]) => Map<string, string>;
  staleness: (root: string, planned: Map<string, string>) => { path: string; fact: string }[];
  applyIndex: (root: string, planned: Map<string, string>) => { path: string; fact: string }[];
};

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');
const toolPath = join(repoRoot, 'tools', 'orient-index.mjs');
const brief = (entries: Entry[]) => entries.map((entry) => `${'  '.repeat(entry.depth)}${entry.start}-${entry.end} ${entry.name}`);

// ---------------------------------------------------------------------------------------------------
// Outlines: every declaration of a large file, with the lines to read

test('a C# outline gives each type and method its lines, and never reads a declaration out of a string', () => {
  const text = [
    'namespace Demo.Tools;', //                                   1
    '',
    '/// <summary>A thing.</summary>',
    'public sealed class Widget : IDisposable', //                4
    '{',
    '    private readonly string _name = new("x");',
    '',
    '    /// <summary>Makes one.</summary>',
    '    public Widget(string name)', //                          9
    '    {',
    '        _name = name;',
    '    }',
    '',
    '    [Fact]',
    '    public async Task<int> CountAsync(', //                  15
    '        int a,',
    '        int b)',
    '    {',
    '        var text = """',
    '    public void Fake()', //                                  20: inside a raw string
    '    {',
    '    }',
    '    """;',
    '        return a + b;',
    '    }', //                                                   25
    '',
    '    public int Twice(int x) => x * 2;',
    '',
    '    private sealed class Inner',
    '    {', //                                                   30
    '        internal void Touch() { }',
    '    }',
    '',
    '    public string Name => _name;',
    '',
    '    public void Dispose()', //                               36
    '    {',
    '    }',
    '}',
    '', //                                                        40
    'public sealed record Pair(int Left, int Right);',
  ].join('\n');
  assert.deepEqual(brief(tool.outlineCSharp(text)), [
    '4-39 class Widget',
    '  9-12 Widget()',
    '  15-25 CountAsync()',
    '  27-27 Twice()',
    '  29-32 class Inner',
    '    31-31 Touch()',
    '  36-38 Dispose()',
    '41-41 record Pair',
  ]);
});

test('a tuple return type is read past to the parameters', () => {
  const text = [
    'public sealed class Pick',
    '{',
    '    public (string? Value, int From) Resolve(string name) =>',
    '        (name, 1);',
    '',
    '    private static (int A, int B) Both(',
    '        int a)',
    '    {',
    '        return (a, a);',
    '    }',
    '}',
  ].join('\n');
  assert.deepEqual(brief(tool.outlineCSharp(text)), ['1-11 class Pick', '  3-4 Resolve()', '  6-10 Both()']);
});

test('a verbatim string that spans lines is read past, and top-level statements give their local functions and routes', () => {
  const texts = [
    'public static class Texts',
    '{',
    '    public const string Help = @"line one',
    '}',
    'public void Nope()',
    '";',
    '',
    '    public static string Read() => Help;',
    '}',
  ].join('\n');
  assert.deepEqual(brief(tool.outlineCSharp(texts)), ['1-9 class Texts', '  8-8 Read()']);

  const program = [
    'var app = builder.Build();',
    'Console.WriteLine(Describe(app));',
    'if (shared) Start(app);',
    'app.MapGet("/api/status", (Service s) =>', //               4
    '    s.Status());',
    'if (shared)',
    '{',
    '    app.MapPost("/api/quests/{id}/accept", async (string id) =>', // 8
    '    {',
    '        return Results.Ok();',
    '    });',
    '}',
    'return 0;',
    '',
    'static string Describe(object app) => app.ToString()!;', // 15
    '',
    'int Verify()',
    '{',
    '    return 0;',
    '}', //                                                      20
  ].join('\n');
  assert.deepEqual(brief(tool.outlineCSharp(program)), [
    '4-5 GET /api/status',
    '8-11 POST /api/quests/{id}/accept',
    '15-15 Describe()',
    '17-20 Verify()',
  ]);
});

test('a script outline gives each top-level declaration, class member and phase, and a template is read past', () => {
  const text = [
    "import { x } from './x';",
    '',
    '/** Reads one. */',
    'export function readOne(', //                    4
    '  path: string,',
    '): string {',
    '  const text = `',
    'export function nope() {', //                    8: inside a template
    '}',
    '`;',
    '  return text + path;',
    '}', //                                           12
    '',
    'export const twice = (n: number) => n * 2;',
    '',
    'const local = 3;',
    '',
    'export class Box {', //                          18
    '  constructor(private size: number) {}',
    '',
    '  grow(by: number) {',
    '    this.size += by;',
    '  }',
    '}',
    '',
    "export type Shape = { kind: 'box' } | {", //     26
    "  kind: 'ball';",
    '};',
    '',
    "section('1. The first phase');", //              30
    'run();',
    "section('2. The second phase');",
    'done();',
  ].join('\n');
  assert.deepEqual(brief(tool.outlineScript(text, 'tools/demo.mjs')), [
    '4-12 function readOne',
    '14-14 const twice',
    '18-24 class Box',
    '  19-19 constructor()',
    '  21-23 grow()',
    '26-28 type Shape',
    '30-31 § 1. The first phase',
    '32-33 § 2. The second phase',
  ]);
});

test("a long function's body is outlined too: its own functions, effects and return", () => {
  const filler = Array.from({ length: 160 }, (_, n) => `  const v${n} = ${n};`);
  const text = [
    'export function Page() {',
    '  const onSave = () => {',
    '    save();',
    '  };',
    ...filler,
    '  useEffect(() => {',
    '    load();',
    '  }, []);',
    '  return (',
    '    <div />',
    '  );',
    '}',
  ].join('\n');
  assert.deepEqual(brief(tool.outlineScript(text, 'src/Page.tsx')), ['1-171 function Page', '  2-4 const onSave', '  165-167 useEffect', '  168-170 return']);
});

test("a test file's outline nests its cases under their groups, with the helpers beside them", () => {
  const text = [
    "import { test, describe } from 'node:test';",
    '',
    'const helper = () => 1;',
    '',
    "describe('the group', () => {",
    "  test('the first case', () => {",
    '    helper();',
    '  });',
    '',
    "  it('the second case', async () => {",
    '    await helper();',
    '  });',
    '});',
    '',
    "test('a lone case', () => {});",
  ].join('\n');
  assert.deepEqual(brief(tool.outlineScript(text, 'src/demo.test.ts')), [
    '3-3 const helper',
    "5-13 describe 'the group'",
    "  6-8 test 'the first case'",
    "  10-12 it 'the second case'",
    "15-15 test 'a lone case'",
  ]);
});

test("a markdown outline gives each heading to the next of its level, and a fence's lines are not headings", () => {
  const text = ['# Title', '', 'Intro.', '', '## One', '', '```sh', '# not a heading', '```', '', '### One point one', '', 'Text.', '', '## Two', '', 'Last.'].join('\n');
  assert.deepEqual(brief(tool.outlineMarkdown(text)), ['1-17 Title', '  5-13 One', '    11-13 One point one', '  15-17 Two']);
});

test('a scan that ends inside a string or comment says so, and is not trusted', () => {
  assert.equal(tool.continued(['var a = """', 'never closed'], 'cs').clean, false);
  assert.equal(tool.continued(['const a = `', 'never closed'], 'ts').clean, false);
  const ts = tool.continued(['const re = /[`]/;', 'const t = `a', 'b`;', 'next();'], 'ts');
  assert.deepEqual(ts, { inside: [false, false, true, false], clean: true });
});

// ---------------------------------------------------------------------------------------------------
// The index's sections, over a fixture repository

function fixtureRepository(name: string) {
  const fx = makeFixture(name);
  fx.write('daoris.json', JSON.stringify({ documents: { decisions: 'docs/decisions' } }));
  fx.write('src/Daoris.Web/src/bridge/call.ts', "export const call = (type) => getBridge().invoke('DAORIS.DRIVER', type, {});\n");
  fx.write('src/Daoris.Web/src/bridge/trees.ts', [
    "import { call } from './call';",
    '',
    'export const useDiff = (id: string) =>',
    "  call<{ a: string }>('SESSION_DIFF', { id });",
    '',
    'export function useEither(flag: boolean) {',
    "  return call(flag ? 'LAND' : 'HANDOFF');",
    '}',
    '',
    "export const useChange = () => useDriverChange('FORGET');",
    '',
    'function useDriverChange<T>(type: string) {',
    '  return useMutation({ mutationFn: (body: T) => call(type, body) });',
    '}',
    '',
  ].join('\n'));
  fx.write('src/Daoris.Web/src/bridge/log.ts', "export const logEvent = (bridge) => bridge.invoke('DAORIS.LOG', 'EVENT', {});\n");
  fx.write('src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs', [
    'public sealed partial class DriverModule',
    '{',
    '    public override string ModuleName => "DAORIS.DRIVER";',
    '}',
    '',
  ].join('\n'));
  fx.write('src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.Trees.cs', [
    'namespace Daoris.Desktop.Modules;',
    '',
    'public sealed partial class DriverModule',
    '{',
    '    [DriverRoute("SESSION_DIFF")]',
    '    private async Task<object?> DiffAsync(IpcRequest request, CancellationToken cancellationToken)',
    '    {',
    '        return null;',
    '    }',
    '',
    '    [DriverRoute("LAND")]',
    '    [DriverRoute("HANDOFF")]',
    '    private Task<object?> LandAsync(IpcRequest request, CancellationToken cancellationToken) => Task.FromResult<object?>(null);',
    '}',
    '',
  ].join('\n'));
  fx.write('src/Daoris.Desktop/Daoris.Desktop.Modules/LogModule.cs', [
    'public sealed class LogModule',
    '{',
    '    public override string ModuleName => "DAORIS.LOG";',
    '',
    '    public Task<object?> HandleAsync(IpcRequest request)',
    '    {',
    '        switch (request.Type)',
    '        {',
    '            case "EVENT":',
    '                return null;',
    '            case "LINES":',
    '                return null;',
    '        }',
    '    }',
    '}',
    '',
  ].join('\n'));
  fx.write('docs/decisions/D1.md', [
    '## D1 — The first decision, which is long enough to show (2026-08-04)',
    '',
    'Body.',
    '',
    '**Built 2026-08-05 (TASK1): the first build.** More words.',
    '',
    '```md',
    '**Amended 2026-08-06: inside a fence**',
    '```',
    '',
    '*Amended by D2 (TASK2, 2026-08-07): a later reading.* And more.',
    '',
    '**As built**, the undated form.',
    '',
  ].join('\n'));
  fx.write('docs/decisions/D2.md', '## D2 — The second (2026-08-07)\n\n*Decided 2026-08-07, in its own first paragraph.*\n\nBody.\n');
  fx.write('src/Daoris.Web/src/locales/en/nav.json', JSON.stringify({ 'nav.quests': 'Quests', 'nav.map': 'Map' }, null, 2));
  fx.write('src/Daoris.Web/src/locales/zh/nav.json', JSON.stringify({ 'nav.quests': '任务', 'nav.map': '地图' }, null, 2));
  fx.write('src/Daoris.Web/src/locales/en/work.json', JSON.stringify({ 'work.list.a': 'A', 'work.list.b': 'B', 'work.list.c': 'C', 'work.head': 'H' }, null, 2));
  fx.write('src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/GitFixture.cs', [
    'namespace Daoris.Driver.Tests;',
    '',
    'internal static class GitFixture',
    '{',
    '    public static string Init(string root)',
    '    {',
    '        return root;',
    '    }',
    '',
    '    private static void Hidden() { }',
    '}',
    '',
  ].join('\n'));
  fx.write('src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/TickTests.cs', [
    'public sealed class TickTests',
    '{',
    '    internal sealed class ParkStandIn : IAsyncDisposable',
    '    {',
    '    }',
    '',
    '    private sealed record Row(int A);',
    '}',
    '',
  ].join('\n'));
  fx.write('src/Daoris.Cli/test/_fixture.ts', 'export function makeFixture(name: string) {\n  return name;\n}\n');
  return fx;
}

const FIXTURE_FILES = [
  'daoris.json',
  'docs/decisions/D1.md',
  'docs/decisions/D2.md',
  'src/Daoris.Cli/test/_fixture.ts',
  'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/GitFixture.cs',
  'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/TickTests.cs',
  'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.Trees.cs',
  'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.cs',
  'src/Daoris.Desktop/Daoris.Desktop.Modules/LogModule.cs',
  'src/Daoris.Web/src/bridge/call.ts',
  'src/Daoris.Web/src/bridge/log.ts',
  'src/Daoris.Web/src/bridge/trees.ts',
  'src/Daoris.Web/src/locales/en/nav.json',
  'src/Daoris.Web/src/locales/en/work.json',
  'src/Daoris.Web/src/locales/zh/nav.json',
];

test('the bridge routes name each route, the handler that answers it and the bridge function that sends it', () => {
  const fx = fixtureRepository('orient-index-routes');
  const routes = tool.planIndex(fx.root, FIXTURE_FILES).get('docs/index/routes.md')!;
  for (const row of [
    '| `SESSION_DIFF` | `DriverModule.Trees.cs:6` DiffAsync | `bridge/trees.ts:4` useDiff |',
    '| `LAND` | `DriverModule.Trees.cs:13` LandAsync | `bridge/trees.ts:7` useEither |',
    '| `HANDOFF` | `DriverModule.Trees.cs:13` LandAsync | `bridge/trees.ts:7` useEither |',
    // A route the page sends and no handler answers, and one answered and sent by nothing, each say so.
    '| `FORGET` | — | `bridge/trees.ts:10` useChange |',
    '| `EVENT` | `LogModule.cs:9` | `bridge/log.ts:1` logEvent |',
    '| `LINES` | `LogModule.cs:11` | — |',
  ]) assert.ok(routes.includes(row), `routes.md lacks ${row}\n${routes}`);
  assert.match(routes, /## DAORIS\.DRIVER \(4\)/);
  assert.match(routes, /## DAORIS\.LOG \(2\)/);
  fx.cleanup();
});

test("the bridge's first argument is read past its type arguments", () => {
  const text = "call<{ a: Map<string, number> }>(flag ? 'A' : 'B', { x: (1, 2) })";
  const argument = tool.firstArgument(text, 'call'.length)!;
  assert.equal(argument.text, "flag ? 'A' : 'B'");
  assert.equal(text.slice(argument.start, argument.start + argument.text.length), argument.text);
  assert.equal(tool.firstArgument('call = 1', 'call'.length), null);
});

test('a helper that forwards its route is found, and so is a helper of a helper, so no list can miss one', () => {
  const fx = makeFixture('orient-index-helpers');
  fx.write('src/Daoris.Web/src/bridge/remotes.ts', [
    'const callRemotes = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>',
    "  getBridge().invoke<TData>('DAORIS.REMOTES', type, payload ? { payload } : {});",
    '',
    "export const useRemotes = () => callRemotes<Wiring>('STATE');",
    '',
    'const useWiringChange = <T,>(type: string) => useMutation({ mutationFn: (body: T) => callRemotes(type, body) });',
    '',
    "export const useUnwire = () => useWiringChange<{ workspace: string }>('REMOVE');",
    '',
  ].join('\n'));
  const routes = tool.planIndex(fx.root, ['src/Daoris.Web/src/bridge/remotes.ts']).get('docs/index/routes.md')!;
  assert.ok(routes.includes('| `STATE` | — | `bridge/remotes.ts:4` useRemotes |'), routes);
  assert.ok(routes.includes('| `REMOVE` | — | `bridge/remotes.ts:8` useUnwire |'), routes);
  fx.cleanup();
});

test("the decisions digest gives each decision's title with its entry's lines, then each dated note with its lines", () => {
  const fx = fixtureRepository('orient-index-decisions');
  const digest = tool.planIndex(fx.root, FIXTURE_FILES).get('docs/index/decisions.md')!;
  const lines = digest.split('\n');
  const rows = lines.filter((line) => /^- D\d+:/.test(line));
  assert.deepEqual(rows, [
    // The entry runs to the line before its first note; a note in a fence is text, not a note, and neither is the
    // entry's own dated first paragraph.
    '- D1:1-3 (2026-08-04) The first decision, which is long enough to show · 3 notes',
    '- D2:1-5 (2026-08-07) The second',
    '- D1:5-9 Built 2026-08-05 (TASK1): the first build.',
    '- D1:11-11 Amended by D2 (TASK2, 2026-08-07): a later reading.',
    '- D1:13-13 As built',
  ]);
  // The opening says where the titles and the notes are, by the digest's own lines.
  const [, titlesFrom, titlesTo, notesFrom] = /here lines (\d+)-(\d+)\); then each dated note, with its lines \(from line (\d+)\)/.exec(digest)!.map(Number);
  assert.equal(lines[titlesFrom! - 1], rows[0]);
  assert.equal(lines[titlesTo! - 1], rows[1]);
  assert.equal(lines[notesFrom! - 1], rows[2]);
  fx.cleanup();
});

test("a note's label is its emphasised opening, dated or in the record's own forms, and a sentence is not one", () => {
  assert.equal(tool.noteLabel('**LAND3, built 2026-10-04: a landing tidies every branch.** On the owner\'s ask.'), 'LAND3, built 2026-10-04: a landing tidies every branch.');
  assert.equal(tool.noteLabel('*Amended by LEFT1 (2026-09-30), from the owner. **A look fetches four** at once.* More.'),
    'Amended by LEFT1 (2026-09-30), from the owner. **A look fetches four** at once.');
  assert.equal(tool.noteLabel('**Built**, as the design says.'), 'Built');
  assert.equal(tool.noteLabel('**Proven without the rehearsal**, by a unit test.'), null);
  assert.equal(tool.noteLabel('Plain text 2026-10-01.'), null);
  // A long label is cut at a word, so a row stays one line.
  const long = tool.noteLabel(`**Built 2026-10-01 (DEV3): ${'a word '.repeat(30)}**`)!;
  assert.ok(long.length <= 81 && long.endsWith('…'), long);
});

test('the catalogue areas, the test fixtures and the files of the index come from the tree', () => {
  const fx = fixtureRepository('orient-index-sections');
  const planned = tool.planIndex(fx.root, FIXTURE_FILES);
  assert.deepEqual([...planned.keys()].sort(), [
    'docs/index/README.md', 'docs/index/catalogues.md', 'docs/index/decisions.md', 'docs/index/fixtures.md',
    'docs/index/routes.md', 'docs/index/verbs.md',
  ]);
  const catalogues = planned.get('docs/index/catalogues.md')!;
  assert.ok(catalogues.includes('| `nav` | 2 | en, zh |'), catalogues);
  assert.ok(catalogues.includes('| `work` | 4 | en | `list` 3 |'), catalogues);

  const fixtures = planned.get('docs/index/fixtures.md')!;
  assert.ok(fixtures.includes('`GitFixture.cs:3` class GitFixture: Init 5'), fixtures);
  assert.doesNotMatch(fixtures, /Hidden/, 'a private member is the fixture\'s own');
  assert.ok(fixtures.includes('`TickTests.cs:3` class ParkStandIn'), fixtures);
  assert.doesNotMatch(fixtures, /Row/, 'a private type with no fixture name is the test\'s own');
  assert.ok(fixtures.includes('`_fixture.ts`: makeFixture 1'), fixtures);
  // Every file the index writes is a lookup table read in one go, and starts by saying it is generated.
  for (const [path, text] of planned) assert.match(text, /never edit by hand/, path);
  fx.cleanup();
});

test('a file over the size is outlined beside the index, measured with LF endings so a CRLF checkout agrees', () => {
  const fx = makeFixture('orient-index-large');
  const method = (n: number) => `    public int M${n}(int x)\r\n    {\r\n        return x + ${n};\r\n    }\r\n`;
  let body = 'public sealed class Big\r\n{\r\n';
  for (let n = 0; Buffer.byteLength(body.replace(/\r\n/g, '\n')) < tool.LARGE + 100; n += 1) body += method(n);
  fx.write('src/Big.cs', `${body}}\r\n`);
  // CRLF puts this one over the size on disk; with LF endings it is under it.
  let small = 'public sealed class Small\r\n{\r\n';
  for (let n = 0; Buffer.byteLength(`${small}x`.replace(/\r\n/g, '\n')) < tool.LARGE - 400; n += 1) small += method(n);
  fx.write('src/Small.cs', `${small}}\r\n`);
  assert.ok(readFileSync(join(fx.root, 'src/Small.cs')).length > tool.LARGE, 'the fixture is over the size on disk');

  const planned = tool.planIndex(fx.root, ['src/Big.cs', 'src/Small.cs']);
  assert.ok(planned.has('docs/index/outlines/src/Big.cs.md'));
  assert.ok(!planned.has('docs/index/outlines/src/Small.cs.md'));
  const outline = planned.get('docs/index/outlines/src/Big.cs.md')!;
  assert.match(outline, /^- 1-\d+ class Big$/m);
  assert.match(outline, /^ {2}- 3-6 M0\(\)$/m);
  assert.match(planned.get('docs/index/README.md')!, /`src\/Big\.cs` \| \d+ \| \d+ \|/);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// The check: the committed index is what the tree says, or the gate names each file

test('the check names a missing, stale or stray file, and the write leaves nothing for it to name', () => {
  const fx = fixtureRepository('orient-index-staleness');
  const missing = tool.staleness(fx.root, tool.planIndex(fx.root, FIXTURE_FILES));
  assert.deepEqual(missing.map(({ path, fact }) => `${path}: ${fact}`), [
    'docs/index/README.md: missing', 'docs/index/catalogues.md: missing', 'docs/index/decisions.md: missing',
    'docs/index/fixtures.md: missing', 'docs/index/routes.md: missing', 'docs/index/verbs.md: missing',
  ]);
  tool.applyIndex(fx.root, tool.planIndex(fx.root, FIXTURE_FILES));
  assert.deepEqual(tool.staleness(fx.root, tool.planIndex(fx.root, FIXTURE_FILES)), []);

  // A route moves two lines down: the routes file no longer says where it is. A file written by hand is not the tool's.
  const trees = join(fx.root, 'src/Daoris.Desktop/Daoris.Desktop.Modules/DriverModule.Trees.cs');
  writeFileSync(trees, readFileSync(trees, 'utf8').replace('{\n    [DriverRoute("SESSION_DIFF")]', '{\n    // one\n    // two\n    [DriverRoute("SESSION_DIFF")]'));
  fx.write('docs/index/notes.md', 'written by hand\n');
  assert.deepEqual(tool.staleness(fx.root, tool.planIndex(fx.root, FIXTURE_FILES)).map(({ path, fact }) => `${path}: ${fact}`),
    ['docs/index/notes.md: not generated', 'docs/index/routes.md: stale']);

  tool.applyIndex(fx.root, tool.planIndex(fx.root, FIXTURE_FILES));
  assert.deepEqual(tool.staleness(fx.root, tool.planIndex(fx.root, FIXTURE_FILES)), []);
  assert.ok(!fx.exists('docs/index/notes.md'), 'a file the tool does not generate is removed');
  fx.cleanup();
});

test('--check exits 1 naming each stale file and the command that writes it, and 0 once it is written', () => {
  const fx = fixtureRepository('orient-index-check');
  assert.equal(spawnSync('git', ['init', '-q'], { cwd: fx.root }).status, 0);
  const run = (...args: string[]) => spawnSync(process.execPath, [toolPath, '--root', fx.root, ...args], { encoding: 'utf8' });

  const missing = run('--check');
  assert.equal(missing.status, 1, missing.stdout + missing.stderr);
  assert.match(missing.stderr, /docs\/index\/routes\.md: missing/);
  assert.match(missing.stderr, /node tools\/orient-index\.mjs writes it again/);

  const written = run();
  assert.equal(written.status, 0, written.stderr);
  const fresh = run('--check');
  assert.equal(fresh.status, 0, fresh.stderr);
  assert.match(fresh.stdout, /is fresh, 6 files/);
  assert.equal(run('--chek').status, 2, 'a word it does not know is a usage error');
  fx.cleanup();
});

test('the files are what git would stage, so an ignored file is not read, and a folder that is not a work tree is refused', () => {
  const fx = makeFixture('orient-index-files');
  fx.write('.gitignore', 'ignored/\n');
  fx.write('kept.cs', 'public class Kept { }\n');
  fx.write('ignored/skipped.cs', 'public class Skipped { }\n');
  assert.throws(() => tool.listFiles(fx.root), /not the top of a git work tree/);
  assert.equal(spawnSync('git', ['init', '-q'], { cwd: fx.root }).status, 0);
  assert.deepEqual(tool.listFiles(fx.root), ['.gitignore', 'kept.cs']);
  const refused = spawnSync(process.execPath, [toolPath, '--root', join(fx.root, 'ignored'), '--check'], { encoding: 'utf8' });
  assert.equal(refused.status, 2, refused.stderr);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// This repository: the readers still find what they were written for

test("this repository's index finds the routes, the verbs, the areas, the fixtures and every decision", () => {
  const planned = tool.planIndex(repoRoot);
  const routes = planned.get('docs/index/routes.md')!;
  const driver = /## DAORIS\.DRIVER \((\d+)\)/.exec(routes);
  assert.ok(driver && Number(driver[1]) > 90, 'the scan found the driver module\'s routes');
  const driverRows = routes.slice(routes.indexOf('## DAORIS.DRIVER')).split('\n## ')[0]!.split('\n').filter((line) => line.startsWith('| `'));
  assert.deepEqual(driverRows.filter((row) => row.split(' | ')[1] === '—'), [], 'every route the page sends the driver has its handler');
  assert.match(routes, /\| GET \/api\/status \| `Program\.cs:\d+` \|/);

  const verbs = planned.get('docs/index/verbs.md')!;
  for (const verb of ['`drive`', '`sessions`', '`trees`', '`check`', '`sync`', '`map`', '`shot`', '`verify`']) assert.ok(verbs.includes(verb), `verbs.md lacks ${verb}`);

  const areas = planned.get('docs/index/catalogues.md')!.split('\n').filter((line) => /^\| `[a-zA-Z]/.test(line));
  assert.ok(areas.length >= 70, `${areas.length} catalogue areas`);

  const fixtures = planned.get('docs/index/fixtures.md')!;
  for (const name of ['GitFixture', 'LandedFixture', 'DaorisHost', 'shellHarness', 'makeFixture']) assert.ok(fixtures.includes(name), `fixtures.md lacks ${name}`);

  const digest = planned.get('docs/index/decisions.md')!;
  const decisions = readFileSync(join(repoRoot, 'docs/decisions/D134.md'), 'utf8');
  assert.match(digest, /^- D134:1-\d+ \(2026-10-03\) /m, 'D134 has its row');
  const doc8a = decisions.split('\n').findIndex((line) => line.startsWith('**Built 2026-10-03 (DOC8a)')) + 1;
  assert.match(digest, new RegExp(`^- D134:${doc8a}-\\d+ Built 2026-10-03 \\(DOC8a\\)`, 'm'), 'and its DOC8a note, at its line');
  const files = readdirSync(join(repoRoot, 'docs/decisions')).filter((name) => /^D\d+\.md$/.test(name));
  assert.equal(digest.split('\n').filter((line) => /^- D\d+:1-/.test(line)).length, files.length, 'every decision file has its title row');
});

test("every large file in this repository is outlined, and the scans of their strings and comments end clean", () => {
  const files = tool.listFiles(repoRoot);
  const planned = tool.planIndex(repoRoot, files);
  const outlines = [...planned.keys()].filter((path) => path.startsWith('docs/index/outlines/'));
  assert.ok(outlines.length >= 40, `${outlines.length} outlines`);
  const scanned: string[] = [];
  const unclean: string[] = [];
  for (const path of outlines) {
    const source = path.slice('docs/index/outlines/'.length, -'.md'.length);
    const text = planned.get(path)!;
    assert.match(text, /^\s*- \d+-\d+ /m, `${source}: the outline found no declaration`);
    const language = /\.cs$/.test(source) ? 'cs' : /\.[cm]?[jt]sx?$/.test(source) ? 'ts' : null;
    if (!language) continue;
    scanned.push(source);
    const lines = readFileSync(join(repoRoot, source), 'utf8').replace(/\r\n/g, '\n').split('\n');
    if (!tool.continued(lines, language).clean) unclean.push(source);
  }
  // A scan that misreads one file falls back to reading every line as code, so one odd file in another lane is the
  // tool's to learn and no reason to fail that lane's branch; a scanner that broke misreads many.
  assert.ok(unclean.length <= Math.floor(scanned.length / 20), `the scans did not end clean in ${unclean.join(', ')}`);
});
