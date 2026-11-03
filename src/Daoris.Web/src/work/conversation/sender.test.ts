import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { isPartnerRecord, SENDERS, senderOf, wordSenderOf } from './sender';

/**
 * COMMS1a (the comms design §3.3, §13 point 11): who said a word, read once from an event's marks. The shared table is
 * `fixtures/senders.json` among the service's tests, which the service's and the driver's suites hold row for row; this page's twin reads
 * the `event` shape, and tells a partner from a helper by the work of the record the event's `helper` names.
 */
type EventMarks = { origin: string; from?: string; opinion?: string; helper?: string; helperWork?: string | null; channel?: string };
type WordMarks = { from?: string; by?: string; helper?: string; key?: string; channel?: string };
type Row = {
  name: string; word: WordMarks | null; event: EventMarks | null; sender: string;
  today: { persons: boolean | null; drawn: string }; disagrees: Record<string, string>;
};
const TABLE = JSON.parse(readFileSync(join(process.cwd(), '..', 'Daoris.Service', 'Daoris.Service.Tests', 'fixtures', 'senders.json'), 'utf8')) as {
  senders: string[]; decisions: Record<string, string>; rows: Row[];
};
const EVENT_ROWS = TABLE.rows.filter((row): row is Row & { event: EventMarks } => row.event !== null);
const WORD_ROWS = TABLE.rows.filter((row): row is Row & { word: WordMarks } => row.word !== null);

describe('who said a word (COMMS1a)', () => {
  it('has the closed set of senders the table names, the composed prompt apart', () => {
    expect([...SENDERS].sort()).toEqual([...TABLE.senders].sort());
  });

  it.each(EVENT_ROWS.map((row) => [row.name, row] as const))('reads %s as the table says', (_name, row) => {
    const { helperWork, ...marks } = row.event;
    expect(senderOf(marks, helperWork)).toBe(row.sender);
  });

  /** ONEWAY1m: the page reads a park's answer from a record's kept words, so it reads a kept word's marks by the service's rule too. */
  it.each(WORD_ROWS.map((row) => [row.name, row] as const))('reads a kept word of %s as the table says', (_name, row) => {
    expect(wordSenderOf(row.word)).toBe(row.sender);
  });

  it('lists every difference from what the page drew before, each with its decision', () => {
    for (const row of TABLE.rows) {
      expect(Object.keys(row.disagrees), row.name).toEqual(row.today.drawn === row.sender ? [] : ['drawn']);
      for (const decision of Object.values(row.disagrees)) expect(TABLE.decisions[decision]).toBeTruthy();
    }
  });

  it('tells a partner by the work of its record, or by a piece it serves, and a helper otherwise', () => {
    expect(isPartnerRecord({ work: 'partner' })).toBe(true);
    expect(isPartnerRecord({ serves: 'q', piece: 'p1' })).toBe(true);
    expect(isPartnerRecord({ serves: 'q' })).toBe(false);
    expect(isPartnerRecord({ work: 'help', serves: 'q' })).toBe(false);
    expect(isPartnerRecord(undefined)).toBe(false);
  });
});
