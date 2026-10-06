import type { ComponentType } from 'react';
import { describe, expect, it } from 'vitest';
import { cleanup, render } from '@testing-library/react';
import { composeStories } from '@storybook/react-vite';
import i18n from './i18n';
import { readerIn } from './storyLanguage';

/**
 * A story's language is the story's, never the page's remembered choice (STORY2, found by TRACE1b). A 中文 reader made
 * with `i18n.cloneInstance` shares the page's language detector, and a clone's own start caches its language under
 * `daoris.language` as the page's switch does: one module of 中文 stories, merely imported, brought every later story up
 * in 中文 in that browser profile, English ones too. Storybook imports a story's module to show any story in it, so the
 * import is checked here as well as the render; `stories.test.tsx` checks every story's render.
 */
const KEY = 'daoris.language';
type StoriesModule = Parameters<typeof composeStories>[0];
type Composed = Record<string, ComponentType<Record<string, unknown>>>;
const composed = (module: StoriesModule) => composeStories(module) as unknown as Composed;
const HAN = /\p{Script=Han}/u;

describe('a story in another language (STORY2)', () => {
  // First in the file on purpose: the vm pool gives each file its own module instances, so this import is the module's
  // first here, and its module-scope code runs after the remembered language is set.
  it('shows 中文 and leaves daoris.language as found, from its module’s import to its render', async () => {
    localStorage.setItem(KEY, 'en');
    const quest = composed(await import('./quests/QuestPage.stories'));

    // The same quest and session, in English and then in 中文: the 中文 words are the reader's, not the fixture's.
    const english = render(<quest.LongTitleWithNoShortTitle />);
    expect(english.container.textContent).not.toMatch(HAN);
    cleanup();
    const chinese = render(<quest.LongTitleChineseDark />);
    expect(chinese.container.textContent).toMatch(HAN);
    cleanup();
    expect(localStorage.getItem(KEY)).toBe('en');

    // The menu bar's language is an arg a reviewer sets from the controls, and either one is drawn by a reader of its own.
    const menu = composed(await import('./work/AppMenu.stories'));
    render(<menu.Closed language="zh" />);
    cleanup();
    expect(localStorage.getItem(KEY)).toBe('en');

    // And an English story leaves a 中文 choice as found.
    localStorage.setItem(KEY, 'zh');
    render(<menu.Closed language="en" />);
    cleanup();
    expect(localStorage.getItem(KEY)).toBe('zh');
  });

  it('remembers no language for any story module imported, as a fresh profile finds it', async () => {
    localStorage.removeItem(KEY);
    for (const load of Object.values(import.meta.glob<StoriesModule>('./**/*.stories.tsx'))) await load();
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('hands a reader in the language asked, on the page’s catalogues, leaving the page’s language and choice alone', () => {
    localStorage.setItem(KEY, 'en');
    const was = i18n.language;
    const zh = readerIn('zh');
    expect(zh.language).toBe('zh');
    expect(zh.t('nav.quests')).toBe('委托');
    expect(zh.t('quests.card.sat', { days: 12 })).toBe('搁置 12 天');
    expect(readerIn('en').t('nav.quests')).toBe('Quests');
    expect(i18n.language).toBe(was);
    expect(localStorage.getItem(KEY)).toBe('en');
  });

  it('keeps the page’s own switch remembering the person’s choice', async () => {
    const was = i18n.language;
    await i18n.changeLanguage('zh');
    expect(localStorage.getItem(KEY)).toBe('zh');
    await i18n.changeLanguage('en');
    expect(localStorage.getItem(KEY)).toBe('en');
    await i18n.changeLanguage(was);
  });

  it('clones no reader from the page’s instance in any story: a clone shares the detector that remembers', () => {
    const sources = import.meta.glob<string>('./**/*.stories.tsx', { query: '?raw', import: 'default', eager: true });
    expect(Object.entries(sources).filter(([, source]) => /\.cloneInstance\(/.test(source)).map(([path]) => path)).toEqual([]);
  });
});
