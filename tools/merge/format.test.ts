import { test } from 'vitest';
import assert from 'node:assert/strict';
import { classCount, count, pad } from './format.ts';

test('a column is padded to its width, and a word as wide or wider keeps one space after it', () => {
  assert.equal(pad('cli', 6), 'cli   ');
  assert.equal(pad('reached', 6), 'reached ');
  assert.equal(pad('web-rules', 9), 'web-rules ');
});

test('a count says its word in the singular for one and the plural otherwise, a class as classes', () => {
  assert.deepEqual([count(0, 'gate'), count(1, 'gate'), count(2, 'gate')], ['0 gates', '1 gate', '2 gates']);
  assert.deepEqual([classCount(1), classCount(3)], ['1 class', '3 classes']);
});
