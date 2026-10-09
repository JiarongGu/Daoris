import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { commandDriver, readDriverChoices } from '../src/driverconfig.ts';
import { OPINION_DECLARED_ONLY } from '../src/opinions.ts';
import { MANIFEST, pluginsRoot } from '../src/plugins.ts';
import type { CheckoutsReader } from '../src/reviews.ts';
import { WORKFLOW_LIMITS, currentWorkflow, workflowCanonical, type WorkflowPlugin, type WorkflowStep } from '../src/workflows.ts';
import { commandWorkflow } from '../src/workflowdoor.ts';
import { makeFixture } from './_fixture.ts';

/**
 * WORKFLOW1a (D157 point 7, the workflow design §2.7, §3.2, §4.7): a repository's Current workflow, derived from its rules as
 * they stand, each step with its source and its limit. 🔴 A TWIN with the driver's `WorkflowCurrent`: both hold ONE table, the
 * driver suite's `fixtures/workflow-current.json`, cell for cell, the version included. The driver's `WorkflowCurrentTests`
 * reads the same file.
 */
const TABLE = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop',
  'Daoris.Desktop.Driver.Tests', 'fixtures', 'workflow-current.json'), 'utf8')) as {
  current: {
    name: string;
    config: Record<string, unknown>;
    repository: string | null;
    workspace: string | null;
    plugins: WorkflowPlugin[];
    startHolds: string[];
    steps: WorkflowStep[];
    version: string;
  }[];
  limits: { code: string; says: string }[];
};

function at(fx: { root: string }): string {
  return join(fx.root, 'driver.json');
}

test('Current is derived from the rules as the driver derives it, step for step and cell for cell (the shared table)', () => {
  const fx = makeFixture('workflow-current');
  assert.ok(TABLE.current.length >= 20, `only ${TABLE.current.length} rows, so this proves little`);
  for (const row of TABLE.current) {
    writeFileSync(at(fx), JSON.stringify(row.config), 'utf8');
    const drawn = currentWorkflow(readDriverChoices(at(fx)), row.repository, row.workspace, row.plugins);

    assert.deepEqual(drawn.startHolds, row.startHolds, `${row.name}: the plugins that may hold a start`);
    assert.deepEqual(drawn.steps.map((step) => step.id), row.steps.map((step) => step.id), `${row.name}: the steps`);
    for (const [index, step] of row.steps.entries()) {
      const derived = drawn.steps[index]!;
      for (const cell of ['id', 'kind', 'participation', 'executor', 'press', 'source', 'settings', 'runtime', 'limit'] as const) {
        assert.deepEqual(derived[cell], step[cell], `${row.name}: ${step.id}'s ${cell}`);
      }
      // The order the version reads a step's settings in is the table's.
      assert.deepEqual(Object.keys(derived.settings), Object.keys(step.settings), `${row.name}: ${step.id}'s settings, in order`);
    }
    assert.equal(drawn.version, row.version, `${row.name}: the version`);
  }
  fx.cleanup();
});

test('each limit is said in the table\'s words, in its order, and the opinion\'s is the rule\'s own declared-only sentence', () => {
  assert.deepEqual(Object.entries(WORKFLOW_LIMITS).map(([code, says]) => ({ code, says })), TABLE.limits);
  assert.equal(WORKFLOW_LIMITS['opinion-declared'], OPINION_DECLARED_ONLY);
});

test('the version is a digest of the text drawn: twelve hex characters, changed by any cell, escaped by hand', () => {
  const fx = makeFixture('workflow-version');
  writeFileSync(at(fx), '{}', 'utf8');
  const plain = currentWorkflow(readDriverChoices(at(fx)), 'web-app', null, []);
  assert.match(plain.version, /^[0-9a-f]{12}$/);

  writeFileSync(at(fx), JSON.stringify({ standing: { 'web-app': { says: 'a "quoted"\\word\nand a line' } } }), 'utf8');
  const said = currentWorkflow(readDriverChoices(at(fx)), 'web-app', null, []);
  assert.notEqual(said.version, plain.version);
  assert.match(workflowCanonical(said), /standing="a \\"quoted\\"\\\\word\\u000aand a line"/);
  fx.cleanup();
});

/** A door's run: `driver.json` at `path`, the registry's checkouts as `checkouts` reads them, its lines and its exit code. */
async function show(argv: string[], path: string, checkouts?: CheckoutsReader): Promise<{ code: number; out: string }> {
  const saved = process.env.DAORIS_DRIVER_CONFIG;
  process.env.DAORIS_DRIVER_CONFIG = path;
  const lines: string[] = [];
  try {
    const code = await commandDriver({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    }, undefined, checkouts, (each, context) => commandWorkflow(each, context));
    return { code, out: lines.join('\n') };
  } finally {
    if (saved === undefined) delete process.env.DAORIS_DRIVER_CONFIG;
    else process.env.DAORIS_DRIVER_CONFIG = saved;
  }
}

const unread: CheckoutsReader = async () => ({ unread: 'the service did not answer' });

function checkoutsOf(rows: { repository: string; workspace: string | null; root: string | null }[]): CheckoutsReader {
  return async () => ({ checkouts: rows });
}

const RULES = {
  workspaceLandings: { work: { form: 'branch', pattern: 'work/{quest}-{slug}', plugin: 'example.pull-request', autoAccept: true } },
  workspaceReviews: { work: { required: true, environments: [{ name: 'local', kind: 'local', procedure: 'README.md', address: 'http://localhost:4200' }] } },
  opinions: { 'web-app': { reviewers: ['codex-acp'], required: true } },
};

test('workflow show --workspace draws the workspace\'s Current from the file alone, each step with its source and its limit', async () => {
  const fx = makeFixture('workflow-show-workspace');
  writeFileSync(at(fx), JSON.stringify(RULES), 'utf8');

  const { code, out } = await show(['workflow', 'show', '--workspace', 'work'], at(fx), unread);

  assert.equal(code, 0);
  assert.match(out, /^daoris: the workspace `work` follows Current/);
  assert.match(out, /version [0-9a-f]{12}/);
  assert.match(out, /No plugin here may hold a start\./);
  assert.match(out, /The work — Agent alone · an agent/);
  // The repository's own opinion does not reach the workspace's Current.
  assert.doesNotMatch(out, /Second opinion/);
  assert.match(out, /Your look — Agent \+ you · an agent · your press: Reviewed\. In `local`\. The workspace's review rule\./);
  assert.match(out, /Partial: work here lands only once you say it is reviewed/);
  assert.match(out, /The landing — Automatic · Daoris\. On a branch named `work\/\{quest\}-\{slug\}`, accepted automatically, pushed by `example\.pull-request`\. The workspace's landing rule\./);
  assert.match(out, /The pull request — You · `example\.pull-request` · your press: Merge\. Opened by its plugin at the landing; you merge it on the platform\. The workspace's landing rule\./);
  assert.match(out, /Its plugin cannot land work on this machine now/);
  fx.cleanup();
});

test('workflow show --repository finds its workspace in the registry, and reads the plugins beside the file', async () => {
  const fx = makeFixture('workflow-show-repository');
  writeFileSync(at(fx), JSON.stringify(RULES), 'utf8');
  for (const [folder, manifest] of Object.entries({
    'example.hold': '{"id":"example.hold","hooks":{"command":["node","hold.mjs"],"points":["quest/consider"]}}',
    'example.pull-request': '{"id":"example.pull-request","hooks":{"command":["node","land.mjs"],"points":["work/land"]}}',
  })) {
    mkdirSync(join(pluginsRoot(fx.root), folder), { recursive: true });
    writeFileSync(join(pluginsRoot(fx.root), folder, MANIFEST), manifest, 'utf8');
  }

  const { code, out } = await show(['workflow', 'show', '--repository', 'Web-App'], at(fx),
    checkoutsOf([{ repository: 'notes-site', workspace: null, root: null }, { repository: 'web-app', workspace: 'work', root: null }]));

  assert.equal(code, 0);
  assert.match(out, /^daoris: `Web-App` follows Current/);
  assert.match(out, /in the workspace `work`/);
  assert.match(out, /Plugins that may hold a start: `example\.hold`\./);
  assert.match(out, /Second opinion — Agent alone · an agent\. Read by `codex-acp` before it lands; required; its answers read again\. This repository's opinion rule\./);
  assert.match(out, /Declared only: nothing reads it yet/);
  assert.match(out, /The pull request — You · `example\.pull-request`/);
  assert.match(out, /Its plugin answers no `work\/state`/);
  fx.cleanup();
});

test('a repository the registry does not hold is read as in no workspace, and said so', async () => {
  const fx = makeFixture('workflow-show-unregistered');
  writeFileSync(at(fx), JSON.stringify(RULES), 'utf8');

  const { out } = await show(['workflow', 'show', '--repository', 'web-app'], at(fx), checkoutsOf([]));

  assert.match(out, /not in the registry, so it is read as in no workspace/);
  assert.match(out, /The landing — You · Daoris · your press: Accept\. Merged into its line\. Daoris's default\./);
  fx.cleanup();
});

test('with the registry unread, a repository is drawn only where no workspace-level rule could reach it', async () => {
  const fx = makeFixture('workflow-show-unread');
  writeFileSync(at(fx), JSON.stringify(RULES), 'utf8');
  await assert.rejects(show(['workflow', 'show', '--repository', 'web-app'], at(fx), unread), (error: Error) => {
    assert.equal(error.message, 'cannot say which workspace\'s rules reach `web-app`: the registry could not be read — the '
      + 'service did not answer — and this file sets rules for a workspace. Nothing was drawn. `--workspace <name>` draws a '
      + 'workspace\'s Current from this file alone.');
    return true;
  });

  writeFileSync(at(fx), JSON.stringify({ opinions: RULES.opinions }), 'utf8');
  const { code, out } = await show(['workflow', 'show', '--repository', 'web-app'], at(fx), unread);
  assert.equal(code, 0);
  assert.match(out, /The registry was not read \(the service did not answer\); no rule here is set for a workspace, so none reaches it\./);
  assert.match(out, /Second opinion/);
  fx.cleanup();
});

test('workflow show says what it takes: Current for one repository or one workspace, or a named workflow', async () => {
  const fx = makeFixture('workflow-show-usage');
  const usage = '`driver workflow show` takes --repository <name>|--workspace <name>: the workflow its work follows, '
    + 'Current, read from its rules as they stand — e.g. `daoris driver workflow show --repository web-app`; or a named '
    + 'workflow\'s <id>[@<version>].';
  await assert.rejects(show(['workflow', 'show', '--repository', 'a', '--workspace', 'b'], at(fx), unread), (error: Error) => {
    assert.equal(error.message, usage);
    return true;
  });
  // The kind is WORKFLOW1e's to read.
  await assert.rejects(show(['workflow', 'show', '--repository', 'a', '--kind', 'docs'], at(fx), unread), (error: Error) => {
    assert.equal(error.message, `\`--kind\` is not a flag \`driver workflow show\` takes — ${usage}`);
    return true;
  });
  fx.cleanup();
});
