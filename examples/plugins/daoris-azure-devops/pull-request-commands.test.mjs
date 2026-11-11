// The pull request's own commands, marked as tools for an agent performing a pull-request step: one opens the pull request from a
// pushed branch (finding the active one the second time), one says how it stands with its address as data, one completes it.
// The plugin's process, started as Daoris starts it, against the stand-in; nothing reaches a network.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, TOKEN, WEB, folder, signedIn, standIn, start } from './stand-in.mjs';

const run = async (plugin, command, args) =>
  (await plugin.ask('hook/command/run', { command, run: 'a'.repeat(32), args, answers: {}, from: 'agent', place: 'machine', language: 'en' })).result;

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

test('an agent opens the pull request, reads it open, completes it and reads it completed', async (t) => {
  const api = await standIn();
  const plugin = start(t, { DAORIS_HOST_API: api.address, ...signedIn(TOKEN) });
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });

  const opened = await run(plugin, 'create-pull-request', { origin: WEB, branch: 'daoris/s-1', title: 'Fix the ledger' });
  assert.equal(opened.exit, 0);
  assert.equal(opened.data.id, 1);
  assert.equal(opened.data.target, 'main');
  assert.match(opened.data.url, /\/pullrequest\/1$/);
  assert.equal(api.pulls[0].title, 'Fix the ledger');
  assert.equal((await run(plugin, 'create-pull-request', { origin: WEB, branch: 'daoris/s-1' })).data.id, 1, 'the active one is found again');
  assert.equal(api.pulls.length, 1);

  const open = await run(plugin, 'pull-request-status', { origin: WEB, 'pull-request': opened.data.url });
  assert.equal(open.data.state, 'open');

  const completed = await run(plugin, 'complete-pull-request', { origin: WEB, 'pull-request': '1', strategy: 'squash' });
  assert.equal(completed.exit, 0);
  assert.equal(api.pulls[0].status, 'completed');
  assert.equal(api.pulls[0].completionOptions.mergeStrategy, 'squash');
  assert.equal((await run(plugin, 'pull-request-status', { origin: WEB, branch: 'daoris/s-1' })).data.state, 'completed');
});

test('an origin that names no Azure DevOps repository is said, exit 1', async (t) => {
  const plugin = start(t, signedIn(TOKEN));
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });

  const refused = await run(plugin, 'pull-request-status', { origin: 'https://example.com/app.git', branch: 'x' });

  assert.equal(refused.exit, 1);
  assert.match(refused.said[0], /names no Azure DevOps repository/);
});
