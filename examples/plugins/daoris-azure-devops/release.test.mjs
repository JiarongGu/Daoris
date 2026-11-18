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

test('a stage waiting on an approval tells Daoris, once, that the step waits on its approver: the act, who is asked, since when, the release\'s link', async (t) => {
  const asked = { status: 'pending', approver: { displayName: 'Release Approvers' }, createdOn: '2026-11-16T09:50:00Z' };
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'queued', { preDeployApprovals: [asked] })] });
  api.onRead = moving(api, 'Production', 4, { status: 'succeeded', preDeployApprovals: [{ ...asked, status: 'approved' }] });

  const { step, progress } = await released(t, api, DECLARED);

  assert.equal(progress.length, 1);
  const [waits] = progress.map((frame) => frame.params);
  assert.equal(waits.actor, 'approver');
  assert.equal(waits.invocation, 'i-1');
  assert.notEqual(waits.id, 'i-1');
  assert.equal(waits.evidence.environment, 'Production');
  assert.equal(waits.evidence.url, RELEASE_WEB);
  assert.match(waits.evidence.message, /^Release-12 of app-release \(#3\), build 20261110\.41 from `main`: Approve Production in Azure DevOps: asked of Release Approvers since 2026-11-16 09:50 UTC\./);
  assert.equal(step.params.state, 'reached');
  assert.deepEqual(writes(api), []);
});

test('a stage the step observes that nothing started waits on its deployer, and its start in Azure DevOps ends the wait', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'notStarted')] });
  api.onRead = (method, path, n) => {
    if (method !== 'GET' || path !== '/acme/Proj/_apis/release/releases/90') return;
    if (n >= 3) api.releases[0].environments[1].status = n >= 5 ? 'succeeded' : 'inProgress';
  };

  const { step, progress } = await released(t, api, DECLARED);

  assert.deepEqual(progress.map((frame) => frame.params.actor ?? null), ['deployer', null]);
  assert.match(progress[0].params.evidence.message, /Deploy Production in Azure DevOps: nothing started it, and the step observes it\./);
  assert.match(progress[1].params.evidence.message, /Production deploying\./);
  assert.equal(step.params.state, 'reached');
  assert.deepEqual(writes(api), []);
});

test('a watch tells its first look though nothing waits, so a wait kept from an earlier process gives way to where the stage stands', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });
  api.onRead = moving(api, 'Production', 3, { status: 'succeeded' });

  const { step, progress } = await released(t, api, DECLARED);

  assert.deepEqual(progress.map((frame) => frame.params.actor ?? null), [null]);
  assert.match(progress[0].params.evidence.message, /Production deploying\./);
  assert.equal(step.params.state, 'reached');
});

test('no release made from the merged build names the releases of its definition it found and the builds they carry', async (t) => {
  const api = await merged();
  api.releases[0].artifacts[0].definitionReference.version = { id: '40', name: '20261110.40' };

  const { step } = await released(t, api, DECLARED);

  assert.equal(step.params.state, 'failed');
  assert.equal(step.params.code, 'no-release');
  assert.match(step.params.evidence.message, /It found Release-12 \(#90\) from build 20261110\.40, not this one\./);
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

async function onFeature(on, triggers) {
  const api = await merged();
  api.pulls[0].targetRefName = 'refs/heads/feature/x';
  api.builds[1].sourceBranch = 'refs/heads/feature/x';
  api.definitions[0].triggers = triggers ?? [{ triggerType: 'artifactSource', artifactAlias: '_app-ci', triggerConditions: on.map((name) => ({ sourceBranch: name })) }];
  api.releases.length = 0;
  return api;
}

test('a build whose branch the release definition\'s trigger leaves out finishes saying Nothing to release, at once, naming the trigger', async (t) => {
  const api = await onFeature(['refs/heads/main']);

  for (const action of ['observe', 'start-stage']) {
    const { step } = await released(t, api, { ...DECLARED, action, findSeconds: 30 });

    assert.equal(step.params.state, 'reached');
    assert.match(step.params.evidence.message, /Nothing to release: release definition app-release \(#3\) releases no build of `feature\/x` — its trigger takes `main`\./);
  }
  assert.deepEqual(writes(api), []);
});

test('a release definition trigger that cannot be read is not taken for leaving the branch out: the step waits and fails no-release', async (t) => {
  const api = await onFeature([], [{ triggerType: 'artifactSource', artifactAlias: '_app-ci', triggerConditions: 'refs/heads/main' }]);

  const { step } = await released(t, api, DECLARED);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
});

test('a release definition trigger that takes the build\'s branch still waits for its release', async (t) => {
  const api = await onFeature(['refs/heads/feature/*']);

  const { step } = await released(t, api, DECLARED);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message, /No release of app-release \(#3\) was made from build/);
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
