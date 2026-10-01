import { describe, expect, it } from 'vitest';
import type { Entry } from '../api';
import {
  answeredBy, atFloor, convergenceFilters, findingId, findingTitle, lower, readingOf, SIMILARITY, searchFilters,
} from './records';

// What Search's and Convergence's lists keep and read (FRAME1f, D118 §3f), as values.

const entry = (id: string, title = id) => ({ id, repository: id.split(':')[0]!, kind: 'Rule', path: 'a.md', title });

describe("Search's and Convergence's memory", () => {
  /** *Local only* is the default, so only an explicit choice of everything is kept as a change. */
  it('reads local only back as the default unless everything was chosen', () => {
    expect(searchFilters({})).toEqual({ localOnly: true });
    expect(searchFilters({ localOnly: 'no' })).toEqual({ localOnly: true });
    expect(searchFilters({ localOnly: false })).toEqual({ localOnly: false });
  });

  /** A kept similarity is read within the slider's ends and in its steps; anything unreadable is the start. */
  it('reads the similarity back within the slider, in its steps', () => {
    expect(convergenceFilters({})).toEqual({ threshold: SIMILARITY.initial });
    expect(convergenceFilters({ threshold: 'high' })).toEqual({ threshold: 0.75 });
    expect(convergenceFilters({ threshold: Number.NaN })).toEqual({ threshold: 0.75 });
    expect(convergenceFilters({ threshold: 0.6500000001 })).toEqual({ threshold: 0.65 });
    expect(convergenceFilters({ threshold: 0.2 })).toEqual({ threshold: SIMILARITY.min });
    expect(convergenceFilters({ threshold: 1.4 })).toEqual({ threshold: SIMILARITY.max });
  });

  /** UX5 U42: a step down is a tenth, never below the floor, and the floor offers nothing lower. */
  it('lowers by a tenth to the floor, and knows the floor', () => {
    expect(lower(0.75)).toBe(0.65);
    expect(lower(0.55)).toBe(0.5);
    expect(atFloor(0.5)).toBe(true);
    expect(atFloor(0.51)).toBe(false);
  });
});

describe('a finding', () => {
  /** The service names no finding, so its entries are its name: found again, in any order, it is the same one. */
  it('is named by its entries, whatever their order', () => {
    const one = { entries: [entry('game:b'), entry('engine:a')] };
    const again = { entries: [entry('engine:a'), entry('game:b')] };
    expect(findingId(one)).toBe(findingId(again));
    expect(findingId(one)).not.toBe(findingId({ entries: [entry('engine:a'), entry('game:c')] }));
  });

  it("is titled by its entries' titles, each once", () => {
    expect(findingTitle({ entries: [entry('engine:a', 'reaching-in'), entry('game:a', 'reaching-in')] })).toBe('reaching-in');
    expect(findingTitle({ entries: [entry('engine:a', 'a'), entry('game:b', 'b')] })).toBe('a · b');
  });
});

describe('an answer', () => {
  /** TIER1 (D24): the tier that answered, never the one configured, and none answering is not nothing matching. */
  it('is worded by the tier that made it', () => {
    expect(answeredBy('lexical', true)).toEqual({ byMeaning: false, nothing: false });
    expect(answeredBy('lexical+semantic', false)).toEqual({ byMeaning: true, nothing: false });
    expect(answeredBy(null, true)).toEqual({ byMeaning: true, nothing: false });
    expect(answeredBy('none', true)).toEqual({ byMeaning: false, nothing: true });
  });

  /** An entry the service has nothing for now is gone, which a failed read is not. */
  it('reads an entry as read, on its way, gone, or failed in its own words', () => {
    const read = { id: 'game:a', title: 'a', body: 'text' } as Entry;
    expect(readingOf({ data: read, error: null })).toEqual({ state: 'read', entry: read });
    expect(readingOf({ error: null })).toEqual({ state: 'loading' });
    expect(readingOf({ error: Object.assign(new Error("no entry with id 'game:a'"), { status: 404 }) })).toEqual({ state: 'gone' });
    expect(readingOf({ error: Object.assign(new Error('the index is being rebuilt'), { status: 503 }) }))
      .toEqual({ state: 'unanswered', sentence: 'the index is being rebuilt' });
  });
});
