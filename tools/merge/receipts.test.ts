import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { censusFirst, coverage, readReceipts, receiptsFolder, refusalAtDone, runsFile, writeRun, type CheckRun, type GateReceipt } from './receipts.ts';

const pass = (unit: string, over: Partial<GateReceipt> = {}): GateReceipt => ({
  unit, command: `run ${unit}`, verdict: 'pass', ms: 1000, waitMs: 0, replayed: false, at: '2026-11-27T10:00:00Z', ...over,
});
const run = (over: Partial<CheckRun> = {}): CheckRun => ({
  schema: 1, id: 'r1', kind: 'preflight', trigger: 'done', branch: 'b', tip: 'tip1', tree: 'tree1', base: 'base1',
  startedAt: '2026-11-27T10:00:00Z', wallMs: 5000, gates: [pass('censuses'), pass('reached')], ...over,
});

test('the receipts are kept in the home\'s checks folder, and in the checkout\'s local folder where there is no home', () => {
  assert.equal(receiptsFolder('/h', '/c').replaceAll('\\', '/'), '/h/data/checks/receipts');
  assert.equal(receiptsFolder(null, '/c').replaceAll('\\', '/'), '/c/local/receipts');
});

test('a run writes its receipts under its tree and appends its measure to the runs file, never replacing an earlier run', () => {
  const { dir, remove } = fixtureFolder('receipts');
  try {
    writeRun(dir, run());
    writeRun(dir, run({ id: 'r2', gates: [pass('cli', { ms: 2000 })] }));
    const read = readReceipts(dir, 'tree1');
    assert.deepEqual(read.map((each) => each.unit).sort(), ['censuses', 'cli', 'reached'], 'a later run adds to the tree\'s receipts');
    const measure = readFileSync(runsFile(dir), 'utf8').trim().split('\n').map((line) => JSON.parse(line) as CheckRun);
    assert.deepEqual(measure.map((each) => each.id), ['r1', 'r2']);
    assert.equal(measure[0]?.gates.length, 2);
    assert.equal(readReceipts(dir, 'other').length, 0);
  } finally {
    remove();
  }
});

test('a later receipt of a unit replaces the earlier one on the same tree, a failure included, so a fixed red reads green', () => {
  const { dir, remove } = fixtureFolder('receipts');
  try {
    writeRun(dir, run({ gates: [pass('censuses', { verdict: 'fail', note: 'RED a.cs' })] }));
    assert.equal(readReceipts(dir, 'tree1')[0]?.verdict, 'fail');
    writeRun(dir, run({ id: 'r2', gates: [pass('censuses')] }));
    assert.equal(readReceipts(dir, 'tree1').find((each) => each.unit === 'censuses')?.verdict, 'pass');
  } finally {
    remove();
  }
});

test('a receipt file that does not read is no receipt, and the measure file is never lost to it', () => {
  const { dir, remove } = fixtureFolder('receipts');
  try {
    writeRun(dir, run());
    assert.ok(existsSync(join(dir, 'tree1.json')));
    writeRun(dir, run({ tree: 'tree2' }));
    assert.equal(readReceipts(dir, 'missing-tree').length, 0);
  } finally {
    remove();
  }
});

test('coverage names each required command with no receipt and each whose receipt failed; a replayed pass covers', () => {
  const required = [{ unit: 'censuses', command: 'dotnet test --filter Category=Census' }, { unit: 'reached', command: 'node tools/reached-checks.mjs --tier fast' }, { unit: 'cli', command: 'npm run verify:checks' }];
  const seen = coverage(required, [pass('censuses', { replayed: true }), pass('reached', { verdict: 'fail' })]);
  assert.deepEqual(seen.missing.map((each) => each.unit), ['cli']);
  assert.deepEqual(seen.failed.map((each) => each.unit), ['reached']);
  assert.deepEqual(seen.covered.map((each) => each.unit), ['censuses']);
});

test('an ended or unfinished unit is no pass: it is missing, never covered', () => {
  const seen = coverage([{ unit: 'cli', command: 'npm run verify:checks' }], [pass('cli', { verdict: 'ended' })]);
  assert.deepEqual(seen.missing.map((each) => each.unit), ['cli']);
});

test('the refusal at done is one line per command, naming the command to run, and null where every required unit passed', () => {
  const required = [{ unit: 'cli', command: 'npm run verify:checks' }, { unit: 'reached', command: 'node tools/reached-checks.mjs --tier fast' }];
  assert.equal(refusalAtDone(required, [pass('cli'), pass('reached')], 'node tools/merge-branch.mjs --preflight'), null);
  const said = refusalAtDone(required, [pass('cli', { verdict: 'fail' })], 'node tools/merge-branch.mjs --preflight');
  assert.ok(said);
  assert.match(said, /`npm run verify:checks` failed/);
  assert.match(said, /`node tools\/reached-checks\.mjs --tier fast` has no receipt/);
  assert.match(said, /`node tools\/merge-branch\.mjs --preflight`/);
});

test('the censuses run first, the others in the order given, and a unit no list names keeps its place', () => {
  const gates = ['reached', 'cli', 'censuses', 'comments', 'test-map'].map((name) => ({ name }));
  assert.deepEqual(censusFirst(gates, ['censuses', 'test-map']).map((gate) => gate.name), ['censuses', 'test-map', 'reached', 'cli', 'comments']);
  assert.deepEqual(censusFirst(gates, []).map((gate) => gate.name), gates.map((gate) => gate.name), 'with none declared the order stands');
  assert.deepEqual(censusFirst(gates, ['absent']).map((gate) => gate.name), gates.map((gate) => gate.name));
});
