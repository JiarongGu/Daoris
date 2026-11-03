import { test } from 'node:test';
import assert from 'node:assert/strict';
import { BUDGET, checkSizes, scanSizes, sizeOf } from './size-ratchet.mjs';

const big = 'x'.repeat(BUDGET + 10);

test('CODESHAPE1: a source file over the budget is found, with its LF size, and the rest are not', () => {
  const files = { 'src/a/Big.cs': big, 'src/a/Small.cs': 'x', 'docs/Big.md': big, 'src/a/obj/Gen.cs': big };
  const found = scanSizes('.', { files: Object.keys(files), read: (path) => files[path] });
  assert.deepEqual(found, { 'src/a/Big.cs': BUDGET + 10 });
  assert.equal(sizeOf('a\r\nb'), 3);
});

test('CODESHAPE1: a listed file may not grow, a new one may not pass the budget, and the list only tightens', () => {
  assert.deepEqual(checkSizes({ 'a.cs': 50000 }, { 'a.cs': 50000 }), []);
  assert.match(checkSizes({ 'a.cs': 50001 }, { 'a.cs': 50000 })[0], /grew from 50000 to 50001/);
  assert.match(checkSizes({ 'b.cs': 50000 }, {})[0], /over the \d+-byte budget/);
  assert.match(checkSizes({ 'a.cs': 49000 }, { 'a.cs': 50000 })[0], /shrank/);
  assert.match(checkSizes({}, { 'a.cs': 50000 })[0], /under the budget now or gone/);
});
