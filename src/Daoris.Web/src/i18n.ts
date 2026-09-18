import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import en from './locales/en.json';
import zh from './locales/zh.json';

// UI chrome is translated; DATA is not (D42). Quest content, registry declarations, knowledge bodies
// and the service's own sentences render verbatim — machine-translating a refusal would break the
// contract that the service's sentence is the message. English is a catalog like any other, so a
// missing key is a visible `key.name`, never a silent fallback that "works" in one language only.
void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      zh: { translation: zh },
    },
    fallbackLng: 'en',
    supportedLngs: ['en', 'zh'],
    // Flat dotted keys, the bilingual sibling's proven convention: every key greps from code
    // straight to the catalog line, with no nesting to mentally re-join.
    keySeparator: false,
    nsSeparator: false,
    interpolation: { escapeValue: false }, // React escapes; double-escaping mangles the family's CJK
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'daoris.language',
    },
  });

// The document declares what it speaks — screen readers and font selection both read this.
i18n.on('languageChanged', (language) => {
  document.documentElement.lang = language;
});
document.documentElement.lang = i18n.language;

export default i18n;
