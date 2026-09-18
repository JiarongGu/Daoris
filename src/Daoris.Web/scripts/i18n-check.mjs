#!/usr/bin/env node
// The en/zh parity gate — the bilingual sibling's proven check, adopted here (D42). The build fails
// when the catalogs' key sets diverge, because a missing translation otherwise "works" in English and
// is discovered by the first reader it fails — silently, in the other language.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const locales = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'locales');
const keysOf = (language) =>
  new Set(Object.keys(JSON.parse(readFileSync(join(locales, `${language}.json`), 'utf8'))));

const en = keysOf('en');
const zh = keysOf('zh');
const missing = [...en].filter((key) => !zh.has(key));
const stale = [...zh].filter((key) => !en.has(key));

if (missing.length || stale.length) {
  if (missing.length) console.error(`i18n: missing in zh:\n  ${missing.join('\n  ')}`);
  if (stale.length) console.error(`i18n: in zh but not en (stale):\n  ${stale.join('\n  ')}`);
  process.exit(1);
}
console.log(`i18n: en and zh agree on ${en.size} keys`);
