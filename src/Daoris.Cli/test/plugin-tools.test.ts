import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { DAORIS_OWN_TOOLS, MANIFEST, commandPlugin, pluginsRoot, readManifest, readPlugins } from '../src/plugins.ts';
import { TOOLS } from '../src/tools.ts';
import { driverRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * PLUGTOOL1a, D150 point 7 (the UX6 design §7.2) — a plugin's tools, the CLI's half of a twin with the driver's
 * `PluginTools.cs`: the manifest's `tools`, the programs its process runs, read by the catalogue by one table.
 * 🔴 `PluginToolsTests.cs`'s theory is parsed below and held to `TOOL_ROWS`, cell for cell and in order, so a row changed
 * on one side alone fails `npm run verify`.
 *
 * 🔴 A problem in `tools` never refuses the plugin: it is that tool's sentence, and the plugin still contributes. This side
 * reads and lists; it starts nothing, so finding a tool and running its checks are the driver's, at a trial.
 */

const DRIVER_TESTS = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'PluginToolsTests.cs',
);

type Row = [string, string | null, number, string | null, string | null, string | null, string | null, string | null, number, string | null];

/**
 * [case, the manifest's `tools` as JSON or null for none, how many tools are read, then the last one read: its id, its
 * kind (`own`, `known`, `other`), its name, the program found for it, its range as written, how many checks it carries,
 * and a fragment of its problem, which with no tool read is the field's]. The twin is
 * `PluginToolsTests.The_tools_read_as_the_cli_reads_them`.
 */
const TOOL_ROWS: Row[] = [
  ['no tools', null, 0, null, null, null, null, null, 0, null],
  ['null is none', 'null', 0, null, null, null, null, null, 0, null],
  ['none declared', '[]', 0, null, null, null, null, null, 0, null],
  ['not an array', '{ "id": "az" }', 0, null, null, null, null, null, 0, '`tools` must be an array of the tools the plugin runs'],
  ["Daoris's own", '[{ "id": "git" }]', 1, 'git', 'own', 'Git', null, null, 0, null],
  ['one Daoris knows and does not run', '[{ "id": "az", "versions": ">=2.60", "for": "Pushes the branch and opens its pull request.", "ready": [{ "run": ["az", "account", "show", "--output", "none"], "says": "Signed in", "fix": "az login" }] }]', 1, 'az', 'known', 'Azure CLI', null, '>=2.60', 1, null],
  ['one Daoris does not know', '[{ "id": "terraform" }]', 1, 'terraform', 'other', 'terraform', 'terraform', null, 0, null],
  ['one Daoris does not know, named', '[{ "id": "tf", "name": "Terraform", "command": "terraform", "versionArguments": ["version"] }]', 1, 'tf', 'other', 'Terraform', 'terraform', null, 0, null],
  ['a range with its top', '[{ "id": "az", "versions": ">=2.60 <3" }]', 1, 'az', 'known', 'Azure CLI', null, '>=2.60 <3', 0, null],
  ['one exact version', '[{ "id": "node", "versions": "22.11.0" }]', 1, 'node', 'own', 'Node.js', null, '22.11.0', 0, null],
  ['spaces between and around', '[{ "id": "az", "versions": " >=2.60  <3 " }]', 1, 'az', 'known', 'Azure CLI', null, ' >=2.60  <3 ', 0, null],
  ['four checks', '[{ "id": "gh", "ready": [{ "run": ["gh"], "says": "a" }, { "run": ["gh"], "says": "b" }, { "run": ["gh"], "says": "c" }, { "run": ["gh"], "says": "d" }] }]', 1, 'gh', 'known', 'GitHub CLI', null, null, 4, null],
  ['an empty argument in a check', '[{ "id": "gh", "ready": [{ "run": ["gh", "auth", "status", "--hostname", ""], "says": "Signed in" }] }]', 1, 'gh', 'known', 'GitHub CLI', null, null, 1, null],
  ['null fields are none', '[{ "id": "az", "name": null, "command": null, "versionArguments": null, "versions": null, "for": null, "ready": null }]', 1, 'az', 'known', 'Azure CLI', null, null, 0, null],
  ['a field it does not know is passed over', '[{ "id": "az", "url": "https://example.test/az.zip", "sha256": "00" }]', 1, 'az', 'known', 'Azure CLI', null, null, 0, null],
  ['a broken tool leaves the next one read', '[{ "id": 7 }, { "id": "git" }]', 2, 'git', 'own', 'Git', null, null, 0, null],
  ['not an object', '["az"]', 1, null, null, null, null, null, 0, 'tool 1 in `tools` is not an object with an `id`'],
  ['no id', '[{ "versions": ">=1" }]', 1, null, null, null, null, null, 0, "tool 1 in `tools` needs an `id`: a tool's name in lowercase, like `az`"],
  ['an id that is not text', '[{ "id": 7 }]', 1, null, null, null, null, null, 0, 'tool 1 in `tools` needs an `id`'],
  ['a blank id', '[{ "id": " " }]', 1, null, null, null, null, null, 0, 'tool 1 in `tools` needs an `id`'],
  ['an id in capitals', '[{ "id": "Az" }]', 1, null, null, null, null, null, 0, 'tool 1 in `tools` has the `id` `Az`, which is not one: lowercase letters, digits, dots and dashes'],
  ['the second of two', '[{ "id": "git" }, { "id": "a z" }]', 2, null, null, null, null, null, 0, 'tool 2 in `tools` has the `id` `a z`'],
  ['an id twice', '[{ "id": "az" }, { "id": "az", "versions": ">=2" }]', 2, 'az', null, null, null, null, 0, 'tool `az` is declared twice in `tools`; the first is read'],
  ["a known tool's command", '[{ "id": "gh", "command": "gh2" }]', 1, 'gh', null, null, null, null, 0, "tool `gh` is GitHub CLI, which Daoris knows: its name, its file and how its version is asked are Daoris's, so `command` is not a plugin's to declare"],
  ["a known tool's name", '[{ "id": "git", "name": "Git" }]', 1, 'git', null, null, null, null, 0, "so `name` is not a plugin's to declare"],
  ["a known tool's version question", '[{ "id": "node", "versionArguments": ["-v"] }]', 1, 'node', null, null, null, null, 0, "so `versionArguments` is not a plugin's to declare"],
  ['a name that is not text', '[{ "id": "tf", "name": 5 }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `name` must be text: what a person calls it"],
  ['a blank name', '[{ "id": "tf", "name": "" }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `name` must be text"],
  ['a command with a folder', '[{ "id": "tf", "command": "bin/terraform" }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program found on the PATH, with no folder in it"],
  ['a command that is a whole path', '[{ "id": "tf", "command": "/usr/bin/terraform" }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program"],
  ['a command on a drive', '[{ "id": "tf", "command": "C:terraform" }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program"],
  ['a blank command', '[{ "id": "tf", "command": " " }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `command` must be the name of a program"],
  ['a version question that is not a list', '[{ "id": "tf", "versionArguments": "version" }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `versionArguments` must be an array of text: what prints its version"],
  ['a version question holding a number', '[{ "id": "tf", "versionArguments": ["version", 2] }]', 1, 'tf', null, null, null, null, 0, "tool `tf`'s `versionArguments` must be an array of text"],
  ['not a range', '[{ "id": "az", "versions": ">2.60" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range: `>=2.60`, `>=2.60 <3`, or one exact version"],
  ['a word', '[{ "id": "az", "versions": "latest" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['a v before it', '[{ "id": "az", "versions": "v2.60" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['five numbers', '[{ "id": "az", "versions": "1.2.3.4.5" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['its top first', '[{ "id": "az", "versions": "<3 >=2.60" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['a top alone', '[{ "id": "az", "versions": "<3" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['blank', '[{ "id": "az", "versions": "  " }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['versions that are not text', '[{ "id": "az", "versions": 2.6 }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` must be a range"],
  ['a range that holds nothing', '[{ "id": "az", "versions": ">=3 <2" }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `versions` `>=3 <2` holds no version: `<2` is not above `>=3`"],
  ['a top no higher than its floor', '[{ "id": "az", "versions": ">=2.60 <2.60.0" }]', 1, 'az', null, null, null, null, 0, 'holds no version: `<2.60.0` is not above `>=2.60`'],
  ['a blank for', '[{ "id": "az", "for": " " }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `for` must be one sentence: why the plugin runs it"],
  ['for that is not text', '[{ "id": "az", "for": ["pushes"] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `for` must be one sentence"],
  ['ready that is not a list', '[{ "id": "az", "ready": { "run": ["az"], "says": "a" } }]', 1, 'az', null, null, null, null, 0, "tool `az`'s `ready` must be an array of checks"],
  ['five checks', '[{ "id": "gh", "ready": [{ "run": ["gh"], "says": "a" }, { "run": ["gh"], "says": "b" }, { "run": ["gh"], "says": "c" }, { "run": ["gh"], "says": "d" }, { "run": ["gh"], "says": "e" }] }]', 1, 'gh', null, null, null, null, 0, 'tool `gh` has 5 checks in `ready`, and a tool has at most four'],
  ['a check that is not an object', '[{ "id": "az", "ready": ["az login"] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`: the command whose exit 0 means ready, as an array"],
  ['a check with no run', '[{ "id": "az", "ready": [{ "says": "Signed in" }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`"],
  ['a run that is empty', '[{ "id": "az", "ready": [{ "run": [], "says": "Signed in" }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`"],
  ['a run that is a line', '[{ "id": "az", "ready": [{ "run": "az account show", "says": "Signed in" }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`"],
  ['a run whose first word is blank', '[{ "id": "az", "ready": [{ "run": [" ", "account"], "says": "Signed in" }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`"],
  ['a run holding a number', '[{ "id": "az", "ready": [{ "run": ["az", 7], "says": "Signed in" }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs a `run`"],
  ['a check with no says', '[{ "id": "az", "ready": [{ "run": ["az", "account", "show"] }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 needs `says`: what it means when it passes"],
  ['a fix that is not text', '[{ "id": "az", "ready": [{ "run": ["az", "account", "show"], "says": "Signed in", "fix": ["az", "login"] }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 1 has a `fix` that is not text: the command a person runs when it does not pass"],
  ['the second check', '[{ "id": "az", "ready": [{ "run": ["az", "version"], "says": "Runs" }, { "run": ["az", "account", "show"], "says": " " }] }]', 1, 'az', null, null, null, null, 0, "tool `az`'s check 2 needs `says`"],
];

function plugin(home: string, id: string, tools: string | null, extra = ''): string {
  const folder = join(pluginsRoot(home), id);
  mkdirSync(folder, { recursive: true });
  const field = tools === null ? '' : `, "tools": ${tools}`;
  writeFileSync(join(folder, MANIFEST),
    `{ "id": "${id}", "name": "Acme lands", "hooks": { "command": ["node", "lands.mjs"], "points": ["work/land"] }${field}${extra} }`);
  return folder;
}

test('the driver’s tools table is this table, row for row and in this order', () => {
  assert.deepEqual(driverRows(readFileSync(DRIVER_TESTS, 'utf8'), 'The_tools_read_as_the_cli_reads_them'), TOOL_ROWS);
});

test('the tools read as the driver reads them: each tool or its first problem, never the plugin’s', () => {
  TOOL_ROWS.forEach(([name, tools, count, id, kind, toolName, command, versions, checks, problem], at) => {
    const fx = makeFixture(`pluginTools-${at}`);
    plugin(fx.root, 'acme.lands', tools);

    const catalog = readPlugins(fx.root);
    const [entry] = catalog.plugins;
    const read = entry!.manifest.tools;

    // 🔴 Never the plugin's problem: it is sound whatever its tools say.
    assert.equal(entry!.problem, null, `${name}: ${entry!.problem}`);
    assert.equal(catalog.contributing.length, 1, name);
    assert.equal(read.length, count, `${name}: ${read.length} tools read`);

    const last = read.length > 0 ? read[read.length - 1]! : null;
    assert.equal(last?.id ?? null, id, `${name}: id`);
    assert.equal(last?.kind ?? null, kind, `${name}: kind`);
    assert.equal(last?.name ?? null, toolName, `${name}: name`);
    assert.equal(last?.command ?? null, command, `${name}: command`);
    assert.equal(last?.versions ?? null, versions, `${name}: versions`);
    assert.equal(last?.ready.length ?? 0, checks, `${name}: checks`);

    const said = last === null ? entry!.manifest.toolsProblem : last.problem;
    if (problem === null) assert.equal(said, null, `${name}: ${said}`);
    else assert.ok(said?.includes(problem), `${name}: ${said}`);
    if (last !== null) assert.equal(entry!.manifest.toolsProblem, null, name);
    fx.cleanup();
  });
});

test('the design’s example is read whole: every field a person is shown, as written', () => {
  const fx = makeFixture('pluginTools-example');
  plugin(fx.root, 'acme.lands', `[
    { "id": "node", "versions": ">=22" },
    { "id": "git", "versions": ">=2.29" },
    {
      "id": "az",
      "versions": ">=2.60",
      "for": "Pushes the branch and opens its pull request.",
      "ready": [
        { "run": ["az", "extension", "show", "--name", "azure-devops", "--output", "none"],
          "says": "Its devops extension is added", "fix": "az extension add --name azure-devops" },
        { "run": ["az", "account", "show", "--output", "none"], "says": "Signed in" }
      ]
    },
    { "id": "tf", "name": "Terraform", "command": "terraform", "versionArguments": ["version", "-json"] }
  ]`);

  const tools = readPlugins(fx.root).plugins[0]!.manifest.tools;

  assert.deepEqual(tools.map((tool) => tool.id), ['node', 'git', 'az', 'tf']);
  assert.deepEqual(tools.map((tool) => tool.kind), ['own', 'own', 'known', 'other']);
  assert.ok(tools.every((tool) => tool.problem === null));

  const az = tools[2]!;
  assert.equal(az.name, 'Azure CLI');
  assert.equal(az.command, null);
  assert.equal(az.versionArguments, null);
  assert.equal(az.for, 'Pushes the branch and opens its pull request.');
  assert.deepEqual(az.ready[0]!.run, ['az', 'extension', 'show', '--name', 'azure-devops', '--output', 'none']);
  assert.equal(az.ready[0]!.says, 'Its devops extension is added');
  assert.equal(az.ready[0]!.fix, 'az extension add --name azure-devops');
  assert.equal(az.ready[1]!.fix, null);

  const tf = tools[3]!;
  assert.equal(tf.name, 'Terraform');
  assert.equal(tf.command, 'terraform');
  assert.deepEqual(tf.versionArguments, ['version', '-json']);
  assert.equal(tf.versions, null);
  assert.deepEqual(tf.ready, []);
  fx.cleanup();
});

test('a refused plugin takes no tools, and its manifest read as written keeps them', () => {
  const fx = makeFixture('pluginTools-refused');
  const folder = join(pluginsRoot(fx.root), 'acme.later');
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, MANIFEST),
    '{ "id": "acme.later", "tools": [{ "id": "gh" }], "harnesses": [ { "name": "claude-code", "command": ["claude"] } ] }');

  const [refused] = readPlugins(fx.root).plugins;
  const written = readManifest('acme.later', folder, true);

  assert.notEqual(refused!.problem, null);
  assert.deepEqual(refused!.manifest.tools, []);
  assert.equal(written.problem, null);
  assert.deepEqual(written.manifest.tools.map((tool) => tool.id), ['gh']);
  fx.cleanup();
});

test('Daoris’s own tools are the three it runs itself; the other two it knows, a plugin declares', () => {
  assert.deepEqual(DAORIS_OWN_TOOLS, ['git', 'node', 'pwsh']);
  assert.ok(DAORIS_OWN_TOOLS.every((id) => TOOLS.some((tool) => tool.id === id)));
  assert.deepEqual(TOOLS.map((tool) => tool.id).filter((id) => !DAORIS_OWN_TOOLS.includes(id)), ['gh', 'az']);
});

test('plugin list names the tools a plugin runs, and says each tool’s problem without refusing it', () => {
  const fx = makeFixture('pluginTools-list');
  plugin(fx.root, 'acme.lands', '[{ "id": "git", "versions": ">=2.29" }, { "id": "az", "versions": ">=2.60" }, { "id": "Az" }]');
  plugin(fx.root, 'acme.plain', null);
  plugin(fx.root, 'acme.wrong', '"az"');

  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = fx.root;
  const lines: string[] = [];
  try {
    assert.equal(commandPlugin({ root: process.cwd(), argv: ['list'], write: (line) => lines.push(line), packageRoot: process.cwd() }), 0);
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }

  const out = lines.join('\n');
  assert.ok(out.includes('runs Git >=2.29, Azure CLI >=2.60'), out);
  assert.equal(out.match(/tool not read/g)?.length, 2, out);
  assert.ok(out.includes('tool not read: tool 3 in `tools` has the `id` `Az`'), out);
  assert.ok(out.includes('tool not read: `tools` must be an array'), out);
  assert.equal(out.match(/⚠/g), null, out);
  fx.cleanup();
});
