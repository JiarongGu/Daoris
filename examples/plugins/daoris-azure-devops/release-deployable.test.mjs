// A release target named by the deployable it releases, and the read-only discovery that proposes targets. One repository's line builds
// one pipeline whose release definition carries two deployables, the schema's migration and the API, ordered by a stage condition; two
// decoys take the same builds. Discovery lists every definition with its stages, conditions, triggers and links, proposes each stage's
// deploysOn, and chooses none; a deployable whose target is undeclared holds listing them; a declared target that no longer exists holds
// target-changed; each of the two deployables' release nodes acts on its own stage of the one build's release.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, RELEASE, TOKEN, WEB, checkout, folder, performed, signedIn, standIn, start, tip, unitAt } from './stand-in.mjs';
import { declared } from './target.mjs';

const MERGED = 'c'.repeat(40);
const FAST = { everySeconds: 0.02, findSeconds: 0.4 };
const started = [{ name: 'ReleaseStarted', conditionType: 'event', value: '' }];
const after = (name) => [{ name, conditionType: 'environmentState', value: '4' }];
const stage = (id, name, conditions = []) => ({ id, name, rank: id % 10, conditions });

const CONFIG = { id: 50, name: 'Config', on: ['dev'], environments: [stage(501, 'Migration', started), stage(502, 'Api', after('Migration'))] };
const DECOYS = [
  { id: 174, name: 'Disabled - Config', on: ['dev'], environments: [stage(201, 'Production', started)] },
  { id: 56, name: 'Retired-Config', environments: [stage(301, 'Prod')] },
];

/** The schema owner's line: a merged pull request into dev, pipeline 7's build 41 of it, and the definitions taking pipeline 7's builds. */
async function line({ definitions = [CONFIG, ...DECOYS], made = [] } = {}) {
  const api = await standIn();
  api.pulls.push({ pullRequestId: 1, status: 'completed', repository: { webUrl: WEB }, sourceRefName: 'refs/heads/daoris/work', targetRefName: 'refs/heads/dev',
    lastMergeCommit: { commitId: MERGED }, lastMergeSourceCommit: { commitId: tip } });
  api.builds.push({ id: 41, buildNumber: '20261113.41', definition: { id: 7, name: 'ci-7' }, project: { id: 'project-1' }, repository: { id: 'repo-1' },
    sourceBranch: 'refs/heads/dev', sourceVersion: MERGED, status: 'completed', result: 'succeeded' });
  api.pipelines.push({ id: 7, name: 'ci-7', project: { id: 'project-1' }, repository: { id: 'repo-1' },
    _links: { web: { href: 'https://dev.azure.com/acme/Proj/_build/definition?definitionId=7' } } });
  for (const each of definitions) {
    api.definitions.push({ id: each.id, name: each.name, artifacts: [{ type: 'Build', alias: '_ci', isPrimary: true,
      definitionReference: { definition: { id: '7', name: 'ci' }, project: { id: 'project-1' } } }],
    triggers: (each.on ?? []).length === 0 ? [] : [{ triggerType: 'artifactSource', artifactAlias: '_ci', triggerConditions: each.on.map((name) => ({ sourceBranch: name })) }],
    environments: each.environments, _links: { web: { href: `https://dev.azure.com/acme/Proj/_release?definitionId=${each.id}` } } });
  }
  for (const each of made) {
    api.releases.push({ id: each.id, name: `Release-${each.id}`, releaseDefinition: { id: each.of },
      environments: each.environments.map((one) => ({ ...one, id: one.id + 1000, definitionEnvironmentId: one.id, preDeployApprovals: [], postDeployApprovals: [] })),
      artifacts: [{ type: 'Build', sourceId: 'project-1:7', definitionReference: { version: { id: '41' } } }] });
  }
  return api;
}

async function released(t, api, options) {
  const unit = unitAt(checkout(WEB), ['release'], { line: 'dev', pullRequest: `${WEB}/pullrequest/1` });
  return performed(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) }, unit, { step: RELEASE, options: { ...FAST, ...options } });
}

const writes = (api) => api.calls.filter((call) => call.method === 'POST' || call.method === 'PATCH');

const discovered = async (t, api, args) => {
  const plugin = start(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) });
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
  return (await plugin.ask('hook/command/run', { command: 'discover-releases', run: 'a'.repeat(32), args, answers: {}, from: 'agent', place: 'machine', language: 'en' })).result;
};

test('the manifest declares discover-releases as a read: a tool that takes the origin and an optional pipeline, and never writes', () => {
  const commands = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8')).interfaces.commands;
  const discover = commands.find((each) => each.name === 'discover-releases');
  assert.equal(discover?.tool, true);
  assert.notEqual(discover.writes, true);
  assert.deepEqual(discover.args.map((arg) => [arg.name, arg.required === true]), [['origin', true], ['pipeline', false]]);
});

test('discovery lists every definition taking the line\'s pipeline with its stages, conditions, triggers and links, proposes deploysOn, and chooses none', async (t) => {
  const api = await line();

  const answer = await discovered(t, api, { origin: WEB });

  assert.equal(answer.exit, 0);
  assert.deepEqual(writes(api), []);
  const [pipeline] = answer.data.pipelines;
  assert.equal(pipeline.id, 7);
  assert.deepEqual(pipeline.definitions.map((each) => each.id), [50, 174, 56]);
  const config = pipeline.definitions[0];
  assert.equal(config.url, 'https://dev.azure.com/acme/Proj/_release?definitionId=50');
  assert.deepEqual(config.triggers, ['dev']);
  assert.deepEqual(config.stages.map((each) => [each.id, each.name, each.after, each.deploysOn]),
    [[501, 'Migration', [], 'merge'], [502, 'Api', ['Migration'], 'merge']]);
  assert.deepEqual(pipeline.definitions[2].stages.map((each) => each.deploysOn), ['start']);
  assert.equal(answer.data.chosen, undefined);
  assert.match(answer.said.join('\n'), /#50 Config \(501 Migration, 502 Api; CD on dev\)/);
  assert.match(answer.said.join('\n'), /candidates only: declare the one each deployable releases on/i);
});

test('a deployable whose target is undeclared holds, naming it and listing what discovery finds, and writes nothing', async (t) => {
  const api = await line();

  const { step } = await released(t, api, { deployable: 'config-api' });

  assert.deepEqual([step.params.state, step.params.code], ['failed', 'no-target']);
  const said = step.params.evidence.message;
  assert.match(said, /^No release target is declared for deployable `config-api`: pipeline, releaseDefinition, stage and action\. Found for pipeline 7:/);
  assert.match(said, /#50 Config \(501 Migration, 502 Api; CD on dev\)/);
  assert.match(said, /#174 Disabled - Config \(201 Production; CD on dev\)/);
  assert.deepEqual(writes(api), []);
});

test('a declared target that no longer exists holds target-changed: a pipeline gone, and a definition gone, each naming the deployable', async (t) => {
  const pipelineGone = await line();
  const definitionGone = await line();

  const gone = (await released(t, pipelineGone, { deployable: 'config-api', pipeline: 9, releaseDefinition: 'none', stage: 'deploy', action: 'observe' })).step;
  const removed = (await released(t, definitionGone, { deployable: 'config-api', pipeline: 7, releaseDefinition: 77, stage: 502, action: 'observe' })).step;

  assert.deepEqual([gone.params.code, removed.params.code], ['target-changed', 'target-changed']);
  assert.match(gone.params.evidence.message, /^The declared release target of deployable `config-api` no longer resolves: pipeline 9 is not found\./);
  assert.match(removed.params.evidence.message, /^The declared release target of deployable `config-api` no longer resolves: release definition #77 is not found\./);
  assert.deepEqual([...writes(pipelineGone), ...writes(definitionGone)], []);
});

test('two deployables on one build: each release node acts on its own stage of the one release, and says the deployable it released', async (t) => {
  const made = [{ id: 60, of: 50, environments: [{ ...stage(501, 'Migration', started), status: 'succeeded' }, { ...stage(502, 'Api', after('Migration')), status: 'succeeded' }] }];
  const api = await line({ made });
  const target = { pipeline: 7, releaseDefinition: 50, action: 'observe' };

  const migration = (await released(t, api, { ...target, deployable: 'config-migration', stage: 501, deploysOn: 'merge' })).step;
  const service = (await released(t, api, { ...target, deployable: 'config-api', stage: 502, deploysOn: 'merge' })).step;

  assert.deepEqual([migration.params.state, service.params.state], ['reached', 'reached']);
  assert.match(migration.params.evidence.message, /^Deployable `config-migration`: Release-60 of Config from build 20261113\.41 \(cccccccc\): Migration succeeded/);
  assert.match(service.params.evidence.message, /^Deployable `config-api`: Release-60 of Config from build 20261113\.41 \(cccccccc\): Api succeeded/);
  assert.deepEqual(writes(api), []);
});

test('deploysOn is read as declared, push where undeclared, and an unknown act is no declaration', () => {
  const target = { pipeline: 7, releaseDefinition: 50, stage: 502, action: 'observe' };

  assert.equal(declared({ ...target, deploysOn: 'start' }).deploysOn, 'start');
  assert.equal(declared(target).deploysOn, 'push');
  assert.deepEqual(declared({ ...target, deploysOn: 'later' }).missing, ['deploysOn']);
  assert.equal(declared({ ...target, deployable: ' config-api ' }).deployable, 'config-api');
  assert.equal(declared(target).deployable, null);
});
