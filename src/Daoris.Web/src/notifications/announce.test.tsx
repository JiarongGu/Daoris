import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Confirmation } from '../person/Confirmations';
import { usePressAnnouncements } from './announce';

// A press announced in the window in front (NOTIFY2b, the notification design §3.3, §5, Fable's F1.1): a toast with *Open*, said
// once in the focused main window on the press's own event by its id (a service's on its told list's first sight), and staying until
// the view opens or the press ends; never in a window not focused, and never while the view is open.

const PIPE: Confirmation = { source: 'pipe', id: 'c1', route: 'STOP_SESSION', fields: [], expires: '2026-10-21T09:10:00Z' };
const SERVICE: Confirmation = { source: 'service', id: 'h1', route: 'POST /x', act: 'a verdict', fields: [], expires: '2026-10-21T09:04:00Z' };

describe('announcing a press', () => {
  let focused: ReturnType<typeof vi.spyOn>;
  beforeEach(() => { focused = vi.spyOn(document, 'hasFocus').mockReturnValue(true); });
  afterEach(() => { vi.restoreAllMocks(); });

  const announce = (initial: { presses: Confirmation[]; viewOpen: boolean }) => {
    const onOpen = vi.fn();
    const hook = renderHook((props: { presses: Confirmation[]; viewOpen: boolean }) => usePressAnnouncements({
      ...props, words: (press) => `asks: ${press.id}`, openLabel: 'Open', onOpen,
    }), { initialProps: initial });
    return { ...hook, onOpen };
  };

  it("says a terminal's press once, on its event, with a door that shows its card, and keeps it", () => {
    const { result, onOpen } = announce({ presses: [PIPE], viewOpen: false });

    act(() => { result.current.heard('c1'); });
    act(() => { result.current.heard('c1'); });

    expect(result.current.toasts).toHaveLength(1);
    const [toast] = result.current.toasts;
    expect(toast).toMatchObject({ text: 'asks: c1', stays: true, kind: 'error' });
    toast!.door!.onOpen();
    expect(onOpen).toHaveBeenCalledWith(PIPE);
  });

  it('says nothing in a window not focused, nor where the view is open, and goes once the view opens', () => {
    focused.mockReturnValue(false);
    const { result, rerender } = announce({ presses: [PIPE], viewOpen: false });
    act(() => { result.current.heard('c1'); });
    expect(result.current.toasts).toHaveLength(0);

    focused.mockReturnValue(true);
    rerender({ presses: [PIPE, { ...PIPE, id: 'c2' }], viewOpen: true });
    act(() => { result.current.heard('c2'); });
    expect(result.current.toasts).toHaveLength(0);

    rerender({ presses: [PIPE, { ...PIPE, id: 'c2' }, { ...PIPE, id: 'c3' }], viewOpen: false });
    act(() => { result.current.heard('c3'); });
    expect(result.current.toasts).toHaveLength(1);
    rerender({ presses: [PIPE, { ...PIPE, id: 'c2' }, { ...PIPE, id: 'c3' }], viewOpen: true });
    expect(result.current.toasts).toHaveLength(0);
  });

  it('takes the toast away when its press ends, or the person closes it', () => {
    const { result, rerender } = announce({ presses: [PIPE, { ...PIPE, id: 'c2' }], viewOpen: false });
    act(() => { result.current.heard('c1'); });
    act(() => { result.current.heard('c2'); });
    expect(result.current.toasts).toHaveLength(2);

    rerender({ presses: [{ ...PIPE, id: 'c2' }], viewOpen: false });
    expect(result.current.toasts.map((toast) => toast.text)).toEqual(['asks: c2']);
    act(() => { expect(result.current.close(result.current.toasts[0]!.id)).toBe(true); });
    expect(result.current.toasts).toHaveLength(0);
    expect(result.current.close(12345)).toBe(false);
  });

  // NOTIFY2j: the shell tells every window one list at once, so the window focused as it is told says it, and no other.
  it("says a service's press once across two windows told it, in the one focused as they are", () => {
    focused.mockReturnValue(false);
    const behind = announce({ presses: [], viewOpen: false });
    focused.mockReturnValue(true);
    const front = announce({ presses: [], viewOpen: false });

    focused.mockReturnValue(false);
    behind.rerender({ presses: [SERVICE], viewOpen: false });
    focused.mockReturnValue(true);
    front.rerender({ presses: [SERVICE], viewOpen: false });
    expect(front.result.current.toasts.map((toast) => toast.text)).toEqual(['asks: h1']);
    expect(behind.result.current.toasts).toHaveLength(0);

    // The focus moving to the other window later says it there no more: it heard the press when it came.
    behind.rerender({ presses: [{ ...SERVICE }], viewOpen: false });
    expect(behind.result.current.toasts).toHaveLength(0);
  });

  it("says no service's press already waiting at this window's first read, and says one told after it", () => {
    const onOpen = vi.fn();
    const { result, rerender } = renderHook(
      (props: { presses: Confirmation[]; serviceRead: boolean }) => usePressAnnouncements({
        ...props, viewOpen: false, words: (press) => `asks: ${press.id}`, openLabel: 'Open', onOpen,
      }),
      { initialProps: { presses: [] as Confirmation[], serviceRead: false } },
    );

    rerender({ presses: [SERVICE], serviceRead: true });
    expect(result.current.toasts).toHaveLength(0);
    rerender({ presses: [SERVICE, { ...SERVICE, id: 'h2' }], serviceRead: true });
    expect(result.current.toasts.map((toast) => toast.text)).toEqual(['asks: h2']);
  });

  it("says a service's press the first time the list told it holds it in a focused window, and never again", () => {
    const { result, rerender } = announce({ presses: [], viewOpen: false });
    rerender({ presses: [SERVICE], viewOpen: false });
    expect(result.current.toasts.map((toast) => toast.text)).toEqual(['asks: h1']);

    act(() => { result.current.close(result.current.toasts[0]!.id); });
    rerender({ presses: [{ ...SERVICE }], viewOpen: false });
    expect(result.current.toasts).toHaveLength(0);

    // First seen while the window is not focused: counted in the bell, and said by no toast later.
    focused.mockReturnValue(false);
    rerender({ presses: [SERVICE, { ...SERVICE, id: 'h2' }], viewOpen: false });
    focused.mockReturnValue(true);
    rerender({ presses: [SERVICE, { ...SERVICE, id: 'h2' }], viewOpen: false });
    expect(result.current.toasts).toHaveLength(0);
  });
});
