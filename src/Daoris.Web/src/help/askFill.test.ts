import { renderHook, act } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { fillAskDaoris, registerAskFill, useCanFillAsk } from './askFill';

// FLOWCHART2c (D223 §3): a page fills Ask Daoris's box with words for the person to finish, through the door the window registers.

describe('filling Ask Daoris', () => {
  it('hands the words to the registered door and says whether it did', () => {
    const door = vi.fn();
    expect(fillAskDaoris('Why?')).toBe(false);
    const release = registerAskFill(door);
    expect(fillAskDaoris('Why?')).toBe(true);
    expect(door).toHaveBeenCalledWith('Why?');
    release();
    expect(fillAskDaoris('Again?')).toBe(false);
  });

  it('tells a page whether there is a door, as it comes and goes, and a later door is not undone by an earlier one letting go', () => {
    const { result } = renderHook(() => useCanFillAsk());
    expect(result.current).toBe(false);
    let first = () => {};
    let second = () => {};
    act(() => { first = registerAskFill(() => {}); });
    expect(result.current).toBe(true);
    act(() => { second = registerAskFill(() => {}); first(); });
    expect(result.current).toBe(true);
    act(() => { second(); });
    expect(result.current).toBe(false);
  });
});
