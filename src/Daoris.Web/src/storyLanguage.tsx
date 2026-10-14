import type { Decorator } from '@storybook/react-vite';
import type { i18n as I18n } from 'i18next';
import { useEffect, useRef, type ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import i18n from './locales/i18n';

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

/**
 * Points the page's own `i18n` at a language and hands back what puts it as it was. The page's switch remembers through
 * its detector (STORY2), so the detector is set aside for the change and put back after: nothing is cached under
 * `daoris.language`. The catalogues are loaded, so the change is made before this returns.
 */
function pointPageAt(language: 'en' | 'zh'): () => void {
  const was = i18n.language;
  const detector = i18n.services.languageDetector;
  i18n.services.languageDetector = undefined;
  void i18n.changeLanguage(language);
  i18n.services.languageDetector = detector;
  return () => {
    i18n.services.languageDetector = undefined;
    void i18n.changeLanguage(was);
    i18n.services.languageDetector = detector;
  };
}

/** The wrapper a 中文 story draws in: the page's language is 中文 from its first render until it unmounts (ACCTUX4j). */
function InChinese({ children }: { children: ReactNode }) {
  // Set during render, not in an effect: a helper that calls the page's `i18n.t` runs while the children render.
  const restore = useRef<(() => void) | null>(null);
  restore.current ??= pointPageAt('zh');
  // The effect sets it again after a strict-mode remount's cleanup has given the language back.
  useEffect(() => {
    restore.current ??= pointPageAt('zh');
    return () => { restore.current?.(); restore.current = null; };
  }, []);
  return <I18nextProvider i18n={ZH}>{children}</I18nextProvider>;
}

/**
 * The story in 中文, whatever the window's language. What reads the provider's reader (`useTranslation`) speaks 中文
 * through it; a pure helper that calls the page's `i18n.t`, such as `runsForLine` or `nextStartSaid`, speaks it because
 * the page's `i18n` is pointed at 中文 for the story's life and given back after, its remembered choice untouched
 * (ACCTUX4j; ACCTUX4b's 中文 stories had drawn those words in English).
 */
export const chinese: Decorator = (Story) => <InChinese><Story /></InChinese>;
