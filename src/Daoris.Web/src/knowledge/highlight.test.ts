import { describe, expect, it } from 'vitest';
import { mark, termsOf } from './highlight';

/**
 * Marking the matched terms in a search excerpt, after the deployed application showed twenty-six
 * excerpts for one word with the word marked in none of them (D62).
 */
describe('termsOf', () => {
  it('tokenises the way the service does — lower-cased, nothing of two characters or fewer', () => {
    expect(termsOf('The Encoding of a File')).toEqual(['the', 'encoding', 'file']);
  });

  it('keeps each term once', () => {
    expect(termsOf('trap trap TRAP')).toEqual(['trap']);
  });

  it('has nothing to mark for an empty or too-short query', () => {
    expect(termsOf('')).toEqual([]);
    expect(termsOf('a of')).toEqual([]);
  });

  /**
   * 🔴 The service cuts a run of ideographs into overlapping bigrams and matches THOSE — that is how
   * `会话记录` finds a body that says `会话的记录`. Marking the query as one four-character term
   * marked nothing in that body: one result, zero marks, on the deployed application. The marker
   * cuts the same way the index does, so what is marked is what matched.
   */
  it('cuts a run of ideographs into the bigrams the service matched on', () => {
    expect(termsOf('会话记录')).toEqual(['会话', '话记', '记录']);
    expect(termsOf('记录')).toEqual(['记录']);
    expect(termsOf('书')).toEqual(['书']);
    expect(termsOf('the 会话 log')).toEqual(['the', '会话', 'log']);
  });
});

describe('mark', () => {
  it('marks the term wherever it appears, whatever its case', () => {
    expect(mark('Encoding traps: set the encoding first.', 'encoding')).toEqual([
      { text: 'Encoding', hit: true },
      { text: ' traps: set the ', hit: false },
      { text: 'encoding', hit: true },
      { text: ' first.', hit: false },
    ]);
  });

  /** Substring, because that is what the service matched — the hit is inside the longer word. */
  it('marks a term inside a longer word, which is what actually matched', () => {
    expect(mark('re-encoding', 'encoding')).toEqual([
      { text: 're-', hit: false },
      { text: 'encoding', hit: true },
    ]);
  });

  it('marks every term of a multi-word query', () => {
    const runs = mark('a lock and a manifest', 'lock manifest');
    expect(runs.filter((r) => r.hit).map((r) => r.text)).toEqual(['lock', 'manifest']);
  });

  it('leaves an excerpt whole when nothing in the query could match', () => {
    expect(mark('some excerpt', '')).toEqual([{ text: 'some excerpt', hit: false }]);
    expect(mark('some excerpt', 'zzz')).toEqual([{ text: 'some excerpt', hit: false }]);
  });

  it('marks 中文 as readily as anything else', () => {
    expect(mark('会话的记录在此', '记录')).toEqual([
      { text: '会话的', hit: false },
      { text: '记录', hit: true },
      { text: '在此', hit: false },
    ]);
    // A longer query marks the pieces that matched, each where the body has it.
    expect(mark('会话的记录在此', '会话记录')).toEqual([
      { text: '会话', hit: true },
      { text: '的', hit: false },
      { text: '记录', hit: true },
      { text: '在此', hit: false },
    ]);
  });

  /**
   * A query is text, never a pattern. Punctuation is not part of a term — the service tokenises it
   * away, and so does this — so `c++` and `a.b` leave only fragments too short to be terms, and mark
   * nothing rather than becoming a regex that marks the wrong thing.
   */
  it('never lets punctuation in the query become a pattern', () => {
    expect(mark('written in c++ and a.b notation', 'c++ a.b')).toEqual([
      { text: 'written in c++ and a.b notation', hit: false },
    ]);
    // A hyphenated term IS a term, and matches as the literal text it is.
    expect(mark('the re-encoding step', 're-encoding')).toEqual([
      { text: 'the ', hit: false },
      { text: 're-encoding', hit: true },
      { text: ' step', hit: false },
    ]);
  });

  it('is empty for empty text', () => {
    expect(mark('', 'anything')).toEqual([]);
  });
});
