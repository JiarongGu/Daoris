#!/usr/bin/env node
// The en/zh parity gate — the bilingual sibling's proven check, adopted here (D42). The build fails
// when the catalogs' key sets diverge, because a missing translation otherwise "works" in English and
// is discovered by the first reader it fails — silently, in the other language.
//
// And since REV3, when their PLACEHOLDERS diverge: a placeholder is part of the contract
// (`translation-parity`), and a key present in both with `{{nmae}}` in one, or with the `{{name}}`
// the sentence exists to say dropped from the other, passed the key check and failed the reader.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const locales = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'locales');
const catalog = (language) => JSON.parse(readFileSync(join(locales, `${language}.json`), 'utf8'));

const enCatalog = catalog('en');
const zhCatalog = catalog('zh');
const en = new Set(Object.keys(enCatalog));
const zh = new Set(Object.keys(zhCatalog));
const missing = [...en].filter((key) => !zh.has(key));
const stale = [...zh].filter((key) => !en.has(key));

if (missing.length || stale.length) {
  if (missing.length) console.error(`i18n: missing in zh:\n  ${missing.join('\n  ')}`);
  if (stale.length) console.error(`i18n: in zh but not en (stale):\n  ${stale.join('\n  ')}`);
  process.exit(1);
}

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
// language says in its own words (the defaults' reasons, D72). Named here, so adding one is noticed.
const PASSED_THROUGH = [/^settings\.rules\.defaultWhy\./];

const mismatched = [];
for (const key of en) {
  const inEnglish = family(enCatalog, key);
  const inChinese = family(zhCatalog, key);
  const invented = [...placeholders(zhCatalog[key])].filter((name) => !inEnglish.has(name));
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

console.log(`i18n: en and zh agree on ${en.size} keys and their placeholders`);
