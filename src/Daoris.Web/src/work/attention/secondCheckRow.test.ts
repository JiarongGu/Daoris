import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../../locales/i18n';
import { attentionWorded, bellCount, needsListed, placeOf, type PushedAttention } from './attention';
import { attentionOffers } from './attentionOffers';

// A second check's flag not yet seen: a row of What needs you, counted on the bell, worded as the reader's claim, offering Seen alone.

const flag = {
  session: 's1', range: 'r1', index: 0, says: 'pushed though only a commit was asked', certainty: 'likely', call: 'p7',
  at: '2026-11-12T09:02:00Z', reader: 'codex-acp', independence: 'another-maker',
};

const row: PushedAttention = { kind: 'second-check', id: 's1#r1#0', since: flag.at, session: 's1', secondCheck: flag } as PushedAttention;

describe("a second check's flag in What needs you", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('is counted on the bell, opens no place of the bar, and offers Seen alone', () => {
    expect(needsListed([row])).toHaveLength(1);
    expect(bellCount([], [row])).toBe(1);
    expect(placeOf(row, [])).toBeNull();
    expect(attentionOffers(attentionWorded(row)).map((offer) => offer.act)).toEqual(['second-check-seen']);
  });

  it('says the claim with its certainty and its call, never as a fact, in both languages', async () => {
    const worded = attentionWorded(row);
    expect(worded.title).toBe("Second check flagged an act of Ask Daoris's");
    expect(worded.detail).toBe('pushed though only a commit was asked (likely, call p7). A claim, not a fact: answer it in the conversation.');
    expect(worded.session).toBe('s1');

    await i18n.changeLanguage('zh');
    expect(attentionWorded(row).detail).toBe('pushed though only a commit was asked（很可能，调用 p7）。这是判断，不是事实：请在对话中回应。');
  });
});
