import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useMapWhen, useShownLines } from './LinesMenu';

// What the map's menu remembers (MAP4b, MAP4c), through the page's one storage guard since FRAME1c
// (audit A10): kept for this viewer, and for the visit alone where the browser refuses to keep it.

afterEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
});

describe("the map's remembered lines", () => {
  it('keeps the kinds hidden and which quests, and reads them back', () => {
    const lines = renderHook(() => useShownLines());
    act(() => lines.result.current[1]('asks'));
    const when = renderHook(() => useMapWhen());
    act(() => when.result.current[1]('week'));

    expect(renderHook(() => useShownLines()).result.current[0].has('asks')).toBe(false);
    expect(renderHook(() => useMapWhen()).result.current[0]).toBe('week');
  });

  it('draws every kind and every quest where storage is refused, and still switches for the visit', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new DOMException('refused', 'SecurityError'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new DOMException('full', 'QuotaExceededError'); });

    const lines = renderHook(() => useShownLines());
    expect(lines.result.current[0].has('asks')).toBe(true);
    act(() => lines.result.current[1]('asks'));
    expect(lines.result.current[0].has('asks')).toBe(false);

    const when = renderHook(() => useMapWhen());
    expect(when.result.current[0]).toBe('all');
    act(() => when.result.current[1]('open'));
    expect(when.result.current[0]).toBe('open');
  });
});
