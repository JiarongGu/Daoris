import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { en, zh } from '.';

/**
 * A session note's codes are a twin (LANG1a, D142 point 4; the language design §5; `.claude/knowledge/twins.md`): two writers
 * declare them, the driver's `NoteCodes` and the service's `LedgerNoteCodes`, and this page words them, with no code shared.
 * This side parses both declarations, one per line as each writer writes them, and holds both catalogues to them: every
 * code has its entry saying exactly its values, and every `note.*` entry is a code a writer declares, so a retired code
 * leaves nothing behind. Each writer's own test reads the catalogues from its side; the parity gate holds `en` and `zh` to one
 * key set. LANG1b's `note.ts` map joins this table when it is built.
 */

type Declared = { code: string; values: string[]; key: string; writer: string };

const root = join(process.cwd(), '..', '..');
const DECLARATION = /new\("(?<code>[a-z][a-z.-]*)", \[(?<values>[^\]]*)\](?:, Key: "(?<key>[^"]+)")?\)/g;

/** A writer's codes, read from its source as it declares them. */
function declared(writer: string, path: string): Declared[] {
  const source = readFileSync(join(root, ...path.split('/')), 'utf8');
  return [...source.matchAll(DECLARATION)].map((match) => {
    const { code, values, key } = match.groups as { code: string; values: string; key?: string };
    return {
      code,
      values: values.split(',').map((value) => value.trim().replace(/^"|"$/g, '')).filter((value) => value.length > 0),
      key: key ?? `note.${code}`,
      writer,
    };
  });
}

const driver = declared('driver', 'src/Daoris.Desktop/Daoris.Desktop.Driver/NoteCodes.cs');
const ledger = declared('service', 'src/Daoris.Service/Daoris.Service.Core/NoteParts.cs');
const codes = [...driver, ...ledger];

const placeholders = (text: string): string[] =>
  [...new Set([...text.matchAll(/\{\{\s*([\w.]+)[^}]*\}\}/g)].map((match) => match[1]))].sort();

describe('a session note’s codes, held to both catalogues', () => {
  it('reads both writers’ declarations', () => {
    // A scan that matched nothing would pass on a moved or renamed file: it has to see them.
    expect(driver.length).toBeGreaterThan(60);
    expect(ledger.map((code) => code.code)).toEqual(['ledger.answered', 'ledger.parked', 'ledger.went-on']);
    expect(new Set(codes.map((code) => code.code)).size).toBe(codes.length);
  });

  it.each([['en', en], ['zh', zh]] as const)('words every code in %s, saying exactly its values', (_, catalogue) => {
    for (const { code, values, key, writer } of codes) {
      const entry = catalogue[key];
      expect(entry, `${writer}'s \`${code}\` has no \`${key}\``).toBeTruthy();
      expect(placeholders(entry ?? ''), `\`${key}\` and the values \`${code}\` carries`).toEqual([...values].sort());
    }
  });

  it.each([['en', en], ['zh', zh]] as const)('keeps no `note.*` entry in %s that no writer declares', (_, catalogue) => {
    const keys = new Set(codes.map((code) => code.key));
    const orphans = Object.keys(catalogue).filter((key) => key.startsWith('note.') && !keys.has(key));
    expect(orphans).toEqual([]);
  });
});
