import { test } from 'node:test';
import assert from 'node:assert/strict';
import { summarise } from './ux-trail.mjs';

// The UX trail's terminal reader (UXTRAIL1): from a trail's entries, the time from each step waiting on the person to their press in
// its row, with the pages opened between, and the waits never pressed.

const at = (minute, second = 0) => `2026-11-16T09:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}Z`;

test('a wait is paired with the first press in its row, the pages between counted and named', () => {
  const summary = summarise([
    { kind: 'wait', row: 'step:q7/sign', since: at(50), at: at(50, 1) },
    { kind: 'page', page: '/overview', at: at(50, 10) },
    { kind: 'press', press: 'Open', page: '/overview', at: at(50, 20) },
    { kind: 'page', page: '/sessions', at: at(50, 40) },
    { kind: 'press', press: 'Approve…', row: 'step:q7/sign', page: '/sessions', at: at(51) },
    { kind: 'wait', row: 'review:s2', since: at(51, 30), at: at(51, 30) },
  ]);

  assert.deepEqual(summary.waits, [
    { row: 'step:q7/sign', seconds: 60, press: 'Approve…', pages: ['/overview', '/sessions'] },
    { row: 'review:s2', seconds: null, press: null, pages: [] },
  ]);
  assert.equal(summary.presses, 2);
  assert.equal(summary.pages, 2);
});
