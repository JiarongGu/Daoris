/**
 * The tooling's one atomic write (REFAC2, the 2026-10-07 second-opinion review's refactor 4):
 *
 *   node --test tools/fsx.test.mjs
 *
 * `npm run verify` runs it beside the orientation index's. Its scratch is a gitignored folder of this repository.
 */
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { writeAtomic } from './fsx.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const scratch = join(here, '..', 'local', 'scratch', 'fsx-test');
rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });
after(() => rmSync(scratch, { recursive: true, force: true }));

/** A rename that refuses with `code` for its first `refusals` calls, then moves the file, recording each call. */
function refusing(code, refusals) {
  const calls = [];
  return {
    calls,
    rename: (from, to) => {
      calls.push([from, to]);
      if (calls.length <= refusals) throw Object.assign(new Error(`${code}: held, rename`), { code });
      renameSync(from, to);
    },
  };
}

const folder = (name) => {
  const at = join(scratch, name);
  rmSync(at, { recursive: true, force: true });
  mkdirSync(at, { recursive: true });
  return at;
};

// ---------------------------------------------------------------------------------------------------------
// writeAtomic

test('a file is written whole into a folder made for it, replacing what was there, with nothing left beside it', () => {
  const at = folder('write');
  const file = join(at, 'deep', 'record.json');
  writeAtomic(file, '{"a":1}\n');
  assert.equal(readFileSync(file, 'utf8'), '{"a":1}\n');

  writeAtomic(file, 'x — 灵台\n');
  const bytes = readFileSync(file);
  assert.equal(bytes.toString('utf8'), 'x — 灵台\n');
  assert.equal(bytes[0], 0x78, 'UTF-8 with no BOM');
  writeAtomic(file, Buffer.from([0, 1, 2]));
  assert.deepEqual([...readFileSync(file)], [0, 1, 2], 'bytes as they are');
  assert.deepEqual(readdirSync(dirname(file)), ['record.json']);
});

test('a rename refused while something holds the file is tried again until it gives way', () => {
  const at = folder('held');
  const file = join(at, 'state.json');
  writeFileSync(file, 'old\n');
  for (const code of ['EPERM', 'EACCES', 'EBUSY']) {
    const held = refusing(code, 2);
    writeAtomic(file, `${code}\n`, { waitMs: 1, rename: held.rename });
    assert.equal(held.calls.length, 3, code);
    assert.equal(readFileSync(file, 'utf8'), `${code}\n`);
    assert.deepEqual(readdirSync(at), ['state.json'], code);
  }
});

test('each write goes beside under a name of its own, never a name another write would share', () => {
  const at = folder('names');
  const file = join(at, 'log.json');
  const seen = [];
  for (let i = 0; i < 3; i++) writeAtomic(file, `${i}\n`, { rename: (from, to) => { seen.push(from); renameSync(from, to); } });
  assert.equal(new Set(seen).size, 3);
  for (const beside of seen) {
    assert.equal(dirname(beside), at, 'beside the file, so the rename never crosses a volume');
    assert.notEqual(beside, `${file}.partial`);
  }
});

test('a failed rename leaves the file as it was and removes what was written beside it', () => {
  const at = folder('failed');
  const file = join(at, 'D1.md');
  writeFileSync(file, 'as it was\n');

  // Held past its tries: the last refusal is thrown.
  const held = refusing('EPERM', 10);
  assert.throws(() => writeAtomic(file, 'new\n', { tries: 3, waitMs: 1, rename: held.rename }), /EPERM/);
  assert.equal(held.calls.length, 3);
  assert.equal(readFileSync(file, 'utf8'), 'as it was\n');
  assert.deepEqual(readdirSync(at), ['D1.md'], 'nothing beside it for `git add -A` to stage');

  // Any other refusal: thrown at once.
  const other = refusing('EXDEV', 10);
  assert.throws(() => writeAtomic(file, 'new\n', { waitMs: 1, rename: other.rename }), /EXDEV/);
  assert.equal(other.calls.length, 1);
  assert.equal(readFileSync(file, 'utf8'), 'as it was\n');
  assert.deepEqual(readdirSync(at), ['D1.md']);
});
