import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { en, zh } from '../locales';
import { HANDED_CUTS } from './handed';

/**
 * An instruction's account carries codes the driver declares and this page words (CONTEXT1, D143 point 1), with no code
 * shared: a twin, as a note's codes are (LANG1a, `.claude/knowledge/twins.md`). This side parses the driver's declarations
 * in `InstructionAccount.cs` and holds both catalogues to them both ways, and the page's map of each cut's values to the
 * driver's. The driver's `HandedCodesTests` reads the catalogues from its side.
 */

const root = join(process.cwd(), '..', '..');
const source = readFileSync(join(root, 'src', 'Daoris.Desktop', 'Daoris.Desktop.Driver', 'InstructionAccount.cs'), 'utf8');

/** A class's codes as the driver declares them: each `const string`, by its name and value. */
function declared(holder: string): Map<string, string> {
  const block = new RegExp(`public static class ${holder}\\s*\\{([\\s\\S]*?)\\n\\}`).exec(source)?.[1] ?? '';
  return new Map([...block.matchAll(/public const string (\w+) = "([^"]+)";/g)].map((match) => [match[1]!, match[2]!]));
}

const FAMILIES = [
  ['work.handed.section.', 'HandedSections'],
  ['work.handed.source.', 'HandedSources'],
  ['work.handed.none.', 'HandedNones'],
  ['work.handed.cut.', 'HandedCuts'],
] as const;

/** Each cut's values as the driver's `HandedCuts.Values` names them. */
function cutValues(): Record<string, string[]> {
  const names = declared('HandedCuts');
  return Object.fromEntries([...source.matchAll(/\[(\w+)\] = \[([^\]]*)\],/g)].map((match) => [
    names.get(match[1]!) ?? `unknown ${match[1]}`,
    match[2]!.split(',').map((value) => value.trim().replace(/^"|"$/g, '')).filter((value) => value.length > 0),
  ]));
}

const placeholders = (text: string): string[] =>
  [...new Set([...text.matchAll(/\{\{\s*([\w.]+)[^}]*\}\}/g)].map((match) => match[1]!))].sort();

describe('an instruction account’s codes, held to both catalogues', () => {
  it('reads the driver’s declarations', () => {
    // A scan that matched nothing would pass on a moved or renamed file: it has to see them.
    expect(declared('HandedSections').size).toBeGreaterThanOrEqual(20);
    expect(declared('HandedSources').size).toBeGreaterThanOrEqual(10);
    expect([...declared('HandedNones').values()].sort()).toEqual(['agent', 'empty', 'no-ask', 'not-answered', 'not-set']);
    expect(Object.keys(cutValues()).sort()).toEqual([...declared('HandedCuts').values()].sort());
  });

  it.each([['en', en], ['zh', zh]] as const)('words every code in %s, and keeps no entry no code declares', (_, catalogue) => {
    for (const [prefix, holder] of FAMILIES) {
      const codes = [...declared(holder).values()].sort();
      const entries = Object.keys(catalogue).filter((key) => key.startsWith(prefix)).map((key) => key.slice(prefix.length)).sort();
      expect(entries, holder).toEqual(codes);
    }
  });

  it.each([['en', en], ['zh', zh]] as const)('says exactly each cut’s values in %s', (_, catalogue) => {
    for (const [code, values] of Object.entries(cutValues())) {
      expect(placeholders(catalogue[`work.handed.cut.${code}`] ?? ''), code).toEqual([...values].sort());
    }
  });

  it('maps exactly the cuts the driver declares, each with its values', () => {
    expect(Object.fromEntries(Object.entries(HANDED_CUTS).map(([code, values]) => [code, [...values]]))).toEqual(cutValues());
  });
});
