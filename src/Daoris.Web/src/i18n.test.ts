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

  /** The same drift for an account: 账户 15 times and 账号 11, found building the menus (FRAME3). */
  it('call an account 账户 in 中文, one word as for the workspace', () => {
    expect(Object.entries(zh).filter(([, value]) => value.includes('账号')).map(([key]) => key)).toEqual([]);
  });

  /**
   * And for a quest (POLISH4): 委托 43 times, and 任务 in about twenty strings the map, the chain
   * strip, the monitor and the strikes setting grew later. 任务 is the family's word for a *task*, and
   * the glossary keeps the two apart, so a string about a quest never says 任务.
   */
  it('call a quest 委托 in 中文, never 任务', () => {
    const aboutQuests = Object.entries(en).filter(([, value]) => /\bquests?\b/i.test(value)).map(([key]) => key);
    expect(aboutQuests.filter((key) => (zh as Record<string, string>)[key]!.includes('任务'))).toEqual([]);
  });

  /**
   * A view is named by the name it has (UX5 U19). D66 made the Work frame the Sessions view, and two
   * sentences still sent a person to Work: the status bar's sessions tip and a quest's *open in Work*.
   * Nothing on the window is called that any more.
   */
  it('name only views that exist: Work has been Sessions since D66', () => {
    expect(Object.entries(en).filter(([, value]) => /\bWork\b/.test(value)).map(([key]) => key)).toEqual([]);
    expect(Object.entries(zh).filter(([, value]) => /工作台|「工作」/.test(value)).map(([key]) => key)).toEqual([]);
  });

  /**
   * A count is said in its number (UX5 U54): *1 session(s)* on the usage rows, where every other
   * count in the catalogue has its `_other` form. English pluralises; the catalogue can say both.
   */
  it('say a count in its number, never with (s)', () => {
    expect(Object.entries(en).filter(([, value]) => /\w\(s\)/.test(value)).map(([key]) => key)).toEqual([]);
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
