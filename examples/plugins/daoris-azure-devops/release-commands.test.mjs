// The approver's own press, `approve-release`, against the stand-in: a release id Azure DevOps does not answer is said not found, with
// Azure DevOps' status, and never read as a release with nothing pending; a release it answers with no approval pending says so.
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
});
