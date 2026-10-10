import { test, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import { commandRetire, commandImport, commandWire } from '../src/manage.ts';
import { DaorisError } from '../src/errors.ts';
import { makeFixture } from './_fixture.ts';

/**
 * The registration lifecycle from a terminal (D50): a headless machine running the driver is just
 * another machine, and everything a person manages must be settable without a screen.
 *
 * Both verbs talk to the loopback host, so the transport is stubbed here rather than mocked away: what
 * is being asserted is the REQUEST these commands make and the answer they relay, and the service's
 * own sentence reaching the person verbatim is the contract (the same rule the platform follows).
 */
type Call = { url: string; method: string; body: unknown };

const real = globalThis.fetch;
let calls: Call[] = [];

function stubService(status: number, payload: unknown) {
  calls = [];
  globalThis.fetch = (async (url: string | URL, init?: RequestInit) => {
    calls.push({
      url: String(url),
      method: init?.method ?? 'GET',
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    });
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => payload,
    };
  }) as typeof fetch;
}

afterEach(() => {
  globalThis.fetch = real;
  delete process.env.DAORIS_SERVICE_URL;
});

const run = (
  command: (args: { root: string; argv: string[]; write: (s: string) => void; packageRoot: string }) => unknown,
  root: string, argv: string[],
) => {
  const out: string[] = [];
  return { out, result: command({ root, argv, write: (s: string) => out.push(s), packageRoot: '' }) };
};

test('retire names this repository when nothing else is named', async () => {
  const fx = makeFixture('retire-here');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177/';
  stubService(200, { repository: 'retire-here', retired: true, message: 'Nothing was deleted.' });

  const { out, result } = run(commandRetire, fx.root, []);
  assert.equal(await result, 0);

  assert.equal(calls[0]!.method, 'DELETE');
  assert.match(calls[0]!.url, /\/api\/registry\/retire-here$/);
  // The service's own sentence reaches the person — it is the part that says what a retire does NOT do.
  assert.ok(out.some((line) => line.includes('Nothing was deleted.')));
  fx.cleanup();
});

test('retire can name another repository, for a machine with no screen', async () => {
  const fx = makeFixture('retire-other');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { repository: 'elsewhere', retired: true, message: 'gone from the map' });

  assert.equal(await run(commandRetire, fx.root, ['elsewhere']).result, 0);

  assert.match(calls[0]!.url, /\/api\/registry\/elsewhere$/);
  fx.cleanup();
});

/** Retiring something that was never registered is an answer, not a failure — so it exits clean. */
test('retiring an unregistered repository is clean and says so', async () => {
  const fx = makeFixture('retire-absent');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { repository: 'ghost', retired: false, message: 'was not registered here' });

  const { out, result } = run(commandRetire, fx.root, ['ghost']);

  assert.equal(await result, 0);
  assert.ok(out.some((line) => line.includes('was not registered here')));
  fx.cleanup();
});

test('retire --dry-run asks nothing of the service', async () => {
  const fx = makeFixture('retire-dry');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, {});

  const { out, result } = run(commandRetire, fx.root, ['--dry-run']);

  assert.equal(await result, 0);
  assert.equal(calls.length, 0);
  assert.ok(out.some((line) => /would retire/.test(line)));
  fx.cleanup();
});

test('import posts the folder, resolved against the working directory', async () => {
  const fx = makeFixture('import-folder');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { folder: 'x', imported: 2, repositories: ['engine', 'game'], message: 'Registered 2' });

  const { out, result } = run(commandImport, fx.root, ['./family']);
  assert.equal(await result, 0);

  assert.equal(calls[0]!.method, 'POST');
  assert.match(calls[0]!.url, /\/api\/registry\/import$/);
  // Absolute, because the service may be a different process with a different working directory —
  // a relative path would resolve against whatever the host happens to be sitting in.
  const sent = (calls[0]!.body as { folder: string }).folder;
  assert.ok(sent.endsWith('family'), sent);
  assert.match(sent, /^([A-Za-z]:|\/)/, `not absolute: ${sent}`);
  assert.ok(out.some((line) => line.includes('Registered 2')));
  fx.cleanup();
});

/**
 * Setting a folder up AS a workspace (D77): one statement, where it was an import and a re-wiring per
 * repository. The name travels; silence still sends none, so an unnamed import still moves nobody.
 */
test('import --workspace names the circle every row lands in', async () => {
  const fx = makeFixture('import-workspace');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { folder: 'x', imported: 2, repositories: ['a', 'b'], message: 'Registered 2 into workspace `work`' });

  const { out, result } = run(commandImport, fx.root, ['--workspace', 'work', './family']);
  assert.equal(await result, 0);

  const body = calls[0]!.body as { folder: string; workspace?: string };
  assert.equal(body.workspace, 'work');
  // The flag's value is never read as the folder (REV3's operand rule).
  assert.ok(body.folder.endsWith('family'), body.folder);
  assert.ok(out.some((line) => line.includes('into workspace `work`')));
  fx.cleanup();
});

test('import --workspace with no name is refused before anything is sent', async () => {
  const fx = makeFixture('import-workspace-empty');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, {});

  await assert.rejects(async () => run(commandImport, fx.root, ['./family', '--workspace']).result, DaorisError);
  assert.equal(calls.length, 0);
  fx.cleanup();
});

/** With no folder named, the service imports the root it was configured with — so we send none. */
test('import with no folder lets the service use its own root', async () => {
  const fx = makeFixture('import-default');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { folder: 'x', imported: 0, repositories: [], message: 'Nothing under' });

  assert.equal(await run(commandImport, fx.root, []).result, 0);

  assert.deepEqual(calls[0]!.body, {});
  fx.cleanup();
});

/**
 * A folder is a machine path, and a machine path never leaves the machine (D47 §4) — `connect` already
 * sends its root only to a local service. `import` sent it anywhere, and a shared deployment refused
 * it only after it had arrived (REV3).
 */
test('import names no folder to a service that is not on this machine', async () => {
  const fx = makeFixture('import-remote');
  process.env.DAORIS_SERVICE_URL = 'https://team.example.com';
  stubService(200, {});

  try {
    await run(commandImport, fx.root, ['./family']).result;
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.match(error.message, /not on this machine/);
  }
  assert.equal(calls.length, 0, 'nothing was sent');
  fx.cleanup();
});

/** A refusal is the service's sentence, and it becomes the CLI's — never a bare status code. */
test('a refusal reaches the person verbatim, as a policy failure', async () => {
  const fx = makeFixture('import-refused');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(409, { error: 'a shared deployment is fed, not scanned' });

  try {
    await run(commandImport, fx.root, []).result;
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.equal(error.exitCode, 1);
    assert.match(error.message, /fed, not scanned/);
  }

  fx.cleanup();
});

/** WIRE1: the terminal door of the drawer's Move to workspace (D50, D161's ENTRY1d1 note). */
test('wire moves a named repository by the re-wiring route, and says so', async () => {
  const fx = makeFixture('wire-ok');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, { repository: 'aurora-engine', inWorkspace: 'work' });

  const { out, result } = run(commandWire, fx.root, ['--workspace', 'work', 'aurora-engine']);
  assert.equal(await result, 0);

  assert.equal(calls[0]!.method, 'POST');
  assert.match(calls[0]!.url, /\/api\/registry\/aurora-engine\/workspace$/);
  assert.deepEqual(calls[0]!.body, { workspace: 'work' });
  assert.deepEqual(out, ['daoris: `aurora-engine` is now in workspace `work`.']);
  fx.cleanup();
});

test('wire needs a repository and a workspace, and sends nothing without them', async () => {
  const fx = makeFixture('wire-usage');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, {});

  for (const argv of [[], ['aurora-engine'], ['--workspace', 'work']]) {
    await assert.rejects(async () => run(commandWire, fx.root, argv).result, (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 2 && /daoris wire <repository> --workspace <name>/.test(error.message));
  }
  assert.equal(calls.length, 0);
  fx.cleanup();
});

test('wire --dry-run asks nothing of the service', async () => {
  const fx = makeFixture('wire-dry');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';
  stubService(200, {});

  const { out, result } = run(commandWire, fx.root, ['aurora-engine', '--workspace', 'work', '--dry-run']);
  assert.equal(await result, 0);
  assert.equal(calls.length, 0);
  assert.match(out[0]!, /would move `aurora-engine` to workspace `work`/);
  fx.cleanup();
});

test('wire relays the unknown repository and the shared host boundary in the service\'s words, as refusals', async () => {
  const fx = makeFixture('wire-refused');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  for (const [status, said] of [[404, '`nope` is not registered here'], [409, 'this host serves one circle']] as const) {
    stubService(status, { error: said });
    await assert.rejects(async () => run(commandWire, fx.root, ['nope', '--workspace', 'work']).result, (error: unknown) =>
      error instanceof DaorisError && error.exitCode === 1 && error.message === said);
  }
  fx.cleanup();
});

test('wire without a service url names the variable', async () => {
  const fx = makeFixture('wire-no-url');
  await assert.rejects(async () => run(commandWire, fx.root, ['a', '--workspace', 'w']).result, /DAORIS_SERVICE_URL/);
  fx.cleanup();
});

test('both verbs refuse without a service url, naming the variable', async () => {
  const fx = makeFixture('manage-no-url');

  for (const command of [commandRetire, commandImport]) {
    try {
      await run(command, fx.root, []).result;
      assert.fail('expected a refusal');
    } catch (error) {
      assert.ok(error instanceof DaorisError);
      assert.match(error.message, /DAORIS_SERVICE_URL/);
    }
  }

  fx.cleanup();
});
