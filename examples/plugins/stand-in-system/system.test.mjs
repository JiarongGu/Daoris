import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdirSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { apply, keep, plan, read } from './system.mjs';

const folder = dirname(fileURLToPath(import.meta.url));

function scratch(name) {
  const data = join(folder, '..', '..', '..', '_fixtures', `stand-in-system-${name}-${process.pid}`);
  rmSync(data, { recursive: true, force: true });
  mkdirSync(data, { recursive: true });
  return data;
}

test('a plan types each change and names the version it read', () => {
  const state = apply({ items: {}, serial: 0 }, plan({ items: {}, serial: 0 }, [{ resource: 'release', value: '1' }]).operations).state;
  const { operations } = plan(state, [
    { resource: 'release', value: '2' },
    { resource: 'owner', value: 'team' },
    { resource: 'missing', remove: true },
    { resource: 'unchanged', value: '1' },
  ]);
  assert.deepEqual(operations.map((operation) => [operation.resource, operation.op, operation.before, operation.after]), [
    ['release', 'update', '1', '2'],
    ['owner', 'create', null, 'team'],
    ['unchanged', 'create', null, '1'],
  ]);
  assert.equal(operations[0].precondition.version, state.items.release.etag);
  assert.equal(operations[1].precondition.version, null);
});

test('an apply writes every operation under its etag, and a plan read before a change is refused as stale', () => {
  const empty = { items: {}, serial: 0 };
  const first = apply(empty, plan(empty, [{ resource: 'release', value: '1' }]).operations);
  assert.deepEqual(first.answer, { state: 'applied', applied: ['release'] });

  const planned = plan(first.state, [{ resource: 'release', value: '2' }]);
  const moved = apply(first.state, plan(first.state, [{ resource: 'release', value: 'by hand' }]).operations).state;
  const stale = apply(moved, planned.operations);
  assert.equal(stale.answer.state, 'stale');
  assert.equal(stale.answer.refused, 'release');
  assert.deepEqual(stale.answer.present, [{ resource: 'release', exists: true, value: 'by hand', version: moved.items.release.etag }]);
  assert.equal(stale.state, moved, 'a stale plan changes nothing');

  const again = apply(moved, plan(moved, [{ resource: 'release', value: '2' }]).operations);
  assert.equal(again.answer.state, 'applied');
  assert.equal(again.state.items.release.value, '2');
});

test('an apply of what the store already holds, or of an empty plan, writes nothing and answers none', () => {
  const empty = { items: {}, serial: 0 };
  const planned = plan(empty, [{ resource: 'release', value: '1' }]).operations;
  const applied = apply(empty, planned).state;

  const twice = apply(applied, planned);
  assert.deepEqual(twice.answer, { state: 'none', applied: [] });
  assert.equal(twice.state, applied, 'nothing is written');
  assert.deepEqual(apply(applied, []).answer, { state: 'none', applied: [] });
  assert.equal(apply(applied, plan(applied, [{ resource: 'gone', remove: true }]).operations).answer.state, 'none');
});

test('the adapter answers both points on the hook wire, keeping the store in its data folder', async () => {
  const data = scratch('wire');
  keep(data, { items: { release: { value: '1', etag: 'e1' } }, serial: 1 });
  const child = spawn(process.execPath, [join(folder, 'hooks.mjs')], { env: { ...process.env, DAORIS_PLUGIN_DATA: data } });
  const answers = createInterface({ input: child.stdout })[Symbol.asyncIterator]();
  const ask = async (id, method, params) => {
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
    return JSON.parse((await answers.next()).value);
  };

  try {
    const hello = await ask(1, 'initialize', { protocolVersion: 1, points: ['plan/read', 'apply/perform'] });
    assert.deepEqual(hello.result.points, ['plan/read', 'apply/perform']);

    const planned = await ask(2, 'hook/plan/read', { system: 'stand-in', changes: [{ resource: 'release', value: '2' }] });
    assert.deepEqual(planned.result.operations, [
      { resource: 'release', op: 'update', before: '1', after: '2', precondition: { kind: 'etag', version: 'e1' } },
    ]);

    const applied = await ask(3, 'hook/apply/perform', { system: 'stand-in', operations: planned.result.operations });
    assert.deepEqual(applied.result, { state: 'applied', applied: ['release'] });
    assert.equal(read(data).items.release.value, '2');

    const twice = await ask(4, 'hook/apply/perform', { system: 'stand-in', operations: planned.result.operations });
    assert.deepEqual(twice.result, { state: 'none', applied: [] });

    keep(data, { items: { release: { value: 'by hand', etag: 'e-hand' } }, serial: 5 });
    const stale = await ask(5, 'hook/apply/perform', { system: 'stand-in', operations: planned.result.operations });
    assert.equal(stale.result.state, 'stale');
  } finally {
    child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method: 'shutdown' })}\n`);
    child.stdin.end();
    rmSync(data, { recursive: true, force: true });
  }
});
