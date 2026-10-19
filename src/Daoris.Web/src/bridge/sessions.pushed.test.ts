import { describe, expect, it, vi } from 'vitest';

// The session records as the shell pushes them (PUSH1d2, D182): a partition per workspace keyed by session, composed into the widest
// list the service answers (every repository, closed included, every workspace), from which each view's list is read.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import type { Session } from '../data/api';
import { keys, SESSIONS_SLICE, sessionsIn } from '../data/queries';
import { sessionsSlice } from './sessions';
import { mergeKeyed } from './slices';

const record = (id: string, extra: Partial<Session> = {}): Session => ({
  id, repository: 'engine', adapter: 'claude-code', state: 'completed', created: '2026-10-19T08:00:00+00:00',
  updated: '2026-10-19T09:00:00+00:00', workspace: 'default', ...extra,
});

const keyed = (...records: Session[]) => Object.fromEntries(records.map((each) => [each.id, each]));

describe('the sessions, pushed (PUSH1d2)', () => {
  it('feeds the widest entry the pull answers, read from the one snapshot route, its deltas merged by key', () => {
    expect(sessionsSlice.name).toBe(SESSIONS_SLICE);
    expect(sessionsSlice.key).toEqual(keys.sessions(null, true, null));
    expect(sessionsSlice.merge).toBe(mergeKeyed);
  });

  it("composes every workspace's records in the service's order: the open first, then by when each began", () => {
    const composed = sessionsSlice.compose(new Map<string, unknown>([
      ['games', keyed(record('g-late', { workspace: 'games', created: '2026-10-19T10:00:00+00:00' }),
        record('g-open', { workspace: 'games', state: 'working', created: '2026-10-19T11:00:00+00:00' }))],
      ['default', keyed(record('d-early', { created: '2026-10-19T07:00:00+00:00' }),
        record('d-parked', { state: 'awaiting-person', created: '2026-10-19T09:00:00+00:00' }))],
    ]), true) as Session[];

    expect(composed.map((each) => each.id)).toEqual(['d-parked', 'g-open', 'd-early', 'g-late']);
  });

  it('composes nothing before the owner judged, and an empty list once it judged none', () => {
    expect(sessionsSlice.compose(new Map(), false)).toBeUndefined();
    expect(sessionsSlice.compose(new Map(), true)).toEqual([]);
    // A partition of another shape holds no record.
    expect(sessionsSlice.compose(new Map<string, unknown>([['default', 'x'], ['games', keyed(record('g1'))]]), true))
      .toEqual([record('g1')]);
  });

  it("reads each view's list from the widest as the service filters it: the repository, the workspace, the open", () => {
    const all = [
      record('open-engine', { state: 'working' }),
      record('closed-engine'),
      record('open-site', { repository: 'site', state: 'queued', workspace: 'Games' }),
      record('help', { repository: 'daoris:help', workspace: 'default' }),
    ];

    expect(sessionsIn(all, null, true, null)).toBe(all);
    expect(sessionsIn(all, 'engine', true, null).map((each) => each.id)).toEqual(['open-engine', 'closed-engine']);
    expect(sessionsIn(all, null, false, null).map((each) => each.id)).toEqual(['open-engine', 'open-site']);
    // A workspace compared as the service compares it: trimmed, and its ASCII letters in either case.
    expect(sessionsIn(all, null, true, ' games').map((each) => each.id)).toEqual(['open-site']);
    expect(sessionsIn(all, null, true, 'DEFAULT').map((each) => each.id)).toEqual(['open-engine', 'closed-engine', 'help']);
    expect(sessionsIn(all, 'daoris:help', true, null).map((each) => each.id)).toEqual(['help']);
  });
});
