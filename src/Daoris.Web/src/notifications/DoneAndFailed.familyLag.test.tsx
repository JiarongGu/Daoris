import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { noticeWords } from './DoneAndFailed';

describe('Done and failed: a pin that waited past its limit', () => {
  it.each([
    ['en', 'web still pins core at an older commit: bbbbbbbbbbbb breaks it, and the wait has passed its limit'],
    ['zh', 'web 仍固定在 core 的较早提交：bbbbbbbbbbbb 使它出错，等待已超出期限'],
  ])('names the consumer, the producer and the commit that breaks it (%s)', async (language, words) => {
    await i18n.changeLanguage(language);
    expect(noticeWords({
      id: 'family-lag:web.core.202611260930', kind: 'family-lag', at: '2026-11-26T09:30:00.000Z', read: false, repository: 'web', act: 'core', code: 'bbbbbbbbbbbb',
    }, i18n.t)).toBe(words);
    await i18n.changeLanguage('en');
  });
});
