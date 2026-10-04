import { describe, expect, it } from 'vitest';
import type { Ask, Quest, Session } from '../api';
import {
  answered, askStanding, freshest, keptFilters, latestSessions, questFilters, questGroups, questionOf, questStanding,
} from './records';

// Quests' list and its memory as values (FRAME1d, D118 §2, §3f): every list a person could have is an argument.

const quest = (over: Partial<Quest>): Quest => ({
  id: 'abc123', from: 'game', to: 'engine', title: 'A quest', body: 'Why.', status: 'Open',
  filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z', ...over,
});

describe("Quests' filters, as kept", () => {
  it('reads what was kept, and anything unreadable as the default', () => {
    expect(questFilters({ to: 'engine', closed: true })).toEqual({ to: 'engine', closed: true });
    expect(questFilters({})).toEqual({ to: null, closed: false });
    expect(questFilters({ to: '', closed: 'yes' })).toEqual({ to: null, closed: false });
    expect(questFilters({ to: 7 })).toEqual({ to: null, closed: false });
  });

  it('keeps nothing for the defaults, and only what differs from them', () => {
    expect(keptFilters({ to: null, closed: false })).toBeNull();
    expect(keptFilters({ to: 'engine', closed: false })).toEqual({ to: 'engine' });
    expect(keptFilters({ to: null, closed: true })).toEqual({ closed: true });
  });
});

describe("Quests' groups", () => {
  const rows = [
    quest({ id: 'o1' }), quest({ id: 't1', status: 'Taken' }), quest({ id: 'd1', status: 'Done' }), quest({ id: 'x1', status: 'Declined' }),
  ];

  it('groups by where each quest is in its life, open first', () => {
    expect(questGroups(rows, false).map(({ group, quests }) => [group, quests.map((q) => q.id)])).toEqual([
      ['held', []], ['open', ['o1']], ['progress', ['t1']],
    ]);
  });

  it('adds the closed group, done and declined, only where closed quests are shown', () => {
    expect(questGroups(rows, true).at(-1)).toEqual({ group: 'closed', quests: [rows[2], rows[3]] });
  });

  /**
   * DRIFT1d2 (D133 §4): a done a departure holds is outstanding, and the service lists it without closed ones, so the list
   * shows it first, awaiting the person's yes, whether closed quests are shown or not, and never twice.
   */
  it('puts a quest a departure holds first, awaiting the person, with closed quests shown or not', () => {
    const held = quest({ id: 'h1', status: 'Done', held: true });

    expect(questGroups([...rows, held], false)[0]).toEqual({ group: 'held', quests: [held] });
    const shown = questGroups([...rows, held], true);
    expect(shown[0]).toEqual({ group: 'held', quests: [held] });
    expect(shown.at(-1)!.quests.map((q) => q.id)).toEqual(['d1', 'x1']);
  });
});

describe('the session that marks a quest', () => {
  const session = (over: Partial<Session>): Session => ({
    id: 's1', quest: 'abc123', repository: 'engine', adapter: 'stub', state: 'working',
    created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z', ...over,
  });

  it('is its freshest attempt, and a chat that serves no quest marks none', () => {
    const latest = latestSessions([
      session({ id: 'old', state: 'failed', updated: '2026-09-01T23:00:00Z' }),
      session({ id: 'new' }),
      session({ id: 'chat', quest: null }),
    ]);
    expect(latest.get('abc123')?.id).toBe('new');
    expect(latest.size).toBe(1);
  });
});

describe('the record a page shows', () => {
  const listed = quest({ updated: '2026-09-02T00:00:00Z' });

  it('is the list\'s copy where it is newer than the door\'s last answer, and the answer where the list is behind', () => {
    expect(freshest(listed, quest({ status: 'Taken', updated: '2026-09-01T00:00:00Z' }))).toBe(listed);
    const answer = quest({ status: 'Taken', updated: '2026-09-03T00:00:00Z' });
    expect(freshest(listed, answer)).toBe(answer);
  });

  it('is the answer on a tie, since a dismissal moves no time, and the answer alone before the list has it', () => {
    const answer = quest({ conflicts: [], updated: listed.updated });
    expect(freshest(listed, answer)).toBe(answer);
    expect(freshest(undefined, answer)).toBe(answer);
  });

  it('is the list\'s copy where the answer was about another record, or there is none', () => {
    expect(freshest(listed, quest({ id: 'other' }))).toBe(listed);
    expect(freshest(listed, null)).toBe(listed);
    expect(freshest(undefined, null)).toBeUndefined();
  });
});

describe('the question a quest waits on (D79)', () => {
  const asked = quest({ id: 'q2', status: 'Open' });

  it('is a taken quest\'s, found among every quest, and named by its id where the page lacks it', () => {
    expect(questionOf(quest({ status: 'Taken', awaits: 'q2' }), [asked])).toEqual({ id: 'q2', quest: asked });
    expect(questionOf(quest({ status: 'Taken', awaits: 'q9' }), [asked])).toEqual({ id: 'q9', quest: undefined });
    expect(questionOf(quest({ status: 'Open', awaits: 'q2' }), [asked])).toBeNull();
  });

  it('is answered once it has closed, either way', () => {
    expect(answered(asked)).toBe(false);
    expect(answered(quest({ status: 'Done' }))).toBe(true);
    expect(answered(quest({ status: 'Declined' }))).toBe(true);
    expect(answered(undefined)).toBe(false);
  });
});

// UX6b (design §1 rule 6): Quests reopens its chosen record only while it still waits on something. The install reopened
// yesterday's done quest while two asks waited to be published.
describe('whether a chosen record still waits', () => {
  const ask = (over: Partial<Ask>): Ask => ({
    id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration.', state: 'Proposed', tier: 'declarations',
    asked: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z', links: [], attachments: [], proposal: [], quests: [], ...over,
  });

  it('is unread while every quest is still on its way, and gone once they answered without it', () => {
    expect(questStanding(undefined, false)).toBe('unread');
    expect(questStanding(undefined, true)).toBe('gone');
  });

  it('waits while open or taken, and closes done or declined', () => {
    expect(questStanding(quest({ status: 'Open' }), true)).toBe('live');
    expect(questStanding(quest({ status: 'Taken' }), true)).toBe('live');
    expect(questStanding(quest({ status: 'Done' }), true)).toBe('closed');
    expect(questStanding(quest({ status: 'Declined' }), true)).toBe('closed');
  });

  it('still waits closed while a departure holds it for the person, or a move another machine made waits on them', () => {
    expect(questStanding(quest({ status: 'Done', held: true }), true)).toBe('live');
    const conflict = { machine: 'm2', sequence: 3, attempted: 'Taken' as const, at: '2026-09-02T00:00:00Z' };
    expect(questStanding(quest({ status: 'Done', conflicts: [conflict] }), true)).toBe('live');
    expect(questStanding(quest({ status: 'Done', conflicts: [] }), true)).toBe('closed');
  });

  it('waits on an ask while it is proposed, open or published, and closes it done or closed', () => {
    expect(askStanding(undefined, false)).toBe('unread');
    expect(askStanding(undefined, true)).toBe('gone');
    expect(askStanding(ask({ state: 'Proposed' }), true)).toBe('live');
    expect(askStanding(ask({ state: 'Open' }), true)).toBe('live');
    expect(askStanding(ask({ state: 'Published' }), true)).toBe('live');
    expect(askStanding(ask({ state: 'Done' }), true)).toBe('closed');
    expect(askStanding(ask({ state: 'Closed' }), true)).toBe('closed');
  });

  it('still waits on a done ask whose go-ahead the person has not answered', () => {
    const request = { session: 's1', at: '2026-09-02T00:00:00Z', why: 'to push' };
    const goAhead = { number: 1, kind: 'push', on: 'origin', act: 'main', asked: [request] };
    expect(askStanding(ask({ state: 'Done', goAheads: [{ ...goAhead, state: 'asked' }] }), true)).toBe('live');
    expect(askStanding(ask({ state: 'Done', goAheads: [{ ...goAhead, state: 'approved' }] }), true)).toBe('closed');
  });
});
