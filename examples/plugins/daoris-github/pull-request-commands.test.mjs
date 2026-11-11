// The pull request's own commands, marked as tools for an agent performing a pull-request step: one opens the pull request from a
// pushed branch (finding the open one the second time), one says how it stands with its address as data, one merges it.
// Each round is the plugin's own process, started as Daoris starts it, against a stand-in; nothing reaches a network.
import { test, after } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const folder = dirname(fileURLToPath(import.meta.url));
const TOKEN = 'throwaway-token-for-the-stand-in';
const ORIGIN = 'https://github.com/acme/app.git';
const scratch = mkdtempSync(join(tmpdir(), 'github-commands-'));
after(() => rmSync(scratch, { recursive: true, force: true }));

/** GitHub's pull-request calls as the REST API answers them, for one token. */
async function standIn() {
  const pulls = [];
  const server = createServer((request, response) => {
    let text = '';
    request.on('data', (chunk) => (text += chunk));
    request.on('end', () => {
      const url = new URL(request.url, 'http://stand-in');
      const say = (status, json) => { response.writeHead(status, { 'content-type': 'application/json' }); response.end(JSON.stringify(json)); };
      if (request.headers.authorization !== `Bearer ${TOKEN}`) return say(401, { message: 'Bad credentials' });
      if (request.method === 'GET' && url.pathname === '/repos/acme/app') return say(200, { default_branch: 'main' });
      if (url.pathname === '/repos/acme/app/pulls' && request.method === 'GET') return say(200, pulls.filter((pr) => pr.head.ref === url.searchParams.get('head')?.split(':')[1]));
      if (url.pathname === '/repos/acme/app/pulls' && request.method === 'POST') {
        const sent = JSON.parse(text);
        const number = pulls.length + 1;
        pulls.push({ number, state: 'open', merged_at: null, html_url: `https://github.com/acme/app/pull/${number}`, head: { ref: sent.head, sha: 'a'.repeat(40) }, base: { ref: sent.base }, merge_commit_sha: null, title: sent.title });
        return say(201, pulls[number - 1]);
      }
      const one = /^\/repos\/acme\/app\/pulls\/(\d+)(\/merge)?$/.exec(url.pathname);
      const pr = one ? pulls[Number(one[1]) - 1] : null;
      if (pr && !one[2] && request.method === 'GET') return say(200, pr);
      if (pr && one[2] && request.method === 'PUT') {
        Object.assign(pr, { state: 'closed', merged_at: '2026-11-12T09:00:00Z', merge_commit_sha: 'c'.repeat(40), method: JSON.parse(text).merge_method });
        return say(200, { merged: true, sha: pr.merge_commit_sha });
      }
      return say(404, { message: 'Not Found' });
    });
  });
  await new Promise((done) => server.listen(0, '127.0.0.1', done));
  after(() => server.close());
  return { pulls, address: `http://127.0.0.1:${server.address().port}` };
}

/** One round in a process of its own, as Daoris runs a command: the handshake, the round, its answer. */
async function round(env, command, args) {
  const child = spawn(process.execPath, [join(folder, 'plugin.mjs')], { cwd: folder, windowsHide: true, env: { PATH: process.env.PATH, ...env }, stdio: ['pipe', 'pipe', 'pipe'] });
  const lines = createInterface({ input: child.stdout })[Symbol.asyncIterator]();
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: 1, points: [] } })}\n`);
  await lines.next();
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: 2, method: 'hook/command/run', params: { command, run: 'a'.repeat(32), args, answers: {}, from: 'agent', place: 'machine', language: 'en' } })}\n`);
  const answer = JSON.parse((await lines.next()).value).result;
  child.kill();
  return answer;
}

function signedIn() {
  const at = mkdtempSync(join(scratch, 'private-'));
  writeFileSync(join(at, 'credential.json'), `${JSON.stringify({ token: TOKEN, kept: '2026-11-11T09:00:00.000Z' })}\n`);
  return { DAORIS_PLUGIN_PRIVATE: at, DAORIS_PLUGIN_DATA: mkdtempSync(join(scratch, 'data-')) };
}

test('the manifest marks the pull request commands as tools, the two that change something as writes', () => {
  const commands = JSON.parse(readFileSync(join(folder, 'plugin.json'), 'utf8')).interfaces.commands;
  const of = (name) => commands.find((each) => each.name === name);
  for (const name of ['create-pull-request', 'pull-request-status', 'complete-pull-request']) {
    assert.equal(of(name)?.tool, true, `${name} is a tool`);
    assert.ok(of(name).args.some((arg) => arg.name === 'origin' && arg.required), `${name} takes the origin`);
  }
  assert.equal(of('create-pull-request').writes, true);
  assert.equal(of('complete-pull-request').writes, true);
  assert.notEqual(of('pull-request-status').writes, true);
});

test('an agent opens the pull request, reads it open, merges it and reads it completed', async () => {
  const api = await standIn();
  const env = { DAORIS_HOST_API: api.address, ...signedIn() };

  const opened = await round(env, 'create-pull-request', { origin: ORIGIN, branch: 'daoris/s-1', title: 'Fix the ledger' });
  assert.equal(opened.exit, 0);
  assert.deepEqual([opened.data.id, opened.data.target, opened.data.url], [1, 'main', 'https://github.com/acme/app/pull/1']);
  assert.equal((await round(env, 'create-pull-request', { origin: ORIGIN, branch: 'daoris/s-1' })).data.id, 1, 'the open one is found again');
  assert.equal(api.pulls.length, 1);
  assert.equal((await round(env, 'pull-request-status', { origin: ORIGIN, 'pull-request': opened.data.url })).data.state, 'open');

  const merged = await round(env, 'complete-pull-request', { origin: ORIGIN, 'pull-request': '1', strategy: 'squash' });
  assert.equal(merged.exit, 0);
  assert.equal(api.pulls[0].method, 'squash');
  const read = await round(env, 'pull-request-status', { origin: ORIGIN, branch: 'daoris/s-1' });
  assert.deepEqual([read.data.state, read.data.mergeCommit], ['completed', 'c'.repeat(40)]);
});

test('an origin that names no GitHub repository is said, exit 1', async () => {
  const refused = await round(signedIn(), 'pull-request-status', { origin: 'C:/work/app', branch: 'x' });

  assert.equal(refused.exit, 1);
  assert.match(refused.said[0], /names no GitHub repository/);
});
