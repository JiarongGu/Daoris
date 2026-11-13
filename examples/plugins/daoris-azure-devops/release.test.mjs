// The release step against the stand-in, its target declared: the declared stage watched to its end, started only where the action says.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RELEASE, TOKEN, WEB, checkout, performed, signedIn, standIn, tip, unitAt } from './stand-in.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.4 };
const RELEASE_WEB = 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90';
/** Pipeline 7's build, release definition 3, its stage 102 (Production): what the step's options name. */
const DECLARED = { pipeline: 7, releaseDefinition: 3, stage: 102, action: 'observe' };

const stage = (id, name, status, more = {}) => ({ id, definitionEnvironmentId: id + 90, name, rank: id - 10, status, preDeployApprovals: [], postDeployApprovals: [], ...more });

/** A merged pull request whose commit pipeline 7 built on main, and, unless `classic` is false, release definition 3 taking that build. */
async function merged({ classic = true, environments = [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'succeeded')], built = true } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/main',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: tip } });
  const build = (id, version) => ({ id, buildNumber: `20261110.${id}`, definition: { id: 7, name: 'app-ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
    sourceBranch: 'refs/heads/main', sourceVersion: version, status: 'completed', result: 'succeeded',
    _links: { web: { href: `https://dev.azure.com/acme/Proj/_build/results?buildId=${id}` } } });
  api.builds.push(build(40, 'd'.repeat(40)), ...(built ? [build(41, MERGED)] : []));
  api.pipelines.push({ id: 7, name: 'app-ci', project: { id: 'project-1' }, repository: { id: 'repo-1' } });
  if (classic) {
    api.definitions.push({ id: 3, name: 'app-release', triggers: [], artifacts: [{ type: 'Build', alias: '_app-ci', isPrimary: true,
      definitionReference: { definition: { id: '7', name: 'app-ci' }, project: { id: 'project-1' } } }],
    environments: [{ id: 101, name: 'Dev', rank: 1, conditions: [] }, { id: 102, name: 'Production', rank: 2, conditions: [] }] });
    api.releases.push({ id: 90, name: 'Release-12', releaseDefinition: { id: 3 }, environments,
      artifacts: [{ type: 'Build', sourceId: 'project-1:7', definitionReference: { version: { id: '41' } } }], _links: { web: { href: RELEASE_WEB } } });
  }
  return api;
}

async function released(t, api, options, more = {}) {
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const unit = unitAt(root, ['release'], { line: 'main', pullRequest: `${WEB}/pullrequest/1` });
  return performed(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) }, unit, { step: RELEASE, options: { ...FAST, ...options }, ...more });
}

/** Moves a stage on once its release has been read `after` times. */
const moving = (api, name, after, change) => (method, path, n) => {
  if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && n >= after) Object.assign(api.releases[0].environments.find((each) => each.name === name), change);
};
const writes = (api) => api.calls.filter((call) => call.method === 'POST' || call.method === 'PATCH');

test('the declared stage of the release made from the merged commit\'s build is watched until it succeeds, its outcome the evidence', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });
  api.onRead = moving(api, 'Production', 3, { status: 'succeeded' });

  const { step } = await released(t, api, DECLARED);

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.ref, 'azure-devops-pull-request/release@1:i-1');
  assert.equal(step.params.evidence.url, RELEASE_WEB);
  assert.match(step.params.evidence.message, /Release-12 of app-release from build 20261110\.41 \(cccccccc\): Production succeeded\./);
  const builds = api.calls.find((call) => call.path === '/acme/Proj/_apis/build/builds');
  assert.match(builds.query, /definitions=7/);
  assert.match(builds.query, /branchName=refs%2Fheads%2Fmain/);
  assert.ok(api.calls.some((call) => call.path === '/acme/Proj/_apis/release/releases' && /definitionId=3/.test(call.query) && /artifactVersionId=41/.test(call.query)));
  assert.deepEqual(writes(api), []);
});

test('the declared stage whose deployment fails ends the step as failed with its words', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });
  api.onRead = moving(api, 'Production', 2, { status: 'rejected' });

  const { step } = await released(t, api, DECLARED);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'stage-failed']);
  assert.match(step.params.evidence.message, /Production failed its deployment/);
  assert.equal(step.params.evidence.url, RELEASE_WEB);
});

test('a stage partly succeeded is a success only where the step\'s success says so', async (t) => {
  const strict = await merged({ environments: [stage(12, 'Production', 'partiallySucceeded')] });
  const loose = await merged({ environments: [stage(12, 'Production', 'partiallySucceeded')] });

  const failed = (await released(t, strict, DECLARED)).step;
  const reached = (await released(t, loose, { ...DECLARED, success: 'partially-succeeded' })).step;

  assert.deepEqual([failed.params.state, failed.params.code, reached.params.state], ['failed', 'stage-failed', 'reached']);
  assert.match(failed.params.evidence.message, /Production partly succeeded, and the step's success asks succeeded/);
});

test('a stage waiting on an approval is said and waited on, never approved here, and its approval decides', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'),
    stage(12, 'Production', 'queued', { preDeployApprovals: [{ status: 'pending', approver: { displayName: 'Release Approvers' } }] })] });
  api.onRead = moving(api, 'Production', 4, { status: 'succeeded', preDeployApprovals: [{ status: 'approved', approver: { displayName: 'Release Approvers' } }] });

  const { plugin, step } = await released(t, api, DECLARED);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /: Production succeeded after an approval by Release Approvers\. Way: the token\.$/);
  assert.match(await plugin.stop(), /Production waits on an approval by Release Approvers/);
  assert.deepEqual(writes(api), []);
});

test('start-stage starts the declared stage nothing started, once, and no other; observe starts nothing', async (t) => {
  const started = await merged({ environments: [stage(11, 'Dev', 'notStarted'), stage(12, 'Production', 'notStarted')] });
  started.onRead = (method, path) => {
    const production = started.releases[0].environments[1];
    if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && production.status === 'inProgress') production.status = 'succeeded';
  };
  const watched = await merged({ environments: [stage(11, 'Dev', 'notStarted'), stage(12, 'Production', 'notStarted')] });

  const { step } = await released(t, started, { ...DECLARED, action: 'start-stage' });
  const observed = (await released(t, watched, DECLARED, { deadlineAt: new Date(Date.now() + 5600).toISOString() })).step;

  assert.equal(step.params.state, 'reached');
  assert.deepEqual(started.deployed.map((each) => [each.release, each.environment]), [[90, 'Production']]);
  assert.match(step.params.evidence.message, /Production started here, succeeded/);
  assert.deepEqual([observed.params.state, observed.params.code], ['failed', 'timeout']);
  assert.match(observed.params.evidence.message, /Production not started: the step observes it, and start-stage would start it/);
  assert.deepEqual(writes(watched), []);
});

test('no build of the merged commit in the declared pipeline is said as no release found, after the declared period', async (t) => {
  const api = await merged({ built: false });

  const { step } = await released(t, api, DECLARED);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message, /no build of pipeline 7 ran for cccccccc on `main` within 0\.4 s/);
});

test('a merge the declared pipeline does not build (its trigger leaves the change out) finishes saying so, at once', async (t) => {
  const api = await merged({ built: false });
  Object.assign(api.pipelines[0], { process: { type: 1 },
    triggers: [{ triggerType: 'continuousIntegration', settingsSourceType: 1, branchFilters: ['+refs/heads/main'], pathFilters: ['+/src'] }] });
  api.changes.push('/docs/readme.md');

  const { step } = await released(t, api, { ...DECLARED, findSeconds: 30 });

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Nothing to release: pipeline 7 does not build cccccccc on `main`, which changed \/docs\/readme\.md/);
});

test('the declared stage of a pipeline run\'s own stages is watched where the target names no release definition, an approval said', async (t) => {
  const api = await merged({ classic: false });
  api.builds[1].status = 'inProgress';
  api.timelines[41] = [
    { id: 's1', type: 'Stage', name: 'Build', identifier: 'Build', order: 1, state: 'completed', result: 'succeeded' },
    { id: 's2', type: 'Stage', name: 'Deploy', identifier: 'Deploy', order: 2, state: 'pending', result: null },
    { id: 'c1', parentId: 's2', type: 'Checkpoint', state: 'inProgress' },
    { id: 'a1', parentId: 'c1', type: 'Checkpoint.Approval', state: 'inProgress' },
  ];
  api.onRead = (method, path, n) => {
    if (method !== 'GET' || path !== '/acme/Proj/_apis/build/builds/41/timeline' || n < 4) return;
    Object.assign(api.timelines[41][1], { state: 'completed', result: 'succeeded' });
    api.timelines[41][3].state = 'completed';
    api.builds[1].status = 'completed';
  };

  const { plugin, step } = await released(t, api, { pipeline: 7, releaseDefinition: 'none', stage: 'Deploy', action: 'observe' });

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.url, 'https://dev.azure.com/acme/Proj/_build/results?buildId=41');
  assert.match(step.params.evidence.message, /app-ci run 20261110\.41 \(cccccccc\): Deploy succeeded after an approval\./);
  assert.match(await plugin.stop(), /Deploy waits on an approval/);
});

test('a watch that outlasts its deadline fails as timeout, saying where the declared stage stood', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });

  const { step } = await released(t, api, DECLARED, { deadlineAt: new Date(Date.now() + 5600).toISOString() });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'timeout']);
  assert.match(step.params.evidence.message, /stopped watching at the step's deadline: Production deploying\./);
});
