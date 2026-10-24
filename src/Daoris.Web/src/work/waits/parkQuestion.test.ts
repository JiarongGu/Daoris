import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import type { GoAhead } from '../../data/api';
import { afterPress, choiceWords, openGoAheads, readQuestion } from './parkQuestion';

// PARKVIEW1 (the park's ask design §2, §4): the park's question as SESSION_QUESTION answers it, read defensively by the table the
// driver reads the file by; the words a choice sends; the go-aheads it still waits on; and what the box keeps once a press took its words.

type Row = { name: string; file: Record<string, unknown>; widen?: Record<string, number>; read: Record<string, unknown> | null };

/** The driver's shared table (Fable's read, Q6): each row's `file` is what SESSION_QUESTION answers for it, the same fields. */
const ROWS = (JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'park-question.json'), 'utf8',
)) as { rows: Row[] }).rows;

/** A row's file with its widened fields set to that many `x`, as the driver's test sets them. */
function widened(row: Row): Record<string, unknown> {
  const file = structuredClone(row.file);
  for (const [path, length] of Object.entries(row.widen ?? {})) {
    const parts = path.split('.');
    const at = parts.length === 1 ? file : (file[parts[0]!] as Record<string, unknown>[])[Number(parts[1])]!;
    at[parts.at(-1)!] = 'x'.repeat(length);
  }
  return file;
}

describe('readQuestion', () => {
  it.each(ROWS.map((row) => [row.name, row] as const))('reads the shared table\'s row: %s', (_name, row) => {
    const read = readQuestion({ question: widened(row) });

    if (row.read === null) {
      expect(read).toBeNull();
      return;
    }
    const words = (value: unknown) => (typeof value === 'number' ? 'x'.repeat(value) : value);
    expect(read).toEqual({
      id: row.read.id, question: words(row.read.question), choices: row.read.choices, recommend: row.read.recommend,
      why: words(row.read.why), by: row.read.by, owner: row.read.owner ?? null, at: row.file.at,
    });
  });

  /** SUPERVISOR1b: the route carries the supervisor's defer on the question beside it; read field by field, a part that does not read left out. */
  it('reads the supervisor\'s defer the route carries beside the question, and none where it does not read', () => {
    const asked = { id: 'q1', question: 'Which?', choices: [], at: '2026-10-24T09:00:00Z' };
    const left = readQuestion({ question: { ...asked, left: { bound: 'sign-in', reasons: [{ says: 'Yours.', source: 'D191 point 3' }, { says: 'no source' }] } } });
    expect(left?.left).toEqual({ bound: 'sign-in', reasons: [{ says: 'Yours.', source: 'D191 point 3' }] });

    expect(readQuestion({ question: asked })?.left).toBeUndefined();
    expect(readQuestion({ question: { ...asked, left: { reasons: [] } } })?.left).toBeUndefined();
    expect(readQuestion({ question: { ...asked, left: 'sign-in' } })?.left).toBeUndefined();
  });

  it('reads none from an older shell, and nothing at all', () => {
    expect(readQuestion(null)).toBeNull();
    expect(readQuestion({})).toBeNull();
  });
});

describe('choiceWords', () => {
  it('sends the label, then the person\'s own words after a blank line', () => {
    expect(choiceWords('1,200', '')).toBe('1,200');
    expect(choiceWords('1,200', '  but only on the dashboard  ')).toBe('1,200\n\nbut only on the dashboard');
  });
});

describe('openGoAheads', () => {
  it('keeps the go-aheads still asked, in their order', () => {
    const goAhead = (number: number, state: GoAhead['state']) =>
      ({ number, kind: 'write', on: 'production', act: 'config', state, asked: [] }) as GoAhead;

    expect(openGoAheads([goAhead(1, 'approved'), goAhead(2, 'asked'), goAhead(3, 'asked')]).map((row) => row.number)).toEqual([2, 3]);
    expect(openGoAheads(undefined)).toEqual([]);
  });
});

describe('afterPress', () => {
  it('takes off the words the press took, and keeps what was typed since', () => {
    expect(afterPress('only the menu entry', 'only the menu entry')).toBe('');
    expect(afterPress('only the menu entry\n\nand say so', 'only the menu entry')).toBe('and say so');
  });

  it('leaves a box changed under the press as it is, and an empty press touches nothing', () => {
    expect(afterPress('something else now', 'only the menu entry')).toBe('something else now');
    expect(afterPress('words', '')).toBe('words');
  });
});
