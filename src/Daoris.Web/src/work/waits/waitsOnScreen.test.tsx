import { describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useState } from 'react';
import { useWaitsOnScreen } from './waitsOnScreen';

describe("whether the head's waits are on screen", () => {
  /**
   * Read after every drawing of the Sessions frame, an answer set again each time though nothing moved is an update
   * React cannot drop with the frame just drawn, so a streamed hand-back's every line would leave one behind its commit.
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
