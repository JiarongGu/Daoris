import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const here = new URL('.', import.meta.url);

/** A Playwright context as the init page reads one: its cookies, and what is added to it. */
function context(cookies, browser) {
  const added = [];
  return { added, browser: () => browser, cookies: async () => cookies, addCookies: async more => { added.push(...more); } };
}

function browserOf(person, ...sessions) {
  const browser = { contexts: () => [person, ...sessions] };
  return browser;
}

test('each session is handed a browser context of its own, its first tab given the sign-in', () => {
  const manifest = JSON.parse(readFileSync(new URL('plugin.json', here), 'utf8'));
  const command = manifest.servers.find(server => server.name === 'browser').command;
  assert.ok(command.includes('--isolated'), 'the server makes a context of its own rather than taking the person\'s tabs');
  const page = command[command.indexOf('--init-page') + 1];
  assert.equal(page, '${plugin}/share-sign-in.cjs');
  assert.ok(existsSync(new URL('share-sign-in.cjs', here)));
});

test("a session's own context starts with the person's cookies", async () => {
  const signedIn = [{ name: 'who', value: 'person', domain: 'example.test', path: '/' }];
  const browser = {};
  const person = context(signedIn, browser);
  const own = context([], browser);
  Object.assign(browser, browserOf(person, own));
  const shareSignIn = require('./share-sign-in.cjs').default;

  await shareSignIn({ page: { context: () => own } });

  assert.deepEqual(own.added, signedIn);
  assert.deepEqual(person.added, []);
});

test("a page in the person's own context is left as it is", async () => {
  const browser = {};
  const person = context([{ name: 'who', value: 'person', domain: 'example.test', path: '/' }], browser);
  Object.assign(browser, browserOf(person));
  const shareSignIn = require('./share-sign-in.cjs').default;

  await shareSignIn({ page: { context: () => person } });

  assert.deepEqual(person.added, []);
});
