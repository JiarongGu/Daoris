import { describe, expect, it } from 'vitest';
import type { Session } from '../data/api';
import { listedGroup, type SessionGrouping } from './groups';
import { lineHeadOf, lineMembersBefore, lineSaid } from './lineState';

const session = (id: string, state: string, created: string, line?: string, extra: Partial<Session> = {}) =>
  ({ id, state, created, updated: created, repository: 'engine', kind: 'chat', line: line ?? id, ...extra }) as Session;

const row = (id: string, group: SessionGrouping['group'], line?: SessionGrouping['line']): SessionGrouping =>
  ({ session: id, group, state: group === 'archived' ? 'done' : group, shown: group, archived: false, teammate: false, line });

const WORKING = { state: 'working', group: 'working', reason: 'working', by: '80c78acf', role: 'part' } as const;

describe('a line of work read as one session', () => {
  const sessions = [
    session('a674bf6a', 'stopped', '2026-11-12T09:00:00Z'),
    session('80c78acf', 'working', '2026-11-12T09:10:00Z', 'a674bf6a', { from: 'a674bf6a' }),
    session('5e55a000', 'completed', '2026-11-12T08:00:00Z'),
    session('4e1b0001', 'working', '2026-11-12T08:05:00Z', undefined, { serves: '5e55a000' }),
  ];

  it('places a part whose own record stopped where its line works', () => {
    expect(listedGroup(row('a674bf6a', 'done', WORKING))).toBe('working');
    expect(listedGroup(row('a674bf6a', 'done'))).toBe('done');
  });

  it('says the line works in its newer part, numbered among the parts, where the drawn part stopped', () => {
    const groupings = [row('a674bf6a', 'done', WORKING), row('80c78acf', 'working', WORKING)];
    expect(lineSaid('a674bf6a', groupings[0], groupings, sessions, ['a674bf6a', '80c78acf']))
      .toEqual({ shown: 'working', by: '80c78acf', role: 'part', part: 2 });
    expect(lineSaid('80c78acf', groupings[1], groupings, sessions)).toBeNull();
  });

  it('names the helper a working line is read from', () => {
    const line = { state: 'working', group: 'working', reason: 'working', by: '4e1b0001', role: 'helper' } as const;
    const groupings = [row('5e55a000', 'done', line), row('4e1b0001', 'working', line)];
    expect(lineSaid('5e55a000', groupings[0], groupings, sessions)).toEqual({ shown: 'working', by: '4e1b0001', role: 'helper', part: null });
  });

  it('opens any part as its line\'s session, its newest part, and draws the parts before it by membership', () => {
    expect(lineHeadOf(sessions, 'a674bf6a')?.id).toBe('80c78acf');
    expect(lineHeadOf(sessions, '80c78acf')?.id).toBe('80c78acf');
    // A co-worker is a line of its own: opening it opens it.
    expect(lineHeadOf(sessions, '4e1b0001')?.id).toBe('4e1b0001');
    expect(lineMembersBefore(sessions, '80c78acf')).toEqual(['a674bf6a']);
    expect(lineMembersBefore(sessions, 'a674bf6a')).toEqual([]);
  });

  it('keeps a teammate\'s record its own session', () => {
    const theirs = [...sessions, session('kit@m/b1', 'working', '2026-11-12T10:00:00Z', 'a674bf6a')];
    expect(lineHeadOf(theirs, 'a674bf6a')?.id).toBe('80c78acf');
    expect(lineHeadOf(theirs, 'kit@m/b1')?.id).toBe('kit@m/b1');
  });
});
