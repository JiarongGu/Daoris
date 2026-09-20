import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { commandConnect, registration, isLocalService } from '../src/connect.ts';
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
  const body = registration('/home/dev/Repo', manifest, 'Repo', 'http://localhost:5177');

  assert.equal(body.root, '/home/dev/Repo');
  assert.equal(body.repository, 'Repo');
});

test('a remote service is not told the root', () => {
  const body = registration('/home/dev/Repo', manifest, 'Repo', 'https://daoris.example.com');

  assert.equal('root' in body, false);
});

test('loopback is the boundary, in every spelling', () => {
  assert.equal(isLocalService('http://localhost:5177'), true);
  assert.equal(isLocalService('http://127.0.0.1:5177'), true);
  assert.equal(isLocalService('http://[::1]:5177'), true);
  assert.equal(isLocalService('https://daoris.example.com'), false);
  assert.equal(isLocalService('http://192.168.1.10:5177'), false);
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
  const body = registration('/home/dev/Repo', declared, 'Repo', 'https://daoris.example.com');
  assert.equal(body.join, true);
  assert.equal(body.shareKnowledge, true);

  const silent = registration('/home/dev/Repo', manifest, 'Repo', 'https://daoris.example.com');
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
  const wired = registration('/home/dev/Repo', manifest, 'Repo', 'http://localhost:5177', 'aurora');
  assert.equal(wired.workspace, 'aurora');

  // Silence must be ABSENT, not empty: an absent field preserves the existing row, and `""` would be
  // a statement that re-points every repository to the default on the next ordinary sync tick.
  const silent = registration('/home/dev/Repo', manifest, 'Repo', 'http://localhost:5177');
  assert.equal('workspace' in silent, false);
});

test('a remote service is told the workspace too — the wiring is what it registers under', () => {
  const body = registration('/home/dev/Repo', manifest, 'Repo', 'https://daoris.example.com', 'aurora');
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
