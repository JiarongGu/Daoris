import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandDriver, driverConfigPath, isBranchName, landingProblem, readDriverChoices } from '../src/driverconfig.ts';
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
  assert.match(driverConfigPath({ DAORIS_HOME: '/x/data' }), /[\\/]data[\\/]driver\.json$/);
  // 🔴 No default under the user profile (D63): a machine nobody pointed is refused, not written to.
  assert.throws(() => driverConfigPath({}), /DAORIS_HOME/);
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

/**
 * WSR2: a repository's line — the branch its work grows from and lands on — set by the person, per
 * repository or for a whole workspace, winning over the checkout's guess. `CanonicalLineTests.cs`
 * holds the same branch-name table, answer for answer.
 */
const BRANCHES: [string, boolean][] = [
  ['main', true], ['develop', true], ['feature/team-x', true], ['release/2026.09', true],
  ['', false], ['has space', false], ['-dash', false], ['a..b', false], ['ends/', false], ['x.lock', false],
  ['a:b', false], ['@', false], ['a@{1}', false], ['.hidden', false], ['feat/.x', false], ['a//b', false],
  ['a b', false], ['a\u0085b', false],
];

test('a line is a name git would take', () => {
  for (const [name, valid] of BRANCHES) assert.equal(isBranchName(name), valid, JSON.stringify(name));
});

test('line sets a repository\'s line, a workspace\'s default, and clears either', () => {
  const fx = makeFixture('driver-line');

  const set = run(['line', 'engine', 'develop'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).lines, { engine: 'develop' });
  assert.match(set.out, /engine.*develop/);

  run(['line', '--workspace', 'aurora', 'release'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).workspaceLines, { aurora: 'release' });
  assert.match(run(['list'], at(fx)).out, /line\s+engine\s+develop[\s\S]*line\s+workspace aurora\s+release/);

  run(['line', 'engine', '--clear'], at(fx));
  run(['line', '--workspace', 'aurora', '--clear'], at(fx));
  const cleared = readDriverChoices(at(fx));
  assert.deepEqual([cleared.lines, cleared.workspaceLines], [{}, {}]);
  // Absent is the checkout's guess, and a file that never chose a line carries none.
  assert.equal('lines' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

test('line refuses a name git would not take, and a missing one', () => {
  const fx = makeFixture('driver-line-refused');
  assert.match(captureError(() => run(['line', 'engine', 'a..b'], at(fx))).message, /branch name/);
  assert.match(captureError(() => run(['line', 'engine'], at(fx))).message, /<branch>\|--clear/);
  fx.cleanup();
});

test('the lines survive edits made by verbs that do not know them, and one git would refuse is not read', () => {
  const fx = makeFixture('driver-line-preserve');
  writeFileSync(at(fx), JSON.stringify({ lines: { engine: 'develop', odd: 'has space' }, workspaceLines: { aurora: 'release' } }));

  run(['cap', '3'], at(fx));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.lines, { engine: 'develop' });
  assert.deepEqual(choices.workspaceLines, { aurora: 'release' });
  fx.cleanup();
});

/**
 * WSR1 (D87): how work lands — merged into the line, or put on a branch a pattern names, for the
 * person to push. `LandingTests.cs` holds the same pattern table, answer for answer.
 */
const PATTERNS: [string, boolean][] = [
  ['feature/{quest}-{slug}', true], ['review/{session}', true], ['team/{repository}/{quest}', true],
  ['', false], ['feature/fixed', false], ['feature/{slug}', false], ['feature/{nope}-{quest}', false],
  ['feature/{quest', false], ['feature/{quest}..x', false], ['feature {quest}', false],
];

test('a pattern names a branch git would take, and one per session', () => {
  for (const [pattern, valid] of PATTERNS) {
    assert.equal(landingProblem({ form: 'branch', pattern }) === null, valid, JSON.stringify(pattern));
  }

  assert.equal(landingProblem({ form: 'merge' }), null);
  assert.match(landingProblem({ form: 'push' })!, /a plugin's to do/);
});

test('landing sets a repository\'s rule, a workspace\'s, and clears either', () => {
  const fx = makeFixture('driver-landing');

  const branch = run(['landing', '--workspace', 'aurora', 'branch', 'feature/{quest}-{slug}'], at(fx));
  assert.match(branch.out, /feature\/\{quest\}-\{slug\}/);
  assert.match(branch.out, /never pushes/);
  run(['landing', 'engine', 'merge'], at(fx));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.workspaceLandings, { aurora: { form: 'branch', pattern: 'feature/{quest}-{slug}' } });
  assert.deepEqual(choices.landings, { engine: { form: 'merge' } });
  assert.match(run(['list'], at(fx)).out,
    /landing\s+engine\s+merge[\s\S]*landing\s+workspace aurora\s+branch feature\/\{quest\}-\{slug\}/);

  run(['landing', 'engine', '--clear'], at(fx));
  run(['landing', '--workspace', 'aurora', '--clear'], at(fx));
  const cleared = readDriverChoices(at(fx));
  assert.deepEqual([cleared.landings, cleared.workspaceLandings], [{}, {}]);
  assert.equal('landings' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

test('landing --tidy asks for the tree and branch to go once the work lands, and is written only when on (D88)', () => {
  const fx = makeFixture('driver-landing-tidy');

  const said = run(['landing', 'engine', 'merge', '--tidy'], at(fx));
  assert.match(said.out, /tree and its branch go/);
  assert.deepEqual(readDriverChoices(at(fx)).landings, { engine: { form: 'merge', tidy: true } });
  assert.match(run(['list'], at(fx)).out, /landing\s+engine\s+merge, tidy/);

  run(['landing', 'engine', 'merge'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).landings.engine.tidy, undefined);
  fx.cleanup();
});

test('landing refuses a rule that could not land work, and says what it needs', () => {
  const fx = makeFixture('driver-landing-refused');
  assert.match(captureError(() => run(['landing', 'engine', 'branch', 'feature/fixed'], at(fx))).message, /\{quest\}` or `\{session\}/);
  assert.match(captureError(() => run(['landing', 'engine', 'branch'], at(fx))).message, /pattern/);
  assert.match(captureError(() => run(['landing', 'engine', 'push'], at(fx))).message, /a plugin's to do/);
  assert.match(captureError(() => run(['landing', 'engine'], at(fx))).message, /merge\|branch <pattern>\|--clear/);
  fx.cleanup();
});

/**
 * WSR4 (D100): a branch rule may name the plugin that pushes it and opens the pull request. Only a
 * branch, and only an id — `LandingTests.cs` holds these cases, answer for answer.
 */
const PLUGIN_SHAPES: [string, string | undefined, string | undefined, RegExp | null][] = [
  ['branch', 'feature/{quest}-{slug}', 'example.github-pull-request', null],
  ['branch', 'feature/{quest}-{slug}', undefined, null],
  ['merge', undefined, 'example.github-pull-request', /only a branch/],
  ['branch', 'feature/{quest}-{slug}', 'Not An Id', /not a plugin id/],
  ['branch', 'feature/{quest}-{slug}', '../elsewhere', /not a plugin id/],
  ['push', undefined, undefined, /a plugin's to do/],
];

test('only a branch rule names a plugin, and only by an id', () => {
  for (const [form, pattern, plugin, problem] of PLUGIN_SHAPES) {
    const said = landingProblem({ form, ...(pattern ? { pattern } : {}), ...(plugin ? { plugin } : {}) });
    if (problem === null) assert.equal(said, null, JSON.stringify([form, plugin]));
    else assert.match(said ?? '', problem, JSON.stringify([form, plugin]));
  }
});

/** A plugin folder under the fixture's home, which is where `driver.json` is (the driver's own reading). */
function plugin(fx: { root: string }, id: string, points: string[]): void {
  const folder = join(fx.root, 'plugins', id);
  mkdirSync(folder, { recursive: true });
  writeFileSync(join(folder, 'plugin.json'), JSON.stringify({ id, hooks: { command: ['node', '${plugin}/land.mjs'], points } }));
}

test('landing --plugin names a plugin on this machine that lands work, and says who pushes (D100)', () => {
  const fx = makeFixture('driver-landing-plugin');
  plugin(fx, 'example.github-pull-request', ['work/land']);

  const said = run(['landing', '--workspace', 'aurora', 'branch', 'feature/{quest}-{slug}', '--plugin', 'example.github-pull-request'], at(fx));
  assert.match(said.out, /plugin `example\.github-pull-request` pushes it and opens the pull request/);
  assert.match(said.out, /Daoris itself never pushes/);
  assert.deepEqual(readDriverChoices(at(fx)).workspaceLandings,
    { aurora: { form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'example.github-pull-request' } });
  assert.match(run(['list'], at(fx)).out, /landing\s+workspace aurora\s+branch feature\/\{quest\}-\{slug\}, plugin example\.github-pull-request/);
  fx.cleanup();
});

test('landing --plugin refuses a plugin missing, off, or landing nothing, each in its own sentence, and writes nothing', () => {
  const fx = makeFixture('driver-landing-plugin-refused');
  const set = (id: string) => () => run(['landing', 'engine', 'branch', 'feature/{quest}-{slug}', '--plugin', id], at(fx));

  assert.match(captureError(set('example.nowhere')).message, /not installed/);

  plugin(fx, 'example.watches', ['session/ended']);
  assert.match(captureError(set('example.watches')).message, /`work\/land`/);

  plugin(fx, 'example.off', ['work/land']);
  writeFileSync(join(fx.root, 'plugins.json'), JSON.stringify({ disabled: ['example.off'] }));
  assert.match(captureError(set('example.off')).message, /daoris plugin enable example\.off/);

  plugin(fx, 'example.lands', ['work/land']);
  assert.match(captureError(() => run(['landing', 'engine', 'merge', '--plugin', 'example.lands'], at(fx))).message, /only a branch/);
  assert.match(captureError(() => run(['landing', 'engine', 'branch', 'feature/{quest}', '--plugin'], at(fx))).message, /--plugin <id>/);

  assert.deepEqual(readDriverChoices(at(fx)).landings, {});
  fx.cleanup();
});

test('a rule\'s plugin is read and kept as the driver keeps it, and a merge naming one is not read', () => {
  const fx = makeFixture('driver-landing-plugin-preserve');
  writeFileSync(at(fx), JSON.stringify({
    landings: { odd: { form: 'merge', plugin: 'example.github-pull-request' } },
    workspaceLandings: { aurora: { form: 'branch', pattern: 'review/{session}', plugin: 'example.github-pull-request' } },
  }));

  run(['cap', '3'], at(fx));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.landings, {});
  assert.deepEqual(choices.workspaceLandings, { aurora: { form: 'branch', pattern: 'review/{session}', plugin: 'example.github-pull-request' } });
  fx.cleanup();
});

test('the landing rules survive edits by verbs that do not know them, and one that could not land is not read', () => {
  const fx = makeFixture('driver-landing-preserve');
  writeFileSync(at(fx), JSON.stringify({
    landings: { engine: { form: 'merge' }, odd: { form: 'push' }, bad: { form: 'branch', pattern: 'fixed' } },
    workspaceLandings: { aurora: { form: 'branch', pattern: 'review/{session}' } },
  }));

  run(['cap', '3'], at(fx));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.landings, { engine: { form: 'merge' } });
  assert.deepEqual(choices.workspaceLandings, { aurora: { form: 'branch', pattern: 'review/{session}' } });
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
    error.message, /list, drive, undrive, hold, resume, trees, line, landing, across, notify, strikes, retry, timeout, cap, adapter, intake, helper/);
  fx.cleanup();
});

/**
 * The intake's harness (INT4b, D65 §1b) — the terminal's half of the desktop's `SET_INTAKE`, one file
 * and two doors (D50). 🔴 Off until a harness is NAMED: an intake spends a real login on every ask,
 * and a machine whose file predates the field answers asks by declarations only, as it always has.
 */
test('intake names the harness that answers asks, and off turns it off again', () => {
  const fx = makeFixture('driver-intake');

  assert.match(run(['list'], at(fx)).out, /intake {5}off/);

  const on = run(['intake', 'claude-code-acp'], at(fx));
  assert.equal(readDriverChoices(at(fx)).intakeAdapter, 'claude-code-acp');
  assert.match(on.out, /claude-code-acp/);
  assert.match(on.out, /login/);
  assert.match(run(['list'], at(fx)).out, /intake {5}claude-code-acp/);

  run(['intake', 'off'], at(fx));
  assert.equal(readDriverChoices(at(fx)).intakeAdapter, null);
  // Absent, never null on disk: silence is what both artefacts read as off.
  assert.equal('intakeAdapter' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

/**
 * Ask Daoris's own agent (HELP1, D89): the terminal's half of the desktop's `SET_HELPER`. Off until
 * named, and apart from the intake's: answering asks and helping a person are two jobs.
 */
test('helper names the agent Ask Daoris runs on, apart from the intake, and off turns it off', () => {
  const fx = makeFixture('driver-helper');
  run(['intake', 'claude-code-acp'], at(fx));

  assert.match(run(['list'], at(fx)).out, /helper {5}off/);
  const on = run(['helper', 'codex-acp'], at(fx));
  assert.match(on.out, /Ask Daoris/);
  assert.deepEqual(
    [readDriverChoices(at(fx)).helperAdapter, readDriverChoices(at(fx)).intakeAdapter], ['codex-acp', 'claude-code-acp']);
  assert.match(run(['list'], at(fx)).out, /helper {5}codex-acp/);

  run(['helper', 'off'], at(fx));
  assert.equal('helperAdapter' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  assert.match(captureError(() => run(['helper'], at(fx))).message, /<adapter>\|off/);
  fx.cleanup();
});

test('intake needs a harness or off, and says so', () => {
  const fx = makeFixture('driver-intake-arg');
  assert.match(captureError(() => run(['intake'], at(fx))).message, /<adapter>\|off/);
  fx.cleanup();
});

/** The field the driver reads, written back by every verb that does not know it — the D50 trap in §1b. */
test('the intake harness survives other verbs and is spelled as the driver reads it', () => {
  const fx = makeFixture('driver-intake-preserve');
  run(['intake', 'stub'], at(fx));

  run(['drive', 'engine'], at(fx));
  run(['notify', 'off'], at(fx));

  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).intakeAdapter, 'stub');
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

/**
 * How long a session may run before the driver kills it (D80's companion). Found on the first real
 * development run: the default thirty minutes killed a session that had done the work and was running
 * its repository's gates. The field existed only in the file; now both doors reach it (D50).
 */
test('the session timeout is set from a terminal, listed, and left alone until it is', () => {
  const fx = makeFixture('driver-timeout');
  writeFileSync(at(fx), `${JSON.stringify({ drivable: ['engine'], cap: 1 }, null, 2)}\n`, 'utf8');

  // Absent is the driver's own default, said as such, and an unrelated edit does not write it.
  assert.match(run(['list'], at(fx)).out, /timeout\s+30 minutes.*default/);
  run(['hold', 'engine'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).timeoutMinutes, undefined);

  const set = run(['timeout', '120'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).timeoutMinutes, 120);
  assert.match(set.out, /120 minutes/);
  assert.match(run(['list'], at(fx)).out, /timeout\s+120 minutes/);

  for (const bad of ['0', '-5', '1.5', 'soon']) {
    assert.match(captureError(() => run(['timeout', bad], at(fx))).message, /whole number of minutes/);
  }

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

  // A flag's value is not the quest, wherever the flag stands (REV3): `--at 2 42` forgave quest #2.
  run(['retry', '--at', '2', 'b9c7d1'], at(fx));
  assert.equal(readDriverChoices(at(fx)).forgiven['b9c7d1'], 2);
  assert.equal(readDriverChoices(at(fx)).forgiven['2'], undefined);

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

/**
 * READ1 (D107): reading and writing across repositories. A checkout is read by agents outside it unless
 * its repository, else its workspace, says off; a repository's sessions write into another only where
 * the person declared it. The FILE is the twin: `AcrossTests.cs` holds these shape cases, answer for
 * answer. Only the driver resolves the setting, so the precedence is held there alone.
 */
test('absent is reading on and no relationship, and nothing is written until something is set', () => {
  const fx = makeFixture('driver-across-absent');
  const choices = readDriverChoices(at(fx));
  assert.deepEqual([choices.readAcross, choices.workspaceReadAcross, choices.writeAcross], [{}, {}, {}]);

  run(['cap', '3'], at(fx));
  const written = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal('readAcross' in written || 'workspaceReadAcross' in written || 'writeAcross' in written, false);
  fx.cleanup();
});

test('only a boolean is read as reading, and only other names as a relationship', () => {
  const fx = makeFixture('driver-across-shape');
  writeFileSync(at(fx), JSON.stringify({
    readAcross: { engine: false, game: true, odd: 'no', none: null },
    workspaceReadAcross: { aurora: false, forge: 1 },
    writeAcross: { plugins: ['engine', 'Engine', 'plugins', 3, '', 'game'], bad: 'engine', empty: [] },
  }));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.readAcross, { engine: false, game: true });
  assert.deepEqual(choices.workspaceReadAcross, { aurora: false });
  // One entry per name in any case, never itself, in the order first written.
  assert.deepEqual(choices.writeAcross, { plugins: ['engine', 'game'] });
  fx.cleanup();
});

test('across sets a repository\'s reading, a workspace\'s, and clears either by name in any case', () => {
  const fx = makeFixture('driver-across-read');

  const off = run(['across', 'Engine', 'read', 'off'], at(fx));
  assert.equal(off.code, 0);
  assert.match(off.out, /`Engine`'s checkout is read by no agent outside it/);
  run(['across', '--workspace', 'aurora', 'read', 'on'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).readAcross, { Engine: false });
  assert.deepEqual(readDriverChoices(at(fx)).workspaceReadAcross, { aurora: true });
  assert.match(run(['list'], at(fx)).out, /across\s+Engine\s+read off[\s\S]*across\s+workspace aurora\s+read on/);

  run(['across', 'engine', 'read', '--clear'], at(fx));
  run(['across', '--workspace', 'Aurora', 'read', '--clear'], at(fx));
  const cleared = readDriverChoices(at(fx));
  assert.deepEqual([cleared.readAcross, cleared.workspaceReadAcross], [{}, {}]);
  assert.equal('readAcross' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

test('a relationship is declared once in any case, says whose say-so it is, and the last one cleared leaves no entry', () => {
  const fx = makeFixture('driver-across-write');

  const declared = run(['across', 'plugins', 'write-to', 'engine'], at(fx));
  assert.match(declared.out, /sessions in `plugins` may also write into `engine`/);
  run(['across', 'Plugins', 'write-to', 'ENGINE'], at(fx));
  run(['across', 'plugins', 'write-to', 'game'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).writeAcross, { plugins: ['engine', 'game'] });
  assert.match(run(['list'], at(fx)).out, /across\s+plugins\s+writes into engine, game/);

  run(['across', 'plugins', 'write-to', 'Engine', '--clear'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).writeAcross, { plugins: ['game'] });
  run(['across', 'plugins', 'write-to', 'game', '--clear'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).writeAcross, {});
  assert.equal('writeAcross' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

test('a repository writing into itself or into no name is refused in the driver\'s sentences, and nothing is written', () => {
  const fx = makeFixture('driver-across-refused');
  assert.equal(
    captureError(() => run(['across', 'plugins', 'write-to', 'Plugins'], at(fx))).message,
    '`plugins` writes in its own tree already — a relationship names another repository.');
  assert.equal(
    captureError(() => run(['across', 'plugins', 'write-to'], at(fx))).message,
    'a relationship names the repository it may write into.');
  assert.match(captureError(() => run(['across', 'plugins', 'read'], at(fx))).message, /on\|off\|--clear/);
  assert.match(captureError(() => run(['across'], at(fx))).message, /<repository>\|--workspace <name>/);
  assert.match(captureError(() => run(['across', 'plugins', 'wander'], at(fx))).message, /read .*write-to/);
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('the setting survives edits by verbs that do not know it', () => {
  const fx = makeFixture('driver-across-preserve');
  run(['across', 'engine', 'read', 'off'], at(fx));
  run(['across', '--workspace', 'aurora', 'read', 'off'], at(fx));
  run(['across', 'plugins', 'write-to', 'engine'], at(fx));

  run(['drive', 'engine'], at(fx));
  run(['line', 'engine', 'develop'], at(fx));

  const held = readDriverChoices(at(fx));
  assert.deepEqual(held.readAcross, { engine: false });
  assert.deepEqual(held.workspaceReadAcross, { aurora: false });
  assert.deepEqual(held.writeAcross, { plugins: ['engine'] });
  fx.cleanup();
});
