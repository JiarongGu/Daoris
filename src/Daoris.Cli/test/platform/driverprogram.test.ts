import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { DaorisError } from '../../src/platform/errors.ts';
import { INSTALL_MARKER } from '../../src/platform/home.ts';
import {
  DRIVER_PROGRAM_FILE, DRIVER_PROGRAM_HOME, DRIVER_PROGRAM_VARIABLE, driverProgram, runDriverProgram, type DriverStart,
} from '../../src/platform/driverprogram.ts';
import { captureError, makeFixture } from '../_fixture.ts';

/**
 * The seam (PUSH1f2a, D194 point 1, the alone-path design §1.1): `daoris driver …` is run by Daoris's own driver program, found where
 * `DAORIS_DRIVER_PROGRAM` names its apphost, else in this package's install, else refused; started through the CLI's one spawn site
 * with the terminal's streams and its environment exactly; its exit the run's, a failed start, a Ctrl+C and any other end mapped as
 * the CLI's `relay` maps an agent's.
 */

const MISSING = '`daoris driver` is run by Daoris\'s own driver program, which this `daoris` cannot find: it is not the one a Daoris '
  + 'install carries, and `DAORIS_DRIVER_PROGRAM` names none. Run the install\'s `daoris` (its `app/bin/`), or `daoris-driver driver …` '
  + 'where it is installed; nothing was done.';

/** A build's apphost, an empty file of the program's own name, in a folder of the test's. */
function apphost(name: string): { program: string; cleanup: () => void } {
  const fx = makeFixture(name);
  return { program: fx.write(DRIVER_PROGRAM_FILE, ''), cleanup: fx.cleanup };
}

test('the program is the apphost DAORIS_DRIVER_PROGRAM names, before any install', () => {
  const { program, cleanup } = apphost('driver-program-named');
  try {
    assert.deepEqual(driverProgram({ [DRIVER_PROGRAM_VARIABLE]: program }, null), { program });
  } finally {
    cleanup();
  }
});

test('with none named, the program is this package\'s install\'s, under app/daoris-driver', () => {
  const fx = makeFixture('driver-program-install');
  try {
    fx.write(INSTALL_MARKER, '');
    const program = fx.write(join(...DRIVER_PROGRAM_HOME, DRIVER_PROGRAM_FILE), '');
    assert.deepEqual(driverProgram({}, fx.root), { program });
  } finally {
    fx.cleanup();
  }
});

test('with none named and no install, the verb is refused naming where the program is, and never found on PATH', () => {
  assert.deepEqual(driverProgram({ PATH: process.env.PATH }, null), { refusal: MISSING });
  // An install that carries no program is no program either.
  const fx = makeFixture('driver-program-no-program');
  try {
    fx.write(INSTALL_MARKER, '');
    assert.deepEqual(driverProgram({}, fx.root), { refusal: MISSING });
  } finally {
    fx.cleanup();
  }
});

test('DAORIS_DRIVER_PROGRAM naming anything but an existing apphost is refused, naming what it names', () => {
  const fx = makeFixture('driver-program-refused');
  try {
    const dll = fx.write('daoris-driver.dll', '');
    const cmd = fx.write('daoris-driver.cmd', '');
    const folder = join(fx.root, 'daoris-driver.exe-folder');
    mkdirSync(join(folder, DRIVER_PROGRAM_FILE), { recursive: true });
    const absent = join(fx.root, 'absent', DRIVER_PROGRAM_FILE);
    const quoted = `"${fx.write(DRIVER_PROGRAM_FILE, '')}"`;
    for (const named of [dll, cmd, join(folder, DRIVER_PROGRAM_FILE), absent, quoted]) {
      const found = driverProgram({ [DRIVER_PROGRAM_VARIABLE]: named }, null);
      assert.ok('refusal' in found, `${named} was taken for the program`);
      assert.ok(found.refusal.includes(`\`${named}\``), found.refusal);
      assert.ok(found.refusal.includes(`\`${DRIVER_PROGRAM_FILE}\``), found.refusal);
      assert.ok(found.refusal.endsWith('nothing was done.'), found.refusal);
    }
  } finally {
    fx.cleanup();
  }
});

/** A start that records what it was handed and answers as it is told. */
function standIn(answer: Partial<ReturnType<DriverStart>>): { start: DriverStart; seen: Parameters<DriverStart>[] ; listeners: number[] } {
  const seen: Parameters<DriverStart>[] = [];
  const listeners: number[] = [];
  const start: DriverStart = (...args) => {
    seen.push(args);
    listeners.push(process.listenerCount('SIGINT'), process.listenerCount('SIGBREAK'));
    return { status: null, signal: null, ...answer };
  };
  return { start, seen, listeners };
}

test('the family and its words are handed whole, with the terminal\'s environment exactly and its streams', () => {
  const { program, cleanup } = apphost('driver-program-words');
  try {
    const env = { [DRIVER_PROGRAM_VARIABLE]: program, PATH: 'X:/daoris/only-this', DAORIS_HOME: 'X:/daoris/home', DAORIS_SESSION_ID: 's1' };
    const words = ['standing', 'engine', 'a "quoted" word & 100% 中文', 'C:\\a path\\', '--clear'];
    const { start, seen } = standIn({ status: 0 });
    assert.equal(runDriverProgram('driver', words, { env, install: null, start }), 0);
    assert.equal(seen.length, 1);
    const [command, handed] = seen[0]!;
    assert.deepEqual(command, [program, 'driver', ...words]);
    // Exactly: no tools' PATH laid over it, nothing added, nothing taken (the TOOLS5 rule's one exception, D194 point 1).
    assert.deepEqual(handed, env);
  } finally {
    cleanup();
  }
});

test('Ctrl+C and Ctrl+Break leave the CLI waiting for the program: a listener for each is set before the start', () => {
  const { program, cleanup } = apphost('driver-program-signals');
  const before = [process.listenerCount('SIGINT'), process.listenerCount('SIGBREAK')];
  try {
    const { start, listeners } = standIn({ status: 0 });
    runDriverProgram('driver', ['list'], { env: { [DRIVER_PROGRAM_VARIABLE]: program }, install: null, start });
    assert.ok(listeners[0]! > before[0]!, 'no SIGINT listener was set before the start');
    assert.ok(listeners[1]! > before[1]!, 'no SIGBREAK listener was set before the start');
  } finally {
    cleanup();
  }
});

test('the program\'s own 0, 1 and 2 are the run\'s', () => {
  const { program, cleanup } = apphost('driver-program-exits');
  try {
    for (const status of [0, 1, 2]) {
      const { start } = standIn({ status });
      assert.equal(runDriverProgram('driver', ['list'], { env: { [DRIVER_PROGRAM_VARIABLE]: program }, install: null, start }), status);
    }
  } finally {
    cleanup();
  }
});

test('a Ctrl+C\'s end is 2 with nothing said: STATUS_CONTROL_C_EXIT on Windows, SIGINT elsewhere', () => {
  const { program, cleanup } = apphost('driver-program-ctrl-c');
  try {
    for (const answer of [{ status: 0xC000013A }, { status: -1073741510 }, { signal: 'SIGINT' as const }]) {
      const { start } = standIn(answer);
      assert.equal(runDriverProgram('driver', ['list'], { env: { [DRIVER_PROGRAM_VARIABLE]: program }, install: null, start }), 2);
    }
  } finally {
    cleanup();
  }
});

test('a start that fails is 2, saying the program and the error; any other status or signal is 2, naming it', () => {
  const { program, cleanup } = apphost('driver-program-failures');
  try {
    const env = { [DRIVER_PROGRAM_VARIABLE]: program };
    const failed = captureError(() => runDriverProgram('driver', ['list'], {
      env, install: null, start: standIn({ error: Object.assign(new Error('spawn EACCES'), { code: 'EACCES' }) }).start,
    }));
    assert.equal(failed.exitCode, 2);
    assert.ok(failed.message.includes(program) && failed.message.includes('spawn EACCES'), failed.message);

    const crashed = captureError(() => runDriverProgram('driver', ['list'], { env, install: null, start: standIn({ status: 134 }).start }));
    assert.equal(crashed.exitCode, 2);
    assert.ok(crashed.message.includes(program) && crashed.message.includes('134'), crashed.message);

    const killed = captureError(() => runDriverProgram('driver', ['list'], { env, install: null, start: standIn({ signal: 'SIGKILL' }).start }));
    assert.equal(killed.exitCode, 2);
    assert.ok(killed.message.includes('SIGKILL'), killed.message);
  } finally {
    cleanup();
  }
});

test('with no program the verb is refused, exit 2, and nothing is started', () => {
  const { start, seen } = standIn({ status: 0 });
  const refused = captureError(() => runDriverProgram('driver', ['list'], { env: {}, install: null, start }));
  assert.ok(refused instanceof DaorisError);
  assert.equal(refused.exitCode, 2);
  assert.equal(refused.message, MISSING);
  assert.equal(seen.length, 0);
});
