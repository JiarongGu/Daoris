import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_COOLOFF_MINUTES, SESSION_LANGUAGES, commandDriver, driverConfigPath, isBranchName, landingProblem, languageFor,
  pausedAsk, pausedQuest, readDriverChoices, releasedFor, standingFor, writeAcrossProblem, writeDriverChoices,
} from '../src/driverconfig.ts';
import {
  REVIEW_WAITING, applyReviewEdit, holdsProcedure, reviewFor, reviewGate, reviewRuleOf, reviewSays, type CheckoutsReader,
} from '../src/reviews.ts';
import {
  OPINION_DECLARED_ONLY, applyOpinionEdit, oneFamily, opinionFor, opinionGate, opinionRuleOf, opinionSays, sameAgentOf,
} from '../src/opinions.ts';
import { MANIFEST, pluginsRoot, readPlugins } from '../src/plugins.ts';
import {
  applyWorkflowEdit, keptVersions, resolveWorkflow, workflowChoiceOf, type WorkflowChoiceEdit, type WorkflowTaskChoice,
} from '../src/workflowchoice.ts';
import type { RecordsReader } from '../src/strikes.ts';
import { driverRows as csharpRows } from './_csharp.ts';
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

/**
 * A repository named twice is one only as the driver's `OrdinalIgnoreCase` finds it (CASEFOLD1, `casefold.ts`), which
 * `DriverConfig`'s lists compare by: a name full case mapping would lower to the same letters is another repository.
 */
test('a repository opted in, held or given trees is another only as the driver parts it: İzmir is not i̇zmir', () => {
  const fx = makeFixture('driver-case');
  for (const verb of ['drive', 'hold']) {
    run([verb, 'İzmir'], at(fx));
    run([verb, 'i\u{307}zmir'], at(fx));
    run([verb, 'straße'], at(fx));
    run([verb, 'STRASSE'], at(fx));
  }
  run(['trees', 'İzmir', 'on'], at(fx));
  run(['trees', 'i\u{307}zmir', 'on'], at(fx));
  run(['trees', 'i\u{307}zmir', 'off'], at(fx));

  const choices = readDriverChoices(at(fx));
  assert.deepEqual(choices.drivable, ['İzmir', 'i\u{307}zmir', 'straße', 'STRASSE']);
  assert.deepEqual(choices.holds, ['İzmir', 'i\u{307}zmir', 'straße', 'STRASSE']);
  assert.deepEqual(choices.trees, ['İzmir']);
  const listed = run(['list'], at(fx)).out;
  assert.match(listed, /drivable {3}i\u{307}zmir {2}\(held by you/u);
  assert.doesNotMatch(listed, /drivable {3}i\u{307}zmir[^\n]*own tree/u);
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

/**
 * LAND2a (D145): *Accept automatically*, `autoAccept` on a branch rule — a quest's done lands its work and the rule's
 * plugin pushes it and opens the pull request with no press. 🔴 A TWIN with the driver's `LandingRules.Problem` and
 * `DriverConfig`: `LandingAutoAcceptTests.cs` holds both tables row for row, and the tests below hold them to these, cell
 * for cell. Only a branch rule takes it, with a plugin or without; a merge naming a plugin is told that first.
 */
const AUTO_ACCEPT_SHAPES: [form: string, pattern: string | null, plugin: string | null, autoAccept: boolean, problem: string | null][] = [
  ['branch', 'feature/{quest}-{slug}', 'example.github-pull-request', true, null],
  ['branch', 'feature/{quest}-{slug}', null, true, null],
  ['branch', 'feature/{quest}-{slug}', null, false, null],
  ['merge', null, null, true, 'only a branch rule accepts automatically'],
  ['merge', null, null, false, null],
  ['merge', null, 'example.github-pull-request', true, 'only a branch rule hands its work to a plugin'],
];

/** What the file says, read: absent is off, only JSON `true` is on, and a merge carrying it is not read at all. */
const AUTO_ACCEPT_ROWS: [name: string, file: string, map: string, key: string, expected: 'on' | 'off' | 'none'][] = [
  ['absent is off', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}"}}}', 'landings', 'engine', 'off'],
  ['true on a branch rule is on', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":true}}}', 'landings', 'engine', 'on'],
  ['without a plugin it is still on', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","tidy":true,"autoAccept":true}}}', 'landings', 'engine', 'on'],
  ['false is off', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":false}}}', 'landings', 'engine', 'off'],
  ['text is not true', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":"true"}}}', 'landings', 'engine', 'off'],
  ['a number is not true', '{"landings":{"engine":{"form":"branch","pattern":"feature/{quest}","autoAccept":1}}}', 'landings', 'engine', 'off'],
  ['a merge carrying it is not read', '{"landings":{"engine":{"form":"merge","autoAccept":true}}}', 'landings', 'engine', 'none'],
  ['a merge without it is read', '{"landings":{"engine":{"form":"merge","autoAccept":false}}}', 'landings', 'engine', 'off'],
  ['a workspace\'s rule carries it', '{"workspaceLandings":{"aurora":{"form":"branch","pattern":"review/{session}","autoAccept":true}}}', 'workspaceLandings', 'aurora', 'on'],
];

const LANDING_TESTS = () => readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'LandingAutoAcceptTests.cs'), 'utf8').replace(/\r\n/g, '\n');

test('only a branch rule accepts automatically (the twin\'s shapes)', () => {
  for (const [form, pattern, plugin, autoAccept, problem] of AUTO_ACCEPT_SHAPES) {
    const said = landingProblem({
      form, ...(pattern ? { pattern } : {}), ...(plugin ? { plugin } : {}), ...(autoAccept ? { autoAccept } : {}),
    });
    if (problem === null) assert.equal(said, null, JSON.stringify([form, plugin, autoAccept]));
    else assert.ok(said?.includes(problem), `${JSON.stringify([form, plugin, autoAccept])}: ${said}`);
  }
});

test('an automatic acceptance reads as the driver reads it (the twin\'s table)', () => {
  const fx = makeFixture('driver-landing-auto-read');
  for (const [name, file, map, key, expected] of AUTO_ACCEPT_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    const choices = readDriverChoices(at(fx));
    const rule = (map === 'workspaceLandings' ? choices.workspaceLandings : choices.landings)[key];
    assert.equal(rule === undefined ? 'none' : rule.autoAccept ? 'on' : 'off', expected, name);
  }
  fx.cleanup();
});

test('the driver’s automatic acceptance tables are these tables, row for row and in this order', () => {
  const source = LANDING_TESTS();
  assert.deepEqual(csharpRows(source, 'Only_a_branch_rule_accepts_automatically', {}, 'LandingAutoAcceptTests'), AUTO_ACCEPT_SHAPES);
  assert.deepEqual(csharpRows(source, 'AutoAccept_reads_as_the_cli_reads_it', {}, 'LandingAutoAcceptTests'), AUTO_ACCEPT_ROWS);
});

test('landing --auto-accept with a plugin says it pushes without a press, is listed, and is written only when on (LAND2a)', () => {
  const fx = makeFixture('driver-landing-auto');
  plugin(fx, 'example.github-pull-request', ['work/land']);

  const said = run(['landing', '--workspace', 'aurora', 'branch', 'feature/{quest}-{slug}', '--tidy',
    '--plugin', 'example.github-pull-request', '--auto-accept'], at(fx));
  assert.match(said.out, /`example\.github-pull-request` pushes it and opens a pull request without asking you each time/);
  assert.match(said.out, /Switch it off to accept each one yourself/);
  assert.deepEqual(readDriverChoices(at(fx)).workspaceLandings, {
    aurora: { form: 'branch', pattern: 'feature/{quest}-{slug}', plugin: 'example.github-pull-request', tidy: true, autoAccept: true },
  });
  // The same key in the same place the driver writes it: after the tidy.
  assert.match(readFileSync(at(fx), 'utf8'), /"tidy": true,\s*"autoAccept": true/);
  assert.match(run(['list'], at(fx)).out, /landing\s+workspace aurora\s+branch feature\/\{quest\}-\{slug\}, plugin example\.github-pull-request, tidy, accept automatically/);

  run(['cap', '2'], at(fx));
  assert.equal(readDriverChoices(at(fx)).workspaceLandings.aurora?.autoAccept, true, 'another verb keeps it');

  run(['landing', '--workspace', 'aurora', 'branch', 'feature/{quest}-{slug}', '--plugin', 'example.github-pull-request'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).workspaceLandings.aurora.autoAccept, undefined);
  fx.cleanup();
});

test('landing --auto-accept with no plugin is allowed, and warns that nothing leaves the machine', () => {
  const fx = makeFixture('driver-landing-auto-alone');

  const said = run(['landing', 'engine', 'branch', 'review/{session}', '--auto-accept'], at(fx));
  assert.match(said.out, /no plugin opens a pull request, so each done's branch waits here for you to push it/);
  assert.match(said.out, /nothing leaves this machine/);
  assert.deepEqual(readDriverChoices(at(fx)).landings, { engine: { form: 'branch', pattern: 'review/{session}', autoAccept: true } });
  fx.cleanup();
});

test('a repository\'s --no-auto-accept replaces the workspace\'s switch whole, and --clear hands it back', () => {
  const fx = makeFixture('driver-landing-auto-repository');
  run(['landing', '--workspace', 'aurora', 'branch', 'feature/{quest}-{slug}', '--auto-accept'], at(fx));

  const byHand = run(['landing', 'engine', 'branch', 'feature/{quest}-{slug}', '--no-auto-accept'], at(fx));
  assert.match(byHand.out, /waits for your Accept/);
  assert.deepEqual(readDriverChoices(at(fx)).landings, { engine: { form: 'branch', pattern: 'feature/{quest}-{slug}' } });

  run(['landing', 'engine', '--clear'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).landings, {});
  assert.equal(readDriverChoices(at(fx)).workspaceLandings.aurora?.autoAccept, true);
  fx.cleanup();
});

test('landing refuses a merge that accepts automatically, and both switches at once, and writes nothing', () => {
  const fx = makeFixture('driver-landing-auto-refused');

  assert.match(captureError(() => run(['landing', 'engine', 'merge', '--auto-accept'], at(fx))).message,
    /only a branch rule accepts automatically/);
  assert.match(captureError(() => run(['landing', 'engine', 'branch', 'review/{session}', '--auto-accept', '--no-auto-accept'], at(fx))).message,
    /`--auto-accept` or `--no-auto-accept`/);
  assert.deepEqual(readDriverChoices(at(fx)).landings, {});
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
    error.message, /list, drive, undrive, hold, resume, trees, line, landing, across, standing, language, review, opinion, workflow, notify, strikes, retry, timeout, cooloff, cap, adapter, intake, helper/);
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

/**
 * `cooloff` (TOOL4e, D125 §2.2): how long an account cools when its agent's limit names no time this reads. 🔴 A TWIN with
 * the driver's `DriverConfig.CoolOff`: `CoolOffTests.cs` holds this table row for row, and the test below holds it to
 * this one, cell for cell. Absent is an hour; a whole number of at least 1 is the setting; anything else is not read.
 */
const COOLOFF_ROWS: [name: string, file: string, minutes: number][] = [
  ['absent is the default, an hour', '{}', 60],
  ['a whole number of minutes is the setting', '{"cooloff":90}', 90],
  ['one minute is the least there is', '{"cooloff":1}', 1],
  ['zero is a spin, and is not read', '{"cooloff":0}', 60],
  ['less than nothing is not read', '{"cooloff":-5}', 60],
  ['a part of a minute is not read', '{"cooloff":1.5}', 60],
  ['text is not a number', '{"cooloff":"90"}', 60],
  ['null is absent', '{"cooloff":null}', 60],
];

test('the cool-off reads as the driver reads it (the twin\'s table)', () => {
  const fx = makeFixture('driver-cooloff-read');
  for (const [name, file, minutes] of COOLOFF_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    assert.equal(readDriverChoices(at(fx)).cooloff ?? DEFAULT_COOLOFF_MINUTES, minutes, name);
  }
  fx.cleanup();
});

test('the driver’s cool-off table is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'CoolOffTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'Cooloff_reads_as_the_cli_reads_it', {}, 'CoolOffTests'), COOLOFF_ROWS);
});

test('the cool-off is set from a terminal, listed, and left alone until it is', () => {
  const fx = makeFixture('driver-cooloff');
  writeFileSync(at(fx), `${JSON.stringify({ drivable: ['engine'], cap: 1 }, null, 2)}\n`, 'utf8');

  assert.match(run(['list'], at(fx)).out, /cooloff\s+60 minutes.*the default/);
  run(['hold', 'engine'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).cooloff, undefined);

  const set = run(['cooloff', '90'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).cooloff, 90);
  assert.match(set.out, /an account whose agent names no time for its limit cools for 90 minutes/);
  assert.match(run(['list'], at(fx)).out, /cooloff\s+90 minutes/);
  run(['resume', 'engine'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).cooloff, 90, 'another verb keeps it');

  for (const bad of ['0', '-5', '1.5', 'soon']) {
    assert.match(captureError(() => run(['cooloff', bad], at(fx))).message, /whole number of minutes, at least 1/);
  }
  assert.match(captureError(() => run(['cooloff'], at(fx))).message, /needs a name|whole number of minutes/);
  fx.cleanup();
});

/**
 * `released` (SESSUX1b, D126 §3.4): the quests the person released from their stop, each against the session they stopped, so
 * a later stop holds the quest again. 🔴 A TWIN with the driver's `DriverConfig.Released`: `ReleasedTests.cs` holds this table
 * row for row, and the test below holds it to this one, cell for cell.
 */
const RELEASED_ROWS: [name: string, file: string, quest: string, session: string | null][] = [
  ['absent is no release', '{}', 'q1', null],
  ['a quest against the session it stopped is a release', '{"released":{"q1":"s1"}}', 'q1', 's1'],
  ['a quest is matched in any case', '{"released":{"Q1":"s1"}}', 'q1', 's1'],
  ['a session is read without the spaces around it', '{"released":{"q1":" s1 "}}', 'q1', 's1'],
  ['a blank session names no stop', '{"released":{"q1":"  "}}', 'q1', null],
  ['a session that is not text is not read', '{"released":{"q1":7}}', 'q1', null],
  ['a quest written twice in any case is read where first written', '{"released":{"q1":"s1","Q1":"s2"}}', 'q1', 's1'],
  ['another quest\'s release is not this one\'s', '{"released":{"q2":"s1"}}', 'q1', null],
  ['a list is not a map', '{"released":["q1"]}', 'q1', null],
  ['null is absent', '{"released":null}', 'q1', null],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"released":{"straße":"s1"}}', 'STRASSE', null],
  ['a dotted capital I is not an i with a dot above', '{"released":{"İzmir":"s1"}}', 'i\u{307}zmir', null],
];

test('a release reads as the driver reads it (the twin\'s table)', () => {
  const fx = makeFixture('driver-released-read');
  for (const [name, file, quest, session] of RELEASED_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    assert.equal(releasedFor(readDriverChoices(at(fx)), quest), session, name);
  }
  fx.cleanup();
});

test('the driver’s release table is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'ReleasedTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'Released_reads_as_the_cli_reads_it', {}, 'ReleasedTests'), RELEASED_ROWS);
});

/**
 * The terminal's Try again for a stop (D126 §3.4, §7.1). `daoris driver` reads no verdict, so it cannot see whether the
 * driver parked a quest or a stop holds it: the stop's sentence names the session, `--session` releases that stop, and a
 * retry without it marks the strikes, here at the count `--at` gives (RETRY1b reads it from the records when none is given).
 * A stop is not a strike, so a release moves no mark.
 */
test('retry --session releases a stop, and a retry without it still marks the strikes', () => {
  const fx = makeFixture('driver-release');

  const said = run(['retry', 'q1', '--session', 's1'], at(fx));
  const written = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(written.released, { q1: 's1' });
  assert.equal(written.forgiven.q1, undefined);
  assert.match(said.out, /#q1` is released from your stop of session `s1`/);
  assert.match(said.out, /A later stop holds it again/);

  // One release per quest, under the spelling first written; a `#` is no part of the id.
  run(['retry', '#Q1', '--session', 's2'], at(fx));
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).released, { q1: 's2' });

  run(['retry', 'q2', '--at', '3'], at(fx));
  const marked = readDriverChoices(at(fx));
  assert.equal(marked.forgiven.q2, 3);
  assert.equal(releasedFor(marked, 'q2'), null);
  assert.equal(releasedFor(marked, 'q1'), 's2');

  assert.match(run(['list'], at(fx)).out, /released\s+#q1\s+\(your stop of session s2\)/);
  assert.match(captureError(() => run(['retry', 'q1', '--session'], at(fx))).message, /`--session` needs the session your stop ended/);
  // A flag's value is never the quest (REV3), `--session`'s included.
  assert.match(captureError(() => run(['retry', '--session', 's1'], at(fx))).message, /needs a name/);
  assert.match(captureError(() => run(['retry', 'q1', '--session', 's1', '--at', '2'], at(fx))).message, /either/);
  fx.cleanup();
});

/**
 * `standing` (KNOWUSE1b, D135 §3): what the person has told this machine holds for every session in a repository, in their
 * words, with when it was set. 🔴 A TWIN with the driver's `DriverConfig.Standing`: `StandingTests.cs` holds this table row
 * for row, and the test below holds it to this one, cell for cell. A row's time is the moment read, in UTC to the second.
 */
const STANDING_ROWS: [name: string, file: string, repository: string, says: string | null, at: string | null][] = [
  ['absent is none', '{}', 'app', null, null],
  ['the person\'s words are the repository\'s answer', '{"standing":{"app":{"says":"dev writes allowed","at":"2026-10-03T09:00:00Z"}}}', 'app', 'dev writes allowed', '2026-10-03T09:00:00Z'],
  ['a repository is matched in any case', '{"standing":{"App":{"says":"dev only"}}}', 'app', 'dev only', null],
  ['the words are read without the spaces around them', '{"standing":{"app":{"says":"  dev only  "}}}', 'app', 'dev only', null],
  ['blank words are no answer', '{"standing":{"app":{"says":"  "}}}', 'app', null, null],
  ['words that are not text are not read', '{"standing":{"app":{"says":7}}}', 'app', null, null],
  ['an entry that is not an object is not read', '{"standing":{"app":"dev only"}}', 'app', null, null],
  ['a repository written twice in any case is read where first written', '{"standing":{"app":{"says":"first"},"APP":{"says":"second"}}}', 'app', 'first', null],
  ['a time that does not read leaves the answer standing with its time unknown', '{"standing":{"app":{"says":"dev only","at":"Oct 3"}}}', 'app', 'dev only', null],
  ['a time at an offset is read in UTC', '{"standing":{"app":{"says":"dev only","at":"2026-10-03T11:00:00+02:00"}}}', 'app', 'dev only', '2026-10-03T09:00:00Z'],
  ['another repository\'s answer is not this one\'s', '{"standing":{"api":{"says":"dev only"}}}', 'app', null, null],
  ['a list is not a map', '{"standing":["app"]}', 'app', null, null],
  ['null is absent', '{"standing":null}', 'app', null, null],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"standing":{"straße":{"says":"dev only"}}}', 'STRASSE', null, null],
  ['a dotted capital I is not an i with a dot above', '{"standing":{"İzmir":{"says":"dev only"}}}', 'i\u{307}zmir', null, null],
];

test('a standing answer reads as the driver reads it (the twin\'s table)', () => {
  const fx = makeFixture('driver-standing-read');
  for (const [name, file, repository, says, at_] of STANDING_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    const read = standingFor(readDriverChoices(at(fx)), repository);
    assert.equal(read?.says ?? null, says, name);
    assert.equal(read?.at ? read.at.toISOString().replace(/\.\d{3}Z$/, 'Z') : null, at_, `${name}: at`);
  }
  fx.cleanup();
});

test('the driver’s standing table is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'StandingTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'Standing_reads_as_the_cli_reads_it', {}, 'StandingTests'), STANDING_ROWS);
});

/**
 * The terminal's door onto a standing answer (KNOWUSE1b, D50): set in the person's words, replaced under the spelling first
 * written, listed, and cleared; the repository's page and Ask Daoris's `setting` kind are the other doors.
 */
test('standing keeps an answer for a repository, replaces it, lists it and clears it', () => {
  const fx = makeFixture('driver-standing');

  const said = run(['standing', 'Work-App', 'dev writes allowed;', 'test locally against dev;', 'prod only on a yes'], at(fx));
  const written = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal(written.standing['Work-App'].says, 'dev writes allowed; test locally against dev; prod only on a yes');
  assert.match(written.standing['Work-App'].at, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/);
  assert.match(said.out, /every session in `Work-App` is handed your standing answer/);
  assert.match(said.out, /never written into the repository/);

  run(['standing', 'work-app', 'dev only'], at(fx));
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(at(fx), 'utf8')).standing), ['Work-App']);
  assert.equal(standingFor(readDriverChoices(at(fx)), 'WORK-APP')?.says, 'dev only');
  assert.match(run(['list'], at(fx)).out, /standing\s+Work-App\s+"dev only"/);

  const cleared = run(['standing', 'work-app', '--clear'], at(fx));
  assert.equal('standing' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  assert.match(cleared.out, /keeps no standing answer/);
  fx.cleanup();
});

test('standing refuses no repository, no words and words past the bound, and writes nothing', () => {
  const fx = makeFixture('driver-standing-refused');
  assert.match(captureError(() => run(['standing'], at(fx))).message, /`driver standing` needs <repository>/);
  assert.match(captureError(() => run(['standing', 'app'], at(fx))).message, /`driver standing` needs <repository>/);
  assert.match(captureError(() => run(['standing', 'app', 'x'.repeat(2001)], at(fx))).message, /at most 2,000 characters/);
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('a standing answer is written only once set, and a verb that knows nothing of it preserves it', () => {
  const fx = makeFixture('driver-standing-preserve');
  run(['drive', 'app'], at(fx));
  assert.equal('standing' in JSON.parse(readFileSync(at(fx), 'utf8')), false);

  run(['standing', 'app', 'dev only'], at(fx));
  run(['hold', 'app'], at(fx));
  run(['retry', 'q1', '--session', 's1'], at(fx));

  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).standing.app.says, 'dev only');
  fx.cleanup();
});

/**
 * The session languages' closed table (LANG1c, D142 point 7): each code and the name the line gives the agent. 🔴 A TWIN with
 * the driver's `SessionLanguages.Table`: `SessionLanguageTests.cs` holds this table row for row, and the test below holds it
 * to this one, cell for cell.
 */
const LANGUAGE_TABLE_ROWS: [code: string, name: string][] = [
  ['en', 'English'],
  ['zh', 'Simplified Chinese (简体中文)'],
];

test('the session languages are the twin\'s closed table, in its order', () => {
  assert.deepEqual(Object.entries(SESSION_LANGUAGES), LANGUAGE_TABLE_ROWS);
});

test('the driver’s language table is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'SessionLanguageTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'The_table_names_each_language_for_the_agent', {}, 'SessionLanguageTests'), LANGUAGE_TABLE_ROWS);
});

/**
 * `languages` and `workspaceLanguages` (LANG1c, D142 point 7): the language a session is asked to write to the person in, the
 * repository's winning over its workspace's, neither set none. 🔴 A TWIN with the driver's `SessionLanguages.Resolve`:
 * `SessionLanguageTests.cs` holds this table row for row, and the test below holds it to this one, cell for cell.
 */
const LANGUAGE_ROWS: [name: string, file: string, repository: string, workspace: string | null, code: string | null, source: string | null][] = [
  ['absent is none', '{}', 'app', 'work', null, null],
  ['a repository\'s language is its own', '{"languages":{"app":"zh"}}', 'app', 'work', 'zh', 'repository'],
  ['a workspace\'s language is each repository\'s there that sets none', '{"workspaceLanguages":{"work":"zh"}}', 'app', 'work', 'zh', 'workspace'],
  ['the repository\'s wins over its workspace\'s', '{"languages":{"app":"en"},"workspaceLanguages":{"work":"zh"}}', 'app', 'work', 'en', 'repository'],
  ['another workspace\'s is not this one\'s', '{"workspaceLanguages":{"home":"zh"}}', 'app', 'work', null, null],
  ['another repository\'s is not this one\'s', '{"languages":{"api":"zh"}}', 'app', 'work', null, null],
  ['a repository in no workspace takes the default one\'s', '{"workspaceLanguages":{"default":"zh"}}', 'app', null, 'zh', 'workspace'],
  ['a repository is matched in any case', '{"languages":{"App":"zh"}}', 'app', 'work', 'zh', 'repository'],
  ['a workspace is matched in any case', '{"workspaceLanguages":{"Work":"zh"}}', 'app', 'work', 'zh', 'workspace'],
  ['a code is read in any case, without the spaces around it', '{"languages":{"app":" ZH "}}', 'app', 'work', 'zh', 'repository'],
  ['a code the table does not hold is not read, and the workspace\'s stands', '{"languages":{"app":"fr"},"workspaceLanguages":{"work":"zh"}}', 'app', 'work', 'zh', 'workspace'],
  ['a value that is not text is not read', '{"languages":{"app":7}}', 'app', 'work', null, null],
  ['a repository written twice in any case is read where first written', '{"languages":{"app":"zh","APP":"en"}}', 'app', 'work', 'zh', 'repository'],
  ['a list is not a map', '{"languages":["app"]}', 'app', 'work', null, null],
  ['null is absent', '{"languages":null,"workspaceLanguages":null}', 'app', 'work', null, null],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"languages":{"straße":"zh"}}', 'STRASSE', 'work', null, null],
  ['a dotted capital I is not an i with a dot above', '{"languages":{"İzmir":"zh"}}', 'i\u{307}zmir', 'work', null, null],
];

test('a session language resolves as the driver resolves it (the twin\'s table)', () => {
  const fx = makeFixture('driver-language-read');
  for (const [name, file, repository, workspace, code, source] of LANGUAGE_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    const read = languageFor(readDriverChoices(at(fx)), repository, workspace);
    assert.equal(read?.code ?? null, code, name);
    assert.equal(read?.source ?? null, source, `${name}: from`);
    if (read) assert.equal(read.name, SESSION_LANGUAGES[read.code], `${name}: named by the table`);
  }
  fx.cleanup();
});

test('the driver’s language reading is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'SessionLanguageTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'Languages_read_as_the_cli_reads_them', {}, 'SessionLanguageTests'), LANGUAGE_ROWS);
});

/**
 * The terminal's door onto the session language (LANG1c, D50): set for a repository or a workspace, replaced under the spelling
 * first written, listed with where each was set, and cleared; a repository's page, the workspace's page and Ask Daoris are
 * its other doors.
 */
test('language sets a repository\'s and a workspace\'s, replaces, lists and clears them', () => {
  const fx = makeFixture('driver-language');
  assert.match(run(['list'], at(fx)).out, /language\s+none set/);

  const said = run(['language', 'Work-App', 'ZH'], at(fx));
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).languages, { 'Work-App': 'zh' });
  assert.match(said.out, /sessions in `Work-App` write to you in Simplified Chinese \(简体中文\)/);
  assert.match(said.out, /Settings → Appearance, and neither sets the other/);

  const shared = run(['language', '--workspace', 'work', 'en'], at(fx));
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).workspaceLanguages, { work: 'en' });
  assert.match(shared.out, /each repository in the workspace `work` that sets none of its own write to you in English/);
  assert.match(shared.out, /daoris driver language <repository> --clear/);

  run(['language', 'work-app', 'en'], at(fx));
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).languages, { 'Work-App': 'en' });
  const listed = run(['list'], at(fx)).out;
  assert.match(listed, /language\s+Work-App\s+en \(English\)\s+\(set for it\)/);
  assert.match(listed, /language\s+workspace work\s+en \(English\)\s+\(for each repository there that sets none\)/);
  assert.doesNotMatch(listed, /language\s+none set/);
  assert.equal(languageFor(readDriverChoices(at(fx)), 'other', 'WORK')?.source, 'workspace');

  const cleared = run(['language', 'work-app', '--clear'], at(fx));
  assert.equal('languages' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  assert.match(cleared.out, /`Work-App` takes its workspace's session language again, else none/);
  run(['language', '--workspace', 'WORK', '--clear'], at(fx));
  assert.equal('workspaceLanguages' in JSON.parse(readFileSync(at(fx), 'utf8')), false);
  fx.cleanup();
});

test('language refuses a code the table does not hold, and no name or no language, and writes nothing', () => {
  const fx = makeFixture('driver-language-refused');
  assert.equal(captureError(() => run(['language', 'app', 'fr'], at(fx))).message,
    '`fr` is not a language a session can be asked to write in here — one of `en`, `zh`.');
  assert.match(captureError(() => run(['language'], at(fx))).message, /`driver language` needs <repository>\|--workspace <name>/);
  assert.match(captureError(() => run(['language', 'app'], at(fx))).message, /then en\|zh\|--clear/);
  assert.match(captureError(() => run(['language', '--workspace', 'work'], at(fx))).message, /then en\|zh\|--clear/);
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('a session language is written only once set, and a verb that knows nothing of it preserves it', () => {
  const fx = makeFixture('driver-language-preserve');
  run(['drive', 'app'], at(fx));
  const fresh = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal('languages' in fresh, false);
  assert.equal('workspaceLanguages' in fresh, false);

  run(['language', 'app', 'zh'], at(fx));
  run(['language', '--workspace', 'work', 'en'], at(fx));
  run(['hold', 'app'], at(fx));
  run(['standing', 'app', 'dev only'], at(fx));

  const kept = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(kept.languages, { app: 'zh' });
  assert.deepEqual(kept.workspaceLanguages, { work: 'en' });
  fx.cleanup();
});

test('a release is written only once set, and a verb that knows nothing of it preserves it', () => {
  const fx = makeFixture('driver-released-preserve');
  run(['drive', 'engine'], at(fx));
  assert.equal('released' in JSON.parse(readFileSync(at(fx), 'utf8')), false);

  run(['retry', 'q1', '--session', 's1'], at(fx));
  run(['hold', 'engine'], at(fx));
  run(['strikes', '5'], at(fx));

  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).released, { q1: 's1' });
  fx.cleanup();
});

/**
 * `pausedAsks` and `pausedQuests` (PAUSE1a, D132 point 5, design §2.5): each pause by its ask or quest, with when it was made
 * and the stops it made. 🔴 A TWIN with the driver's `DriverConfig.PausedAsks` and `PausedQuests`: `PausedWorkTests.cs` holds
 * this table row for row, and the test below holds it to this one, cell for cell. A row's `at` is the moment read, in UTC to
 * the second; its `stopped` each `quest=session`, sorted and joined by commas.
 */
const PAUSE_ROWS: [name: string, file: string, scope: string, id: string, paused: boolean, at: string | null, stopped: string][] = [
  ['absent is no pause', '{}', 'ask', 'a1', false, null, ''],
  ['an ask paused, with when and the stops its pause made', '{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11Z","stopped":{"q7":"s-1"}}}}', 'ask', 'a1', true, '2026-10-02T14:02:11Z', 'q7=s-1'],
  ['a quest paused that stopped nothing', '{"pausedQuests":{"q9":{"at":"2026-10-02T14:05:40Z","stopped":{}}}}', 'quest', 'q9', true, '2026-10-02T14:05:40Z', ''],
  ['an ask\'s pause is not a quest\'s', '{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}', 'quest', 'a1', false, null, ''],
  ['another ask\'s pause is not this one\'s', '{"pausedAsks":{"a2":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}', 'ask', 'a1', false, null, ''],
  ['an id is matched in any case', '{"pausedAsks":{"A1":{"at":"2026-10-02T14:02:11Z","stopped":{}}}}', 'ask', 'a1', true, '2026-10-02T14:02:11Z', ''],
  ['an id written twice in any case is read where first written', '{"pausedQuests":{"q9":{"at":"2026-10-02T14:05:40Z"},"Q9":{"at":"2026-10-03T00:00:00Z"}}}', 'quest', 'q9', true, '2026-10-02T14:05:40Z', ''],
  ['a time at an offset is read as its moment', '{"pausedAsks":{"a1":{"at":"2026-10-02T16:02:11+02:00"}}}', 'ask', 'a1', true, '2026-10-02T14:02:11Z', ''],
  ['a time with a fraction is read to the second', '{"pausedAsks":{"a1":{"at":"2026-10-02T14:02:11.5Z"}}}', 'ask', 'a1', true, '2026-10-02T14:02:11Z', ''],
  ['a time that is not ISO 8601 is unknown, and the pause still holds', '{"pausedAsks":{"a1":{"at":"Oct 2"}}}', 'ask', 'a1', true, null, ''],
  ['a date that does not exist is unknown, and the pause still holds', '{"pausedAsks":{"a1":{"at":"2026-02-30T00:00:00Z"}}}', 'ask', 'a1', true, null, ''],
  ['a pause with no time or stops is still a pause', '{"pausedAsks":{"a1":{}}}', 'ask', 'a1', true, null, ''],
  ['a stopped session is read without the spaces around it, and a blank one or one that is not text is not read', '{"pausedAsks":{"a1":{"stopped":{"q1":" s-1 ","q2":"  ","q3":7}}}}', 'ask', 'a1', true, null, 'q1=s-1'],
  ['a quest stopped twice in any case is read where first written', '{"pausedAsks":{"a1":{"stopped":{"q1":"s-1","Q1":"s-2","q2":"s-3"}}}}', 'ask', 'a1', true, null, 'q1=s-1,q2=s-3'],
  ['stops that are not a map are none', '{"pausedAsks":{"a1":{"stopped":["q1"]}}}', 'ask', 'a1', true, null, ''],
  ['an entry that is not an object is no pause', '{"pausedAsks":{"a1":true}}', 'ask', 'a1', false, null, ''],
  ['a list is not a map', '{"pausedAsks":["a1"]}', 'ask', 'a1', false, null, ''],
  ['null is absent', '{"pausedQuests":null}', 'quest', 'q9', false, null, ''],
  ['a letter whose capital is two letters is not those two: straße is not STRASSE', '{"pausedAsks":{"straße":{}}}', 'ask', 'STRASSE', false, null, ''],
  ['a dotted capital I is not an i with a dot above', '{"pausedAsks":{"İzmir":{}}}', 'ask', 'i\u{307}zmir', false, null, ''],
];

/** A moment in UTC to the second, as both tables spell it. */
function second(at: Date | null): string | null {
  return at === null ? null : at.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

/** A pause's stops as both tables spell them: each `quest=session`, sorted, joined by commas. */
function spelled(stopped: Record<string, string>): string {
  return Object.keys(stopped).sort().map((quest) => `${quest}=${stopped[quest]}`).join(',');
}

test('a pause reads as the driver reads it (the twin\'s table)', () => {
  const fx = makeFixture('driver-paused-read');
  for (const [name, file, scope, id, paused, moment, stopped] of PAUSE_ROWS) {
    writeFileSync(at(fx), file, 'utf8');
    const choices = readDriverChoices(at(fx));
    const pause = scope === 'ask' ? pausedAsk(choices, id) : pausedQuest(choices, id);
    const read = pause === null ? [false, null, ''] : [true, second(pause.at), spelled(pause.stopped)];
    assert.deepEqual(read, [paused, moment, stopped], name);
  }
  fx.cleanup();
});

test('the driver’s pause table is this table, row for row and in this order', () => {
  const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
    'Daoris.Desktop.Driver.Tests', 'PausedWorkTests.cs'), 'utf8').replace(/\r\n/g, '\n');

  assert.deepEqual(csharpRows(source, 'Pauses_read_as_the_cli_reads_them', {}, 'PausedWorkTests'), PAUSE_ROWS);
});

/**
 * 🔴 This editor never edits a pause (design §2.5): pausing and resuming are `daoris-driver`'s, which reads the work and
 * reaches the running loop. So every verb keeps the pauses exactly as written, an entry it would not read included, and a
 * file that holds none is given none.
 */
test('a verb that knows nothing of pauses keeps them as written, and a file without them gets none', () => {
  const fx = makeFixture('driver-paused-preserve');
  run(['drive', 'engine'], at(fx));
  const fresh = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal('pausedAsks' in fresh || 'pausedQuests' in fresh, false);

  const pausedAsks = {
    a1: { at: '2026-10-02T14:02:11Z', stopped: { q7: 's-1a2b3c4d' }, door: 'screen' },
    a2: true,
  };
  const pausedQuests = { q9: { at: 'Oct 2', stopped: {} } };
  writeFileSync(at(fx), JSON.stringify({ ...fresh, pausedAsks, pausedQuests }), 'utf8');

  run(['hold', 'engine'], at(fx));
  run(['retry', 'q1', '--session', 's1'], at(fx));
  run(['strikes', '5'], at(fx));

  const kept = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(kept.pausedAsks, pausedAsks);
  assert.deepEqual(kept.pausedQuests, pausedQuests);
  fx.cleanup();
});

test('list shows each pause, when it was made, the stops it made and the door that resumes it', () => {
  const fx = makeFixture('driver-paused-list');
  writeFileSync(at(fx), JSON.stringify({
    pausedAsks: { a1b2c3: { at: '2026-10-02T14:02:11Z', stopped: { q7f3e1: 's-1a2b3c4d', q2: 's-2' } } },
    pausedQuests: { q9d0aa: { stopped: {} } },
  }), 'utf8');

  const out = run(['list'], at(fx)).out;
  assert.match(out, /paused\s+ask #a1b2c3\s+\(since 2026-10-02T14:02:11Z; its pause stopped #q2's session s-2, #q7f3e1's session s-1a2b3c4d\)/);
  assert.match(out, /paused\s+quest #q9d0aa\s+\(when is not recorded\)/);
  // PAUSE1b: the resume is daoris-driver's, which reads the work and reaches the running loop (D132 §7.2); `driver` names it.
  assert.match(out, /paused\s+ask #a1b2c3 .*— resume: daoris-driver ask --resume a1b2c3\n/);
  assert.match(out, /paused\s+quest #q9d0aa .*— resume: daoris-driver quest resume q9d0aa\n/);
  // A pause's words say paused, never held (design §8.1).
  assert.doesNotMatch(out.split('\n').filter((line) => line.includes('paused')).join('\n'), /held/);
  fx.cleanup();
});

test('a file written before strikes existed gets the default rather than none', () => {
  const fx = makeFixture('driver-strikes-absent');
  writeFileSync(at(fx), `${JSON.stringify({ drivable: ['Game'], cap: 2 }, null, 2)}\n`, 'utf8');

  assert.equal(readDriverChoices(at(fx)).strikes, 3);

  fx.cleanup();
});

/**
 * A verb that may answer later (RETRY1b): `retry <quest>` without `--at` counts the quest's failures from the records the
 * reader hands it, which the `driver` row hands in from the service client.
 */
async function retried(argv: string[], path: string, records: RecordsReader): Promise<{ code: number; out: string }> {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = await commandDriver({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }, records);
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

/** Six failures on `q1`, as the records door answers them, beside records that are no strike against it. */
const SECOND_PARK = [
  ...['s1', 's2', 's3', 's4', 's5'].map((id) => ({ id, quest: 'q1', repository: 'Game', state: 'failed' })),
  { id: 's6', quest: 'q1', repository: 'Game', state: 'stopped', interrupted: true },
  { id: 's7', quest: 'q1', repository: 'Game', state: 'failed', limit: true },
  { id: 's8', quest: 'q1', repository: 'Game', state: 'stopped' },
  { id: 'origin/s9', quest: 'q1', repository: 'Game', state: 'failed' },
  { id: 's10', quest: 'q2', repository: 'Game', state: 'failed' },
];

/**
 * RETRY1b: a quest retried once at the limit (3) and parked again has six failures, so a mark at the limit left it parked
 * (6 − 3 is still 3), as both doors did on the install. Without `--at`, the retry counts its failures from this machine's
 * records as the driver does and marks it there, so the planner's count (failures less the mark) is under the limit and
 * the quest starts; three more park it again.
 */
test('a retry after a second park marks the quest at its failures as the records count them, so it starts', async () => {
  const fx = makeFixture('driver-retry-second-park');
  run(['retry', 'q1', '--at', '3'], at(fx));

  const said = await retried(['retry', '#q1'], at(fx), async () => ({ records: SECOND_PARK }));

  const choices = readDriverChoices(at(fx));
  assert.equal(said.code, 0);
  assert.equal(choices.forgiven.q1, 6);
  // The planner's own expression (`Planner.cs`): the quest starts while failures less the mark are under the limit.
  assert.ok(6 - choices.forgiven.q1! < choices.strikes, 'the mark leaves the quest parked');
  assert.match(said.out, /quest `#q1` may be started again/);
  assert.match(said.out, /Counting from 6 failure\(s\), as this machine's records count them/);
  assert.match(said.out, /3 more failure\(s\) will park it again/);
  fx.cleanup();
});

/**
 * RETRY1b: a terminal that cannot read the records never guesses a mark. It says why, writes nothing, and says how to give
 * the count: the failures the quest's *Sitting* names are counted past its mark, so the mark is added back.
 */
test('a retry that cannot read the records refuses, says how to give --at, and writes nothing', async () => {
  const fx = makeFixture('driver-retry-unread');
  run(['retry', 'q1', '--at', '3'], at(fx));
  const unread = async () => ({ unread: 'no DAORIS_SERVICE_URL is set' });

  await assert.rejects(retried(['retry', 'q1'], at(fx), unread), (error: Error) => {
    assert.match(error.message, /^cannot count the failures of `#q1`: this machine's session records could not be read — no DAORIS_SERVICE_URL is set\./);
    assert.match(error.message, /Nothing was written\./);
    assert.match(error.message, /`daoris driver retry q1 --at <n>`, with n the failures its \*Sitting\* names plus 3, the mark its last retry left\./);
    assert.match(error.message, /The quest's page's \*Try again\* counts them for you\./);
    return true;
  });
  assert.equal(readDriverChoices(at(fx)).forgiven.q1, 3);

  // A quest never retried has no mark to add back.
  await assert.rejects(retried(['retry', 'q2'], at(fx), unread), (error: Error) => {
    assert.match(error.message, /`daoris driver retry q2 --at <n>`, with n the failures its \*Sitting\* names\./);
    return true;
  });
  assert.equal(readDriverChoices(at(fx)).forgiven.q2, undefined);
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

/**
 * A relationship's names compare as the driver's `AcrossRules` and `DriverConfig` compare them, by `OrdinalIgnoreCase`
 * (CASEFOLD1, `casefold.ts`): a name full case mapping would lower to the same letters is another repository, so it is
 * neither refused as the repository's own, nor read as a repeat, nor cleared with the other.
 */
test('a relationship names another repository only as the driver parts them: İzmir is not i̇zmir', () => {
  assert.equal(writeAcrossProblem('İzmir', 'i\u{307}zmir'), null);
  assert.equal(writeAcrossProblem('straße', 'STRASSE'), null);

  const fx = makeFixture('driver-across-case');
  writeFileSync(at(fx), JSON.stringify({ writeAcross: { plugins: ['İzmir', 'i\u{307}zmir', 'straße', 'STRASSE'] } }));
  assert.deepEqual(readDriverChoices(at(fx)).writeAcross, { plugins: ['İzmir', 'i\u{307}zmir', 'straße', 'STRASSE'] });

  run(['across', 'plugins', 'write-to', 'i\u{307}zmir', '--clear'], at(fx));
  assert.deepEqual(readDriverChoices(at(fx)).writeAcross, { plugins: ['İzmir', 'straße', 'STRASSE'] });
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

/**
 * The review rule, `reviews` and `workspaceReviews` (REVIEWENV1a, D154 point 2, the review-environment design §1.1–§1.3,
 * §1.7). 🔴 A TWIN with the driver's `ReviewRules`: both hold ONE table, the driver suite's `fixtures/review-rules.json`, cell for cell — the
 * reading and its precedence, every refusal in the same words, each door's sentences, the edits both doors make, and what a
 * checkout holding a procedure means. The driver's `ReviewRulesTests` reads the same file.
 */
const REVIEW_TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'review-rules.json'), 'utf8')) as {
  read: [name: string, file: string, repository: string, workspace: string | null, source: string | null, rule: string | null][];
  problems: [name: string, value: string, problem: string | null][];
  says: [name: string, rule: string, sentences: string[]][];
  gate: [name: string, rule: string, sentences: string[]][];
  edits: [name: string, file: string, edit: string, after: string | null, refusal: string | null][];
  procedures: [name: string, files: string[], procedure: string, holds: boolean][];
};

test('a review rule resolves as the driver resolves it (the shared table)', () => {
  const fx = makeFixture('driver-review-read');
  for (const [name, file, repository, workspace, source, rule] of REVIEW_TABLE.read) {
    writeFileSync(at(fx), file, 'utf8');
    const read = reviewFor(readDriverChoices(at(fx)), repository, workspace);
    assert.equal(read?.source ?? null, source, name);
    assert.deepEqual(read === null ? null : read.rule, rule === null ? null : JSON.parse(rule), `${name}: the rule`);
  }
  fx.cleanup();
});

test('a review rule\'s first problem is the driver\'s, in its words (the shared table)', () => {
  for (const [name, value, problem] of REVIEW_TABLE.problems) {
    assert.equal(reviewRuleOf(JSON.parse(value)).problem, problem, name);
  }
});

test('each door says what a review rule lets a step do, in the driver\'s words (the shared table)', () => {
  for (const [name, rule, sentences] of REVIEW_TABLE.says) {
    const read = reviewRuleOf(JSON.parse(rule));
    assert.equal(read.problem, null, `${name}: the rule reads`);
    assert.deepEqual(reviewSays(read.rule!), sentences, name);
  }
});

/**
 * REVIEWENV1c2: what each door says after the rule's sentences, now that the landing's gate reads the rule (REVIEWENV1c): that
 * work waiting for the person's review lands only on their verdict, by the terminal's door, and that no set-up step is composed
 * for them yet; nothing after none here. One sentence on each side, held to the shared table's `gate` rows.
 */
test('each door says what the gate does with a review rule, in the driver\'s words (the shared table)', () => {
  for (const [name, rule, sentences] of REVIEW_TABLE.gate) {
    const read = reviewRuleOf(JSON.parse(rule));
    assert.equal(read.problem, null, `${name}: the rule reads`);
    assert.deepEqual(reviewGate(read.rule!), sentences, name);
  }
  assert.deepEqual(reviewGate({ environments: [{ name: 'dev', kind: 'deployed', procedure: 'README.md' }] }), [REVIEW_WAITING]);
});

test('a review edit writes what the driver writes, or refuses in its words (the shared table)', () => {
  const fx = makeFixture('driver-review-edits');
  for (const [name, file, edit, after, refusal] of REVIEW_TABLE.edits) {
    writeFileSync(at(fx), file, 'utf8');
    const choices = readDriverChoices(at(fx));
    if (refusal !== null) {
      assert.equal(captureError(() => applyReviewEdit(choices, JSON.parse(edit))).message, refusal, name);
      continue;
    }

    writeDriverChoices(at(fx), { ...choices, ...applyReviewEdit(choices, JSON.parse(edit)) });
    const written = JSON.parse(readFileSync(at(fx), 'utf8'));
    const maps = Object.fromEntries(['reviews', 'workspaceReviews'].filter((key) => key in written).map((key) => [key, written[key]]));
    assert.deepEqual(maps, JSON.parse(after!), name);
  }
  fx.cleanup();
});

test('a checkout holds a procedure only as a regular file at that path (the shared table)', () => {
  for (const [name, files, procedure, holds] of REVIEW_TABLE.procedures) {
    const fx = makeFixture('driver-review-procedure');
    for (const file of files) fx.write(file, '# how\n');
    assert.equal(holdsProcedure(fx.root, procedure), holds, name);
    fx.cleanup();
  }
});

/** Each repository's checkout as the registry would answer it to `driver review`, through the one module that reads it. */
function checkoutsOf(rows: { repository: string; workspace: string; root: string | null }[]): CheckoutsReader {
  return async () => ({ checkouts: rows });
}

async function runReview(argv: string[], path: string, checkouts: CheckoutsReader): Promise<{ code: number; out: string }> {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = await commandDriver({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }, undefined, checkouts);
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

async function reviewRefusal(argv: string[], path: string, checkouts: CheckoutsReader): Promise<string> {
  try {
    await runReview(argv, path, checkouts);
  } catch (error) {
    return (error as Error).message;
  }
  throw new Error('expected a refusal');
}

/**
 * The terminal's door onto the review rule (REVIEWENV1a, D50; design §1.7): an environment added or replaced, keeping the
 * others; `none`; `--drop`; `--required|--not-required`; `--clear`; and `list`. Each says what it lets a step do, in the
 * driver's sentences, and what the landing's gate does with it (REVIEWENV1c2).
 */
test('review declares a repository\'s environment, says what it lets a step do, checks the procedure, and lists it', async () => {
  const fx = makeFixture('driver-review');
  const checkout = join(fx.root, 'storefront');
  fx.write('storefront/README.md', '# Run it against dev\n');
  const checkouts = checkoutsOf([{ repository: 'storefront', workspace: 'work', root: checkout }]);
  assert.match(run(['list'], at(fx)).out, /review\s+none set/);

  const said = await runReview(['review', 'storefront', 'dev', '--kind', 'local', '--procedure', 'README.md',
    '--address', 'http://localhost:4200', '--required'], at(fx), checkouts);
  assert.equal(said.code, 0);
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).reviews, {
    storefront: { required: true, environments: [{ name: 'dev', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' }] },
  });
  assert.match(said.out, /`storefront` declares the review environment `dev`, its default/);
  assert.match(said.out, /Before work here lands, it is shown to you in `dev` and waits for you to say it is right\./);
  assert.match(said.out, /shows it in Daoris's browser at `http:\/\/localhost:4200`; your own servers and processes are not touched/);
  assert.ok(said.out.includes(`  ${REVIEW_WAITING}`), 'what the gate does with it');
  assert.doesNotMatch(said.out, /Declared only|nothing reads it yet/);
  assert.match(said.out, /`README\.md` is in `storefront`'s checkout here/);

  const listed = run(['list'], at(fx)).out;
  assert.match(listed, /review\s+storefront\s+dev \(local, http:\/\/localhost:4200, by README\.md\), required before landing/);
  assert.doesNotMatch(listed, /review\s+none set/);
  fx.cleanup();
});

test('review refuses a procedure the repository\'s checkout does not hold, and writes nothing', async () => {
  const fx = makeFixture('driver-review-missing');
  const checkout = join(fx.root, 'storefront');
  fx.write('storefront/README.md', '# Run it\n');
  const message = await reviewRefusal(['review', 'storefront', 'dev', '--kind', 'deployed', '--procedure', 'docs/deploying-to-dev.md'],
    at(fx), checkoutsOf([{ repository: 'StoreFront', workspace: 'work', root: checkout }]));

  assert.match(message, /`docs\/deploying-to-dev\.md` is not a file in `storefront`'s checkout here/);
  assert.match(message, /Nothing was written/);
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('review writes a rule it could not check, and says so', async () => {
  const fx = makeFixture('driver-review-unchecked');
  const nowhere = await runReview(['review', 'storefront', 'dev', '--kind', 'deployed', '--procedure', 'README.md'],
    at(fx), checkoutsOf([{ repository: 'storefront', workspace: 'work', root: null }]));
  assert.match(nowhere.out, /Not checked: `storefront` has no checkout on this machine; a set-up step sits until its tree holds `README\.md`/);

  const unread = await runReview(['review', 'media-api', 'dev', '--kind', 'deployed', '--procedure', 'README.md'],
    at(fx), async () => ({ unread: 'no DAORIS_SERVICE_URL is set' }));
  assert.match(unread.out, /Not checked: the registry could not be read — no DAORIS_SERVICE_URL is set/);
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(at(fx), 'utf8')).reviews), ['storefront', 'media-api']);
  fx.cleanup();
});

test('review for a workspace names each repository there whose checkout lacks the procedure, and writes the rule', async () => {
  const fx = makeFixture('driver-review-workspace');
  fx.write('storefront/README.md', '# Run it\n');
  fx.write('media-api/src/main.ts', '');
  const said = await runReview(['review', '--workspace', 'work', 'local', '--kind', 'local', '--procedure', 'README.md',
    '--address', 'http://localhost:4200', '--run', 'npm run serve'], at(fx), checkoutsOf([
    { repository: 'storefront', workspace: 'work', root: join(fx.root, 'storefront') },
    { repository: 'media-api', workspace: 'Work', root: join(fx.root, 'media-api') },
    { repository: 'elsewhere', workspace: 'home', root: join(fx.root, 'elsewhere') },
  ]));

  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).workspaceReviews, {
    work: { environments: [{ name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200', run: 'npm run serve' }] },
  });
  assert.match(said.out, /repositories in the workspace `work` that set none of their own declare the review environment `local`/);
  assert.match(said.out, /may run `npm run serve` in its tree on a port nobody holds, without asking you each time/);
  assert.match(said.out, /`media-api` holds no `README\.md` here: its set-up steps sit until it does/);
  assert.doesNotMatch(said.out, /`storefront` holds no/);
  assert.doesNotMatch(said.out, /elsewhere/);
  assert.match(run(['list'], at(fx)).out, /review\s+workspace work\s+local \(local, http:\/\/localhost:4200, by README\.md, runs `npm run serve`\), on a task's asking\s+\(for each repository there that sets none\)/);
  fx.cleanup();
});

test('review none, --drop, --not-required and --clear each change only what they name, and say it', async () => {
  const fx = makeFixture('driver-review-edit');
  const checkouts = checkoutsOf([]);
  await runReview(['review', 'storefront', 'local', '--kind', 'local', '--procedure', 'README.md', '--address', 'http://localhost:4200', '--required'], at(fx), checkouts);
  await runReview(['review', 'storefront', 'dev', '--kind', 'deployed', '--procedure', 'docs/deploying-to-dev.md'], at(fx), checkouts);

  const dropped = await runReview(['review', 'storefront', '--drop', 'local'], at(fx), checkouts);
  assert.match(dropped.out, /`storefront` no longer declares `local`; `dev` is its default now/);
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).reviews.storefront.environments.map((each: { name: string }) => each.name), ['dev']);

  const relaxed = await runReview(['review', 'storefront', '--not-required'], at(fx), checkouts);
  assert.equal('required' in JSON.parse(readFileSync(at(fx), 'utf8')).reviews.storefront, false);
  assert.match(relaxed.out, /When a task asks for it, work here may be shown to you in `dev`; nothing waits for it\./);

  const none = await runReview(['review', 'media-api', 'none'], at(fx), checkouts);
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).reviews['media-api'], false);
  assert.match(none.out, /No review environment here, whatever its workspace says/);
  // Nothing waits where a repository has none, so the gate's sentence is not said after it (REVIEWENV1c2).
  assert.equal(none.out.includes(REVIEW_WAITING), false);
  assert.match(run(['list'], at(fx)).out, /review\s+media-api\s+none here, whatever its workspace says/);

  const cleared = await runReview(['review', 'storefront', '--clear'], at(fx), checkouts);
  assert.equal('storefront' in JSON.parse(readFileSync(at(fx), 'utf8')).reviews, false);
  assert.match(cleared.out, /`storefront` takes its workspace's review rule again, else none/);
  fx.cleanup();
});

test('review refuses production, a missing name and an unknown form, in the driver\'s words, and writes nothing', async () => {
  const fx = makeFixture('driver-review-refused');
  const checkouts = checkoutsOf([]);
  assert.equal(await reviewRefusal(['review', 'storefront', 'prod', '--kind', 'deployed', '--procedure', 'README.md'], at(fx), checkouts),
    '`prod` reads as production, and production is never a review environment — name where work is looked at before it lands, such as `local` or `dev`.');
  assert.match(await reviewRefusal(['review'], at(fx), checkouts), /`driver review` needs <repository>\|--workspace <name>/);
  assert.match(await reviewRefusal(['review', 'storefront'], at(fx), checkouts), /`driver review` needs <repository>\|--workspace <name>/);
  assert.equal(await reviewRefusal(['review', 'storefront', 'dev', '--procedure', 'README.md'], at(fx), checkouts),
    'an environment\'s `kind` is `local` or `deployed`.');
  assert.equal(await reviewRefusal(['review', 'storefront', 'none', '--required'], at(fx), checkouts),
    'one change at a time: add an environment (and whether it is required), drop one, say none, say whether it is required, or clear.');
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('a review rule is written only once set, and a verb that knows nothing of it preserves it', async () => {
  const fx = makeFixture('driver-review-preserve');
  run(['drive', 'storefront'], at(fx));
  const fresh = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal('reviews' in fresh, false);
  assert.equal('workspaceReviews' in fresh, false);

  await runReview(['review', 'storefront', 'none'], at(fx), checkoutsOf([]));
  await runReview(['review', '--workspace', 'work', 'dev', '--kind', 'deployed', '--procedure', 'README.md'], at(fx), checkoutsOf([]));
  run(['hold', 'storefront'], at(fx));
  run(['language', 'storefront', 'zh'], at(fx));

  const kept = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(kept.reviews, { storefront: false });
  assert.deepEqual(kept.workspaceReviews, { work: { environments: [{ name: 'dev', kind: 'deployed', procedure: 'README.md' }] } });
  fx.cleanup();
});

/**
 * The second-opinion rule, `opinions` and `workspaceOpinions` (XAGENT1a, D155 point 3, the second-agent design §2.2–§2.6).
 * 🔴 A TWIN with the driver's `OpinionRules`: both hold ONE table, the driver suite's `fixtures/opinion-rules.json`, cell for
 * cell — the reading and its precedence, every refusal in the same words, each door's sentences, the edits both doors make,
 * and which reviewers are the working agent's own family. The driver's `OpinionRulesTests` reads the same file.
 */
const OPINION_TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'opinion-rules.json'), 'utf8')) as {
  read: [name: string, file: string, repository: string, workspace: string | null, source: string | null, rule: string | null][];
  problems: [name: string, value: string, problem: string | null][];
  says: [name: string, rule: string, sameAgent: string[], sentences: string[]][];
  gate: [name: string, rule: string, sentences: string[]][];
  edits: [name: string, file: string, edit: string, after: string | null, refusal: string | null][];
  families: [name: string, working: string, reviewer: string, same: boolean][];
  pluginFamilies: [name: string, plugins: Record<string, string>, working: string, reviewer: string, same: boolean][];
};

/** A home holding each plugin folder of a row, its `plugin.json` written as the row spells it. */
function pluginHome(fx: { root: string }, plugins: Record<string, string>): string {
  for (const [folder, manifest] of Object.entries(plugins)) {
    mkdirSync(join(pluginsRoot(fx.root), folder), { recursive: true });
    writeFileSync(join(pluginsRoot(fx.root), folder, MANIFEST), manifest, 'utf8');
  }
  return fx.root;
}

test('an opinion rule resolves as the driver resolves it (the shared table)', () => {
  const fx = makeFixture('driver-opinion-read');
  for (const [name, file, repository, workspace, source, rule] of OPINION_TABLE.read) {
    writeFileSync(at(fx), file, 'utf8');
    const read = opinionFor(readDriverChoices(at(fx)), repository, workspace);
    assert.equal(read?.source ?? null, source, name);
    assert.deepEqual(read === null ? null : read.rule, rule === null ? null : JSON.parse(rule), `${name}: the rule`);
  }
  fx.cleanup();
});

test('an opinion rule\'s first problem is the driver\'s, in its words (the shared table)', () => {
  for (const [name, value, problem] of OPINION_TABLE.problems) {
    assert.equal(opinionRuleOf(JSON.parse(value)).problem, problem, name);
  }
});

test('each door says what an opinion rule lets a reviewer do, in the driver\'s words (the shared table)', () => {
  for (const [name, rule, sameAgent, sentences] of OPINION_TABLE.says) {
    const read = opinionRuleOf(JSON.parse(rule));
    assert.equal(read.problem, null, `${name}: the rule reads`);
    assert.deepEqual(opinionSays(read.rule!, sameAgent), sentences, name);
  }
});

/**
 * XAGENT1f4: what each door says after the rule's sentences, now that the gate reads the rule (XAGENT1f): what work waiting for
 * another agent's reading at landing, and a chain's next step under `steps`, wait for, with the person's terminal doors, and that
 * what is declared safe is not handed to a reviewer yet; nothing after none here. Held to the shared table's `gate` rows.
 */
test('each door says what the gate does with an opinion rule, in the driver\'s words (the shared table)', () => {
  assert.ok(OPINION_TABLE.gate.length > 0, 'the table holds gate rows');
  for (const [name, rule, sentences] of OPINION_TABLE.gate) {
    const read = opinionRuleOf(JSON.parse(rule));
    assert.equal(read.problem, null, `${name}: the rule reads`);
    assert.deepEqual(opinionGate(read.rule!), sentences, name);
    assert.ok(!opinionGate(read.rule!).includes(OPINION_DECLARED_ONLY), `${name}: never that nothing reads it`);
  }
});

/** The table's `gate` sentences for one of its rows, by name: what a door must say after that rule's own. */
function opinionGateRow(name: string): string[] {
  const row = OPINION_TABLE.gate.find(([each]) => each === name);
  assert.ok(row !== undefined, `the table's gate row "${name}"`);
  return row[2];
}

test('an opinion edit writes what the driver writes, or refuses in its words (the shared table)', () => {
  const fx = makeFixture('driver-opinion-edits');
  for (const [name, file, edit, after, refusal] of OPINION_TABLE.edits) {
    writeFileSync(at(fx), file, 'utf8');
    const choices = readDriverChoices(at(fx));
    if (refusal !== null) {
      assert.equal(captureError(() => applyOpinionEdit(choices, JSON.parse(edit))).message, refusal, name);
      continue;
    }

    writeDriverChoices(at(fx), { ...choices, ...applyOpinionEdit(choices, JSON.parse(edit)) });
    const written = JSON.parse(readFileSync(at(fx), 'utf8'));
    const maps = Object.fromEntries(['opinions', 'workspaceOpinions'].filter((key) => key in written).map((key) => [key, written[key]]));
    assert.deepEqual(maps, JSON.parse(after!), name);
  }
  fx.cleanup();
});

test('two agents are one family as the driver judges them, by the toolchains\' owners and makers (the shared table)', () => {
  for (const [name, working, reviewer, same] of OPINION_TABLE.families) {
    assert.equal(oneFamily(working, reviewer), same, name);
    assert.deepEqual(sameAgentOf({ on: ['landing'], reviewers: [reviewer] }, working), same ? [reviewer] : [], `${name}: the rule's`);
  }
});

/**
 * XAGENT1b2 (design §3.1): a plugin's harness is judged as the driver's choice judges it, by its `accountOf` and the maker its
 * plugin declares, read by this side's own catalogue from the same folders; a refused plugin contributes nothing.
 */
test('a plugin\'s agent is one family by its owner or its declared maker, as the driver judges it (the shared table)', () => {
  for (const [name, plugins, working, reviewer, same] of OPINION_TABLE.pluginFamilies) {
    const fx = makeFixture('driver-opinion-plugin-families');
    const catalog = readPlugins(pluginHome(fx, plugins));
    assert.equal(oneFamily(working, reviewer, catalog), same, name);
    assert.deepEqual(sameAgentOf({ on: ['landing'], reviewers: [reviewer] }, working, catalog), same ? [reviewer] : [], `${name}: the rule's`);
    fx.cleanup();
  }
});

/** The terminal's door reads the plugins beside its file (XAGENT1b2), as a landing rule's plugin is read there. */
test('opinion names a plugin\'s agent of the working agent\'s maker as the same agent, from the plugins beside the file', () => {
  const fx = makeFixture('driver-opinion-plugins');
  pluginHome(fx, {
    'acme.agent': '{"id":"acme.agent","harnesses":[{"name":"acme-agent","command":["acme"],"maker":"Acme"}]}',
    'acme.other': '{"id":"acme.other","harnesses":[{"name":"acme-other","command":["other"],"maker":"ACME"}]}',
    'older.agent': '{"id":"older.agent","harnesses":[{"name":"older-agent","command":["older"]}]}',
  });
  run(['adapter', 'acme-agent'], at(fx));

  const said = run(['opinion', 'web-app', '--reviewers', 'older-agent,acme-other'], at(fx));
  assert.equal(said.code, 0);
  assert.match(said.out, /  `acme-other` is the same agent as the one that does the work here: a fresh conversation/);
  assert.doesNotMatch(said.out, /`older-agent`[^\n]* the same agent as the one/);
  fx.cleanup();
});

/**
 * The terminal's door onto the second-opinion rule (XAGENT1a, D50; design §2.5): `--reviewers` and the switches set over what
 * stands, `none`, `--clear`, and `list`. Each says what it lets a reviewer do, in the driver's sentences, and what the gate
 * does with it, the table's `gate` row (XAGENT1f4).
 */
test('opinion declares a repository\'s reviewers, says what they do, and lists it', () => {
  const fx = makeFixture('driver-opinion');
  assert.match(run(['list'], at(fx)).out, /opinion\s+none set/);

  const said = run(['opinion', 'web-app', '--reviewers', 'codex-acp', '--on', 'landing,steps', '--verify', '--minutes', '30'], at(fx));
  assert.equal(said.code, 0);
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).opinions, {
    'web-app': { on: ['landing', 'steps'], reviewers: ['codex-acp'], verify: true, minutes: 30 },
  });
  assert.match(said.out, /second opinions for `web-app`: `codex-acp`\./);
  assert.match(said.out, /Before work here lands, `codex-acp` reads it, in a copy of its own that nothing is taken back from/);
  assert.match(said.out, /It may build and run what this repository declares safe, in that copy\./);
  // The same rule as the table's row, so the door says that row's sentences after the rule's own, each on its line.
  for (const sentence of opinionGateRow('the design\'s repository rule says both, then what verify is not handed yet')) {
    assert.ok(said.out.includes(`  ${sentence}\n`), `what the gate does: ${sentence}`);
  }
  assert.doesNotMatch(said.out, /Declared only|nothing reads it yet/);

  const listed = run(['list'], at(fx)).out;
  assert.match(listed, /opinion\s+web-app\s+codex-acp; before landing and each next step; may build and run what is declared safe; at most 30 minutes a pass/);
  assert.doesNotMatch(listed, /opinion\s+none set/);
  fx.cleanup();
});

test('opinion for a workspace says when one of its reviewers is the working agent\'s own family', () => {
  const fx = makeFixture('driver-opinion-workspace');
  const said = run(['opinion', '--workspace', 'work', '--reviewers', 'codex-acp, claude-code-acp', '--required'], at(fx));

  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).workspaceOpinions, {
    work: { on: ['landing'], reviewers: ['codex-acp', 'claude-code-acp'], required: true },
  });
  assert.match(said.out, /second opinions for repositories in the workspace `work` that set none of their own: `codex-acp`, else `claude-code-acp`\./);
  assert.match(said.out, /If no reviewer can read it, the work waits for you\./);
  // This machine's work runs on `claude-code` by default, whose family both Claude Code doors are.
  assert.match(said.out, /`claude-code-acp` is the same agent as the one that does the work here/);
  assert.match(said.out, /A repository with a rule of its own keeps it/);
  assert.match(run(['list'], at(fx)).out, /opinion\s+workspace work\s+codex-acp, else claude-code-acp; before landing; required; at most 20 minutes a pass\s+\(for each repository there that sets none\)/);
  fx.cleanup();
});

test('opinion switches change only what they name, none and --clear say it, and each is said', () => {
  const fx = makeFixture('driver-opinion-edit');
  run(['opinion', 'web-app', '--reviewers', 'dsh', '--required', '--verify'], at(fx));

  const relaxed = run(['opinion', 'Web-App', '--not-required', '--no-verify', '--no-recheck'], at(fx));
  assert.deepEqual(JSON.parse(readFileSync(at(fx), 'utf8')).opinions, { 'web-app': { on: ['landing'], reviewers: ['dsh'], recheck: false } });
  assert.match(relaxed.out, /If no reviewer can read it, you are told so, and nothing waits\./);
  assert.match(relaxed.out, /Commits made in answer to its findings are not read again\./);

  const none = run(['opinion', 'notes-site', 'none'], at(fx));
  assert.equal(JSON.parse(readFileSync(at(fx), 'utf8')).opinions['notes-site'], false);
  assert.match(none.out, /No second opinion here, whatever its workspace says/);
  // Nothing waits where a repository has none, so the table says nothing after it, and neither does the door (XAGENT1f4).
  assert.deepEqual(opinionGateRow('none here says nothing after'), []);
  assert.doesNotMatch(none.out, /daoris-driver opinion|Declared only/);
  assert.match(run(['list'], at(fx)).out, /opinion\s+notes-site\s+none here, whatever its workspace says/);

  const cleared = run(['opinion', 'web-app', '--clear'], at(fx));
  assert.equal('web-app' in JSON.parse(readFileSync(at(fx), 'utf8')).opinions, false);
  assert.match(cleared.out, /`web-app` takes its workspace's second-opinion rule again, else none/);
  fx.cleanup();
});

test('opinion refuses in the driver\'s words, and a form it does not take, and writes nothing', () => {
  const fx = makeFixture('driver-opinion-refused');
  const refused = (argv: string[]) => captureError(() => run(argv, at(fx))).message;

  assert.equal(refused(['opinion', 'web-app', '--reviewers', 'codex-acp', '--on', 'merge']),
    '`merge` is not an occasion — `landing`, `steps` or both.');
  assert.equal(refused(['opinion', 'web-app', '--reviewers', 'codex-acp', '--minutes', 'half']),
    '`minutes` is a whole number from 5 to 120: how long one pass may take, 20 when absent.');
  assert.equal(refused(['opinion', 'web-app', '--required']),
    '`web-app` has no second-opinion rule of its own — name its reviewers first.');
  assert.equal(refused(['opinion', '--workspace', 'work', 'none']),
    '`none` is a repository\'s: a workspace with no second opinion sets none, and `--clear` takes its rule away.');
  assert.equal(refused(['opinion', 'web-app', 'none', '--reviewers', 'dsh']),
    'one change at a time: set its reviewers and how they read, say none, or clear.');
  assert.match(refused(['opinion']), /`driver opinion` needs <repository>\|--workspace <name>/);
  assert.match(refused(['opinion', 'web-app']), /`driver opinion` needs <repository>\|--workspace <name>/);
  assert.match(refused(['opinion', 'web-app', 'codex-acp']), /`driver opinion` needs <repository>\|--workspace <name>/);
  assert.match(refused(['opinion', 'web-app', '--reviewer', 'dsh']), /`--reviewer` is not a flag `driver opinion` takes/);
  assert.equal(refused(['opinion', 'web-app', '--reviewers', 'dsh', '--verify', '--no-verify']),
    'a reviewer may build and run or it may not — say `--verify` or `--no-verify`, not both.');
  assert.throws(() => readFileSync(at(fx)));
  fx.cleanup();
});

test('an opinion rule is written only once set, and a verb that knows nothing of it preserves it', () => {
  const fx = makeFixture('driver-opinion-preserve');
  run(['drive', 'web-app'], at(fx));
  const fresh = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.equal('opinions' in fresh, false);
  assert.equal('workspaceOpinions' in fresh, false);

  run(['opinion', 'notes-site', 'none'], at(fx));
  run(['opinion', '--workspace', 'work', '--reviewers', 'codex-acp'], at(fx));
  run(['hold', 'web-app'], at(fx));
  run(['language', 'web-app', 'zh'], at(fx));

  const kept = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(kept.opinions, { 'notes-site': false });
  assert.deepEqual(kept.workspaceOpinions, { work: { on: ['landing'], reviewers: ['codex-acp'] } });
  fx.cleanup();
});

/**
 * Which workflow work follows, `workflows` and `workspaceWorkflows` (WORKFLOW1e, D157 point 10, the workflow design §2.5,
 * §4.1–§4.3), and the versions a kept run names (§2.6). 🔴 A TWIN with the driver's `WorkflowSelection` and
 * `WorkflowRunBindings.KeptVersions`: both hold ONE table, the driver suite's `fixtures/workflow-selection.json`, cell for cell — the
 * reading and its writing, each entry's first problem in the same words, §4.1's order, the edits both doors make, and the versions
 * kept. The driver's `WorkflowSelectionTests` reads the same file.
 */
interface SelectionRow { name: string }
const SELECTION_TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'workflow-selection.json'), 'utf8')) as {
  read: (SelectionRow & { file: unknown; read: unknown })[];
  problems: (SelectionRow & { scope: 'repository' | 'workspace'; entry: unknown; problem: string | null })[];
  resolve: (SelectionRow & {
    file: unknown; repository: string | null; workspace: string | null; task: WorkflowTaskChoice | null; selection: unknown;
  })[];
  edits: (SelectionRow & { file: unknown; edit: WorkflowChoiceEdit; read: unknown | null; refusal: string | null })[];
  kept: (SelectionRow & { runs: unknown[]; workflow: string; versions: number[] })[];
};

/** Both maps as `writeDriverChoices` writes them back, each `{}` where it writes none. */
function writtenMaps(path: string): unknown {
  const back = JSON.parse(readFileSync(path, 'utf8'));
  return { workflows: back.workflows ?? {}, workspaceWorkflows: back.workspaceWorkflows ?? {} };
}

test('both workflow maps are read and written as the driver reads and writes them (the shared table)', () => {
  const fx = makeFixture('driver-workflow-read');
  for (const row of SELECTION_TABLE.read) {
    writeFileSync(at(fx), JSON.stringify(row.file), 'utf8');
    writeDriverChoices(at(fx), readDriverChoices(at(fx)));
    assert.deepEqual(writtenMaps(at(fx)), row.read, row.name);
  }
  fx.cleanup();
});

test('a workflow choice\'s first problem is the driver\'s, in its words (the shared table)', () => {
  for (const row of SELECTION_TABLE.problems) {
    assert.equal(workflowChoiceOf(row.entry, row.scope === 'workspace').problem, row.problem, row.name);
  }
});

test('a selection resolves as the driver resolves it, in §4.1\'s order (the shared table)', () => {
  const fx = makeFixture('driver-workflow-resolve');
  for (const row of SELECTION_TABLE.resolve) {
    writeFileSync(at(fx), JSON.stringify(row.file), 'utf8');
    assert.deepEqual(resolveWorkflow(readDriverChoices(at(fx)), row.repository, row.workspace, row.task), row.selection, row.name);
  }
  fx.cleanup();
});

test('a workflow edit is the driver\'s, or refused in its words (the shared table)', () => {
  const fx = makeFixture('driver-workflow-edits');
  for (const row of SELECTION_TABLE.edits) {
    writeFileSync(at(fx), JSON.stringify(row.file), 'utf8');
    const choices = readDriverChoices(at(fx));
    const edited = applyWorkflowEdit(choices, row.edit);
    assert.equal(edited.refusal, row.refusal, row.name);
    if (row.refusal !== null) continue;
    writeDriverChoices(at(fx), { ...choices, ...edited.maps! });
    assert.deepEqual(writtenMaps(at(fx)), row.read, `${row.name}: the maps after`);
  }
  fx.cleanup();
});

test('the versions a kept run names are the driver\'s (the shared table)', () => {
  for (const row of SELECTION_TABLE.kept) {
    assert.deepEqual(keptVersions(row.runs, row.workflow), row.versions, row.name);
  }
});

test('a workflow choice keeps its kinds in the order written, and a verb that knows nothing of it preserves it', () => {
  const fx = makeFixture('driver-workflow-preserve');
  writeFileSync(at(fx), JSON.stringify({
    workflows: { 'web-app': { default: 'feature-review' } },
    workspaceWorkflows: { work: { kinds: { feature: { label: 'Feature' }, docs: { label: 'Documentation', paths: ['docs/**'] } } } },
  }), 'utf8');
  run(['hold', 'web-app'], at(fx));
  run(['language', 'web-app', 'zh'], at(fx));

  const kept = JSON.parse(readFileSync(at(fx), 'utf8'));
  assert.deepEqual(kept.workflows, { 'web-app': { default: 'feature-review' } });
  assert.deepEqual(Object.keys(kept.workspaceWorkflows.work.kinds), ['feature', 'docs']);

  run(['drive', 'notes-site'], at(fx));
  const fresh = makeFixture('driver-workflow-fresh');
  run(['drive', 'web-app'], at(fresh));
  const none = JSON.parse(readFileSync(at(fresh), 'utf8'));
  assert.equal('workflows' in none, false);
  assert.equal('workspaceWorkflows' in none, false);
  fresh.cleanup();
  fx.cleanup();
});
