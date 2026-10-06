import type { Decorator } from '@storybook/react-vite';
import type { i18n as I18n } from 'i18next';
import { I18nextProvider } from 'react-i18next';
import i18n from './i18n';

// A story in a language of its own (STORY2). Stories only: nothing in the product imports this.

/**
 * A reader in one language, on the page's catalogues, that leaves the page's remembered language alone.
 *
 * Not `i18n.cloneInstance({ lng })`: a clone shares the page's language detector, and its own start caches its language
 * under `daoris.language` as the page's switch does, so one module of 中文 stories, merely imported, brought every later
 * story up in 中文 in that browser profile (found by TRACE1b). A reader made from the page's options has no detector, so
 * it has nothing to remember with; the page's own switch still remembers, because that is the person's choice. Its
 * catalogues are the page's, so it starts at once, before the story that asks for it renders.
 */
export function readerIn(language: 'en' | 'zh'): I18n {
  const reader = i18n.createInstance({ ...i18n.options, lng: language });
  void reader.init();
  return reader;
}

const ZH = readerIn('zh');

/** The story in 中文, whatever the window's language. */
export const chinese: Decorator = (Story) => <I18nextProvider i18n={ZH}><Story /></I18nextProvider>;
