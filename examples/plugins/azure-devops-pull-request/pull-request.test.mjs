// The pull-request step and the state query against a stand-in of Azure DevOps' REST API on this machine's loopback: open, reuse, state with
// the merge commit, a refused token, no token, an earlier attempt asked of a platform that is down, and that the plugin runs no git, gh or
// az. `node --test` runs it; nothing of Azure DevOps' or Daoris's is reached.
import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const folder = dirname(fileURLToPath(import.meta.url));
const TOKEN = 'throwaway-token-for-the-stand-in';
const scratch = mkdtempSync(join(tmpdir(), 'azure-pr-'));
after(() => rmSync(scratch, { recursive: true, force: true }));

const WEB = 'https://dev.azure.com/acme/Proj/_git/app';

/**
 * Azure DevOps' pull-request calls as its REST API answers them, admitting one token as Basic's password and counting what it is asked.
 * `status`, while set, is what every call is answered with instead: Azure DevOps down or limiting the token.
 */
async function standIn() {
  const api = { pulls: [], calls: [], address: '', status: null };
  const { pulls, calls } = api;
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => (text += chunk));
    request.on('end', () => {
      const url = new URL(request.url, 'http://stand-in');
      const basic = request.headers.authorization === `Basic ${Buffer.from(`:${TOKEN}`).toString('base64')}`;
      calls.push({ method: request.method, path: url.pathname, basic, body: text ? JSON.parse(text) : null });
      const say = (status, json) => { response.writeHead(status, { 'content-type': 'application/json' }); response.end(JSON.stringify(json)); };
      if (!basic) return say(401, { message: 'TF400813: not authorized' });
      if (api.status) return say(api.status, { message: 'the stand-in is not answering' });
      const root = '/acme/Proj/_apis/git/repositories/app';
      if (request.method === 'GET' && url.pathname === root) return say(200, { defaultBranch: 'refs/heads/main' });
      if (url.pathname === `${root}/pullrequests` && request.method === 'GET') {
        const source = url.searchParams.get('searchCriteria.sourceRefName');
        return say(200, { value: pulls.filter((pr) => pr.sourceRefName === source) });
      }
      if (url.pathname === `${root}/pullrequests` && request.method === 'POST') {
        const sent = JSON.parse(text);
        const made = { pullRequestId: pulls.length + 1, status: 'active', repository: { webUrl: WEB }, sourceRefName: sent.sourceRefName,
          targetRefName: sent.targetRefName, title: sent.title };
        pulls.push(made);
        return say(201, made);
      }
      const one = new RegExp(`^${root}/pullrequests/(\\d+)$`).exec(url.pathname);
      if (one && pulls[Number(one[1]) - 1]) return say(200, pulls[Number(one[1]) - 1]);
      return say(404, { message: 'not found' });
    });
  });
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  after(() => server.close());
  api.address = `http://127.0.0.1:${server.address().port}`;
  return api;
}

/** A checkout whose configuration names origin, so the plugin reads where to ask with no git. */
function checkout(origin) {
  const root = mkdtempSync(join(scratch, 'root-'));
  mkdirSync(join(root, '.git'));
  writeFileSync(join(root, '.git', 'config'), `[core]\n\tbare = false\n[remote "origin"]\n\turl = ${origin}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n`);
  return root;
}

/**
 * The plugin started as the driver starts it, with the frames it is sent and what it says on stderr; `journal`, where given, is the kit's
 * journal it finds in its data. Killed when the test ends, so a failed assertion leaves no plugin holding the run open.
 */
function start(t, env, journal = null) {
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
  return { ask, next, stop };
}

const POINTS = ['work/watch', 'work/step', 'work/perform', 'work/state'];
const STEP = 'azure-devops-pull-request/pull-request';
const tip = 'b'.repeat(40);
const unitAt = (root) => ({ repository: 'app', workspace: 'work', root, branch: 'daoris/work', tip, line: null, pullRequest: null, awaiting: [STEP] });

async function opened(t, env, root, key = 'k-1') {
  const plugin = start(t, env);
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
  await plugin.ask('hook/work/watch', { generation: 1, resync: false, units: [unitAt(root)] });
  const perform = (invocation, idempotencyKey) => plugin.ask('hook/work/perform', {
    unit: 'app:daoris/work', step: STEP, stepVersion: 1, invocation, idempotencyKey, subjectRevision: tip, inputs: [],
    deadlineAt: new Date(Date.now() + 60000).toISOString() });
  await perform('i-1', key);
  const step = await plugin.next((frame) => frame.method === 'work/step' && frame.params.invocation === 'i-1');
  return { plugin, perform, step };
}

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
  const source = readFileSync(join(folder, 'plugin.mjs'), 'utf8');
  const manifest = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8'));

  assert.equal(/child_process|spawn|execFile|execSync/.test(source), false);
  assert.deepEqual(manifest.tools.map((tool) => tool.id), ['node']);
});
