import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandHarness, profileHome, readHarnessSettings, writeHarnessSettings } from '../src/toolchain.ts';
import { USE_DEFAULTS, USE_MODES, resolveScope, scopeProblem, withRotation, withUse } from '../src/rotation.ts';
import type { RotationUse, UseChange } from '../src/rotation.ts';
import { COOLING_FILE } from '../src/cooling.ts';
import { driverRows as csharpRows } from './_csharp.ts';
import { captureError, makeFixture } from './_fixture.ts';

/**
 * How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14, as §16.6 amends them): `rotationUse` and
 * `workspaceRotationUse` in `harnesses.json`, beside the lists — *use accounts* (`use`: `goal` or `order`), *keep for
 * conversations* (`keep`), and *switch before the limit* (`early`, `near`) — and the rule binding a scope's default and
 * kept account to its list. The CLI's half of a TWIN with the driver's `Harnesses.Use.cs`: 🔴 `RotationUseTwinTests.cs`
 * holds the same tables, row for row and in the same order, and the last test here holds each to this one, cell for cell.
 *
 * Nothing here counts accounts (§2 rule 6): a row with one account and a row with six are read by the same rules, and no
 * refusal is of a list for its length.
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

/**
 * A scope's settings in one word each, today's defaults left out, then `?name` for each value or setting this build does
 * not know — the cell both tables compare. The defaults are spelled here on purpose, not read from `USE_DEFAULTS`: the
 * tables say what today's defaults are, and a test below holds the constant to them.
 */
function said(use: RotationUse, unknown: string[]): string {
  const parts = [
    use.use !== 'goal' ? use.use : null,
    use.keep !== null ? `keep=${use.keep}` : null,
    !use.early ? 'early=off' : null,
    use.near !== 90 ? `near=${use.near}` : null,
    ...unknown.map((name) => `?${name}`),
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(' ') : 'none';
}

/** A change as both tables spell it: JSON, where `"keep": null` keeps none, a field absent is left as it was, and `null` clears. */
function change(json: string): UseChange | null {
  return JSON.parse(json) as UseChange | null;
}

test('today\'s defaults live in one place: make the most of them, keeping none, switching before the limit at 90%', () => {
  assert.deepEqual({ ...USE_DEFAULTS }, { use: 'goal', keep: null, early: true, near: 90 });
  assert.deepEqual([...USE_MODES], ['goal', 'order']);
});

// ——— Read and resolved (§2 rule 1, §14, §16.6): the scope a start reads, its list, where it begins, and its settings.

const SCOPE_ROWS: [name: string, file: string, agent: string, workspace: Cell, from: string, list: Cell, begins: Cell, use: string][] = [
  ['nothing set anywhere is the machine\'s scope with no list, every setting its default', '{}', 'claude-code', null, 'machine', null, null, 'none'],
  ['a machine list and no settings: make the most of them, switching before the limit, beginning at its first', '{"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', 'none'],
  ['every setting as written', '{"rotation":{"claude-code":["account-1","account-2","account-3"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}}}', 'claude-code', null, 'machine', '["account-1","account-2","account-3"]', 'account-1', 'order keep=account-3 early=off near=85'],
  ['a choice equal to today\'s default is that choice', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"goal","early":true,"near":90}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'none'],
  ['a way to use accounts this build does not know reads as the default, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"pace"}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?use'],
  ['a way to use accounts in another case is one this build does not know', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"ORDER"}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?use'],
  ['switching before the limit is off only as JSON false', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":false}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'early=off'],
  ['a switch that is neither true nor false reads as on, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":"no"}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?early'],
  ['the retired prefer and parallel are skipped, and not said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"prefer":"left","parallel":true}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'none'],
  ['near at its lowest, 50, as written', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":50}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'near=50'],
  ['near at its highest, 99, as written', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":99}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'near=99'],
  ['near written as a whole number with a point is that number', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85.0}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'near=85'],
  ['near under 50 reads as 90, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":49}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?near'],
  ['near over 99 reads as 90, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":100}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?near'],
  ['near that is not whole reads as 90, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":85.5}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?near'],
  ['near that is not a number reads as 90, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"near":"85"}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', '?near'],
  ['a kept account is trimmed', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":" account-2 "}}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', 'keep=account-2'],
  ['a kept account not in the list is none', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":"account-9"}}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', 'none'],
  ['a kept account in another case than the list\'s is none', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":"ACCOUNT-2"}}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', 'none'],
  ['a kept account that is not a name reads as none, and is said', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"keep":["account-2"]}}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', '?keep'],
  ['a setting this build does not know changes nothing, and is said', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"weekly":"pace","use":"order"}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'order ?weekly'],
  ['settings with no list are not read', '{"rotationUse":{"claude-code":{"use":"order","early":false}}}', 'claude-code', null, 'machine', null, null, 'none'],
  ['settings that are not an object are none', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":"order"}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'none'],
  ['the machine\'s default in its list is where it begins', '{"defaults":{"claude-code":"account-2"},"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-2', 'none'],
  ['a default outside its list: the list wins, and it begins at its first', '{"defaults":{"claude-code":"account-9"},"rotation":{"claude-code":["account-1","account-2"]}}', 'claude-code', null, 'machine', '["account-1","account-2"]', 'account-1', 'none'],
  ['no list: the scope is its default alone', '{"defaults":{"claude-code":"account-2"}}', 'claude-code', null, 'machine', null, 'account-2', 'none'],
  ['a workspace with a list of its own reads its own settings, never the machine\'s', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}', 'claude-code', 'work', 'workspace', '["account-2","account-3"]', 'account-2', 'early=off'],
  ['a workspace with a list of its own and no settings has every setting its default', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}', 'claude-code', 'work', 'workspace', '["account-2","account-3"]', 'account-2', 'none'],
  ['a workspace that names its own default and no list is its own scope, that account alone', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaces":{"work":{"claude-code":"account-2"}}}', 'claude-code', 'work', 'workspace', null, 'account-2', 'none'],
  ['a workspace\'s own default in its own list is where it begins', '{"workspaces":{"work":{"claude-code":"account-3"}},"workspaceRotation":{"work":{"claude-code":["account-2","account-3"]}}}', 'claude-code', 'work', 'workspace', '["account-2","account-3"]', 'account-3', 'none'],
  ['a workspace that names nothing reads the machine\'s scope and settings', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2"]}}}', 'claude-code', 'home', 'machine', '["account-1","account-2"]', 'account-1', 'order'],
  ['a workspace\'s settings with no list of its own are not read', '{"rotation":{"claude-code":["account-1"]},"workspaces":{"work":{"claude-code":"account-1"}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}', 'claude-code', 'work', 'workspace', null, 'account-1', 'none'],
  ['a workspace\'s list for another agent is not this one\'s', '{"rotation":{"claude-code":["account-1"]},"workspaceRotation":{"work":{"codex":["account-9"]}},"workspaceRotationUse":{"work":{"codex":{"early":false}}}}', 'claude-code', 'work', 'machine', '["account-1"]', 'account-1', 'none'],
  ['one account in a list: every setting as written, nothing counted', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"order","early":false}}}', 'claude-code', null, 'machine', '["account-1"]', 'account-1', 'order early=off'],
  ['a file that does not read is none', 'not json', 'claude-code', null, 'machine', null, null, 'none'],
];

test('a scope is read as the driver reads it: its list, where it begins, and its settings only with that list', () => {
  const fx = makeFixture('rotation-use-scope');
  for (const [name, file, agent, workspace, from, list, begins, use] of SCOPE_ROWS) {
    const scope = resolveScope(read(fx, file), agent, workspace);
    assert.deepEqual(
      { from: scope.from, list: scope.list, begins: scope.begins, use: said(scope.use, scope.unknown) },
      { from, list: list === null ? [] : JSON.parse(list), begins, use },
      name);
  }
  fx.cleanup();
});

// ——— Edited (§2, §16.6): a change merged into the scope's settings, each choice written as made, so a later default
// never overturns one; what this build does not know kept as written; `null` clears the scope's settings, and a scope
// with none is dropped.

const EDIT_ROWS: [why: string, before: string, agent: string, change: string, workspace: Cell, after: string][] = [
  ['one by one, in order, set on the machine', '{}', 'claude-code', '{"use":"order"}', null, '{"rotationUse":{"claude-code":{"use":"order"}}}'],
  ['every setting set at once', '{}', 'claude-code', '{"near":85,"early":false,"keep":"account-3","use":"order"}', null, '{"rotationUse":{"claude-code":{"use":"order","keep":"account-3","early":false,"near":85}}}'],
  ['a setting changed keeps the others', '{"rotationUse":{"claude-code":{"use":"order","early":false}}}', 'claude-code', '{"use":"goal"}', null, '{"rotationUse":{"claude-code":{"use":"goal","early":false}}}'],
  ['a choice equal to today\'s default is written as chosen, so a later default does not overturn it', '{"rotationUse":{"claude-code":{"use":"order","early":false,"near":85}}}', 'claude-code', '{"use":"goal","early":true,"near":90}', null, '{"rotationUse":{"claude-code":{"use":"goal","early":true,"near":90}}}'],
  ['keeping none takes the kept account out, and nothing else', '{"rotationUse":{"claude-code":{"use":"order","keep":"account-3"}}}', 'claude-code', '{"keep":null}', null, '{"rotationUse":{"claude-code":{"use":"order"}}}'],
  ['keeping none where it was all that was set leaves the scope no settings', '{"rotationUse":{"claude-code":{"keep":"account-3"}}}', 'claude-code', '{"keep":null}', null, '{}'],
  ['another agent\'s settings are kept', '{"rotationUse":{"codex":{"early":false}}}', 'claude-code', '{"use":"order"}', null, '{"rotationUse":{"claude-code":{"use":"order"},"codex":{"early":false}}}'],
  ['a workspace\'s settings set, the machine\'s kept', '{"rotationUse":{"claude-code":{"use":"order"}}}', 'claude-code', '{"early":false}', 'work', '{"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}'],
  ['the machine\'s settings cleared, another agent\'s kept', '{"rotationUse":{"claude-code":{"use":"order","weekly":"pace"},"codex":{"early":false}}}', 'claude-code', 'null', null, '{"rotationUse":{"codex":{"early":false}}}'],
  ['a workspace\'s settings cleared, and a workspace left with none dropped', '{"workspaceRotationUse":{"work":{"claude-code":{"use":"order"}},"lab":{"codex":{"early":false}}}}', 'claude-code', 'null', 'work', '{"workspaceRotationUse":{"lab":{"codex":{"early":false}}}}'],
  ['a kept account is kept trimmed', '{}', 'claude-code', '{"keep":" account-2 "}', null, '{"rotationUse":{"claude-code":{"keep":"account-2"}}}'],
  ['a value and a setting this build does not know are kept when another is set', '{"rotationUse":{"claude-code":{"use":"pace","weekly":{"spread":true}}}}', 'claude-code', '{"early":false}', null, '{"rotationUse":{"claude-code":{"use":"pace","early":false,"weekly":{"spread":true}}}}'],
  ['a value this build does not know is replaced when that setting is set', '{"rotationUse":{"claude-code":{"use":"pace"}}}', 'claude-code', '{"use":"order"}', null, '{"rotationUse":{"claude-code":{"use":"order"}}}'],
  ['the retired prefer and parallel go with any edit', '{"rotationUse":{"claude-code":{"prefer":"left","parallel":true,"near":85}}}', 'claude-code', '{"use":"order"}', null, '{"rotationUse":{"claude-code":{"use":"order","near":85}}}'],
  ['no change changes nothing', '{"rotationUse":{"claude-code":{"near":85}}}', 'claude-code', '{}', null, '{"rotationUse":{"claude-code":{"near":85}}}'],
];

test('settings are set and cleared as the driver writes them (the twin\'s table)', () => {
  const fx = makeFixture('rotation-use-edit');
  for (const [why, before, agent, edit, workspace, after] of EDIT_ROWS) {
    writeHarnessSettings(at(fx), withUse(read(fx, before), agent, change(edit), workspace));
    assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), JSON.parse(after), why);
  }
  fx.cleanup();
});

// ——— A list's settings come with it (§2 rule 1): a list cleared takes its scope's settings; a list replaced keeps them.

const ORDER_ROWS: [why: string, before: string, agent: string, order: Cell, workspace: Cell, after: string][] = [
  ['the machine\'s list cleared takes its settings with it, another agent\'s kept', '{"rotation":{"claude-code":["account-1"],"codex":["account-1"]},"rotationUse":{"claude-code":{"use":"order"},"codex":{"early":false}}}', 'claude-code', null, null, '{"rotationUse":{"codex":{"early":false}}}'],
  ['a workspace\'s list cleared takes its settings with it, the machine\'s kept', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"use":"order"}},"workspaceRotation":{"work":{"claude-code":["account-2"]}},"workspaceRotationUse":{"work":{"claude-code":{"early":false}}}}', 'claude-code', null, 'work', '{"rotationUse":{"claude-code":{"use":"order"}}}'],
  ['a list replaced keeps its settings', '{"rotation":{"claude-code":["account-1","account-2"]},"rotationUse":{"claude-code":{"use":"order","keep":"account-2"}}}', 'claude-code', '["account-2","account-1"]', null, '{"rotationUse":{"claude-code":{"use":"order","keep":"account-2"}}}'],
  ['a list of nobody is a clear, and its settings go', '{"rotation":{"claude-code":["account-1"]},"rotationUse":{"claude-code":{"early":false}}}', 'claude-code', '[]', null, '{}'],
];

test('a list\'s settings go with it, as the driver writes them', () => {
  const fx = makeFixture('rotation-use-order');
  for (const [why, before, agent, order, workspace, after] of ORDER_ROWS) {
    writeHarnessSettings(at(fx), withRotation(read(fx, before), agent, order === null ? null : JSON.parse(order), workspace));
    assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), JSON.parse(after), why);
  }
  fx.cleanup();
});

// ——— The rule binding a scope (§3.1, §4.6): its default and its kept account are of its list, and driven work keeps an
// account. The first problem is the one said; no list is refused for how many it names.

const PROBLEM_ROWS: [why: string, scopeDefault: Cell, list: string, keep: Cell, refused: Cell][] = [
  ['a default in its list, and a kept account beside another', 'account-2', '["account-1","account-2"]', 'account-1', null],
  ['no list: a default is its one account, and nothing is refused', 'account-9', '[]', null, null],
  ['one account, its default, keeping none: nothing is counted', 'account-1', '["account-1"]', null, null],
  ['six accounts, nothing counted', 'account-5', '["account-1","account-2","account-3","account-4","account-5","account-6"]', 'account-6', null],
  ['no default and no kept account', null, '["account-1"]', null, null],
  ['a default outside its list', 'account-9', '["account-1","account-2"]', null, 'default account-9'],
  ['a default in another case than the list\'s is outside it', 'Account-1', '["account-1"]', null, 'default Account-1'],
  ['a kept account outside its list', null, '["account-1","account-2"]', 'account-9', 'keep account-9'],
  ['a kept account with no list is outside it', null, '[]', 'account-1', 'keep account-1'],
  ['keeping the one account a list holds leaves driven work none', null, '["account-1"]', 'account-1', 'alone account-1'],
  ['the default is said before the kept account', 'account-9', '["account-1"]', 'account-8', 'default account-9'],
];

test('a scope is refused as the driver refuses it', () => {
  for (const [why, scopeDefault, list, keep, refused] of PROBLEM_ROWS) {
    const problem = scopeProblem({ default: scopeDefault, list: JSON.parse(list), keep });
    assert.equal(problem === null ? null : `${problem.kind} ${problem.account}`, refused, why);
  }
});

// ——— The doors (§3.1, §4.6, §16.6): `profile use`, and the refusals `profile order` and `profile default` gain.

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

/** The accounts `account-1` … `account-n`, for a door that must not care how many. */
function many(n: number): string[] {
  return Array.from({ length: n }, (_, index) => `account-${index + 1}`);
}

test('`profile use` sets how the machine\'s list is used, and with `--workspace` one workspace\'s', () => {
  const fx = makeFixture('rotation-use-verb');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-3');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2', 'account-3'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-2', 'account-1', '--workspace', 'work'], at(fx));

  const machine = run(['profile', 'use', 'claude-code', 'order', '--keep', 'account-3'], at(fx));
  const scoped = run(['profile', 'use', 'claude-code', '--early', 'off', '--near', '85', '--workspace', 'work'], at(fx));

  assert.equal(machine.code, 0);
  assert.match(machine.out, /on this machine, `claude-code`'s list is account-1, then account-2, then account-3\./);
  assert.match(machine.out, /use accounts\s+one by one, in order — one limit stops every session on that account/);
  assert.match(machine.out, /kept for conversations\s+account-3/);
  assert.match(machine.out, /switch before the limit\s+on, at 90% — Claude Code's sessions here do not say how near their limits are/);
  assert.match(machine.out, /Written to /);
  assert.equal(scoped.code, 0);
  assert.match(scoped.out, /in `work`, `claude-code`'s list is account-2, then account-1\./);
  assert.match(scoped.out, /use accounts\s+make the most of them\n/);
  assert.match(scoped.out, /switch before the limit\s+off \(near: 85%\)/);
  assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), {
    rotationUse: { 'claude-code': { use: 'order', keep: 'account-3' } },
    workspaceRotationUse: { work: { 'claude-code': { early: false, near: 85 } } },
  });
  fx.cleanup();
});

test('`profile use` with no flag prints the scope\'s settings and what each account last said', () => {
  const fx = makeFixture('rotation-use-print');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  const ahead = new Date(Date.now() + 86_400_000).toISOString().replace(/\.\d+Z$/, 'Z');
  writeFileSync(join(fx.root, COOLING_FILE), JSON.stringify({ 'claude-code': { 'account-2': { until: ahead, stated: true } } }), 'utf8');
  const before = readFileSync(at(fx), 'utf8');

  const printed = run(['profile', 'use', 'claude-code'], at(fx));
  const borrowed = run(['profile', 'use', 'claude-code', '--workspace', 'home'], at(fx));

  assert.equal(printed.code, 0);
  assert.match(printed.out, /use accounts\s+make the most of them/);
  assert.match(printed.out, /kept for conversations\s+none/);
  assert.match(printed.out, /switch before the limit\s+on, at 90%/);
  assert.match(printed.out, /account-1\s+nothing said about what it has left/);
  assert.match(printed.out, /account-2\s+cooling until .* — as the agent said/);
  assert.match(borrowed.out, /`home` names no account of its own for `claude-code`, so it uses this machine's: account-1, then account-2\./);
  assert.equal(readFileSync(at(fx), 'utf8'), before, 'a look writes nothing');
  fx.cleanup();
});

test('`profile use` says the step the next start would follow (TOOL6b), with one account and with many', () => {
  const fx = makeFixture('rotation-use-next');
  accounts(fx, 'claude-code', ...many(3));
  run(['profile', 'order', 'claude-code', ...many(3)], at(fx));
  run(['profile', 'order', 'claude-code', 'account-3', 'account-1', '--workspace', 'work'], at(fx));
  run(['profile', 'default', 'claude-code', 'account-1', '--workspace', 'work'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-2', '--workspace', 'solo'], at(fx));

  const goal = run(['profile', 'use', 'claude-code'], at(fx)).out;
  const kept = run(['profile', 'use', 'claude-code', '--keep', 'account-3'], at(fx)).out;
  const order = run(['profile', 'use', 'claude-code', 'order', '--workspace', 'work'], at(fx)).out;
  const one = run(['profile', 'use', 'claude-code', '--workspace', 'solo'], at(fx)).out;

  assert.match(goal, /next start\s+the ready account running the fewest of Daoris's sessions; then one whose week resets within a day; then the one Daoris started on least recently; then this list's order, from `account-1`\n/);
  assert.match(goal, /\n {2}no account has said what it has left yet: Daoris spreads starts across them by its own sessions, and learns each account's weekly reset from the limits it meets\n/);
  assert.match(kept, /then this list's order, from `account-1`; driven work passes `account-3`, kept for conversations\n/);
  assert.match(order, /next start\s+the first ready account of this list, from `account-1`, its default\n/);
  assert.doesNotMatch(order, /no account has said what it has left yet/);
  assert.match(one, /next start\s+`account-2`, the one account this list holds\n/);
  fx.cleanup();
});

test('`profile use --clear` returns a scope to today\'s defaults, and refuses a setting beside it', () => {
  const fx = makeFixture('rotation-use-clear');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'use', 'claude-code', 'order', '--early', 'off'], at(fx));

  const both = captureError(() => run(['profile', 'use', 'claude-code', 'goal', '--clear'], at(fx)));
  const cleared = run(['profile', 'use', 'claude-code', '--clear'], at(fx));

  assert.match(both.message, /`--clear` sets no setting beside it/);
  assert.match(cleared.out, /on this machine, `claude-code` sets nothing of its own about how its list is used: today's defaults apply/);
  assert.deepEqual(written(fx, 'rotationUse'), {});
  fx.cleanup();
});

test('`profile use` refuses a scope with no list, naming the door that gives it one, and writes nothing', () => {
  const fx = makeFixture('rotation-use-nolist');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'default', 'claude-code', 'account-2', '--workspace', 'work'], at(fx));

  const own = captureError(() => run(['profile', 'use', 'claude-code', 'order', '--workspace', 'work'], at(fx)));
  const borrowed = captureError(() => run(['profile', 'use', 'claude-code', '--early', 'off', '--workspace', 'home'], at(fx)));
  const look = run(['profile', 'use', 'claude-code', '--workspace', 'work'], at(fx));
  run(['profile', 'order', 'claude-code', '--clear'], at(fx));
  const machine = captureError(() => run(['profile', 'use', 'claude-code', 'order'], at(fx)));

  assert.match(own.message, /`work` has no list of its own for `claude-code`.*`daoris agent profile order claude-code <account>… --workspace work` gives it one/);
  assert.match(borrowed.message, /`home` has no list of its own for `claude-code`.*until then it uses this machine's/);
  assert.match(look.out, /`work` names its own account for `claude-code`, `account-2`, and no list of its own/);
  assert.match(machine.message, /`claude-code` has no list on this machine.*`daoris agent profile order claude-code <account>…` sets one/);
  assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), {});
  fx.cleanup();
});

test('`profile use` refuses what is not a setting, naming what is', () => {
  const fx = makeFixture('rotation-use-values');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  const refused = (...flags: string[]) => captureError(() => run(['profile', 'use', 'claude-code', ...flags], at(fx))).message;

  assert.match(refused('fastest'), /`fastest` is not a way to use accounts — goal \(make the most of them\) or order \(one by one, in order\)/);
  assert.match(refused('account-1'), /An account is kept for conversations with `--keep <account>`/);
  assert.match(refused('goal', 'order'), /takes one way to use accounts, and `goal order` names more/);
  assert.match(refused('--early', '1'), /`--early` is on or off/);
  for (const near of ['49', '100', '85.5', 'most']) assert.match(refused('--near', near), /`--near` is a whole percent from 50 to 99/, near);
  assert.match(refused('--near'), /`--near` needs a value/);
  assert.match(refused('--keep', 'account-1', '--no-keep'), /`--keep` and `--no-keep` together/);
  // §16.6 retired *start on* and *sessions at once*: their flags are refused like any flag this door does not take.
  assert.match(refused('--prefer', 'left'), /`--prefer` is not a flag of `agent profile use` — it takes \[goal\|order\]/);
  assert.match(refused('--parallel', 'on'), /`--parallel` is not a flag of `agent profile use`/);
  assert.deepEqual(written(fx, 'rotationUse'), {});
  fx.cleanup();
});

test('`profile use --keep` keeps an account of the list, and never the one account driven work would have', () => {
  const fx = makeFixture('rotation-use-keep');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-9');
  run(['profile', 'order', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2', '--workspace', 'work'], at(fx));

  const outside = captureError(() => run(['profile', 'use', 'claude-code', '--keep', 'account-9', '--workspace', 'work'], at(fx)));
  const alone = captureError(() => run(['profile', 'use', 'claude-code', '--keep', 'account-1'], at(fx)));
  const kept = run(['profile', 'use', 'claude-code', '--keep', 'account-2', '--workspace', 'work'], at(fx));
  const cleared = run(['profile', 'use', 'claude-code', '--no-keep', '--workspace', 'work'], at(fx));

  assert.match(outside.message, /`account-9` is not in `claude-code`'s list in `work` \(account-1, then account-2\) — the kept account is one of the list/);
  assert.match(alone.message, /`claude-code`'s list on this machine holds no account but `account-1`, so keeping it for conversations would leave driven work none/);
  assert.match(alone.message, /`daoris agent profile order claude-code account-1 <account>…` adds one/);
  assert.match(kept.out, /kept for conversations\s+account-2/);
  assert.match(cleared.out, /kept for conversations\s+none/);
  assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), {});
  fx.cleanup();
});

test('`profile use` takes every setting with one account and with many: nothing counts them', () => {
  for (const n of [1, 6]) {
    const fx = makeFixture(`rotation-use-count-${n}`);
    accounts(fx, 'claude-code', ...many(n));
    run(['profile', 'order', 'claude-code', ...many(n)], at(fx));

    const set = run(['profile', 'use', 'claude-code', 'order', '--early', 'off', '--near', '50'], at(fx));

    assert.equal(set.code, 0, `${n} accounts`);
    assert.deepEqual(written(fx, 'rotationUse'), { rotationUse: { 'claude-code': { use: 'order', early: false, near: 50 } } }, `${n} accounts`);
    fx.cleanup();
  }
});

test('`profile use` on a door sets its owner\'s, and says so', () => {
  const fx = makeFixture('rotation-use-door');
  accounts(fx, 'claude-code', 'account-1');
  run(['profile', 'order', 'claude-code', 'account-1'], at(fx));

  const door = run(['profile', 'use', 'claude-code-acp', 'order'], at(fx));

  assert.match(door.out, /runs as `claude-code`/);
  assert.deepEqual(written(fx, 'rotationUse'), { rotationUse: { 'claude-code': { use: 'order' } } });
  fx.cleanup();
});

test('`profile order` refuses a list without its scope\'s default or kept account, naming both and the fix', () => {
  const fx = makeFixture('rotation-use-order-refused');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-3');
  run(['profile', 'default', 'claude-code', 'account-1'], at(fx));
  run(['profile', 'default', 'claude-code', 'account-2', '--workspace', 'work'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2', 'account-3'], at(fx));
  run(['profile', 'use', 'claude-code', '--keep', 'account-3'], at(fx));
  const before = readFileSync(at(fx), 'utf8');

  const machine = captureError(() => run(['profile', 'order', 'claude-code', 'account-2', 'account-3'], at(fx)));
  const scoped = captureError(() => run(['profile', 'order', 'claude-code', 'account-1', 'account-3', '--workspace', 'work'], at(fx)));
  const kept = captureError(() => run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx)));
  const alone = captureError(() => run(['profile', 'order', 'claude-code', 'account-3'], at(fx)));

  assert.match(machine.message, /this machine's default for `claude-code` is `account-1`, and this list does not hold it — the list is every account its starts may run on, and the default is where they begin within it/);
  assert.match(machine.message, /`daoris agent profile order claude-code account-2 account-3 account-1`.*`daoris agent profile default claude-code account-2`/);
  assert.match(scoped.message, /`work`'s default for `claude-code` is `account-2`, and this list does not hold it/);
  assert.match(scoped.message, /`daoris agent profile order claude-code account-1 account-3 account-2 --workspace work`.*`daoris agent profile default claude-code account-1 --workspace work`/);
  assert.match(kept.message, /`account-3` is kept for conversations on this machine, and this list does not hold it — the kept account is one of the list/);
  assert.match(kept.message, /`daoris agent profile use claude-code --no-keep`/);
  assert.match(alone.message, /this machine's default for `claude-code` is `account-1`/, 'the default is said first');
  assert.equal(readFileSync(at(fx), 'utf8'), before, 'a refusal writes nothing');

  run(['profile', 'default', 'claude-code', '--clear'], at(fx));
  const keptAlone = captureError(() => run(['profile', 'order', 'claude-code', 'account-3'], at(fx)));
  assert.match(keptAlone.message, /`account-3` is kept for conversations, and this list holds no other account, so driven work would have none/);
  fx.cleanup();
});

test('`profile default` refuses an account its scope\'s list does not hold, naming the list and the fix', () => {
  const fx = makeFixture('rotation-use-default-refused');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-3');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-3'], at(fx));
  run(['profile', 'order', 'claude-code', 'account-2', '--workspace', 'work'], at(fx));

  const machine = captureError(() => run(['profile', 'default', 'claude-code', 'account-2'], at(fx)));
  const scoped = captureError(() => run(['profile', 'default', 'claude-code', 'account-1', '--workspace', 'work'], at(fx)));
  const inList = run(['profile', 'default', 'claude-code', 'account-3'], at(fx));
  // A workspace with no list of its own takes any account as its own: it is then that account alone.
  const ownScope = run(['profile', 'default', 'claude-code', 'account-2', '--workspace', 'lab'], at(fx));

  assert.match(machine.message, /`claude-code`'s list on this machine is account-1, then account-3, and `account-2` is not in it — the list is every account its starts may run on, and the default is where they begin within it/);
  assert.match(machine.message, /`daoris agent profile order claude-code account-1 account-3 account-2` adds it/);
  assert.match(scoped.message, /`claude-code`'s list in `work` is account-2, and `account-1` is not in it/);
  assert.match(scoped.message, /`daoris agent profile order claude-code account-2 account-1 --workspace work` adds it/);
  assert.equal(inList.code, 0);
  assert.equal(ownScope.code, 0);
  const settings = readHarnessSettings(at(fx));
  assert.equal(settings.defaults['claude-code'], 'account-3');
  assert.equal(settings.workspaces.lab?.['claude-code'], 'account-2');
  assert.equal(settings.workspaces.work, undefined);
  fx.cleanup();
});

test('`profile order` and `profile default` take one account and many alike: no list is refused for its length', () => {
  for (const n of [1, 6]) {
    const fx = makeFixture(`rotation-use-length-${n}`);
    accounts(fx, 'claude-code', ...many(n));

    const ordered = run(['profile', 'order', 'claude-code', ...many(n)], at(fx));
    const defaulted = run(['profile', 'default', 'claude-code', `account-${n}`], at(fx));
    const reordered = run(['profile', 'order', 'claude-code', ...many(n).reverse()], at(fx));

    assert.deepEqual([ordered.code, defaulted.code, reordered.code], [0, 0, 0], `${n} accounts`);
    fx.cleanup();
  }
});

test('`profile remove` takes a kept account out of every scope\'s settings', () => {
  const fx = makeFixture('rotation-use-remove');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'use', 'claude-code', 'order', '--keep', 'account-2'], at(fx));

  run(['profile', 'remove', 'claude-code', 'account-2'], at(fx));

  assert.deepEqual(written(fx, 'rotationUse'), { rotationUse: { 'claude-code': { use: 'order' } } });
  fx.cleanup();
});

// ——— `daoris agent list` (§3.1, §3.2, §6, §16.6): how each list is used beneath it, a default outside its list named,
// and what this build does not know said. Every agent is pinned to a version nothing installed, so the listing asks no
// tool anything.

function listed(fx: { root: string }, wiring: Record<string, unknown>): string {
  const nowhere = { 'claude-code': '0.0.0-none', 'claude-code-acp': '0.0.0-none', codex: '0.0.0-none', 'codex-acp': '0.0.0-none', dsh: '0.0.0-none' };
  writeFileSync(at(fx), JSON.stringify({ versions: nowhere, ...wiring }), 'utf8');
  return run(['list'], at(fx)).out;
}

test('`agent list` says how each list is used beneath it, the machine\'s and each workspace\'s own', () => {
  const fx = makeFixture('rotation-use-list');
  accounts(fx, 'claude-code', 'account-1', 'account-2', 'account-3');

  const plain = listed(fx, { rotation: { 'claude-code': ['account-1', 'account-2'] } });
  const set = listed(fx, {
    defaults: { 'claude-code': 'account-9' },
    rotation: { 'claude-code': ['account-1', 'account-2', 'account-3'] },
    rotationUse: { 'claude-code': { use: 'order', keep: 'account-3', early: false, near: 85 } },
    workspaceRotation: { work: { 'claude-code': ['account-2'] } },
    workspaceRotationUse: { work: { 'claude-code': { keep: 'account-2', near: 80, weekly: 'pace', early: 'no' } } },
  });

  assert.match(plain, /rotation\s+account-1, then account-2\n\s+use accounts: make the most of them\n\s+kept for conversations: none\n\s+switch before the limit: on, at 90% — Claude Code's sessions here do not say how near their limits are\n/);
  assert.match(set, /rotation\s+account-1, then account-2, then account-3\n\s+use accounts: one by one, in order — one limit stops every session on that account\n\s+kept for conversations: account-3\n\s+switch before the limit: off \(near: 85%\)\n\s+its default, `account-9`, is not in this list: name it in the list, or make one of the list the default\n/);
  assert.match(set, /rotation in work account-2\n\s+use accounts: make the most of them\n\s+kept for conversations: account-2\n\s+switch before the limit: on, at 80% — Claude Code's sessions here do not say how near their limits are\n\s+`account-2` is kept for conversations, and this list holds no other account for driven work\n\s+`early` holds a value this build does not know, so it reads as today's default; it is kept as written\n\s+`weekly` is a setting this build does not know: nothing here reads it, and it is kept as written\n/);
  fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

const DRIVER_TABLE = join(
  dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'RotationUseTwinTests.cs');

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(DRIVER_TABLE, 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'RotationUseTwinTests');

  assert.deepEqual(rows('A_scope_is_read_as_the_cli_reads_it'), SCOPE_ROWS);
  assert.deepEqual(rows('Settings_are_set_and_cleared_as_the_cli_writes_them'), EDIT_ROWS);
  assert.deepEqual(rows('A_lists_settings_go_with_it_as_the_cli_writes_them'), ORDER_ROWS);
  assert.deepEqual(rows('A_scope_is_refused_as_the_cli_refuses_it'), PROBLEM_ROWS);
});
