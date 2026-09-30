#!/usr/bin/env node
// The names check (NAME1a, D116; docs/2026-10-01-naming-design.md §6), beside the parity check. The parity
// check holds that both catalogues have every key; this holds that each key a person reads as a name says
// what the glossary (`src/locales/glossary.json`) says it should, in each language:
//
// - glossary: a label whose English names a term says the term's Chinese name, and neither language uses a
//   word the term must not be called;
// - budget: a label is no longer than its kind's budget, English in characters and Chinese in units;
// - form: case by kind, no closing stop or colon, one ellipsis, no pronoun on a Chinese button, Latin set
//   apart from Chinese and no Latin punctuation inside it;
// - door: a door says its destination's name.
//
// Alone it REPORTS and exits 0. It exits 2 when the glossary itself is malformed, since a glossary that
// cannot be read has stopped checking anything. `--strict` exits 1 on a glossary, form or door finding
// (facts) and never on a budget (a judgement: a character count estimates a width, and the window is where
// a width is a fact, D54). NAME1b renamed what NAME1a's audit found and put `--strict` in the web's build,
// beside the parity check; `--all` also reads every sentence for the words a term must not be called, and
// reports.
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from '../../../tools/fsx.mjs';

const LOCALES = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'locales');
const PLURAL = /_(zero|one|two|few|many|other)$/;
const KINDS = [
  'nav', 'title', 'tab', 'section', 'field', 'choice', 'button', 'status', 'menu', 'command', 'headline',
  'placeholder', 'toast', 'sentence',
];
/** A palette row and a menu item are a name, then a dash and a gloss; the gloss is a sentence, so only the name is measured. */
const nameOf = (text, language) => String(text).split(language === 'en' ? ' — ' : '——')[0];
/** The kinds whose names the form rules hold; a toast and a sentence are sentences. */
const LABELS = new Set(KINDS.filter((kind) => kind !== 'toast' && kind !== 'sentence'));
/** A character a Chinese face sets a full em wide: ideographs, CJK and full-width marks, 「……」 and 「——」. */
const WIDE = /[—…“”⺀-鿿豈-﫿︰-﹏＀-￯]/;
const HAN = '\\u4e00-\\u9fff';
const PLACEHOLDER = /\{\{\s*([\w.]+)[^}]*\}\}/g;
const CODE = /`[^`\n]+`/g;

/** A glossary, both catalogues, read from the web's own locales. */
export function load(root = LOCALES) {
  const catalogue = (language) => Object.assign({}, ...readdirSync(join(root, language))
    .filter((file) => file.endsWith('.json'))
    .sort()
    .map((file) => JSON.parse(readFileSync(join(root, language, file), 'utf8'))));
  return {
    glossary: JSON.parse(readFileSync(join(root, 'glossary.json'), 'utf8')),
    en: catalogue('en'),
    zh: catalogue('zh'),
  };
}

/** A key's kind: its own entry before any prefix, a longer prefix before a shorter, else a sentence. */
export function kindOf(glossary) {
  const exact = new Map();
  const prefixes = [];
  for (const [kind, { keys }] of Object.entries(glossary.kinds)) {
    for (const pattern of keys) {
      if (pattern.endsWith('.*')) prefixes.push({ prefix: pattern.slice(0, -1), kind });
      else exact.set(pattern, kind);
    }
  }
  prefixes.sort((a, b) => b.prefix.length - a.prefix.length);
  return (key) => {
    const stem = key.replace(PLURAL, '');
    return exact.get(stem) ?? prefixes.find(({ prefix }) => stem.startsWith(prefix))?.kind ?? 'sentence';
  };
}

/** How long a name is: English in characters, Chinese in units; a placeholder as its typical value. */
export function measure(text, language, rules) {
  const filled = String(text)
    .replace(PLACEHOLDER, (_, name) => 'x'.repeat(rules.numericPlaceholders.includes(name) ? rules.numericLength : rules.otherLength))
    .replace(/`/g, '');
  if (language === 'en') return [...filled].length;
  let units = 0;
  for (const character of filled) units += WIDE.test(character) ? 1 : rules.latinUnit;
  return units;
}

/** What a sentence says once its placeholders and code are taken out: the words a person reads as words. */
const plain = (text) => String(text).replace(CODE, ' ').replace(PLACEHOLDER, ' ');
const escape = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const says = (text, word) => new RegExp(`(^|[^\\p{L}])${escape(word)}(?![\\p{L}])`, 'iu').test(text);

/** Problems that make the glossary unreadable as an authority. Empty when it can be trusted. */
export function validate(glossary, en, zh) {
  const problems = [];
  const keys = Object.keys(en);
  const kinds = glossary.kinds ?? {};
  for (const kind of KINDS) if (!(kind in kinds)) problems.push(`the kind ${kind} is missing`);
  const claimedBy = new Map();
  for (const [kind, { keys: patterns = [] }] of Object.entries(kinds)) {
    if (!KINDS.includes(kind)) problems.push(`${kind} is not one of the design's kinds`);
    for (const pattern of patterns) {
      if (claimedBy.has(pattern)) problems.push(`${pattern} is named by both ${claimedBy.get(pattern)} and ${kind}`);
      claimedBy.set(pattern, kind);
      if (pattern.endsWith('.*')) {
        if (!keys.some((key) => key.startsWith(pattern.slice(0, -1)))) problems.push(`${kind}: ${pattern} names no key in the catalogues`);
      } else if (!(pattern in en) || !(pattern in zh)) {
        problems.push(`${kind}: ${pattern} is not a key in both catalogues`);
      }
    }
  }
  const named = new Map();
  const byTerm = new Map((glossary.terms ?? []).map((term) => [term.term, term]));
  for (const term of glossary.terms ?? []) {
    if (!term.avoid || !Array.isArray(term.avoid.en) || !Array.isArray(term.avoid.zh)) problems.push(`${term.term}: its avoid lists are missing`);
    if (term.use) {
      if (!byTerm.get(term.use)?.en) problems.push(`${term.term} points at ${term.use}, which is not a named term`);
      continue;
    }
    if (!term.en || !term.zh) {
      problems.push(`${term.term} has no name in ${term.en ? 'Chinese' : 'English'}`);
      continue;
    }
    for (const [language, name] of [['English', term.en.toLowerCase()], ['Chinese', term.zh]]) {
      const already = named.get(`${language}:${name}`);
      if (already) problems.push(`${already} and ${term.term} share the ${language} name ${name}`);
      named.set(`${language}:${name}`, term.term);
    }
    try {
      new RegExp(term.match, 'i');
    } catch {
      problems.push(`${term.term}: its match does not compile`);
    }
  }
  for (const { door, opens } of glossary.doors ?? []) {
    for (const key of [door, opens]) if (!(key in en) || !(key in zh)) problems.push(`door ${door}: ${key} is not a key in both catalogues`);
  }
  return problems;
}

/** Everything the glossary finds in the catalogues: one finding a key, a rule and a language. */
export function check(glossary, en, zh, { all = false } = {}) {
  const kindFor = kindOf(glossary);
  const byTerm = new Map(glossary.terms.map((term) => [term.term, term]));
  const named = glossary.terms.filter((term) => !term.use);
  const matchers = named.map((term) => ({ term, pattern: new RegExp(term.match, 'i') }));
  // A proper noun keeps its capitals mid-name, and so does a place's own name (*Open Sessions*).
  const proper = [...glossary.properNouns, ...Object.keys(en).filter((key) => kindFor(key) === 'nav').map((key) => en[key])]
    .filter((name) => /^[A-Z]/.test(name))
    .sort((a, b) => b.length - a.length);
  const findings = new Map();
  const note = (key, kind, rule, language, message) => {
    const at = `${key}|${rule}|${language}`;
    const value = language === 'en' ? en[key] : zh[key];
    const found = findings.get(at);
    if (found) found.message += `; ${message}`;
    else findings.set(at, { key, kind, rule, language, message, value });
  };

  for (const key of Object.keys(en)) {
    const kind = kindFor(key);
    const label = LABELS.has(kind);
    if (!label && !all) continue;
    const english = plain(en[key]);
    const chinese = plain(zh[key] ?? '');

    // The glossary: what a label's English names, its Chinese names by the term's name. A sentence may
    // say a thing its own way, so it is held only to the words a term must not be called.
    const said = matchers.filter(({ pattern }) => pattern.test(english)).map(({ term }) => term);
    for (const term of said) {
      const own = [term.zh, ...(term.zhForms ?? [])];
      if (label && term.pair !== false && !own.some((name) => chinese.includes(name))) {
        note(key, kind, 'glossary', 'zh', `names ${term.term} in English, and says ${own.join(' or ')} nowhere`);
      }
      // Only names are set aside: this term's, and those of the other terms the same English names
      // (NAME1b: *a git remote* says 远程仓库, which holds the 远程 a workspace's remote must not be
      // called). 规范包 holds 包 and is right, while a short form such as 等你 would hide the 等你决定 it is
      // there to catch, so short forms are never set aside.
      const names = [term.zh, ...said.filter((other) => other !== term).map((other) => other.zh)]
        .sort((a, b) => b.length - a.length);
      const rest = names.reduce((text, name) => text.split(name).join('¤'), chinese);
      for (const word of term.avoid.zh) if (rest.includes(word)) note(key, kind, 'glossary', 'zh', `says ${word} for ${term.term}, which is ${term.zh}`);
    }
    for (const term of glossary.terms) {
      const target = term.use ? byTerm.get(term.use) : term;
      for (const word of term.avoid.en) if (says(english, word)) note(key, kind, 'glossary', 'en', `says "${word}"; the name is ${target.en}`);
      if (term.use) for (const word of term.avoid.zh) if (chinese.includes(word)) note(key, kind, 'glossary', 'zh', `says ${word}; the name is ${target.zh}`);
    }
    if (!label) continue;

    // The budget, from the room the frame gives the kind.
    const budget = glossary.kinds[kind]?.budget;
    for (const language of ['en', 'zh']) {
      const value = language === 'en' ? en[key] : zh[key] ?? '';
      const length = measure(kind === 'menu' || kind === 'command' ? nameOf(value, language) : value, language, glossary.measure);
      if (budget && length > budget[language]) {
        note(key, kind, 'budget', language, `${length} ${language === 'en' ? 'characters' : 'units'}, over the ${kind} budget of ${budget[language]}`);
      }
    }

    // The form, by kind: English's case and marks.
    const casing = glossary.kinds[kind]?.case;
    const raw = String(en[key]).trim();
    if (/\.\.\./.test(raw)) note(key, kind, 'form', 'en', 'three dots where … belongs');
    else if (/[.:]$/.test(raw)) note(key, kind, 'form', 'en', 'ends in a full stop or a colon');
    const words = english.replace(/[^\p{L}\p{N}'’\s-]/gu, ' ').trim();
    const opening = proper.find((name) => words.startsWith(name));
    const first = words.match(/\p{L}/u)?.[0];
    const startsWithCode = /^\s*`/.test(en[key]) || /^\s*\{\{/.test(en[key]);
    if (first && !startsWithCode && !opening) {
      if (casing === 'sentence' && first !== first.toUpperCase()) note(key, kind, 'form', 'en', `starts lower-case; a ${kind} is sentence case`);
      if (casing === 'lower' && first !== first.toLowerCase()) note(key, kind, 'form', 'en', `starts capitalised; a ${kind} is lower case`);
    }
    if (casing === 'sentence') {
      const unnamed = proper.reduce((text, name) => text.split(name).join(' ¤ '), words);
      const capitals = unnamed.split(/\s+/).slice(1).filter((word) => /^\p{Lu}\p{Ll}/u.test(word));
      if (capitals.length) note(key, kind, 'form', 'en', `Title Case (${capitals.join(', ')}); a ${kind} is sentence case`);
    }

    // And Chinese's marks.
    const hanzi = String(zh[key] ?? '');
    const bare = hanzi.replace(PLACEHOLDER, '').replace(CODE, 'X');
    if (/\.\.\./.test(hanzi)) note(key, kind, 'form', 'zh', 'three dots where … belongs');
    if (/……/.test(hanzi)) note(key, kind, 'form', 'zh', '…… where a label takes one …');
    if (/[。：:]$/.test(hanzi.trim())) note(key, kind, 'form', 'zh', 'ends in a full stop or a colon');
    // 它 stands for its object; 这个文件夹 names it, so only the pronoun is reported.
    if (kind === 'button' && /它/.test(hanzi)) note(key, kind, 'form', 'zh', 'names a pronoun, not its object');
    if (new RegExp(`[${HAN}][A-Za-z0-9]|[A-Za-z0-9][${HAN}]`).test(bare)) note(key, kind, 'form', 'zh', 'Latin or a number not set apart from Chinese');
    if (new RegExp(`[${HAN}][,;:?!()]|[,;:?!(][${HAN}]`).test(bare)) note(key, kind, 'form', 'zh', 'Latin punctuation inside Chinese');
    if (/(^|[^—])—([^—]|$)/.test(hanzi)) note(key, kind, 'form', 'zh', 'a single — where Chinese takes ——');
  }

  // The doors: each says its destination's name, singular or plural, in each language.
  for (const { door, opens } of glossary.doors) {
    if (!(door in en) || !(opens in en)) continue;
    const kind = kindFor(door);
    const englishDoor = plain(en[door].split(' — ')[0]).toLowerCase();
    const destination = plain(en[opens]).replace(/[…:]/g, '').trim().toLowerCase();
    const singular = destination.replace(/s$/, '');
    if (!englishDoor.includes(destination) && !englishDoor.includes(singular)) {
      note(door, kind, 'door', 'en', `opens ${opens} ("${en[opens]}") and does not say its name`);
    }
    const chineseDoor = plain(zh[door] ?? '').split('——')[0];
    const chineseDestination = plain(zh[opens] ?? '').replace(/[…：（）]/g, '').trim();
    if (!chineseDoor.includes(chineseDestination)) {
      note(door, kind, 'door', 'zh', `opens ${opens} (「${zh[opens]}」) and does not say its name`);
    }
  }
  return [...findings.values()];
}

/** The findings as a person reads them: the count per rule, then each finding, labels by kind. */
export function report(findings, { labels } = {}) {
  const count = (rule, language) => findings.filter((finding) => finding.rule === rule && (!language || finding.language === language)).length;
  const lines = [
    `names-check: ${labels ?? '?'} label keys; ${findings.length} findings — `
      + ['glossary', 'budget', 'form', 'door'].map((rule) => `${rule} ${count(rule)} (en ${count(rule, 'en')}, zh ${count(rule, 'zh')})`).join(', '),
    'Under --strict (the build runs it) a glossary, form or door finding fails; the budgets are a report and never fail (D54).',
  ];
  const order = (finding) => `${KINDS.indexOf(finding.kind).toString().padStart(2, '0')}|${finding.key}|${finding.rule}|${finding.language}`;
  for (const finding of [...findings].sort((a, b) => order(a).localeCompare(order(b)))) {
    lines.push(`  ${finding.rule} ${finding.language} ${finding.key} [${finding.kind}]: ${finding.message} — ${JSON.stringify(finding.value)}`);
  }
  return lines.join('\n');
}

/** The exit code: 1 under --strict when a fact is found (glossary, form, door), never for a budget. */
export function verdict(findings, { strict }) {
  return strict && findings.some((finding) => finding.rule !== 'budget') ? 1 : 0;
}

if (isMain(import.meta.url)) {
  const flags = new Set(process.argv.slice(2));
  const { glossary, en, zh } = load();
  const problems = validate(glossary, en, zh);
  if (problems.length) {
    console.error(`names-check: the glossary is malformed:\n  ${problems.join('\n  ')}`);
    process.exit(2);
  }
  const findings = check(glossary, en, zh, { all: flags.has('--all') });
  const kindFor = kindOf(glossary);
  const labels = Object.keys(en).filter((key) => LABELS.has(kindFor(key)) && !PLURAL.test(key)).length;
  console.log(flags.has('--json') ? JSON.stringify(findings, null, 2) : report(findings, { labels }));
  process.exit(verdict(findings, { strict: flags.has('--strict') }));
}
