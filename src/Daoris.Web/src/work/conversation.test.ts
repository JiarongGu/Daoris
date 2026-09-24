import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

// A session's conversation over the bridge (D76, CONV1): the history read once, live batches merged
// by sequence, a gap closed by asking for what was missed, and earlier pages on request. The
// bridge is mocked as present; the host is the test.

const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { useSessionEvents } from '../shell';
import { mergeEvents, type SessionEvent } from './conversation';

const said = (seq: number, text = `m${seq}`): SessionEvent => ({ seq, at: '2026-09-25T00:00:00Z', kind: 'message', text });

const emit = (session: string, events: SessionEvent[]) =>
  act(() => { eventHandlers.get('DAORIS.SESSION_EVENTS')!({ session, events }); });

const texts = (events: SessionEvent[]) => events.map((e) => e.text);

afterEach(() => {
  invoke.mockReset();
  eventHandlers.clear();
});

describe('mergeEvents', () => {
  it('keeps one of each sequence, in order, whichever arrived first', () => {
    expect(mergeEvents([said(1), said(3)], [said(2), said(3), said(4)]).map((e) => e.seq)).toEqual([1, 2, 3, 4]);
  });
});

describe('useSessionEvents', () => {
  it('opens a session with its newest page, and says when earlier turns exist', async () => {
    invoke.mockResolvedValue({ session: 's1', events: [said(4), said(5)], earlier: true, latest: 5 });

    const { result } = renderHook(() => useSessionEvents('s1'));

    await waitFor(() => expect(texts(result.current.events)).toEqual(['m4', 'm5']));
    expect(result.current.earlier).toBe(true);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1' } });
  });

  it('merges a live batch after the history, never doubling what it already holds', async () => {
    invoke.mockResolvedValue({ session: 's1', events: [said(1), said(2)], earlier: false, latest: 2 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    emit('s1', [said(2), said(3)]);

    expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3']);
  });

  /** A batch that skips ahead is a batch that missed something: it asks, rather than joining two halves. */
  it('asks for what it missed when a batch skips ahead, and merges it in order', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { after?: number } }) =>
      request.payload.after === 2
        ? { session: 's1', events: [said(3), said(4), said(5)], earlier: false, latest: 5 }
        : { session: 's1', events: [said(1), said(2)], earlier: false, latest: 2 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    emit('s1', [said(5)]);

    await waitFor(() => expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3', 'm4', 'm5']));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HISTORY', { payload: { id: 's1', after: 2 } });
  });

  it('loads earlier turns before what it holds, until there are none', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { before?: number } }) =>
      request.payload.before === 3
        ? { session: 's1', events: [said(1), said(2)], earlier: false, latest: 4 }
        : { session: 's1', events: [said(3), said(4)], earlier: true, latest: 4 });
    const { result } = renderHook(() => useSessionEvents('s1'));
    await waitFor(() => expect(result.current.events).toHaveLength(2));

    await act(() => result.current.loadEarlier());

    expect(texts(result.current.events)).toEqual(['m1', 'm2', 'm3', 'm4']);
    expect(result.current.earlier).toBe(false);
  });

  it('holds one session at a time, and ignores another session\'s batch', async () => {
    invoke.mockImplementation(async (_module: string, _type: string, request: { payload: { id: string } }) => ({
      session: request.payload.id, events: [said(1, `${request.payload.id} said`)], earlier: false, latest: 1,
    }));
    const { result, rerender } = renderHook(({ id }) => useSessionEvents(id), { initialProps: { id: 's1' } });
    await waitFor(() => expect(texts(result.current.events)).toEqual(['s1 said']));

    rerender({ id: 's2' });
    await waitFor(() => expect(texts(result.current.events)).toEqual(['s2 said']));
    emit('s1', [said(2, 'late from s1')]);

    expect(texts(result.current.events)).toEqual(['s2 said']);
  });

  it('holds nothing with no session attended', () => {
    const { result } = renderHook(() => useSessionEvents(null));

    expect(result.current.events).toEqual([]);
    expect(invoke).not.toHaveBeenCalled();
  });
});
