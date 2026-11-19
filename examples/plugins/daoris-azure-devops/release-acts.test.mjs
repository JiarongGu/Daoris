// The release manager's acts as its own tools against the stand-in, never a real system: an agent creates the release from the merged
// build, starts its stage and reads it waiting on its approver, then ended; each answers a receipt Daoris journals on the step the agent
// holds; an act read first and keyed by its effect is never made twice; no act approves.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, TOKEN, WEB, folder, signedIn, standIn, start } from './stand-in.mjs';

const MERGED = 'c'.repeat(40);
const TARGET = { origin: WEB, 'pull-request': '1', pipeline: '7', 'release-definition': '43', stage: '403' };

/** Work merged into dev, built by pipeline 7 (build 41), released by definition 43 whose trigger takes dev. */
async function line() {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/dev',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: 'b'.repeat(40) } });
  api.builds.push({ id: 41, buildNumber: '20261119.41', definition: { id: 7, name: 'ci' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
    sourceBranch: 'refs/heads/dev', sourceVersion: MERGED, status: 'completed', result: 'succeeded', finishTime: '2020-01-01T00:00:00Z' });
  api.pipelines.push({ id: 7, name: 'ci', project: { id: 'project-1' }, repository: { id: 'repo-1' } });
  api.definitions.push({ id: 43, name: 'Portal-feature', artifacts: [{ type: 'Build', alias: '_ci', definitionReference: { definition: { id: '7', name: 'ci' }, project: { id: 'project-1' } } }],
    triggers: [{ triggerType: 'artifactSource', artifactAlias: '_ci', triggerConditions: [{ sourceBranch: 'dev' }] }], environments: [{ id: 403, name: 'Test', rank: 1, conditions: [] }] });
  return api;
}

let rounds = 0;
const run = async (plugin, command, args) => {
  rounds += 1;
  const answer = await plugin.ask('hook/command/run', { command, run: String(rounds).padStart(32, 'a'), args, answers: {}, from: 'agent', place: 'machine', language: 'en' });
  assert.ok(answer.result, JSON.stringify(answer));
  return answer.result;
};
const writes = (api) => api.calls.filter((call) => call.method === 'POST' || call.method === 'PATCH');

test('the manifest hands the release manager its acts as tools that write, its read as a tool, and the approval to the approver\'s holder alone', () => {
  const commands = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8')).interfaces.commands;
  const of = (name) => commands.find((each) => each.name === name);
  for (const name of ['create-release', 'start-stage', 'release-status']) {
    assert.equal(of(name)?.tool, true, `${name} is a tool`);
    for (const arg of ['origin', 'pull-request', 'pipeline', 'release-definition', 'stage']) assert.ok(of(name).args.some((each) => each.name === arg && each.required), `${name} takes ${arg}`);
  }
  assert.equal(of('create-release').writes, true);
  assert.equal(of('start-stage').writes, true);
  assert.notEqual(of('release-status').writes, true);
  assert.deepEqual([of('approve-release').tool, of('approve-release').writes, of('approve-release').role], [true, true, 'approver']);
  for (const name of ['create-release', 'start-stage', 'release-status']) assert.equal(of(name).role, undefined, `${name} is any session's tool`);
});

test('an agent creates the release, starts its stage, reads it waiting on its approver and then ended, each with its receipt, and nothing twice', async (t) => {
  const api = await line();
  const plugin = start(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) });
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });

  const created = await run(plugin, 'create-release', TARGET);
  assert.equal(created.exit, 0);
  assert.deepEqual([created.data.receipt.fact, created.data.receipt.state, created.data.receipt.release, created.data.receipt.by], ['release-created', 'reached', 1, 'Daoris']);
  assert.match(created.data.receipt.message, /^Release-1 of Portal-feature \(#43\) carries cccccccc, created by Daoris\./);
  const again = await run(plugin, 'create-release', TARGET);
  assert.equal(again.data.receipt.state, 'reached');
  assert.equal(api.releases.length, 1);

  const started = await run(plugin, 'start-stage', TARGET);
  assert.deepEqual([started.data.receipt.fact, started.data.receipt.state, started.data.receipt.by], ['stage-started', 'reached', 'Daoris']);
  assert.equal(api.deployed.length, 1);
  assert.equal((await run(plugin, 'start-stage', TARGET)).data.receipt.state, 'reached');
  assert.equal(api.deployed.length, 1);

  const stage = api.releases[0].environments[0];
  stage.status = 'inProgress';
  stage.preDeployApprovals = [{ id: 7001, status: 'pending', approver: { displayName: 'A Person' } }];
  const waiting = await run(plugin, 'release-status', TARGET);
  assert.equal(waiting.exit, 0);
  assert.deepEqual([waiting.data.receipt.fact, waiting.data.receipt.state, waiting.data.receipt.waitsOn], ['stage-ended', 'waiting', 'approver']);
  assert.match(waiting.data.receipt.message, /approve Test of Release-1/);
  const made = writes(api).length;

  Object.assign(stage.preDeployApprovals[0], { status: 'approved', approvedBy: { displayName: 'A Person' } });
  stage.status = 'succeeded';
  const ended = await run(plugin, 'release-status', TARGET);
  assert.deepEqual([ended.data.receipt.fact, ended.data.receipt.state], ['stage-ended', 'reached']);
  assert.match(ended.data.receipt.message, /^Test of Release-1 succeeded\./);
  assert.equal(writes(api).length, made, 'reading the stage writes nothing');
});

test('an act on a definition its route leaves out is refused, a failed receipt naming why, and nothing is made', async (t) => {
  const api = await line();
  api.definitions.push({ id: 42, name: 'Portal', artifacts: api.definitions[0].artifacts, environments: [{ id: 402, name: 'Live', rank: 1, conditions: [] }],
    triggers: [{ triggerType: 'artifactSource', artifactAlias: '_ci', triggerConditions: [{ sourceBranch: 'main' }] }] });
  const plugin = start(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) });
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });

  const refused = await run(plugin, 'create-release', { ...TARGET, 'release-definition': '42', stage: '402' });

  assert.equal(refused.exit, 1);
  assert.deepEqual([refused.data.receipt.fact, refused.data.receipt.state, refused.data.receipt.code], ['release-created', 'failed', 'target-changed']);
  assert.deepEqual(writes(api), []);
});
