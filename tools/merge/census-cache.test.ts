import { test } from 'vitest';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readFileSync, utimesSync } from 'node:fs';
import { join } from 'node:path';
import { fixtureFolder } from './fixture-home.ts';
import { CENSUS_SCHEMA, cachedCensus, censusKey, treeSide } from './census-cache.ts';

type Map_ = { units: string[]; problems: string[]; ts: { reader: string } };

test('the census is kept by its content tree and the schema of what is kept: the same tree is the same key, any other tree or schema another', () => {
  assert.equal(censusKey('a1b2'), censusKey('a1b2'));
  assert.notEqual(censusKey('a1b2'), censusKey('a1b3'));
  assert.notEqual(censusKey('a1b2', CENSUS_SCHEMA), censusKey('a1b2', CENSUS_SCHEMA + 1));
});

test('what the plan derives of a tree is read back on the same tree only, and a tree git could not name keeps nothing', () => {
  const { dir, remove } = fixtureFolder('census-side');
  try {
    treeSide(dir, 't1').save({ candidates: { 'driver|src/T': { files: ['a'], placed: [{}] } } });
    assert.deepEqual(treeSide(dir, 't1').value, { candidates: { 'driver|src/T': { files: ['a'], placed: [{}] } } });
    assert.deepEqual(treeSide(dir, 't2').value, {});
    treeSide(dir, null).save({ x: 1 });
    assert.deepEqual(readdirSync(dir).filter((name) => name.startsWith('reuse-')).length, 1);
  } finally {
    remove();
  }
});

test('a census read once is read from its cache on the same tree, its readers given back; another tree reads it again, and only the newest few stay', () => {
  const { dir, remove } = fixtureFolder('census-cache');
  try {
    let computed = 0;
    const read = (tree: string | null) => cachedCensus<Map_>({
      dir, tree, keep: ['units', 'problems'],
      compute: () => { computed += 1; return { units: [`unit-${tree}`], problems: [], ts: { reader: 'computed' } }; },
      revive: (saved) => ({ units: saved.units ?? [], problems: saved.problems ?? [], ts: { reader: 'revived' } }),
    });
    const first = read('t1');
    assert.equal(first.hit, false);
    assert.equal(computed, 1);
    assert.deepEqual(JSON.parse(readFileSync(join(dir, censusKey('t1')), 'utf8')), { units: ['unit-t1'], problems: [] }, 'only the fields named are kept');

    const again = read('t1');
    assert.equal(again.hit, true);
    assert.equal(computed, 1, 'the same tree is not read again');
    assert.deepEqual(again.map, { units: ['unit-t1'], problems: [], ts: { reader: 'revived' } });

    assert.equal(read('t2').hit, false, 'another tree is read');
    assert.equal(read(null).hit, false);
    assert.equal(read(null).hit, false, 'a tree git could not name is read every time');
    assert.equal(computed, 4);
    const age = (tree: string, day: number) => utimesSync(join(dir, censusKey(tree)), new Date(Date.UTC(2020, 0, day)), new Date(Date.UTC(2020, 0, day)));
    age('t1', 1);
    age('t2', 2);
    read('t3');
    age('t3', 3);
    read('t4');
    assert.deepEqual(readdirSync(dir).filter((name) => name.endsWith('.json')).sort(), ['t2', 't3', 't4'].map((tree) => censusKey(tree)).sort());
    assert.ok(!existsSync(join(dir, censusKey('t1'))), 'the oldest goes');
  } finally {
    remove();
  }
});
