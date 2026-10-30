import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useState } from 'react';
import { useFollowTail } from './followTail';

/** A scroller jsdom lays nothing out for: its heights are set by hand, as a window would give them. */
function scroller(height: number, client: number) {
  const element = document.createElement('div');
  Object.defineProperty(element, 'scrollHeight', { configurable: true, value: height });
  Object.defineProperty(element, 'clientHeight', { configurable: true, value: client });
  element.scrollTop = 0;
  return { current: element };
}

describe('following a conversation\'s tail', () => {
  it('opens a live session at its tail, and follows it', () => {
    const ref = scroller(4000, 800);
    const { result } = renderHook(() => useFollowTail(ref, 1, 's1'));
    expect(ref.current.scrollTop).toBe(4000);
    expect(result.current.atTail).toBe(true);
  });

  /** SESS2 H1: an ended session opens at its head, which says how it ended, and is not followed. */
  it('opens an ended session at its top, not following, with the way to the bottom offered', () => {
    const ref = scroller(4000, 800);
    const { result, rerender } = renderHook(({ grew }) => useFollowTail(ref, grew, 's1', true), { initialProps: { grew: 1 } });
    expect(ref.current.scrollTop).toBe(0);
    expect(result.current.atTail).toBe(false);

    // Growth (an earlier page loading) does not pull a reader at the top down to the tail.
    rerender({ grew: 2 });
    expect(ref.current.scrollTop).toBe(0);
  });

  it('counts a short ended session as at its tail, since there is nowhere further to go', () => {
    const ref = scroller(600, 800);
    const { result } = renderHook(() => useFollowTail(ref, 1, 's1', true));
    expect(result.current.atTail).toBe(true);
  });

  /**
   * PAGELOOP1: a line streamed in while the reader is at the tail is followed with no second drawing. The tail re-set in the
   * layout effect was an update React could not drop, its holder having just drawn, so each line left one behind its commit, and
   * a hand-back of fifty lines took React past its update-depth limit (#185).
   */
  it('follows a growth its holder drew itself without drawing again', () => {
    const ref = scroller(4000, 800);
    let drawn = 0;
    let grow = (_grew: number) => {};
    renderHook(() => {
      drawn += 1;
      const [grew, setGrew] = useState(1);
      grow = setGrew;
      return useFollowTail(ref, grew, 's1');
    });
    const before = drawn;

    act(() => grow(2));

    expect(drawn - before).toBe(1);
    expect(ref.current.scrollTop).toBe(4000);
  });
});
