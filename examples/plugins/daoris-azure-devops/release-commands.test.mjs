// The approver role's act, `approve-release`, against the stand-in: a release id Azure DevOps does not answer is said not found, with
// Azure DevOps' status, and never read as a release with nothing pending; a release it answers with no approval pending says so; each
// answers its receipt (fact `approved`), and an approval given already is read first, whoever gave it, and given no second time.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { POINTS, TOKEN, WEB, signedIn, standIn, start } from './stand-in.mjs';

/** One release, its Prod stage waiting on no approval. */
async function released() {
  const api = await standIn();
  api.releases.push({ id: 13515, name: 'Release-2019', releaseDefinition: { id: 50 },
    environments: [{ id: 1301, definitionEnvironmentId: 301, name: 'Prod', status: 'notStarted', preDeployApprovals: [], postDeployApprovals: [] }], artifacts: [] });
  return api;
}

async function approve(t, api, release) {
  const plugin = start(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) });
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
  const args = { origin: WEB, release, stage: 'Prod', decision: 'approve', comment: '' };
  return (await plugin.ask('hook/command/run', { command: 'approve-release', run: 'b'.repeat(32), args, answers: {}, from: 'window', place: 'app', language: 'en' })).result;
}

const writes = (api) => api.calls.filter((call) => call.method !== 'GET');

test('a release id Azure DevOps does not answer is said not found, with its status, and nothing is changed', async (t) => {
  const api = await released();

  const answer = await approve(t, api, '2019');

  assert.equal(answer.exit, 1);
  assert.match(answer.said[0], /^Release 2019 was not found in Azure DevOps \(404\)/);
  assert.doesNotMatch(answer.said.join(' '), /pending/);
  assert.deepEqual(writes(api), []);
});

test('a release Azure DevOps answers with no approval of the stage pending says so, and nothing is changed', async (t) => {
  const api = await released();

  const answer = await approve(t, api, '13515');

  assert.equal(answer.exit, 1);
  assert.match(answer.said[0], /^Release-2019 has no approval of Prod pending: nothing is changed\./);
  assert.deepEqual(writes(api), []);
  assert.deepEqual(answer.data.receipt, { fact: 'approved', state: 'failed', code: 'not-pending', message: answer.said[0] });
});

test('the approver\'s act answers its receipt, read after it, and a second approves nothing more: the approval read first, whoever gave it', async (t) => {
  const api = await released();
  api.releases[0].environments[0].preDeployApprovals.push({ id: 77, status: 'pending', approver: { displayName: 'Release Approvers' } });

  const first = await approve(t, api, '13515');
  assert.equal(first.exit, 0);
  assert.deepEqual(first.data.receipt, { fact: 'approved', state: 'reached', release: 13515, by: 'Daoris', message: 'Prod of Release-2019 approved by Daoris.' });
  assert.deepEqual(writes(api).map((call) => call.path), ['/acme/Proj/_apis/release/approvals/77']);

  const again = await approve(t, api, '13515');
  assert.equal(again.exit, 0);
  assert.deepEqual(again.data.receipt, { fact: 'approved', state: 'reached', release: 13515, by: 'outside: Daoris', message: 'Prod of Release-2019 approved by outside: Daoris.' });
  assert.equal(writes(api).length, 1);
});
