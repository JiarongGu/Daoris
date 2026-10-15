import { test } from 'node:test';
import assert from 'node:assert/strict';
import { join } from 'node:path';
import { endpoint, registryCheckouts, RUNNING_FILE, runningService, sessionRecords } from '../../src/service/service.ts';
import { makeFixture, type Fixture } from '../_fixture.ts';

/**
 * 🔴 FINDDAORIS1 (D171 point 1): a terminal outside Daoris's own panel is handed no variable. A `daoris` the install carries
 * finds the service as the driver host does: the variable; else the address the running desktop left in the home, taken only
 * while the process it names lives and the address answers, on this machine, as a Daoris host; else the refusal, which then
 * says Daoris is not running and how to start it. A checkout reads no address file, so no test reaches a live install.
 */

/** An install's shape (D93) with its home, and the address a desktop left there. */
function installed(name: string): Fixture & { data: string } {
  const fx = makeFixture(name);
  fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
  return Object.assign(fx, { data: join(fx.root, 'data') });
}

function leave(fx: Fixture, home: string, url: string, pid = process.pid): void {
  fx.write(join(home.slice(fx.root.length + 1), RUNNING_FILE), JSON.stringify({ url, pid, started: 1, since: '2026-10-16T08:00:00+00:00' }));
}

/** A host that answers each GET as the test says, and records what it was asked. */
function host(answer: (url: string) => Response) {
  const asked: string[] = [];
  return { asked, get: async (url: string) => { asked.push(url); return answer(url); } };
}

const daoris = () => host((url) => (url.endsWith('/api/status') ? Response.json({ tier: 'lexical only' }) : Response.json([])));

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

test('the records and the registry are read from the service the running desktop holds', async () => {
  const fx = installed('running-reads');
  try {
    leave(fx, fx.data, 'http://localhost:5181');
    const local = daoris();
    assert.deepEqual(await sessionRecords({}, local.get, fx.root), { records: [] });
    assert.deepEqual(await registryCheckouts({}, local.get, fx.root), { checkouts: [] });
    assert.deepEqual(local.asked, [
      'http://localhost:5181/api/status', 'http://localhost:5181/api/sessions?includeClosed=true',
      'http://localhost:5181/api/status', 'http://localhost:5181/api/registry',
    ]);

    const none = installed('running-reads-none');
    try {
      const unread = await sessionRecords({}, daoris().get, none.root);
      assert.ok('unread' in unread && /^no DAORIS_SERVICE_URL is set/.test(unread.unread) && unread.unread.includes('Daoris is not running'),
        JSON.stringify(unread));
    } finally {
      none.cleanup();
    }
  } finally {
    fx.cleanup();
  }
});
