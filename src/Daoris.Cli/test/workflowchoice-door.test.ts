import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { commandDriver } from '../src/driverconfig.ts';
import { DaorisError } from '../src/errors.ts';
import type { CheckoutsReader } from '../src/reviews.ts';
import { GATE_READS, WHAT_CHOOSES, commandWorkflow } from '../src/workflowdoor.ts';
import { makeFixture, type Fixture } from './_fixture.ts';

/**
 * WORKFLOW1e (D157 point 10, the workflow design §4.1–§4.3, §4.7): `use` and `kind` edit which workflow work follows, through the
 * table both twins hold (`workflowchoice.ts`, held to the driver's by `driverconfig.test.ts`); `show --repository|--workspace
 * [--kind]` draws what a task of that kind would follow. A named workflow is chosen only where its newest version reads here, and
 * every verb says what the gate reads of it since WORKFLOW1f: the version a run bound at its first start.
 */

const NOW = new Date('2026-10-09T09:12:34.567Z');
const unread: CheckoutsReader = async () => ({ unread: 'the service did not answer' });
const registry = (workspace: string | null): CheckoutsReader => async () => ({
  checkouts: [{ repository: 'web-app', workspace, root: null }, { repository: 'notes-site', workspace, root: null }],
});

/** The door, on a home of its own: its lines and its exit code, or the refusal it threw. */
async function run(fx: Fixture, argv: string[], checkouts: CheckoutsReader = unread): Promise<{ code: number; out: string }> {
  const saved = { home: process.env.DAORIS_HOME, config: process.env.DAORIS_DRIVER_CONFIG };
  process.env.DAORIS_HOME = fx.root;
  delete process.env.DAORIS_DRIVER_CONFIG;
  const lines: string[] = [];
  try {
    const code = await commandDriver({ root: fx.root, argv: ['workflow', ...argv], write: (line) => lines.push(line), packageRoot: process.cwd() },
      undefined, checkouts, (args, context) => commandWorkflow(args, { ...context, now: () => NOW }));
    return { code, out: lines.join('\n') };
  } finally {
    if (saved.home === undefined) delete process.env.DAORIS_HOME;
    else process.env.DAORIS_HOME = saved.home;
    if (saved.config !== undefined) process.env.DAORIS_DRIVER_CONFIG = saved.config;
  }
}

async function refused(fx: Fixture, argv: string[], checkouts: CheckoutsReader = unread): Promise<DaorisError> {
  try {
    await run(fx, argv, checkouts);
  } catch (error) {
    assert.ok(error instanceof DaorisError, `not a refusal: ${(error as Error).message}`);
    return error;
  }
  throw new Error(`\`driver workflow ${argv.join(' ')}\` did not refuse`);
}

function choices(fx: Fixture): { workflows?: Record<string, unknown>; workspaceWorkflows?: Record<string, unknown> } {
  return JSON.parse(fx.read('driver.json'));
}

test('use names a workflow saved here for a repository, says what it replaces, and --plan writes nothing', async () => {
  const fx = makeFixture('workflowchoice-use');
  await run(fx, ['new', 'docs-to-pr', '--from', 'pull-request-no-press', '--name', 'Documentation to a pull request']);

  const plan = await run(fx, ['use', 'docs-to-pr', '--repository', 'web-app', '--plan']);
  assert.equal(plan.out, [
    'daoris: `web-app` now chooses `docs-to-pr` as its default — Documentation to a pull request, v1 now: A pull request opens once '
      + 'its quest is done; you merge it.',
    '  This replaces the workspace\'s for every kind: `web-app` follows its own kinds and its own default, and Current where it names none.',
    '  Each new piece of work binds what is chosen at its first start; work already started keeps what it bound.',
    '  --plan: nothing was written.',
    GATE_READS,
  ].join('\n'));
  assert.equal(fx.exists('driver.json'), false);

  const { code } = await run(fx, ['use', 'docs-to-pr', '--repository', 'web-app']);
  assert.equal(code, 0);
  assert.deepEqual(choices(fx).workflows, { 'web-app': { default: 'docs-to-pr' } });
  assert.equal((await run(fx, ['use', 'docs-to-pr', '--repository', 'WEB-APP'])).out,
    'daoris: `WEB-APP` chooses `docs-to-pr` already as its default: nothing was written.');

  assert.match((await run(fx, ['use', 'current', '--repository', 'notes-site'])).out,
    /^daoris: `notes-site` now chooses Current as its default: its rules as they stand, read at each gate\./);
  assert.deepEqual(choices(fx).workflows, { 'web-app': { default: 'docs-to-pr' }, 'notes-site': { default: 'current' } });

  const cleared = await run(fx, ['use', '--clear', '--repository', 'web-app']);
  assert.match(cleared.out,
    /^daoris: `web-app` no longer chooses a workflow as its default\.\n {2}`web-app` names no workflow now, so its workspace's choice reaches it again\./);
  assert.deepEqual(choices(fx).workflows, { 'notes-site': { default: 'current' } });
  fx.cleanup();
});

test('use refuses a workflow not saved here, one whose newest version does not read, and a choice that is no choice', async () => {
  const fx = makeFixture('workflowchoice-use-refused');
  const missing = await refused(fx, ['use', 'docs-to-pr', '--repository', 'web-app']);
  assert.equal(missing.exitCode, 2);
  assert.equal(missing.message, 'no workflow `docs-to-pr` is saved here — `daoris driver workflow list` names them, and `new` names one.');

  await run(fx, ['new', 'docs-to-pr', '--from', 'merge-after-accept']);
  const file = JSON.parse(fx.read('workflows/docs-to-pr.json'));
  file.versions.push({
    version: 2, at: '2026-10-09T10:00:00Z', door: 'terminal',
    steps: [{ id: 'work', kind: 'work' }, { id: 'landing', kind: 'landing', form: 'queue', accept: 'you' }],
  });
  fx.write('workflows/docs-to-pr.json', JSON.stringify(file));
  const notRead = await refused(fx, ['use', 'docs-to-pr', '--repository', 'web-app']);
  assert.equal(notRead.exitCode, 1);
  assert.match(notRead.message, /^`docs-to-pr` v2 cannot be read here: .* It can be chosen once a version that reads is saved after it\.$/);

  const shape = await refused(fx, ['use', 'Docs', '--workspace', 'work']);
  assert.equal(shape.exitCode, 1);
  assert.equal(shape.message, '`Docs` is not a workflow\'s id: lower-case letters, digits and dashes, at most 40, such as `docs-to-pr`; '
    + 'or `current`. Nothing was written.');

  for (const argv of [['use', 'docs-to-pr'], ['use', '--clear', 'docs-to-pr', '--workspace', 'work'], ['use', 'a', '--repository', 'r', '--workspace', 'w']]) {
    const usage = await refused(fx, argv);
    assert.equal(usage.exitCode, 2);
    assert.match(usage.message, /^`driver workflow use` takes <id>\|current\|--clear, then --repository <name>\|--workspace <name>/, argv.join(' '));
  }
  assert.match((await refused(fx, ['use', 'docs-to-pr', '--workspace', 'work', '--label', 'x'])).message,
    /^`--label` is not a flag `driver workflow use` takes — /);
  assert.equal(fx.exists('driver.json'), false);
  fx.cleanup();
});

test('kind declares a workspace\'s kind with its paths, use maps it, a repository maps it where its workspace declares it, and --drop', async () => {
  const fx = makeFixture('workflowchoice-kind');
  await run(fx, ['new', 'docs-to-pr', '--from', 'pull-request-no-press', '--name', 'Documentation to a pull request']);

  const declared = await run(fx, ['kind', 'work', 'docs', '--label', 'Documentation', '--paths', 'docs/**, **/*.md']);
  assert.equal(declared.out, [
    'daoris: the workspace `work` declares kind `docs` — Documentation, its paths `docs/**`, `**/*.md`.',
    '  It maps to no workflow there: `daoris driver workflow use <id> --workspace work --kind docs` maps one, and `--repository <name>` '
      + 'maps it for one repository.',
    `  Written to ${join(fx.root, 'driver.json')}.`,
    GATE_READS,
  ].join('\n'));

  const undeclared = await refused(fx, ['use', 'docs-to-pr', '--workspace', 'work', '--kind', 'design']);
  assert.equal(undeclared.exitCode, 1);
  assert.equal(undeclared.message, 'workspace `work` declares no kind `design`: a kind is declared first, with its label. '
    + '`daoris driver workflow kind <workspace> design --label "…"` declares one. Nothing was written.');

  assert.match((await run(fx, ['use', 'docs-to-pr', '--workspace', 'work', '--kind', 'docs'])).out,
    /^daoris: the workspace `work` now chooses `docs-to-pr` for kind `docs` — Documentation to a pull request, v1 now: /);
  assert.deepEqual(choices(fx).workspaceWorkflows, {
    work: { kinds: { docs: { label: 'Documentation', paths: ['docs/**', '**/*.md'], workflow: 'docs-to-pr' } } },
  });

  // A repository's kind is checked against its workspace's, where the registry says which that is.
  const elsewhere = await refused(fx, ['use', 'docs-to-pr', '--repository', 'web-app', '--kind', 'docs'], registry('other'));
  assert.equal(elsewhere.message, '`web-app`\'s workspace, `other`, declares no kind `docs`: a kind is declared first, with its label. '
    + '`daoris driver workflow kind <workspace> docs --label "…"` declares one. Nothing was written.');
  assert.match((await run(fx, ['use', 'current', '--repository', 'web-app', '--kind', 'docs'], registry('work'))).out,
    /^daoris: `web-app` now chooses Current for kind `docs`: /);
  const unchecked = await run(fx, ['use', 'docs-to-pr', '--repository', 'notes-site', '--kind', 'docs']);
  assert.match(unchecked.out,
    /\n {2}The registry was not read \(the service did not answer\), so kind `docs` was not checked against the kinds `notes-site`'s workspace declares\.\n/);
  assert.deepEqual(choices(fx).workflows, { 'web-app': { kinds: { docs: 'current' } }, 'notes-site': { kinds: { docs: 'docs-to-pr' } } });

  const dropped = await run(fx, ['kind', 'work', 'docs', '--drop']);
  assert.equal(dropped.out.split('\n').slice(0, 2).join('\n'), [
    'daoris: the workspace `work` no longer declares kind `docs`.',
    '  `web-app`, `notes-site` still map it; no task of that kind reaches those mappings until `docs` is declared again.',
  ].join('\n'));
  assert.equal(choices(fx).workspaceWorkflows, undefined);

  const none = await refused(fx, ['kind', 'work', 'docs', '--drop']);
  assert.equal(none.message, 'workspace `work` declares no kind `docs`, so there is none to drop. Nothing was written.');
  for (const argv of [['kind', 'work', 'docs'], ['kind', 'work', 'docs', '--label', 'D', '--drop'], ['kind', 'work']]) {
    assert.match((await refused(fx, argv)).message, /^`driver workflow kind` takes <workspace> <kind> --label "…"/, argv.join(' '));
  }
  assert.equal((await refused(fx, ['kind', 'work', 'docs', '--label', 'Documentation', '--paths', '../docs'])).exitCode, 1);
  fx.cleanup();
});

test('show draws the workflow a task of a kind would follow: the named one\'s newest version, or Current with what chose it', async () => {
  const fx = makeFixture('workflowchoice-show');
  await run(fx, ['new', 'docs-to-pr', '--from', 'pull-request-no-press', '--name', 'Documentation to a pull request']);
  await run(fx, ['kind', 'work', 'docs', '--label', 'Documentation']);
  await run(fx, ['kind', 'work', 'feature', '--label', 'Feature']);
  await run(fx, ['use', 'docs-to-pr', '--workspace', 'work', '--kind', 'docs']);

  const named = await run(fx, ['show', '--repository', 'web-app', '--kind', 'docs'], registry('work'));
  assert.equal(named.code, 0);
  const lines = named.out.split('\n');
  assert.match(lines[0]!, /^daoris: `web-app`, for kind `docs` \(Documentation\), follows `docs-to-pr` — Documentation to a pull request, v1 \(digest [0-9a-f]{12}\), chosen by the workspace `work` for Documentation\.$/);
  assert.equal(lines[1], '  A pull request opens once its quest is done; you merge it.');
  assert.equal(lines[lines.length - 1], WHAT_CHOOSES);

  const feature = await run(fx, ['show', '--workspace', 'work', '--kind', 'feature']);
  assert.match(feature.out, /^daoris: the workspace `work`, for kind `feature` \(Feature\), follows Current: for each repository there that sets none of its own, read from the rules as they stand at each gate \(version [0-9a-f]{12}\)\.\n {2}Chosen: since nothing names a workflow\.\n/);

  const design = await run(fx, ['show', '--repository', 'web-app', '--kind', 'design'], registry('work'));
  assert.match(design.out, /\n {2}Chosen: since nothing names a workflow\.\n {2}`design` is not a kind the workspace `work` declares, so it is read as none\.\n/);

  // With nothing named and no kind asked, the drawing is Current's as it always was.
  const plain = await run(fx, ['show', '--repository', 'notes-site'], registry('elsewhere'));
  assert.doesNotMatch(plain.out, /Chosen:/);

  // A choice whose workflow cannot be read is said, with what chose it.
  await run(fx, ['use', 'docs-to-pr', '--repository', 'notes-site']);
  fx.write('workflows/docs-to-pr.json', '{ not json');
  const gone = await run(fx, ['show', '--repository', 'notes-site'], registry('elsewhere'));
  assert.equal(gone.code, 1);
  assert.match(gone.out, /^daoris: `notes-site` chooses `docs-to-pr`, `notes-site`'s default, which cannot be read here: `docs-to-pr\.json` is not readable JSON\.$/m);
  fx.cleanup();
});

test('an edit keeps every version a bound run names beyond the newest twenty', async () => {
  const fx = makeFixture('workflowchoice-kept-runs');
  await run(fx, ['new', 'docs-to-pr', '--from', 'merge-after-accept']);
  fx.write('workflows/runs/a1b2c3.json', JSON.stringify({ run: 'a1b2c3', workflow: 'docs-to-pr', version: 1 }));
  for (let n = 0; n < 21; n += 1) {
    const changed = n % 2 === 0 ? ['form=branch', 'pattern=docs/{quest}', 'plugin=none'] : ['form=merge', 'pattern=', 'plugin='];
    const { code } = await run(fx, ['edit', 'docs-to-pr', 'set', 'landing', ...changed]);
    assert.equal(code, 0);
  }
  const versions = (JSON.parse(fx.read('workflows/docs-to-pr.json')) as { versions: { version: number }[] }).versions.map((each) => each.version);
  assert.equal(versions[0], 1, 'the bound run\'s version is kept');
  assert.equal(versions.length, 20);
  assert.equal(versions[versions.length - 1], 22);
  fx.cleanup();
});

test('every verb says the gate reads the version a run bound at its first start, never that no gate reads it (WORKFLOW1f)', () => {
  for (const said of [WHAT_CHOOSES, GATE_READS]) {
    assert.doesNotMatch(said, /no gate|nothing at a gate|as Current says|yet/);
    assert.match(said, /at its first start/);
  }
  assert.match(GATE_READS, /how it lands, your look and its second opinion follow that version/);
});
