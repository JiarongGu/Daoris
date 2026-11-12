// The release step against the stand-in: the merged commit's build found, its classic release or its pipeline's stages watched to their
// end, a failed stage, a pending approval waited on and said, a stage started by `deploy`, nothing found, and a watch that runs out of time.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RELEASE, TOKEN, WEB, checkout, performed, signedIn, standIn, tip, unitAt } from './stand-in.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.4 };
const BUILD_WEB = 'https://dev.azure.com/acme/Proj/_build/results?buildId=41';
const RELEASE_WEB = 'https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=90';

const stage = (id, name, status, more = {}) => ({ id, name, rank: id - 10, status, preDeployApprovals: [], postDeployApprovals: [], ...more });

/** A merged pull request whose commit pipeline 7 built on main, and, unless `classic` is false, release definition 3 taking that build. */
async function merged({ classic = true, environments = [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'succeeded')], built = true } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/main',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: tip } });
  const build = (id, version) => ({ id, buildNumber: `20261110.${id}`, definition: { id: 7, name: 'app-ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
    sourceBranch: 'refs/heads/main', sourceVersion: version, status: 'completed', result: 'succeeded',
    _links: { web: { href: `https://dev.azure.com/acme/Proj/_build/results?buildId=${id}` } } });
  api.builds.push(build(40, 'd'.repeat(40)), ...(built ? [build(41, MERGED)] : []));
  api.pipelines.push({ id: 7, name: 'app-ci' });
  if (classic) {
    api.definitions.push({ id: 3, name: 'app-release', artifacts: [{ type: 'Build', isPrimary: true,
      definitionReference: { definition: { id: '7', name: 'app-ci' }, project: { id: 'project-1' } } }] });
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

test('the classic release made from the merged commit\'s build is found and watched until each stage succeeds, each stage\'s outcome its evidence', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });
  api.onRead = moving(api, 'Production', 3, { status: 'succeeded' });

  const { step } = await released(t, api, {});

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.ref, 'azure-devops-pull-request/release@1:i-1');
  assert.equal(step.params.evidence.url, RELEASE_WEB);
  assert.match(step.params.evidence.message, /Release-12 of app-release from build 20261110\.41 \(cccccccc\): Dev succeeded; Production succeeded\./);
  const builds = api.calls.find((call) => call.path === '/acme/Proj/_apis/build/builds');
  assert.match(builds.query, /repositoryId=repo-1/);
  assert.match(builds.query, /branchName=refs%2Fheads%2Fmain/);
  assert.ok(api.calls.some((call) => call.path === '/acme/Proj/_apis/release/releases' && /artifactVersionId=41/.test(call.query)));
});

test('a stage whose deployment fails ends the step as failed with its words', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'inProgress')] });
  api.onRead = moving(api, 'Production', 2, { status: 'rejected' });

  const { step } = await released(t, api, {});

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'stage-failed']);
  assert.match(step.params.evidence.message, /Production failed its deployment/);
  assert.equal(step.params.evidence.url, RELEASE_WEB);
});

test('a stage waiting on an approval is said and waited on, never approved here, and its approval decides', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'),
    stage(12, 'Production', 'queued', { preDeployApprovals: [{ status: 'pending', approver: { displayName: 'Release Approvers' } }] })] });
  api.onRead = moving(api, 'Production', 4, { status: 'succeeded', preDeployApprovals: [{ status: 'approved', approver: { displayName: 'Release Approvers' } }] });

  const { plugin, step } = await released(t, api, { stages: 'Production' });

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /: Production succeeded after an approval by Release Approvers\. Way: the token\.$/);
  assert.match(await plugin.stop(), /Production waits on an approval by Release Approvers/);
  assert.equal(api.calls.some((call) => call.method === 'PATCH'), false);
});

test('deploy starts a named stage no trigger starts, of the release definition and pipeline named, once', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'notStarted')] });
  api.onRead = (method, path) => {
    const production = api.releases[0].environments[1];
    if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && production.status === 'inProgress') production.status = 'succeeded';
  };

  const { step } = await released(t, api, { pipeline: 'app-ci', releaseDefinition: 'app-release', deploy: 'Production' });

  assert.equal(step.params.state, 'reached');
  assert.deepEqual(api.deployed.map((each) => [each.release, each.environment]), [[90, 'Production']]);
  assert.match(step.params.evidence.message, /Production started here, succeeded/);
  assert.match(api.calls.find((call) => call.path === '/acme/Proj/_apis/build/builds').query, /definitions=7/);
});

test('no build of the merged commit is said as no release found', async (t) => {
  const api = await merged({ built: false });

  const { step } = await released(t, api, {});

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message, /no build ran for cccccccc on `main`/);
});

test('a build no release was made from, with no stages of its own, is said as no release found', async (t) => {
  const none = await merged();
  none.releases.length = 0;
  const yaml = await merged({ classic: false });

  const fromNone = (await released(t, none, {})).step;
  const fromYaml = (await released(t, yaml, {})).step;

  assert.deepEqual([fromNone.params.code, fromYaml.params.code], ['no-release', 'no-release']);
  assert.match(fromNone.params.evidence.message, /no release of app-release was made from build 20261110\.41/);
  assert.match(fromYaml.params.evidence.message, /build 20261110\.41 has no stages, and no release definition takes its artifact/);
});

/** A merged build with no release of its own, taken by release definition 3 whose continuous-deployment trigger is off. */
async function untriggered() {
  const api = await merged();
  api.releases.length = 0;
  api.definitions[0].artifacts[0].alias = '_app-ci';
  Object.assign(api.definitions[0], { triggers: [], environments:[stage(11, 'Production', 'notStarted', { conditions: [{ name: 'ReleaseStarted', conditionType: 'event' }] })] });
  return api;
}

const releasesAsked = (api) => api.calls.filter((call) => call.method === 'GET' && call.path === '/acme/Proj/_apis/release/releases').length;

test('a build whose release definition makes no release by itself is said so at the first look, and the step holds for the person', async (t) => {
  const api = await untriggered();

  const { step } = await released(t, api, { findSeconds: 60 });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message,
    /^No release of app-release exists for build 20261110\.41 \(cccccccc\): its continuous-deployment trigger is off, so none is made by itself\. Make one and try the step again, or set the step's createRelease option\./);
  assert.equal(step.params.evidence.url, BUILD_WEB);
  assert.equal(releasesAsked(api), 1);
  assert.equal(api.calls.some((call) => call.method === 'POST'), false);
});

test('createRelease makes the release no trigger makes, from the merged build, and watches it; a later attempt finds it rather than making another', async (t) => {
  const api = await untriggered();
  api.onRead = (method, path) => {
    const made = api.releases[0];
    if (method === 'GET' && made && path === `/acme/Proj/_apis/release/releases/${made.id}`) made.environments[0].status = 'succeeded';
  };

  const first = (await released(t, api, { createRelease: true })).step;
  const again = (await released(t, api, { createRelease: true })).step;

  assert.deepEqual([first.params.state, again.params.state], ['reached', 'reached']);
  const posted = api.calls.filter((call) => call.method === 'POST' && call.path === '/acme/Proj/_apis/release/releases');
  assert.equal(posted.length, 1);
  assert.equal(posted[0].body.definitionId, 3);
  assert.deepEqual(posted[0].body.artifacts, [{ alias: '_app-ci', instanceReference: { id: '41', name: '20261110.41' } }]);
  assert.match(first.params.evidence.message, /^Release-1 of app-release from build 20261110\.41 \(cccccccc\), made here as no trigger makes one: Production succeeded\./);
});

test('a pipeline\'s own stages are watched where no release definition takes its build, an approval said', async (t) => {
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

  const { plugin, step } = await released(t, api, {});

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.url, BUILD_WEB);
  assert.match(step.params.evidence.message, /app-ci run 20261110\.41 \(cccccccc\): Build succeeded; Deploy succeeded after an approval\./);
  assert.match(await plugin.stop(), /Deploy waits on an approval/);
});

test('a watch that outlasts its deadline fails as timeout, saying where each stage stood', async (t) => {
  const api = await merged({ environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Production', 'notStarted')] });

  const { step } = await released(t, api, {}, { deadlineAt: new Date(Date.now() + 5600).toISOString() });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'timeout']);
  assert.match(step.params.evidence.message, /stopped watching at the step's deadline: Dev succeeded; Production not started: nothing started it, and the step's deploy option starts it\./);
});
