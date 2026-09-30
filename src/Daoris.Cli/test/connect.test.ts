import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandConnect, registration, usesOf } from '../src/connect.ts';
import { isLocalService } from '../src/service.ts';
import { readManifest } from '../src/config.ts';
import { DaorisError } from '../src/errors.ts';
import { makeFixture } from './_fixture.ts';
import type { Manifest } from '../src/types.ts';

const manifest = {
  source: 'github:Owner/Daoris#v0.0.1',
  packs: ['desktop-app'],
  domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
} as Manifest;

/**
 * The root is the one field spawning a session needs (D46), and it is a MACHINE path — so it travels
 * only to a service on this machine. `sensitive-info` keeps machine paths out of tracked files; the
 * same judgement keeps them off the network.
 */
test('a local service is told the root', () => {
  const body = registration('/srv/Repo', manifest, 'Repo', 'http://localhost:5177');

  assert.equal(body.root, '/srv/Repo');
  assert.equal(body.repository, 'Repo');
});

/**
 * D91: what a repository says it uses, read by the rule the service's `Declared.Uses` holds too — the
 * same table, line for line (twins): absent is empty; trimmed; a blank dropped; a repeat in any case
 * dropped, the first spelling kept; the repository's own name dropped.
 */
test('uses reads by one rule, the service\'s twin', () => {
  const table: [string[] | undefined, string][] = [
    [undefined, ''],
    [['engine'], 'engine'],
    [[' engine ', ''], 'engine'],
    [['engine', 'ENGINE', 'tools'], 'engine,tools'],
    [['game', 'Game', 'engine'], 'engine'],
  ];
  for (const [uses, expected] of table) {
    assert.equal(usesOf({ summary: '', owns: [], accepts: [], ...(uses ? { uses } : {}) }, 'game').join(','), expected, String(uses));
  }
});

test('a declaration goes on the wire with what it uses, and without the field when it uses none', () => {
  const said = registration('/srv/game', { ...manifest, domain: { ...manifest.domain!, uses: [' engine', 'game'] } }, 'game', 'http://localhost:5177');
  assert.deepEqual(said.domain?.uses, ['engine']);

  const silent = registration('/srv/game', manifest, 'game', 'http://localhost:5177');
  assert.equal('uses' in (silent.domain ?? {}), false);
});

/**
 * D115 §2.2 (DEV4): a repository's lanes travel as their words, and ALWAYS as a list — none is `[]`.
 * The service keeps a registration's lanes when one says nothing about them (the page's add, an older
 * client), so only an explicit empty list can take away lanes a repository stopped declaring.
 */
test('the registration carries the lanes\' words, and none is an explicit empty list', () => {
  const lanes = [{ id: 'core', title: 'Core', summary: 'The runtime.', steward: false }];
  const said = registration('/srv/engine', manifest, 'engine', 'https://daoris.example.com', undefined, lanes);
  assert.deepEqual(said.lanes, lanes);

  const none = registration('/srv/engine', manifest, 'engine', 'https://daoris.example.com');
  assert.deepEqual(none.lanes, []);
});

test('connect --dry-run sends the lanes daoris.lanes.json declares, words only, and says how to address one', async () => {
  const fx = makeFixture('connect-lanes');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'An engine.', owns: ['the runtime'], accepts: ['a quest'] },
  }));
  fx.write('daoris.lanes.json', JSON.stringify({
    lanes: [
      { id: 'core', title: 'Core', summary: 'The runtime.', paths: ['runtime/**'] },
      { id: 'assets', title: 'Assets', summary: 'The pipeline.', paths: ['assets/**'], gates: ['cli'] },
    ],
  }));
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  const out: string[] = [];
  const code = await commandConnect({
    root: fx.root, argv: ['--dry-run'], write: (s: string) => out.push(s), packageRoot: '',
  });

  assert.equal(code, 0);
  const payload = JSON.parse(out.slice(0, -1).join('\n'));
  assert.deepEqual(payload.lanes, [
    { id: 'core', title: 'Core', summary: 'The runtime.', steward: false },
    { id: 'assets', title: 'Assets', summary: 'The pipeline.', steward: false },
  ]);
  assert.equal(JSON.stringify(payload).includes('runtime/**'), false, 'no glob goes on the wire');

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

test('connect refuses an unreadable daoris.lanes.json before it sends anything', async () => {
  const fx = makeFixture('connect-lanes-unreadable');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'An engine.', owns: ['the runtime'], accepts: ['a quest'] },
  }));
  fx.write('daoris.lanes.json', JSON.stringify({ lanes: [{ id: 'core', paths: ['a/**'] }, { id: 'core', paths: ['b/**'] }] }));
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  const out: string[] = [];
  try {
    await commandConnect({ root: fx.root, argv: ['--dry-run'], write: (s: string) => out.push(s), packageRoot: '' });
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.equal(error.exitCode, 1);
    assert.match(error.message, /lane 'core': the id is used twice/);
  }
  assert.deepEqual(out, []);

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

test('a remote service is not told the root', () => {
  const body = registration('/srv/Repo', manifest, 'Repo', 'https://daoris.example.com');

  assert.equal('root' in body, false);
});

test('loopback is the boundary, in every spelling', () => {
  assert.equal(isLocalService('http://localhost:5177'), true);
  assert.equal(isLocalService('http://127.0.0.1:5177'), true);
  assert.equal(isLocalService('http://[::1]:5177'), true);
  assert.equal(isLocalService('https://daoris.example.com'), false);
  // TEST-NET-3 (RFC 5737), which exists to be written down — a fixture must not look like somebody's
  // actual subnet, and the sensitive scan cannot tell the two apart.
  assert.equal(isLocalService('http://203.0.113.10:5177'), false);
  // A subdomain that merely CONTAINS the word is not local — the check is the hostname, not a substring.
  assert.equal(isLocalService('http://localhost.example.com'), false);
});

test('an unparseable url withholds the root rather than guessing', () => {
  assert.equal(isLocalService('not a url'), false);
});

/**
 * The manifest's remote declaration travels with the registration (D47 §4): the sync loop feeds only
 * what the repository itself, under review, said may leave. Silence means local — explicitly false on
 * the wire, so a service never has to guess what an absent field meant.
 */
test('the registration carries the remote declaration, and silence means local', () => {
  const declared = { ...manifest, remote: { join: true, knowledge: true } } as Manifest;
  const body = registration('/srv/Repo', declared, 'Repo', 'https://daoris.example.com');
  assert.equal(body.join, true);
  assert.equal(body.shareKnowledge, true);

  const silent = registration('/srv/Repo', manifest, 'Repo', 'https://daoris.example.com');
  assert.equal(silent.join, false);
  assert.equal(silent.shareKnowledge, false);
});

/**
 * The two layers of remote defaulting — readManifest's normalization and registration's `?? false` —
 * were each proven alone and their agreement proven nowhere. A REAL manifest file goes through both
 * here, so neither layer can drift into emitting a combination the other refuses.
 */
test('a manifest read from disk and put on the wire says the same thing at both layers', () => {
  const fx = makeFixture('connect-layers');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
    remote: { join: true },
  }));

  const body = registration(fx.root, readManifest(fx.root), 'Repo', 'https://daoris.example.com');

  assert.equal(body.join, true);
  assert.equal(body.shareKnowledge, false);
  fx.cleanup();
});

/**
 * The FIX-LOG names `connect --dry-run` printing the remote declaration as the verification for the
 * stale-dist regression — until now a manual step. This is the same payload assertion, bin-independent.
 */
test('connect --dry-run prints the exact payload, remote declaration and root included', async () => {
  const fx = makeFixture('connect-dry-run');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
    remote: { join: true, knowledge: true },
  }));
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  const out: string[] = [];
  const code = await commandConnect({
    root: fx.root, argv: ['--dry-run'], write: (s: string) => out.push(s), packageRoot: '',
  });

  assert.equal(code, 0);
  const payload = JSON.parse(out.slice(0, -1).join('\n'));
  assert.equal(payload.join, true);
  assert.equal(payload.shareKnowledge, true);
  assert.equal(payload.root, fx.root);
  assert.match(out.at(-1)!, /would register/);

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

/**
 * Membership is WIRING, like a git remote (D48 as amended) — a statement to this machine's registry,
 * carried by no tracked file. A repository's workspace therefore travels on the registration and
 * nowhere else; the manifest keeps only what it always kept.
 */
test('--workspace is a wiring statement, and silence says nothing at all', () => {
  const wired = registration('/srv/Repo', manifest, 'Repo', 'http://localhost:5177', 'aurora');
  assert.equal(wired.workspace, 'aurora');

  // Silence must be ABSENT, not empty: an absent field preserves the existing row, and `""` would be
  // a statement that re-points every repository to the default on the next ordinary sync tick.
  const silent = registration('/srv/Repo', manifest, 'Repo', 'http://localhost:5177');
  assert.equal('workspace' in silent, false);
});

test('a remote service is told the workspace too — the wiring is what it registers under', () => {
  const body = registration('/srv/Repo', manifest, 'Repo', 'https://daoris.example.com', 'aurora');
  assert.equal(body.workspace, 'aurora');
  assert.equal('root' in body, false);
});

/**
 * The whole point of the git shape: no tracked file changes. A workspace written into the manifest
 * would travel to every clone, which breaks the fork and taxes contributors who never run Daoris.
 */
test('connect --workspace writes nothing into the repository', async () => {
  const fx = makeFixture('connect-workspace');
  const written = JSON.stringify({
    source: 's',
    domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
  });
  fx.write('daoris.json', written);
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  const out: string[] = [];
  const code = await commandConnect({
    root: fx.root, argv: ['--workspace', 'aurora', '--dry-run'],
    write: (s: string) => out.push(s), packageRoot: '',
  });

  assert.equal(code, 0);
  assert.equal(JSON.parse(out.slice(0, -1).join('\n')).workspace, 'aurora');
  assert.equal(readFileSync(join(fx.root, 'daoris.json'), 'utf8'), written);

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

/** `--workspace` with nothing after it is a mistake worth naming, not a silent default. */
test('connect --workspace with no name is refused', async () => {
  const fx = makeFixture('connect-workspace-bare');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
  }));
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  try {
    await commandConnect({
      root: fx.root, argv: ['--workspace'], write: () => {}, packageRoot: '',
    });
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.match(error.message, /--workspace/);
  }

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

/** Registering an empty declaration is refused as POLICY (exit 1) — the one exit-1 in connect. */
test('connect refuses a repository that has not said what it is', async () => {
  const fx = makeFixture('connect-undeclared');
  fx.write('daoris.json', '{"source":"s"}');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  try {
    await commandConnect({ root: fx.root, argv: [], write: () => {}, packageRoot: '' });
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.equal(error.exitCode, 1);
    assert.match(error.message, /domain/);
  }

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

/**
 * `connect` from a LINKED WORKTREE is refused, naming the main tree (D51/SURF3).
 *
 * A registration re-pointed at an ephemeral tree is the worst failure available here: everything
 * keeps working until the tree is removed, and then the repository's root is a path that does not
 * exist. Git itself marks a linked worktree — its `.git` is a FILE naming the main repository's
 * `.git/worktrees/<name>` — so the check reads one file and spawns nothing, which is what keeps the
 * CLI's no-spawn discipline intact (only `toolchain.ts` spawns).
 */
test('connect from a linked worktree is refused, naming the main tree', async () => {
  const fx = makeFixture('connect-linked');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'A test repo.', owns: ['itself'], accepts: ['a quest'] },
  }));
  fx.write('.git', 'gitdir: /srv/Repo/.git/worktrees/session-ab12cd34\n');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  try {
    await commandConnect({ root: fx.root, argv: ['--dry-run'], write: () => {}, packageRoot: '' });
    assert.fail('expected a refusal');
  } catch (error) {
    assert.ok(error instanceof DaorisError);
    assert.equal((error as DaorisError).exitCode, 1);
    assert.match((error as DaorisError).message, /linked worktree/);
    // The actionable half: WHERE to run connect instead.
    assert.match((error as DaorisError).message, /[/\\]srv[/\\]Repo/);
  }

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});

/**
 * A SUBMODULE also wears a `.git` file — `gitdir: ../.git/modules/<name>` — and a submodule is a
 * repository of its own, entirely registrable. Only the worktree marker refuses.
 */
test('a submodule checkout is not mistaken for a linked worktree', async () => {
  const fx = makeFixture('connect-submodule');
  fx.write('daoris.json', JSON.stringify({
    source: 's',
    domain: { summary: 'A submodule.', owns: ['itself'], accepts: ['a quest'] },
  }));
  fx.write('.git', 'gitdir: ../.git/modules/sub\n');
  process.env.DAORIS_SERVICE_URL = 'http://localhost:5177';

  const lines: string[] = [];
  const code = await commandConnect({
    root: fx.root, argv: ['--dry-run'], write: (line) => lines.push(line), packageRoot: '',
  });
  assert.equal(code, 0);

  delete process.env.DAORIS_SERVICE_URL;
  fx.cleanup();
});
