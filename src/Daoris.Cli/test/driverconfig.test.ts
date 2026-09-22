import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandDriver, driverConfigPath, readDriverChoices } from '../src/driverconfig.ts';
import { makeFixture, captureError } from './_fixture.ts';

/**
 * `daoris driver` — the machine's standing driving choices from a terminal (D50).
 *
 * The driver re-reads `driver.json` every tick, which is what makes parity cheap: the desktop's
 * checkboxes and these verbs edit the same file, and hand-editing keeps working because the FILE is
 * the truth. A headless machine has no checkbox and still has to be told what it may drive.
 */

function at(fx: { root: string }): string {
  return join(fx.root, 'driver.json');
}

function run(argv: string[], path: string): { code: number; out: string } {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = commandDriver({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }) as number;
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

test('the path is the override, or the conventional home beside the other wiring files', () => {
  assert.equal(driverConfigPath({ DAORIS_DRIVER_CONFIG: '/x/d.json' }), '/x/d.json');
  assert.match(driverConfigPath({}), /[\\/]\.daoris[\\/]driver\.json$/);
});

test('a machine that opted nothing in says so, and says how to opt something in', () => {
  const fx = makeFixture('driver-empty');
  const result = run(['list'], at(fx));

  assert.equal(result.code, 0);
  assert.match(result.out, /drives no repository/);
  assert.match(result.out, /daoris driver drive <repository>/);
  fx.cleanup();
});

test('drive opts a repository in; undrive takes it back out', () => {
  const fx = makeFixture('driver-drive');

  run(['drive', 'aurora-engine'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).drivable, ['aurora-engine']);

  // Driving is additive, and the sentence says so — a person opting a repository in deserves to know
  // it does not take the repository over.
  assert.match(run(['drive', 'other'], at(fx)).out, /additive/);

  run(['undrive', 'aurora-engine'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).drivable, ['other']);
  fx.cleanup();
});

test('opting a repository in twice leaves one entry, not two', () => {
  const fx = makeFixture('driver-twice');
  run(['drive', 'engine'], at(fx));
  run(['drive', 'engine'], at(fx));

  assert.deepEqual(readDriverChoices(at(fx)).drivable, ['engine']);
  fx.cleanup();
});

test('hold pauses something still opted in; resume releases it', () => {
  const fx = makeFixture('driver-hold');
  run(['drive', 'engine'], at(fx));
  run(['hold', 'engine'], at(fx));

  const listed = run(['list'], at(fx));
  // A hold does not un-opt-in: the repository is still drivable, just not now.
  assert.deepEqual(readDriverChoices(at(fx)).drivable, ['engine']);
  assert.match(listed.out, /held by you/);

  run(['resume', 'engine'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).holds, []);
  fx.cleanup();
});

/** A hold on something nobody opted in reads as protection and is not. It says so. */
test('a hold on something not drivable is reported as changing nothing', () => {
  const fx = makeFixture('driver-inert');
  run(['hold', 'never-opted-in'], at(fx));

  assert.match(run(['list'], at(fx)).out, /the hold changes nothing/);
  fx.cleanup();
});

/**
 * The trap this command most easily falls into: the driver writes fields the CLI has no verb for,
 * and an editor that rewrote the file from its own idea of the shape would silently delete the
 * command that makes the stub adapter run — the family rehearsal's whole driver phase, gone.
 */
test('an edit preserves the fields this command has no verb for', () => {
  const fx = makeFixture('driver-preserve');
  writeFileSync(at(fx), `${JSON.stringify({
    drivable: ['engine'],
    holds: [],
    cap: 2,
    adapter: 'stub',
    timeoutMinutes: 45,
    pollSeconds: 9,
    commands: { stub: ['node', 'agent.mjs'] },
  }, null, 2)}\n`, 'utf8');

  run(['hold', 'engine'], at(fx));

  const back = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(back.commands, { stub: ['node', 'agent.mjs'] });
  assert.equal(back.timeoutMinutes, 45);
  assert.equal(back.pollSeconds, 9);
  assert.deepEqual(back.holds, ['engine']);
  fx.cleanup();
});

test('cap refuses anything that is not a whole number of sessions', () => {
  const fx = makeFixture('driver-cap');

  run(['cap', '3'], at(fx));
  assert.equal(readDriverChoices(at(fx)).cap, 3);

  for (const bad of ['0', '-1', 'lots', '1.5']) {
    assert.match(captureError(() => run(['cap', bad], at(fx))).message, /whole number/);
  }

  fx.cleanup();
});

/**
 * The adapter set belongs to the driver, not the CLI: a list here would refuse a harness that
 * actually works. It notes what it does not manage a toolchain for, and lets the driver name what
 * exists (D23) — the answer from the side that knows.
 */
test('adapter sets the name and notes when no toolchain is managed for it', () => {
  const fx = makeFixture('driver-adapter');

  const known = run(['adapter', 'claude-code'], at(fx));
  assert.equal(readDriverChoices(at(fx)).adapter, 'claude-code');
  assert.doesNotMatch(known.out, /manages no toolchain/);

  const stub = run(['adapter', 'stub'], at(fx));
  assert.equal(readDriverChoices(at(fx)).adapter, 'stub');
  assert.match(stub.out, /manages no toolchain/);
  fx.cleanup();
});

/**
 * Session trees (D51/SURF3): whether a repository's sessions open their own worktree instead of
 * running in the registered root. One standing flag, per repository — `on|off` rather than a verb
 * pair, because unlike drive/hold the two directions do not mean different things.
 */
test('trees opts a repository into session worktrees, and off takes it back out', () => {
  const fx = makeFixture('driver-trees');

  const on = run(['trees', 'engine', 'on'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).trees, ['engine']);
  // The price is stated where the choice is made: a fresh tree holds nothing git does not track,
  // so a repository whose gates need installed dependencies pays that per tree.
  assert.match(on.out, /nothing git does not track/);

  run(['trees', 'engine', 'off'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).trees, []);
  fx.cleanup();
});

test('trees needs on or off, and says so', () => {
  const fx = makeFixture('driver-trees-arg');
  assert.match(captureError(() => run(['trees', 'engine'], at(fx))).message, /on\|off/);
  assert.match(captureError(() => run(['trees', 'engine', 'maybe'], at(fx))).message, /on\|off/);
  fx.cleanup();
});

test('the trees list survives edits made by verbs that do not know it', () => {
  const fx = makeFixture('driver-trees-preserve');
  run(['trees', 'engine', 'on'], at(fx));

  run(['drive', 'engine'], at(fx));
  run(['cap', '3'], at(fx));

  assert.deepEqual(readDriverChoices(at(fx)).trees, ['engine']);
  fx.cleanup();
});

test('list names the repositories whose sessions get their own tree', () => {
  const fx = makeFixture('driver-trees-list');
  run(['drive', 'engine'], at(fx));
  run(['trees', 'engine', 'on'], at(fx));

  assert.match(run(['list'], at(fx)).out, /own tree/);
  fx.cleanup();
});

/**
 * A file the command cannot parse is reported, never silently replaced: the driver reads the same
 * file, and rewriting it would destroy whatever the person was in the middle of typing.
 */
test('an unreadable file is refused rather than overwritten', () => {
  const fx = makeFixture('driver-torn');
  writeFileSync(at(fx), '{ half a file', 'utf8');

  const error = captureError(() => run(['drive', 'engine'], at(fx)));

  assert.match(error.message, /not readable JSON/);
  assert.equal(readFileSync(at(fx), 'utf8'), '{ half a file');
  fx.cleanup();
});

test('a verb with no name says what it needed', () => {
  const fx = makeFixture('driver-nameless');
  assert.match(captureError(() => run(['drive'], at(fx))).message, /needs a name/);
  fx.cleanup();
});

test('an unknown verb names the ones that exist', () => {
  const fx = makeFixture('driver-verb');
  const error = captureError(() => run(['frobnicate'], at(fx)));

  assert.match(error.message, /unknown driver verb 'frobnicate'/);
  assert.match(
    error.message, /list, drive, undrive, hold, resume, trees, notify, strikes, retry, cap, adapter/);
  fx.cleanup();
});

/**
 * Notifications (SURF5b, design §4). The desktop has a checkbox and a machine with no screen has
 * this — D50's two doors onto one file. The setting governs the JUDGEMENT ("is this worth
 * interrupting somebody for"), not the toast, which is why a headless driver honours it too.
 */
test('notify off stops this machine saying so, and on starts it again', () => {
  const fx = makeFixture('driver-notify');

  const off = run(['notify', 'off'], at(fx));
  assert.equal(readDriverChoices(at(fx)).notify, false);
  assert.match(off.out, /will not/);

  const on = run(['notify', 'on'], at(fx));
  assert.equal(readDriverChoices(at(fx)).notify, true);
  assert.match(on.out, /parks|ends/);
  fx.cleanup();
});

/**
 * 🔴 Silence means ON, on both sides. Every machine that already has a driver.json predates this
 * field, and a reader that took absence for "off" would ship the feature switched off on exactly
 * the machines that have been driving longest.
 */
test('a file that never mentioned notifying still notifies', () => {
  const fx = makeFixture('driver-notify-absent');
  run(['drive', 'engine'], at(fx));

  assert.equal(readDriverChoices(at(fx)).notify, true);
  assert.match(run(['list'], at(fx)).out, /notify {5}on/);
  fx.cleanup();
});

test('notify needs on or off, and says so', () => {
  const fx = makeFixture('driver-notify-arg');
  assert.match(captureError(() => run(['notify'], at(fx))).message, /on\|off/);
  assert.match(captureError(() => run(['notify', 'maybe'], at(fx))).message, /on\|off/);
  fx.cleanup();
});

test('the notify choice survives edits made by verbs that do not know it', () => {
  const fx = makeFixture('driver-notify-preserve');
  run(['notify', 'off'], at(fx));

  run(['drive', 'engine'], at(fx));
  run(['cap', '3'], at(fx));
  run(['trees', 'engine', 'on'], at(fx));

  assert.equal(readDriverChoices(at(fx)).notify, false);
  fx.cleanup();
});

/** The field the driver actually reads — the two artefacts must spell it the same way. */
test('notify is written as the driver spells it', () => {
  const fx = makeFixture('driver-notify-shape');
  run(['notify', 'off'], at(fx));

  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).notify, false);
  fx.cleanup();
});

/**
 * DRV6's second door. The desktop can show a parked quest; a headless machine running
 * `daoris-driver` has no screen, and it is the machine most likely to be the one burning an account
 * unattended — so the terminal owns the whole verb, not a view of it.
 */
test('the strike limit is set from a terminal, and zero is the old behaviour', () => {
  const fx = makeFixture('driver-strikes');

  const set = run(['strikes', '5'], at(fx));
  assert.equal(readDriverChoices(at(fx)).strikes, 5);
  assert.match(set.out, /5/);

  // 🔴 Zero must be settable and must not be read as "unset" — it is the person asking for the loop
  // to keep trying, which is what every machine did before this existed.
  run(['strikes', '0'], at(fx));
  assert.equal(readDriverChoices(at(fx)).strikes, 0);
  assert.match(run(['list'], at(fx)).out, /never parks|keeps trying/i);

  fx.cleanup();
});

test('a file written before strikes existed gets the default rather than none', () => {
  const fx = makeFixture('driver-strikes-absent');
  writeFileSync(at(fx), `${JSON.stringify({ drivable: ['Game'], cap: 2 }, null, 2)}\n`, 'utf8');

  assert.equal(readDriverChoices(at(fx)).strikes, 3);

  fx.cleanup();
});

test('retrying a quest marks it at its current failures rather than erasing them', () => {
  const fx = makeFixture('driver-retry');

  const said = run(['retry', 'a78553', '--at', '4'], at(fx));
  assert.equal(readDriverChoices(at(fx)).forgiven['a78553'], 4);
  assert.match(said.out, /a78553/);

  fx.cleanup();
});

/**
 * The counterpart-set worry, once more: `strikes` and `forgiven` are written by the driver too, and
 * an editor that rewrote the file from its own idea of the shape would delete them. This family has
 * been bitten by exactly that twice — the harness pin, and profiles before it.
 */
test('a verb that knows nothing of strikes preserves them', () => {
  const fx = makeFixture('driver-strikes-preserve');
  run(['strikes', '7'], at(fx));
  run(['retry', 'q1', '--at', '2'], at(fx));

  run(['drive', 'Game'], at(fx));

  const held = readDriverChoices(at(fx));
  assert.equal(held.strikes, 7);
  assert.equal(held.forgiven['q1'], 2);
  assert.deepEqual(held.drivable, ['Game']);

  fx.cleanup();
});
