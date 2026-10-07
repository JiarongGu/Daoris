import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { NOTE_CODES, NOTE_VALUES } from '../work/noteLines';
import { REASONS } from '../work/say';
import { en, zh } from '.';

/**
 * A session note's codes are a twin (LANG1a, D142 point 4; the language design §5; `.claude/knowledge/twins.md`): two writers
 * declare them, the driver's `NoteCodes` and the service's `LedgerNoteCodes`, and this page words them, with no code shared.
 * This side parses both declarations, one per line as each writer writes them, and holds both catalogues to them: every
 * code has its entry saying exactly its values, and every `note.*` entry is a code a writer declares, so a retired code
 * leaves nothing behind. Each writer's own test reads the catalogues from its side; the parity gate holds `en` and `zh` to one
 * key set. LANG1b's `work/noteLines.ts` map is the third side: held below to both declarations code for code, with the same
 * values, key and family of reasons, and every value a writer declares has a way the page says it.
 */

type Declared = { code: string; values: string[]; key: string; writer: string; why?: string };

const root = join(process.cwd(), '..', '..');
const DECLARATION =
  /new\("(?<code>[a-z][a-z.-]*)", \[(?<values>[^\]]*)\](?:, Key: "(?<key>[^"]+)")?\)(?: \{ Why = (?<why>\w+) \})?/g;

const names = (values: string) =>
  values.split(',').map((value) => value.trim().replace(/^"|"$/g, '')).filter((value) => value.length > 0);

const source = (path: string) => readFileSync(join(root, ...path.split('/')), 'utf8');

/** A writer's codes, read from its source as it declares them. */
function declared(writer: string, path: string): Declared[] {
  return [...source(path).matchAll(DECLARATION)].map((match) => {
    const { code, values, key, why } = match.groups as { code: string; values: string; key?: string; why?: string };
    return { code, values: names(values), key: key ?? `note.${code}`, writer, ...(why ? { why: why.toLowerCase() } : {}) };
  });
}

/** The reasons the driver declares for one family (`NoteCodes.Continue`, `NoteCodes.Cooling`): each key and its values. */
function reasons(family: string): { key: string; values: string[] }[] {
  const text = source('src/Daoris.Desktop/Daoris.Desktop.Driver/NoteCodes.cs');
  const block = new RegExp(`NoteReasons ${family} = new\\(\\s*\\[([\\s\\S]*?)\\]\\);`).exec(text)?.[1] ?? '';
  return [...block.matchAll(/new\(\w+\.\w+, \[([^\]]*)\], "([^"]+)"\)/g)].map((match) => ({ key: match[2], values: names(match[1]) }));
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

describe('the page’s map of a note’s codes, held to both writers', () => {
  /** LANG1b: a code a writer adds fails here until the page maps it; one it retires leaves nothing behind. */
  it('maps exactly the codes the writers declare, each with its values, its key and its family of reasons', () => {
    const page = Object.entries(NOTE_CODES).map(([code, entry]) => ({
      code, values: [...entry.values], key: entry.key ?? `note.${code}`, ...(entry.why ? { why: entry.why } : {}),
    }));
    const declaredCodes = codes.map(({ code, values, key, why }) => ({ code, values, key, ...(why ? { why } : {}) }));
    const byCode = (a: { code: string }, b: { code: string }) => a.code.localeCompare(b.code);
    expect(page.sort(byCode)).toEqual(declaredCodes.sort(byCode));
  });

  it('has a way to say every value a writer declares', () => {
    const values = new Set(codes.flatMap((code) => code.values));
    expect([...values].filter((value) => !(value in NOTE_VALUES))).toEqual([]);
  });

  /**
   * AGT3d (D125's AGT3c note): a limit's cooling line carries whose accounts and the kind of limit, a window the page words
   * by `harness.window.*`; a limit whose reset named none has a line of its own, since a value its entry says and the part
   * lacks shows the line as recorded.
   */
  it('reads a limit’s window and owner on the cooling line, and the line without a window apart', () => {
    const cooling = driver.find((code) => code.code === 'account.cooling');
    const none = driver.find((code) => code.code === 'account.cooling-no-window');
    expect(cooling).toEqual({ code: 'account.cooling', values: ['until', 'why', 'owner', 'window'], key: 'note.account.cooling', writer: 'driver', why: 'cooling' });
    expect(none).toEqual({ code: 'account.cooling-no-window', values: ['until', 'why', 'owner'], key: 'note.account.cooling-no-window', writer: 'driver', why: 'cooling' });
    expect(NOTE_VALUES.window).toBe('window');
    expect(NOTE_VALUES.owner).toBe('text');
  });

  /** A `why` the driver writes is a reason the page words, by the same key, needing the values the driver writes beside it. */
  it('words every reason a `why` may name', () => {
    const continued = reasons('Continue');
    expect(continued.length).toBeGreaterThan(10);
    const page = Object.values(REASONS).map(({ key, needs }) => ({ key, values: [...(needs ?? [])] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual(continued.sort(byKey));

    const cooling = reasons('Cooling');
    expect(cooling.map((reason) => reason.key).sort()).toEqual([
      'harness.cooling.why.assumed', 'harness.cooling.why.default', 'harness.cooling.why.notBelieved', 'harness.cooling.why.stated',
    ]);
  });
});
