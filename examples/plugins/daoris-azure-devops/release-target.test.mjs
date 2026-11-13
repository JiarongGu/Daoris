// The release step's target, declared or held for: with no target declared, or one that no longer resolves, the step holds listing
// what it found, even one, and writes nothing; a line feeding three release definitions (one labelled disabled that still makes a release on
// every merge, one for a retired environment, the real one started by hand) never has a stage started on a found definition; a release is
// made only where the action says so, from the exact merged build, its self-starting stages said first, and never made twice.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RELEASE, TOKEN, WEB, checkout, performed, signedIn, standIn, tip, unitAt } from './stand-in.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.4 };

const started = [{ name: 'ReleaseStarted', conditionType: 'event', value: '' }];
const environment = (id, name, conditions = []) => ({ id, name, rank: id % 10, conditions });

/** A merged pull request into dev whose commit pipeline 7 built (build 41), and the release definitions taking pipeline 7's builds. */
async function line({ definitions = [], made = [], builds = [{ id: 41, pipeline: 7, version: MERGED }] } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/dev',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: tip } });
  for (const each of builds) {
    api.builds.push({ id: each.id, buildNumber: `20261110.${each.id}`, definition: { id: each.pipeline, name: `ci-${each.pipeline}` }, project: { id: 'project-1' },
      repository: { id: 'repo-1' }, sourceBranch: 'refs/heads/dev', sourceVersion: each.version, status: 'completed', result: each.result ?? 'succeeded' });
  }
  api.pipelines.push({ id: 7, name: 'ci-7', project: { id: 'project-1' }, repository: { id: 'repo-1' } });
  for (const each of definitions) {
    api.definitions.push({ id: each.id, name: each.name, artifacts: [{ type: 'Build', alias: '_ci', isPrimary: true,
      definitionReference: { definition: { id: String(each.pipeline ?? 7), name: 'ci' }, project: { id: 'project-1' } } }],
    triggers: (each.on ?? []).length === 0 ? [] : [{ triggerType: 'artifactSource', artifactAlias: '_ci', triggerConditions: each.on.map((name) => ({ sourceBranch: name })) }],
    environments: each.environments, _links: { web: { href: `https://dev.azure.com/acme/Proj/_release?definitionId=${each.id}` } } });
  }
  for (const each of made) {
    api.releases.push({ id: each.id, name: `Release-${each.id}`, releaseDefinition: { id: each.of },
      environments: each.environments.map((stage) => ({ ...stage, id: stage.id + 1000, definitionEnvironmentId: stage.id, status: stage.status ?? 'notStarted',
        preDeployApprovals: [], postDeployApprovals: [] })),
      artifacts: [{ type: 'Build', sourceId: 'project-1:7', definitionReference: { version: { id: String(each.build ?? 41) } } }] });
  }
  return api;
}

async function released(t, api, options = {}, more = {}) {
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const unit = unitAt(root, ['release'], { line: 'dev', pullRequest: `${WEB}/pullrequest/1` });
  return performed(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) }, unit, { step: RELEASE, options: { ...FAST, ...options }, ...more });
}

const writes = (api) => api.calls.filter((call) => call.method === 'POST' || call.method === 'PATCH');

/** The reported line: a definition labelled disabled whose trigger still makes a release per merge, a retired one, the real one by hand. */
const THREE = [
  { id: 174, name: 'Disabled - Portal', on: ['dev'], environments: [environment(201, 'Production')] },
  { id: 56, name: 'Retired-Prod-Portal', environments: [environment(301, 'Prod')] },
  { id: 42, name: 'Portal', environments: [environment(401, 'Dev'), environment(402, 'Prod')] },
];
const DISABLED_MADE = { id: 90, of: 174, environments: [{ ...environment(201, 'Production'), status: 'notStarted' }] };

test('a line feeding three release definitions with no target declared holds, listing each by id with its stages and trigger, and writes nothing', async (t) => {
  const api = await line({ definitions: THREE, made: [DISABLED_MADE] });

  const { step } = await released(t, api, { findSeconds: 60 });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-target']);
  const said = step.params.evidence.message;
  assert.match(said, /^No release target is declared: pipeline, releaseDefinition, stage and action\. Found for pipeline 7:/);
  assert.match(said, /#174 Disabled - Portal \(201 Production; CD on dev\)/);
  assert.match(said, /#56 Retired-Prod-Portal \(301 Prod; no CD\)/);
  assert.match(said, /#42 Portal \(401 Dev, 402 Prod; no CD\)/);
  assert.match(step.params.evidence.url, /_release/);
  assert.deepEqual(writes(api), []);
});

test('one release definition is listed and held for too: discovery proposes, never chooses', async (t) => {
  const api = await line({ definitions: [THREE[2]] });

  const { step } = await released(t, api, { action: 'create-and-start' });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-target']);
  assert.match(step.params.evidence.message, /#42 Portal \(401 Dev, 402 Prod; no CD\)/);
  assert.deepEqual(writes(api), []);
});

test('the real definition declared with start-stage: the disabled one\'s release is never touched, and no release of the real one holds after the period', async (t) => {
  const api = await line({ definitions: THREE, made: [DISABLED_MADE] });

  const { step } = await released(t, api, { pipeline: 7, releaseDefinition: 42, stage: 402, action: 'start-stage' });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message, /^No release of Portal \(#42\) was made from build 20261110\.41 \(cccccccc\) within 0\.4 s; the step's action does not create one\./);
  assert.deepEqual(writes(api), []);
});

test('a declared target that no longer resolves holds with the candidates: a stage gone, a definition gone, a definition taking another pipeline', async (t) => {
  const stageGone = await line({ definitions: THREE });
  const definitionGone = await line({ definitions: THREE });
  const otherPipeline = await line({ definitions: [{ ...THREE[2], pipeline: 8 }] });

  const codes = [];
  for (const [api, options] of [[stageGone, { releaseDefinition: 42, stage: 499 }], [definitionGone, { releaseDefinition: 77, stage: 402 }],
    [otherPipeline, { releaseDefinition: 42, stage: 402 }]]) {
    const { step } = await released(t, api, { pipeline: 7, action: 'start-stage', ...options });
    codes.push(step.params.code);
    assert.match(step.params.evidence.message, /^The declared release target no longer resolves: /);
    assert.deepEqual(writes(api), []);
  }

  assert.deepEqual(codes, ['target-changed', 'target-changed', 'target-changed']);
});

test('one definition with no trigger and no release: observe holds after its period, create-and-start makes it from build 41, its self-starting stages said first', async (t) => {
  const one = [{ id: 42, name: 'Portal', environments: [environment(401, 'Dev', started), environment(402, 'Prod')] }];
  const observed = await line({ definitions: one });
  const created = await line({ definitions: one });
  created.onRead = (method, path) => {
    const made = created.releases[0];
    const prod = made?.environments.find((each) => each.definitionEnvironmentId === 402);
    if (method === 'GET' && prod && path === `/acme/Proj/_apis/release/releases/${made.id}` && prod.status === 'inProgress') prod.status = 'succeeded';
  };

  const held = (await released(t, observed, { pipeline: 7, releaseDefinition: 42, stage: 402, action: 'observe' })).step;
  const { plugin, step } = await released(t, created, { pipeline: 7, releaseDefinition: 42, stage: 402, action: 'create-and-start' });

  assert.deepEqual([held.params.state, held.params.code], ['failed', 'no-release']);
  assert.deepEqual(writes(observed), []);
  assert.equal(step.params.state, 'reached');
  const posted = created.calls.filter((call) => call.method === 'POST');
  assert.equal(posted.length, 1);
  assert.equal(posted[0].body.definitionId, 42);
  assert.deepEqual(posted[0].body.artifacts, [{ alias: '_ci', instanceReference: { id: '41', name: '20261110.41' } }]);
  assert.match(posted[0].body.description, /k-1/);
  assert.deepEqual(created.deployed.map((each) => each.environment), ['Prod']);
  assert.match(step.params.evidence.message, /Release-1 of Portal from build 20261110\.41 \(cccccccc\), made here, its creation starting Dev by itself: Prod started here, succeeded/);
  assert.match(await plugin.stop(), /creating a release of Portal \(#42\) from build 20261110\.41; its creation starts Dev by itself/);
});

test('a wrong build is refused: the declared pipeline\'s build of another commit, another pipeline\'s build, a failed build, a release of another build', async (t) => {
  const declared = { pipeline: 7, releaseDefinition: 42, stage: 402, action: 'create-and-start' };
  const one = [{ id: 42, name: 'Portal', environments: [environment(402, 'Prod')] }];
  const elsewhere = await line({ definitions: one, builds: [{ id: 40, pipeline: 7, version: 'd'.repeat(40) }, { id: 41, pipeline: 9, version: MERGED }] });
  const failed = await line({ definitions: one, builds: [{ id: 41, pipeline: 7, version: MERGED, result: 'failed' }] });
  // A release whose definition's artifact is build 40, listed for build 41 by a second artifact of the same pipeline.
  const another = await line({ definitions: one, made: [{ id: 90, of: 42, build: 40, environments: [environment(402, 'Prod')] }] });
  another.releases[0].artifacts[0].alias = '_ci';
  another.releases[0].artifacts.push({ type: 'Build', alias: '_tools', sourceId: 'project-1:7', definitionReference: { version: { id: '41' } } });

  const fromElsewhere = (await released(t, elsewhere, declared)).step;
  const fromFailed = (await released(t, failed, declared)).step;
  const fromAnother = (await released(t, another, { ...declared, action: 'start-stage' })).step;

  assert.deepEqual([fromElsewhere.params.code, fromFailed.params.code, fromAnother.params.code], ['no-release', 'build-failed', 'no-release']);
  assert.match(fromElsewhere.params.evidence.message, /no build of pipeline 7 ran for cccccccc on `dev`/);
  assert.deepEqual([...writes(elsewhere), ...writes(failed), ...writes(another)], []);
});

test('an interrupted creation is reconciled: the retry finds the release the lost answer made, and makes no second', async (t) => {
  const one = [{ id: 42, name: 'Portal', environments: [environment(402, 'Prod')] }];
  const api = await line({ definitions: one });
  let lost = false;
  api.lose = (method, path) => {
    if (lost || method !== 'POST' || path !== '/acme/Proj/_apis/release/releases') return false;
    lost = true;
    return true;
  };
  api.onRead = (method, path) => {
    const prod = api.releases[0]?.environments[0];
    if (method === 'GET' && prod && path === `/acme/Proj/_apis/release/releases/${api.releases[0].id}` && prod.status === 'inProgress') prod.status = 'succeeded';
  };
  const declared = { pipeline: 7, releaseDefinition: 42, stage: 402, action: 'create-and-start' };

  const first = (await released(t, api, declared)).step;
  const again = (await released(t, api, declared)).step;

  assert.deepEqual([first.params.state, first.params.code], ['failed', 'unavailable']);
  assert.equal(again.params.state, 'reached');
  assert.equal(api.calls.filter((call) => call.method === 'POST').length, 1);
  assert.equal(api.releases.length, 1);
});
