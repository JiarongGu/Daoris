import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { PERMISSIONS_FILE, commandRules, readPermissions } from '../src/permissions.ts';
import { commandRemote } from '../src/remotes.ts';
import { readRemotes } from '../src/remotemap.ts';
import {
  addKeyAccount, keyOf, profileHome, readHarnessSettings, writeHarnessSettings,
} from '../src/toolchain.ts';
import { STATE_FILE, disablePlugin, readPluginState } from '../src/plugins.ts';
import { readDriverChoices, writeDriverChoices } from '../src/driverconfig.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * The home's JSON files — `permissions.json`, `remotes.json`, `harnesses.json`, `keys.json`,
 * `plugins.json`, `driver.json` — are each read by the CLI and by a C# twin, and edited by both.
 *
 * Two rules, found by REV3 and held here for every one of them:
 *   1. A BOM is not an unreadable file. The C# readers strip it (`File.ReadAllText`), so a CLI that
 *      read the same bytes as "nothing" disagreed with the driver about what the machine says.
 *   2. An editor never writes over a file it could not read. Reading one as empty and writing the
 *      edit back erased every deny rule, every other workspace's key and every disabled plugin — and
 *      said the edit had succeeded.
 */

const BOM = '﻿';
const TORN = '{ "machine": { "deny": ["Bash(rm:*)"] }, }';

function withHome<T>(home: string, fn: () => T): T {
  const saved = process.env.DAORIS_HOME;
  process.env.DAORIS_HOME = home;
  try {
    return fn();
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved;
  }
}

const quiet = { root: process.cwd(), write: () => {}, packageRoot: process.cwd() };

test('permissions.json: a BOM reads, and an edit over a torn file is refused with the file untouched', () => {
  const fx = makeFixture('homefiles-permissions');
  const path = join(fx.root, PERMISSIONS_FILE);

  writeFileSync(path, `${BOM}{ "machine": { "deny": ["Bash(rm:*)"] } }`, 'utf8');
  const read = readPermissions(path);
  assert.equal(read.problem, undefined);
  assert.deepEqual(read.machine.deny, ['Bash(rm:*)']);
  withHome(fx.root, () => commandRules({ ...quiet, argv: ['allow', 'Bash(npm test:*)'] }));
  assert.deepEqual(readPermissions(path).machine.deny, ['Bash(rm:*)'], 'the deny survived the edit');

  writeFileSync(path, TORN, 'utf8');
  const error = withHome(fx.root, () => captureError(() => commandRules({ ...quiet, argv: ['allow', 'Bash(x:*)'] })));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), TORN);
  fx.cleanup();
});

test('remotes.json: a BOM reads as wired, and an edit over a torn map is refused with the map untouched', async () => {
  const fx = makeFixture('homefiles-remotes');
  const path = join(fx.root, 'remotes.json');
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
  const path = join(fx.root, 'harnesses.json');

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

test('keys.json: a BOM reads, and a new key over a torn file is refused', () => {
  const fx = makeFixture('homefiles-keys');
  const path = join(fx.root, 'keys.json');

  writeFileSync(path, `${BOM}{ "claude-code": { "account-1": "sk-ant-first-key-0001" } }`, 'utf8');
  assert.equal(keyOf(fx.root, 'claude-code', 'account-1'), 'sk-ant-first-key-0001');

  const torn = '{ "claude-code": { "account-1": "sk-ant-first-key-0001" }, }';
  writeFileSync(path, torn, 'utf8');
  const error = captureError(() => addKeyAccount(fx.root, 'claude-code', 'sk-ant-second-key-0002', () => {}));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), torn);
  // Refused before anything was made: no account directory for a key that was never kept.
  assert.equal(existsSync(profileHome(fx.root, 'claude-code', 'account-2')), false);
  fx.cleanup();
});

test('plugins.json: a BOM reads, and a switch over a torn file is refused', () => {
  const fx = makeFixture('homefiles-plugins');
  const path = join(fx.root, STATE_FILE);

  writeFileSync(path, `${BOM}{ "disabled": ["acme.agent"] }`, 'utf8');
  assert.deepEqual(readPluginState(fx.root).disabled, ['acme.agent']);

  const torn = '{ "disabled": ["acme.agent"], }';
  writeFileSync(path, torn, 'utf8');
  const error = captureError(() => disablePlugin(fx.root, 'other.one'));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), torn);
  fx.cleanup();
});

test('driver.json: a BOM reads rather than refusing, and the writer holds the same rule', () => {
  const fx = makeFixture('homefiles-driver');
  const path = join(fx.root, 'driver.json');

  writeFileSync(path, `${BOM}{ "drivable": ["engine"] }`, 'utf8');
  const choices = readDriverChoices(path);
  assert.deepEqual(choices.drivable, ['engine']);

  const torn = '{ "drivable": ["engine"], }';
  writeFileSync(path, torn, 'utf8');
  const error = captureError(() => writeDriverChoices(path, choices));
  assert.match(error.message, /will not overwrite a file it could not understand/);
  assert.equal(readFileSync(path, 'utf8'), torn);
  fx.cleanup();
});
