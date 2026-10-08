import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  API_VERSION, KNOWLEDGE_SERVER, MANIFEST, STATE_FILE, commandPlugin, dataFolder, disablePlugin,
  enablePlugin, isPluginId, pluginsRoot, readPluginState, readPlugins, reservedHarnesses, resolvable,
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
  assert.ok(reservedHarnesses().has('claude-code'));
  assert.ok(reservedHarnesses().has('acp-stub'));
  fx.cleanup();
});

test('presence is asked of the file or PATH, never by running the command', () => {
  const fx = makeFixture('plugins-presence');
  const here = join(fx.root, 'agent.mjs');
  writeFileSync(here, '// would wait on stdin forever if it were run');
  assert.equal(resolvable(here), true);
  assert.equal(resolvable(join(fx.root, 'gone.mjs')), false);
  // `node` is on this machine's PATH — the tests are running on it.
  assert.equal(resolvable('node'), true);
  assert.equal(resolvable('no-such-program-daoris-ever-heard-of'), false);
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

/**
 * XAGENT1b2 (D155 point 4, the second-agent design §3.1): a declared harness may say what a person calls its tool and who
 * makes it, so a plugin's agent can count as another maker's. Each is the plugin's word, read trimmed; one that is not text,
 * or is blank, declares none and never refuses the plugin. Twin: `PluginCatalogTests`'
 * `A_declared_harness_may_say_its_product_and_maker_and_an_older_manifest_reads_as_before`, the same manifest read the same.
 */
test('a declared harness may say its product and maker, and an older manifest reads as before', () => {
  const fx = makeFixture('plugins-maker');
  plugin(fx.root, 'acme.agent', `{ "id": "acme.agent",
    "harnesses": [ { "name": "acme-agent", "command": ["acme"], "product": " Acme Agent ", "maker": "Acme" },
                   { "name": "older-agent", "command": ["older"] },
                   { "name": "odd-agent", "command": ["odd"], "product": 7, "maker": "  " } ] }`);

  const [entry] = readPlugins(fx.root).plugins;
  assert.equal(entry!.problem, null);
  assert.deepEqual(
    entry!.manifest.harnesses.map((harness) => [harness.name, harness.product, harness.maker]),
    [['acme-agent', 'Acme Agent', 'Acme'], ['older-agent', null, null], ['odd-agent', null, null]]);
  fx.cleanup();
});

// ——— Servers (D65 §1f, INT1): what a plugin hands every session, beside the knowledge host.

test('servers are read with the placeholder expanded in the command and the environment', () => {
  const fx = makeFixture('plugins-servers');
  const folder = plugin(fx.root, 'browser', JSON.stringify({
    id: 'browser',
    servers: [{ name: 'browser', command: ['node', '${plugin}/serve.mjs', '--headless'], env: { BROWSER_DATA: '${plugin}/data' } }],
  }));

  const [entry] = readPlugins(fx.root).plugins;
  assert.equal(entry!.problem, null);
  const [server] = entry!.manifest.servers;
  assert.equal(server!.name, 'browser');
  assert.deepEqual(server!.command, ['node', join(folder, 'serve.mjs'), '--headless']);
  assert.equal(server!.env.BROWSER_DATA, join(folder, 'data'));
  fx.cleanup();
});

/**
 * `${data}` is the plugin's own data folder (D77) — what survives an update (D64 §3), such as a
 * browser's signed-in profile, which otherwise landed under the user's profile (D63). Twin:
 * `PluginCatalogTests.cs`.
 */
test('the data placeholder is the plugin\'s own data folder, in a command and an environment', () => {
  const fx = makeFixture('plugins-data');
  plugin(fx.root, 'browser', JSON.stringify({
    id: 'browser',
    servers: [{ name: 'browser', command: ['npx', '@playwright/mcp', '--user-data-dir', '${data}/profile'], env: { BROWSER_STATE: '${data}' } }],
  }));

  const [entry] = readPlugins(fx.root).plugins;
  const [server] = entry!.manifest.servers;
  const data = dataFolder(fx.root, 'browser');
  assert.deepEqual(server!.command, ['npx', '@playwright/mcp', '--user-data-dir', join(data, 'profile')]);
  assert.equal(server!.env.BROWSER_STATE, resolve(data));
  fx.cleanup();
});

/**
 * `${browser}` is not the read's to expand (D78): it is the in-app browser's endpoint, which exists only
 * while the shell runs, so it survives the read and the driver fills it at hand-over. Twin:
 * `PluginCatalogTests.cs`.
 */
test('the browser placeholder survives the read for the hand-over to fill', () => {
  const fx = makeFixture('plugins-browser');
  plugin(fx.root, 'in-app-browser', JSON.stringify({
    id: 'in-app-browser',
    servers: [{ name: 'browser', command: ['npx', '@playwright/mcp', '--cdp-endpoint', '${browser}'] }],
  }));

  const [entry] = readPlugins(fx.root).plugins;
  assert.deepEqual(entry!.manifest.servers[0]!.command, ['npx', '@playwright/mcp', '--cdp-endpoint', '${browser}']);
  fx.cleanup();
});

test('a server named for the knowledge host is refused naming it, and the plugin contributes nothing', () => {
  const fx = makeFixture('plugins-server-knowledge');
  plugin(fx.root, 'sly', JSON.stringify({
    id: 'sly',
    harnesses: [{ name: 'sly-agent', command: ['sly'] }],
    servers: [{ name: KNOWLEDGE_SERVER, command: ['sly', '--serve'] }],
  }));

  const catalog = readPlugins(fx.root);
  assert.match(catalog.plugins[0]!.problem!, /daoris-knowledge/);
  assert.match(catalog.plugins[0]!.problem!, /knowledge host/);
  assert.deepEqual(catalog.plugins[0]!.manifest.servers, []);
  assert.deepEqual(catalog.plugins[0]!.manifest.harnesses, []);
  assert.deepEqual(catalog.contributing, []);
  fx.cleanup();
});

test('two plugins declaring the same server keep the first by id and refuse the second naming it', () => {
  const fx = makeFixture('plugins-server-twice');
  plugin(fx.root, 'b.two', '{ "id": "b.two", "servers": [ { "name": "browser", "command": ["two"] } ] }');
  plugin(fx.root, 'a.one', '{ "id": "a.one", "servers": [ { "name": "browser", "command": ["one"] } ] }');

  const catalog = readPlugins(fx.root);
  assert.equal(catalog.plugins[0]!.problem, null);
  assert.match(catalog.plugins[1]!.problem!, /a\.one/);
  assert.match(catalog.plugins[1]!.problem!, /browser/);
  assert.deepEqual(catalog.contributing.map((p) => p.manifest.id), ['a.one']);
  fx.cleanup();
});

test('a server without a command is a malformed manifest naming the server', () => {
  const fx = makeFixture('plugins-server-silent');
  plugin(fx.root, 'silent', '{ "id": "silent", "servers": [ { "name": "browser" } ] }');

  const [entry] = readPlugins(fx.root).plugins;
  assert.match(entry!.problem!, /browser/);
  assert.match(entry!.problem!, /command/);
  assert.deepEqual(entry!.manifest.servers, []);
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

/**
 * An id compares as the driver's `PluginCatalog` compares it, by `OrdinalIgnoreCase` (CASEFOLD1, `casefold.ts`). An id is
 * lowercase letters, digits, dots and dashes, so the one letter the two folds part in a row naming one is the Kelvin sign,
 * which lowers to a `k` and whose capital is itself: a row naming it switches no plugin off, and is no row of another's.
 */
test('a disabled row is a plugin\'s only as the driver finds it: the Kelvin sign is not a k', () => {
  const fx = makeFixture('plugins-state-fold');
  plugin(fx.root, 'acme.keep', '{ "id": "acme.keep", "harnesses": [ { "name": "acme-keep", "command": ["acme"] } ] }');
  writeFileSync(join(fx.root, STATE_FILE), JSON.stringify({ disabled: ['acme.\u{212A}eep'] }), 'utf8');

  assert.equal(readPlugins(fx.root).plugins[0]!.enabled, true);
  disablePlugin(fx.root, 'acme.keep');
  assert.deepEqual(readPluginState(fx.root).disabled, ['acme.keep', 'acme.\u{212A}eep']);
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
  plugin(fx.root, 'browser', '{ "id": "browser", "servers": [ { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@latest"] } ] }');
  disablePlugin(fx.root, 'acme.agent');

  const { code, out } = run(['list'], fx.root);
  assert.equal(code, 0);
  assert.match(out, /acme\.agent\s+Acme agent 1\.0\.0\s+\(off\)/);
  assert.match(out, /declares acme-agent; speaks on quest\/consider/);
  assert.match(out, /browser[\s\S]*hands sessions browser/);
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
  const sly = join(fx.root, 'sly');
  mkdirSync(sly, { recursive: true });
  writeFileSync(join(sly, MANIFEST), `{ "id": "sly", "servers": [ { "name": "${KNOWLEDGE_SERVER}", "command": ["x"] } ] }`);
  // The catalogue refuses a name in any case, so `add` must too — it copied this one in, and the
  // catalogue then refused it on its next read (REV3 CLEAN1).
  const shouting = join(fx.root, 'shouting');
  mkdirSync(shouting, { recursive: true });
  writeFileSync(join(shouting, MANIFEST), '{ "id": "shouting", "harnesses": [ { "name": "DSH", "command": ["x"] } ] }');
  const home = join(fx.root, 'home');

  for (const [source, why] of [
    [bad, /needs plugin API 99/], [shadow, /dsh.*this build/], [sly, /daoris-knowledge.*knowledge host/],
    [shouting, /DSH.*this build/],
  ] as const) {
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

test('a plugin whose folder a running process holds is refused whole, never half-removed', {
  // A process's working directory holds its folder on Windows only; elsewhere the move succeeds.
  skip: process.platform !== 'win32',
}, async () => {
  // REV3 modules F5, the terminal's twin: the desktop's hook process runs IN the plugin's folder, and
  // a recursive delete took every file it could before failing, stranding a plugin with no manifest.
  const fx = makeFixture('plugins-remove-held');
  plugin(fx.root, 'acme.held', '{ "id": "acme.held" }');
  const folder = join(pluginsRoot(fx.root), 'acme.held');
  const { spawn } = await import('node:child_process');
  const holder = spawn(process.execPath, ['-e', 'setTimeout(() => {}, 20000)'], { cwd: folder, stdio: 'ignore' });
  // The folder is let go when the holder has ended, not a fixed while after the kill (FLAKE1, 2026-10-07).
  const ended = new Promise((resolve) => holder.once('exit', resolve));
  try {
    await new Promise((resolve) => setTimeout(resolve, 300));
    const error = captureError(() => run(['remove', 'acme.held'], fx.root));
    assert.match(error.message, /not removed/);
    assert.equal(existsSync(join(folder, 'plugin.json')), true, 'the removal took the manifest and stranded the plugin');
  } finally {
    holder.kill();
    await ended;
    fx.cleanup();
  }
});

test('an id that is not a plugin id is refused before it becomes a path — the home and .data survive', () => {
  // REV3: `remove ..` joined the id under plugins/ and deleted the whole Daoris home; `remove .`
  // deleted plugins/ with every plugin's kept data inside it.
  const fx = makeFixture('plugins-remove-escape');
  plugin(fx.root, 'acme.agent', '{ "id": "acme.agent" }');
  mkdirSync(dataFolder(fx.root, 'acme.agent'), { recursive: true });
  writeFileSync(join(fx.root, 'driver.json'), '{}', 'utf8');

  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = fx.root;
  try {
    for (const id of ['..', '.', '.data', '../acme.agent', 'acme/agent', 'acme\\agent']) {
      for (const verb of ['remove', 'enable', 'disable']) {
        const error = captureError(() => commandPlugin({
          root: process.cwd(), argv: [verb, id], write: () => {}, packageRoot: process.cwd(),
        }));
        assert.match(error.message, /not a plugin id/, `${verb} ${id}`);
      }
    }
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }

  assert.equal(existsSync(join(fx.root, 'driver.json')), true);
  assert.equal(existsSync(join(pluginsRoot(fx.root), 'acme.agent')), true);
  assert.equal(existsSync(dataFolder(fx.root, 'acme.agent')), true);
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

/**
 * PLUG8 (D101): the kit a plugin is made with lives with the code that starts plugins and speaks their
 * wire, so `try` checks what the driver does — and the CLI spawns nothing outside `toolchain.ts`. Asked
 * for here, the CLI says where the kit is, with the command to type, and needs no home to say it.
 */
/**
 * REFAC1 (the second-opinion review of 2026-10-07): a plugin id's shape has one owner in this package, `isPluginId`, which
 * the catalogue, `plugin` and the landing rule's check (`driverconfig.ts`) all ask; CASEFOLD1e had to mend the same pattern
 * in two places. Each caller keeps its own lowering. The driver's `PluginCatalog.IsId` is the twin, held by its own tables.
 */
test('only plugins.ts spells a plugin id’s shape, and isPluginId answers it', () => {
  const sources = join(dirname(dirname(fileURLToPath(import.meta.url))), 'src');
  const spelled = readdirSync(sources)
    .filter((name) => name.endsWith('.ts') && /\[a-z0-9\]\[a-z0-9\.-\]/.test(readFileSync(join(sources, name), 'utf8')));
  assert.deepEqual(spelled, ['plugins.ts']);

  for (const id of ['acme.agent', 'example.github-pull-request', '0day', 'a']) assert.equal(isPluginId(id), true, id);
  for (const id of ['', '.data', '-x', 'Acme', 'acme/agent', 'acme agent', 'acme.agent\n', 'straße']) {
    assert.equal(isPluginId(id), false, JSON.stringify(id));
  }
});

test('new and try say where the plugin kit is, and touch nothing', () => {
  const saved = process.env.DAORIS_HOME;
  delete process.env.DAORIS_HOME;
  try {
    for (const verb of ['new', 'try']) {
      const error = captureError(() => commandPlugin({
        root: process.cwd(), argv: [verb, 'acme.gate'], write: () => {}, packageRoot: process.cwd(),
      }));
      assert.match(error.message, new RegExp(`\`daoris-driver plugins ${verb}\``));
      assert.match(error.message, /Settings → Plugins/);
      assert.equal((error as { exitCode?: number }).exitCode, 2);
    }
  } finally {
    if (saved !== undefined) process.env.DAORIS_HOME = saved;
  }
});
