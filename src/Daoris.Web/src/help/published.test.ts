import { describe, expect, it } from 'vitest';
import type { Ask, Quest, Session } from '../data/api';
import { PUBLISHED } from '../asks/fixtures';
import { DONE, OPEN, TAKEN, WORKING } from '../quests/fixtures';
import { type HelpPublishedEntry, publishedCounts, publishedItems } from './published';

// ASKQUESTS1 (the Ask Daoris design §9 item 12): what a conversation published, each with its live state read from the lists the
// shell pushes, never polled. A list not answered yet is reading, never empty, and a record it no longer holds is gone.

const entry = (over: Partial<HelpPublishedEntry>): HelpPublishedEntry => ({
  at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: null, quest: null, ...over,
});

const ASK: Ask = { ...PUBLISHED, id: 'a1b2c3d4', quests: [OPEN.id, TAKEN.id] };
const WORKED: Session = { ...WORKING, id: 's9w8v7u6', quest: TAKEN.id, state: 'working' };

describe('a conversation’s published list', () => {
  it('shows an ask with the quests it became, and a quest published on its own, newest first, each with its state', () => {
    const items = publishedItems(
      [entry({ proposal: 'p2', kind: 'setup', ask: null, quest: DONE.id }), entry({ ask: ASK.id, quest: OPEN.id })],
      { quests: [OPEN, TAKEN, DONE], asks: [ASK], sessions: [WORKED] },
    );

    expect(items.map((item) => item.key)).toEqual([`quest:${DONE.id}`, `ask:${ASK.id}`]);
    const [done, ask] = items;
    expect(done.kind === 'quest' && done.quest.shown).toBe('Done');
    if (ask.kind !== 'ask') throw new Error('an ask');
    // Its quests in the order it became them, the one its Apply answered counted once.
    expect(ask.ask.quests.map((quest) => [quest.id, quest.shown])).toEqual([[OPEN.id, 'Open'], [TAKEN.id, 'Taken']]);
    // The taken one names the session working it here, by its state.
    expect(ask.ask.quests[1].session).toEqual({ id: WORKED.id, state: 'working' });
  });

  it('moves with the pushed lists: the same entry reads taken once its quest is taken', () => {
    const entries = [entry({ ask: null, quest: OPEN.id })];
    const before = publishedItems(entries, { quests: [OPEN], asks: [], sessions: [] });
    const after = publishedItems(entries, {
      quests: [{ ...OPEN, status: 'Taken' }], asks: [], sessions: [{ ...WORKED, quest: OPEN.id }],
    });

    expect(before[0].kind === 'quest' && before[0].quest.shown).toBe('Open');
    expect(after[0].kind === 'quest' && [after[0].quest.shown, after[0].quest.session?.state]).toEqual(['Taken', 'working']);
  });

  it('reads a list not answered yet as reading, never as gone, and one that holds no such record as gone', () => {
    const entries = [entry({ ask: ASK.id })];

    const reading = publishedItems(entries, { quests: undefined, asks: undefined, sessions: undefined });
    expect(reading[0].kind === 'ask' && reading[0].ask.record).toBe('reading');
    expect(publishedCounts(reading).reading).toBe(true);

    const gone = publishedItems(entries, { quests: [], asks: [], sessions: [] });
    expect(gone[0].kind === 'ask' && gone[0].ask.record).toBe('gone');
    expect(publishedCounts(gone)).toMatchObject({ gone: 1, reading: false });
  });

  it('says a take is another machine’s only where both machines are known and differ', () => {
    const taken = (over: Partial<Quest>) => publishedItems([entry({ quest: TAKEN.id })], { quests: [{ ...TAKEN, ...over }], asks: [], sessions: [] })[0];

    expect(taken({ holder: 'm-b', here: 'm-a' })).toMatchObject({ quest: { elsewhere: true } });
    expect(taken({ holder: 'm-a', here: 'm-a' })).toMatchObject({ quest: { elsewhere: false } });
    expect(taken({ holder: 'm-b', here: undefined })).toMatchObject({ quest: { elsewhere: false } });
    expect(taken({ holder: undefined, here: 'm-a' })).toMatchObject({ quest: { elsewhere: false } });
  });

  it('counts what waits on the person apart: a done a departure holds, an ask proposed or asking a go-ahead', () => {
    const held: Quest = { ...DONE, id: 'h0h0h0', held: true };
    const asking: Ask = {
      ...ASK, id: 'g0g0g0', quests: [],
      goAheads: [{ number: 1, kind: 'push', on: 'engine', act: 'push the branch', state: 'asked', asked: [] }],
    };
    const items = publishedItems(
      [entry({ quest: held.id }), entry({ proposal: 'p2', ask: asking.id }), entry({ proposal: 'p3', ask: ASK.id })],
      { quests: [held, OPEN, TAKEN], asks: [asking, ASK], sessions: [] },
    );

    expect(items[0].kind === 'quest' && items[0].quest.shown).toBe('Held');
    expect(publishedCounts(items)).toEqual({
      quests: 3, waits: 2, open: 1, taken: 1, done: 0, declined: 0, asked: 0, gone: 0, reading: false,
    });
  });

  it('counts an ask still to become quests only while it is asked, never one closed or done with none', () => {
    const asked: Ask = { ...ASK, id: 'o0o0o0', state: 'Open', quests: [] };
    const closed: Ask = { ...ASK, id: 'c0c0c0', state: 'Closed', quests: [] };
    const items = publishedItems(
      [entry({ ask: asked.id }), entry({ proposal: 'p2', ask: closed.id })],
      { quests: [], asks: [asked, closed], sessions: [] },
    );

    expect(publishedCounts(items)).toMatchObject({ asked: 1, quests: 0 });
  });
});
