import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { noticeWords } from './DoneAndFailed';

describe('Done and failed: a run step outcome', () => {
  it.each([
    ['en', 'reached', 'The push of run q1 is done'],
    ['en', 'failed', 'The push of run q1 failed'],
    ['zh', 'reached', '进展 q1 的推送已完成'],
    ['zh', 'failed', '进展 q1 的推送失败了'],
  ])('says the step by its kind and the run it is of (%s, %s)', async (language, state, words) => {
    await i18n.changeLanguage(language);
    expect(noticeWords({
      id: 'outcome:q1/push/2', kind: 'outcome', at: '2026-11-12T09:02:00.000Z', read: false, quest: 'q1', act: 'push', route: 'push', state, count: 2,
    }, i18n.t)).toBe(words);
    await i18n.changeLanguage('en');
  });
});
