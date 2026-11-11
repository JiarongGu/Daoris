// The release step with no options, for a repository whose one build is the artifact of several release definitions (the shapes of an
// organisation's four: a CD trigger on the branch, none, an approval, a definition whose only stage is manual), and for a merge that builds
// nothing (every pipeline path-filtered or off): the releases made from the merged commit's build are watched, a merge no pipeline takes
// finishes saying so.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { TOKEN, WEB, checkout, performed, RELEASE, signedIn, standIn, tip, unitAt } from './stand-in.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.4 };

const stage = (id, name, status, more = {}) => ({ id, name, rank: id, status, preDeployApprovals: [], postDeployApprovals: [],
  conditions: [{ name: 'ReleaseStarted', conditionType: 'event', value: '' }], ...more });
const manual = (id, name) => stage(id, name, 'notStarted', { conditions: [] });
const approval = (status) => ({ status, approver: { displayName: 'Release Approvers' } });

/** A merged pull request into `branch` whose commit pipeline 7 built (build 41), and what release management holds for that build. */
async function place({ branch = 'dev', built = true, definitions = [], made = [], pipelines = [], files = {}, changes = [] } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: `refs/heads/${branch}`,
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: tip } });
  if (built) {
    api.builds.push({ id: 41, buildNumber: '20261110.41', definition: { id: 7, name: 'app-ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
      sourceBranch: `refs/heads/${branch}`, sourceVersion: MERGED, status: 'completed', result: 'succeeded' });
  }
  for (const each of definitions) {
    const alias = '_app-ci';
    api.definitions.push({ id: each.id, name: each.name, artifacts: [{ type: 'Build', alias, isPrimary: true,
      definitionReference: { definition: { id: '7', name: 'app-ci' }, project: { id: 'project-1' } } }],
      triggers: (each.on ?? []).length === 0 ? [] : [{ triggerType: 'artifactSource', artifactAlias: alias,
        triggerConditions: each.on.map((name) => ({ sourceBranch: name, tags: [] })) }] });
  }
  for (const each of made) {
    api.releases.push({ id: each.id, name: `Release-${each.id}`, releaseDefinition: { id: each.of }, environments: each.environments,
      artifacts: [{ type: 'Build', sourceId: 'project-1:7', definitionReference: { version: { id: '41' } } }],
      _links: { web: { href: `https://dev.azure.com/acme/Proj/_releaseProgress?releaseId=${each.id}` } } });
  }
  api.pipelines.push(...pipelines);
  Object.assign(api.files, files);
  api.changes.push(...changes);
  return api;
}

async function released(t, api, options = {}) {
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const unit = unitAt(root, ['release'], { line: 'dev', pullRequest: `${WEB}/pullrequest/1` });
  return performed(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) }, unit, { step: RELEASE, options: { ...FAST, ...options } });
}

const asked = (api, fragment) => api.calls.filter((call) => call.path.includes(fragment));

test('a build taken by a CD-triggered definition, an untriggered one and a disabled one whose only stage is manual is released by the one that deploys', async (t) => {
  const api = await place({
    definitions: [{ id: 53, name: 'Dev-LumachainReportApi', on: ['dev'] }, { id: 54, name: 'QA-LumachainReportApi' }, { id: 55, name: 'Prod-LumachainReportApi' },
      { id: 174, name: 'Disabled - ReportAPI', on: ['dev'] }],
    made: [{ id: 90, of: 53, environments: [stage(11, 'WebApp-DEV', 'inProgress')] }, { id: 91, of: 174, environments: [manual(21, 'Manual')] }],
  });
  api.onRead = (method, path, n) => {
    if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && n >= 3) api.releases[0].environments[0].status = 'succeeded';
  };

  const { step } = await released(t, api);

  assert.deepEqual([step.params.state, step.params.evidence.ref], ['reached', 'azure-devops-pull-request/release@1:i-1']);
  assert.match(step.params.evidence.message, /Release-90 of Dev-LumachainReportApi from build 20261110\.41 \(cccccccc\): WebApp-DEV succeeded/);
  assert.match(step.params.evidence.message, /Disabled - ReportAPI/);
  assert.equal(api.calls.some((call) => call.method === 'PATCH'), false);
});

test('a build taken by a dev, a QA and a Prod definition is released by the one a CD trigger on the branch makes, the others never read', async (t) => {
  const api = await place({
    definitions: [{ id: 24, name: 'Dev-ConfigService', on: ['dev'] }, { id: 25, name: 'QA-ConfigService' }, { id: 26, name: 'Prod-ConfigService' }],
    made: [{ id: 90, of: 24, environments: [stage(11, 'Dev', 'succeeded')] }],
  });

  const { step } = await released(t, api);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Release-90 of Dev-ConfigService from build 20261110\.41 \(cccccccc\): Dev succeeded\./);
});

test('a branch with a slash is taken by the definition whose trigger names it', async (t) => {
  const api = await place({
    branch: 'globalplatform/identityservice',
    definitions: [{ id: 6, name: 'Dev-LumachainIdentity', on: ['globalplatform/identityservice'] }, { id: 11, name: 'Prod-LumachainIdentity' }, { id: 17, name: 'QA-LumachainIdentity' }],
    made: [{ id: 90, of: 6, environments: [stage(11, 'Dev', 'succeeded')] }],
  });

  const { step } = await released(t, api);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Release-90 of Dev-LumachainIdentity/);
});

test('of five definitions, the one triggering on the merged branch is watched, its approval waited on and never given, the master and dev one left alone', async (t) => {
  const api = await place({
    branch: 'feature/lumaapp',
    definitions: [{ id: 2, name: 'Report-UI v1', on: ['master', 'dev'] }, { id: 42, name: 'Report-UI', on: ['feature/lumaapp'] }, { id: 45, name: 'QA-LumaApp' },
      { id: 56, name: 'Deprecated-Prod-LumaApp' }, { id: 148, name: 'QA-LumaApp - Storage Account CDN' }],
    made: [{ id: 90, of: 42, environments: [stage(11, 'Dev', 'succeeded'), stage(12, 'Prod', 'notStarted', { preDeployApprovals: [approval('pending')] })] }],
  });
  api.onRead = (method, path, n) => {
    if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && n >= 3) {
      Object.assign(api.releases[0].environments[1], { status: 'succeeded', preDeployApprovals: [approval('approved')] });
    }
  };

  const { plugin, step } = await released(t, api);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /Release-90 of Report-UI from build 20261110\.41 \(cccccccc\): Dev succeeded; Prod succeeded after an approval by Release Approvers\./);
  assert.match(await plugin.stop(), /Prod waits on an approval by Release Approvers/);
  assert.equal(api.calls.some((call) => call.method === 'PATCH'), false);
});

test('two triggered definitions are watched together and a failure of one fails the step in its words', async (t) => {
  const api = await place({
    definitions: [{ id: 53, name: 'Dev-A', on: ['dev'] }, { id: 54, name: 'Dev-B', on: ['dev'] }],
    made: [{ id: 90, of: 53, environments: [stage(11, 'Dev', 'succeeded')] }, { id: 91, of: 54, environments: [stage(21, 'Dev', 'rejected')] }],
  });

  const { step } = await released(t, api);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'stage-failed']);
  assert.match(step.params.evidence.message, /Release-91 of Dev-B[^;]*: Dev failed its deployment/);
});

test('several definitions with a trigger on no merged branch and no release made from the build fail as no release, not ambiguous', async (t) => {
  const api = await place({ definitions: [{ id: 25, name: 'QA-ConfigService' }, { id: 26, name: 'Prod-ConfigService', on: ['master'] }] });

  const { step } = await released(t, api);

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
  assert.match(step.params.evidence.message, /no release was made from build 20261110\.41 .*none of QA-ConfigService, Prod-ConfigService has a trigger on `dev`/);
});

const infrastructure = () => ({
  id: 9, name: 'terraform-plan', repository: { id: 'repo-1' }, process: { type: 2, yamlFilename: '/pipelines/plan.yml' },
  triggers: [{ triggerType: 'continuousIntegration', settingsSourceType: 2 }],
});
const PLAN = 'trigger:\n  branches:\n    include:\n    - master\n  paths:\n    include:\n    - terraform/*\n    - pipelines/plan.yml\n\npool:\n  vmImage: ubuntu-latest\n';

test('a merge that changes only files every pipeline\'s path filter leaves out builds nothing and finishes saying so, at once', async (t) => {
  const api = await place({ branch: 'master', built: false, pipelines: [infrastructure()], files: { '/pipelines/plan.yml': PLAN }, changes: ['/docs/readme.md'] });

  const started = Date.now();
  const { step } = await released(t, api, { findSeconds: 30 });

  assert.deepEqual([step.params.state, step.params.evidence.ref], ['reached', 'azure-devops-pull-request/release@1:i-1']);
  assert.match(step.params.evidence.message, /Nothing to release: no pipeline of the repository builds cccccccc on `master`, which changed \/docs\/readme\.md/);
  assert.ok(Date.now() - started < 10000);
});

test('a merge into a branch no pipeline triggers on, or a pipeline that is off, builds nothing; a classic pipeline\'s path filter counts the same', async (t) => {
  const yaml = await place({ branch: 'master', built: false, pipelines: [infrastructure()], files: { '/pipelines/plan.yml': 'trigger:\n  branches:\n    include: [release/*]\n' }, changes: ['/terraform/main.tf'] });
  const off = await place({ branch: 'master', built: false, pipelines: [infrastructure()], files: { '/pipelines/plan.yml': 'trigger: none\n' }, changes: ['/terraform/main.tf'] });
  const classic = await place({ branch: 'master', built: false, changes: ['/docs/a.md', '/README.md'], pipelines: [{ id: 3, name: 'classic', repository: { id: 'repo-1' }, process: { type: 1 },
    triggers: [{ triggerType: 'continuousIntegration', settingsSourceType: 1, branchFilters: ['+refs/heads/master'], pathFilters: ['+/src', '-/src/docs'] }] }] });

  const states = [];
  for (const api of [yaml, off, classic]) states.push((await released(t, api)).step.params.state);

  assert.deepEqual(states, ['reached', 'reached', 'reached']);
});

test('a merge a pipeline takes by its path filter, and one that cannot be read, still fail as no release after the find', async (t) => {
  const taken = await place({ branch: 'master', built: false, pipelines: [infrastructure()], files: { '/pipelines/plan.yml': PLAN }, changes: ['/terraform/vpc/main.tf', '/docs/a.md'] });
  const unread = await place({ branch: 'master', built: false, pipelines: [infrastructure()], files: {}, changes: ['/docs/a.md'] });
  const unknown = await place({ branch: 'master', built: false, pipelines: [{ id: 7, name: 'app-ci' }], changes: ['/docs/a.md'] });

  for (const api of [taken, unread, unknown]) {
    const { step } = await released(t, api);
    assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-release']);
    assert.match(step.params.evidence.message, /no build ran for cccccccc on `master`/);
  }
});
