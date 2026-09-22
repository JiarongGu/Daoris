import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  API_VERSION, MANIFEST, RESERVED_HARNESSES, STATE_FILE, commandPlugin, dataFolder, disablePlugin,
  enablePlugin, pluginsRoot, readPluginState, readPlugins,
} from '../src/plugins.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * `daoris plugin` — the CLI's half of the plugin catalogue's TWIN CONTRACT (D64). The driver's
 * `Plugins.cs` reads the same folder by the same rules in another language; the four rules the module
 * header states are asserted in both, and they move together.
 */

function plugin(home: string, folder: string, manifest: string): string {
  const path = join(pluginsRoot(home), folder);
  mkdirSync(path, { recursive: true });
  writeFileSync(join(path, MANIFEST), manifest, 'utf8');
  return path;
}

function run(argv: string[], home: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = home;
  const lines: string[] = [];
  try {
    const code = commandPlugin({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

test('a home with no plugins folder is an empty catalogue, not a crash', () => {
  const fx = makeFixture('plugins-empty');
  assert.deepEqual(readPlugins(fx.root), { plugins: [], contributing: [] });
  fx.cleanup();
});

test('a manifest is read whole, and the plugin placeholder is its own folder', () => {
  const fx = makeFixture('plugins-manifest');
  const folder = plugin(fx.root, 'acme.agent', JSON.stringify({
    id: 'acme.agent', name: 'Acme agent', version: '1.2.0', description: 'An agent.',
    harnesses: [{
      name: 'acme-agent', command: ['${plugin}/agent.mjs', '--acp'], posture: 'edits',
      profileVariable: 'ACME_HOME', package: '@acme/agent', install: ['npm', 'install', '-g', '@acme/agent'],
      versionArguments: ['--version'],
    }],
    hooks: { command: ['node', '${plugin}/hooks.mjs'], points: ['quest/consider', 'session/ended'] },
  }));

  const [entry] = readPlugins(fx.root).plugins;
  assert.ok(entry);
  assert.equal(entry.manifest.id, 'acme.agent');
  assert.equal(entry.manifest.name, 'Acme agent');
  // Absent means 1: the field is how a plugin opts into saying something (PLUG1's rule).
  assert.equal(entry.manifest.apiVersion, 1);
  assert.equal(entry.enabled, true);
  assert.equal(entry.problem, null);
  assert.equal(entry.folder, folder);
  // What it keeps lives BESIDE the install, never inside it.
  assert.equal(entry.data, join(pluginsRoot(fx.root), '.data', 'acme.agent'));

  const [harness] = entry.manifest.harnesses;
  assert.ok(harness);
  // 🔴 A plugin cannot work out its own folder; the placeholder is the host telling it.
  assert.equal(harness.command[0], join(folder, 'agent.mjs'));
  assert.equal(harness.command[1], '--acp');
  assert.equal(harness.posture, 'edits');
  assert.equal(harness.profileVariable, 'ACME_HOME');
  assert.equal(entry.manifest.hooks?.command[1], join(folder, 'hooks.mjs'));
  assert.deepEqual(entry.manifest.hooks?.points, ['quest/consider', 'session/ended']);
  fx.cleanup();
});

// ——— Twin rule 1: the version is read before anything else.

test('a newer api version is refused naming both numbers, and nothing of it is taken', () => {
  const fx = makeFixture('plugins-future');
  plugin(fx.root, 'future', '{ "id": "future", "apiVersion": 99, "harnesses": [ { "name": "x", "command": ["x"] } ] }');

  const [entry] = readPlugins(fx.root).plugins;
  assert.match(entry!.problem!, /99/);
  assert.match(entry!.problem!, new RegExp(`${API_VERSION}`));
  assert.deepEqual(entry!.manifest.harnesses, []);
  assert.equal(entry!.manifest.hooks, null);
  fx.cleanup();
});

test('a non-integer api version is malformed rather than old', () => {
  const fx = makeFixture('plugins-odd');
  plugin(fx.root, 'odd', '{ "id": "odd", "apiVersion": "2" }');
  assert.match(readPlugins(fx.root).plugins[0]!.problem!, /apiVersion.*integer/);
  fx.cleanup();
});

// ——— Twin rule 2: a broken manifest is a named problem, never a crash.

test('a malformed manifest is a named problem under its folder name, and contributes nothing', () => {
  const fx = makeFixture('plugins-broken');
  plugin(fx.root, 'broken', '{ not json');
  plugin(fx.root, 'one', '{ "id": "two" }');
  plugin(fx.root, 'Bad Name', '{ "id": "Bad Name" }');
  plugin(fx.root, 'silent', '{ "id": "silent", "harnesses": [ { "name": "silent-agent" } ] }');
  mkdirSync(join(pluginsRoot(fx.root), 'notes'), { recursive: true });

  const catalog = readPlugins(fx.root);
  const by = (id: string) => catalog.plugins.find((p) => p.manifest.id === id)!;
  assert.match(by('broken').problem!, /plugin\.json/);
  assert.equal(by('broken').enabled, true);
  assert.match(by('two').problem!, /`two`.*`one`/);
  assert.match(by('Bad Name').problem!, /`id`/);
  assert.match(by('silent').problem!, /command/);
  // A folder with no manifest is not a plugin and is not listed.
  assert.equal(catalog.plugins.some((p) => p.manifest.id === 'notes'), false);
  assert.deepEqual(catalog.contributing, []);
  fx.cleanup();
});

// ——— Twin rule 3: a conflict is refused before anything loads, naming both sides.

test('a harness this build carries is refused naming both sides', () => {
  const fx = makeFixture('plugins-shadow');
  plugin(fx.root, 'shadow', '{ "id": "shadow", "harnesses": [ { "name": "claude-code", "command": ["evil"] } ] }');

  const [entry] = readPlugins(fx.root).plugins;
  assert.match(entry!.problem!, /claude-code/);
  assert.match(entry!.problem!, /this build/);
  assert.ok(RESERVED_HARNESSES.has('claude-code'));
  assert.ok(RESERVED_HARNESSES.has('acp-stub'));
  fx.cleanup();
});

test('two plugins declaring the same harness keep the first by id and refuse the second naming it', () => {
  const fx = makeFixture('plugins-twice');
  plugin(fx.root, 'b.two', '{ "id": "b.two", "harnesses": [ { "name": "shared", "command": ["two"] } ] }');
  plugin(fx.root, 'a.one', '{ "id": "a.one", "harnesses": [ { "name": "shared", "command": ["one"] } ] }');

  const catalog = readPlugins(fx.root);
  assert.equal(catalog.plugins[0]!.manifest.id, 'a.one');
  assert.equal(catalog.plugins[0]!.problem, null);
  assert.match(catalog.plugins[1]!.problem!, /a\.one/);
  assert.match(catalog.plugins[1]!.problem!, /shared/);
  assert.deepEqual(catalog.contributing.map((p) => p.manifest.id), ['a.one']);
  fx.cleanup();
});

// ——— Twin rule 4: disabled is a row, never a rename.

test('disabled is a row in plugins.json that stays one row per id', () => {
  const fx = makeFixture('plugins-state');
  plugin(fx.root, 'acme.agent', '{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["acme"] } ] }');

  assert.equal(readPlugins(fx.root).plugins[0]!.enabled, true);
  disablePlugin(fx.root, 'acme.agent');
  const off = readPlugins(fx.root).plugins[0]!;
  assert.equal(off.enabled, false);
  assert.equal(off.problem, null);
  assert.equal(existsSync(off.folder), true);
  assert.match(readFileSync(join(fx.root, STATE_FILE), 'utf8'), /acme\.agent/);

  enablePlugin(fx.root, 'acme.agent');
  assert.equal(readPlugins(fx.root).plugins[0]!.enabled, true);
  enablePlugin(fx.root, 'acme.agent');
  disablePlugin(fx.root, 'acme.agent');
  disablePlugin(fx.root, 'acme.agent');
  assert.deepEqual(readPluginState(fx.root).disabled, ['acme.agent']);
  fx.cleanup();
});

// ——— The command.

test('without a home every verb refuses naming DAORIS_HOME (D63)', () => {
  const saved = process.env.DAORIS_HOME;
  delete process.env.DAORIS_HOME;
  try {
    const error = captureError(() => commandPlugin({
      root: process.cwd(), argv: ['list'], write: () => {}, packageRoot: process.cwd(),
    }));
    assert.match(error.message, /DAORIS_HOME/);
  } finally {
    if (saved !== undefined) process.env.DAORIS_HOME = saved;
  }
});

test('list names every plugin, what it declares and speaks, and why a refused one contributes nothing', () => {
  const fx = makeFixture('plugins-list');
  plugin(fx.root, 'acme.agent', JSON.stringify({
    id: 'acme.agent', name: 'Acme agent', version: '1.0.0',
    harnesses: [{ name: 'acme-agent', command: ['acme'] }],
    hooks: { command: ['node', 'h.mjs'], points: ['quest/consider'] },
  }));
  plugin(fx.root, 'future', '{ "id": "future", "apiVersion": 99 }');
  disablePlugin(fx.root, 'acme.agent');

  const { code, out } = run(['list'], fx.root);
  assert.equal(code, 0);
  assert.match(out, /acme\.agent\s+Acme agent 1\.0\.0\s+\(off\)/);
  assert.match(out, /declares acme-agent; speaks on quest\/consider/);
  assert.match(out, /future[\s\S]*⚠ needs plugin API 99/);
  fx.cleanup();
});

test('add copies a folder in under its id, replacing wholesale and leaving the data folder alone', () => {
  const fx = makeFixture('plugins-add');
  const source = join(fx.root, 'somewhere', 'my-plugin-src');
  mkdirSync(source, { recursive: true });
  writeFileSync(join(source, MANIFEST), '{ "id": "acme.agent", "harnesses": [ { "name": "acme-agent", "command": ["${plugin}/a.mjs"] } ] }');
  writeFileSync(join(source, 'a.mjs'), '// v1');
  const home = join(fx.root, 'home');
  mkdirSync(join(dataFolder(home, 'acme.agent')), { recursive: true });
  writeFileSync(join(dataFolder(home, 'acme.agent'), 'kept.json'), '{}');

  const first = run(['add', source], home);
  assert.equal(first.code, 0, first.out);
  assert.match(first.out, /added plugin `acme\.agent`/);
  assert.match(first.out, /kept, untouched/);
  const installed = join(pluginsRoot(home), 'acme.agent');
  assert.equal(readFileSync(join(installed, 'a.mjs'), 'utf8'), '// v1');

  // A second add REPLACES: the stale file from the first install is gone.
  writeFileSync(join(installed, 'stale.txt'), 'from the previous install');
  writeFileSync(join(source, 'a.mjs'), '// v2');
  const second = run(['add', source], home);
  assert.match(second.out, /replaced plugin `acme\.agent`/);
  assert.equal(readFileSync(join(installed, 'a.mjs'), 'utf8'), '// v2');
  assert.equal(existsSync(join(installed, 'stale.txt')), false);
  assert.equal(existsSync(join(dataFolder(home, 'acme.agent'), 'kept.json')), true);

  // The catalogue reads it with the placeholder expanded to where it LANDED, not where it came from.
  assert.equal(readPlugins(home).plugins[0]!.manifest.harnesses[0]!.command[0], join(installed, 'a.mjs'));
  fx.cleanup();
});

test('add refuses a folder whose manifest is unsound, or that would shadow a harness this build carries', () => {
  const fx = makeFixture('plugins-add-refused');
  const bad = join(fx.root, 'bad');
  mkdirSync(bad, { recursive: true });
  writeFileSync(join(bad, MANIFEST), '{ "id": "bad", "apiVersion": 99 }');
  const shadow = join(fx.root, 'shadow');
  mkdirSync(shadow, { recursive: true });
  writeFileSync(join(shadow, MANIFEST), '{ "id": "shadow", "harnesses": [ { "name": "dsh", "command": ["x"] } ] }');
  const home = join(fx.root, 'home');

  for (const [source, why] of [[bad, /needs plugin API 99/], [shadow, /dsh.*this build/]] as const) {
    const saved = process.env.DAORIS_HOME;
    process.env.DAORIS_HOME = home;
    try {
      const error = captureError(() => commandPlugin({
        root: process.cwd(), argv: ['add', source], write: () => {}, packageRoot: process.cwd(),
      }));
      assert.match(error.message, why);
      assert.match(error.message, /Nothing was copied/);
    } finally {
      if (saved === undefined) delete process.env.DAORIS_HOME;
      else process.env.DAORIS_HOME = saved;
    }
  }
  assert.equal(existsSync(pluginsRoot(home)), false);
  fx.cleanup();
});

test('remove takes the install folder and names the data folder rather than deleting it', () => {
  const fx = makeFixture('plugins-remove');
  plugin(fx.root, 'acme.agent', '{ "id": "acme.agent" }');
  mkdirSync(dataFolder(fx.root, 'acme.agent'), { recursive: true });
  disablePlugin(fx.root, 'acme.agent');

  const { code, out } = run(['remove', 'acme.agent'], fx.root);
  assert.equal(code, 0);
  assert.match(out, /removed/);
  assert.match(out, /yours to delete/);
  assert.equal(existsSync(join(pluginsRoot(fx.root), 'acme.agent')), false);
  assert.equal(existsSync(dataFolder(fx.root, 'acme.agent')), true);
  // The disabled row goes with it, so a re-add starts on.
  assert.deepEqual(readPluginState(fx.root).disabled, []);

  // Removing what is not there is the end state asked for: exit 0, and it says so.
  assert.match(run(['remove', 'acme.agent'], fx.root).out, /nothing to remove/);
  fx.cleanup();
});

test('enable and disable edit the row, and an unknown id is refused naming the list', () => {
  const fx = makeFixture('plugins-switch');
  plugin(fx.root, 'acme.agent', '{ "id": "acme.agent" }');

  assert.match(run(['disable', 'acme.agent'], fx.root).out, /is off/);
  assert.deepEqual(readPluginState(fx.root).disabled, ['acme.agent']);
  assert.match(run(['enable', 'acme.agent'], fx.root).out, /is on/);
  assert.deepEqual(readPluginState(fx.root).disabled, []);

  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = fx.root;
  try {
    const error = captureError(() => commandPlugin({
      root: process.cwd(), argv: ['enable', 'nobody'], write: () => {}, packageRoot: process.cwd(),
    }));
    assert.match(error.message, /no plugin `nobody`/);
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
  fx.cleanup();
});
