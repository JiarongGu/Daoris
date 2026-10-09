import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { commandDriver } from '../src/driverconfig.ts';
import { DaorisError } from '../src/errors.ts';
import { HOME_SENTENCE } from '../src/home.ts';
import type { CheckoutsReader } from '../src/reviews.ts';
import { NOT_CHOSEN, commandWorkflow } from '../src/workflowdoor.ts';
import { makeFixture, type Fixture } from './_fixture.ts';

/**
 * WORKFLOW1d (D157 points 5 and 6, the workflow design §2.4–§2.7, §4.7): the terminal's door to named workflows. Each writer
 * prints its change as the card draws it (`namedworkflows.ts`, held to the driver's table by `namedworkflows.test.ts`) and saves
 * a new version, never editing one; `--plan` saves nothing; a refusal is 1 where a version would not read, 2 where the verb
 * cannot be done; and every verb that names a workflow says nothing chooses one yet.
 */

const NOW = new Date('2026-10-09T09:12:34.567Z');
const unread: CheckoutsReader = async () => ({ unread: 'the service did not answer' });

/** The door, on a home of its own: its lines and its exit code, or the refusal it threw. */
async function run(fx: Fixture, argv: string[], checkouts: CheckoutsReader = unread, now: Date = NOW): Promise<{ code: number; out: string }> {
  const saved = { home: process.env.DAORIS_HOME, config: process.env.DAORIS_DRIVER_CONFIG };
  process.env.DAORIS_HOME = fx.root;
  delete process.env.DAORIS_DRIVER_CONFIG;
  const lines: string[] = [];
  try {
    const code = await commandDriver({ root: fx.root, argv: ['workflow', ...argv], write: (line) => lines.push(line), packageRoot: process.cwd() },
      undefined, checkouts, (args, context) => commandWorkflow(args, { ...context, now: () => now }));
    return { code, out: lines.join('\n') };
  } finally {
    if (saved.home === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved.home;
    if (saved.config !== undefined) process.env.DAORIS_DRIVER_CONFIG = saved.config;
  }
}

/** The refusal a run threw: its exit code and message. */
async function refused(fx: Fixture, argv: string[], checkouts: CheckoutsReader = unread): Promise<DaorisError> {
  try {
    await run(fx, argv, checkouts);
  } catch (error) {
    assert.ok(error instanceof DaorisError, `not a refusal: ${(error as Error).message}`);
    return error;
  }
  throw new Error(`\`driver workflow ${argv.join(' ')}\` did not refuse`);
}

function saved(fx: Fixture, id: string): { id: string; name: string; versions: { version: number; at: string; door: string; steps: unknown[] }[] } {
  return JSON.parse(fx.read(`workflows/${id}.json`));
}

test('new from a preset: --plan says the change and writes nothing, then v1 is written, BOM-less, LF and whole', async () => {
  const fx = makeFixture('workflowdoor-new');

  const plan = await run(fx, ['new', 'docs-to-pr', '--from', 'pull-request-no-press', '--name', 'Documentation to a pull request', '--plan']);
  assert.equal(plan.code, 0);
  assert.equal(plan.out, [
    'daoris: `docs-to-pr` v1, a new workflow — Documentation to a pull request, from the preset `pull-request-no-press`:',
    '  A pull request opens once its quest is done; you merge it.',
    '  Your part:',
    '    + pull-request: you merge its pull request on the platform.',
    '  Steps, by id:',
    '    + work · work',
    '    + landing · landing: form `branch`; accept `automatic`',
    '    + pull-request · pull-request',
    '  Without asking you each time:',
    '    the plugin the landing rule names, if any, pushes each branch with no press of yours.',
    '  --plan: nothing was written.',
    NOT_CHOSEN,
  ].join('\n'));
  assert.equal(fx.exists('workflows/docs-to-pr.json'), false);

  const { code, out } = await run(fx, ['new', 'docs-to-pr', '--from', 'pull-request-no-press', '--name', 'Documentation to a pull request']);
  assert.equal(code, 0);
  assert.match(out, /Written to .*docs-to-pr\.json\./);
  const bytes = readFileSync(join(fx.root, 'workflows', 'docs-to-pr.json'));
  assert.notEqual(bytes[0], 0xef, 'no BOM');
  const text = bytes.toString('utf8');
  assert.ok(!text.includes('\r') && text.endsWith('}\n'));
  assert.deepEqual(JSON.parse(text), {
    id: 'docs-to-pr', name: 'Documentation to a pull request', versions: [{
      version: 1, at: '2026-10-09T09:12:34Z', door: 'terminal',
      steps: [{ id: 'work', kind: 'work' }, { id: 'landing', kind: 'landing', form: 'branch', accept: 'automatic' }, { id: 'pull-request', kind: 'pull-request' }],
    }],
  });
  assert.deepEqual(readdirSync(join(fx.root, 'workflows')), ['docs-to-pr.json'], 'nothing written beside it is left');

  // A workflow is new once: a version is added by `edit`.
  const again = await refused(fx, ['new', 'docs-to-pr', '--from', 'merge-after-accept']);
  assert.equal(again.exitCode, 1);
  assert.equal(again.message, '`docs-to-pr` is a workflow here already, at v1: `daoris driver workflow edit docs-to-pr …` adds a version to it.');
  fx.cleanup();
});

test('new with a second opinion and your look, and from a saved workflow\'s version', async () => {
  const fx = makeFixture('workflowdoor-new-gates');
  const { out: gated } = await run(fx, ['new', 'reviewed', '--from', 'merge-after-accept', '--with-opinion', '--with-look']);
  assert.match(gated, /from the preset `merge-after-accept`, with a second opinion and with your look first:/);
  assert.equal(saved(fx, 'reviewed').name, 'Merge after you accept + second opinion + your look');
  // The longest preset's name with both is still a name.
  await run(fx, ['new', 'longest', '--from', 'pull-request-after-accept', '--with-opinion', '--with-look']);
  assert.equal(saved(fx, 'longest').name.length, 60);
  assert.deepEqual(saved(fx, 'reviewed').versions[0]!.steps.map((step) => (step as { id: string }).id), ['work', 'opinion', 'look', 'landing']);

  const { out } = await run(fx, ['new', 'copy', '--from', 'reviewed@1', '--name', 'A copy']);
  assert.match(out, /^daoris: `copy` v1, a new workflow — A copy, from `reviewed` v1:/);
  assert.deepEqual(saved(fx, 'copy').versions[0]!.steps, saved(fx, 'reviewed').versions[0]!.steps);

  const none = await refused(fx, ['new', 'other', '--from', 'nothing-like-it']);
  assert.equal(none.exitCode, 2);
  assert.match(none.message, /^`nothing-like-it` is no preset, no saved workflow and no `current:<repository>` — the presets are `merge-after-accept`/);
  fx.cleanup();
});

test('new from Current copies a repository\'s rules as Current draws them, and the change is drawn from nothing', async () => {
  const fx = makeFixture('workflowdoor-current');
  fx.write('driver.json', JSON.stringify({
    workspaceLandings: { work: { form: 'branch', pattern: 'work/{quest}-{slug}', autoAccept: true } },
    workspaceReviews: { work: { required: true, environments: [{ name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' }] } },
  }));
  const checkouts: CheckoutsReader = async () => ({ checkouts: [{ repository: 'web-app', workspace: 'work', root: null }] });

  const { code, out } = await run(fx, ['new', 'web-app-today', '--from', 'current:web-app'], checkouts);

  assert.equal(code, 0);
  assert.match(out, /^daoris: `web-app-today` v1, a new workflow — web-app's Current, from `web-app`'s Current \(version [0-9a-f]{12}\):/m);
  assert.deepEqual(saved(fx, 'web-app-today').versions[0]!.steps, [
    { id: 'work', kind: 'work' },
    { id: 'look', kind: 'look', environment: 'local' },
    { id: 'landing', kind: 'landing', form: 'branch', accept: 'automatic', pattern: 'work/{quest}-{slug}', plugin: 'none' },
  ]);
  assert.match(out, /\+ look: you look at it running before it lands\./);
  fx.cleanup();
});

test('edit saves the next version from the change language, said step by step; a stale base, no change and a bad change', async () => {
  const fx = makeFixture('workflowdoor-edit');
  await run(fx, ['new', 'docs', '--from', 'pull-request-after-accept', '--with-look', '--name', 'Docs']);

  const plan = await run(fx, ['edit', 'docs', '--base', '1', 'remove', 'look', 'set', 'landing', 'accept=automatic', 'plugin=example.pull-request', '--plan']);
  assert.equal(plan.out, [
    'daoris: `docs` v1 → v2 — Docs:',
    '  A pull request opens once its quest is done; you merge it.',
    '  Your part:',
    '    − look: you no longer look at it running before it lands.',
    '    − landing: you no longer accept each piece of work before it lands.',
    '    = pull-request: you still merge its pull request on the platform.',
    '  Steps, by id:',
    '      work · work',
    '    − look · look',
    '    ~ landing · landing: accept `you` → `automatic`; plugin as declared → `example.pull-request`',
    '      pull-request · pull-request',
    '  Without asking you each time:',
    '    `example.pull-request` pushes each branch with no press of yours.',
    '  --plan: nothing was written.',
    NOT_CHOSEN,
  ].join('\n'));
  assert.equal(saved(fx, 'docs').versions.length, 1);

  const v1 = saved(fx, 'docs').versions[0]!;
  await run(fx, ['edit', 'docs', 'add', 'go-ahead', '--after', 'pull-request', 'act=post the release note', 'on=the team\'s channel',
    'add', 'go-ahead', '--after', 'go-ahead', '--id', 'tag', 'act=tag the release', 'on=the line',
    'add', 'go-ahead', '--after', 'work', '--id', 'dev-config', 'act=write the configuration to dev', 'on=dev']);
  const v2 = saved(fx, 'docs').versions[1]!;
  assert.equal(v2.version, 2);
  assert.deepEqual(v2.steps.map((step) => (step as { id: string }).id), ['work', 'dev-config', 'look', 'landing', 'pull-request', 'go-ahead', 'tag']);
  assert.deepEqual(saved(fx, 'docs').versions[0], v1, 'v1 is never edited');

  const moved = await run(fx, ['edit', 'docs', 'move', 'tag', '--after', 'pull-request', 'set', 'tag', 'refused=wait']);
  assert.equal(moved.code, 0);
  assert.match(moved.out, / {4}~ tag · go-ahead: refused `stop` → `wait`; moved\n/);
  assert.deepEqual(saved(fx, 'docs').versions[2]!.steps.map((step) => (step as { id: string }).id),
    ['work', 'dev-config', 'look', 'landing', 'pull-request', 'tag', 'go-ahead']);

  // `failed` is a check's and a stage's, so the version would not read: refused, and nothing written.
  const notTaken = await refused(fx, ['edit', 'docs', 'on', 'go-ahead', 'failed=wait']);
  assert.equal(notTaken.exitCode, 1);
  assert.equal(notTaken.message, 'v4, step `go-ahead`: a step of kind `go-ahead` takes no `failed` — it takes `act`, `on` and `refused`.');

  const stale = await refused(fx, ['edit', 'docs', '--base', '1', 'remove', 'look']);
  assert.equal(stale.exitCode, 1);
  assert.equal(stale.message, '`docs` is at v3 now, not v1: read it again (`daoris driver workflow show docs`) and make the change against v3.');

  const same = await run(fx, ['edit', 'docs', '--base', '3', 'set', 'look', 'environment=']);
  assert.equal(same.out, 'daoris: `docs` v3 already says this: nothing was written.');
  assert.equal(saved(fx, 'docs').versions.length, 3);

  const before = await refused(fx, ['edit', 'docs', 'move', 'look', '--after', 'landing']);
  assert.equal(before.exitCode, 1);
  assert.equal(before.message, 'v4, step `look`: a step of kind `look` comes before the landing, in the order the runtime keeps.');
  const missing = await refused(fx, ['edit', 'docs', 'remove', 'checks']);
  assert.equal(missing.exitCode, 1);
  assert.match(missing.message, /^no step `checks` in v3 — its steps are `work`, `dev-config`, `look`/);
  const word = await refused(fx, ['edit', 'docs', 'rename', 'look']);
  assert.equal(word.exitCode, 2);
  assert.match(word.message, /^`rename` is not a change — a change is add <kind> --after <step>/);
  const flag = await refused(fx, ['edit', 'docs', 'add', 'go-ahead', '--after', 'tag', '--before', 'go-ahead', 'act=a', 'on=dev']);
  assert.equal(flag.exitCode, 2);
  assert.match(flag.message, /^`--before` is not a flag a change takes — a change is add <kind> --after <step>/);
  fx.cleanup();
});

test('apply saves a file\'s steps as the next version; show draws a version, its line and its limits', async () => {
  const fx = makeFixture('workflowdoor-apply');
  await run(fx, ['new', 'docs', '--from', 'merge-after-accept', '--name', 'Docs']);
  fx.write('next.json', JSON.stringify({ steps: [{ id: 'work', kind: 'work' }, { id: 'opinion', kind: 'opinion', reviewers: ['codex-acp'] },
    { id: 'landing', kind: 'landing', form: 'merge', accept: 'you' }] }));

  const applied = await run(fx, ['apply', 'docs', 'next.json']);
  assert.equal(applied.code, 0);
  assert.match(applied.out, /^daoris: `docs` v1 → v2 — Docs:\n {2}Another agent reads the work first\. You accept each piece of work, and it merges into the line\./);
  assert.match(applied.out, / {4}\+ opinion · opinion: reviewers `codex-acp`/);

  const shown = await run(fx, ['show', 'docs']);
  assert.equal(shown.out, [
    `daoris: \`docs\` — Docs: v2, its newest, saved 2026-10-09T09:12:34Z at a terminal (digest ${shown.out.match(/digest ([0-9a-f]{12})/)![1]}).`,
    '  Another agent reads the work first. You accept each piece of work, and it merges into the line.',
    '  work · work — Agent alone.',
    '  opinion · opinion — Agent alone. reviewers `codex-acp`; required false; steps false; recheck true.',
    '      Partial: work here lands only once another agent\'s reading of it is settled, or you go on without one by '
      + '`daoris-driver opinion`; the review does not draw it yet, and no task chooses its own reviewer yet.',
    '  landing · landing — You · your press: Accept. form `merge`; accept `you`; pattern as declared; plugin as declared.',
    '  Versions kept: v1, v2.',
    NOT_CHOSEN,
  ].join('\n'));
  assert.match((await run(fx, ['show', 'docs@1'])).out, /^daoris: `docs` — Docs: v1, saved /);
  const gone = await refused(fx, ['show', 'docs@7']);
  assert.equal(gone.message, '`docs` keeps no v7 — it keeps v1, v2.');

  fx.write('nothing.json', '{"name": "no steps"}');
  const empty = await refused(fx, ['apply', 'docs', 'nothing.json']);
  assert.equal(empty.exitCode, 1);
  assert.match(empty.message, /^`nothing\.json` holds no steps/);
  fx.cleanup();
});

test('a round trip: export to a file, import on another home, and show the same workflow, every version kept', async () => {
  const one = makeFixture('workflowdoor-export');
  const two = makeFixture('workflowdoor-import');
  await run(one, ['new', 'docs-to-pr', '--from', 'pull-request-after-accept', '--name', '文档直接开拉取请求']);
  await run(one, ['edit', 'docs-to-pr', 'set', 'landing', 'accept=automatic']);

  const exported = await run(one, ['export', 'docs-to-pr', '--to', join(two.root, 'carried.json')]);
  assert.match(exported.out, /^daoris: `docs-to-pr` written to .*carried\.json, with its 2 versions/);
  const twice = await refused(one, ['export', 'docs-to-pr', '--to', join(two.root, 'carried.json')]);
  assert.match(twice.message, /exists already: export writes a new file, and never over one\.$/);

  const plan = await run(two, ['import', 'carried.json', '--plan']);
  assert.match(plan.out, /^daoris: `docs-to-pr` — 文档直接开拉取请求, from carried\.json: 2 versions, its newest v2:/);
  assert.equal(two.exists('workflows/docs-to-pr.json'), false);
  const imported = await run(two, ['import', 'carried.json']);
  assert.equal(imported.code, 0);

  assert.deepEqual(saved(two, 'docs-to-pr'), saved(one, 'docs-to-pr'));
  const there = (await run(one, ['show', 'docs-to-pr'])).out;
  const here = (await run(two, ['show', 'docs-to-pr'])).out;
  assert.equal(here, there);
  assert.match(here, /^daoris: `docs-to-pr` — 文档直接开拉取请求: v2, its newest/);

  // The same file again is nothing to do; another's versions are refused, naming `apply`.
  assert.equal((await run(two, ['import', 'carried.json'])).out, 'daoris: `docs-to-pr` is here already, with the same versions: nothing was written.');
  await run(one, ['edit', 'docs-to-pr', 'set', 'landing', 'accept=you']);
  await run(one, ['export', 'docs-to-pr', '--to', join(two.root, 'newer.json')]);
  const differs = await refused(two, ['import', 'newer.json']);
  assert.equal(differs.exitCode, 1);
  assert.equal(differs.message, '`docs-to-pr` is a workflow here already, at v2, and the file\'s versions differ: '
    + '`daoris driver workflow apply docs-to-pr newer.json` adds the file\'s newest version to it.');

  // Printed without --to, it is the file whole.
  assert.deepEqual(JSON.parse((await run(one, ['export', 'docs-to-pr'])).out), saved(one, 'docs-to-pr'));
  one.cleanup();
  two.cleanup();
});

test('import refuses a file a version of which does not read here, and list says what each file holds', async () => {
  const fx = makeFixture('workflowdoor-list');
  assert.equal((await run(fx, ['list'])).out.split('\n')[0], `daoris: no workflow is named in ${join(fx.root, 'workflows')} yet.`);

  fx.write('newer.json', JSON.stringify({ id: 'release', name: 'Release', versions: [
    { version: 1, at: '2026-10-09T09:12:00Z', door: 'screen', steps: [{ id: 'work', kind: 'work' }, { id: 'landing', kind: 'landing', form: 'branch', accept: 'you' }, { id: 'pull-request', kind: 'pull-request' }] },
    { version: 2, at: '2026-10-09T09:20:00Z', door: 'screen', steps: [{ id: 'work', kind: 'work' }, { id: 'landing', kind: 'landing', form: 'branch', accept: 'you' }, { id: 'pull-request', kind: 'pull-request' }, { id: 'checks', kind: 'check', plugin: 'example.pull-request' }] },
  ] }));
  const newer = await refused(fx, ['import', 'newer.json']);
  assert.equal(newer.exitCode, 1);
  assert.equal(newer.message, '`newer.json` cannot be imported: v2, step `checks`: this build does not run a step of kind `check` yet.');

  // The same file under the home, as a newer build would leave it: kept, listed, and never chosen here.
  fx.write('workflows/release.json', fx.read('newer.json'));
  fx.write('workflows/broken.json', '{ not json');
  await run(fx, ['new', 'docs', '--from', 'merge-after-accept', '--name', 'Docs']);
  const { code, out } = await run(fx, ['list']);
  assert.equal(code, 0);
  assert.deepEqual(out.split('\n').slice(0, 4), [
    `daoris: 3 workflows named in ${join(fx.root, 'workflows')}:`,
    '  `broken` — cannot be read: `broken.json` is not readable JSON.',
    '  `docs` — Docs: v1, 2 steps. You accept each piece of work, and it merges into the line.',
    '  `release` — Release: v2 cannot be read here (v2, step `checks`: this build does not run a step of kind `check` yet.); v1 is the newest that reads.',
  ]);
  assert.match(out, /Presets built in, for `daoris driver workflow new <id> --from <preset>`/);
  assert.match(out, / {2}`pull-request-no-press` — A pull request with no press\. A pull request opens once its quest is done; you merge it\./);
  assert.ok(out.endsWith(NOT_CHOSEN));

  const unreadable = await refused(fx, ['show', 'release']);
  assert.equal(unreadable.exitCode, 1);
  assert.equal(unreadable.message, '`release` v2 cannot be read here: v2, step `checks`: this build does not run a step of kind `check` yet. '
    + 'The file keeps it as written.');
  const broken = await refused(fx, ['edit', 'broken', 'remove', 'look']);
  assert.equal(broken.exitCode, 1);
  assert.equal(broken.message, '`broken` cannot be read: `broken.json` is not readable JSON.');
  assert.equal(fx.read('workflows/broken.json'), '{ not json');
  fx.cleanup();
});

test('an id that is no id names no path, an unknown one is said, and the verbs say what they take', async () => {
  const fx = makeFixture('workflowdoor-refusals');
  for (const argv of [['show', '../driver'], ['edit', '..\\driver', 'remove', 'look'], ['new', 'Docs', '--from', 'merge-after-accept']]) {
    const refusal = await refused(fx, argv);
    assert.equal(refusal.exitCode, 1, argv.join(' '));
    assert.match(refusal.message, /is not a workflow's id — lower-case letters, digits and dashes, at most 40/);
  }
  const current = await refused(fx, ['new', 'current', '--from', 'merge-after-accept']);
  assert.equal(current.message, '`current` names Current, the workflow drawn from the rules as they stand, so no saved workflow takes it.');
  assert.equal((await refused(fx, ['show', 'docs'])).message, 'no workflow `docs` here — `daoris driver workflow list` names them.');
  assert.equal((await refused(fx, ['show', 'docs'])).exitCode, 2);

  for (const argv of [[], ['frobnicate'], ['list', 'extra'], ['show']]) {
    const usage = await refused(fx, argv);
    assert.equal(usage.exitCode, 2);
    assert.match(usage.message, /^`driver workflow` takes list; show <id>\[@<version>\]; show --repository <name>\|--workspace <name>; new <id>/, argv.join(' '));
  }
  assert.match((await refused(fx, ['new', 'docs', '--from', 'merge-after-accept', '--kind', 'docs'])).message,
    /^`--kind` is not a flag `driver workflow new` takes — /);
  fx.cleanup();
});

test('with no home, every verb is refused before anything is read or written', async () => {
  const fx = makeFixture('workflowdoor-no-home');
  const saved = { home: process.env.DAORIS_HOME, config: process.env.DAORIS_DRIVER_CONFIG };
  delete process.env.DAORIS_HOME;
  delete process.env.DAORIS_DRIVER_CONFIG;
  try {
    for (const argv of [['workflow', 'list'], ['workflow', 'new', 'docs', '--from', 'merge-after-accept']]) {
      assert.throws(() => commandDriver({ root: fx.root, argv, write: () => {}, packageRoot: process.cwd() }, undefined, unread,
        (args, context) => commandWorkflow(args, context)), (error: Error) => error.message.startsWith(HOME_SENTENCE));
    }
    assert.equal(existsSync(join(fx.root, 'workflows')), false);
  } finally {
    if (saved.home !== undefined) process.env.DAORIS_HOME = saved.home;
    if (saved.config !== undefined) process.env.DAORIS_DRIVER_CONFIG = saved.config;
  }
  fx.cleanup();
});
