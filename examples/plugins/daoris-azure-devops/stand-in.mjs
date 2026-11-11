// A stand-in of Azure DevOps' REST API on this machine's loopback, and the plugin started over it as the driver starts it: the tests'
// one harness. Its organization, project and repository are `acme`, `Proj` and `app`; nothing of Azure DevOps' or Daoris's is reached.
import { after } from 'node:test';
import { spawn } from 'node:child_process';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

export const folder = dirname(fileURLToPath(import.meta.url));
export const TOKEN = 'throwaway-token-for-the-stand-in';
export const WEB = 'https://dev.azure.com/acme/Proj/_git/app';
export const POINTS = ['work/watch', 'work/step', 'work/perform', 'work/state'];
export const STEP = 'daoris-azure-devops/pull-request';
export const RELEASE = 'daoris-azure-devops/release';
export const tip = 'b'.repeat(40);

const scratch = mkdtempSync(join(tmpdir(), 'azure-pr-'));
after(() => rmSync(scratch, { recursive: true, force: true }));

const GIT = '/acme/Proj/_apis/git/repositories/app';
const PROJECT = '/acme/Proj/_apis';

/**
 * Azure DevOps' calls as its REST API answers them, admitting one token as Basic's password; else `status`, while set. A test writes
 * what the platform holds (`pulls`, `builds`, `timelines`, `pipelines`, `definitions`, `releases`), `patch` to answer a pull request's
 * update with a refusal, and `onRead(method, path, n)` to move it on as it is read.
 */
export async function standIn() {
  const api = {
    pulls: [], calls: [], address: '', status: null, me: { id: 'identity-1', providerDisplayName: 'Daoris' }, patch: null,
    builds: [], timelines: {}, pipelines: [], definitions: [], releases: [], deployed: [], onRead: null,
  };
  const { pulls, calls } = api;
  const reads = new Map();
  const answer = (request, url, text) => {
    const path = url.pathname;
    const query = (name) => url.searchParams.get(name);
    if (request.method === 'GET' && path === '/acme/_apis/connectionData') return [200, { authenticatedUser: api.me, authorizedUser: api.me }];
    if (request.method === 'GET' && path === GIT) return [200, { id: 'repo-1', name: 'app', defaultBranch: 'refs/heads/main' }];
    if (path === `${GIT}/pullrequests` && request.method === 'GET') {
      return [200, { value: pulls.filter((pr) => pr.sourceRefName === query('searchCriteria.sourceRefName')) }];
    }
    if (path === `${GIT}/pullrequests` && request.method === 'POST') {
      const sent = JSON.parse(text);
      const made = { pullRequestId: pulls.length + 1, status: 'active', repository: { webUrl: WEB }, sourceRefName: sent.sourceRefName,
        targetRefName: sent.targetRefName, title: sent.title };
      pulls.push(made);
      return [201, made];
    }
    const pull = /^\/acme\/Proj\/_apis\/git\/repositories\/app\/pullrequests\/(\d+)$/.exec(path);
    const pr = pull ? pulls[Number(pull[1]) - 1] : null;
    if (pr && request.method === 'GET') return [200, pr];
    if (pr && request.method === 'PATCH') {
      if (api.patch) return [api.patch.status, { message: api.patch.message }];
      const sent = JSON.parse(text);
      Object.assign(pr, sent, sent.autoCompleteSetBy ? { autoCompleteSetBy: { id: sent.autoCompleteSetBy.id, displayName: api.me.providerDisplayName } } : {});
      return [200, pr];
    }
    if (request.method === 'GET' && path === `${PROJECT}/build/definitions`) return [200, { value: api.pipelines.filter((each) => each.name === query('name')) }];
    if (request.method === 'GET' && path === `${PROJECT}/build/builds`) {
      const wanted = query('definitions')?.split(',').map(Number);
      return [200, { value: api.builds.filter((build) => build.sourceBranch === query('branchName') && build.repository.id === query('repositoryId')
        && (!wanted || wanted.includes(build.definition.id))).sort((a, b) => b.id - a.id) }];
    }
    const build = /^\/acme\/Proj\/_apis\/build\/builds\/(\d+)(\/timeline)?$/.exec(path);
    if (build && request.method === 'GET') {
      const found = api.builds.find((each) => each.id === Number(build[1]));
      if (!found) return [404, { message: 'no such build' }];
      return build[2] ? [200, { records: api.timelines[found.id] ?? [] }] : [200, found];
    }
    if (request.method === 'GET' && path === `${PROJECT}/release/definitions`) {
      return [200, { value: api.definitions.filter((each) => (query('searchText') === null || each.name === query('searchText'))
        && (query('artifactSourceId') === null || each.artifacts.some((artifact) => artifact.type === query('artifactType')
          && `${artifact.definitionReference.project.id}:${artifact.definitionReference.definition.id}` === query('artifactSourceId')))) }];
    }
    const definition = /^\/acme\/Proj\/_apis\/release\/definitions\/(\d+)$/.exec(path);
    if (definition && request.method === 'GET') {
      const found = api.definitions.find((each) => each.id === Number(definition[1]));
      return found ? [200, found] : [404, { message: 'no such release definition' }];
    }
    if (request.method === 'GET' && path === `${PROJECT}/release/releases`) {
      return [200, { value: api.releases.filter((each) => each.releaseDefinition.id === Number(query('definitionId'))
        && each.artifacts.some((artifact) => artifact.type === query('artifactTypeId') && artifact.sourceId === query('sourceId')
          && artifact.definitionReference.version.id === query('artifactVersionId'))) }];
    }
    const release = /^\/acme\/Proj\/_apis\/release\/releases\/(\d+)(?:\/environments\/(\d+))?$/.exec(path);
    const held = release ? api.releases.find((each) => each.id === Number(release[1])) : null;
    if (held && !release[2] && request.method === 'GET') return [200, held];
    const environment = held && release[2] ? held.environments.find((each) => each.id === Number(release[2])) : null;
    if (environment && request.method === 'PATCH') {
      const sent = JSON.parse(text);
      environment.status = sent.status;
      api.deployed.push({ release: held.id, environment: environment.name, comment: sent.comment });
      return [200, environment];
    }
    return [404, { message: 'not found' }];
  };
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => (text += chunk));
    request.on('end', () => {
      const url = new URL(request.url, 'http://stand-in');
      const basic = request.headers.authorization === `Basic ${Buffer.from(`:${TOKEN}`).toString('base64')}`;
      calls.push({ method: request.method, path: url.pathname, query: url.search, basic, body: text ? JSON.parse(text) : null });
      const say = (status, json) => { response.writeHead(status, { 'content-type': 'application/json' }); response.end(JSON.stringify(json)); };
      if (!basic) return say(401, { message: 'TF400813: not authorized' });
      if (api.status) return say(api.status, { message: 'the stand-in is not answering' });
      const key = `${request.method} ${url.pathname}`;
      reads.set(key, (reads.get(key) ?? 0) + 1);
      api.onRead?.(request.method, url.pathname, reads.get(key));
      return say(...answer(request, url, text));
    });
  });
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  after(() => server.close());
  api.address = `http://127.0.0.1:${server.address().port}`;
  return api;
}

/** A checkout whose configuration names origin, so the plugin reads where to ask with no git. */
export function checkout(origin) {
  const root = mkdtempSync(join(scratch, 'root-'));
  mkdirSync(join(root, '.git'));
  writeFileSync(join(root, '.git', 'config'), `[core]\n\tbare = false\n[remote "origin"]\n\turl = ${origin}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n`);
  return root;
}

export function signedIn(token) {
  const at = mkdtempSync(join(scratch, 'private-'));
  writeFileSync(join(at, 'credential.json'), `${JSON.stringify({ token, kept: '2026-11-11T09:00:00.000Z' })}\n`);
  return { DAORIS_PLUGIN_PRIVATE: at };
}

/** The plugin started as the driver starts it over `journal`, the kit's, and killed when the test ends, so a failure holds no run open. */
export function start(t, env, journal = null) {
  const data = mkdtempSync(join(scratch, 'data-'));
  if (journal) writeFileSync(join(data, 'work-journal.json'), `${JSON.stringify(journal)}\n`);
  const child = spawn(process.execPath, [join(folder, 'plugin.mjs')], {
    cwd: folder,
    windowsHide: true,
    env: { PATH: process.env.PATH, DAORIS_PLUGIN_DATA: data, ...env },
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  const closed = new Promise((done) => child.on('close', done));
  t.after(() => {
    child.kill();
    return closed;
  });
  let said = '';
  child.stderr.on('data', (chunk) => (said += chunk));
  const heard = [];
  const waiting = [];
  createInterface({ input: child.stdout }).on('line', (line) => {
    const frame = JSON.parse(line);
    heard.push(frame);
    for (const wait of [...waiting]) if (wait.test(frame)) { waiting.splice(waiting.indexOf(wait), 1); wait.done(frame); }
  });
  const next = (test) => new Promise((done) => {
    const found = heard.find(test);
    if (found) done(found); else waiting.push({ test, done });
  });
  let id = 0;
  const ask = async (method, params) => {
    const mine = ++id;
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: mine, method, params })}\n`);
    return next((frame) => frame.id === mine);
  };
  const stop = async () => {
    child.kill();
    await closed;
    return said;
  };
  return { ask, next, stop, said: () => said };
}

export const unitAt = (root, awaiting = [STEP], more = {}) => ({ repository: 'app', workspace: 'work', root, branch: 'daoris/work', tip, line: null,
  pullRequest: null, awaiting, ...more });

/** The plugin told `unit` and asked to perform `step` once under `key`, with `options`; its `work/step` for that attempt awaited. */
export async function performed(t, env, unit, { step = STEP, key = 'k-1', options, deadlineAt } = {}) {
  const plugin = start(t, env);
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
  await plugin.ask('hook/work/watch', { generation: 1, resync: false, units: [unit] });
  const perform = (invocation, idempotencyKey) => plugin.ask('hook/work/perform', {
    unit: 'app:daoris/work', step, stepVersion: 1, invocation, idempotencyKey, subjectRevision: tip, inputs: [],
    deadlineAt: deadlineAt ?? new Date(Date.now() + 60000).toISOString(), ...(options ? { options } : {}) });
  await perform('i-1', key);
  const done = await plugin.next((frame) => frame.method === 'work/step' && frame.params.invocation === 'i-1');
  return { plugin, perform, step: done };
}
