// The test Daoris's plugin kit writes beside its storage.mjs: two processes writing one file at once keep every record either
// wrote, and a lock or a file another process left behind is taken over or refused as the store says. `node --test` runs it with
// nothing of Daoris's installed: it imports nothing but Node and the store beside it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { records } from './storage.mjs';

const store = pathToFileURL(join(dirname(fileURLToPath(import.meta.url)), 'storage.mjs')).href;

/** A file of the test's own, in a folder removed after. */
function scratch(t) {
  const at = mkdtempSync(join(tmpdir(), 'daoris-storage-'));
  t.after(() => rmSync(at, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }));
  return join(at, 'records.json');
}

/** A process writing `count` records of its own into `file` once `go` is there, so both begin together. */
function writer(file, go, who, count) {
  const script = `
    import { existsSync } from 'node:fs';
    import { records } from ${JSON.stringify(store)};
    const kept = records(${JSON.stringify(file)});
    while (!existsSync(${JSON.stringify(go)})) await new Promise((resolve) => setTimeout(resolve, 5));
    for (let n = 0; n < ${count}; n++) kept.put('rows', '${who}-' + n, { who: '${who}', n });
  `;
  const child = spawn(process.execPath, ['--input-type=module', '-e', script], { stdio: ['ignore', 'ignore', 'pipe'] });
  let said = '';
  child.stderr.on('data', (chunk) => { said += chunk; });
  return new Promise((resolve) => child.on('close', (code) => resolve({ code, said })));
}

test('two processes writing one file at once keep every record either wrote', async (t) => {
  const file = scratch(t);
  const go = `${file}.go`;
  const count = 150;
  const both = Promise.all([writer(file, go, 'a', count), writer(file, go, 'b', count)]);
  writeFileSync(go, '');
  for (const { code, said } of await both) assert.equal(code, 0, said);

  const rows = records(file).all('rows');
  assert.equal(Object.keys(rows).length, 2 * count, 'a record one process wrote was lost to the other\'s write');
  assert.deepEqual(rows['a-0'], { who: 'a', n: 0 });
  assert.deepEqual(rows[`b-${count - 1}`], { who: 'b', n: count - 1 });
});

test('a record is written and removed alone, the others kept', (t) => {
  const kept = records(scratch(t));
  kept.put('rows', 'one', 1);
  kept.put('rows', 'two', 2);
  kept.put('other', 'three', 3);
  kept.remove('rows', 'one');
  kept.remove('rows', 'none');

  assert.equal(kept.get('rows', 'one'), undefined);
  assert.deepEqual(kept.all('rows'), { two: 2 });
  assert.deepEqual(kept.read(), { rows: { two: 2 }, other: { three: 3 } });
});

test('a lock left by a process that has gone is taken over', (t) => {
  const file = scratch(t);
  const gone = spawnSync(process.execPath, ['-e', 'process.stdout.write(String(process.pid))'], { encoding: 'utf8' }).stdout;
  writeFileSync(`${file}.lock`, `${gone}\n`);

  records(file).put('rows', 'after', true);

  assert.deepEqual(records(file).all('rows'), { after: true });
});

test('a file that does not read is refused and kept as it is', (t) => {
  const file = scratch(t);
  writeFileSync(file, '{ "rows": ');

  assert.throws(() => records(file).put('rows', 'one', 1), /does not read/);
  assert.equal(readFileSync(file, 'utf8'), '{ "rows": ');
});
