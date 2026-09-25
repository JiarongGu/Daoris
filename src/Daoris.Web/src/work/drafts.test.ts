import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { DRAFTS, MAX_DRAFTS, type Drafts, draftOf, readDrafts, useDraft, withDraft } from './drafts';

describe('a draft per session (CONV4b)', () => {
  afterEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  it('keeps each session its own words, and forgets a draft that was emptied', () => {
    const one = withDraft([], 's1', 'half a thought');
    const two = withDraft(one, 's2', 'another');

    expect(two).toEqual([['s1', 'half a thought'], ['s2', 'another']]);
    expect(withDraft(two, 's1', '')).toEqual([['s2', 'another']]);
    // Pure: what it was given is untouched.
    expect(one).toEqual([['s1', 'half a thought']]);
  });

  it('keeps the newest drafts when there are too many, and a touched one counts as new', () => {
    let all: Drafts = [];
    for (let i = 0; i < MAX_DRAFTS; i += 1) all = withDraft(all, `s${i}`, `draft ${i}`);
    all = withDraft(all, 's0', 'touched again');
    all = withDraft(all, 'newest', 'last');

    expect(all).toHaveLength(MAX_DRAFTS);
    expect(draftOf(all, 's0')).toBe('touched again');
    expect(draftOf(all, 's1')).toBe('');
    expect(draftOf(all, 'newest')).toBe('last');
  });

  /**
   * REV3: a map's keys are not in the order they were written — integer-like keys come first. With
   * the limit reached, an all-digit session id (about one in fifty) was always the "oldest", and the
   * keystroke being typed was dropped as it was made.
   */
  it('keeps the draft being typed however its session id is spelled, through a reload too', () => {
    let all: Drafts = [];
    for (let i = 0; i < MAX_DRAFTS; i += 1) all = withDraft(all, `a${i}`, `draft ${i}`);
    all = withDraft(all, '12345678', 'h');
    expect(draftOf(all, '12345678')).toBe('h');

    window.localStorage.setItem(DRAFTS, JSON.stringify(all));
    all = withDraft(readDrafts(), 'b0', 'another');
    expect(draftOf(all, '12345678')).toBe('h');
  });

  it('reads nothing rather than failing when storage is garbage or refused, and reads the old map', () => {
    window.localStorage.setItem(DRAFTS, 'not json');
    expect(readDrafts()).toEqual([]);

    window.localStorage.setItem(DRAFTS, JSON.stringify({ s1: 'kept', s2: 42 }));
    expect(readDrafts()).toEqual([['s1', 'kept']]);

    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('refused'); });
    expect(readDrafts()).toEqual([]);
  });

  it('holds a draft for each session as the person moves between them, and it survives a reload', () => {
    const { result, rerender, unmount } = renderHook(({ session }) => useDraft(session), {
      initialProps: { session: 's1' as string | null },
    });

    act(() => result.current[1]('for the first'));
    rerender({ session: 's2' });
    expect(result.current[0]).toBe('');
    act(() => result.current[1]((was) => `${was}for the second`));
    rerender({ session: 's1' });
    expect(result.current[0]).toBe('for the first');
    unmount();

    // A new window reads the same storage: the page was reloaded.
    const again = renderHook(() => useDraft('s2'));
    expect(again.result.current[0]).toBe('for the second');
  });

  it('keeps nothing for no session', () => {
    const { result } = renderHook(() => useDraft(null));

    act(() => result.current[1]('into nowhere'));
    expect(result.current[0]).toBe('');
    expect(readDrafts()).toEqual([]);
  });
});
