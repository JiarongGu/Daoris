import { test } from 'vitest';
import assert from 'node:assert/strict';
import { abnormalEnd, ownedAfter, parseWindowsList, stopOwned, type Proc } from './processes.ts';

const p = (pid: number, ppid: number, created: string, command = 'x'): Proc => ({ pid, ppid, created, command });
const T0 = '2026-11-13T10:00:00.000Z';
const T1 = '2026-11-13T10:00:05.000Z';
const T2 = '2026-11-13T10:00:09.000Z';

test('what a gate leaves is every process that appeared after it started and descends from its steps, even through a parent that has ended', () => {
  const before = [p(1, 0, '2026-11-13T09:00:00.000Z'), p(50, 1, '2026-11-13T09:30:00.000Z', 'the owner\'s build')];
  const after = [
    ...before,
    p(200, 100, T1, 'instance'),
    p(201, 200, T2, 'its child'),
    p(300, 1, T1, 'a sibling gate\'s'),
  ];
  const owned = ownedAfter({ before, after, roots: [100], startedAt: T0 });
  assert.deepEqual(owned.map((each) => each.pid), [200, 201]);
});

test('a process that was already there, or began before the gate, or only reuses a root\'s pid, is not the gate\'s', () => {
  const before = [p(200, 100, T1)];
  const after = [p(200, 100, T1), p(201, 200, '2026-11-13T09:59:00.000Z')];
  assert.deepEqual(ownedAfter({ before, after, roots: [100], startedAt: T0 }), []);
});

test('stopping checks each process by pid and creation time before it ends it, and names one that would not end', () => {
  const owned = [{ pid: 200, created: T1 }, { pid: 201, created: T2 }, { pid: 202, created: T2 }];
  const killed: number[] = [];
  const result = stopOwned(owned, {
    list: () => [p(200, 100, T1), p(201, 999, '2027-01-01T00:00:00.000Z'), p(202, 200, T2)],
    kill: (pid) => { killed.push(pid); return pid !== 202; },
  });
  assert.deepEqual(killed, [200, 202], 'a reused pid with another creation time is never touched');
  assert.deepEqual(result.map((each) => [each.pid, each.stopped]), [[200, true], [201, true], [202, false]]);
});

test('an exit that says the process ended (a signal, a crash status) is abnormal, a check\'s own failure is not', () => {
  for (const code of [255, 130, 126, 3221225794, -1]) assert.equal(abnormalEnd(code), true, String(code));
  for (const code of [0, 1, 2]) assert.equal(abnormalEnd(code), false, String(code));
});

test('the Windows listing is read with each process\'s parent and creation time', () => {
  const text = JSON.stringify([{ ProcessId: 4, ParentProcessId: 1, CreationDate: '2026-11-13T10:00:05.0000000Z', CommandLine: 'node a' }]);
  assert.deepEqual(parseWindowsList(text), [{ pid: 4, ppid: 1, created: '2026-11-13T10:00:05.000Z', command: 'node a' }]);
});
