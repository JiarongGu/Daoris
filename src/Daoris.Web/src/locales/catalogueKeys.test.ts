import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const here = import.meta.dirname;

/** Every top-level key of one catalogue file as it is written, a repeat included (parsing the JSON would keep only the last). */
function writtenKeys(text: string): string[] {
  return [...text.matchAll(/^ {2}"((?:[^"\\]|\\.)+)"\s*:/gm)].map((match) => match[1]);
}

/**
 * A key written twice in one language, in one file or in two, is read as the last one written: the earlier sentence is dead, and a card
 * that names facts the later one lacks says the wrong thing in silence.
 */
describe('the catalogues\' keys', () => {
  for (const language of readdirSync(here, { withFileTypes: true }).filter((entry) => entry.isDirectory()).map((entry) => entry.name)) {
    it(`word no key twice in ${language}`, () => {
      const seen = new Map<string, string>();
      const repeated: string[] = [];
      for (const file of readdirSync(join(here, language)).filter((name) => name.endsWith('.json'))) {
        for (const key of writtenKeys(readFileSync(join(here, language, file), 'utf8'))) {
          if (seen.has(key)) repeated.push(`${key} (${seen.get(key)}, ${file})`);
          else seen.set(key, file);
        }
      }

      expect(repeated).toEqual([]);
    });
  }
});
