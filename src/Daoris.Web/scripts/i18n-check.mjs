#!/usr/bin/env node
// The parity gate — the bilingual sibling's proven check, adopted here (D42). The build fails when the catalogs' key sets diverge,
// because a missing translation otherwise "works" in English and is discovered by the first reader it fails — silently, in the other
// language.
//
// And since REV3, when their PLACEHOLDERS diverge: a placeholder is part of the contract
// (`translation-parity`), and a key present in both with `{{nmae}}` in one, or with the `{{name}}`
// the sentence exists to say dropped from the other, passed the key check and failed the reader.
//
// And since MOD2, per AREA: each language is `src/locales/<language>/<area>.json`, merged at load
// (`src/locales/index.ts`). A key lives in the file named by its longest dotted prefix that has a
// file, so a new key has one home, found by its prefix. The gate holds that every language has the
// same areas, that each area's files hold the same keys (both directions), that no key is in two
// files — the merge would keep only the last — and that every key is in its home.
//
// And since I18NOPEN1 (D222), across EVERY catalogue present: a language is a folder under `src/locales`, `en` the source the rest
// are held to, so adding a language is adding a catalogue and the gate holds it the moment it exists.
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from '../../../tools/fsx.mjs';

const LOCALES = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'locales');
const SOURCE = 'en';

/** The languages present under `root`: a folder each, English first, then the rest in order. */
export function languagesIn(root = LOCALES) {
  const found = readdirSync(root, { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name).sort();
  return [...found.filter((name) => name === SOURCE), ...found.filter((name) => name !== SOURCE)];
}

/** A language's areas, by name, each as its file holds it. */
const areasOf = (root, language, problems) => Object.fromEntries(
  readdirSync(join(root, language))
    .filter((file) => file.endsWith('.json'))
    .sort()
    .map((file) => {
      try {
        return [file.slice(0, -'.json'.length), JSON.parse(readFileSync(join(root, language, file), 'utf8'))];
      } catch (error) {
        problems.push(`${language}/${file} does not parse: ${error.message}`);
        return [file.slice(0, -'.json'.length), {}];
      }
    }));

/** The area a key belongs in: the longest area name that is the key or a dotted prefix of it. */
const homeOf = (key, names) =>
  names.filter((name) => key === name || key.startsWith(`${name}.`)).sort((a, b) => b.length - a.length)[0] ?? null;

/** The placeholders a sentence interpolates — `{{name}}`, `{{ name, format }}` — by name. */
const placeholders = (text) =>
  new Set([...String(text).matchAll(/\{\{\s*([\w.]+)\s*(?:,[^}]*)?\}\}/g)].map((match) => match[1]));

/** A key and its plural forms are one sentence family: `once` in the singular may be `{{count}}` in the plural. */
const family = (catalogue, key) => {
  const stem = key.replace(/_(zero|one|two|few|many|other)$/, '');
  const names = new Set();
  for (const [candidate, text] of Object.entries(catalogue)) {
    if (candidate === stem || candidate.startsWith(`${stem}_`)) {
      for (const name of placeholders(text)) names.add(name);
    }
  }
  return names;
};

// Keys whose English IS the driver's own sentence, passed through as the only copy, which the other
// language says in its own words (the defaults' reasons, D72; why a quest is sitting, UX5 U27).
// Named here, so adding one is noticed.
const PASSED_THROUGH = [/^settings\.rules\.defaultWhy\./, /^work\.sitting\./];

// The facts the page hands a passed-through sentence beside the driver's words, which the other language may say
// though the English, the driver's own sentence, names none: the quest, the session a person's stop holds it by
// (`sittingSentence`, SESSUX1d), how many failed sessions parked it (SESSUX1i), the account a wait holds it for, until
// when and why (TOOL4g), the ask or quest whose pause holds it (PAUSE1e), the accounts not signed in with each one's
// sign-in (TOOL6g), and the machine whose take holds it with the session here not carried on (CARRY2c; its `session` is
// that machine's). Named here, as the keys are, so a fact the page does not hand is still refused.
const PASSED_FACTS = [
  {
    pattern: /^work\.sitting\./,
    names: ['quest', 'session', 'failed', 'agent', 'account', 'when', 'because', 'pause', 'accounts', 'logins', 'machine', 'last'],
  },
];

/** Every disagreement between the catalogues under `root`, `en` the source, and the line a clean run says. */
export function checkLocales(root = LOCALES) {
  const problems = [];
  const languages = languagesIn(root);
  if (!languages.includes(SOURCE)) return { problems: [`there is no ${SOURCE} catalogue: the source the others are held to`], summary: '' };
  const others = languages.filter((language) => language !== SOURCE);
  const areas = Object.fromEntries(languages.map((language) => [language, areasOf(root, language, problems)]));
  const sourceAreas = Object.keys(areas[SOURCE]);

  // The same areas in every language, both directions.
  for (const language of others) {
    for (const area of sourceAreas) if (!(area in areas[language])) problems.push(`${language} has no ${area}.json (${SOURCE} has)`);
    for (const area of Object.keys(areas[language])) if (!(area in areas[SOURCE])) problems.push(`${SOURCE} has no ${area}.json (${language} has)`);
  }

  for (const language of languages) {
    const names = Object.keys(areas[language]);
    const seen = new Map();
    for (const [area, catalogue] of Object.entries(areas[language])) {
      for (const key of Object.keys(catalogue)) {
        // In two files: the merge keeps the last one read and the other says nothing.
        if (seen.has(key)) problems.push(`${language}: ${key} is in both ${seen.get(key)}.json and ${area}.json`);
        seen.set(key, area);
        const home = homeOf(key, names);
        if (home === null) problems.push(`${language}: ${key} has no area — add ${key.split('.')[0]}.json`);
        else if (home !== area) problems.push(`${language}: ${key} is in ${area}.json, and belongs in ${home}.json`);
      }
    }
  }

  // Each area's files hold the same keys as English's, both directions.
  for (const language of others) {
    for (const area of sourceAreas.filter((name) => name in areas[language])) {
      const source = new Set(Object.keys(areas[SOURCE][area]));
      const held = new Set(Object.keys(areas[language][area]));
      const missing = [...source].filter((key) => !held.has(key));
      const stale = [...held].filter((key) => !source.has(key));
      if (missing.length) problems.push(`missing in ${language}/${area}.json:\n    ${missing.join('\n    ')}`);
      if (stale.length) problems.push(`in ${language}/${area}.json but not ${SOURCE} (stale):\n    ${stale.join('\n    ')}`);
    }
  }

  if (problems.length) return { problems, summary: '' };

  // And the placeholders, which are part of each sentence's contract.
  const merged = (language) => Object.assign({}, ...Object.values(areas[language]));
  const catalogues = Object.fromEntries(languages.map((language) => [language, merged(language)]));
  const mismatched = [];
  for (const key of Object.keys(catalogues[SOURCE])) {
    const inSource = family(catalogues[SOURCE], key);
    const facts = new Set(PASSED_FACTS.filter(({ pattern }) => pattern.test(key)).flatMap(({ names }) => names));
    for (const language of others) {
      const inLanguage = family(catalogues[language], key);
      const invented = [...placeholders(catalogues[language][key])].filter((name) => !inSource.has(name) && !facts.has(name));
      const dropped = PASSED_THROUGH.some((pattern) => pattern.test(key))
        ? []
        : [...placeholders(catalogues[SOURCE][key])].filter((name) => !inLanguage.has(name));
      if (invented.length) mismatched.push(`${key}: ${language} interpolates {{${invented.join('}}, {{')}}}, which ${SOURCE} never passes`);
      if (dropped.length) mismatched.push(`${key}: ${language} drops {{${dropped.join('}}, {{')}}}`);
    }
  }

  if (mismatched.length) return { problems: mismatched, summary: '', placeholders: true };

  const largest = sourceAreas.reduce((top, area) =>
    (Object.keys(areas[SOURCE][area]).length > Object.keys(areas[SOURCE][top]).length ? area : top), sourceAreas[0]);
  const count = Object.keys(catalogues[SOURCE]).length;
  const summary = `i18n: ${languages.join(', ')} agree on ${count} keys in ${sourceAreas.length} areas and their placeholders `
    + `(largest: ${largest}, ${Object.keys(areas[SOURCE][largest]).length})`;
  return { problems: [], summary };
}

if (isMain(import.meta.url)) {
  const { problems, summary, placeholders: onPlaceholders } = checkLocales();
  if (problems.length) {
    console.error(`i18n: the ${onPlaceholders ? 'placeholders' : 'catalogues'} disagree:\n  ${problems.join('\n  ')}`);
    process.exit(1);
  }
  console.log(summary);
}
