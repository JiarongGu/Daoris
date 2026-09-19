import { test } from 'node:test';
import assert from 'node:assert/strict';
import { registration, isLocalService } from '../src/connect.ts';
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
