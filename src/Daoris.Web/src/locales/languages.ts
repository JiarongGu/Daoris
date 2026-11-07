import i18n from 'i18next';
import { languages } from './index';

/** The page language `language` stands for: itself where a catalogue is present, else its base language (`zh-CN` → `zh`), else English. */
export function pageLanguage(language: string): string {
  const lower = language.toLowerCase();
  return languages.find((each) => each.toLowerCase() === lower) ?? languages.find((each) => each.toLowerCase() === lower.split('-')[0]) ?? 'en';
}

/**
 * A language named in itself, so a person who cannot read the page's current language can still find theirs: the name its own
 * catalogue gives (`language.<tag>`), else the platform's name for the tag, else the tag.
 */
export function languageName(tag: string): string {
  const own = i18n.isInitialized ? i18n.getResource(tag, 'translation', `language.${tag}`) : undefined;
  if (typeof own === 'string' && own !== '') return own;
  try {
    return new Intl.DisplayNames([tag], { type: 'language' }).of(tag) ?? tag;
  } catch {
    return tag;
  }
}
