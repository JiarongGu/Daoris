import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { askStanding, standingLine } from './askStanding';
import type { AtWorkQuest } from './types';

// An ask counts its quests by where they stand, so it never sits at zero while its quests are worked, checked or wait on the person
// before their Done is written at their workflow's end.

const quest = (id: string, status: string, state?: string): AtWorkQuest => ({
  id, title: `Quest ${id}`, status, ...(state ? { taker: { session: `s-${id}`, agent: 'claude-code', state } } : status === 'Open' ? { taker: { untaken: true } } : {}),
});

describe("an ask's quests by where they stand", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('counts landed, waiting on you, in checks or opinion, being worked and not started', () => {
    const counts = askStanding([
      quest('a', 'Done', 'completed'), quest('b', 'Taken', 'awaiting-person'), quest('c', 'Taken', 'awaiting-step'),
      quest('d', 'Taken', 'awaiting-step'), quest('e', 'Taken', 'working'), quest('f', 'Open'), quest('g', 'Declined'),
    ]);

    expect(counts).toEqual({ landed: 1, you: 1, checks: 2, working: 1, notStarted: 1 });
  });

  it('a quest taken on another machine is being worked, and one taken with no record here too', () => {
    expect(askStanding([
      { id: 'x', title: 'X', status: 'Taken', taker: { elsewhere: true } }, { id: 'y', title: 'Y', status: 'Taken', taker: { unrecorded: true } },
    ])).toEqual({ landed: 0, you: 0, checks: 0, working: 2, notStarted: 0 });
  });

  it('says each count that is not zero, in both languages', async () => {
    const counts = { landed: 2, you: 1, checks: 0, working: 3, notStarted: 0 };
    expect(standingLine(i18n.t, counts)).toBe('2 landed · 1 waiting on you · 3 being worked');
    await i18n.changeLanguage('zh');
    expect(standingLine(i18n.t, counts)).toBe('2 个已落地 · 1 个等你 · 3 个进行中');
    expect(standingLine(i18n.t, { landed: 0, you: 0, checks: 0, working: 0, notStarted: 0 })).toBeNull();
  });
});
