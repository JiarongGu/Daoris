// The server against a real Chromium-family browser launched here, under local/scratch: never Daoris's own browser.
// Run it with DAORIS_REAL_BROWSER=<path of a Chromium, Chrome or Edge executable>; without it the test is skipped.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const executable = process.env.DAORIS_REAL_BROWSER;
const here = fileURLToPath(new URL('.', import.meta.url));
const scratch = join(fileURLToPath(new URL('../../../local/scratch/', import.meta.url)), 'browser-share-real');

const page = `<!doctype html><body><script>
const set = new URLSearchParams(location.search).get('set');
if (set) localStorage.setItem(set.split('=')[0], set.split('=')[1]);
document.title = 'storage:' + JSON.stringify(Object.fromEntries(Object.entries(localStorage)));
</script>`;

function listen() {
  return new Promise(resolve => {
    const server = createServer((request, response) => { response.setHeader('content-type', 'text/html'); response.end(page); });
    server.listen(0, '127.0.0.1', () => resolve(server));
  });
}

async function targets(port) {
  return (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).filter(t => t.type === 'page');
}

async function until(check, what) {
  for (let i = 0; i < 100; i++) {
    const value = await check();
    if (value) return value;
    await new Promise(r => setTimeout(r, 100));
  }
  throw new Error(`timed out: ${what}`);
}

/** One session's server, spoken to over stdio as a harness does. */
function session(endpoint, data, client) {
  const child = spawn(process.execPath, [join(here, 'browser-server.mjs'), '--cdp-endpoint', endpoint, '--data', data, '--output-dir', join(scratch, 'output')], { stdio: ['pipe', 'pipe', 'inherit'] });
  const waiting = new Map();
  let buffer = '';
  child.stdout.on('data', chunk => {
    buffer += chunk;
    let at;
    while ((at = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, at);
      buffer = buffer.slice(at + 1);
      if (!line.trim()) continue;
      const message = JSON.parse(line);
      waiting.get(message.id)?.(message);
    }
  });
  let id = 0;
  const call = (method, params) => new Promise(resolve => {
    const mine = ++id;
    waiting.set(mine, resolve);
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id: mine, method, params })}\n`);
  });
  return {
    child,
    async start() {
      await call('initialize', { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: client, version: '1' } });
      child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'notifications/initialized' })}\n`);
    },
    async tool(name, args = {}) {
      const answer = await call('tools/call', { name, arguments: args });
      return answer.result?.content?.map(c => c.text).join('\n') ?? JSON.stringify(answer);
    },
    end: () => new Promise(resolve => { child.once('exit', resolve); child.stdin.end(); }),
  };
}

test('sessions share the person\'s context through tabs of their own, and an isolated agent gets a context of its own', { skip: !executable, timeout: 240_000 }, async () => {
  rmSync(join(scratch, 'profile'), { recursive: true, force: true });
  mkdirSync(join(scratch, 'data'), { recursive: true });
  const data = join(scratch, 'data');
  writeFileSync(join(data, 'isolation.json'), '{"isolated": ["codex"]}');
  const site = await listen();
  const origin = `http://127.0.0.1:${site.address().port}`;
  const port = 19000 + Math.floor(Math.random() * 3000);
  const browser = spawn(executable, [`--remote-debugging-port=${port}`, `--user-data-dir=${join(scratch, 'profile')}`, '--headless=new', '--no-first-run', '--disable-gpu', 'about:blank'], { stdio: 'ignore' });
  const sessions = [];
  try {
    await until(async () => fetch(`http://127.0.0.1:${port}/json/version`).then(r => r.ok, () => false), 'the browser listens');
    await fetch(`http://127.0.0.1:${port}/json/new?${origin}/?set=who=person`, { method: 'PUT' });
    await until(async () => (await targets(port)).some(t => t.url.includes('set=who')), "the person's tab");
    const personTab = (await targets(port)).find(t => t.url.includes('set=who'));
    const endpoint = `http://127.0.0.1:${port}`;

    const first = session(endpoint, data, 'claude-code');
    const second = session(endpoint, data, 'claude-code');
    const isolated = session(endpoint, data, 'codex-mcp-client');
    sessions.push(first, second, isolated);
    await first.start();
    await second.start();
    await isolated.start();

    // Shared: the person's storage is there, and a tab of its own is the session's whole tab list.
    assert.match(await first.tool('browser_navigate', { url: `${origin}/?set=a=1` }), /storage:\{[^}]*"who":"person"/);
    assert.match(await first.tool('browser_tabs', { action: 'list' }), /0:.*\(current\)/s);
    assert.doesNotMatch(await first.tool('browser_tabs', { action: 'list' }), /^- 1:/m);
    assert.match(await second.tool('browser_navigate', { url: `${origin}/?set=b=2` }), /"who":"person"/);
    assert.doesNotMatch(await second.tool('browser_tabs', { action: 'list' }), /^- 1:/m,"the second session never adopts the first's tab");

    // Isolated: starts with the storage of the origin the person has open, and from then on what each writes stays apart.
    const own = await isolated.tool('browser_navigate', { url: `${origin}/?set=c=3` });
    assert.match(own, /"who":"person"/);
    assert.match(await first.tool('browser_navigate', { url: `${origin}/` }), /"b":"2"/, 'shared sessions see each other');
    assert.doesNotMatch(await first.tool('browser_navigate', { url: `${origin}/` }), /"c":"3"/, 'and never the isolated one');
    await first.tool('browser_navigate', { url: `${origin}/?set=d=4` });
    assert.doesNotMatch(await isolated.tool('browser_navigate', { url: `${origin}/` }), /"d":"4"/, 'what a shared session writes later never reaches the isolated one');

    // The person's tab stays on its page, and ending a session closes only that session's tabs.
    assert.equal((await targets(port)).find(t => t.id === personTab.id)?.url, personTab.url);
    const before = (await targets(port)).length;
    await first.end();
    await second.end();
    await isolated.end();
    sessions.length = 0;
    await until(async () => (await targets(port)).length < before, 'the sessions\' tabs close');
    assert.equal((await targets(port)).filter(t => t.url.includes('set=who')).length, 1, "the person's tab stays open");
  } finally {
    for (const open of sessions) open.child.kill();
    browser.kill();
    site.close();
  }
});
