import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { noticeWords } from './DoneAndFailed';

describe('Done and failed: a step passed on your words', () => {
  it.each([
    ['en', 'review', 'reached', 'Your review of run q1 was accepted on your words kept on ask #a1'],
    ['en', 'quest.holds', 'skipped', "The quest's holds of run q1 was passed on your words kept on ask #a1"],
    ['zh', 'review', 'reached', '进展 q1 的你的审阅已按你留在需求 #a1 上的原话采纳'],
    ['zh', 'quest.holds', 'skipped', '进展 q1 的委托的拦阻已按你留在需求 #a1 上的原话放行'],
  ])('says the step, the run and the ask its words are kept on (%s, %s)', async (language, route, state, words) => {
    await i18n.changeLanguage(language);
    expect(noticeWords({
      id: `through:q1/${route}/1`, kind: 'through', at: '2026-11-12T09:02:00.000Z', read: false, quest: 'q1', act: route, route, state, code: 'a1', count: 1,
    }, i18n.t)).toBe(words);
    await i18n.changeLanguage('en');
  });
});
