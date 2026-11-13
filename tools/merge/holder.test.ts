import { test } from 'vitest';
import assert from 'node:assert/strict';
import { holderRuns, pidAlive, processStart, sameHolder, START_TOLERANCE_MS } from './holder.ts';

const BEGAN = Date.parse('2026-11-01T08:00:00Z');
const LATER = Date.parse('2026-11-05T08:00:00Z');

test('a pid that is no positive whole number runs nothing, and this process runs', () => {
  assert.equal(pidAlive(0), false);
  assert.equal(pidAlive(-4), false);
  assert.equal(pidAlive(1.5), false);
  assert.equal(pidAlive(process.pid), true);
});

test('a process\'s start is asked of PowerShell on Windows and of ps elsewhere, and a machine that will not say answers null', () => {
  const asked: string[] = [];
  const run = (stdout: string, status = 0) => (command: string) => {
    asked.push(command);
    return { status, stdout };
  };
  assert.equal(processStart(4242, { platform: 'win32', run: run(String(BEGAN)) }), BEGAN);
  assert.equal(processStart(4242, { platform: 'linux', run: run('Sun Nov  1 08:00:00 2026') }), Date.parse('Sun Nov  1 08:00:00 2026'));
  assert.deepEqual(asked, ['powershell', 'ps']);
  assert.equal(processStart(4242, { platform: 'win32', run: run('', 0) }), null, 'no such process');
  assert.equal(processStart(4242, { platform: 'linux', run: run('x', 1) }), null, 'ps refused');
  assert.equal(processStart(0, { run: run(String(BEGAN)) }), null);
});

test('the holder is the same while the pid\'s start is within the tolerance, and another once a later process took the pid', () => {
  assert.equal(sameHolder(4242, BEGAN, () => BEGAN + START_TOLERANCE_MS), true);
  assert.equal(sameHolder(4242, BEGAN, () => LATER), false);
  assert.equal(sameHolder(4242, BEGAN, () => null), true, 'a machine that will not say leaves the pid alone to decide');
  assert.equal(sameHolder(4242, undefined, () => LATER), true, 'a record with no start likewise');
});

test('a lock\'s text names a running holder only when its pid lives and began when it says; unreadable text names none', () => {
  const text = JSON.stringify({ pid: process.pid, started: Math.round(Date.now() - process.uptime() * 1000) });
  assert.equal(holderRuns(text, () => true), true);
  assert.equal(holderRuns(text, () => false), false);
  assert.equal(holderRuns(JSON.stringify({ pid: process.pid, started: BEGAN }), () => true), false, 'another process began then');
  assert.equal(holderRuns('not json', () => true), false);
});
