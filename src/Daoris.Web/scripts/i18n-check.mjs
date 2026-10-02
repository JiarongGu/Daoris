#!/usr/bin/env node
// The en/zh parity gate — the bilingual sibling's proven check, adopted here (D42). The build fails
// when the catalogs' key sets diverge, because a missing translation otherwise "works" in English and
// is discovered by the first reader it fails — silently, in the other language.
//
// And since REV3, when their PLACEHOLDERS diverge: a placeholder is part of the contract
// (`translation-parity`), and a key present in both with `{{nmae}}` in one, or with the `{{name}}`
// the sentence exists to say dropped from the other, passed the key check and failed the reader.
//
// And since MOD2, per AREA: each language is `src/locales/<language>/<area>.json`, merged at load
// (`src/locales/index.ts`). A key lives in the file named by its longest dotted prefix that has a
// file, so a new key has one home, found by its prefix. The gate holds that both languages have the
// same areas, that each area's two files hold the same keys (both directions), that no key is in two
// files — the merge would keep only the last — and that every key is in its home.
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const locales = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'locales');
const LANGUAGES = ['en', 'zh'];

/** A language's areas, by name, each as its file holds it. */
const areasOf = (language) => Object.fromEntries(
  readdirSync(join(locales, language))
    .filter((file) => file.endsWith('.json'))
    .sort()
    .map((file) => {
      const path = join(locales, language, file);
      try {
        return [file.slice(0, -'.json'.length), JSON.parse(readFileSync(path, 'utf8'))];
      } catch (error) {
        console.error(`i18n: ${language}/${file} does not parse: ${error.message}`);
        process.exit(1);
      }
    }));

/** The area a key belongs in: the longest area name that is the key or a dotted prefix of it. */
const homeOf = (key, names) =>
  names.filter((name) => key === name || key.startsWith(`${name}.`)).sort((a, b) => b.length - a.length)[0] ?? null;

const areas = Object.fromEntries(LANGUAGES.map((language) => [language, areasOf(language)]));
const problems = [];

// The same areas in both languages, both directions.
const enAreas = Object.keys(areas.en);
const zhAreas = Object.keys(areas.zh);
for (const area of enAreas) if (!(area in areas.zh)) problems.push(`zh has no ${area}.json (en has)`);
for (const area of zhAreas) if (!(area in areas.en)) problems.push(`en has no ${area}.json (zh has)`);

for (const language of LANGUAGES) {
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

// Each area's two files hold the same keys, both directions.
for (const area of enAreas.filter((name) => name in areas.zh)) {
  const en = new Set(Object.keys(areas.en[area]));
  const zh = new Set(Object.keys(areas.zh[area]));
  const missing = [...en].filter((key) => !zh.has(key));
  const stale = [...zh].filter((key) => !en.has(key));
  if (missing.length) problems.push(`missing in zh/${area}.json:\n    ${missing.join('\n    ')}`);
  if (stale.length) problems.push(`in zh/${area}.json but not en (stale):\n    ${stale.join('\n    ')}`);
}

if (problems.length) {
  console.error(`i18n: the catalogues disagree:\n  ${problems.join('\n  ')}`);
  process.exit(1);
}

const merged = (language) => Object.assign({}, ...Object.values(areas[language]));
const enCatalog = merged('en');
const zhCatalog = merged('zh');
const en = new Set(Object.keys(enCatalog));

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
// (`sittingSentence`, SESSUX1d), how many failed sessions parked it (SESSUX1i), and the account a wait holds it for, until
// when and why (TOOL4g). Named here, as the keys are, so a fact the page does not hand is still refused.
const PASSED_FACTS = [
  { pattern: /^work\.sitting\./, names: ['quest', 'session', 'failed', 'agent', 'account', 'when', 'because'] },
];

const mismatched = [];
for (const key of en) {
  const inEnglish = family(enCatalog, key);
  const inChinese = family(zhCatalog, key);
  const facts = new Set(PASSED_FACTS.filter(({ pattern }) => pattern.test(key)).flatMap(({ names }) => names));
  const invented = [...placeholders(zhCatalog[key])].filter((name) => !inEnglish.has(name) && !facts.has(name));
  const dropped = PASSED_THROUGH.some((pattern) => pattern.test(key))
    ? []
    : [...placeholders(enCatalog[key])].filter((name) => !inChinese.has(name));
  if (invented.length) mismatched.push(`${key}: zh interpolates {{${invented.join('}}, {{')}}}, which en never passes`);
  if (dropped.length) mismatched.push(`${key}: zh drops {{${dropped.join('}}, {{')}}}`);
}

if (mismatched.length) {
  console.error(`i18n: placeholders disagree:\n  ${mismatched.join('\n  ')}`);
  process.exit(1);
}

const largest = enAreas.reduce((top, area) =>
  (Object.keys(areas.en[area]).length > Object.keys(areas.en[top]).length ? area : top), enAreas[0]);
console.log(`i18n: en and zh agree on ${en.size} keys in ${enAreas.length} areas and their placeholders `
  + `(largest: ${largest}, ${Object.keys(areas.en[largest]).length})`);
