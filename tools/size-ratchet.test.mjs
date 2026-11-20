import { test } from 'node:test';
import assert from 'node:assert/strict';
import { BUDGET, checkSizes, lowered, scanSizes, settleStaged, sizeOf } from './size-ratchet.mjs';

const big = 'x'.repeat(BUDGET + 10);

test('CODESHAPE1: a source file over the budget is found, with its LF size, and the rest are not', () => {
  const files = { 'src/a/Big.cs': big, 'src/a/Small.cs': 'x', 'docs/Big.md': big, 'src/a/obj/Gen.cs': big };
  const found = scanSizes('.', { files: Object.keys(files), read: (path) => files[path] });
  assert.deepEqual(found, { 'src/a/Big.cs': BUDGET + 10 });
  assert.equal(sizeOf('a\r\nb'), 3);
});

test('a file does not pass the size main\'s list gives it, whatever its own list says', () => {
  const own = { 'a.cs': 50500 };
  assert.deepEqual(checkSizes({ 'a.cs': 50500 }, own, { 'a.cs': 50500 }), []);
  assert.match(checkSizes({ 'a.cs': 50500 }, own, { 'a.cs': 50000 })[0], /grew from 50000 to 50500/);
  assert.match(checkSizes({ 'a.cs': 50500 }, own, {})[0], /over the \d+-byte budget/);
  assert.match(checkSizes({ 'a.cs': 49900 }, own, { 'a.cs': 50000 })[0], /shrank/);
});

test('CODESHAPE1: a listed file may not grow, a new one may not pass the budget, and the list only tightens', () => {
  assert.deepEqual(checkSizes({ 'a.cs': 50000 }, { 'a.cs': 50000 }), []);
  assert.match(checkSizes({ 'a.cs': 50001 }, { 'a.cs': 50000 })[0], /grew from 50000 to 50001/);
  assert.match(checkSizes({ 'b.cs': 50000 }, {})[0], /over the \d+-byte budget/);
  assert.match(checkSizes({ 'a.cs': 49000 }, { 'a.cs': 50000 })[0], /shrank/);
  assert.match(checkSizes({}, { 'a.cs': 50000 })[0], /under the budget now or gone/);
});

test('a merge lowers the list to the tree: a shrunk entry is cut, one under the budget or gone leaves, a grown or new file is left to fail', () => {
  const list = { 'a.cs': 50000, 'b.cs': 50000, 'c.cs': 50000, 'd.cs': 50000 };
  const found = { 'a.cs': 49000, 'b.cs': 50000, 'd.cs': 50500, 'e.cs': 60000 };
  assert.deepEqual(lowered(list, found), { 'a.cs': 49000, 'b.cs': 50000, 'd.cs': 50000 });
});

test('a commit settles the staged files against the list: shrinks are written, growth and a new file over the budget are refused, naming the file', () => {
  const list = { 'a.cs': 50000, 'b.cs': 50000, 'c.cs': 50000 };
  const sizes = { 'a.cs': 49000, 'b.cs': 100, 'c.cs': null, 'd.cs': 10 };
  assert.deepEqual(settleStaged(sizes, list, list), { list: { 'a.cs': 49000 }, problems: [] });
  const refused = settleStaged({ 'a.cs': 50001, 'e.cs': BUDGET + 1 }, list, list);
  assert.deepEqual(refused.list, list);
  assert.match(refused.problems[0], /^a\.cs grew from 50000 to 50001/);
  assert.match(refused.problems[1], /^e\.cs is \d+ bytes, over the \d+-byte budget/);
  assert.match(settleStaged({ 'a.cs': 50200 }, { 'a.cs': 50200 }, { 'a.cs': 50000 }).problems[0], /grew from 50000 to 50200/);
});
