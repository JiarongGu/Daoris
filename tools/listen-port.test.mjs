/**
 * A test server's port that Node's fetch will reach:
 *
 *   node --test tools/listen-port.test.mjs
 *
 * `npm run verify` runs it beside the rehearsal kit's. The first port is handed in, a blocked one bound for the case, so the
 * re-listen is seen every run rather than when the system's port 0 happens on one.
 */
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { test } from 'node:test';
import { BAD_PORTS, listenFetchable } from './listen-port.mjs';

const answering = () => createServer((request, response) => response.writeHead(200).end('here'));

/** A port fetch refuses that this machine can bind now: the high ones, past the range a user needs privilege for. */
async function freeBlockedPort() {
  for (const port of [...BAD_PORTS].filter((one) => one >= 1024).reverse()) {
    const probe = createServer();
    const bound = await new Promise((resolve) => {
      probe.once('error', () => resolve(false));
      probe.listen(port, '127.0.0.1', () => resolve(true));
    });
    if (bound) {
      await new Promise((resolve) => probe.close(resolve));
      return port;
    }
  }
  throw new Error('no blocked port above 1023 is free on this machine');
}

test('every port on the list is one Node\'s fetch refuses as a bad port, unasked', async () => {
  for (const port of BAD_PORTS) {
    const refused = await fetch(`http://127.0.0.1:${port}/`).then(() => null, (error) => error);
    assert.ok(refused, `fetch reached port ${port}`);
    assert.equal(refused.cause?.message, 'bad port', `port ${port}: ${refused.cause?.message}`);
  }
});

test('a server handed a blocked port is listened again on one fetch reaches', async () => {
  const blocked = await freeBlockedPort();
  const server = answering();
  try {
    const url = await listenFetchable(server, { first: blocked });
    const port = server.address().port;
    assert.notEqual(port, blocked);
    assert.ok(!BAD_PORTS.has(port), `listened on ${port}, which fetch refuses`);
    assert.equal(url, `http://127.0.0.1:${port}`);
    assert.equal(await (await fetch(url)).text(), 'here');
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
});

test('a server the system hands a fetchable port keeps it, and answers', async () => {
  const server = answering();
  try {
    const url = await listenFetchable(server);
    assert.ok(!BAD_PORTS.has(server.address().port));
    assert.equal(await (await fetch(url)).text(), 'here');
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
});

test('a listen that fails is the caller\'s error, not a retry', async () => {
  const holder = answering();
  const url = await listenFetchable(holder);
  const second = answering();
  try {
    await assert.rejects(listenFetchable(second, { first: Number(new URL(url).port) }), { code: 'EADDRINUSE' });
  } finally {
    await new Promise((resolve) => holder.close(resolve));
  }
});
