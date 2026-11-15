import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { test } from 'node:test';
import { census } from './layers-census.mjs';
import { quickSteps } from './quick/steps.mjs';
import { checkTimes, scanRecords } from './time-ratchet.mjs';
import { judge, readVerdicts, say, writeVerdicts } from './quick/verdicts.mjs';
import { runQuick } from './quick.mjs';

const scratch = () => {
  mkdirSync('_fixtures', { recursive: true });
  return mkdtempSync(join('_fixtures', 'quick-'));
};

test('the steps are the index, the contracts, each check of verify:checks once but the time ratchet, and the census\'s counts', () => {
  const root = scratch();
  try {
    writeFileSync(join(root, 'package.json'), JSON.stringify({ scripts: { 'verify:checks': 'node a.mjs &&npm run b && node tools/time-ratchet.mjs && node c.mjs && node a.mjs' } }));
    assert.deepEqual(quickSteps(root), [
      'node tools/orient-index.mjs', 'node tools/contracts.mjs --if-stale', 'node a.mjs', 'npm run b', 'node c.mjs', 'node tools/layers-census.mjs --counts',
    ]);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red time record on disk leaves quick green, and the gates that judge time still read it red', async () => {
  const root = scratch();
  try {
    writeFileSync(join(root, 'package.json'), JSON.stringify({ scripts: { 'verify:checks': 'node tools/time-ratchet.mjs && node fine.mjs' } }));
    mkdirSync(join(root, 'tools'), { recursive: true });
    writeFileSync(join(root, 'tools', 'layers-census.json'), '{}');
    const records = join(root, '_fixtures', 'test-timing');
    mkdirSync(records, { recursive: true });
    writeFileSync(join(records, 'driver.trx'), '<TestRun><Results><UnitTestResult testName="Ns.Loaded.slow" duration="00:01:40.0000000" outcome="Passed" /></Results></TestRun>');
    const tree = { files: [], read: () => '' };
    const exits = {
      'node tools/time-ratchet.mjs': () => (checkTimes(scanRecords(records, root).over, {}).length > 0 ? 1 : 0),
      'node tools/layers-census.mjs': () => census({ root, argv: ['--records', records], tree }),
      'node tools/layers-census.mjs --counts': () => census({ root, argv: ['--counts'], tree }),
    };
    const steps = async (list) => Object.fromEntries(await Promise.all(list.map(async (step) => [step, (await exits[step]?.()) ?? 0])));
    const ran = await steps(quickSteps(root));
    const run = (command) => ({ status: ran[command], output: '' });
    const lines = [];
    assert.equal(runQuick({ root, steps: quickSteps(root), run, say: (line) => lines.push(line), now: () => 0 }), 0, lines.join('\n'));
    assert.match(lines.join('\n'), /judges no test time/);
    const gates = await steps(['node tools/time-ratchet.mjs', 'node tools/layers-census.mjs']);
    assert.deepEqual(gates, { 'node tools/time-ratchet.mjs': 1, 'node tools/layers-census.mjs': 1 }, 'the record is red to both time judgements');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red step does not stop the steps after it, and every red is said', () => {
  const root = scratch();
  try {
    const seen = [];
    const lines = [];
    const run = (command) => {
      seen.push(command);
      return command === 'one' || command === 'three' ? { status: 1, output: `${command} broke\n` } : { status: 0, output: '' };
    };
    const code = runQuick({ root, steps: ['one', 'two', 'three'], run, say: (line) => lines.push(line), now: () => 0 });
    assert.equal(code, 1);
    assert.deepEqual(seen, ['one', 'two', 'three']);
    assert.match(lines.join('\n'), /one broke/);
    assert.match(lines.join('\n'), /3 steps, 2 red/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a red the base already stood in is said as the base\'s, and any other as new', () => {
  const base = { head: 'b', at: 't', steps: { one: 'fail', two: 'pass' } };
  const verdicts = { one: 'fail', two: 'fail', three: 'fail', four: 'pass' };
  assert.deepEqual(judge(verdicts, base), { new: ['two', 'three'], base: ['one'], unknown: [] });
  assert.deepEqual(judge({ one: 'fail' }, null), { new: [], base: [], unknown: ['one'] });
  const text = say({ new: ['two'], base: ['one'], unknown: [] }).join('\n');
  assert.match(text, /NEW +two/);
  assert.match(text, /RED ON THE BASE +one/);
});

test('the base is recorded by --record-base and read back by a later run', () => {
  const root = scratch();
  try {
    const run = (command) => (command === 'one' ? { status: 1, output: 'no\n' } : { status: 0, output: '' });
    const lines = [];
    assert.equal(runQuick({ root, steps: ['one', 'two'], run, say: () => {}, now: () => 0, recordBase: true }), 1);
    assert.deepEqual(readVerdicts(join(root, 'local', 'quick-base.json'))?.steps, { one: 'fail', two: 'pass' });
    const later = (command) => ({ status: command === 'two' || command === 'one' ? 1 : 0, output: '' });
    runQuick({ root, steps: ['one', 'two'], run: later, say: (line) => lines.push(line), now: () => 0 });
    assert.match(lines.join('\n'), /NEW +two/);
    assert.match(lines.join('\n'), /RED ON THE BASE +one/);
    assert.deepEqual(JSON.parse(readFileSync(join(root, 'local', 'quick-base.json'), 'utf8')).steps, { one: 'fail', two: 'pass' }, 'a later run never rewrites the base');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('a verdicts file that does not read is no base', () => {
  const root = scratch();
  try {
    writeFileSync(join(root, 'bad.json'), '{');
    assert.equal(readVerdicts(join(root, 'bad.json')), null);
    assert.equal(readVerdicts(join(root, 'absent.json')), null);
    writeVerdicts(join(root, 'ok.json'), { head: 'h', at: 'a', steps: { x: 'pass' } });
    assert.equal(readVerdicts(join(root, 'ok.json'))?.steps.x, 'pass');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
