import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { readHarnessSettings, writeHarnessSettings } from '../src/toolchain.ts';
import { joinProblem, joinRefusal, placesOf, withJoined } from '../src/rotation.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { makeFixture } from './_fixture.ts';

/**
 * An account put into a scope's list, and where an account runs (ACCT1, D125's ACCT1 note; D130 §3.1). The CLI's half of a
 * TWIN with the driver's `Harnesses.Accounts.cs`: 🔴 `AccountJoinTwinTests.cs` holds the same tables, row for row and in
 * the same order, and the last test here holds each to this one, cell for cell.
 *
 * A scope is a workspace's name, or `null` for this machine's list. An `after` is the sections a write leaves, compared as
 * JSON, or `refused borrows` where the workspace has no list or default of its own and so takes this machine's.
 */

type Cell = string | null;

const SECTIONS = ['defaults', 'workspaces', 'rotation', 'workspaceRotation', 'rotationUse', 'workspaceRotationUse'];

const JOIN_ROWS: [why: string, before: string, agent: string, account: string, workspace: Cell, after: string][] = [
  ['this machine\'s list gains it at its end', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', 'account-2', null, '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-1","account-2"]}}'],
  ['a list that holds it already is as it was', '{"rotation":{"claude-code":["account-2","account-1"]}}', 'claude-code', 'account-2', null, '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2","account-1"]}}'],
  ['a workspace\'s own list gains it, this machine\'s kept', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-1"]}}}', 'claude-code', 'account-2', 'work', '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}}}'],
  ['this machine with a default and no list begins a list at its default', '{"defaults":{"claude-code":"account-1"}}', 'claude-code', 'account-2', null, '{"defaults":{"claude-code":"account-1"},"workspaces":{},"rotation":{"claude-code":["account-1","account-2"]}}'],
  ['this machine naming nobody begins its list with it', '{}', 'claude-code', 'account-2', null, '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2"]}}'],
  ['a default that is the account itself is a list of it alone', '{"defaults":{"claude-code":"account-2"}}', 'claude-code', 'account-2', null, '{"defaults":{"claude-code":"account-2"},"workspaces":{},"rotation":{"claude-code":["account-2"]}}'],
  ['a workspace with its own default and no list begins a list at its default', '{"workspaces":{"work":{"claude-code":"account-1"}}}', 'claude-code', 'account-2', 'work', '{"defaults":{},"workspaces":{"work":{"claude-code":"account-1"}},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}}}'],
  ['a workspace naming neither takes this machine\'s list, so joining it is refused', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', 'account-2', 'forge', 'refused borrows'],
  ['a workspace naming neither on a machine naming nobody is refused too', '{}', 'claude-code', 'account-2', 'forge', 'refused borrows'],
  ['a workspace naming only another agent\'s account takes this machine\'s list', '{"workspaces":{"work":{"codex":"account-1"}}}', 'claude-code', 'account-2', 'work', 'refused borrows'],
  ['another agent\'s list is not this one\'s', '{"rotation":{"codex":["account-1"]}}', 'claude-code', 'account-2', null, '{"defaults":{},"workspaces":{},"rotation":{"codex":["account-1"],"claude-code":["account-2"]}}'],
  ['a list\'s settings stay with it', '{"workspaceRotation":{"work":{"claude-code":["account-1"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}}}}', 'claude-code', 'account-2', 'work', '{"defaults":{},"workspaces":{},"workspaceRotation":{"work":{"claude-code":["account-1","account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}}}}'],
];

test('an account joins a list as the driver joins it (the twin\'s table)', () => {
  for (const [index, [why, before, agent, account, workspace, after]] of JOIN_ROWS.entries()) {
    const fx = makeFixture(`join-${index}`);
    const path = join(fx.root, 'harnesses.json');
    writeFileSync(path, before, 'utf8');
    const settings = readHarnessSettings(path);

    if (joinProblem(settings, agent, workspace) !== null) {
      assert.equal(after, 'refused borrows', `${why}: refused`);
    } else {
      writeHarnessSettings(path, withJoined(settings, agent, account, workspace));
      const root = JSON.parse(readFileSync(path, 'utf8')) as Record<string, unknown>;
      const written = Object.fromEntries(SECTIONS.filter((section) => section in root).map((section) => [section, root[section]]));
      assert.notEqual(after, 'refused borrows', why);
      assert.deepEqual(written, JSON.parse(after), why);
    }
    fx.cleanup();
  }
});

const PLACE_ROWS: [why: string, wiring: string, agent: string, account: string, places: string][] = [
  ['in no list and no default is nowhere', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', 'account-2', '[]'],
  ['this machine\'s list', '{"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', 'account-2', '[{"workspace":null,"list":true,"default":false}]'],
  ['this machine\'s default and its list', '{"defaults":{"claude-code":"account-1"},"rotation":{"claude-code":["account-1"]}}', 'claude-code', 'account-1', '[{"workspace":null,"list":true,"default":true}]'],
  ['this machine\'s default with no list', '{"defaults":{"claude-code":"account-1"}}', 'claude-code', 'account-1', '[{"workspace":null,"list":false,"default":true}]'],
  ['a workspace\'s own default with no list', '{"workspaces":{"work":{"claude-code":"account-1"}}}', 'claude-code', 'account-1', '[{"workspace":"work","list":false,"default":true}]'],
  ['this machine first, then each workspace by name', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"zeta":{"claude-code":["account-1"]},"alpha":{"claude-code":["account-2","account-1"]}}}', 'claude-code', 'account-1', '[{"workspace":null,"list":true,"default":false},{"workspace":"alpha","list":true,"default":false},{"workspace":"zeta","list":true,"default":false}]'],
  ['another agent\'s places are not this one\'s', '{"defaults":{"codex":"account-1"},"rotation":{"codex":["account-1"]}}', 'claude-code', 'account-1', '[]'],
  ['an account compares exactly, as the wiring compares it', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', 'Account-1', '[]'],
];

test('an account\'s places read as the driver reads them (the twin\'s table)', () => {
  for (const [index, [why, wiring, agent, account, places]] of PLACE_ROWS.entries()) {
    const fx = makeFixture(`places-${index}`);
    const path = join(fx.root, 'harnesses.json');
    writeFileSync(path, wiring, 'utf8');

    assert.deepEqual(placesOf(readHarnessSettings(path), agent, account), JSON.parse(places), why);
    fx.cleanup();
  }
});

test('a refused join names this machine\'s list and the workspace\'s own, as the driver says it', () => {
  const sentence = joinRefusal('claude-code', 'forge');

  assert.match(sentence, /`forge` names no `claude-code` account or list of its own/);
  assert.match(sentence, /this machine's list/);
  assert.match(sentence, /daoris agent profile order claude-code <account>… --workspace forge/);
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLE = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'AccountJoinTwinTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLE, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'AccountJoinTwinTests');

  assert.deepEqual(rows('An_account_joins_a_list_as_the_cli_joins_it'), JOIN_ROWS);
  assert.deepEqual(rows('An_account_s_places_read_as_the_cli_reads_them'), PLACE_ROWS);
});
