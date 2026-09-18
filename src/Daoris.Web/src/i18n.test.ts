import { describe, expect, it } from 'vitest';
import i18n from './i18n';
import en from './locales/en.json';
import zh from './locales/zh.json';

describe('the catalogs', () => {
  it('agree on their key sets at runtime, not only in the build gate', () => {
    // scripts/i18n-check.mjs guards the build; this guards anyone running tests without it.
    expect(Object.keys(zh).sort()).toEqual(Object.keys(en).sort());
  });

  it('serve flat dotted keys in both languages, placeholders intact', async () => {
    await i18n.changeLanguage('en');
    expect(i18n.t('nav.quests')).toBe('Quests');
    expect(i18n.t('quests.card.sat', { days: 12 })).toBe('sat 12d');

    await i18n.changeLanguage('zh');
    expect(i18n.t('nav.quests')).toBe('委托');
    expect(i18n.t('quests.card.sat', { days: 12 })).toBe('搁置 12 天');

    await i18n.changeLanguage('en');
  });
});
