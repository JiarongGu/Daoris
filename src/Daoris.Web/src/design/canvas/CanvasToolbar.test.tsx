import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { type SizingActs, useStableActs } from './CanvasToolbar';

// The acts a pan or a zoom step renews hand the toolbar nothing new, so its menu is not drawn again on each step.

const acts = (zoomIn: () => void, toSelection: (() => void) | null = null, minimap: SizingActs['minimap'] = null): SizingActs => ({
  zoomIn, zoomOut: () => undefined, fit: () => undefined, toSelection, actual: () => undefined, zoomTo: () => undefined, minimap,
});

describe('the toolbar acts', () => {
  it('stay one object while only their closures change, and call the newest', () => {
    const first = vi.fn();
    const second = vi.fn();
    const view = renderHook(({ current }) => useStableActs(current), { initialProps: { current: acts(first) } });
    const held = view.result.current;
    view.rerender({ current: acts(second) });
    expect(view.result.current).toBe(held);
    held.zoomIn();
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledTimes(1);
  });

  it('change when the selection or the minimap appears, or the minimap switches', () => {
    const view = renderHook(({ current }) => useStableActs(current), { initialProps: { current: acts(() => undefined) } });
    const none = view.result.current;
    expect(none.toSelection).toBeNull();
    expect(none.minimap).toBeNull();
    view.rerender({ current: acts(() => undefined, () => undefined) });
    expect(view.result.current.toSelection).not.toBeNull();
    view.rerender({ current: acts(() => undefined, null, { on: true, toggle: () => undefined }) });
    expect(view.result.current.minimap?.on).toBe(true);
    view.rerender({ current: acts(() => undefined, null, { on: false, toggle: () => undefined }) });
    expect(view.result.current.minimap?.on).toBe(false);
  });
});
