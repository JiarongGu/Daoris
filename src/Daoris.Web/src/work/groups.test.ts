import { describe, expect, it } from 'vitest';
import type { Session } from '../api';
import {
  cutEnded, endedToArchive, GROUP_ORDER, keptSessionFilters, sessionFilters, type SessionGrouping, sessionsByRepository, sessionsByState,
  shownOf, stripSessions, waitsOnYou,
} from './groups';

// The session list by state (SESSUX1c, D126 §2.1, §4): the reader's groups as the list draws them, by repository as
// before, the strip, and what the list remembers. Pure, so every list a person could have is an argument.

const at = (minutesAgo: number) => new Date(Date.UTC(2026, 9, 2, 12, 0) - minutesAgo * 60_000).toISOString();

const session = (over: Partial<Session> & { id: string; state: Session['state'] }): Session => ({
  repository: 'engine', adapter: 'claude-code', kind: 'driven', created: at(60), updated: at(1), ...over,
});

const grouping = (over: Partial<SessionGrouping> & { session: string; group: SessionGrouping['group'] }): SessionGrouping => ({
  shown: 'working', archived: false, teammate: false, ...over,
});

const ids = (rows: { id: string }[]) => rows.map((row) => row.id);

describe('what the session list remembers', () => {
  /** A first open with nothing remembered is by state (D126 §4.1), and nothing shows archived. */
  it('reads nothing kept, or anything unreadable, as by state with archived hidden', () => {
    expect(sessionFilters({})).toEqual({ group: 'state', archived: false });
    expect(sessionFilters({ group: 'mood', archived: 'yes' })).toEqual({ group: 'state', archived: false });
    expect(sessionFilters({ group: 'repository', archived: true })).toEqual({ group: 'repository', archived: true });
  });

  /** The defaults are nothing kept, as Quests' are: a stale value costs nothing. */
  it('keeps only what differs from the default', () => {
    expect(keptSessionFilters({ group: 'state', archived: false })).toBeNull();
    expect(keptSessionFilters({ group: 'repository', archived: false })).toEqual({ group: 'repository' });
    expect(keptSessionFilters({ group: 'state', archived: true })).toEqual({ archived: true });
  });
});

describe('the list by state', () => {
  const sessions = [
    session({ id: 'w1', state: 'working', repository: 'tools' }),
    session({ id: 'p1', state: 'awaiting-person' }),
    session({ id: 'f1', state: 'failed', quest: 'q1' }),
    session({ id: 'c1', state: 'completed', updated: at(30) }),
    session({ id: 'c2', state: 'completed', updated: at(5) }),
    session({ id: 'r1', state: 'stopped', tree: 'C:/somewhere/.daoris/trees/default/engine/r1' }),
    session({ id: 'a1', state: 'completed', quest: 'q2' }),
  ];
  // The reader's answer, in its order: the groups in the order the person acts on them, and each in its own.
  const groupings = [
    grouping({ session: 'p1', group: 'you', shown: 'awaiting-person' }),
    grouping({ session: 'f1', group: 'you', shown: 'parked', strikes: 3 }),
    grouping({ session: 'r1', group: 'review', shown: 'stopped', work: { commits: 3, uncommitted: 0 } }),
    grouping({ session: 'w1', group: 'working', shown: 'working' }),
    grouping({ session: 'a1', group: 'later', shown: 'awaiting-reply', awaits: 'q9', awaitsOf: 'game' }),
    grouping({ session: 'c2', group: 'ended', shown: 'completed' }),
    grouping({ session: 'c1', group: 'ended', shown: 'completed' }),
  ];

  /** D126 §2.1: five groups in the order the person acts on them, each in the reader's order. */
  it('lists each group in the reader\'s order, and leaves an empty group out', () => {
    const groups = sessionsByState(sessions, groupings, { selected: null, archived: false });

    expect(groups.map((group) => group.group)).toEqual(['you', 'review', 'working', 'later', 'ended']);
    expect(groups.map((group) => ids(group.sessions))).toEqual([['p1', 'f1'], ['r1'], ['w1'], ['a1'], ['c2', 'c1']]);
    expect(GROUP_ORDER).toEqual(['you', 'review', 'working', 'later', 'ended', 'archived']);

    const only = sessionsByState([sessions[0]!], [groupings[3]!], { selected: null, archived: false });
    expect(only.map((group) => group.group)).toEqual(['working']);
  });

  /** Archived is hidden unless ticked (§4.1); the attended session is never taken out from under its reader. */
  it('hides archived sessions unless they are shown, and keeps the attended one', () => {
    const marked = [...groupings.slice(0, 6), grouping({ session: 'c1', group: 'archived', shown: 'completed', archived: true })];

    expect(sessionsByState(sessions, marked, { selected: null, archived: false }).map((group) => group.group))
      .toEqual(['you', 'review', 'working', 'later', 'ended']);
    expect(ids(sessionsByState(sessions, marked, { selected: null, archived: true }).at(-1)!.sessions)).toEqual(['c1']);
    const attended = sessionsByState(sessions, marked, { selected: 'c1', archived: false });
    expect(attended.at(-1)).toEqual({ group: 'archived', sessions: [sessions[3]] });
  });

  /**
   * A record the reader has not answered for yet (it started after the reader looked) is placed by its record alone
   * until the next answer: live and waiting on the person, live, or ended, newest first.
   */
  it('places a session the reader has not answered for by its record, until the next answer', () => {
    const fresh = [
      ...sessions,
      session({ id: 'n1', state: 'queued', repository: 'tools' }),
      session({ id: 'n2', state: 'awaiting-person' }),
      session({ id: 'n3', state: 'failed' }),
      session({ id: 'team/n4', state: 'awaiting-person' }),
      session({ id: 'n5', state: 'awaiting-person', answer: 'Use the second.' }),
    ];
    const groups = sessionsByState(fresh, groupings, { selected: null, archived: false });
    const of = (name: string) => ids(groups.find((group) => group.group === name)!.sessions);

    expect(of('you')).toEqual(['p1', 'f1', 'n2']);
    // A teammate's park waits on them, as the reader lists it (SESSUX1a); one the person answered goes on at the
    // driver's next look (ANSWER1c).
    expect(of('working')).toEqual(['w1', 'n1', 'team/n4', 'n5']);
    expect(of('ended')).toEqual(['n3', 'c2', 'c1']);
  });

  /** The page lists what its scope holds: the reader answers for every record, and one the page does not hold is not drawn. */
  it('draws nothing for a session the page does not hold', () => {
    const groups = sessionsByState([sessions[0]!], groupings, { selected: null, archived: false });
    expect(groups).toEqual([{ group: 'working', sessions: [sessions[0]] }]);
  });

  it('survives nothing at all', () => {
    expect(sessionsByState([], [], { selected: null, archived: false })).toEqual([]);
  });
});

/**
 * SESSUX1e, D126 §5.3: *Archive what ended…* takes every session the reader placed in Ended that is not archived, and
 * says what stays in the list because it needs the person, by the headings the person sees.
 */
describe('what Archive what ended takes', () => {
  const sessions = [
    session({ id: 'p1', state: 'awaiting-person' }),
    session({ id: 'f1', state: 'failed', quest: 'q1' }),
    session({ id: 'r1', state: 'stopped' }),
    session({ id: 'w1', state: 'working' }),
    session({ id: 'a1', state: 'completed', quest: 'q2' }),
    session({ id: 'c1', state: 'completed', updated: at(30) }),
    session({ id: 'c2', state: 'declined', updated: at(5) }),
    session({ id: 'x1', state: 'completed', updated: at(90) }),
  ];
  const groupings = [
    grouping({ session: 'p1', group: 'you', shown: 'awaiting-person' }),
    grouping({ session: 'f1', group: 'you', shown: 'parked', strikes: 3 }),
    grouping({ session: 'r1', group: 'review', shown: 'stopped', work: { commits: 1, uncommitted: 0 } }),
    grouping({ session: 'w1', group: 'working', shown: 'working' }),
    grouping({ session: 'a1', group: 'later', shown: 'awaiting-reply', awaits: 'q9' }),
    grouping({ session: 'c2', group: 'ended', shown: 'declined' }),
    grouping({ session: 'c1', group: 'ended', shown: 'completed' }),
    grouping({ session: 'x1', group: 'archived', shown: 'completed', archived: true }),
  ];

  /** Never a live session, never what needs the person, never one already archived: only Ended, in its order. */
  it('takes what the reader placed in Ended, and counts what stays because it needs you', () => {
    expect(endedToArchive(sessions, groupings)).toEqual({ going: ['c2', 'c1'], kept: { you: 2, review: 1 } });
  });

  /** A record the reader has not answered for may yet be to review, so the list that archives leaves it to the next look. */
  it('leaves a session the reader has not answered for', () => {
    const since = session({ id: 'n1', state: 'completed', updated: at(0) });
    expect(endedToArchive([...sessions, since], groupings).going).toEqual(['c2', 'c1']);
  });

  /** The page's scope: a session the page does not hold is not archived from it. */
  it('takes only what the page holds', () => {
    expect(endedToArchive([sessions[5]!], groupings)).toEqual({ going: ['c1'], kept: { you: 0, review: 0 } });
  });

  it('takes nothing where no reader answered', () => {
    expect(endedToArchive(sessions, undefined)).toEqual({ going: [], kept: { you: 1, review: 0 } });
  });
});

describe('the list by repository', () => {
  const sessions = [
    session({ id: 'w1', state: 'working', repository: 'tools' }),
    session({ id: 'w2', state: 'working' }),
    session({ id: 'p1', state: 'awaiting-person', repository: 'tools' }),
    session({ id: 'f1', state: 'failed', quest: 'q1', updated: at(20) }),
    session({ id: 'c1', state: 'completed', updated: at(30) }),
    session({ id: 'c2', state: 'completed', updated: at(5) }),
  ];

  /**
   * 🔴 The deployed application showed four empty-state sentences on a machine with four real session records (D62;
   * working-surface design §7): an ended session is somewhere, never nowhere. Moved here with `partition`'s cases.
   */
  it('lists an ended session beneath the live ones rather than dropping it, and keeps every live state live', () => {
    const states = sessionsByRepository([
      session({ id: 'q', state: 'queued' }),
      session({ id: 's', state: 'starting' }),
      session({ id: 'w', state: 'working' }),
      session({ id: 'a', state: 'awaiting-person' }),
      session({ id: 'gone', state: 'failed' }),
    ], undefined, { selected: null, archived: false });

    expect(states.live.flatMap((group) => ids(group.sessions)).sort()).toEqual(['a', 'q', 's', 'w']);
    expect(ids(states.ended)).toEqual(['gone']);
    expect(sessionsByRepository([], undefined, { selected: null, archived: false })).toEqual({ live: [], ended: [] });
  });

  /** Today's arrangement (§4.3): a group per repository by name, waiting on you first within it, the ended beneath. */
  it('groups the live sessions by repository, waiting on you first, and puts the ended beneath, newest first', () => {
    const { live, ended } = sessionsByRepository(sessions, undefined, { selected: null, archived: false });

    expect(live.map((group) => [group.repository, ids(group.sessions)])).toEqual([['engine', ['w2']], ['tools', ['p1', 'w1']]]);
    expect(ids(ended)).toEqual(['c2', 'f1', 'c1']);
  });

  /** It gains the reader's words: *parked* sorts with *waiting on you* (§4.3), and archived is hidden unless shown. */
  it('sorts a parked session with waiting on you, and hides what is archived', () => {
    const groupings = [
      grouping({ session: 'f1', group: 'you', shown: 'parked', strikes: 3 }),
      grouping({ session: 'c1', group: 'archived', shown: 'completed', archived: true }),
    ];
    const { live, ended } = sessionsByRepository(sessions, groupings, { selected: null, archived: false });

    expect(live.map((group) => [group.repository, ids(group.sessions)])).toEqual([['engine', ['f1', 'w2']], ['tools', ['p1', 'w1']]]);
    expect(ids(ended)).toEqual(['c2']);
    expect(ids(sessionsByRepository(sessions, groupings, { selected: null, archived: true }).ended)).toEqual(['c2', 'c1']);
  });

  /** The rule that was already here, kept: a session that ends while you read it stays in its group. */
  it('keeps the attended session among the live whatever state it reached', () => {
    const { live, ended } = sessionsByRepository(sessions, undefined, { selected: 'c1', archived: false });

    expect(live.find((group) => group.repository === 'engine')!.sessions.map((row) => row.id)).toContain('c1');
    expect(ids(ended)).not.toContain('c1');
  });
});

describe('a long Ended group', () => {
  const rows = Array.from({ length: 15 }, (_, index) => ({ id: `e${index}` }));

  /** §4.4: the first twelve, then a press for the rest, which are counted and never silently cut. */
  it('shows the first twelve and counts the rest', () => {
    expect(cutEnded(rows, { limit: 12, open: false, keep: null })).toEqual({ rows: rows.slice(0, 12), hidden: 3 });
    expect(cutEnded(rows, { limit: 12, open: true, keep: null })).toEqual({ rows, hidden: 0 });
    expect(cutEnded(rows.slice(0, 4), { limit: 12, open: false, keep: null })).toEqual({ rows: rows.slice(0, 4), hidden: 0 });
  });

  /** The attended session is never cut out of its group. */
  it('keeps the attended session past the cut', () => {
    const { rows: shown, hidden } = cutEnded(rows, { limit: 12, open: false, keep: 'e14' });
    expect(ids(shown)).toEqual([...ids(rows.slice(0, 12)), 'e14']);
    expect(hidden).toBe(2);
  });
});

describe('the strip', () => {
  const sessions = [
    session({ id: 'w1', state: 'working', repository: 'tools' }),
    session({ id: 'p1', state: 'awaiting-person', repository: 'tools' }),
    session({ id: 'f1', state: 'failed', quest: 'q1' }),
    session({ id: 'c1', state: 'completed' }),
  ];
  const groupings = [
    grouping({ session: 'p1', group: 'you', shown: 'awaiting-person' }),
    grouping({ session: 'f1', group: 'you', shown: 'parked', strikes: 3 }),
    grouping({ session: 'w1', group: 'working', shown: 'working' }),
    grouping({ session: 'c1', group: 'ended', shown: 'completed' }),
  ];

  /** §2.5: what waits on the person first, then what runs, in the open list's order; what ended is the open list's. */
  it('holds waiting on you, then working, in the open list\'s order', () => {
    expect(ids(stripSessions(sessions, groupings, { arrangement: 'state', selected: null, archived: false })))
      .toEqual(['p1', 'f1', 'w1']);
    expect(ids(stripSessions(sessions, groupings, { arrangement: 'repository', selected: null, archived: false })))
      .toEqual(['f1', 'p1', 'w1']);
  });

  it('keeps the attended session one press away, whatever it reached', () => {
    expect(ids(stripSessions(sessions, groupings, { arrangement: 'state', selected: 'c1', archived: false })))
      .toEqual(['p1', 'f1', 'w1', 'c1']);
  });
});

describe('a row as the list shows it', () => {
  /** The reader's two derived words stand; every other row keeps the page's own reading, idle among it (UX5 U17). */
  it('shows the reader\'s word where it derived one, and the page\'s own otherwise', () => {
    const failed = session({ id: 'f1', state: 'failed' });
    const chat = session({ id: 'c1', state: 'working', kind: 'chat' });

    expect(shownOf(failed, grouping({ session: 'f1', group: 'you', shown: 'parked' }), undefined)).toBe('parked');
    expect(shownOf(session({ id: 'a1', state: 'completed' }), grouping({ session: 'a1', group: 'later', shown: 'awaiting-reply' }), undefined))
      .toBe('awaiting-reply');
    expect(shownOf(failed, null, undefined)).toBe('failed');
    expect(shownOf(chat, grouping({ session: 'c1', group: 'working', shown: 'working' }), false)).toBe('idle');
    // A word a newer host derives that this page does not know reads as the record's own.
    expect(shownOf(failed, grouping({ session: 'f1', group: 'later', shown: 'account-cooling' }), undefined)).toBe('failed');
  });

  /**
   * ANSWER1c (D131): a park the person answered shows as answered, by the reader's word or, before the reader has
   * answered for it, by its own record, which holds the answer.
   */
  it('shows a park the person answered as answered, by the reader or by its record', () => {
    const answered = session({ id: 'p1', state: 'awaiting-person', answer: 'Use the second.' });
    expect(shownOf(answered, grouping({ session: 'p1', group: 'working', shown: 'answered' }), undefined)).toBe('answered');
    expect(shownOf(answered, null, undefined)).toBe('answered');
    expect(shownOf(session({ id: 'p2', state: 'awaiting-person' }), null, undefined)).toBe('awaiting-person');
  });

  it('says what waits on the person: a session parked to ask, or a parked quest\'s last session', () => {
    expect(waitsOnYou(session({ id: 'p1', state: 'awaiting-person' }), null)).toBe(true);
    expect(waitsOnYou(session({ id: 'f1', state: 'failed' }), grouping({ session: 'f1', group: 'you', shown: 'parked' }))).toBe(true);
    expect(waitsOnYou(session({ id: 'w1', state: 'working' }), null)).toBe(false);
    // ANSWER1c: answered, it goes on at the driver's next look, and nothing about it waits on the person.
    expect(waitsOnYou(session({ id: 'p2', state: 'awaiting-person', answer: 'carry on.' }), null)).toBe(false);
  });
});
