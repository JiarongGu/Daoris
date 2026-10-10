import { describe, expect, it } from 'vitest';
import { type HelpConversationRow, historyGroups, joinPages, listingPage } from './history';

// ASKHIST1c: the history grouped as a person reads time, by this machine's calendar days: what is pinned, then today,
// yesterday, the rest of this week (from its Monday) and older; each row where its last word was said, in the driver's order.

/** A local time, on this machine's calendar. */
const local = (year: number, month: number, day: number, hour = 12) => new Date(year, month - 1, day, hour).toISOString();

const row = (session: string, last: string, pinned: string | null = null): HelpConversationRow => ({
  session, title: session, name: null, opening: session, about: null, created: last, last, pinned, live: false,
  resumable: true, from: null, handed: null, found: null, foundLine: null,
});

// A Thursday, mid-afternoon.
const NOW = new Date(2026, 9, 8, 15, 0);

describe('the history’s groups', () => {
  it('puts the pinned first, then today, yesterday, this week and older, in the order the driver gave', () => {
    const groups = historyGroups([
      row('p1', local(2026, 9, 1), local(2026, 10, 1)),
      row('t1', local(2026, 10, 8, 14)),
      row('t2', local(2026, 10, 8, 0)),
      row('y1', local(2026, 10, 7, 23)),
      row('w1', local(2026, 10, 6)),
      row('w2', local(2026, 10, 5, 0)),
      row('o1', local(2026, 10, 4, 23)),
      row('o2', local(2025, 12, 31)),
    ], NOW);

    expect(groups.map((group) => [group.id, group.rows.map((each) => each.session)])).toEqual([
      ['pinned', ['p1']],
      ['today', ['t1', 't2']],
      ['yesterday', ['y1']],
      ['week', ['w1', 'w2']],
      ['older', ['o1', 'o2']],
    ]);
  });

  it('leaves out a group with nothing in it, and reads a time ahead of this machine’s clock as today', () => {
    const groups = historyGroups([row('ahead', local(2026, 10, 9, 9)), row('o1', local(2026, 1, 2))], NOW);
    expect(groups.map((group) => group.id)).toEqual(['today', 'older']);
  });

  it('starts the week on its Monday, so on a Monday only today and yesterday are this week’s', () => {
    const monday = new Date(2026, 9, 5, 9, 0);
    const groups = historyGroups([row('m', local(2026, 10, 5, 8)), row('s', local(2026, 10, 4, 20)), row('f', local(2026, 10, 2))], monday);
    expect(groups.map((group) => [group.id, group.rows.map((each) => each.session)])).toEqual([
      ['today', ['m']], ['yesterday', ['s']], ['older', ['f']],
    ]);
  });
});

// ASKHIST1d2: the driver orders and searches every conversation and answers a page of them, with how many there are and
// where the next page starts (D158's ASKHIST1d1 note); the page reads each answer, joins its pages, and searches by the
// driver's own rule.

describe('the history’s pages', () => {
  it('reads an answer’s rows, how many there are and where the next page starts', () => {
    const a = row('a', local(2026, 10, 8));
    expect(listingPage({ conversations: [a], cut: true, total: 450, next: 200 })).toEqual({ conversations: [a], total: 450, next: 200 });
    expect(listingPage({ conversations: [a], cut: false, total: 1, next: null })).toEqual({ conversations: [a], total: 1, next: null });
  });

  it('reads a driver from before pages as one page that holds them all, and nothing as nothing', () => {
    const a = row('a', local(2026, 10, 8));
    expect(listingPage({ conversations: [a], cut: true })).toEqual({ conversations: [a], total: 1, next: null });
    expect(listingPage(undefined)).toEqual({ conversations: [], total: 0, next: null });
    expect(listingPage({ conversations: 'none', total: '3', next: -1 })).toEqual({ conversations: [], total: 0, next: null });
  });

  it('joins its pages in order, each conversation once by its session, with the last page’s count and next', () => {
    const [a, b, c] = ['a', 'b', 'c'].map((session) => row(session, local(2026, 10, 8)));
    // Spoken in between two asks, b moved down a page: it shows once, where it was first listed.
    const joined = joinPages([
      { conversations: [a!, b!], total: 4, next: 2 },
      { conversations: [{ ...b!, title: 'moved' }, c!], total: 3, next: null },
    ]);
    expect(joined.conversations.map((each) => each.session)).toEqual(['a', 'b', 'c']);
    expect(joined.conversations[1]!.title).toBe('b');
    expect(joined).toMatchObject({ total: 3, next: null });
    expect(joinPages([])).toEqual({ conversations: [], total: 0, next: null });
  });
});
