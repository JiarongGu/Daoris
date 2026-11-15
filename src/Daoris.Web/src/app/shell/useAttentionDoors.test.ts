import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { memoryOfIntent } from '../../menus/commands';
import type { Attention } from '../../work/attention/AttentionRow';
import { useAttentionDoors } from './useAttentionDoors';

// What needs you's doors that open a session at the part its row is about.

const doors = (attached: boolean) => {
  const open = vi.fn();
  const setWorkIntent = vi.fn();
  const { result } = renderHook(() => useAttentionDoors({
    attached, openInWork: vi.fn(), openAsk: vi.fn(), openQuest: vi.fn(), open, setWorkIntent, showNotifications: vi.fn(), openSettings: vi.fn(),
  }));
  return { doors: result.current.attentionDoors, open, setWorkIntent };
};

describe("What needs you's memory row", () => {
  it('opens its session at the Memory section, where its Keep or Drop and its approvals are', () => {
    const { doors: attention, open, setWorkIntent } = doors(true);

    attention.memory?.({ kind: 'memory', id: 's1', session: 's1' } as Attention);

    expect(open).toHaveBeenCalledWith('sessions', 's1');
    expect(setWorkIntent).toHaveBeenCalledWith('memory/s1');
    expect(memoryOfIntent('memory/s1')).toBe('s1');
    expect(memoryOfIntent('review')).toBeNull();
  });

  it("opens nothing for a sweep's end, which names no session, nor in a browser", () => {
    const { doors: attached, open, setWorkIntent } = doors(true);
    attached.memory?.({ kind: 'memory', id: 'op-1', session: null } as Attention);
    expect(open).not.toHaveBeenCalled();
    expect(setWorkIntent).not.toHaveBeenCalled();
    expect(doors(false).doors.memory).toBeUndefined();
  });
});
