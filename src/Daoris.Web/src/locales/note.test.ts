import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { BACKGROUND_STATES, HANDED_REASONS, HELPER_REASONS, NOTE_CODES, NOTE_VALUES, OPENING_REASONS, RESUMED_REASONS, UNREACHED_REASONS } from '../work/noteLines';
import { driverSource } from '../test/driverSource';
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
function declared(writer: string, text: string): Declared[] {
  return [...text.matchAll(DECLARATION)].map((match) => {
    const { code, values, key, why } = match.groups as { code: string; values: string; key?: string; why?: string };
    return { code, values: names(values), key: key ?? `note.${code}`, writer, ...(why ? { why: why.toLowerCase() } : {}) };
  });
}

/** The reasons the driver declares for one family (`NoteCodes.Continue`, `NoteCodes.Cooling`): each key and its values. */
function reasons(family: string): { key: string; values: string[] }[] {
  const text = driverSource('NoteCodes.cs');
  const block = new RegExp(`NoteReasons ${family} = new\\(\\s*\\[([\\s\\S]*?)\\]\\);`).exec(text)?.[1] ?? '';
  return [...block.matchAll(/new\(\w+\.\w+, \[([^\]]*)\], "([^"]+)"\)/g)].map((match) => ({ key: match[2], values: names(match[1]) }));
}

/** The states the driver's `BackgroundState` declares as known (CONVNOTE2i). */
function backgroundStates(): string[] {
  const block = /static class BackgroundState\s*\{([\s\S]*?)\n\}/.exec(driverSource('NoteCodes.cs'))?.[1] ?? '';
  return [...block.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]);
}

const driver = declared('driver', driverSource('NoteCodes.cs'));
// The service core's files sit in module folders (STRUCT5b), so NoteParts.cs is found by name under the core.
const coreFolder = 'src/Daoris.Service/Daoris.Service.Core';
const noteParts = readdirSync(join(root, ...coreFolder.split('/')), { recursive: true, encoding: 'utf8' })
  .map((entry) => entry.replaceAll('\\', '/'))
  .filter((entry) => entry === 'NoteParts.cs' || entry.endsWith('/NoteParts.cs'));
if (noteParts.length !== 1) throw new Error(`NoteParts.cs is expected once under ${coreFolder}, found ${noteParts.length}`);
const ledger = declared('service', source(`${coreFolder}/${noteParts[0]}`));
const codes = [...driver, ...ledger];

const placeholders = (text: string): string[] =>
  [...new Set([...text.matchAll(/\{\{\s*([\w.]+)[^}]*\}\}/g)].map((match) => match[1]))].sort();

describe('a session note’s codes, held to both catalogues', () => {
  it('reads both writers’ declarations', () => {
    // A scan that matched nothing would pass on a moved or renamed file: it has to see them.
    expect(driver.length).toBeGreaterThan(60);
    expect(ledger.map((code) => code.code)).toEqual([
      'ledger.answered', 'ledger.parked', 'ledger.went-on', 'ledger.went-on-findings', 'ledger.went-on-both',
      'ledger.went-on-helper', 'ledger.went-on-helper-both',
      // Daoris's own words alone (COWORK1b).
      'ledger.went-on-daoris',
      // A quest's note (QUESTCLOSE1b1): the person's done, its lead coded and their words beneath it.
      'quest.person-done',
    ]);
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
    // A reason a `why` names is declared by its writer too (UX7d-1: the opening's, `note.opening.why.*`).
    const keys = new Set([...codes.map((code) => code.key), ...['Continue', 'Cooling', 'Opening', 'Resumed', 'Unreached', 'Handed', 'Helper'].flatMap((family) => reasons(family).map((reason) => reason.key)), ...backgroundStates().map((state) => `note.background.state.${state}`)]);
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
   * by `harness.window.*`, by codes of their own. `account.cooling` keeps what it declared, its moment and why, so a note
   * written before is still worded; a limit whose reset named no window has its own line, since a value its entry says and
   * the part lacks shows the line as recorded.
   */
  it('reads a limit’s window and owner on codes of their own, and keeps the old cooling line as it was', () => {
    const old = driver.find((code) => code.code === 'account.cooling');
    const cooling = driver.find((code) => code.code === 'account.cooling-window');
    const none = driver.find((code) => code.code === 'account.cooling-no-window');
    expect(old).toEqual({ code: 'account.cooling', values: ['until', 'why'], key: 'note.account.cooling', writer: 'driver', why: 'cooling' });
    expect(cooling).toEqual({ code: 'account.cooling-window', values: ['until', 'why', 'owner', 'window'], key: 'note.account.cooling-window', writer: 'driver', why: 'cooling' });
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

  /**
   * UX7d-1 (D152's UX7d-1 note): a conversation's first line names its account's step by a reason of the opening's family,
   * each with its own values. The page's map holds the driver's reasons key for key and value for value, both catalogues say
   * exactly each reason's values, and every value a reason carries has a way the page says it.
   */
  it('words every step a conversation’s first line may name, with its own values', () => {
    const opening = reasons('Opening');
    expect(opening.length).toBeGreaterThan(15);
    const page = Object.values(OPENING_REASONS).map(({ key, values }) => ({ key, values: [...values] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual([...opening].sort(byKey));
    for (const catalogue of [en, zh]) {
      for (const { key, values } of opening) {
        expect(placeholders(catalogue[key] ?? ''), `\`${key}\``).toEqual([...values].sort());
      }
    }
    expect(opening.flatMap((reason) => reason.values).filter((value) => !(value in NOTE_VALUES))).toEqual([]);
  });

  /** CONVNOTE2a: whose words a resumed conversation goes on with is a reason of its own family, held key for key. */
  it('words every way a resumed conversation’s first line says whose words', () => {
    const resumed = reasons('Resumed');
    expect(resumed.length).toBe(7);
    const page = Object.values(RESUMED_REASONS).map(({ key, values }) => ({ key, values: [...values] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual([...resumed].sort(byKey));
    for (const catalogue of [en, zh]) {
      for (const { key, values } of resumed) {
        expect(placeholders(catalogue[key] ?? ''), `\`${key}\``).toEqual([...values].sort());
      }
    }
  });

  /** CONVNOTE2d1: why the person's words never reached the session is a reason of its own family, held key for key. */
  it('words every reason the person’s words never reached the session', () => {
    const unreached = reasons('Unreached');
    expect(unreached.length).toBe(2);
    const page = Object.values(UNREACHED_REASONS).map(({ key, values }) => ({ key, values: [...values] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual([...unreached].sort(byKey));
    for (const catalogue of [en, zh]) {
      for (const { key, values } of unreached) {
        expect(placeholders(catalogue[key] ?? ''), `\`${key}\``).toEqual([...values].sort());
      }
    }
  });

  /**
   * MIXAGENT1a-i: why a conversation was handed to another agent is a reason of its own family, held key for key; SWITCH1c-i adds
   * `curve`, Daoris's move by the usage curve.
   */
  it('words every reason a conversation was handed to another agent', () => {
    const handed = reasons('Handed');
    expect(handed.length).toBe(5);
    const page = Object.values(HANDED_REASONS).map(({ key, values }) => ({ key, values: [...values] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual([...handed].sort(byKey));
    for (const catalogue of [en, zh]) {
      for (const { key, values } of handed) {
        expect(placeholders(catalogue[key] ?? ''), `\`${key}\``).toEqual([...values].sort());
      }
    }
  });

  /** MIXAGENT2a-ii: why no helper answered a question is a reason family of its own, held key for key; HELPERNO1 added the no. */
  it('words every reason no helper answered a question', () => {
    const helper = reasons('Helper');
    expect(helper.length).toBe(10);
    const page = Object.values(HELPER_REASONS).map(({ key, values }) => ({ key, values: [...values] }));
    const byKey = (a: { key: string }, b: { key: string }) => a.key.localeCompare(b.key);
    expect(page.sort(byKey)).toEqual([...helper].sort(byKey));
    for (const catalogue of [en, zh]) {
      for (const { key, values } of helper) {
        expect(placeholders(catalogue[key] ?? ''), `\`${key}\``).toEqual([...values].sort());
      }
    }
  });

  it('words every state background work is known to end in, and no other', () => {
    // CONVNOTE2i: the driver's `BackgroundState` constants; any other state is shown as the harness sent it.
    const known = backgroundStates();
    expect(known.length).toBe(4);
    expect(Object.keys(BACKGROUND_STATES).sort()).toEqual([...known].sort());
    for (const catalogue of [en, zh]) {
      for (const state of known) {
        expect(catalogue[BACKGROUND_STATES[state]], state).toBeTruthy();
        expect(BACKGROUND_STATES[state]).toBe(`note.background.state.${state}`);
      }
    }
  });
});
