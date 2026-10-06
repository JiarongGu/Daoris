import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useKnowledgeMode } from './KnowledgeView';
import type { ListPanes } from './work/listPanes';

// UX6i (D150 §2.2): Knowledge's mode, remembered for the place. Switching to the other mode is a door that names no item
// (UX6b, D150 §1 rule 6): that mode's remembered choice is read again before it is shown, so one whose item went while
// the other mode was in front opens nothing chosen, never its gone state.

const panes = () => ({ reopen: vi.fn() }) as unknown as ListPanes & { reopen: ReturnType<typeof vi.fn> };

describe("Knowledge's mode", () => {
  afterEach(() => window.localStorage.clear());

  it('opens as it was left, and remembers a change for the place', () => {
    window.localStorage.setItem('daoris.list.knowledge.mode', 'convergence');
    const { result } = renderHook(() => useKnowledgeMode(panes()));
    expect(result.current[0]).toBe('convergence');
    act(() => result.current[1]('search'));
    expect(result.current[0]).toBe('search');
    expect(window.localStorage.getItem('daoris.list.knowledge.mode')).toBeNull();
  });

  it("reads the other mode's remembered choice again on a switch, and nothing on the mode in front", () => {
    const lists = panes();
    const { result } = renderHook(() => useKnowledgeMode(lists));
    act(() => result.current[1]('search'));
    expect(lists.reopen).not.toHaveBeenCalled();
    act(() => result.current[1]('convergence'));
    expect(lists.reopen).toHaveBeenCalledWith('convergence');
    expect(lists.reopen).toHaveBeenCalledOnce();
  });
});
