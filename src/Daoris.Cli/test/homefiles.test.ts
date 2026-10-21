import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { commandRemote } from '../src/service/remotes.ts';
import { readRemotes } from '../src/service/remotemap.ts';
import { keyOf, readHarnessSettings, writeHarnessSettings } from '../src/agents/toolchain.ts';
import { disablePlugin, readPluginState } from '../src/plugins/plugins.ts';
import { readTools } from '../src/tools/tools.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * The home's JSON files — `remotes.json`, `harnesses.json`, `keys.json`, `plugins.json`, `tools.json` —
 * are each read by the CLI and by a C# twin, and edited by both. `driver.json` is Daoris's driver program's
 * to read and write since PUSH1f2b, and `permissions.json` since PUSH1f2d; their rules are the driver's tests'.
 *
 * Two rules, found by REV3 and held here for every one of them:
 *   1. A BOM is not an unreadable file. The C# readers strip it (`File.ReadAllText`), so a CLI that
 *      read the same bytes as "nothing" disagreed with the driver about what the machine says.
 *   2. An editor never writes over a file it could not read. Reading one as empty and writing the
 *      edit back erased every deny rule, every other workspace's key and every disabled plugin — and
 *      said the edit had succeeded.
 */

const BOM = '﻿';

const quiet = { root: process.cwd(), write: () => {}, packageRoot: process.cwd() };

test('remotes.json: a BOM reads as wired, and an edit over a torn map is refused with the map untouched', async () => {
  const fx = makeFixture('homefiles-remotes');
  const path = fx.entry('remotes');
  const entry = '"aurora": { "url": "https://aurora.example.com", "key": "dk_aurora_secret_key" }';

  writeFileSync(path, `${BOM}{ ${entry} }`, 'utf8');
  assert.equal(readRemotes({ DAORIS_REMOTE_CONFIG: path }).remotes.get('aurora')?.url, 'https://aurora.example.com');

  const torn = `{ ${entry}, }`;
  writeFileSync(path, torn, 'utf8');
  const saved = process.env.DAORIS_REMOTE_CONFIG;
  process.env.DAORIS_REMOTE_CONFIG = path;
  try {
    const error = await commandRemote({ ...quiet, argv: ['add', 'tools', '--url', 'https://t.example.com', '--key', 'dk_tools_key_1234'] })
      .then(() => null, (thrown: Error) => thrown);
    assert.ok(error);
    assert.match(error.message, /will not overwrite a file it could not understand/);
  } finally {
    if (saved === undefined) delete process.env.DAORIS_REMOTE_CONFIG;
    else process.env.DAORIS_REMOTE_CONFIG = saved;
  }
  assert.equal(readFileSync(path, 'utf8'), torn);
  fx.cleanup();
});

test('harnesses.json: a BOM reads, and a write over a torn file is refused', () => {
  const fx = makeFixture('homefiles-harnesses');
  const path = fx.entry('harnesses');

  writeFileSync(path, `${BOM}{ "defaults": { "codex": "work" } }`, 'utf8');
  assert.equal(readHarnessSettings(path).defaults.codex, 'work');

  const torn = '{ "defaults": { "codex": "work" }, }';
  writeFileSync(path, torn, 'utf8');
  const settings = readHarnessSettings(path);
  const error = captureError(() => writeHarnessSettings(path, { ...settings, defaults: { 'claude-code': 'account-1' } }));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), torn);
  fx.cleanup();
});

// A new key over a torn file is the driver program's since PUSH1f2e5 (`agents-text.json`'s row, refused in `JsonKeeping`'s words).
test('keys.json: a BOM reads', () => {
  const fx = makeFixture('homefiles-keys');
  const path = fx.entry('keys');

  writeFileSync(path, `${BOM}{ "claude-code": { "account-1": "sk-ant-first-key-0001" } }`, 'utf8');
  assert.equal(keyOf(fx.root, 'claude-code', 'account-1'), 'sk-ant-first-key-0001');
  fx.cleanup();
});

test('plugins.json: a BOM reads, and a switch over a torn file is refused', () => {
  const fx = makeFixture('homefiles-plugins');
  const path = fx.entry('plugin-state');

  writeFileSync(path, `${BOM}{ "disabled": ["acme.agent"] }`, 'utf8');
  assert.deepEqual(readPluginState(fx.root).disabled, ['acme.agent']);

  const torn = '{ "disabled": ["acme.agent"], }';
  writeFileSync(path, torn, 'utf8');
  const error = captureError(() => disablePlugin(fx.root, 'other.one'));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), torn);
  fx.cleanup();
});

// The write over a torn file is the driver's alone since PUSH1f2c (`ToolsTests`' BOM and torn-file case): `daoris tool` is its program's.
test('tools.json: a BOM reads', () => {
  const fx = makeFixture('homefiles-tools');
  const path = fx.entry('tools');

  writeFileSync(path, `${BOM}{ "tools": { "git": { "use": "managed", "version": "2.51.0" } } }`, 'utf8');
  assert.deepEqual(readTools(fx.root).entries.git, { way: 'managed', version: '2.51.0', file: null, problem: null });
  fx.cleanup();
});
