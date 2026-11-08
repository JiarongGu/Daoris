/**
 * Where a template literal ends (`tools/js-template.mjs`).
 *
 *   node --test tools/js-template.test.mjs
 */
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { templateEnd } from './js-template.mjs';

const endOf = (text) => templateEnd(`${text}!`, 0);

test('a template ends at its closing quote, past an escaped one', () => {
  assert.equal(endOf('`a`'), 3);
  assert.equal(endOf('`a\\`b`'), 6);
});

test('an expression holds braces, strings, comments and templates of its own', () => {
  assert.equal(endOf('`${{ k: 1 }.k} x`'), 17);
  assert.equal(endOf('`${f(`}`)}`'), 11);
  assert.equal(endOf("`${'}'}`"), 8);
  assert.equal(endOf('`${a /* } */}`'), 14);
  assert.equal(endOf('`${a // }\n}`'), 12);
});

test('a template that never closes ends with the source', () => {
  assert.equal(templateEnd('`${a', 0), 4);
  assert.equal(templateEnd('`abc', 0), 4);
});

test('a pattern in an expression is no comment, nor a quote or brace in it a string or a block', () => {
  assert.equal(endOf("`${a.replace(/^.*\\//, '')}`"), 27);
  assert.equal(endOf('`${a.replace(/[}\'"`]/g, 1)}`'), 28);
  assert.equal(endOf('`${a / 2} ${b / 3}`'), 19);
});

test('the comments of an expression are handed back', () => {
  const found = [];
  const src = '`${a // one\n} ${/* two */ b}`';
  templateEnd(src, 0, found);
  assert.deepEqual(found.map(([from, to]) => src.slice(from, to)), ['// one', '/* two */']);
});
