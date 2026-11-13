import { test } from 'vitest';
import assert from 'node:assert/strict';
import { nowIso, when } from './clock.ts';

test('a summary says a moment to the minute in UTC, and anything that is no moment as an unknown time', () => {
  assert.equal(when('2026-11-13T08:05:59Z'), '2026-11-13 08:05 UTC');
  assert.equal(when('yesterday'), 'an unknown time');
  assert.equal(when(undefined), 'an unknown time');
});

test('a record\'s instant is written to the second, with no fraction', () => {
  assert.match(nowIso(), /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/);
});
