import { test } from 'vitest';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { readReceipts, receiptsFolder } from './receipts.ts';
import { changesOf, checkRunOf, firstGates, keepRun, receiptOf, summaryInputOf, type Git } from './preflight-record.ts';

const result = (name: string, verdict: string, over: Record<string, unknown> = {}) => ({ gate: { name, run: `run ${name}` }, verdict, ms: 2000, note: '', ...over });

test('a gate\'s result becomes a receipt: PASS and FLAKE pass, ENDED proves nothing, anything else fails with its first line as the note', () => {
  const at = '2026-11-27T10:00:00Z';
  assert.equal(receiptOf(result('a', 'PASS'), { at, waitMs: 0 }).verdict, 'pass');
  assert.equal(receiptOf(result('a', 'FLAKE'), { at, waitMs: 0 }).verdict, 'pass');
  assert.equal(receiptOf(result('a', 'ENDED'), { at, waitMs: 0 }).verdict, 'ended');
  const failed = receiptOf(result('a', 'FAIL', { note: 'first line\nsecond' }), { at, waitMs: 3000 });
  assert.deepEqual([failed.verdict, failed.note, failed.waitMs, failed.command, failed.ms], ['fail', 'first line', 3000, 'run a', 2000]);
});

test('the run keeps the kept gates as replayed passes, the results with their waits, the trigger and the tree', () => {
  const run = checkRunOf({
    id: 'r', trigger: 'done', branch: 'b', tip: 't', tree: 'T', base: 'x', startedAt: '2026-11-27T10:00:00Z', wallMs: 9000, at: '2026-11-27T10:00:09Z',
    results: [result('censuses', 'PASS'), result('reached', 'FAIL', { note: 'RED y' })], waits: { reached: 4000 },
    kept: [{ name: 'code-map', why: 'its pass stands on this tree', run: 'dotnet run map --check' }],
  });
  assert.deepEqual(run.gates.map((each) => [each.unit, each.verdict, each.replayed, each.waitMs]), [['censuses', 'pass', false, 0], ['reached', 'fail', false, 4000], ['code-map', 'pass', true, 0]]);
  assert.equal(run.gates[2]?.command, 'dotnet run map --check');
  assert.equal(run.trigger, 'done');
  assert.equal(run.kind, 'preflight');
});

test('the censuses named first run first, so a census red is seen before any other gate runs', () => {
  const gates = ['reached', 'cli', 'censuses'].map((name) => ({ name, run: name }));
  assert.deepEqual(firstGates(gates, JSON.stringify({ verifyBudget: { preflightFirst: ['censuses'] } })).map((gate) => gate.name), ['censuses', 'reached', 'cli']);
  assert.deepEqual(firstGates(gates, JSON.stringify({})).map((gate) => gate.name), ['reached', 'cli', 'censuses']);
  assert.deepEqual(firstGates(gates, 'not json').map((gate) => gate.name), ['reached', 'cli', 'censuses'], 'a declaration that does not read leaves the order');
});

test('the changes are read from git\'s numstat, a binary file counting no lines', () => {
  const git: Git = (_cwd, args) => {
    assert.deepEqual(args, ['diff', '--numstat', 'a...b']);
    return { status: 0, out: '10\t2\ttools/a.ts\n-\t-\tlogo.png\n3\t0\tdocs/areas/x.md\n' };
  };
  assert.deepEqual(changesOf('root', 'a', 'b', git), [
    { path: 'tools/a.ts', added: 10, removed: 2 }, { path: 'logo.png', added: 0, removed: 0 }, { path: 'docs/areas/x.md', added: 3, removed: 0 },
  ]);
  assert.deepEqual(changesOf('root', 'a', 'b', () => ({ status: 128, out: '' })), [], 'a diff git cannot make is no change read');
});

test('the summary\'s input reads the area documents on both sides and the code map\'s modules, and names the deferred gates', () => {
  const git: Git = (_cwd, args) => {
    if (args[0] === 'diff') return { status: 0, out: '1\t1\tdocs/areas/x.md\n2\t0\ttools/a.ts\n' };
    const spec = args.at(-1);
    if (spec === 'base:docs/areas/x.md') return { status: 0, out: '1. Old.\n' };
    if (spec === 'tip:docs/areas/x.md') return { status: 0, out: '1. New.\n' };
    if (spec === 'tip:docs/code-map.json') return { status: 0, out: JSON.stringify({ modules: [{ id: 'tools', path: 'tools', summary: 's' }] }) };
    return { status: 128, out: '' };
  };
  const input = summaryInputOf({
    root: 'root', branch: 'b', base: 'base', tip: 'tip', git, receipts: [], required: [{ unit: 'cli', command: 'c' }],
    deferred: [{ name: 'driver-process', why: 'a Process half' }],
  });
  assert.deepEqual(input.modules, [{ id: 'tools', path: 'tools' }]);
  assert.deepEqual(input.areas, [{ path: 'docs/areas/x.md', before: '1. Old.\n', after: '1. New.\n' }]);
  assert.equal(input.changes.length, 2);
  assert.equal(input.deferred[0]?.name, 'driver-process');
});

test('a code map that does not read leaves no module, so every path is named as placed nowhere, never guessed', () => {
  const git: Git = (_cwd, args) => (args[0] === 'diff' ? { status: 0, out: '1\t0\ttools/a.ts\n' } : { status: 0, out: '{broken' });
  const input = summaryInputOf({ root: 'r', branch: 'b', base: 'x', tip: 'y', git, receipts: [], required: [], deferred: [] });
  assert.deepEqual(input.modules, []);
});

test('a preflight keeps its run: the receipts under the tree in the home, the measure appended, the summary record beside the brief, and the summary said', () => {
  const { dir, remove } = fixtureFolder('keep-run');
  try {
    const git: Git = (_cwd, args) => (args[0] === 'diff' ? { status: 0, out: '4\t1\ttools/a.ts\n' } : { status: 128, out: '' });
    const kept = keepRun({
      root: dir, checkout: dir, home: dir, scratch: join(dir, 'scratch'), git, id: 'r1', trigger: 'done', branch: 'b', base: 'base', tip: 'tip', tree: 'T', baseTip: 'main',
      startedAt: 1_000, now: () => 11_000, results: [result('censuses', 'PASS'), result('reached', 'FAIL', { note: 'RED y' })], waits: { reached: 500 },
      plan: [{ name: 'censuses', run: 'run censuses' }, { name: 'reached', run: 'run reached' }, { name: 'code-map', run: 'run code-map' }],
      set: { run: [{ name: 'censuses', why: '' }, { name: 'reached', why: '' }], kept: [{ name: 'code-map', why: 'its pass stands on this tree' }], deferred: [{ name: 'driver-process', why: 'a Process half' }] },
    });
    const receipts = readReceipts(receiptsFolder(dir, dir), 'T');
    assert.deepEqual(receipts.map((each) => [each.unit, each.verdict, each.replayed]).sort(), [['censuses', 'pass', false], ['code-map', 'pass', true], ['reached', 'fail', false]]);
    assert.equal(kept.run.wallMs, 10_000);
    assert.deepEqual(kept.summary.judgement.map((each) => each.kind).sort(), ['deferred', 'failed']);
    assert.equal(JSON.parse(readFileSync(join(dir, 'scratch', 'summary.json'), 'utf8')).tip, 'tip');
    assert.ok(kept.lines.some((line) => line.startsWith('Verified: 2 passed, 1 failed')));
  } finally {
    remove();
  }
});
