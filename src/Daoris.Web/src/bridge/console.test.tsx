import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

// The live console's two sources (D49 §2): the backlog asked for once, and SESSION_OUTPUT batches as
// they arrive — one stream by the driver's sequence numbers. The bridge is mocked as present.
const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { useSessionConsole } from './shell';

type Line = { sequence: number; text: string };
const lines = (...sequences: number[]): Line[] => sequences.map((sequence) => ({ sequence, text: `line ${sequence}` }));
const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((settle) => { resolve = settle; });
  return { promise, resolve };
};
const batch = (session: string, sequences: number[]) =>
  act(() => eventHandlers.get('DAORIS.SESSION_OUTPUT')!({ session, lines: lines(...sequences) }));

describe('a session console (D49 §2)', () => {
  afterEach(() => {
    invoke.mockReset();
    eventHandlers.clear();
  });

  /**
   * 🔴 REV3: a live batch that arrived before the backlog's answer moved "the newest seen" past the
   * whole backlog, and the backlog was then filtered away — lines 1 to 249 never shown, and nothing said
   * so. The two sources are merged by sequence, whichever lands first.
   */
  it('keeps the backlog when a live batch lands before it', async () => {
    const backlog = deferred<unknown>();
    invoke.mockImplementation(() => backlog.promise);
    const { result } = renderHook(() => useSessionConsole('s1'));

    await batch('s1', [6, 7]);
    await act(async () => backlog.resolve({ session: 's1', lines: lines(1, 2, 3, 4, 5), sequence: 5, live: true, dropped: 0 }));

    expect(result.current.lines.map((line) => line.sequence)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  /** REV3: a gap-fill answered after the person moved on was appended to the NEXT session's console. */
  it('never shows one session\'s lines on another\'s console', async () => {
    const gap = deferred<unknown>();
    invoke.mockImplementation(async (_module: string, _type: string, body?: { payload?: { id?: string; after?: number } }) => {
      if (body?.payload?.after !== undefined) return gap.promise;
      return body?.payload?.id === 's1'
        ? { session: 's1', lines: lines(1, 2), sequence: 2, live: true, dropped: 0 }
        : { session: 's2', lines: [], sequence: 0, live: true, dropped: 0 };
    });
    const { result, rerender } = renderHook(({ id }) => useSessionConsole(id), { initialProps: { id: 's1' } });
    await waitFor(() => expect(result.current.lines).toHaveLength(2));

    await batch('s1', [5]); // skips 3 and 4, so the page asks for what came after 2
    rerender({ id: 's2' });
    await act(async () => gap.resolve({ session: 's1', lines: lines(3, 4, 5), sequence: 5, live: true, dropped: 0 }));

    expect(result.current.lines).toEqual([]);
  });
});
