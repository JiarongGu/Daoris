import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fakeContext, fakePage } from './fakes.mjs';
import { scopeContext } from './scoped-context.mjs';

test("a session's view lists none of the person's pages, and the person's pages are untouched", async () => {
  const person = fakePage('https://person.test/');
  const real = fakeContext({ pages: [person] });
  const view = scopeContext(real);

  assert.deepEqual(view.pages(), []);
  const own = await view.newPage();
  assert.deepEqual(view.pages(), [own]);
  await view.close();

  assert.equal(own.closed, true);
  assert.equal(person.closed, false);
  assert.equal(real.closed, false, "the person's context is never closed");
});

test('a page the person or another session opens afterwards is not adopted', async () => {
  const real = fakeContext();
  const view = scopeContext(real);
  const seen = [];
  view.on('page', page => seen.push(page));

  real.appear(fakePage('https://other.test/'));
  const own = await view.newPage();

  assert.deepEqual(seen, [own]);
  assert.deepEqual(view.pages(), [own]);
});

test("a popup of the session's own page is adopted, and a page of anyone else's is not", async () => {
  const real = fakeContext();
  const view = scopeContext(real);
  const seen = [];
  view.on('page', page => seen.push(page));
  const own = await view.newPage();
  real.appear(fakePage());

  const popup = fakePage('https://popup.test/');
  real.appear(popup);
  own.emit('popup', popup);

  assert.deepEqual(seen, [own, popup]);
  assert.deepEqual(view.pages(), [own, popup]);
  await popup.close();
  assert.deepEqual(view.pages(), [own]);
});

test('everything else reaches the real context as it is', async () => {
  const real = fakeContext({ cookies: [{ name: 'a' }] });
  const view = scopeContext(real);
  assert.deepEqual(await view.cookies(), [{ name: 'a' }]);
  const closed = [];
  view.on('close', () => closed.push(true));
  real.emit('close');
  assert.deepEqual(closed, [true]);
});

test('a listener removed hears nothing more', async () => {
  const view = scopeContext(fakeContext());
  const seen = [];
  const listener = page => seen.push(page);
  view.on('page', listener);
  await view.newPage();
  view.removeListener('page', listener);
  await view.newPage();
  assert.equal(seen.length, 1);
});
