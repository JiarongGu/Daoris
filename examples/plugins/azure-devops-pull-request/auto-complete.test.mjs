// Auto-complete on the pull-request step against the stand-in: set with the token's identity and the step's options, off by default,
// and a refusal by policy or by permission said in the evidence without failing the step.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { TOKEN, WEB, checkout, performed, signedIn, standIn, unitAt } from './stand-in.mjs';

const ORIGIN = 'https://dev.azure.com/acme/Proj/_git/app';
const ASKED = { autoComplete: true, mergeStrategy: 'squash', deleteSourceBranch: true, transitionWorkItems: true, mergeCommitMessage: 'Ship the work' };

async function opened(t, options, more = {}) {
  const api = await standIn();
  Object.assign(api, more);
  const root = checkout(ORIGIN);
  const env = { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) };
  return { api, root, ...(await performed(t, env, unitAt(root), { options })) };
}

const patches = (api) => api.calls.filter((call) => call.method === 'PATCH');

test('asked for, auto-complete is set by the token\'s own identity with the step\'s completion options, and its evidence says so', async (t) => {
  const { api, root, plugin, step } = await opened(t, ASKED);

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.url, `${WEB}/pullrequest/1`);
  assert.match(step.params.evidence.message, /auto-complete set \(squash, source branch deleted\)/i);
  assert.ok(step.params.evidence.message.length <= 300);
  assert.ok(api.calls.some((call) => call.method === 'GET' && call.path === '/acme/_apis/connectionData'));
  const [patch] = patches(api);
  assert.equal(patch.path, '/acme/Proj/_apis/git/repositories/app/pullrequests/1');
  assert.deepEqual(patch.body, {
    autoCompleteSetBy: { id: 'identity-1' },
    completionOptions: { mergeStrategy: 'squash', deleteSourceBranch: true, transitionWorkItems: true, mergeCommitMessage: 'Ship the work' },
  });

  // Azure DevOps completes it once its policies pass, and the state the monitor reads says so.
  Object.assign(api.pulls[0], { status: 'completed', closedDate: '2026-11-10T00:00:00Z', lastMergeCommit: { commitId: 'c'.repeat(40) },
    lastMergeSourceCommit: { commitId: 'b'.repeat(40) } });
  const read = (await plugin.ask('hook/work/state', { root, branch: 'daoris/work', pullRequest: `${WEB}/pullrequest/1`, origin: null, line: 'main' })).result;
  assert.deepEqual([read.state, read.mergeCommit, read.how], ['completed', 'c'.repeat(40), 'squash']);
});

test('with no option asking for it, auto-complete is off: nothing is patched and the evidence says it is off', async (t) => {
  const { api, step } = await opened(t, undefined);

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /auto-complete off/i);
  assert.equal(patches(api).length, 0);
  assert.equal(api.calls.some((call) => call.path.endsWith('/connectionData')), false);
});

test('a policy that refuses auto-complete is said in the evidence, and the pull request step is still reached', async (t) => {
  const { api, step } = await opened(t, ASKED, { patch: { status: 409, message: 'TF401398: the squash merge strategy is not allowed by a policy on main' } });

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.outputs['pull-request'].ref, 'azure-devops-pull-request/pull-request@1:1');
  assert.match(step.params.evidence.message, /auto-complete not set.*409.*TF401398/i);
  assert.equal(patches(api).length, 1);
});

test('a token without permission to set auto-complete is said in the evidence, and the pull request step is still reached', async (t) => {
  const { step } = await opened(t, ASKED, { patch: { status: 403, message: 'TF401027: You need the Git PullRequestContribute permission' } });

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /auto-complete not set.*403.*PullRequestContribute/i);
});

test('a merge strategy Azure DevOps has no name for sets nothing, saying the ones it takes', async (t) => {
  const { api, step } = await opened(t, { autoComplete: true, mergeStrategy: 'fast-forward' });

  assert.equal(step.params.state, 'reached');
  assert.match(step.params.evidence.message, /auto-complete not set.*fast-forward.*squash/i);
  assert.equal(patches(api).length, 0);
});
