import { describe, expect, it } from 'vitest';
import i18n from './i18n';
import en from './locales/en.json';
import zh from './locales/zh.json';

describe('the catalogs', () => {
  it('agree on their key sets at runtime, not only in the build gate', () => {
    // scripts/i18n-check.mjs guards the build; this guards anyone running tests without it.
    expect(Object.keys(zh).sort()).toEqual(Object.keys(en).sort());
  });

  /**
   * One word for the scope (D75): the interface said *workspace* 41 times and *circle* 26, 工作区 27
   * and 圈子 24, and once both in one tooltip. The CLI's word is the one both doors can share. A key or
   * a placeholder may still be named `circle`, because nobody reads those.
   */
  it('call the scope a workspace, never a circle, in both languages', () => {
    const said = (value: string) => value.replace(/\{\{[^}]*\}\}/g, '');
    expect(Object.entries(en).filter(([, value]) => /\bcircles?\b/i.test(said(value))).map(([key]) => key)).toEqual([]);
    expect(Object.entries(zh).filter(([, value]) => said(value).includes('圈子')).map(([key]) => key)).toEqual([]);
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
