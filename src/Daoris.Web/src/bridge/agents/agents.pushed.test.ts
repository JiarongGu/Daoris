import { describe, expect, it, vi } from 'vitest';

// What sessions consumed, as the shell pushes it (PUSH1e, D182): a keyed slice of two partitions, each measured session under its
// id and each account's share under its door and account, composed into `USAGE`'s answer in the route's order.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import { keys, USAGE_SLICE } from '../../data/queries';
import { mergeKeyed } from '../state/slices';
import { usageSlice } from './agents';

const session = (id: string, when: string, extra: Record<string, unknown> = {}) =>
  ({ session: id, repository: 'engine', harness: 'claude-code', profile: 'work', used: 1_000, size: 200_000, when, ...extra });

describe('what sessions consumed, pushed (PUSH1e)', () => {
  it("feeds `USAGE`'s own entry from the one snapshot route, its deltas merged by key", () => {
    expect(usageSlice.name).toBe(USAGE_SLICE);
    expect(usageSlice.key).toEqual(keys.usage);
    expect(usageSlice.merge).toBe(mergeKeyed);
  });

  it('composes nothing before it is judged, and two empty lists once it is judged with nothing measured', () => {
    expect(usageSlice.compose(new Map(), false)).toBeUndefined();
    expect(usageSlice.compose(new Map<string, unknown>([['sessions', {}], ['accounts', {}]]), true)).toEqual({ sessions: [], accounts: [] });
  });

  it("orders the sessions newest first as instants, a tie by id, and the accounts by door then account, as the route does", () => {
    const composed = usageSlice.compose(new Map<string, unknown>([
      ['sessions', {
        b: session('b', '2026-10-20T09:00:00+00:00'),
        a: session('a', '2026-10-20T09:00:00+00:00'),
        // Two hours ahead of UTC: earlier than the others as an instant, though later as text.
        c: session('c', '2026-10-20T10:30:00+02:00'),
        d: session('d', '2026-10-20T09:30:00+00:00'),
        broken: 'no record',
      }],
      ['accounts', {
        'codex/home': { harness: 'codex', profile: 'home', sessions: 1, used: 5 },
        'claude-code/work': { harness: 'claude-code', profile: 'work', sessions: 2, used: 9 },
        'claude-code': { harness: 'claude-code', profile: null, sessions: 1, used: 3 },
      }],
    ]), true) as { sessions: { session: string }[]; accounts: { harness: string; profile: string | null }[] };

    expect(composed.sessions.map((each) => each.session)).toEqual(['d', 'a', 'b', 'c']);
    expect(composed.accounts.map((each) => `${each.harness}/${each.profile ?? ''}`)).toEqual(['claude-code/', 'claude-code/work', 'codex/home']);
  });
});
