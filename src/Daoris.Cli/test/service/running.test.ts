import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createServer, type Server } from 'node:http';
import { once } from 'node:events';
import { join } from 'node:path';
import type { AddressInfo } from 'node:net';
import { endpoint, RUNNING_FILE, runningService } from '../../src/service/service.ts';
import { HOME_ID_FILE } from '../../src/platform/home.ts';
import { makeFixture, type Fixture } from '../_fixture.ts';

/**
 * 🔴 FINDDAORIS1 (D171 point 1): a terminal outside Daoris's own panel is handed no variable. A `daoris` the install carries
 * finds the service as the driver host does: the variable; else the address the running desktop left in the home, taken only
 * while the process it names lives and the address answers, on this machine, as a Daoris host; else the refusal, which then
 * says Daoris is not running and how to start it. A checkout reads no address file, so no test reaches a live install.
 */

/** The id every fixture home here holds (HOMEID1), and another home's. */
const HOME_ID = '0123456789abcdef0123456789abcdef';
const ANOTHER_ID = 'fedcba9876543210fedcba9876543210';

/** An install's shape (D93) with its home, and the address a desktop left there. */
function installed(name: string): Fixture & { data: string } {
  const fx = makeFixture(name);
  fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
  return Object.assign(fx, { data: join(fx.root, 'data') });
}

/**
 * The address a desktop left in `home`, and the home's id (HOMEID1) unless `id` is null; with `named`, the file's own fields
 * beside the rest, as a desktop from RPC1a on writes its `home` and `pipe`.
 */
function leave(fx: Fixture, home: string, url: string, pid = process.pid, id: string | null = HOME_ID, named: Record<string, unknown> = {}): void {
  const under = home.slice(fx.root.length + 1);
  fx.write(join(under, RUNNING_FILE), JSON.stringify({ url, pid, started: 1, since: '2026-10-16T08:00:00+00:00', ...named }));
  if (id) fx.write(join(under, HOME_ID_FILE), JSON.stringify({ id, minted: '2026-10-16T08:00:00+00:00' }));
}

/** A host that answers each GET as the test says, and records what it was asked. */
function host(answer: (url: string) => Response) {
  const asked: string[] = [];
  return { asked, get: async (url: string) => { asked.push(url); return answer(url); } };
}

/** A Daoris host's status, serving the home whose id it names (HOMEID1); none for one from before. */
const status = (home: string | null = HOME_ID) => Response.json(home ? { tier: 'lexical only', home } : { tier: 'lexical only' });

const daoris = (home: string | null = HOME_ID) => host((url) => (url.endsWith('/api/status') ? status(home) : Response.json([])));

/** A real host on the loopback answering every request as told, for the redirect's row. */
async function listening(answer: (path: string, res: import('node:http').ServerResponse) => void): Promise<{ url: string; asked: string[]; server: Server }> {
  const asked: string[] = [];
  const server = createServer((req, res) => { asked.push(req.url ?? ''); answer(req.url ?? '', res); });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  return { url: `http://127.0.0.1:${(server.address() as AddressInfo).port}`, asked, server };
}

test('the variable still wins, and nothing is read or asked', async () => {
  const fx = installed('running-variable');
  try {
    leave(fx, fx.data, 'http://localhost:5181');
    const never = daoris();
    assert.deepEqual(
      await endpoint({ DAORIS_SERVICE_URL: 'http://localhost:5199/', DAORIS_SERVICE_KEY: 'k1' }, never.get, fx.root),
      { url: 'http://localhost:5199', key: 'k1' });
    assert.deepEqual(never.asked, []);
  } finally {
    fx.cleanup();
  }
});

test('a daoris the install carries finds the service the running desktop holds', async () => {
  const fx = installed('running-found');
  try {
    leave(fx, fx.data, 'http://localhost:5181');
    const local = daoris();
    assert.deepEqual(await endpoint({}, local.get, fx.root), { url: 'http://localhost:5181', key: null });
    assert.deepEqual(local.asked, ['http://localhost:5181/api/status']);

    // The home's variable still wins too: the address is the one in the home it names.
    const other = join(fx.root, 'elsewhere');
    leave(fx, other, 'http://localhost:5182');
    assert.equal((await endpoint({ DAORIS_HOME: other }, daoris().get, fx.root)).url, 'http://localhost:5182');
  } finally {
    fx.cleanup();
  }
});

test('with nothing running, the refusal says so and how to start it', async () => {
  const fx = installed('running-none');
  try {
    const never = daoris();
    await assert.rejects(endpoint({}, never.get, fx.root), (error: Error) => {
      assert.match(error.message, /^no DAORIS_SERVICE_URL — this command talks to a knowledge service/);
      assert.ok(error.message.includes(`Daoris is not running on ${fx.data}`), error.message);
      assert.ok(error.message.includes(join(fx.root, 'Daoris.exe')), error.message);
      return true;
    });
    assert.deepEqual(never.asked, []);
  } finally {
    fx.cleanup();
  }
});

test('an address is taken only while its process lives, on this machine, answering as Daoris', async () => {
  const fx = installed('running-judged');
  try {
    // Its process is gone: a desktop that ended without its exit. Nothing is asked.
    leave(fx, fx.data, 'http://localhost:5181', 4242);
    const never = daoris();
    const gone = await runningService(fx.data, fx.root, never.get, () => false);
    assert.ok('why' in gone && gone.why.startsWith(`Daoris is not running on ${fx.data}`), JSON.stringify(gone));
    assert.deepEqual(never.asked, []);

    // It lives, and nothing answers there as a Daoris host.
    const other = host(() => Response.json({ hello: 'world' }));
    const silent = await runningService(fx.data, fx.root, other.get, () => true);
    assert.ok('why' in silent
      && silent.why.startsWith(`Daoris is running on ${fx.data} (pid 4242), and nothing answered as its service at http://localhost:5181`),
    JSON.stringify(silent));
    const down = host(() => { throw new TypeError('fetch failed'); });
    assert.ok('why' in await runningService(fx.data, fx.root, down.get, () => true));

    // Another machine's address is never followed: a terminal's verbs send this machine's paths (D46).
    leave(fx, fx.data, 'https://team.example.com');
    const far = await runningService(fx.data, fx.root, never.get, () => true);
    assert.ok('why' in far && far.why.includes('https://team.example.com') && far.why.includes('not on this machine'), JSON.stringify(far));
    assert.deepEqual(never.asked, []);

    // A file that does not read is none.
    fx.write(join('data', RUNNING_FILE), 'not json');
    assert.ok('why' in await runningService(fx.data, fx.root, never.get, () => true));
  } finally {
    fx.cleanup();
  }
});

/**
 * 🔴 HOMEID1 (D171, the terminal RPC design §3.2): a status naming a `tier` was taken, so another install's service at the
 * address passed, and this CLI's process check is its id alone, so an id the system gave to another process since passed too.
 * An address is now taken only where its status answers this home's id (`home.json`), asked with redirects off.
 */
test('an address is taken only where its status answers this home’s id', async () => {
  const fx = installed('running-home');
  try {
    leave(fx, fx.data, 'http://localhost:5181');

    // Another install's service holds the port this home's desktop wrote: a Daoris host, and not this home's.
    const another = await runningService(fx.data, fx.root, daoris(ANOTHER_ID).get, () => true);
    assert.ok('why' in another
      && another.why.startsWith(`Daoris is running on ${fx.data} (pid ${process.pid}), and the service at http://localhost:5181 answered as another Daoris home's`),
    JSON.stringify(another));

    // A host from before HOMEID1 answers no home: not taken either.
    const older = await runningService(fx.data, fx.root, daoris(null).get, () => true);
    assert.ok('why' in older && older.why.includes("answered as no Daoris home's"), JSON.stringify(older));

    // A process id the system gave to another process since: this CLI cannot read a start time, so it lives to it, and the
    // service at the address, if any, answers another home or none.
    leave(fx, fx.data, 'http://localhost:5181', 4242);
    const reused = await runningService(fx.data, fx.root, daoris(ANOTHER_ID).get, () => true);
    assert.ok('why' in reused && reused.why.includes('(pid 4242)') && reused.why.includes('another Daoris home'), JSON.stringify(reused));
  } finally {
    fx.cleanup();
  }
});

/**
 * RPC1a (the terminal RPC design §4.1, §6.3): the file names the pipe its instance serves as `pipe`, which the driver's program reads
 * (`RunningAddress.Read`, `RunningAddressTests`) since the last family that sent over it became the program's (PUSH1f2f). Here the
 * service's address is all the file gives: a pipe beside it changes nothing for a verb that asks the service.
 */
test('the address is the service’s alone, whatever pipe the file names beside it', async () => {
  const fx = installed('running-pipe');
  try {
    leave(fx, fx.data, 'http://localhost:5181', process.pid, HOME_ID, { home: HOME_ID, pipe: `daoris-${HOME_ID}-1` });
    assert.deepEqual(await runningService(fx.data, fx.root, daoris().get, () => true), { url: 'http://localhost:5181' });
    assert.deepEqual(await endpoint({}, daoris().get, fx.root), { url: 'http://localhost:5181', key: null });
  } finally {
    fx.cleanup();
  }
});

test('a home with no id takes no address, and discovery mints none', async () => {
  const fx = installed('running-no-id');
  try {
    leave(fx, fx.data, 'http://localhost:5181', process.pid, null);
    const local = daoris();
    const none = await runningService(fx.data, fx.root, local.get, () => true);
    assert.ok('why' in none && none.why.includes('answered as another Daoris home'), JSON.stringify(none));
    assert.equal(fx.exists(join('data', HOME_ID_FILE)), false);
  } finally {
    fx.cleanup();
  }
});

test('a redirect is no answer: the status is asked with redirects off', async () => {
  const fx = installed('running-redirect');
  const target = await listening((_, res) => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify({ tier: 'lexical only', home: HOME_ID })); });
  const redirecting = await listening((_, res) => { res.writeHead(302, { location: `${target.url}/api/status` }); res.end(); });
  try {
    leave(fx, fx.data, redirecting.url);
    const found = await runningService(fx.data, fx.root, fetch, () => true);
    assert.ok('why' in found && found.why.includes(`nothing answered as its service at ${redirecting.url}`), JSON.stringify(found));
    assert.deepEqual(redirecting.asked, ['/api/status']);
    assert.deepEqual(target.asked, []);
  } finally {
    redirecting.server.close();
    target.server.close();
    fx.cleanup();
  }
});

/**
 * 🔴 A checkout reads no address file: the account's `DAORIS_HOME` names a live install's home on a developer's machine, and a
 * test that cleared the service's variable to see the refusal must never reach that install. The refusal is the one it was.
 */
test('a checkout reads no address file, and refuses as it always did', async () => {
  const fx = installed('running-checkout');
  try {
    leave(fx, fx.data, 'http://localhost:5181');
    const never = daoris();
    await assert.rejects(endpoint({ DAORIS_HOME: fx.data }, never.get, null), (error: Error) => {
      assert.match(error.message, /^no DAORIS_SERVICE_URL — /);
      assert.doesNotMatch(error.message, /not running/);
      return true;
    });
    assert.deepEqual(never.asked, []);
  } finally {
    fx.cleanup();
  }
});
