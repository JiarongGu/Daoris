import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { commandRemote } from '../src/service/remotes.ts';
import { readRemotes } from '../src/service/remotemap.ts';
import { makeFixture } from './_fixture.ts';

/**
 * The home's JSON files the CLI still edits — `remotes.json` — are read by the CLI and by a C# twin, and edited by
 * both. `driver.json` is Daoris's driver program's to read and write since PUSH1f2b, `tools.json` since PUSH1f2c,
 * `permissions.json` since PUSH1f2d, `harnesses.json` and `keys.json` since PUSH1f2e, and `plugins.json` since
 * PUSH1f2f; their rules are the driver's tests'.
 *
 * Two rules, found by REV3 and held here for each of them:
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

// `plugins.json` and `tools.json` are Daoris's driver program's to read and write since PUSH1f2f and PUSH1f2c: a switch over a torn
// state file is refused by `PluginState` (the plugins text table's row), and `ToolsTests` holds the tools file's BOM and torn file.
