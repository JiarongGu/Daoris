import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandHarness, profileHome, readHarnessSettings, writeHarnessSettings } from '../../src/agents/toolchain.ts';
import {
  CURVE_MOST_POINTS, CURVE_WINDOWS, SWITCHES, USE_DEFAULTS, USE_MODES, curveProblem, resolveScope, scopeProblem, withRotation, withUse,
} from '../../src/accounts/rotation.ts';
import type { RotationUse, UseChange } from '../../src/accounts/rotation.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { captureError, makeFixture } from '../_fixture.ts';

/**
 * How a scope's list is used (TOOL6a; D130 §2, §3.1, §4.6, §14, as §16.6 amends them; D165 point 2, SWITCH1a):
 * `rotationUse` and `workspaceRotationUse` in `harnesses.json`, beside the lists — *use accounts* (`use`: `goal` or
 * `order`), *keep for conversations* (`keep`), and when to switch (`switch`: `smart`, `curve` or `off`; and `curve`, the
 * person's own points per window), read on from the old names `early` and `near` — and the rule binding a scope's default
 * and kept account to its list. The CLI's half of a TWIN with the driver's `RotationUse.cs`:🔴 both hold the table in
 * `fixtures/rotation-use.json`, row for row, the driver's `RotationUseTwinTests` reading it as this file does.
 *
 * Nothing here counts accounts (§2 rule 6): a row with one account and a row with six are read by the same rules, and no
 * refusal is of a list for its length.
 */

type Cell = string | null;

const here = dirname(dirname(fileURLToPath(import.meta.url)));
const TABLE = JSON.parse(readFileSync(join(here, 'fixtures', 'rotation-use.json'), 'utf8')) as {
  read: [why: string, file: unknown, agent: string, workspace: Cell, from: string, list: string[] | null, begins: Cell, said: string][];
  write: [why: string, before: unknown, after: Record<string, unknown>][];
  change: [why: string, before: unknown, agent: string, change: UseChange | null, workspace: Cell, after: Record<string, unknown>][];
  order: [why: string, before: unknown, agent: string, order: string[] | null, workspace: Cell, after: Record<string, unknown>][];
  problem: [why: string, scopeDefault: Cell, list: string[], keep: Cell, refused: Cell][];
  refusal: [why: string, curve: string, refused: Cell][];
};

function at(fx: { root: string }): string {
  const path = homeEntry(fx.root, 'harnesses');
  mkdirSync(dirname(path), { recursive: true });
  return path;
}

/** A file cell: the text on disk where it is a string, else that JSON. */
function read(fx: { root: string }, file: unknown) {
  writeFileSync(at(fx), typeof file === 'string' ? file : JSON.stringify(file), 'utf8');
  return readHarnessSettings(at(fx));
}

/** The named sections of the file as written, those it holds. */
function written(fx: { root: string }, ...sections: string[]): Record<string, unknown> {
  const root = JSON.parse(readFileSync(at(fx), 'utf8')) as Record<string, unknown>;
  return Object.fromEntries(sections.filter((section) => section in root).map((section) => [section, root[section]]));
}

/** The sections a row's `after` names, as written: key for key, in order. */
function writtenAs(fx: { root: string }): string {
  return JSON.stringify(written(fx, 'rotationUse', 'workspaceRotationUse'));
}

/**
 * A scope's settings in one word each, today's defaults left out, then `?name` for each value or setting this build does
 * not know — the cell both twins compare. The defaults are spelled here on purpose, not read from `USE_DEFAULTS`: the
 * table says what today's defaults are, and a test below holds the constant to them.
 */
function said(use: RotationUse, unknown: string[]): string {
  const parts = [
    use.use !== 'goal' ? use.use : null,
    use.keep !== null ? `keep=${use.keep}` : null,
    use.switch !== 'smart' ? `switch=${use.switch}` : null,
    ...(['session', 'weekly'] as const).map((window) => {
      const points = use.curve[window];
      return points ? `${window}=${points.map(([elapsed, used]) => `${elapsed}:${used}`).join(',')}` : null;
    }),
    use.near !== 90 ? `near=${use.near}` : null,
    ...unknown.map((name) => `?${name}`),
  ].filter((part): part is string => part !== null);
  return parts.length > 0 ? parts.join(' ') : 'none';
}

test('today\'s defaults live in one place: make the most of them, keeping none, switching by Daoris\'s curve', () => {
  assert.deepEqual({ ...USE_DEFAULTS }, { use: 'goal', keep: null, switch: 'smart', curve: {}, early: true, near: 90 });
  assert.deepEqual([...USE_MODES], ['goal', 'order']);
  assert.deepEqual([...SWITCHES], ['smart', 'curve', 'off']);
  assert.deepEqual([...CURVE_WINDOWS], ['session', 'weekly']);
  assert.equal(CURVE_MOST_POINTS, 12);
});

// ——— Read and resolved (§2 rule 1, §14, §16.6; D165 §6): the scope a start reads, its list, where it begins, and its
// settings, the old names read on.

test('a scope is read as the driver reads it: its list, where it begins, and its settings only with that list (the shared table)', () => {
  const fx = makeFixture('rotation-use-scope');
  for (const [why, file, agent, workspace, from, list, begins, use] of TABLE.read) {
    const scope = resolveScope(read(fx, file), agent, workspace);
    assert.deepEqual(
      { from: scope.from, list: scope.list, begins: scope.begins, use: said(scope.use, scope.unknown) },
      { from, list: list ?? [], begins, use },
      why);
  }
  fx.cleanup();
});

// ——— Written (D165 §6): the old names written as `switch` and `curve`, never again as themselves; a curve in one order.

test('settings are written as the driver writes them, the old names as the new (the shared table)', () => {
  const fx = makeFixture('rotation-use-write');
  for (const [why, before, after] of TABLE.write) {
    writeHarnessSettings(at(fx), read(fx, before));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— Edited (§2, §16.6; D165 §6): a change merged into the scope's settings, each choice written as made, so a later
// default never overturns one; what this build does not know kept as written; `null` clears the scope's settings, and a
// scope with none is dropped. The old names, which the doors still take, are mapped onto the switch and the curve.

test('settings are set and cleared as the driver writes them (the shared table)', () => {
  const fx = makeFixture('rotation-use-edit');
  for (const [why, before, agent, edit, workspace, after] of TABLE.change) {
    writeHarnessSettings(at(fx), withUse(read(fx, before), agent, edit, workspace));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— A list's settings come with it (§2 rule 1): a list cleared takes its scope's settings; a list replaced keeps them.

test('a list\'s settings go with it, as the driver writes them (the shared table)', () => {
  const fx = makeFixture('rotation-use-order');
  for (const [why, before, agent, order, workspace, after] of TABLE.order) {
    writeHarnessSettings(at(fx), withRotation(read(fx, before), agent, order, workspace));
    assert.equal(writtenAs(fx), JSON.stringify(after), why);
  }
  fx.cleanup();
});

// ——— The rule binding a scope (§3.1, §4.6): its default and its kept account are of its list, and driven work keeps an
// account. The first problem is the one said; no list is refused for how many it names.

test('a scope is refused as the driver refuses it (the shared table)', () => {
  for (const [why, scopeDefault, list, keep, refused] of TABLE.problem) {
    const problem = scopeProblem({ default: scopeDefault, list, keep });
    assert.equal(problem === null ? null : `${problem.kind} ${problem.account}`, refused, why);
  }
});

// ——— A curve's rules (D165 point 2, the switch design §2): one to twelve whole-number points per window, elapsed from 0
// to 100 and rising, used from 0 to 100 and never falling; the first problem said, which window, which point, which rule.

test('a curve is refused as the driver refuses it, in the same sentence (the shared table)', () => {
  for (const [why, curve, refused] of TABLE.refusal) assert.equal(curveProblem(JSON.parse(curve)), refused, why);
});

// ——— The doors (§3.1, §4.6, §16.6; the switch design §5): `profile use`, and the refusals `profile order` and
// `profile default` gain.

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

test('`profile use` sets how the machine\'s list is used, and with `--workspace` one workspace\'s; `--early` and `--near` are the old names', () => {
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
  assert.match(machine.out, /switch accounts\s+Smart — a session moves at a turn's end when its use is over the curve; a start passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n/);
  assert.doesNotMatch(machine.out, /\n\s+curve\s/, 'no curve of its own, none shown');
  assert.match(machine.out, /Written to /);
  assert.equal(scoped.code, 0);
  assert.match(scoped.out, /in `work`, `claude-code`'s list is account-2, then account-1\./);
  assert.match(scoped.out, /use accounts\s+make the most of them\n/);
  // Off still passes an account over Daoris's curve at a start (SWITCH1b-ii); the next start's steps say the pace.
  assert.match(scoped.out, /switch accounts\s+Off — no session moves at a turn's end; a start still passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n/);
  assert.match(scoped.out, /curve\s+session 0:85 — kept, and read only under curve\n/);
  assert.deepEqual(written(fx, 'rotationUse', 'workspaceRotationUse'), {
    rotationUse: { 'claude-code': { use: 'order', keep: 'account-3' } },
    workspaceRotationUse: { work: { 'claude-code': { switch: 'off', curve: { session: [[0, 85]] } } } },
  });
  fx.cleanup();
});

test('`profile use --switch` and `--curve` set the switch and the person\'s curve, and `--no-curve` takes the curve out', () => {
  const fx = makeFixture('rotation-use-switch');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));

  const curved = run(['profile', 'use', 'claude-code', '--switch', 'curve', '--curve', 'weekly=0:30,50:70,100:100', '--curve', 'session=0:85'], at(fx));
  assert.equal(curved.code, 0);
  assert.match(curved.out, /switch accounts\s+My own curve — a session moves at a turn's end when its use is over the curve; a start passes an account whose limit is reached, that draws on usage credits, or whose use is over your curve, Daoris's smart curve on a window yours leaves out\n/);
  assert.match(curved.out, /curve\s+session 0:85; weekly 0:30, 50:70, 100:100\n/);
  assert.deepEqual(written(fx, 'rotationUse'), {
    rotationUse: { 'claude-code': { switch: 'curve', curve: { session: [[0, 85]], weekly: [[0, 30], [50, 70], [100, 100]] } } },
  });

  const week = run(['profile', 'use', 'claude-code', '--curve', 'weekly=0:20,100:95'], at(fx));
  assert.match(week.out, /curve\s+session 0:85; weekly 0:20, 100:95\n/, 'a window set replaces that window and keeps the other');

  const off = run(['profile', 'use', 'claude-code', '--switch', 'off'], at(fx));
  assert.match(off.out, /switch accounts\s+Off — no session moves at a turn's end; a start still passes [^\n]+ over Daoris's smart curve\n/);
  assert.match(off.out, /curve\s+session 0:85; weekly 0:20, 100:95 — kept, and read only under curve\n/);

  const none = run(['profile', 'use', 'claude-code', '--no-curve'], at(fx));
  assert.doesNotMatch(none.out, /\n\s+curve\s/);
  assert.deepEqual(written(fx, 'rotationUse'), { rotationUse: { 'claude-code': { switch: 'off' } } });

  const bare = run(['profile', 'use', 'claude-code', '--switch', 'curve'], at(fx));
  assert.match(bare.out, /curve\s+none of your own — each window takes Daoris's smart curve\n/);
  fx.cleanup();
});

test('`profile use` with no flag prints the scope\'s settings and what each account last said', () => {
  const fx = makeFixture('rotation-use-print');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  const ahead = new Date(Date.now() + 86_400_000).toISOString().replace(/\.\d+Z$/, 'Z');
  writeFileSync(fx.entry('cooling'), JSON.stringify({ 'claude-code': { 'account-2': { until: ahead, stated: true } } }), 'utf8');
  const before = readFileSync(at(fx), 'utf8');

  const printed = run(['profile', 'use', 'claude-code'], at(fx));
  const borrowed = run(['profile', 'use', 'claude-code', '--workspace', 'home'], at(fx));

  assert.equal(printed.code, 0);
  assert.match(printed.out, /use accounts\s+make the most of them/);
  assert.match(printed.out, /kept for conversations\s+none/);
  assert.match(printed.out, /switch accounts\s+Smart — /);
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
  const paced = run(['profile', 'use', 'claude-code', '--switch', 'off', '--no-keep'], at(fx)).out;
  const order = run(['profile', 'use', 'claude-code', 'order', '--workspace', 'work'], at(fx)).out;
  const one = run(['profile', 'use', 'claude-code', '--workspace', 'solo'], at(fx)).out;

  // The curve's steps (D165 point 2, the switch design §3): over its curve last, the least over first; the most room under
  // its curve at step 5; under `off`, the smart curve for step 2 and D130's pace for step 5.
  assert.match(goal, /next start\s+the ready account running the fewest of Daoris's sessions; then one whose week resets within a day; then the one with the most room under its curve; then the one Daoris started on least recently; then this list's order, from `account-1`; one whose limit is reached, that draws on usage credits or whose use is over its curve goes last, the least over first\n/);
  assert.match(goal, /\n {2}no account has said what it has left yet: Daoris spreads starts across them by its own sessions, and learns each account's weekly reset from the limits it meets\n/);
  assert.match(kept, /then this list's order, from `account-1`; driven work passes `account-3`, kept for conversations; one whose limit is reached, [^\n]+ goes last, the least over first\n/);
  assert.match(paced, /next start\s+the ready account running the fewest of Daoris's sessions; then one whose week resets within a day; then the one furthest behind its week's pace; then the one Daoris started on least recently; then this list's order, from `account-1`; one whose limit is reached, that draws on usage credits or whose use is over Daoris's smart curve goes last, the least over first\n/);
  assert.match(order, /next start\s+the first ready account of this list, from `account-1`, its default; one whose limit is reached, that draws on usage credits or whose use is over its curve goes last\n/);
  assert.doesNotMatch(order, /no account has said what it has left yet/);
  assert.match(one, /next start\s+`account-2`, the one account this list holds\n/);
  fx.cleanup();
});

test('`profile use` shows each account\'s last reading and its age, and says no account has said only while none has (TOOL6c)', () => {
  const fx = makeFixture('rotation-use-said');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  accounts(fx, 'codex', 'account-1', 'account-2');
  accounts(fx, 'dsh', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'order', 'codex', 'account-1', 'account-2'], at(fx));
  run(['profile', 'order', 'dsh', 'account-1', 'account-2'], at(fx));
  run(['profile', 'use', 'claude-code', '--near', '85'], at(fx));
  const stamp = (offset: number) => new Date(Date.now() + offset).toISOString().replace(/\.\d+Z$/, 'Z');
  writeFileSync(fx.entry('windows'), JSON.stringify({
    'claude-code': {
      'account-1': {
        session: { reset: stamp(2 * 3_600_000), used: 0.88, standing: 'clear', seen: stamp(-20 * 60_000), session: 's1' },
        weekly: { reset: stamp(100 * 3_600_000), used: 0.14, seen: stamp(-20 * 60_000), session: 's1' },
      },
    },
  }), 'utf8');

  const printed = run(['profile', 'use', 'claude-code'], at(fx)).out;
  const codex = run(['profile', 'use', 'codex'], at(fx)).out;
  const silent = run(['profile', 'use', 'dsh'], at(fx)).out;

  // `--near 85` is the five-hour window of the scope's curve, flat at 85: 88% is over it, said as the start's line says it.
  assert.match(printed, /account-1\s+said \d+ min ago: 88% of its session limit used, resetting [^;]+; 14% of its weekly limit used, resetting [^;]+; over its session curve's 85%\n/);
  assert.match(printed, /account-2\s+nothing said about what it has left/);
  assert.doesNotMatch(printed, /no account has said what it has left yet/);
  // Codex's accounts speak through its own app server, asked by the driver (CODEXUSE1b): the curve's steps, and none said yet.
  assert.match(codex, /switch accounts\s+Smart — a session moves at a turn's end when its use is over the curve; a start passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n/);
  assert.match(codex, /next start\s+the ready account running [^\n]*then the one with the most room under its curve;[^\n]*; one whose limit is reached, [^\n]+ goes last, the least over first\n/);
  assert.match(codex, /no account has said what it has left yet/);
  // An agent whose accounts do not say it passes nothing, says so, and walks without the curve's steps.
  assert.match(silent, /switch accounts\s+Smart — DeepSeek Harness's sessions here do not say how much of each window is used, so a start passes no account by its use\n/);
  assert.match(silent, /next start\s+the ready account running the fewest of Daoris's sessions; then one whose week resets within a day; then the one Daoris started on least recently;/);
  assert.match(silent, /no account has said what it has left yet/);
  fx.cleanup();
});

test('`profile use --clear` returns a scope to today\'s defaults, and refuses a setting beside it', () => {
  const fx = makeFixture('rotation-use-clear');
  accounts(fx, 'claude-code', 'account-1', 'account-2');
  run(['profile', 'order', 'claude-code', 'account-1', 'account-2'], at(fx));
  run(['profile', 'use', 'claude-code', 'order', '--switch', 'off'], at(fx));

  const both = captureError(() => run(['profile', 'use', 'claude-code', 'goal', '--clear'], at(fx)));
  const curve = captureError(() => run(['profile', 'use', 'claude-code', '--no-curve', '--clear'], at(fx)));
  const cleared = run(['profile', 'use', 'claude-code', '--clear'], at(fx));

  assert.match(both.message, /`--clear` sets no setting beside it/);
  assert.match(curve.message, /`--clear` sets no setting beside it/);
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
  const borrowed = captureError(() => run(['profile', 'use', 'claude-code', '--switch', 'off', '--workspace', 'home'], at(fx)));
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
  // What it sets, now that the walk reads the curve (SWITCH1b-iii): no number of a window at or over which an account is near.
  assert.match(refused('--near', '49'), /— e\.g\. `--near 85`: the old name of your curve's five-hour window, flat at that share; a start passes an account whose use of that window is over it\.$/);
  assert.match(refused('--near'), /`--near` needs a value/);
  assert.match(refused('--keep', 'account-1', '--no-keep'), /`--keep` and `--no-keep` together/);
  assert.match(refused('--switch', 'fast'), /`--switch` is smart, curve or off — smart, Daoris's curve; curve, your own \(`--curve`\); off, no switch while a session runs/);
  assert.match(refused('--switch', 'OFF'), /`--switch` is smart, curve or off/);
  assert.match(refused('--switch'), /`--switch` needs a value/);
  assert.match(refused('--curve'), /`--curve` needs a value/);
  assert.match(refused('--curve', 'weekly=0:abc'), /`--curve weekly=0:abc` is not a window and its points — it takes `<window>=<elapsed>:<used>,…`/);
  assert.match(refused('--curve', '0:30'), /`--curve 0:30` is not a window and its points/);
  assert.match(refused('--curve', 'weekly=0:30', '--curve', 'weekly=0:40'), /`--curve` names `weekly` twice — give each window once/);
  assert.match(refused('--curve', 'weekly=0:30', '--no-curve'), /`--curve` and `--no-curve` together/);
  // The curve's own rules say what the driver's say, word for word (the shared table's refusal rows).
  assert.match(refused('--curve', 'monthly=0:50'), /^`monthly` is not a window — a curve's windows are `session` and `weekly`\. /);
  assert.match(refused('--curve', 'weekly=0:30,50:20'), /^`weekly`'s point 2 has used 20%, less than point 1's 30% — used never falls\. /);
  assert.match(refused('--curve', 'session=0:150'), /^`session`'s point 1 has used 150% — used is from 0 to 100\. .*`--curve weekly=0:30,50:70,100:100`/);
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

    const set = run(['profile', 'use', 'claude-code', 'order', '--switch', 'off', '--curve', 'session=0:50'], at(fx));

    assert.equal(set.code, 0, `${n} accounts`);
    assert.deepEqual(written(fx, 'rotationUse'), { rotationUse: { 'claude-code': { use: 'order', switch: 'off', curve: { session: [[0, 50]] } } } }, `${n} accounts`);
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
    workspaceRotationUse: { work: { 'claude-code': { keep: 'account-2', weekly: 'pace', early: 'no', switch: 'eager' } } },
  });

  assert.match(plain, /rotation\s+account-1, then account-2\n\s+use accounts: make the most of them\n\s+kept for conversations: none\n\s+switch accounts: Smart — a session moves at a turn's end when its use is over the curve; a start passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n/);
  // The old names read on: `early: false` is off, and `near: 85` the session window flat at 85, kept under off.
  assert.match(set, /rotation\s+account-1, then account-2, then account-3\n\s+use accounts: one by one, in order — one limit stops every session on that account\n\s+kept for conversations: account-3\n\s+switch accounts: Off — no session moves at a turn's end; a start still passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n\s+curve: session 0:85 — kept, and read only under curve\n\s+its default, `account-9`, is not in this list: name it in the list, or make one of the list the default\n/);
  assert.match(set, /rotation in work account-2\n\s+use accounts: make the most of them\n\s+kept for conversations: account-2\n\s+switch accounts: Smart — a session moves at a turn's end when its use is over the curve; a start passes an account whose limit is reached, that draws on usage credits, or whose use is over Daoris's smart curve\n\s+`account-2` is kept for conversations, and this list holds no other account for driven work\n\s+`switch` holds a value this build does not know, so it reads as today's default; it is kept as written\n\s+`early` is an old name of the switch, and holds a value this build does not read: nothing reads it, and the next write takes it out\n\s+`weekly` is a setting this build does not know: nothing here reads it, and it is kept as written\n/);
  fx.cleanup();
});
