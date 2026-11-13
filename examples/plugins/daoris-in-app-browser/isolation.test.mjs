import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fakeBrowser, fakeContext, fakePage } from './fakes.mjs';
import { chooseContext, isIsolated, readIsolation, openContext } from './isolation.mjs';

const scratch = fileURLToPath(new URL('../../../local/scratch/', import.meta.url));

function settingFolder(text) {
  mkdirSync(scratch, { recursive: true });
  const folder = mkdtempSync(join(scratch, 'isolation-'));
  if (text !== undefined) writeFileSync(join(folder, 'isolation.json'), text);
  return folder;
}

test('nothing set means shared', () => {
  assert.deepEqual(readIsolation(settingFolder()), []);
  assert.equal(isIsolated([], { client: 'codex-mcp-client', role: 'builder' }), false);
});

test('a setting that cannot be read means shared too', () => {
  assert.deepEqual(readIsolation(settingFolder('{ nope')), []);
  assert.deepEqual(readIsolation(settingFolder('{"isolated": "codex"}')), []);
});

test('an agent named in the setting is isolated, by its client name or its role', () => {
  const setting = readIsolation(settingFolder('{"isolated": ["Codex", "role:reviewer"]}'));
  assert.equal(isIsolated(setting, { client: 'codex-mcp-client', role: undefined }), true);
  assert.equal(isIsolated(setting, { client: 'claude-code', role: 'reviewer' }), true);
  assert.equal(isIsolated(setting, { client: 'claude-code', role: 'builder' }), false);
  assert.equal(isIsolated(setting, { client: undefined, role: undefined }), false);
});

test('"*" isolates every agent', () => {
  assert.equal(isIsolated(['*'], { client: 'claude-code' }), true);
});

test("a shared session drives the person's own context", async () => {
  const person = fakeContext();
  const browser = fakeBrowser(person);
  assert.equal(await openContext(browser, { isolated: false }), person);
  assert.equal(browser.made.length, 0);
});

test("an isolated agent gets a new context holding the person's cookies and the local storage of the origins open", async () => {
  const cookies = [{ name: 'who', value: 'person', domain: 'site.test', path: '/' }];
  const person = fakeContext({
    cookies,
    pages: [
      fakePage('https://site.test/board', { token: 'abc', theme: 'dark' }),
      fakePage('https://site.test/other', { token: 'abc' }),
      fakePage('https://elsewhere.test/', { k: 'v' }),
      fakePage('about:blank', { ignored: '1' }),
    ],
  });
  const browser = fakeBrowser(person);

  const own = await openContext(browser, { isolated: true });

  assert.notEqual(own, person);
  assert.equal(browser.made.length, 1);
  assert.deepEqual(own.options.storageState.cookies, cookies);
  assert.deepEqual(own.options.storageState.origins, [
    { origin: 'https://site.test', localStorage: [{ name: 'token', value: 'abc' }, { name: 'theme', value: 'dark' }] },
    { origin: 'https://elsewhere.test', localStorage: [{ name: 'k', value: 'v' }] },
  ]);
});

test('nothing set: the session drives the person\'s context through its own pages, and ends only those', async () => {
  const person = fakeContext({ pages: [fakePage('https://site.test/')] });
  const browser = fakeBrowser(person);

  const { context, isolated, end } = await chooseContext(browser, settingFolder(), { client: 'claude-code' });
  const page = await context.newPage();

  assert.equal(isolated, false);
  assert.equal(browser.made.length, 0);
  assert.deepEqual(context.pages(), [page]);
  assert.equal(person.pages().length, 2);
  await end();
  assert.equal(page.closed, true);
  assert.equal(person.pages().filter(p => p.closed).length, 1);
});

test('an agent set isolated: its own context, closed whole at the end, the person\'s left alone', async () => {
  const person = fakeContext();
  const browser = fakeBrowser(person);
  const folder = settingFolder('{"isolated": ["codex"]}');

  const shared = await chooseContext(browser, folder, { client: 'claude-code' });
  const isolated = await chooseContext(browser, folder, { client: 'codex-mcp-client' });

  assert.equal(shared.isolated, false);
  assert.equal(isolated.isolated, true);
  assert.equal(browser.made.length, 1);
  await isolated.end();
  assert.equal(browser.made[0].closed, true);
  assert.equal(person.closed, false);
});

test("an origin the person's page will not read is left out, not fatal", async () => {
  const blocked = fakePage('https://blocked.test/');
  blocked.evaluate = async () => { throw new Error('denied'); };
  const person = fakeContext({ pages: [blocked, fakePage('https://ok.test/', { a: '1' })] });
  const own = await openContext(fakeBrowser(person), { isolated: true });
  assert.deepEqual(own.options.storageState.origins.map(o => o.origin), ['https://ok.test']);
});
