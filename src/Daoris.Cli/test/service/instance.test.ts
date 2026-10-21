import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { Socket } from 'node:net';
import { dirname, join } from 'node:path';
import { findInstance, INSTANCE_VARIABLE, instanceDoor, pressInstance, type InstanceWorld } from '../../src/service/service.ts';
import type { InstanceRoute } from '../../src/platform/door.ts';
import { DaorisError, type ExitCode } from '../../src/platform/errors.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { leaseHolder, tryLease } from '../../src/platform/lease.ts';
import { InstanceStandIn, ON_WINDOWS, pipeOf } from '../_instance.ts';
import { makeFixture, type Fixture } from '../_fixture.ts';

/**
 * A verb's door to the running Daoris (RPC1f, the terminal RPC design §7.2, §7.4 and §9), row by row of the switch, against a
 * stand-in of the pipe: alone where no instance runs, sent where one answers, refused where one lives and does not answer or refuses
 * the hello, and never alone once a request was sent and its answer lost. The driver's `InstanceDoorTests` hold the same rows of its
 * `InstanceDoor`, case for case; the two share no code (`twins.md`).
 *
 * Windows only, as the pipe is: each test serves its own stand-in in process, on its own home's name.
 */

const WINDOWS = { skip: !ON_WINDOWS && 'a named pipe is served on Windows only, as the driver\'s own pipe tests are' };

/** The id every fixture home here holds (HOMEID1), and another home's. */
const HOME_ID = '0123456789abcdef0123456789abcdef';
const ANOTHER_ID = 'fedcba9876543210fedcba9876543210';

/** A press the census opens, as a verb's would be: every route a `daoris` verb sends is the person's (D171's RPC1a note). */
const OPEN: InstanceRoute = { module: 'TEST.PRESS', route: 'MOVE', class: 'open' };
const PERSON: InstanceRoute = { ...OPEN, class: 'person' };

interface Home {
  fx: Fixture;
  install: string;
  home: string;
  pipe: string;
  out: string[];
  said: string[];
  alone: number;
  standIn: InstanceStandIn | null;
}

/** An install, as FINDDAORIS1 knows one: its marker beside `app/`, its home in `data/`, the home's id written. */
function installed(name: string): Home {
  const fx = makeFixture(name);
  fx.write('INSTALLED.md', '# Daoris — installed desktop\n');
  const home = join(fx.root, 'data');
  mkdirSync(home, { recursive: true });
  writeFileSync(homeEntry(home, 'home-id'), JSON.stringify({ id: HOME_ID, minted: '2026-10-20T08:00:00+00:00' }), 'utf8');
  return { fx, install: fx.root, home, pipe: pipeOf(HOME_ID), out: [], said: [], alone: 0, standIn: null };
}

async function serve(h: Home): Promise<InstanceStandIn> {
  h.standIn = await InstanceStandIn.serve(h.pipe, HOME_ID);
  return h.standIn;
}

async function done(h: Home): Promise<void> {
  await h.standIn?.close();
  h.fx.cleanup();
}

/** The address a desktop left in the home: its process (this one), and the pipe it serves unless `pipe` is null. */
function leave(h: Home, pipe: string | null = h.pipe): void {
  const file = homeEntry(h.home, 'running');
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, JSON.stringify({
    url: 'http://localhost:5177', pid: process.pid, started: 1, since: '2026-10-20T08:00:00+00:00', home: HOME_ID,
    ...(pipe ? { pipe } : {}),
  }), 'utf8');
}

/** A Daoris host's status, answering as the home whose id it names. */
const status = (id: string) => async () => Response.json({ tier: 'lexical only', home: id });

function world(h: Home, over: Partial<InstanceWorld> = {}): InstanceWorld {
  return {
    home: h.home,
    // 🔴 Never the account's: a test reads no variable it was not handed (D171's FINDDAORIS1 note).
    env: {},
    install: h.install,
    get: status(HOME_ID),
    lives: (pid) => pid === process.pid,
    write: (line) => h.out.push(line),
    say: (line) => h.said.push(line),
    // TESTWAIT7: a stand-in that answers is waited for for minutes, as a loaded machine needs.
    answer: 120_000,
    wait: 120_000,
    ...over,
  };
}

const alone = (h: Home) => (): ExitCode => {
  h.alone += 1;
  h.out.push('trees: done alone.');
  return 0;
};

/** The answer in the verb's words: its message, exit 0 where done and 1 where refused as data (§6). */
const words = (h: Home) => (answer: unknown): ExitCode => {
  const { message, done: ok } = answer as { message: string; done: boolean };
  h.out.push(`trees: ${message}`);
  return ok ? 0 : 1;
};

/** A press as the switch takes it, and its refusal's sentence where it threw one. */
async function press(h: Home, w: InstanceWorld, route: InstanceRoute = OPEN): Promise<{ code: number; error: string }> {
  try {
    return { code: await pressInstance(route, { id: 's1a2b3c4', force: true }, alone(h), words(h), w), error: '' };
  } catch (error) {
    if (!(error instanceof DaorisError)) throw error;
    return { code: error.exitCode, error: error.message };
  }
}

test('no running file acts alone', WINDOWS, async () => {
  const h = installed('instance-none');
  try {
    const standIn = await serve(h);
    const { code } = await press(h, world(h));
    assert.equal(code, 0);
    assert.equal(h.alone, 1);
    assert.equal(standIn.connections, 0);
  } finally {
    await done(h);
  }
});

test('a workspace build reads no address file', WINDOWS, async () => {
  const h = installed('instance-workspace');
  try {
    const standIn = await serve(h);
    leave(h);
    const { code } = await press(h, world(h, { install: null }));
    assert.equal(code, 0);
    assert.equal(h.alone, 1);
    assert.equal(standIn.connections, 0);
  } finally {
    await done(h);
  }
});

test('a status answering another home acts alone', WINDOWS, async () => {
  const h = installed('instance-another');
  try {
    const standIn = await serve(h);
    leave(h);
    const { code } = await press(h, world(h, { get: status(ANOTHER_ID) }));
    assert.equal(code, 0);
    assert.equal(h.alone, 1);
    assert.equal(standIn.connections, 0);
  } finally {
    await done(h);
  }
});

test('a headless loop holding the home acts alone', WINDOWS, async () => {
  const h = installed('instance-headless');
  try {
    const standIn = await serve(h);
    leave(h);
    const taken = tryLease(h.home, 'headless');
    assert.ok('lease' in taken);
    try {
      const { code } = await press(h, world(h));
      assert.equal(code, 0);
      assert.equal(h.alone, 1);
      assert.equal(standIn.connections, 0);
    } finally {
      taken.lease.release();
    }
  } finally {
    await done(h);
  }
});

/** RPC1c: a person's press goes to the instance that answers, which shows it in the window; the terminal says it waits there. */
test("a person's route is sent, the terminal says it waits for the window, then says its answer", WINDOWS, async () => {
  const h = installed('instance-person');
  try {
    const standIn = await serve(h);
    standIn.confirming = true;
    standIn.data = { done: true, message: 'archived s1a2b3c4.' };
    leave(h);
    const { code } = await press(h, world(h), PERSON);
    assert.equal(code, 0);
    assert.equal(h.alone, 0);
    assert.equal(standIn.presses.length, 1);
    assert.deepEqual(h.out, ['trees: archived s1a2b3c4.']);
    assert.deepEqual(h.said, ["daoris: waiting for you to confirm this in Daoris's window…"]);
  } finally {
    await done(h);
  }
});

for (const refused of ['refused', 'expired']) {
  test(`a person's route the window answers ${refused} is exit 1, and never done alone`, WINDOWS, async () => {
    const h = installed(`instance-${refused}`);
    try {
      const standIn = await serve(h);
      standIn.confirming = true;
      standIn.press = 'refuse';
      standIn.code = refused;
      leave(h);
      const { code, error } = await press(h, world(h), PERSON);
      assert.equal(code, 1);
      assert.equal(h.alone, 0);
      assert.equal(error, `refused ${refused}. (${refused})`);
      assert.deepEqual(h.out, []);
    } finally {
      await done(h);
    }
  });
}

test("a person's route with no instance acts alone, and a window's route is never sent", WINDOWS, async () => {
  const h = installed('instance-window');
  try {
    const standIn = await serve(h);
    assert.equal((await press(h, world(h), PERSON)).code, 0);
    leave(h);
    assert.equal((await press(h, world(h), { ...OPEN, class: 'window' })).code, 0);
    assert.equal(h.alone, 2);
    assert.equal(standIn.connections, 0);
  } finally {
    await done(h);
  }
});

test("an instance that answers is sent the press once, and its answer is said in the verb's words", WINDOWS, async () => {
  const h = installed('instance-answers');
  try {
    const standIn = await serve(h);
    standIn.data = { done: true, message: 'landed on `main`.' };
    standIn.progress = 'half way';
    leave(h);
    const { code, error } = await press(h, world(h));
    assert.equal(code, 0, error);
    assert.equal(h.alone, 0);
    assert.equal(standIn.presses.length, 1);
    const [sent] = standIn.presses;
    assert.equal(sent!.module, 'TEST.PRESS');
    assert.equal(sent!.type, 'MOVE');
    assert.deepEqual(sent!.payload, { id: 's1a2b3c4', force: true });
    assert.match(sent!.id, /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
    assert.ok(sent!.key!.length >= 16 && sent!.key!.length <= 128);
    // The hello first, naming this home's id and the wire's version.
    const [hello] = standIn.sent;
    assert.equal(hello!.type, 'HELLO');
    assert.equal((hello!.payload as { home: string }).home, HOME_ID);
    assert.equal((hello!.payload as { protocol: number }).protocol, 1);
    assert.deepEqual(h.out, ['  half way', 'trees: landed on `main`.']);
    assert.deepEqual(h.said, []);
  } finally {
    await done(h);
  }
});

test("an answer refused as data is the verb's exit 1", WINDOWS, async () => {
  const h = installed('instance-data');
  try {
    const standIn = await serve(h);
    standIn.data = { done: false, message: 'kept: its tree has uncommitted work.' };
    leave(h);
    const { code } = await press(h, world(h));
    assert.equal(code, 1);
    assert.equal(h.alone, 0);
    assert.deepEqual(h.out, ['trees: kept: its tree has uncommitted work.']);
  } finally {
    await done(h);
  }
});

test('a press the instance refuses is exit 2 with its sentence, and never done alone', WINDOWS, async () => {
  const h = installed('instance-refuses');
  try {
    const standIn = await serve(h);
    standIn.press = 'refuse';
    standIn.code = 'confirm-in-window';
    leave(h);
    const { code, error } = await press(h, world(h));
    assert.equal(code, 2);
    assert.equal(h.alone, 0);
    assert.equal(error, 'refused confirm-in-window. (confirm-in-window)');
  } finally {
    await done(h);
  }
});

test('an instance whose pipe does not answer refuses the press, and nothing is done alone', WINDOWS, async () => {
  const h = installed('instance-silent');
  try {
    // The file names this home's pipe, and nothing serves it.
    leave(h);
    const { code, error } = await press(h, world(h, { answer: 1_000 }));
    assert.equal(code, 2);
    assert.equal(h.alone, 0);
    assert.ok(error.startsWith(`Daoris is running on this home (\`Daoris.exe\`, pid ${process.pid}) and did not answer; nothing was done.`), error);
    assert.match(error, /Its pipe did not answer within 1 seconds\./);
  } finally {
    await done(h);
  }
});

test('an instance naming no pipe refuses the press, and nothing is done alone', WINDOWS, async () => {
  const h = installed('instance-no-pipe');
  try {
    const standIn = await serve(h);
    leave(h, null);
    const { code, error } = await press(h, world(h));
    assert.equal(code, 2);
    assert.equal(h.alone, 0);
    assert.equal(standIn.connections, 0);
    assert.ok(error.startsWith(`Daoris is running on this home (\`Daoris.exe\`, pid ${process.pid}) and did not answer; nothing was done.`), error);
    assert.match(error, /names no pipe/);
  } finally {
    await done(h);
  }
});

/** A connect the pipe's ACL refuses, as Node meets one another account serves: `EPERM`, before any line crosses. */
const refusing = (path: string): Socket => {
  const socket = new Socket();
  setImmediate(() => socket.destroy(Object.assign(new Error(`connect EPERM ${path}`), { code: 'EPERM' })));
  return socket;
};

/**
 * A pipe that refuses this terminal (PIPEOWNER1; the driver's `A_pipe_another_account_or_elevation_holds_…` rows) refuses the press
 * saying so, at once and never as a pipe that did not answer, exit 2, nothing done alone. Node is refused by the pipe's ACL alone (it
 * reads no owner, so the driver's owner row has no twin here); the world's `connect` hands that refusal, since Node cannot serve a pipe
 * with an ACL.
 */
for (const how of ['address', 'variable'] as const) {
  test(`a pipe another account or elevation holds refuses the press saying so (${how}), and nothing is done alone`, WINDOWS, async () => {
    const h = installed(`instance-refused-${how}`);
    try {
      leave(h);
      const named = how === 'variable' ? { env: { [INSTANCE_VARIABLE]: h.pipe } } : {};
      const { code, error } = await press(h, world(h, { connect: refusing, ...named }));
      assert.equal(code, 2);
      assert.equal(h.alone, 0);
      const who = how === 'variable'
        ? `the Daoris ${INSTANCE_VARIABLE} names (${h.pipe})`
        : `Daoris is running on this home (\`Daoris.exe\`, pid ${process.pid}) and`;
      assert.equal(error, `${who} refused this terminal; nothing was done. Its pipe belongs to another Windows account or elevation.`);
    } finally {
      await done(h);
    }
  });
}

for (const [code, why, said] of [
  ['protocol', 'close', "another version of the terminal's pipe"],
  ['home', 'close', 'another home'],
  ['stopping', 'close', 'Daoris is closing; run this again once it is back.'],
  ['stopping', 'update', 'Daoris is updating; run this again once it is back.'],
] as const) {
  test(`a hello refused ${code} (${why}) refuses the press, saying which`, WINDOWS, async () => {
    const h = installed(`instance-hello-${code}-${why}`);
    try {
      const standIn = await serve(h);
      standIn.refuseHello = code;
      standIn.stoppingWhy = why;
      leave(h);
      const refused = await press(h, world(h));
      assert.equal(refused.code, 2);
      assert.equal(h.alone, 0);
      assert.equal(standIn.presses.length, 0);
      assert.ok(refused.error.includes(said), refused.error);
    } finally {
      await done(h);
    }
  });
}

test('a lost answer is asked for by its key and said, never sent again nor done alone', WINDOWS, async () => {
  const h = installed('instance-lost');
  try {
    const standIn = await serve(h);
    standIn.press = 'drop';
    standIn.data = { done: true, message: 'landed on `main`.' };
    leave(h);
    const { code, error } = await press(h, world(h));
    assert.equal(code, 0, error);
    assert.equal(h.alone, 0);
    assert.equal(standIn.presses.length, 1);
    assert.equal(standIn.results.length, 1);
    const [sent] = standIn.presses;
    const asked = standIn.results[0]!.payload as { id: string; key: string };
    assert.equal(asked.id, sent!.id);
    assert.equal(asked.key, sent!.key);
    assert.deepEqual(h.out, ['trees: landed on `main`.']);
  } finally {
    await done(h);
  }
});

for (const [kind, state, said] of [
  ['drop', 'running', 'Daoris was still working on it (request '],
  ['drop', 'unknown', 'Daoris does not know how it ended (request '],
  ['hold', 'running', 'Daoris was still working on it (request '],
] as const) {
  test(`a lost answer (${kind}) the instance says is ${state} is said with its request, and never done alone`, WINDOWS, async () => {
    const h = installed(`instance-lost-${kind}-${state}`);
    try {
      const standIn = await serve(h);
      standIn.press = kind;
      standIn.result = state;
      leave(h);
      const { code, error } = await press(h, world(h, kind === 'hold' ? { wait: 500 } : {}));
      assert.equal(code, 2);
      assert.equal(h.alone, 0);
      assert.equal(standIn.presses.length, 1);
      assert.equal(standIn.results.length, 1);
      assert.ok(error.includes(`${said}${standIn.presses[0]!.id})`), error);
    } finally {
      await done(h);
    }
  });
}

test('the variable names the instance a workspace build reaches', WINDOWS, async () => {
  const h = installed('instance-variable');
  try {
    const standIn = await serve(h);
    const { code, error } = await press(h, world(h, { install: null, env: { [INSTANCE_VARIABLE]: h.pipe } }));
    assert.equal(code, 0, error);
    assert.equal(h.alone, 0);
    assert.equal(standIn.presses.length, 1);
  } finally {
    await done(h);
  }
});

test('the variable naming an instance that does not answer refuses the press', WINDOWS, async () => {
  const h = installed('instance-variable-silent');
  try {
    const { code, error } = await press(h, world(h, { env: { [INSTANCE_VARIABLE]: h.pipe }, answer: 1_000 }));
    assert.equal(code, 2);
    assert.equal(h.alone, 0);
    assert.ok(error.includes(INSTANCE_VARIABLE), error);
  } finally {
    await done(h);
  }
});

/**
 * RPC1j's record, which this terminal keeps too: a press is kept before it is sent, `{ id, key, route, at }` in
 * `<home>/pipe/sent/<id>.json`, so `daoris-driver result <id>` can ask for an answer that never reached it.
 */
test('a press keeps its id and key under the home, as the driver keeps its own', WINDOWS, async () => {
  const h = installed('instance-kept');
  try {
    const standIn = await serve(h);
    leave(h);
    await press(h, world(h));
    const [sent] = standIn.presses;
    const kept = JSON.parse(readFileSync(homeEntry(h.home, 'pipe', 'sent', `${sent!.id}.json`), 'utf8')) as Record<string, unknown>;
    assert.equal(kept.id, sent!.id);
    assert.equal(kept.key, sent!.key);
    assert.equal(kept.route, 'TEST.PRESS.MOVE');
    assert.ok(Math.abs(Date.parse(kept.at as string) - Date.now()) < 60_000);
  } finally {
    await done(h);
  }
});

test('what it finds where the instance answers is its client', WINDOWS, async () => {
  const h = installed('instance-found');
  try {
    await serve(h);
    leave(h);
    const look = await findInstance(world(h));
    look.close();
    assert.equal(look.found, 'answers');
  } finally {
    await done(h);
  }
});

// PUSH1f2b: `daoris driver` is Daoris's driver program's, which is the running Daoris's client itself (`DriverChoicesCommandTests`);
// this door is the other families' until PUSH1f2f.
test('a press with no Daoris running acts alone, holding the lease for its act alone', WINDOWS, async () => {
  const h = installed('instance-driver-alone');
  try {
    const standIn = await serve(h);
    let held: string | null = null;
    const door = instanceDoor(world(h));
    const code = await door({
      what: 'driver drive', route: { module: 'DAORIS.DRIVER', route: 'SET_DRIVABLE', class: 'person' }, payload: {},
      alone: () => {
        held = leaseHolder(h.home)?.what ?? null;
        return 0;
      },
      words: () => 2,
    });
    assert.equal(code, 0);
    assert.equal(held, 'driver drive', 'the act held the home as a verb');
    assert.equal(leaseHolder(h.home), null, 'and let it go');
    assert.equal(standIn.connections, 0);
    assert.ok(!readdirSync(dirname(homeEntry(h.home, 'home-lease'))).some((name) => name.startsWith('home.lease')));
  } finally {
    await done(h);
  }
});
