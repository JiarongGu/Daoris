// The release as the run's own steps (RELEASESTEPS1) against the stand-in, never a real system (D309 point 5): each step settles on the fact
// Azure DevOps holds whoever made it, a release is matched by the landed commit across its builds, made once keyed by its effect across a lost
// answer and a restart, waited for with no fixed give-up; every act keeps to its route (ACTPIN1); an approval is never given here
// (APPROVEHERE1); a refused key waits on the credentials keeper, never failing for access (ACCESSSTEP1).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { RELEASE, TOKEN, WEB, checkout, performed, signedIn, standIn, unitAt } from './stand-in.mjs';
import { effectOf } from './release-steps.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.2 };
const TARGET = { pipeline: 7, releaseDefinition: 43, stage: 403, action: 'create-and-start' };
const STEP = (id) => RELEASE.replace(/release$/, id);

/** Work merged into dev, built by pipeline 7 (`builds`), released by definition 43 whose trigger takes dev; main's 42 takes main. */
async function line({ builds = [{ id: 41 }], made = [] } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/dev',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: 'b'.repeat(40) } });
  for (const each of builds) {
    api.builds.push({ id: each.id, buildNumber: `20261116.${each.id}`, definition: { id: 7, name: 'ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
      sourceBranch: 'refs/heads/dev', sourceVersion: MERGED, status: each.status ?? 'completed', result: 'succeeded', finishTime: '2020-01-01T00:00:00Z' });
  }
  api.pipelines.push({ id: 7, name: 'ci', project: { id: 'project-1' }, repository: { id: 'repo-1' } });
  const definition = (id, name, on, stage) => ({ id, name, artifacts: [{ type: 'Build', alias: '_ci', definitionReference: { definition: { id: '7', name: 'ci' }, project: { id: 'project-1' } } }],
    triggers: [{ triggerType: 'artifactSource', artifactAlias: '_ci', triggerConditions: [{ sourceBranch: on }] }], environments: [{ id: stage, name: 'Test', rank: 1, conditions: [] }] });
  api.definitions.push(definition(42, 'Portal', 'main', 402), definition(43, 'Portal-feature', 'dev', 403));
  for (const each of made) {
    api.releases.push({ id: each.id, name: `Release-${each.id}`, releaseDefinition: { id: 43 }, createdBy: { displayName: 'A Person' },
      environments: [{ id: 1403, definitionEnvironmentId: 403, name: 'Test', status: each.status ?? 'notStarted', preDeployApprovals: each.approvals ?? [], deploySteps: each.steps ?? [] }],
      artifacts: [{ type: 'Build', alias: '_ci', sourceId: 'project-1:7', definitionReference: { version: { id: String(each.build) } } }] });
  }
  return api;
}

const step = (t, api, id, options = {}, more = {}) => performed(t, { DAORIS_HOST_API: api.address, ...signedIn(more.token ?? TOKEN) },
  unitAt(checkout('https://dev.azure.com/acme/Proj/_git/app'), [id], { line: 'dev', pullRequest: `${WEB}/pullrequest/1` }),
  { step: STEP(id), options: { ...FAST, ...TARGET, ...options }, ...(more.deadlineAt ? { deadlineAt: more.deadlineAt } : {}) });
const writes = (api) => api.calls.filter((call) => call.method === 'POST' || call.method === 'PATCH');

test('a release made by hand from another build of the same commit settles create, credited to its maker, and nothing is made', async (t) => {
  const api = await line({ builds: [{ id: 41 }, { id: 44 }], made: [{ id: 13510, build: 41 }] });

  const { step: done } = await step(t, api, 'release-create', { action: 'start-stage' });

  assert.equal(done.params.state, 'reached');
  assert.match(done.params.evidence.message, /^Release-13510 of Portal-feature \(#43\) carries cccccccc, created by outside: A Person\./);
  assert.deepEqual(writes(api), []);
});

test('create waits on the release manager while the build runs, with no fixed give-up, then makes one release keyed by its effect, across a lost answer and a restart', async (t) => {
  const api = await line({ builds: [{ id: 41, status: 'inProgress' }] });
  api.onRead = (method, path, n) => { if (method === 'GET' && path === '/acme/Proj/_apis/build/builds' && n >= 4) api.builds[0].status = 'completed'; };
  let lost = false;
  api.lose = (method, path) => (method === 'POST' && path === '/acme/Proj/_apis/release/releases' && !lost ? (lost = true) : false);

  const first = await step(t, api, 'release-create');
  const again = await step(t, api, 'release-create');

  assert.equal(first.step.params.state, 'reached');
  assert.equal(again.step.params.state, 'reached');
  assert.equal(writes(api).length, 1);
  assert.equal(api.releases.length, 1);
  assert.match(api.releases[0].description, new RegExp(`effect ${effectOf(43, MERGED)}`));
  assert.ok(first.progress.some((frame) => frame.params.actor === 'release-manager' && /build 20261116\.41 running/.test(frame.params.evidence.message)));
  assert.match(again.step.params.evidence.message, /created by Daoris/);
});

test('ACTPIN1 at every step: the main line\'s definition with a build from dev is refused target-changed, the route its choice', async (t) => {
  const api = await line({ made: [{ id: 90, build: 41 }] });

  const { step: done } = await step(t, api, 'release-start', { releaseDefinition: 42, stage: 402 });

  assert.deepEqual([done.params.state, done.params.code], ['failed', 'target-changed']);
  assert.match(done.params.evidence.message, /The route for `dev`: #43 Portal-feature\./);
  assert.deepEqual(writes(api), []);
});

test('a stage started by hand settles start credited to who started it; observed, an unstarted one waits on the deployer', async (t) => {
  const byHand = await line({ made: [{ id: 90, build: 41, status: 'inProgress', steps: [{ requestedBy: { displayName: 'A Person' } }] }] });
  const waiting = await line({ made: [{ id: 90, build: 41 }] });

  const started = (await step(t, byHand, 'release-start')).step;
  const observed = await step(t, waiting, 'release-start', { action: 'observe' }, { deadlineAt: new Date(Date.now() + 5600).toISOString() });

  assert.match(started.params.evidence.message, /^Test of Release-90 started by outside: A Person\./);
  assert.deepEqual([observed.step.params.state, observed.step.params.code], ['failed', 'timeout']);
  assert.equal(observed.progress[0].params.actor, 'deployer');
  assert.deepEqual([...writes(byHand), ...writes(waiting)], []);
});

test('an approval waits on its approver, naming the target, the commit and its branch, and settles on the approval Azure DevOps records; it never approves', async (t) => {
  const pending = { id: 7, status: 'pending', approver: { displayName: 'Release Approvers' } };
  const api = await line({ made: [{ id: 90, build: 41, status: 'queued', approvals: [pending] }] });
  api.onRead = (method, path, n) => {
    if (method === 'GET' && path === '/acme/Proj/_apis/release/releases/90' && n >= 3) Object.assign(api.releases[0].environments[0].preDeployApprovals[0], { status: 'approved', approvedBy: { displayName: 'The Owner' } });
  };

  const { step: done, progress } = await step(t, api, 'release-approve');

  assert.equal(progress[0].params.actor, 'approver');
  assert.match(progress[0].params.evidence.message, /approve Test of Release-90, Portal-feature \(#43\), carrying cccccccc from `dev`/);
  assert.match(done.params.evidence.message, /^Test of Release-90 approved by outside: The Owner\./);
  assert.deepEqual(writes(api), []);
});

test('a release the holder named settles create where its commit is the landed one or descends from it, else is refused naming its commit', async (t) => {
  const LATER = 'd'.repeat(40);
  const OTHER = 'e'.repeat(40);
  const api = await line({ made: [{ id: 91, build: 45 }, { id: 92, build: 46 }] });
  api.builds.push({ id: 45, buildNumber: '20261116.45', definition: { id: 7 }, repository: { id: 'repo-1' }, sourceBranch: 'refs/heads/dev', sourceVersion: LATER },
    { id: 46, buildNumber: '20261116.46', definition: { id: 7 }, repository: { id: 'repo-1' }, sourceBranch: 'refs/heads/dev', sourceVersion: OTHER });
  api.parents[LATER] = [MERGED];

  const descends = (await step(t, api, 'release-create', { release: `${WEB}/_releaseProgress?releaseId=91` })).step;
  const elsewhere = (await step(t, api, 'release-create', { release: '92' })).step;

  assert.equal(descends.params.state, 'reached');
  assert.match(descends.params.evidence.message, /^Release-91 of Portal-feature \(#43\) carries dddddddd, after the landed cccccccc, created by outside: A Person\./);
  assert.equal(descends.params.evidence.by, 'outside: A Person');
  assert.deepEqual([elsewhere.params.state, elsewhere.params.code], ['failed', 'target-changed']);
  assert.match(elsewhere.params.evidence.message, /release 92 carries eeeeeeee, which is neither the landed cccccccc nor after it/);
  assert.deepEqual(writes(api), []);
});

test('each step credits the identity Azure DevOps names on its evidence, beside its words; an approval is never made by any step', async (t) => {
  const byHand = await line({ made: [{ id: 90, build: 41, status: 'inProgress', steps: [{ requestedBy: { displayName: 'A Person' } }],
    approvals: [{ id: 7, status: 'approved', approvedBy: { displayName: 'The Owner' } }] }] });
  const marked = await line({ made: [{ id: 90, build: 41, status: 'queued', approvals: [{ id: 7, status: 'pending', approver: { displayName: 'Release Approvers' } }] }] });

  const started = (await step(t, byHand, 'release-start')).step;
  const approved = (await step(t, byHand, 'release-approve')).step;
  const asked = await Promise.all(['observe', 'start-stage', 'create-and-start'].map((action) =>
    step(t, marked, 'release-approve', { action }, { deadlineAt: new Date(Date.now() + 5400).toISOString() })));

  assert.equal(started.params.evidence.by, 'outside: A Person');
  assert.equal(approved.params.evidence.by, 'outside: The Owner');
  // Whatever the action, the approval stays its approver's: no step approves, so no agent holding the step does.
  assert.ok(asked.every(({ progress }) => progress.some((frame) => frame.params.actor === 'approver')));
  assert.deepEqual(marked.calls.filter((call) => /\/approvals\//.test(call.path)), []);
});

test('ACCESSSTEP1: a refused key waits on the credentials keeper with the page that fixes it, and is never failed for access', async (t) => {
  const api = await line();

  const { step: done, progress } = await step(t, api, 'release-create', {}, { token: 'a-revoked-token', deadlineAt: new Date(Date.now() + 5600).toISOString() });

  assert.equal(progress[0].params.actor, 'credentials-keeper');
  assert.match(progress[0].params.evidence.url, /_usersSettings\/tokens/);
  assert.notEqual(done.params.code, 'unauthorized');
  assert.equal(done.params.code, 'timeout');
});
