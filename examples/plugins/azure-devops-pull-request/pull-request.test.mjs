// The pull-request step and the state query against the stand-in: open, reuse, state with the merge commit, a refused token, no token,
// an earlier attempt asked of a platform that is down, and that the plugin runs no git, gh or az.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, STEP, TOKEN, WEB, checkout, folder, performed, standIn, start, tip, unitAt } from './stand-in.mjs';

const opened = (t, env, root, key = 'k-1') => performed(t, env, unitAt(root), { key });

test('it opens the pull request through the REST API with the handed token, from the origin its checkout names', async (t) => {
  const api = await standIn();
  const root = checkout('daoris::https://acme@dev.azure.com/acme/Proj/_git/app');
  const { step } = await opened(t, { DAORIS_HOST_API: api.address, DAORIS_HOST_TOKEN: TOKEN }, root);

  assert.equal(step.params.state, 'reached');
  assert.equal(step.params.evidence.url, `${WEB}/pullrequest/1`);
  assert.equal(step.params.outputs['pull-request'].ref, 'azure-devops-pull-request/pull-request@1:1');
  const post = api.calls.find((call) => call.method === 'POST');
  assert.deepEqual([post.body.sourceRefName, post.body.targetRefName, post.basic], ['refs/heads/daoris/work', 'refs/heads/main', true]);
  assert.equal(JSON.stringify(step).includes(TOKEN), false);
});

test('a key delivered again makes no second pull request, and one already active for the branch is found, not made again', async (t) => {
  const api = await standIn();
  const root = checkout('git@ssh.dev.azure.com:v3/acme/Proj/app');
  const { plugin, perform } = await opened(t, { DAORIS_HOST_API: api.address, DAORIS_HOST_TOKEN: TOKEN }, root);

  await perform('i-1', 'k-1');
  await perform('i-2', 'k-2');
  const second = await plugin.next((frame) => frame.method === 'work/step' && frame.params.invocation === 'i-2');

  assert.equal(api.calls.filter((call) => call.method === 'POST').length, 1);
  assert.equal(second.params.evidence.url, `${WEB}/pullrequest/1`);
});

test('its state is open, then completed with the merge commit and how once Azure DevOps completes it', async (t) => {
  const api = await standIn();
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const { plugin } = await opened(t, { DAORIS_HOST_API: api.address, DAORIS_HOST_TOKEN: TOKEN }, root);
  const frame = { root, branch: 'daoris/work', pullRequest: `${WEB}/pullrequest/1`, origin: null, line: 'main' };

  const open = (await plugin.ask('hook/work/state', frame)).result;
  Object.assign(api.pulls[0], { status: 'completed', closedDate: '2026-11-06T00:00:00Z', lastMergeCommit: { commitId: 'c'.repeat(40) },
    lastMergeSourceCommit: { commitId: 'a'.repeat(40) }, completionOptions: { mergeStrategy: 'squash' } });
  const done = (await plugin.ask('hook/work/state', { ...frame, pullRequest: null })).result;

  assert.equal(open.state, 'open');
  assert.deepEqual([done.state, done.mergeCommit, done.sourceCommit, done.target, done.how], ['completed', 'c'.repeat(40), 'a'.repeat(40), 'main', 'squash']);
});

test('a refused token fails the step as unauthorized, and no token at all fails it as no-token', async (t) => {
  const api = await standIn();
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');

  const refused = await opened(t, { DAORIS_HOST_API: api.address, DAORIS_HOST_TOKEN: 'not-the-token' }, root);
  const absent = await opened(t, { DAORIS_HOST_API: api.address }, root, 'k-2');

  assert.deepEqual([refused.step.params.state, refused.step.params.code], ['failed', 'unauthorized']);
  assert.deepEqual([absent.step.params.state, absent.step.params.code], ['failed', 'no-token']);
  assert.equal(api.pulls.length, 0);
});

test('an attempt an earlier process began is running while Azure DevOps is down or limiting, saying why, and unknown once the token is refused', async (t) => {
  const api = await standIn();
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const begun = { attempts: { 'k-1': { idempotencyKey: 'k-1', invocation: 'i-1', unit: 'app:daoris/work', step: STEP, stepVersion: 1,
    subjectRevision: tip, inputs: [], options: {}, state: 'begun', life: 'an earlier process' } } };
  const reconciled = async (token) => {
    const plugin = start(t, { DAORIS_HOST_API: api.address, DAORIS_HOST_TOKEN: token }, begun);
    await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
    await plugin.ask('hook/work/watch', { generation: 1, resync: false, units: [unitAt(root)] });
    const answer = (await plugin.ask('hook/work/reconcile', { unit: 'app:daoris/work', step: STEP, invocation: 'i-1', idempotencyKey: 'k-1', subjectRevision: tip })).result;
    return { answer, said: await plugin.stop() };
  };

  api.status = 503;
  const down = await reconciled(TOKEN);
  api.status = 429;
  const limited = await reconciled(TOKEN);
  api.status = null;
  const refused = await reconciled('not-the-token');

  assert.deepEqual([down.answer, limited.answer, refused.answer], [{ state: 'running' }, { state: 'running' }, { state: 'unknown' }]);
  assert.match(down.said, /Azure DevOps answered 503/);
  assert.match(limited.said, /limiting this token/);
});

test('it runs no git, gh or az, and declares no tool for them', () => {
  const source = readdirSync(folder).filter((file) => file.endsWith('.mjs') && !file.endsWith('.test.mjs') && file !== 'stand-in.mjs')
    .map((file) => readFileSync(join(folder, file), 'utf8')).join('\n');
  const manifest = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8'));

  assert.equal(/child_process|spawn|execFile|execSync/.test(source), false);
  assert.deepEqual(manifest.tools.map((tool) => tool.id), ['node']);
});
