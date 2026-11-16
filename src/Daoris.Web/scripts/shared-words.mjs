// The census of window words shared by two things, beside the names check. The glossary holds one name per
// concept in each language; this holds that no other register of names the window shows gives one of its words to a second thing: the
// driver's seeded roles (`Defaults/roles.json`, by id and English name) and the reporting line's levels (the string constants of the first
// class in `Roles/ReportingLine.cs`, by code, which a wait's line names). Two things share a word where both say it and their Chinese
// differs, a thing with no Chinese of its own differing from every other; one thing named alike in two registers (the seeded supervisor
// and the glossary's) is one thing.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const DRIVER = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver');
export const ROLES = join(DRIVER, 'Defaults', 'roles.json');
export const LINE = join(DRIVER, 'Roles', 'ReportingLine.cs');

/** The level codes a `ReportingLine.cs` text declares: the string constants of the file's first class, in order; null where it has none. */
export function lineLevels(text) {
  const start = text.search(/public static class \w+/);
  const open = start < 0 ? -1 : text.indexOf('{', start);
  const close = open < 0 ? -1 : text.indexOf('\n}', open);
  if (close < 0) return null;
  const codes = [...text.slice(open, close).matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]);
  return codes.length ? codes : null;
}

/** The seeded roles and the line's levels as this checkout holds them; null for a register that is not there or reads empty. */
export function readRegisters(roles = ROLES, line = LINE) {
  return {
    roles: existsSync(roles) ? JSON.parse(readFileSync(roles, 'utf8')).roles ?? null : null,
    levels: existsSync(line) ? lineLevels(readFileSync(line, 'utf8')) : null,
  };
}

/** Every named thing of the three registers, with the words the window says it by and its Chinese names. */
export function things(glossary, { roles, levels }) {
  return [
    ...glossary.terms.filter((term) => !term.use)
      .map((term) => ({ thing: `term ${term.term}`, words: [term.term, term.en], zh: [term.zh, ...(term.zhForms ?? [])] })),
    ...(roles ?? []).map((role) => ({ thing: `role ${role.id}`, words: [role.id, role.name?.en ?? ''], zh: [role.name?.zh ?? ''] })),
    ...(levels ?? []).map((level) => ({ thing: `level ${level}`, words: [level], zh: [] })),
  ];
}

/** A finding a word two things share in the names check's shape (rule `shared`, a fact, so `--strict` fails it), in the word's order. */
export function shared(list) {
  const byWord = new Map();
  for (const each of list) {
    for (const word of new Set(each.words.map((said) => String(said).trim().toLowerCase()).filter(Boolean))) {
      byWord.set(word, [...(byWord.get(word) ?? []), each]);
    }
  }
  const findings = [];
  for (const [word, holders] of [...byWord].sort(([a], [b]) => a.localeCompare(b))) {
    const apart = holders.filter((each) => holders.some((other) => other !== each && !other.zh.some((name) => name && each.zh.includes(name))));
    if (apart.length < 2) continue;
    const named = apart.map((each) => `${each.thing} (${each.zh.filter(Boolean).join(' or ') || 'no Chinese'})`).join(', ');
    findings.push({ key: `shared:${word}`, kind: 'sentence', rule: 'shared', language: 'en', message: `one window word for two things: ${named}`, value: word });
  }
  return findings;
}
