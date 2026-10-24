import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Notice } from '../bridge/notices';
import type { Confirmation } from '../person/Confirmations';
import { useAnsweredRead, useMessageAnnouncements, usePressAnnouncements } from './announce';

// A press announced in the window in front (NOTIFY2b, the notification design §3.3, §5, Fable's F1.1): a toast with *Open*, said
// once in the focused main window on the press's own event by its id (a service's on its told list's first sight), and staying until
// the view opens or the press ends; never in a window not focused, and never while the view is open.

const PIPE: Confirmation = { source: 'pipe', id: 'c1', route: 'STOP_SESSION', fields: [], expires: null };
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

  // CONFIRMWAIT1a-i (the confirm-wait design §8.2, §10's two-asks row): two presses within a minute are two toasts in the focused main
  // window, one per press, each by its id; only the balloon behind the window folds them (the modules' `BalloonFold`).
  it('says two presses asked within a minute as two toasts, one per press, each by its id', () => {
    const { result } = announce({ presses: [PIPE, { ...PIPE, id: 'c2', route: 'SET_DRIVABLE' }], viewOpen: false });

    act(() => { result.current.heard('c1'); });
    act(() => { result.current.heard('c2'); });
    act(() => { result.current.heard('c2'); });

    expect(result.current.toasts.map((toast) => toast.text)).toEqual(['asks: c1', 'asks: c2']);
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

// An agent's or a teammate's urgent message to everyone (NOTIFY2g, the notification design §5): the shell's `MESSAGE`, said by a toast
// with *Open* in the focused window alone, once per message, an event heard twice said once; a secondary window's with no door.
describe('announcing an urgent message', () => {
  let focused: ReturnType<typeof vi.spyOn>;
  beforeEach(() => { focused = vi.spyOn(document, 'hasFocus').mockReturnValue(true); });
  afterEach(() => { vi.restoreAllMocks(); });

  const MESSAGE = {
    ask: 'a1', workspace: 'default', teammate: false, id: 'm2', at: '2026-10-22T09:20:00.000Z', who: 'agent', repository: 'db',
    line: 'The schema moved: stop before you land.', level: 'urgent', toEveryone: true,
  };
  const announce = (door = true) => {
    const onOpen = vi.fn();
    const hook = renderHook(() => useMessageAnnouncements({
      words: (arrived) => `${arrived.newest.repository}: ${arrived.newest.line}`, openLabel: 'Open', onOpen: door ? onOpen : undefined,
    }));
    return { ...hook, onOpen };
  };

  it('says it once in the focused window, with a door to its ask, an event heard twice said once', () => {
    const { result, onOpen } = announce();

    act(() => { result.current.heard(MESSAGE); });
    act(() => { result.current.heard({ ...MESSAGE }); });

    expect(result.current.toasts).toHaveLength(1);
    const [toast] = result.current.toasts;
    expect(toast).toMatchObject({ text: 'db: The schema moved: stop before you land.', kind: 'ok' });
    // News, not a wait: it closes by its seven seconds.
    expect(toast!.stays).toBeFalsy();
    toast!.door!.onOpen();
    expect(onOpen).toHaveBeenCalledWith({
      ask: 'a1', workspace: 'default', teammate: false,
      newest: { id: 'm2', at: MESSAGE.at, who: 'agent', repository: 'db', line: MESSAGE.line, level: 'urgent', toEveryone: true },
    });
    act(() => { expect(result.current.close(toast!.id)).toBe(true); });
    expect(result.current.toasts).toHaveLength(0);
  });

  it('says nothing in a window not focused, and never later', () => {
    focused.mockReturnValue(false);
    const { result } = announce();
    act(() => { result.current.heard(MESSAGE); });
    expect(result.current.toasts).toHaveLength(0);

    focused.mockReturnValue(true);
    act(() => { result.current.heard(MESSAGE); });
    expect(result.current.toasts).toHaveLength(0);
    act(() => { result.current.heard({ ...MESSAGE, id: 'm3' }); });
    expect(result.current.toasts).toHaveLength(1);
  });

  it('says one ordinary, or to one repository, never, and one of another shape never', () => {
    const { result } = announce();
    act(() => { result.current.heard({ ...MESSAGE, id: 'm4', level: 'ordinary' }); });
    act(() => { result.current.heard({ ...MESSAGE, id: 'm5', toEveryone: false }); });
    act(() => { result.current.heard({ ask: 'a1' }); });
    expect(result.current.toasts).toHaveLength(0);
  });

  it("gives a secondary window's toast no door, since it holds no view", () => {
    const { result } = announce(false);
    act(() => { result.current.heard(MESSAGE); });
    expect(result.current.toasts).toHaveLength(1);
    expect(result.current.toasts[0]!.door).toBeUndefined();
  });
});

// A conversation's answer is read when its conversation is the one attended in a window visible and focused (NOTIFY2g, §2.4).
describe('reading an answer where its conversation is on screen', () => {
  let focused: ReturnType<typeof vi.spyOn>;
  beforeEach(() => { focused = vi.spyOn(document, 'hasFocus').mockReturnValue(true); });
  afterEach(() => { vi.restoreAllMocks(); });

  const ANSWER: Notice = { id: 'answered-c1', kind: 'answered', at: '2026-10-22T09:30:00.000Z', read: false, session: 'c1', repository: 'game' };
  const reading = (initial: { answered: Notice[]; attended: string | null }) => {
    const onRead = vi.fn();
    const hook = renderHook((props: { answered: Notice[]; attended: string | null }) => useAnsweredRead({ ...props, onRead }), { initialProps: initial });
    return { ...hook, onRead };
  };

  it('reads it once where its conversation is attended in the focused window, and none of another', () => {
    const { rerender, onRead } = reading({ answered: [ANSWER, { ...ANSWER, id: 'answered-c2', session: 'c2' }], attended: 'c1' });
    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onRead).toHaveBeenCalledWith(['answered-c1']);

    // Its read on its way: asked once, however often the window draws before the push.
    rerender({ answered: [ANSWER], attended: 'c1' });
    expect(onRead).toHaveBeenCalledTimes(1);
  });

  it('reads none in a window behind, and reads it once the window comes in front', () => {
    focused.mockReturnValue(false);
    const { onRead } = reading({ answered: [ANSWER], attended: 'c1' });
    expect(onRead).not.toHaveBeenCalled();

    focused.mockReturnValue(true);
    act(() => { window.dispatchEvent(new Event('focus')); });
    expect(onRead).toHaveBeenCalledWith(['answered-c1']);
  });

  it('reads none already read, and none where no conversation is attended', () => {
    const { rerender, onRead } = reading({ answered: [{ ...ANSWER, read: true }], attended: 'c1' });
    rerender({ answered: [ANSWER], attended: null });
    expect(onRead).not.toHaveBeenCalled();
  });
});
