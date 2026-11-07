import { describe, expect, it } from 'vitest';
import i18n from './i18n';
import { areas, catalogues, en, homeOf, languages, zh } from './index';
import { languageName, pageLanguage } from './languages';

/**
 * One file per area: the build gate (`scripts/i18n-check.mjs`) holds these too, and this holds
 * them for anyone running the tests without it.
 */
describe('the catalogues by area', () => {
  it('are areas at all: every language is read from files, not one file', () => {
    expect(Object.keys(areas.en!).length).toBeGreaterThan(1);
    for (const language of languages) expect(Object.keys(areas[language]!).sort(), language).toEqual(Object.keys(areas.en!).sort());
  });

  it('hold the same keys in each area, both directions, in every language present', () => {
    for (const language of languages) {
      for (const [area, keys] of Object.entries(areas.en!)) {
        expect(Object.keys(areas[language]![area] ?? {}).sort(), `${language}/${area}`).toEqual(Object.keys(keys).sort());
      }
    }
  });

  it('lose no key to the merge: no key is in two areas', () => {
    for (const language of languages) {
      const counted = Object.values(areas[language]!).reduce((sum, keys) => sum + Object.keys(keys).length, 0);
      expect(counted, language).toBe(Object.keys(catalogues[language]!).length);
    }
  });

  it('keep each key in the area its prefix names, so a new key has one home', () => {
    const names = Object.keys(areas.en!);
    const misplaced = Object.entries(areas.en!).flatMap(([area, keys]) =>
      Object.keys(keys).filter((key) => homeOf(key, names) !== area).map((key) => `${key} in ${area}`));
    expect(misplaced).toEqual([]);
  });

  it('find the home by the longest prefix with a file, and none where no file starts it', () => {
    const names = ['settings', 'settings.rules', 'work', 'work.intake'];
    expect(homeOf('settings.rules.add', names)).toBe('settings.rules');
    expect(homeOf('settings.rulesBook', names)).toBe('settings');
    expect(homeOf('settings.rules', names)).toBe('settings.rules');
    expect(homeOf('work.intakeRunning.title', names)).toBe('work');
    expect(homeOf('nav.quests', names)).toBeNull();
  });
});

/** The page's languages are the catalogue folders present, `en` the source; adding a language is adding a folder. */
describe('the languages the page offers', () => {
  it('are the catalogues present, English first, and are what i18next supports', () => {
    expect(languages[0]).toBe('en');
    expect([...languages].sort()).toEqual(Object.keys(areas).sort());
    expect((i18n.options.supportedLngs || []).filter((each) => each !== 'cimode')).toEqual(languages);
  });

  it('stand for a detected language by itself, its base language, else English', () => {
    expect([pageLanguage('zh'), pageLanguage('zh-CN'), pageLanguage('en-GB'), pageLanguage('de')]).toEqual(['zh', 'zh', 'en', 'en']);
  });

  it('name each language in itself: its own catalogue where it says so, else the platform\'s name for it', () => {
    expect([languageName('en'), languageName('zh')]).toEqual(['English', '中文']);
    expect(languageName('fr')).toBe('français');
    expect(languageName('zz-not-a-language')).toBe('zz-not-a-language');
  });
});

describe('the catalogs', () => {
  it('agree on their key sets at runtime, not only in the build gate', () => {
    // scripts/i18n-check.mjs guards the build; this guards anyone running tests without it.
    for (const language of languages) expect(Object.keys(catalogues[language]!).sort(), language).toEqual(Object.keys(en).sort());
  });

  /**
 * One word for the scope: the interface said *workspace* 41 times and *circle* 26, 工作区 27
 * and 圈子 24, and once both in one tooltip. The CLI's word is the one both doors can share. A key or
 * a placeholder may still be named `circle`, because nobody reads those.
 */
  it('call the scope a workspace, never a circle, in both languages', () => {
    const said = (value: string) => value.replace(/\{\{[^}]*\}\}/g, '');
    expect(Object.entries(en).filter(([, value]) => /\bcircles?\b/i.test(said(value))).map(([key]) => key)).toEqual([]);
    expect(Object.entries(zh).filter(([, value]) => said(value).includes('圈子')).map(([key]) => key)).toEqual([]);
  });

  /**
 * The page's inline renderer draws `code`, never `**bold**`, so a value that says `**New**` shows its
 * asterisks (the kit card, seen on the window). Emphasis in a catalogue is left out, not marked up.
 */
  it('carry no markdown bold, which the page would show as asterisks', () => {
    const bold = (catalogue: Record<string, string>) =>
      Object.entries(catalogue).filter(([, value]) => /\*\*[^*]+\*\*/.test(value)).map(([key]) => key);
    expect(bold(en)).toEqual([]);
    expect(bold(zh)).toEqual([]);
  });

  /** The same drift for an account: 账户 15 times and 账号 11, found building the menus. */
  it('call an account 账户 in 中文, one word as for the workspace', () => {
    expect(Object.entries(zh).filter(([, value]) => value.includes('账号')).map(([key]) => key)).toEqual([]);
  });

  /**
 * And for a quest: 委托 43 times, and 任务 in about twenty strings the map, the chain
 * strip, the monitor and the strikes setting grew later. 任务 is the family's word for a *task*, and
 * the glossary keeps the two apart, so a string about a quest never says 任务.
 */
  it('call a quest 委托 in 中文, never 任务', () => {
    const aboutQuests = Object.entries(en).filter(([, value]) => /\bquests?\b/i.test(value)).map(([key]) => key);
    expect(aboutQuests.filter((key) => zh[key]!.includes('任务'))).toEqual([]);
  });

  /**
 * A view is named by the name it has. The Work frame is the Sessions view, and two
 * sentences still sent a person to Work: the status bar's sessions tip and a quest's *open in Work*.
 * Nothing on the window is called that any more.
 */
  it('name only views that exist: Work has been Sessions since D66', () => {
    // Except the Roles mode's section of that name, one of the nine kinds of work, and its filter, which are no view,
    // and the drawing's flag *Work done here* with the editor's press that sets it, *Work is done here*, the work a
    // workflow does, and the dialog's *Work alongside*, a second request working beside the first.
    const work = ['nav.roles.work', 'agents.roles.filter.work', 'workflow.graph.end', 'workflow.edit.act.end', 'work.team.door.alongside'];
    expect(Object.entries(en).filter(([key, value]) => !work.includes(key) && /\bWork\b/.test(value)).map(([key]) => key)).toEqual([]);
    expect(Object.entries(zh).filter(([, value]) => /工作台|「工作」/.test(value)).map(([key]) => key)).toEqual([]);
  });

  /**
 * A count is said in its number: *1 session(s)* on the usage rows, where every other
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
