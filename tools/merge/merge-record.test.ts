import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { appendRecord, buildRecord, readRecords, summarize, type GateRecord } from './merge-record.ts';

const gate = (name: string, over: Partial<GateRecord> = {}): GateRecord => ({
  name, verdict: 'PASS', ms: 1000, replayed: false, mainRed: false, abnormal: false, slowest: [], over: false, left: [], ...over,
});
const base = { branch: 'b1', at: '2026-11-13T10:00:00Z', ms: 5000, mainReds: [], previous: [] };

test('a first run that passes is a first pass; a run after another of the branch is not', () => {
  const first = buildRecord({ ...base, gates: [gate('cli'), gate('reached', { verdict: 'FLAKE' })] });
  assert.equal(first.firstPass, true);
  assert.equal(first.attempt, 1);
  const second = buildRecord({ ...base, gates: [gate('cli')], previous: [first] });
  assert.equal(second.attempt, 2);
  assert.equal(second.firstPass, false);
});

test('a failure is a pass of the branch only when it is main\'s red alone', () => {
  const mainOnly = buildRecord({ ...base, gates: [gate('reached', { verdict: 'FAIL', mainRed: true })] });
  assert.equal(mainOnly.passed, true);
  const brought = buildRecord({ ...base, gates: [gate('reached', { verdict: 'FAIL' })] });
  assert.equal(brought.passed, false);
  assert.equal(brought.firstPass, false);
});

test('the record carries each gate\'s time, its slowest classes, and the gates and merge over budget', () => {
  const record = buildRecord({ ...base, ms: 11 * 60_000, gates: [gate('reached', { ms: 6 * 60_000, slowest: [{ name: 'X', ms: 1, tests: 1 }] }), gate('cli')] });
  assert.deepEqual(record.overBudget, { gates: ['reached'], merge: true });
  assert.equal(record.gates[0]!.over, true);
  assert.equal(record.gates[0]!.slowest[0]!.name, 'X');
});

test('records append one line each, read back in order, a torn line skipped', () => {
  const dir = mkdtempSync(join(tmpdir(), 'merge-record-'));
  try {
    const file = join(dir, 'sub', 'merge-record.jsonl');
    assert.deepEqual(readRecords(file), []);
    appendRecord(file, buildRecord({ ...base, gates: [gate('cli')] }));
    appendRecord(file, buildRecord({ ...base, branch: 'b2', gates: [gate('cli')] }));
    assert.deepEqual(readRecords(file).map((each) => each.branch), ['b1', 'b2']);
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('the summary counts runs, first passes, gate time, reds set down as main\'s and gates over budget', () => {
  const a = buildRecord({ ...base, gates: [gate('cli', { ms: 100 }), gate('reached', { ms: 400_000 })], ms: 500_000 });
  const b = buildRecord({ ...base, branch: 'b2', gates: [gate('cli', { ms: 300, replayed: true }), gate('reached', { verdict: 'FAIL', mainRed: true, ms: 200 })], mainReds: [{ gate: 'reached', unit: 'X', key: 'k' }] });
  const sum = summarize([a, b]);
  assert.equal(sum.runs, 2);
  assert.equal(sum.firstPass, 2);
  assert.equal(sum.mainReds, 1);
  assert.equal(sum.replayed, 1);
  assert.equal(sum.gates.cli, 400);
  assert.deepEqual(sum.overBudget, ['reached']);
});
