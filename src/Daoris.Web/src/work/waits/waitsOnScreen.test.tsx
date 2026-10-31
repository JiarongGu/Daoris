import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useState } from 'react';
import { useWaitsOnScreen } from './waitsOnScreen';

describe("whether the head's waits are on screen", () => {
  /**
   * PAGELOOP1: read after every drawing of the Sessions frame, the answer was set again each time though nothing moved, an update
   * React could not drop with the frame just drawn, so a streamed hand-back's every line left one behind its commit.
   */
  it('sets nothing where a drawing changed nothing', () => {
    const scroller = { current: document.createElement('div') };
    let drawn = 0;
    let draw = (_next: number) => {};
    const { result } = renderHook(() => {
      drawn += 1;
      const [, setCount] = useState(0);
      draw = setCount;
      return useWaitsOnScreen(scroller);
    });
    const before = drawn;

    act(() => draw(1));

    expect(drawn - before).toBe(1);
    expect(result.current).toBe(false);
  });
});
