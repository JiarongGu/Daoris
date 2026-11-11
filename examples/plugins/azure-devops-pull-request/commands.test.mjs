// The plugin's own sign-in, through the commands it declares: `login` asks for a personal access token, hidden, and keeps it in the
// private storage Daoris hands it; `status` says it is kept, and that Azure DevOps refused it once the API did; `logout` forgets it.
// The plugin's process, started as Daoris starts it, against the stand-in; nothing reaches a network.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { POINTS, TOKEN, WEB, checkout, signedIn, standIn, start } from './stand-in.mjs';

async function begun(t, env) {
  const plugin = start(t, env);
  await plugin.ask('initialize', { protocolVersion: 1, points: POINTS });
  return plugin;
}

const run = async (plugin, command, answers = {}) =>
  (await plugin.ask('hook/command/run', { command, args: [], answers, from: 'terminal' })).result;

test('login asks for the token hidden, keeps it in its own storage, and status says it is kept', async (t) => {
  const storage = signedIn('');
  const plugin = await begun(t, storage);

  const asked = await run(plugin, 'login');
  assert.deepEqual(asked.ask, { name: 'token', prompt: 'Personal access token', masked: true });
  assert.equal((await run(plugin, 'status')).exit, 1);

  const kept = await run(plugin, 'login', { token: TOKEN });
  assert.equal(kept.exit, 0);
  assert.ok(!JSON.stringify(kept).includes(TOKEN), 'the token is never said back');
  assert.equal(JSON.parse(readFileSync(join(storage.DAORIS_PLUGIN_PRIVATE, 'credential.json'), 'utf8')).token, TOKEN);
  assert.match((await run(plugin, 'status')).said[0], /^A personal access token is kept, since /);

  assert.equal((await run(plugin, 'logout')).exit, 0);
  assert.equal(existsSync(join(storage.DAORIS_PLUGIN_PRIVATE, 'credential.json')), false);
});

test('a token the API refuses is said refused by status, and a plugin handed no storage says so', async (t) => {
  const api = await standIn();
  const root = checkout('https://dev.azure.com/acme/Proj/_git/app');
  const plugin = await begun(t, { DAORIS_HOST_API: api.address, ...signedIn('not-the-token') });
  await plugin.ask('hook/work/state', { root, branch: 'daoris/work', pullRequest: `${WEB}/pullrequest/1`, origin: null, line: 'main' });
  assert.match((await run(plugin, 'status')).said[0], /^Azure DevOps refused the kept token at /);

  const bare = await begun(t, {});
  assert.match((await run(bare, 'login')).said[0], /no private storage/);
});
