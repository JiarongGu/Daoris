import { test } from 'node:test';
import assert from 'node:assert/strict';
import { inRange, measures, summarise } from './ux-trail.mjs';

// The UX trail's terminal reader:from a trail's entries, the time from each step waiting on the person to their press in
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

// The trial's five measures (design §17), read from a fixture trail over two days: an answer in the morning, a return after
// three hours with three waits open, one of them outside the owner's kinds, one answered after its question changed, and
// one that cleared on its own.
const day = (d, h, m, s = 0) => `2026-11-${d}T${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}Z`;
const fixture = [
  { kind: 'start', page: '/overview', at: day(16, 9, 0) },
  { kind: 'wait', row: 'go-ahead:a1', since: day(16, 8, 55), at: day(16, 9, 0, 1) },
  { kind: 'press', press: 'Allow', row: 'go-ahead:a1', page: '/overview', at: day(16, 9, 1) },
  { kind: 'page', page: '/overview', from: '/sessions', at: day(16, 12, 0) },
  { kind: 'wait', row: 'step:q7/sign', since: day(16, 10, 0), at: day(16, 12, 0, 5) },
  { kind: 'wait', row: 'quiet:s9', since: day(16, 11, 0), at: day(16, 12, 0, 5) },
  { kind: 'wait', row: 'go-ahead:a2', since: day(16, 11, 30), at: day(16, 12, 0, 5) },
  { kind: 'page', page: '/sessions', from: '/overview', at: day(16, 12, 1) },
  { kind: 'press', press: 'Approve…', row: 'step:q7/sign', page: '/sessions', at: day(16, 12, 2) },
  { kind: 'cleared', row: 'quiet:s9', at: day(16, 12, 3) },
  { kind: 'changed', row: 'go-ahead:a2', at: day(16, 12, 4) },
  { kind: 'press', press: 'Allow', row: 'go-ahead:a2', page: '/sessions', at: day(16, 12, 5) },
  { kind: 'wait', row: 'trust:t1', since: day(16, 12, 6), at: day(16, 12, 6) },
  { kind: 'cleared', row: 'trust:t1', at: day(16, 12, 7) },
  { kind: 'page', page: '/agents', from: '/sessions', at: day(17, 9, 0) },
];

test('a return after two hours or more is timed to the last of its open waits answered', () => {
  assert.deepEqual(measures(fixture).returns, [{ at: day(16, 12, 0), open: 3, answered: 2, seconds: 300 }]);
});

test('screens opened per answer count the pages since the answer before, the return or the wait', () => {
  const { screens } = measures(fixture);
  assert.deepEqual(screens.perAnswer, [0, 1, 0]);
  assert.equal(screens.withoutOpening, 2);
  assert.equal(screens.answers, 3);
});

test('waits outside the owner kinds are counted once per row', () => {
  assert.deepEqual(measures(fixture).outside, { waits: 5, outside: 1, rows: ['quiet:s9'] });
  assert.deepEqual(measures(fixture, { ownerKinds: ['quiet'] }).outside.rows, ['go-ahead:a1', 'step:q7/sign', 'go-ahead:a2', 'trust:t1']);
});

test('an answer after its row changed is counted as on a changed question', () => {
  assert.deepEqual(measures(fixture).changed, { answers: 3, onChanged: 1, rows: ['go-ahead:a2'] });
});

test('amber that did not need the owner: a wait outside the owner kinds, or one cleared with no press', () => {
  assert.deepEqual(measures(fixture).amber, { waits: 5, needless: 2, rows: ['quiet:s9', 'trust:t1'] });
});

test('a range keeps the entries of its days, both ends included', () => {
  assert.equal(inRange(fixture, '2026-11-17', '2026-11-17').length, 1);
  assert.equal(inRange(fixture, '2026-11-16', '2026-11-17').length, fixture.length);
  assert.equal(inRange(fixture, undefined, '2026-11-15').length, 0);
});
