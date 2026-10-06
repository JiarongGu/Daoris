import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandHarness, profileHome, readHarnessSettings, withDefault, writeHarnessSettings } from '../src/toolchain.ts';
import { resolveRotation, rotationProblem, withRotation, withoutAccount } from '../src/rotation.ts';
import { driverRows as csharpRows, heldSoFar } from './_csharp.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * The order in `harnesses.json` (TOOL4e, D125 §3.1): `rotation` and `workspaceRotation`, the accounts rotation may use
 * per agent, for the machine and for a workspace. The CLI's half of a TWIN with the driver's `Harnesses.cs`
 * (`HarnessSettings`): 🔴 `RotationTwinTests.cs` holds the same tables, row for row and in the same order, and the last
 * test here holds each to this one, cell for cell.
 *
 * An order is a JSON list of account names; `null` is none. A file is the text on disk, and an `after` is the sections a
 * write leaves, compared as JSON.
 */

type Cell = string | null;

function at(fx: { root: string }): string {
  return join(fx.root, 'harnesses.json');
}

function read(fx: { root: string }, file: string) {
  writeFileSync(at(fx), file, 'utf8');
  return readHarnessSettings(at(fx));
}

/** The named sections of the file as written, those it holds. */
function written(fx: { root: string }, ...sections: string[]): Record<string, unknown> {
  const root = JSON.parse(readFileSync(at(fx), 'utf8')) as Record<string, unknown>;
  return Object.fromEntries(sections.filter((section) => section in root).map((section) => [section, root[section]]));
}

// ——— Reading and resolving (§3.1): the workspace's order for the agent, else the machine's, else none.

const ORDER_ROWS: [name: string, file: string, agent: string, workspace: Cell, order: Cell, from: string][] = [
  ['no order anywhere is none, and nothing rotates', '{}', 'claude-code', null, null, 'unset'],
  ['the machine\'s order, as written', '{"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', null, '["account-1","account-2"]', 'machine'],
  ['a workspace\'s own order wins over the machine\'s', '{"rotation":{"claude-code":["account-1","account-2"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}', 'claude-code', 'work', '["account-2","account-3"]', 'workspace'],
  ['a workspace with no order of its own takes the machine\'s', '{"rotation":{"claude-code":["account-1","account-2"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}', 'claude-code', 'home', '["account-1","account-2"]', 'machine'],
  ['a workspace\'s order for another agent is not this one\'s', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"codex":["account-9"]}}}', 'claude-code', 'work', '["account-1"]', 'machine'],
  ['another agent\'s order is not this one\'s', '{"rotation":{"codex":["account-1"]}}', 'claude-code', null, null, 'unset'],
  ['the order is kept as the person wrote it, never sorted', '{"rotation":{"claude-code":["account-3","account-1","account-2"]}}', 'claude-code', null, '["account-3","account-1","account-2"]', 'machine'],
  ['a name is trimmed, and a blank and what is not a name are skipped', '{"rotation":{"claude-code":[" account-1 ","",7,null,"account-2"]}}', 'claude-code', null, '["account-1","account-2"]', 'machine'],
  ['a name written twice, in any case, is read once where first written', '{"rotation":{"claude-code":["account-2","account-1","ACCOUNT-2"]}}', 'claude-code', null, '["account-2","account-1"]', 'machine'],
  ['an order that is not a list is none', '{"rotation":{"claude-code":"account-1"}}', 'claude-code', null, null, 'unset'],
  ['an order that names nobody is none, so the workspace takes the machine\'s', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["  "]}}}', 'claude-code', 'work', '["account-1"]', 'machine'],
  ['a rotation that is not an object is none', '{"rotation":["account-1"]}', 'claude-code', null, null, 'unset'],
  ['a workspace\'s orders that are not an object are none', '{"workspaceRotation":{"work":["account-1"]}}', 'claude-code', 'work', null, 'unset'],
  ['a file that does not read is none', 'not json', 'claude-code', null, null, 'unset'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"rotation":{"claude-code":["straße","STRASSE"]}}', 'claude-code', null, '["straße","STRASSE"]', 'machine'],
  ['a dotted capital I is not an i with a dot above', '{"rotation":{"claude-code":["İzmir","i\\u0307zmir"]}}', 'claude-code', null, '["İzmir","i\\u0307zmir"]', 'machine'],
  ['a workspace is found in any case', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-2"]}}}', 'claude-code', 'WORK', '["account-2"]', 'workspace'],
  ['a workspace written twice in any case is one, holding the later\'s orders', '{"workspaceRotation":{"work":{"claude-code":["account-1"]},"WORK":{"claude-code":["account-2"]}}}', 'claude-code', 'work', '["account-2"]', 'workspace'],
  ['a workspace whose capital is two letters is not those two: straße has no order of STRASSE\'s', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"STRASSE":{"claude-code":["account-2"]}}}', 'claude-code', 'straße', '["account-1"]', 'machine'],
];

/**
 * CASEFOLD1's rows, which `RotationTwinTests` does not hold yet: names compare as the driver's `OrdinalIgnoreCase` does
 * (`casefold.ts`), so a name full case mapping would widen or lower to two letters is read as its own. The twin check below
 * holds each the driver holds, cell for cell.
 */
const DRIVER_OWES = new Set([
  'a letter whose capital is two letters is not those two: straße is not STRASSE',
  'a dotted capital I is not an i with a dot above',
]);

/**
 * CASEFOLD1c's rows, which `RotationTwinTests` does not hold yet either: a workspace is found, edited and read once in any
 * case, as the driver's dictionaries hold one (`OrdinalIgnoreCase`): spelled as first written, holding the last read.
 */
const ORDERS_OWED = new Set([
  ...DRIVER_OWES,
  'a workspace is found in any case',
  'a workspace written twice in any case is one, holding the later\'s orders',
  'a workspace whose capital is two letters is not those two: straße has no order of STRASSE\'s',
]);
const EDITS_OWED = new Set([
  'a workspace order set in another case replaces the one there, as first written',
  'a workspace order cleared in another case',
]);
const FILES_OWED = new Set(['a workspace written twice in any case is written once, as first written, holding the later\'s']);

test('an order resolves as the driver resolves it: the workspace\'s, else the machine\'s, else none', () => {
  const fx = makeFixture('rotation-resolve');
  for (const [name, file, agent, workspace, order, from] of ORDER_ROWS) {
    const resolved = resolveRotation(read(fx, file), agent, workspace);
    assert.deepEqual(resolved, { order: order === null ? [] : JSON.parse(order), from }, name);
  }
  fx.cleanup();
});

// ——— Editing (§3.1, §6): an order set replaces the agent's whole; cleared, its entry goes; written only when set.

const EDIT_ROWS: [why: string, before: string, agent: string, order: Cell, workspace: Cell, after: string][] = [
  ['a machine order set', '{}', 'claude-code', '["account-1","account-2"]', null, '{"rotation":{"claude-code":["account-1","account-2"]}}'],
  ['a machine order replaced whole, in its new order', '{"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', '["account-2","account-1"]', null, '{"rotation":{"claude-code":["account-2","account-1"]}}'],
  ['a machine order cleared, another agent\'s kept', '{"rotation":{"claude-code":["account-1"],"codex":["account-1"]}}', 'claude-code', null, null, '{"rotation":{"codex":["account-1"]}}'],
  ['the last order cleared leaves no rotation in the file', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', null, null, '{}'],
  ['a workspace order set, the machine\'s kept', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', '["account-2","account-3"]', 'work', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}'],
  ['a workspace order cleared, another agent\'s there kept', '{"workspaceRotation":{"work":{"claude-code":["account-2"],"codex":["account-1"]}}}', 'claude-code', null, 'work', '{"workspaceRotation":{"work":{"codex":["account-1"]}}}'],
  ['a workspace left with no order is dropped', '{"workspaceRotation":{"work":{"claude-code":["account-2"]},"lab":{"codex":["account-1"]}}}', 'claude-code', null, 'work', '{"workspaceRotation":{"lab":{"codex":["account-1"]}}}'],
  ['an order of nobody is a clear', '{"rotation":{"claude-code":["account-1"]}}', 'claude-code', '[]', null, '{}'],
  ['clearing what is not set changes nothing', '{"rotation":{"codex":["account-1"]}}', 'claude-code', null, 'work', '{"rotation":{"codex":["account-1"]}}'],
  ['a name is kept trimmed', '{}', 'claude-code', '[" account-1 "]', null, '{"rotation":{"claude-code":["account-1"]}}'],
  ['a workspace order set in another case replaces the one there, as first written', '{"workspaceRotation":{"work":{"claude-code":["account-1"]}}}', 'claude-code', '["account-2"]', 'WORK', '{"workspaceRotation":{"work":{"claude-code":["account-2"]}}}'],
  ['a workspace order cleared in another case', '{"workspaceRotation":{"work":{"claude-code":["account-2"]},"lab":{"codex":["account-1"]}}}', 'claude-code', null, 'WORK', '{"workspaceRotation":{"lab":{"codex":["account-1"]}}}'],
];

test('an order is set and cleared as the driver writes it (the twin\'s table)', () => {
  const fx = makeFixture('rotation-edit');
  for (const [why, before, agent, order, workspace, after] of EDIT_ROWS) {
    writeHarnessSettings(at(fx), withRotation(read(fx, before), agent, order === null ? null : JSON.parse(order), workspace));
    assert.deepEqual(written(fx, 'rotation', 'workspaceRotation'), JSON.parse(after), why);
  }
  fx.cleanup();
});

// ——— Refused by both doors (§3.1): an account that does not exist, or one named twice.

const PROBLEM_ROWS: [why: string, accounts: string, order: string, refused: Cell][] = [
  ['accounts that exist, each once, in any order', '["account-1","account-2"]', '["account-2","account-1"]', null],
  ['an account that does not exist', '["account-1"]', '["account-1","account-9"]', 'missing account-9'],
  ['one account twice', '["account-1","account-2"]', '["account-1","account-2","account-1"]', 'twice account-1'],
  ['a name in another case than its directory\'s names no account', '["account-1"]', '["Account-1"]', 'missing Account-1'],
  ['one account twice in another case, where both directories are there', '["account-1","ACCOUNT-1"]', '["account-1","ACCOUNT-1"]', 'twice ACCOUNT-1'],
  ['the first problem in the order is the one said', '["account-1"]', '["account-8","account-1","account-1"]', 'missing account-8'],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '["straße","STRASSE"]', '["straße","STRASSE"]', null],
  ['a dotted capital I is not an i with a dot above', '["İzmir","i\\u0307zmir"]', '["İzmir","i\\u0307zmir"]', null],
];

test('an order is refused as the driver refuses it', () => {
  for (const [why, accounts, order, refused] of PROBLEM_ROWS) {
    const problem = rotationProblem(JSON.parse(accounts), JSON.parse(order));
    assert.equal(problem === null ? null : `${problem.twice ? 'twice' : 'missing'} ${problem.account}`, refused, why);
  }
});

// ——— An account removed (D66 §3): no default and no order names it afterwards.

const REMOVE_ROWS: [why: string, before: string, agent: string, profile: string, after: string][] = [
  ['a machine default naming it is cleared, another agent\'s kept', '{"defaults":{"claude-code":"account-1","codex":"account-1"}}', 'claude-code', 'account-1', '{"defaults":{"codex":"account-1"},"workspaces":{}}'],
  ['a workspace default naming it is cleared, and a workspace left naming none is dropped', '{"workspaces":{"work":{"claude-code":"account-1"},"lab":{"claude-code":"account-2"}}}', 'claude-code', 'account-1', '{"defaults":{},"workspaces":{"lab":{"claude-code":"account-2"}}}'],
  ['it leaves the machine\'s order, the rest kept in theirs', '{"rotation":{"claude-code":["account-3","account-1","account-2"]}}', 'claude-code', 'account-1', '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-3","account-2"]}}'],
  ['it leaves a workspace\'s order, and an order left naming nobody goes', '{"workspaceRotation":{"work":{"claude-code":["account-1"]},"lab":{"claude-code":["account-1","account-2"]}}}', 'claude-code', 'account-1', '{"defaults":{},"workspaces":{},"workspaceRotation":{"lab":{"claude-code":["account-2"]}}}'],
  ['another agent\'s account of the same name stays', '{"defaults":{"codex":"account-1"},"rotation":{"codex":["account-1"]}}', 'claude-code', 'account-1', '{"defaults":{"codex":"account-1"},"workspaces":{},"rotation":{"codex":["account-1"]}}'],
  ['a kept account removed is kept no longer, the rest of its settings kept', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-1"}}}', 'claude-code', 'account-1', '{"defaults":{},"workspaces":{},"rotation":{"claude-code":["account-2"]},"rotationUse":{"claude-code":{"use":"order"}}}'],
  ['an account removed that leaves a workspace\'s list naming nobody takes its settings too', '{"workspaceRotation":{"work":{"claude-code":["account-1"]}},"workspaceRotationUse":{"work":{"claude-code":{"keep":"account-1","use":"order"}}}}', 'claude-code', 'account-1', '{"defaults":{},"workspaces":{}}'],
];

test('an account removed leaves the wiring as the driver leaves it', () => {
  const fx = makeFixture('rotation-remove');
  for (const [why, before, agent, profile, after] of REMOVE_ROWS) {
    writeHarnessSettings(at(fx), withoutAccount(read(fx, before), agent, profile));
    assert.deepEqual(
      written(fx, 'defaults', 'workspaces', 'rotation', 'workspaceRotation', 'rotationUse', 'workspaceRotationUse'), JSON.parse(after), why);
  }
  fx.cleanup();
});

// ——— 🔴 Each writer keeps the other's sections (D125 §0.1): both write the same file for the same wiring, a key
// neither knows included, so a terminal edit after a screen's, or the other way, loses nothing.

const FILE_ROWS: [why: string, before: string, codexDefault: Cell, after: string][] = [
  ['every section either writes, and a key neither knows, written back as read', '{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}', null, '{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}'],
  ['a default set keeps the orders, their settings, the pins and the key neither knows', '{"later":{"kept":true},"defaults":{"claude-code":"account-1"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}', 'account-9', '{"later":{"kept":true},"defaults":{"claude-code":"account-1","codex":"account-9"},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{"claude-code":"2.1.0"},"workspaceVersions":{"work":{"codex":"0.50.0"}},"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}}}}'],
  ['a file that names no order is written with none', '{"defaults":{"claude-code":"account-1"},"workspaces":{},"versions":{},"workspaceVersions":{}}', null, '{"defaults":{"claude-code":"account-1"},"workspaces":{},"versions":{},"workspaceVersions":{}}'],
  ['an empty file is written with the four sections and no order', '{}', null, '{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{}}'],
  ['a setting\'s fields go out in one order, whatever order they were read in, each as chosen', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85,"early":true,"keep":"account-1","use":"goal"}}}', null, '{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"goal","keep":"account-1","early":true,"near":85}}}'],
  ['a value and a setting this build does not know go back as read, after the ones it knows', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"weekly":"pace","near":120,"use":"drain"}}}', null, '{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"drain","near":120,"weekly":"pace"}}}'],
  ['the retired prefer and parallel are not written back, and an entry naming nothing else goes', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"prefer":"left","keep":"account-1","parallel":true},"codex":{"prefer":"soonest"}}}', null, '{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"keep":"account-1"}}}'],
  ['settings with no list are written back as read, and read only with one', '{"rotationUse":{"claude-code":{"early":false}},"workspaceRotationUse":{"work":{"codex":{"keep":"account-1"}}}}', null, '{"defaults":{},"workspaces":{},"versions":{},"workspaceVersions":{},"rotationUse":{"claude-code":{"early":false}},"workspaceRotationUse":{"work":{"codex":{"keep":"account-1"}}}}'],
  ['a workspace written twice in any case is written once, as first written, holding the later\'s', '{"workspaces":{"work":{"claude-code":"account-1"},"WORK":{"claude-code":"account-2"}},"workspaceVersions":{"Lab":{"codex":"0.50.0"},"lab":{"codex":"0.51.0"}},"workspaceRotation":{"work":{"claude-code":["account-1"]},"Work":{"claude-code":["account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"goal"}},"WORK":{"claude-code":{"use":"order"}}}}', null, '{"defaults":{},"workspaces":{"work":{"claude-code":"account-2"}},"versions":{},"workspaceVersions":{"Lab":{"codex":"0.51.0"}},"workspaceRotation":{"work":{"claude-code":["account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}}}}'],
];

test('both twins write the same file: every section, in the same order, as the same bytes', () => {
  const fx = makeFixture('rotation-file');
  for (const [why, before, codexDefault, after] of FILE_ROWS) {
    const settings = read(fx, before);
    writeHarnessSettings(at(fx), codexDefault === null ? settings : withDefault(settings, 'codex', codexDefault, null));
    assert.equal(readFileSync(at(fx), 'utf8'), `${JSON.stringify(JSON.parse(after), null, 2)}\n`, why);
  }
  fx.cleanup();
});

// ——— The verb (§6): `daoris agent profile order <agent> <profile>…|--clear [--workspace <name>]`.

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_HARNESS_CONFIG;
  process.env.DAORIS_HARNESS_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandHarness({ root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd() }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_HARNESS_CONFIG;
    else process.env.DAORIS_HARNESS_CONFIG = saved;
  }
}

function accounts(fx: { root: string }, agent: string, ...names: string[]): void {
  for (const name of names) mkdirSync(profileHome(fx.root, agent, name), { recursive: true });
}

test('`profile order` writes the machine\'s order as given, and `--workspace` one workspace\'s', () => {
  const fx = makeFixture('rotation-verb');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-3');

  const machine = run(['profile', 'order', 'claude-code', 'account-2', 'account-1'], at(fx));
  const scoped = run(['profile', 'order', 'claude-code', 'account-3', '--workspace', 'work'], at(fx));

  assert.equal(machine.code, 0);
  assert.match(machine.out, /on this machine, `claude-code`'s rotation is account-2, then account-1\./);
  assert.match(machine.out, /An account not in it is never rotated into/);
  assert.match(scoped.out, /in `work`, `claude-code`'s rotation is account-3\./);
  const settings = readHarnessSettings(at(fx));
  assert.deepEqual(resolveRotation(settings, 'claude-code', null), { order: ['account-2', 'account-1'], from: 'machine' });
  assert.deepEqual(resolveRotation(settings, 'claude-code', 'work'), { order: ['account-3'], from: 'workspace' });
  fx.cleanup();
});

test('`profile order` refuses an account that does not exist and one named twice, writing nothing', () => {
  const fx = makeFixture('rotation-verb-refused');
  accounts(fx, 'claude-code', 'account-1', 'account-2');

  const missing = captureError(() => run(['profile', 'order', 'claude-code', 'account-1', 'account-9'], at(fx)));
  const twice = captureError(() => run(['profile', 'order', 'claude-code', 'account-1', 'account-1'], at(fx)));
  const none = captureError(() => run(['profile', 'order', 'claude-code'], at(fx)));

  assert.match(missing.message, /`claude-code` has no account `account-9`.*accounts there: account-1, account-2/);
  assert.match(twice.message, /`account-1` is named twice/);
  assert.match(none.message, /needs the accounts, in order, or --clear/);
  assert.deepEqual(readHarnessSettings(at(fx)).rotation, {});
  fx.cleanup();
});

test('`profile order --clear` takes the order away, and refuses accounts named beside it', () => {
  const fx = makeFixture('rotation-verb-clear');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-2', '--workspace', 'work'], at(fx));

  const scoped = run(['profile', 'order', 'claude-code', '--clear', '--workspace', 'work'], at(fx));
  assert.match(scoped.out, /`work` names no order for `claude-code` now: the machine's applies there, account-1, then account-2\./);
  const machine = run(['profile', 'order', 'claude-code', '--clear'], at(fx));
  assert.match(machine.out, /`claude-code` has no order on this machine now\./);
  assert.deepEqual(resolveRotation(readHarnessSettings(at(fx)), 'claude-code', 'work'), { order: [], from: 'unset' });

  const both = captureError(() => run(['profile', 'order', 'claude-code', 'account-1', '--clear'], at(fx)));
  assert.match(both.message, /`--clear` names no account/);
  fx.cleanup();
});

test('`profile order` on a door writes its owner\'s order, and says so', () => {
  const fx = makeFixture('rotation-verb-door');
  accounts(fx, 'claude-code', 'account-1');

  const door = run(['profile', 'order', 'claude-code-acp', 'account-1'], at(fx));

  assert.match(door.out, /runs as `claude-code`/);
  assert.deepEqual(readHarnessSettings(at(fx)).rotation, { 'claude-code': ['account-1'] });
  fx.cleanup();
});

test('`profile remove` takes the account out of every order, as it un-defaults it', () => {
  const fx = makeFixture('rotation-verb-remove');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-1', '--workspace', 'work'], at(fx));

  const removed = run(['profile', 'remove', 'claude-code', 'account-1'], at(fx));

  assert.match(removed.out, /no default and no order names it/);
  const settings = readHarnessSettings(at(fx));
  assert.deepEqual(settings.rotation, { 'claude-code': ['account-2'] });
  assert.deepEqual(settings.workspaceRotation, {});
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLE = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'RotationTwinTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLE, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'RotationTwinTests');

  const orders = rows('An_order_resolves_as_the_cli_resolves_it');
  const problems = rows('An_order_is_refused_as_the_cli_refuses_it');
  const edits = rows('An_order_is_set_and_cleared_as_the_cli_writes_it');
  const files = rows('Both_twins_write_the_same_file');
  assert.deepEqual(orders, heldSoFar(orders, ORDER_ROWS, ORDERS_OWED));
  assert.deepEqual(edits, heldSoFar(edits, EDIT_ROWS, EDITS_OWED));
  assert.deepEqual(problems, heldSoFar(problems, PROBLEM_ROWS, DRIVER_OWES));
  assert.deepEqual(rows('An_account_removed_leaves_the_wiring_as_the_cli_leaves_it'), REMOVE_ROWS);
  assert.deepEqual(files, heldSoFar(files, FILE_ROWS, FILES_OWED));
});
