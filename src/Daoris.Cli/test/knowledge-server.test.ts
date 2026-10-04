import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { makeFixture, type Fixture } from './_fixture.ts';

// ORIENT1c: `tools/knowledge-server.mjs` is what `.mcp.json` starts. It runs a build of the MCP host the
// workspace keeps under `local/` and never builds at a session's start; a worktree reaches its main checkout's
// build; and with no build it says so in the protocol's own words, so a session reads why and works as before.
type Located = { served: string; folder: string; build: string; entry: string; digest: string; builtAt: string };
type Message = { jsonrpc: string; id?: number | string; method: string; params?: Record<string, unknown> };
type Reply = { jsonrpc: string; id: number | string; result?: Record<string, unknown>; error?: { code: number; message: string } };
// @ts-expect-error — untyped workspace tooling; the same seam dogfood.test.ts documents
const tool = await import('../../../tools/knowledge-server.mjs') as {
  SERVER: string;
  DRIVEN: string;
  mainCheckout: (checkout: string) => string;
  readBuild: (checkout: string) => Omit<Located, 'served'> | null;
  locateServer: (checkout: string) => Located | null;
  serverEnvironment: (env: Record<string, string | undefined>, located: Located | null) => { mode: 'driven' | 'workspace'; env: Record<string, string | undefined> };
  serverCommand: (mode: 'driven' | 'workspace', env: Record<string, string | undefined>, located: Located | null, platform?: string) =>
    { command: string; args: string[]; what: string } | null;
  absenceSentence: (checkout: string, mode: 'driven' | 'workspace') => string;
  absenceReply: (message: Message, sentence: string) => Reply | null;
  sourceDigest: (checkout: string) => string;
  buildPlan: (checkout: string, digest: string) => { needed: boolean; why: string };
  pruneBuilds: (builds: string, keep: number, remove?: (path: string) => void) => { removed: string[]; kept: { name: string; why: string }[] };
};

const toolPath = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', 'tools', 'knowledge-server.mjs');

/** A checkout with a finished build of the server under its `local/`, as `build` leaves one. */
function withBuild(fx: Fixture, at: string, name = '20261004T100000Z-aaaaaaaa', digest = 'd1'): string {
  const checkout = join(fx.root, at);
  fx.write(`${at}/${tool.SERVER}/builds/${name}/daoris-knowledge.dll`, 'not really an assembly');
  fx.write(`${at}/${tool.SERVER}/current.json`, `${JSON.stringify({
    build: `builds/${name}`, entry: 'daoris-knowledge.dll', digest, builtAt: '2026-10-04T10:00:00.000Z',
  }, null, 2)}\n`);
  return checkout;
}

/** A linked worktree of `main` as git writes one: a `.git` file naming its folder under the main `.git`. */
function worktree(main: string, name: string): string {
  mkdirSync(join(main, '.git', 'worktrees', name), { recursive: true });
  writeFileSync(join(main, '.git', 'worktrees', name, 'commondir'), '../..\n');
  const linked = join(main, '.claude', 'worktrees', name);
  mkdirSync(linked, { recursive: true });
  writeFileSync(join(linked, '.git'), `gitdir: ${join(main, '.git', 'worktrees', name)}\n`);
  return linked;
}

// ---------------------------------------------------------------------------------------------------
// Where the server is

test('a worktree names its main checkout, and a checkout of its own names itself', () => {
  const fx = makeFixture('knowledge-server-main');
  const main = join(fx.root, 'main');
  mkdirSync(join(main, '.git'), { recursive: true });
  const linked = worktree(main, 'agent-1');

  assert.equal(tool.mainCheckout(main), main);
  assert.equal(tool.mainCheckout(linked), main);
  assert.equal(tool.mainCheckout(join(fx.root, 'not-a-checkout')), join(fx.root, 'not-a-checkout'));
  fx.cleanup();
});

test('a worktree reaches its main checkout\'s build without building, and its own build first when it made one', () => {
  const fx = makeFixture('knowledge-server-locate');
  const main = withBuild(fx, 'main');
  mkdirSync(join(main, '.git'), { recursive: true });
  const linked = worktree(main, 'agent-1');

  const reached = tool.locateServer(linked);
  assert.equal(reached?.served, main);
  assert.equal(reached?.folder, join(main, tool.SERVER));
  assert.equal(reached?.entry, join(main, tool.SERVER, 'builds', '20261004T100000Z-aaaaaaaa', 'daoris-knowledge.dll'));

  withBuild(fx, 'main/.claude/worktrees/agent-1', '20261004T110000Z-bbbbbbbb');
  const own = tool.locateServer(linked);
  assert.equal(own?.served, linked);
  assert.match(own?.entry ?? '', /bbbbbbbb/);
  fx.cleanup();
});

test('a pointer to a build that is gone is no build', () => {
  const fx = makeFixture('knowledge-server-gone');
  const main = withBuild(fx, 'main');
  mkdirSync(join(main, '.git'), { recursive: true });
  writeFileSync(join(main, tool.SERVER, 'builds', '20261004T100000Z-aaaaaaaa', 'daoris-knowledge.dll'), '');
  assert.notEqual(tool.readBuild(main), null);

  const fresh = makeFixture('knowledge-server-none');
  mkdirSync(join(fresh.root, '.git'), { recursive: true });
  fresh.write(`${tool.SERVER}/current.json`, JSON.stringify({ build: 'builds/x', entry: 'daoris-knowledge.dll', digest: 'd' }));
  assert.equal(tool.readBuild(fresh.root), null);
  assert.equal(tool.locateServer(fresh.root), null);
  fx.cleanup();
  fresh.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// What it runs, and over which store

const ACCOUNT = {
  PATH: '/usr/bin',
  DAORIS_HOME: '/install/data',
  DAORIS_KNOWLEDGE_DB: '/install/data/knowledge.db',
  DAORIS_REMOTE_URL: 'https://team.example',
  DAORIS_REMOTE_KEY: 'secret',
  DAORIS_RULES_HOME: '/install/data',
  DAORIS_EMBED_MODEL: 'some-model',
};

test('a session started by hand gets the workspace\'s own store and reads the checkout, never the machine\'s', () => {
  const located = { served: '/work/main', folder: '/work/main/local/knowledge-server', build: 'builds/x', entry: '/work/main/local/knowledge-server/builds/x/daoris-knowledge.dll', digest: 'd', builtAt: 'then' };

  const { mode, env } = tool.serverEnvironment(ACCOUNT, located);

  assert.equal(mode, 'workspace');
  assert.equal(env['DAORIS_HOME'], join('/work/main/local/knowledge-server', 'home'));
  assert.equal(env['DAORIS_KNOWLEDGE_REPOSITORY'], '/work/main');
  assert.equal(env['DAORIS_KNOWLEDGE_DOCUMENTS'], 'docs');
  assert.equal(env['DAORIS_KNOWLEDGE_INDEX'], 'docs/index');
  // The machine's store, its remotes, its rules and its model are the install's, not this build's to open.
  for (const name of ['DAORIS_KNOWLEDGE_DB', 'DAORIS_REMOTE_URL', 'DAORIS_REMOTE_KEY', 'DAORIS_RULES_HOME', 'DAORIS_EMBED_MODEL']) {
    assert.equal(env[name], undefined, name);
  }
  assert.equal(env['PATH'], '/usr/bin');
});

test('a session a driver started keeps the environment it was handed, the driver\'s store with it', () => {
  const driven = { ...ACCOUNT, [tool.DRIVEN]: 'Daoris' };

  const { mode, env } = tool.serverEnvironment(driven, null);

  assert.equal(mode, 'driven');
  assert.deepEqual(env, driven);
});

test('a workspace session runs the workspace\'s build; a driven one the machine\'s host first, as the driver hands it', () => {
  const fx = makeFixture('knowledge-server-command');
  const main = withBuild(fx, 'main');
  mkdirSync(join(main, '.git'), { recursive: true });
  const located = tool.locateServer(main);

  const workspace = tool.serverCommand('workspace', {}, located);
  assert.deepEqual(workspace && [workspace.command, workspace.args], ['dotnet', [located?.entry]]);

  // Driven: an explicit host, then the home's bin/, then the workspace's build.
  const explicit = fx.write('elsewhere/daoris-knowledge.exe', '');
  const home = join(fx.root, 'data');
  fx.write('data/bin/daoris-knowledge.exe', '');
  assert.equal(tool.serverCommand('driven', { DAORIS_MCP_HOST: explicit, DAORIS_HOME: home }, located, 'win32')?.command, explicit);
  assert.equal(tool.serverCommand('driven', { DAORIS_HOME: home }, located, 'win32')?.command, join(home, 'bin', 'daoris-knowledge.exe'));
  assert.equal(tool.serverCommand('driven', { DAORIS_HOME: join(fx.root, 'empty') }, located, 'win32')?.command, 'dotnet');
  assert.equal(tool.serverCommand('driven', {}, null, 'win32'), null);
  assert.equal(tool.serverCommand('workspace', {}, null), null);
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// No build: said in the protocol, and the session works as before

test('with no build, the server answers the handshake with why, lists no tool, and refuses the rest', () => {
  const sentence = tool.absenceSentence('/work/main', 'workspace');
  assert.match(sentence, /not built/);
  assert.match(sentence, /npm run knowledge:build/);

  const hello = tool.absenceReply({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18' } }, sentence);
  assert.equal(hello?.result?.['protocolVersion'], '2025-06-18');
  assert.equal(hello?.result?.['instructions'], sentence);
  assert.deepEqual((hello?.result?.['serverInfo'] as { name: string }).name, 'daoris-knowledge');
  assert.deepEqual(tool.absenceReply({ jsonrpc: '2.0', id: 2, method: 'tools/list' }, sentence)?.result, { tools: [] });
  assert.deepEqual(tool.absenceReply({ jsonrpc: '2.0', id: 3, method: 'ping' }, sentence)?.result, {});
  assert.equal(tool.absenceReply({ jsonrpc: '2.0', method: 'notifications/initialized' }, sentence), null);
  const refused = tool.absenceReply({ jsonrpc: '2.0', id: 4, method: 'tools/call', params: { name: 'knowledge_search' } }, sentence);
  assert.equal(refused?.error?.code, -32601);
  assert.equal(refused?.error?.message, sentence);
});

test('started with no build anywhere, it speaks the protocol on its standard streams and ends with its input', async () => {
  const fx = makeFixture('knowledge-server-absent');
  // A copy of the tool in a checkout of its own, so the build it looks for is this fixture's, not the workspace's.
  fx.write('tools/knowledge-server.mjs', readFileSync(toolPath, 'utf8'));
  fx.write('tools/fsx.mjs', readFileSync(join(dirname(toolPath), 'fsx.mjs'), 'utf8'));
  mkdirSync(join(fx.root, '.git'), { recursive: true });

  const env = { ...process.env };
  delete env[tool.DRIVEN];
  const child = spawn(process.execPath, [join(fx.root, 'tools', 'knowledge-server.mjs')], { cwd: fx.root, env });
  let out = '';
  let err = '';
  child.stdout.on('data', (chunk) => { out += String(chunk); });
  child.stderr.on('data', (chunk) => { err += String(chunk); });
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'initialize', params: { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'test', version: '0' } } })}\n`);
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' })}\n`);
  child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: 2, method: 'tools/list' })}\n`);
  child.stdin.end();
  const status = await new Promise((done) => child.on('close', done));

  assert.equal(status, 0, err);
  const replies = out.trim().split('\n').map((line) => JSON.parse(line) as Reply);
  assert.deepEqual(replies.map((reply) => reply.id), [1, 2], 'one reply a request, none for the notification');
  assert.match(String(replies[0]?.result?.['instructions']), /not built/);
  assert.deepEqual(replies[1]?.result, { tools: [] });
  assert.match(err, /not built/, 'and the same sentence on standard error, for the person reading the server log');
  assert.equal(existsSync(join(fx.root, tool.SERVER)), false, 'nothing was built at the start');
  fx.cleanup();
});

// ---------------------------------------------------------------------------------------------------
// The build: only when the host's sources changed, and never by removing a build in use

function serviceTree(fx: Fixture): string {
  fx.write('src/Daoris.Service/Directory.Build.props', '<Project />\n');
  fx.write('src/Daoris.Service/Daoris.Service.Core/Thing.cs', 'class Thing {}\n');
  fx.write('src/Daoris.Service/Daoris.Service.Mcp/Program.cs', 'return 0;\n');
  fx.write('src/Daoris.Service/Daoris.Service.Mcp/Daoris.Service.Mcp.csproj', '<Project />\n');
  fx.write('src/Daoris.Service/Daoris.Service.Tests/ThingTests.cs', 'class ThingTests {}\n');
  fx.write('src/Daoris.Service/Daoris.Service.Http/Program.cs', 'return 0;\n');
  fx.write('src/Daoris.Service/Daoris.Service.Core/obj/generated.cs', 'one\n');
  return fx.root;
}

test('the digest moves with the host\'s sources, and not with its tests, the other host or a build\'s output', () => {
  const fx = makeFixture('knowledge-server-digest');
  const checkout = serviceTree(fx);
  const first = tool.sourceDigest(checkout);

  fx.write('src/Daoris.Service/Daoris.Service.Tests/ThingTests.cs', 'class ThingTests { int x; }\n');
  fx.write('src/Daoris.Service/Daoris.Service.Http/Program.cs', 'return 1;\n');
  fx.write('src/Daoris.Service/Daoris.Service.Core/obj/generated.cs', 'two\n');
  assert.equal(tool.sourceDigest(checkout), first);

  fx.write('src/Daoris.Service/Daoris.Service.Core/Thing.cs', 'class Thing { int y; }\n');
  assert.notEqual(tool.sourceDigest(checkout), first);
  fx.cleanup();
});

test('a build is owed with none, and when the sources moved; not when the last one was built from them', () => {
  const fx = makeFixture('knowledge-server-plan');
  const checkout = withBuild(fx, 'main', undefined, 'same');

  assert.deepEqual(tool.buildPlan(checkout, 'same').needed, false);
  assert.match(tool.buildPlan(checkout, 'moved').why, /changed/);
  assert.equal(tool.buildPlan(checkout, 'moved').needed, true);
  assert.equal(tool.buildPlan(join(fx.root, 'empty'), 'any').needed, true);
  fx.cleanup();
});

test('old builds go, the newest stay, and one a running session holds is kept and said', () => {
  const fx = makeFixture('knowledge-server-prune');
  for (const name of ['20261001T000000Z-a', '20261002T000000Z-b', '20261003T000000Z-c', '20261004T000000Z-d', '20261004T120000Z-e']) {
    fx.write(`builds/${name}/daoris-knowledge.dll`, '');
  }
  const removed: string[] = [];
  const pruned = tool.pruneBuilds(join(fx.root, 'builds'), 3, (path) => {
    if (path.endsWith('-a')) throw Object.assign(new Error('EBUSY: resource busy or locked'), { code: 'EBUSY' });
    removed.push(path);
  });

  assert.deepEqual(pruned.removed, ['20261002T000000Z-b']);
  assert.deepEqual(removed, [join(fx.root, 'builds', '20261002T000000Z-b')]);
  assert.deepEqual(pruned.kept, [{ name: '20261001T000000Z-a', why: 'in use' }]);
  fx.cleanup();
});
