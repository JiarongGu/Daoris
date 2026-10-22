import { describe, expect, it } from 'vitest';
import type { GoAhead } from '../../data/api';
import { afterPress, choiceWords, openGoAheads, readQuestion } from './parkQuestion';

// PARKVIEW1 (the park's ask design §2, §4): the park's question as SESSION_QUESTION answers it, read defensively; the words a
// choice sends; the go-aheads it still waits on; and what the box keeps once a press took its words.

describe('readQuestion', () => {
  it('reads the question whole, each answer and the one recommended', () => {
    expect(readQuestion({
      question: {
        id: 'a1b2c3d4', question: 'Which target?', at: '2026-10-22T09:05:00Z', recommend: 2, by: 'before Friday',
        choices: [{ label: '1,200', does: 'Write 1,200.' }, { label: '1,350', does: 'Write 1,350.' }],
      },
    })).toEqual({
      id: 'a1b2c3d4', question: 'Which target?', at: '2026-10-22T09:05:00Z', recommend: 2, by: 'before Friday',
      choices: [{ label: '1,200', does: 'Write 1,200.' }, { label: '1,350', does: 'Write 1,350.' }],
    });
  });

  it('reads none from an older shell, a question with no id or words, and nothing at all', () => {
    expect(readQuestion(null)).toBeNull();
    expect(readQuestion({})).toBeNull();
    expect(readQuestion({ question: { id: 'a1', question: '  ' } })).toBeNull();
    expect(readQuestion({ question: { question: 'Which?' } })).toBeNull();
  });

  it('passes over an answer with no label, and a recommendation that names no answer', () => {
    const read = readQuestion({ question: { id: 'a1', question: 'Which?', choices: [{ does: 'x' }, { label: 'Yes' }, null], recommend: 3 } });

    expect(read?.choices).toEqual([{ label: 'Yes', does: '' }]);
    expect(read?.recommend).toBeNull();
    expect(read?.by).toBeNull();
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
