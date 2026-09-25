import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandRemote } from '../src/remotes.ts';
import { readRemotes, redactKey, remotesPath } from '../src/remotemap.ts';
import { commandStatus } from '../src/commands.ts';
import { makeFixture } from './_fixture.ts';
import { DaorisError } from '../src/errors.ts';

/**
 * `daoris remote` — management parity for the machine's wiring (D50, workspace design §2b).
 *
 * The file is the truth and this is an editor over it, exactly as `driver.json` and the desktop's
 * checkboxes are: hand-editing keeps working, and nothing here is the only way to say anything.
 *
 * It is **offline** — it edits a file under the profile and talks to nothing — which is why it may sit
 * beside `status` without touching the doctrine commands' guarantee. The dogfood suite holds that line
 * structurally; these tests hold the behaviour.
 */

const KEY = 'dk_abcd1234wxyzsecret';

/** A remotes map in the fixture, never the developer's real one — and never OS temp. */
function mapAt(fx: { root: string }): string {
  return join(fx.root, 'remotes.json');
}

/** Run a command with the map pointed at a fixture and the environment otherwise clean. */
async function run(
  argv: string[],
  path: string,
  env: Record<string, string | undefined> = {},
): Promise<{ code: number; out: string }> {
  const saved: Record<string, string | undefined> = {};
  const set = { DAORIS_REMOTE_CONFIG: path, ...env };
  for (const [name, value] of Object.entries({
    DAORIS_REMOTE_URL: undefined, DAORIS_REMOTE_KEY: undefined, DAORIS_REMOTE_WORKSPACE: undefined,
    ...set,
  })) {
    saved[name] = process.env[name];
    if (value === undefined) delete process.env[name];
    else process.env[name] = value;
  }

  const lines: string[] = [];
  try {
    const code = await commandRemote({
      root: process.cwd(), argv, write: (line) => lines.push(line), packageRoot: process.cwd(),
    });
    return { code, out: lines.join('\n') };
  } finally {
    for (const [name, value] of Object.entries(saved)) {
      if (value === undefined) delete process.env[name];
      else process.env[name] = value;
    }
  }
}

test('a machine with no remote says so, and that is not a failure', async () => {
  const fx = makeFixture('remotes-empty');

  const { code, out } = await run(['list'], mapAt(fx));

  assert.equal(code, 0);
  assert.match(out, /no remote/i);
  fx.cleanup();
});

test('adding wires one workspace, and the key is echoed redacted', async () => {
  const fx = makeFixture('remotes-add');
  const path = mapAt(fx);

  const added = await run(['add', 'aurora', '--url', 'https://aurora.example.com/', '--key', KEY], path);

  assert.equal(added.code, 0);
  assert.match(added.out, /aurora/);
  // The audit prefix is the non-secret handle the deployment's own `keys list` prints; the rest of
  // the key must never reach a console, a transcript, or a log someone pastes into an issue.
  assert.match(added.out, /dk_abcd1234…/);
  assert.equal(added.out.includes(KEY), false);

  const stored = JSON.parse(readFileSync(path, 'utf8'));
  assert.deepEqual(stored, { aurora: { url: 'https://aurora.example.com', key: KEY } });

  const listed = await run(['list'], path);
  assert.match(listed.out, /aurora\s+https:\/\/aurora\.example\.com/);
  assert.match(listed.out, /dk_abcd1234…/);
  assert.equal(listed.out.includes(KEY), false);
  fx.cleanup();
});

test('a second workspace joins the map rather than replacing it', async () => {
  const fx = makeFixture('remotes-second');
  const path = mapAt(fx);

  await run(['add', 'aurora', '--url', 'https://aurora.example.com', '--key', KEY], path);
  await run(['add', 'tools', '--url', 'https://tools.example.com', '--key', 'dk_toolskey0000'], path);

  const stored = JSON.parse(readFileSync(path, 'utf8'));
  assert.deepEqual(Object.keys(stored).sort(), ['aurora', 'tools']);
  fx.cleanup();
});

/** The file is written like every Daoris write: BOM-less UTF-8, LF, one trailing newline. */
test('the map is written BOM-less with LF endings', async () => {
  const fx = makeFixture('remotes-bytes');
  const path = mapAt(fx);

  await run(['add', 'aurora', '--url', 'https://aurora.example.com', '--key', KEY], path);

  const bytes = readFileSync(path);
  assert.equal(bytes[0], '{'.charCodeAt(0));
  const text = bytes.toString('utf8');
  assert.equal(text.includes('\r'), false);
  assert.equal(text.endsWith('\n'), true);
  fx.cleanup();
});

test('adding without a url refuses rather than wiring half a remote', async () => {
  const fx = makeFixture('remotes-nourl');

  await assert.rejects(
    () => run(['add', 'aurora', '--key', KEY], mapAt(fx)),
    (error: unknown) => error instanceof DaorisError && /--url/.test(error.message));
  fx.cleanup();
});

test('adding without a workspace name refuses — the name is what the map is keyed by', async () => {
  const fx = makeFixture('remotes-noname');

  await assert.rejects(
    () => run(['add', '--url', 'https://aurora.example.com', '--key', KEY], mapAt(fx)),
    (error: unknown) => error instanceof DaorisError);
  fx.cleanup();
});

/** The key may come from the environment — a headless machine sets it once and runs the verb. */
test('the key is taken from the environment when no flag carries it', async () => {
  const fx = makeFixture('remotes-envkey');
  const path = mapAt(fx);

  const added = await run(
    ['add', 'aurora', '--url', 'https://aurora.example.com'], path, { DAORIS_REMOTE_KEY: KEY });

  assert.equal(added.code, 0);
  assert.equal(added.out.includes(KEY), false);
  assert.equal(JSON.parse(readFileSync(path, 'utf8')).aurora.key, KEY);
  fx.cleanup();
});

/**
 * With the environment pair set, the FILE is not this machine's answer — the loaders read the
 * environment whole (D48 §5). Saying so is the difference between a surface that reports the wiring
 * and one that reports its own edits.
 */
test('list reports the environment when the environment is the answer', async () => {
  const fx = makeFixture('remotes-envwins');
  const path = mapAt(fx);
  await run(['add', 'aurora', '--url', 'https://aurora.example.com', '--key', KEY], path);

  const listed = await run(['list'], path, {
    DAORIS_REMOTE_URL: 'https://env.example.com',
    DAORIS_REMOTE_KEY: 'dk_envkey000000',
    DAORIS_REMOTE_WORKSPACE: 'tools',
  });

  assert.match(listed.out, /environment/i);
  assert.match(listed.out, /tools\s+https:\/\/env\.example\.com/);
  // The file's own entry is not reported as live wiring, because it is not what any loader would use.
  assert.equal(/aurora\s+https/.test(listed.out), false);
  fx.cleanup();
});

test('removing takes the workspace off the map and says what it did not do', async () => {
  const fx = makeFixture('remotes-remove');
  const path = mapAt(fx);
  await run(['add', 'aurora', '--url', 'https://aurora.example.com', '--key', KEY], path);

  const removed = await run(['remove', 'aurora'], path);

  assert.equal(removed.code, 0);
  assert.match(removed.out, /aurora/);
  // The deployment is untouched: the key stays valid there until an operator revokes it, and the
  // repositories that fed it keep everything they have. Unwiring is a local act.
  assert.match(removed.out, /revoke|deployment|nothing/i);
  assert.deepEqual(JSON.parse(readFileSync(path, 'utf8')), {});
  fx.cleanup();
});

test('a workspace name is a person\'s name — case does not make a second row, and remove finds it', async () => {
  // Both C# twins read the map `OrdinalIgnoreCase` (`RemoteTarget`, `RemoteConfig`). Here it was a
  // plain Map: `remove aurora` said "not wired" over an `Aurora` the driver kept syncing to (REV3).
  const fx = makeFixture('remotes-case');
  const path = mapAt(fx);
  await run(['add', 'Aurora', '--url', 'https://aurora.example.com', '--key', KEY], path);

  const replaced = await run(['add', 'aurora', '--url', 'https://new.example.com', '--key', KEY], path);
  assert.match(replaced.out, /replacing https:\/\/aurora\.example\.com/);
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(path, 'utf8'))), ['Aurora']);

  const removed = await run(['remove', 'AURORA'], path);
  assert.doesNotMatch(removed.out, /not wired/);
  assert.deepEqual(JSON.parse(readFileSync(path, 'utf8')), {});
  fx.cleanup();
});

test('removing what was never wired is an answer, not a failure', async () => {
  const fx = makeFixture('remotes-remove-absent');

  const { code, out } = await run(['remove', 'nobody'], mapAt(fx));

  assert.equal(code, 0);
  assert.match(out, /not wired/i);
  fx.cleanup();
});

test('an unknown verb names the ones that exist', async () => {
  const fx = makeFixture('remotes-unknown');

  await assert.rejects(
    () => run(['frobnicate'], mapAt(fx)),
    (error: unknown) => error instanceof DaorisError
      && /list/.test(error.message) && /add/.test(error.message) && /remove/.test(error.message));
  fx.cleanup();
});

/** The reader is the CLI's twin of the service's and the driver's — the same three rules. */
test('the reader agrees with its twins: env whole, half-pairs nowhere, absence silent', () => {
  const fx = makeFixture('remotes-reader');
  const path = fx.write('remotes.json', JSON.stringify({
    aurora: { url: 'https://aurora.example.com/', key: KEY },
    broken: { url: 'https://tools.example.com' },
  }));

  const fromFile = readRemotes({ DAORIS_REMOTE_CONFIG: path });
  assert.equal(fromFile.source, 'file');
  assert.deepEqual([...fromFile.remotes.keys()], ['aurora']);
  assert.equal(fromFile.remotes.get('aurora')!.url, 'https://aurora.example.com');

  const fromEnv = readRemotes({
    DAORIS_REMOTE_CONFIG: path,
    DAORIS_REMOTE_URL: 'https://env.example.com',
    DAORIS_REMOTE_KEY: 'dk_envkey000000',
  });
  assert.equal(fromEnv.source, 'environment');
  assert.deepEqual([...fromEnv.remotes.keys()], ['default']);

  const halfSet = readRemotes({ DAORIS_REMOTE_CONFIG: path, DAORIS_REMOTE_URL: 'https://env.example.com' });
  assert.equal(halfSet.remotes.size, 0);

  const absent = readRemotes({ DAORIS_REMOTE_CONFIG: join(fx.root, 'nothing.json') });
  assert.equal(absent.source, 'none');
  assert.equal(absent.remotes.size, 0);
  fx.cleanup();
});

test('a redacted key shows the audit prefix and nothing else', () => {
  assert.equal(redactKey(KEY), 'dk_abcd1234…');
  assert.equal(redactKey('dk_short'), '…');
  assert.equal(redactKey(''), '…');
});

test('the map has a conventional home, overridable for a test or a second profile', () => {
  assert.match(remotesPath({ DAORIS_HOME: '/x/data' }) ?? '', /[\\/]data[\\/]remotes\.json$/);
  // 🔴 No default under the user profile (D63): no home is no map, which is the silent default (D21).
  assert.equal(remotesPath({}), null);
  assert.equal(remotesPath({ DAORIS_REMOTE_CONFIG: '/tmp/x.json' }), '/tmp/x.json');
});

/**
 * `status --machine` (D50): the manifest says MAY, the machine says WHERE, and a person debugging a
 * sync needs both in one place. Offline — it reads two files and asks nothing.
 */
test('status --machine reports the wiring beside the declaration', async () => {
  const fx = makeFixture('status-machine');
  fx.write('daoris.json', JSON.stringify({
    source: 's', packs: [], remote: { join: true, knowledge: true },
    domain: { summary: 'A test repo.', owns: [], accepts: [] },
  }));
  const map = fx.write('remotes.json', JSON.stringify({
    aurora: { url: 'https://aurora.example.com', key: KEY },
  }));

  const saved = process.env.DAORIS_REMOTE_CONFIG;
  process.env.DAORIS_REMOTE_CONFIG = map;
  const lines: string[] = [];
  try {
    commandStatus({
      root: fx.root, argv: ['--machine'], write: (line) => lines.push(line), packageRoot: process.cwd(),
    });
  } finally {
    if (saved === undefined) delete process.env.DAORIS_REMOTE_CONFIG;
    else process.env.DAORIS_REMOTE_CONFIG = saved;
  }

  const out = lines.join('\n');
  assert.match(out, /aurora/);
  assert.match(out, /https:\/\/aurora\.example\.com/);
  assert.match(out, /dk_abcd1234…/);
  assert.equal(out.includes(KEY), false);
  fx.cleanup();
});

test('status --machine on an unwired machine says the family is local, silently', async () => {
  const fx = makeFixture('status-machine-none');
  fx.write('daoris.json', JSON.stringify({ source: 's', packs: [] }));

  const saved = process.env.DAORIS_REMOTE_CONFIG;
  process.env.DAORIS_REMOTE_CONFIG = join(fx.root, 'nothing.json');
  const lines: string[] = [];
  try {
    commandStatus({
      root: fx.root, argv: ['--machine', '--json'], write: (line) => lines.push(line), packageRoot: process.cwd(),
    });
  } finally {
    if (saved === undefined) delete process.env.DAORIS_REMOTE_CONFIG;
    else process.env.DAORIS_REMOTE_CONFIG = saved;
  }

  const machine = JSON.parse(lines.join('\n')).machine;
  assert.deepEqual(machine.remotes, []);
  assert.equal(machine.source, 'none');
  fx.cleanup();
});
