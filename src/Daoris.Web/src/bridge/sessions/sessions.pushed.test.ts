import { describe, expect, it, vi } from 'vitest';

// The session records as the shell pushes them (PUSH1d2, D182): a partition per workspace keyed by session, composed into the widest
// list the service answers (every repository, closed included, every workspace), from which each view's list is read.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import type { Session } from '../../data/api';
import { CALLS_SLICE, GROUPS_SLICE, keys, SESSIONS_SLICE, sessionsIn } from '../../data/queries';
import type { SessionEntry } from '../../work/entries';
import type { SessionGrouping } from '../../work/groups';
import { callsSlice, groupsIn, groupsSlice, sessionsSlice, withCall } from './sessions';
import { mergeKeyed } from '../state/slices';

describe('the running calls, pushed (WAITSAY1c2)', () => {
  it('feeds its own entry from the one snapshot route, its deltas merged by key', () => {
    expect(callsSlice.name).toBe(CALLS_SLICE);
    expect(callsSlice.key).toEqual(keys.calls);
    expect(callsSlice.merge).toBe(mergeKeyed);
  });

  it('composes a map of session to call, nothing before judged, and an empty map once judged none', () => {
    const call = { title: 'npm test', since: '2026-10-20T08:00:00+00:00' };
    expect(callsSlice.compose(new Map(), false)).toBeUndefined();
    expect(callsSlice.compose(new Map(), true)).toEqual({});
    expect(callsSlice.compose(new Map<string, unknown>([['calling', { a: call }], ['other', 'x']]), true)).toEqual({ a: call });
    // AUTONOMY1b3b: a call held for the person's answer keeps its mark, as `SESSION_GROUPS` writes it beside the title.
    const held = { ...call, waiting: true };
    expect(callsSlice.compose(new Map<string, unknown>([['calling', { a: held }]]), true)).toEqual({ a: held });
    expect(withCall({ session: 'a', calling: call } as never, { a: held })?.calling).toEqual(held);
  });

  // SESSWATCH1a (the session-watch design §5.1): the watcher's mark laid over a quiet session's call, or a call of no title.
  it("lays the watcher's quiet mark over a session's call, or over a call of no title where it makes none", () => {
    const call = { title: 'npm test', since: '2026-10-20T08:00:00+00:00' };
    const since = '2026-10-20T07:50:00+00:00';
    expect(callsSlice.compose(new Map<string, unknown>([['calling', { a: call }], ['quiet', { a: { since }, b: { since } }]]), true))
      .toEqual({ a: { ...call, quiet: since }, b: { title: '', since, quiet: since } });
    expect(callsSlice.compose(new Map<string, unknown>([['calling', {}], ['quiet', {}]]), true)).toEqual({});
  });

  it("overlays the pushed call on a session's grouping, and clears what the pull still says once none runs", () => {
    const call = { title: 'npm test', since: '2026-10-20T08:00:00+00:00' };
    const old = { title: 'old', since: '2026-10-20T07:00:00+00:00' };
    const grouping = { session: 'a', calling: old } as never;
    expect(withCall(grouping, { a: call })?.calling).toEqual(call);
    expect(withCall(grouping, {})?.calling).toBeNull();
    // Not live: the pull's own answer stays, and nothing is made of no grouping.
    expect(withCall(grouping, undefined)).toBe(grouping);
    expect(withCall(undefined, { a: call })).toBeUndefined();
  });
});

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

// The session groups as the shell pushes them (PUSH1c3b, D182's PUSH1c3a note): each row under its session with its group's sort
// inputs, each entry under its key, composed in `SessionGroups.Ordered`'s order into the answer `SESSION_GROUPS` gives.

const row = (session: string, group: SessionGrouping['group']): SessionGrouping =>
  ({ session, group, shown: group === 'working' ? 'working' : 'completed', archived: group === 'archived', teammate: false });

/** What `SESSION_GROUPS` answers for the rows below, in its order: each group's rule, and each tie by the session's id by unit. */
const PULLED: SessionGrouping[] = [
  // Waiting on you, oldest move first: 09:00 at +02:00 is 07:00, before 08:00 though later as a string; 'Yb' before 'ya' by unit.
  row('yz', 'you'), row('Yb', 'you'), row('ya', 'you'),
  // To review, by repository without case ('alpha' before 'Engine' before 'Zeta'), then when each began, to the tick.
  row('r-alpha', 'review'), row('r-eng-early', 'review'), row('r-Eng-late', 'review'), row('r-zeta', 'review'),
  // Working, by repository then when each began as an instant: 10:00 at +02:00 is 08:00, before 09:00.
  row('w-2', 'working'), row('w-1', 'working'),
  // Resumes later, oldest move first; ended and archived newest first, a tie by id.
  row('l-old', 'later'), row('l-new', 'later'),
  row('e-new', 'ended'), row('e-tie-a', 'ended'), row('e-tie-b', 'ended'), row('e-old', 'ended'),
  row('a-new', 'archived'), row('a-old', 'archived'),
];

/** Each row's sort inputs as the slice pushes them (`GroupsStateSlice.OrderedBy`): never a field of the answer. */
const ORDER: Record<string, Record<string, string>> = {
  yz: { updated: '2026-10-19T09:00:00+02:00' },
  Yb: { updated: '2026-10-19T08:00:00+00:00' },
  ya: { updated: '2026-10-19T08:00:00+00:00' },
  'r-zeta': { repository: 'Zeta', created: '2026-10-19T07:00:00+00:00' },
  'r-alpha': { repository: 'alpha', created: '2026-10-19T09:00:00+00:00' },
  'r-Eng-late': { repository: 'Engine', created: '2026-10-19T08:00:00.1234567+00:00' },
  'r-eng-early': { repository: 'engine', created: '2026-10-19T08:00:00.1234561+00:00' },
  'w-1': { repository: 'engine', created: '2026-10-19T09:00:00+00:00' },
  'w-2': { repository: 'engine', created: '2026-10-19T10:00:00+02:00' },
  'l-new': { updated: '2026-10-19T10:00:00+00:00' },
  'l-old': { updated: '2026-10-19T06:00:00+00:00' },
  'e-old': { updated: '2026-10-19T06:00:00+00:00' },
  'e-tie-b': { updated: '2026-10-19T08:00:00+00:00' },
  'e-tie-a': { updated: '2026-10-19T08:00:00+00:00' },
  'e-new': { updated: '2026-10-19T10:00:00+00:00' },
  'a-old': { updated: '2026-10-19T06:00:00+00:00' },
  'a-new': { updated: '2026-10-19T07:00:00+00:00' },
};

const part = (session: string, group: SessionGrouping['group']) =>
  ({ session, quest: null, shown: 'completed', group, began: 'opened', after: null, askedBy: null });

/** The entries `SESSION_GROUPS` answers beside them, each placed by its leading row's place. */
const ENTRIES: SessionEntry[] = [
  { entry: 'session:yz/engine', origin: 'session:yz', repository: 'engine', group: 'you', leading: 'yz', parts: [part('yz', 'you')] },
  {
    entry: 'quest:q1/engine', origin: 'quest:q1', repository: 'engine', group: 'working', leading: 'w-1',
    parts: [part('w-2', 'working'), part('w-1', 'working')],
  },
  {
    entry: 'ask:k1/intake', origin: 'ask:k1', repository: 'intake', group: 'ended', leading: 'e-new',
    parts: [part('e-old', 'ended'), part('e-new', 'ended')],
  },
];

/** The partitions as the owner holds them, each keyed in an order the composition must not keep. */
const partitions = () => new Map<string, unknown>([
  ['entries', Object.fromEntries([...ENTRIES].reverse().map((entry) => [entry.entry, entry]))],
  ['rows', Object.fromEntries([...PULLED].reverse().map((each) => [each.session, { ...each, ...ORDER[each.session] }]))],
]);

describe('the session groups, pushed (PUSH1c3b)', () => {
  it("feeds SESSION_GROUPS' widest entry, read from the one snapshot route, its deltas merged by key", () => {
    expect(groupsSlice.name).toBe(GROUPS_SLICE);
    expect(GROUPS_SLICE).toBe('groups');
    expect(groupsSlice.key).toEqual(['sessions', 'groups', '*']);
    expect(groupsSlice.merge).toBe(mergeKeyed);
  });

  it("composes the rows in the reader's order and the entries by their leading rows, the order's inputs left out", () => {
    expect(groupsSlice.compose(partitions(), true)).toEqual({ sessions: PULLED, entries: ENTRIES });
  });

  it('composes nothing before the owner judged or while it holds no rows, and empty lists once it judged none', () => {
    expect(groupsSlice.compose(partitions(), false)).toBeUndefined();
    expect(groupsSlice.compose(new Map([['entries', {}]]), true)).toBeUndefined();
    expect(groupsSlice.compose(new Map([['rows', {}], ['entries', {}]]), true)).toEqual({ sessions: [], entries: [] });
    // A malformed row or entry is left out rather than guessed at.
    expect(groupsSlice.compose(new Map<string, unknown>([
      ['rows', { yz: { ...row('yz', 'you'), ...ORDER.yz }, bad: { group: 'you' } }],
      ['entries', { 'session:yz/engine': ENTRIES[0], empty: { ...ENTRIES[0], entry: 'empty', parts: [] } }],
    ]), true)).toEqual({ sessions: [row('yz', 'you')], entries: [ENTRIES[0]] });
  });

  it("reads a variant for some sessions as the reader's `only` answers it: their rows, and each entry holding one whole", () => {
    const all = groupsSlice.compose(partitions(), true) as { sessions: SessionGrouping[]; entries: SessionEntry[] };
    const some = groupsIn(all, ['w-2', 'yz', 'w-2']);
    expect(some.sessions.map((each) => each.session)).toEqual(['yz', 'w-2']);
    expect(some.entries).toEqual([ENTRIES[0], ENTRIES[1]]);
    expect(groupsIn(all, undefined)).toBe(all);
  });
});
