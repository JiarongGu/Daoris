import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { regenerate, regenerations, inFolders, readFolders } from './generated.ts';

test('the size list is a generated file of the merge: its shrinks are written from the merged tree, never failed for', () => {
  const folders = readFolders(process.cwd(), (message) => new Error(message));
  const list = folders.find(({ folder }) => folder === 'tools/size-ratchet.json');
  assert.equal(list?.run, 'node tools/size-ratchet.mjs --shrink');
  assert.equal(inFolders('tools/size-ratchet.json', folders), true);
  assert.equal(inFolders('tools/size-ratchet.json.bak', folders), false);
  assert.ok(JSON.parse(readFileSync('daoris.gates.json', 'utf8')).generated.length >= 1);
});

test('each regeneration the chosen gates declare is run once, in the plan\'s order, and a gate declaring none adds none', () => {
  const gates = [
    { name: 'cli', regenerate: 'node tools/contracts.mjs --if-stale' },
    { name: 'comments' },
    { name: 'web-types', regenerate: 'node tools/contracts.mjs --if-stale' },
    { name: 'other', regenerate: 'node tools/other.mjs' },
  ];
  assert.deepEqual(regenerations(gates), [
    { command: 'node tools/contracts.mjs --if-stale', gates: ['cli', 'web-types'] },
    { command: 'node tools/other.mjs', gates: ['other'] },
  ]);
  assert.deepEqual(regenerations([{ name: 'comments' }]), []);
});

test('a stale generated file is regenerated first, so the gate that checks it finds it current', () => {
  const ran: string[] = [];
  const out = regenerate([{ command: 'gen --if-stale', gates: ['cli'] }], {
    run: (command) => { ran.push(command); return { status: 0, output: 'written 2 files' }; },
  });
  assert.deepEqual(ran, ['gen --if-stale']);
  assert.equal(out.ok, true);
  assert.match(out.lines[0]!, /regenerated before cli: gen --if-stale \(written 2 files\)/);
});

test('a regeneration that fails stops the merge\'s gates with its own words, never a red gate for a stale file', () => {
  const out = regenerate([{ command: 'gen', gates: ['cli'] }], { run: () => ({ status: 1, output: 'dotnet missing' }) });
  assert.equal(out.ok, false);
  assert.match(out.lines[0]!, /could not regenerate for cli: gen \(dotnet missing\)/);
});
