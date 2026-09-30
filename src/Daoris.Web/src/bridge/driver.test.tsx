import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The driver's own calls, as hooks (MOD3: moved from `shell.test.tsx` beside the domain they call).

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { keys } from '../queries';
import { useNudge, useSyncNow } from './driver';

describe('the driver domain', () => {
  afterEach(() => {
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  /**
   * A publish is looked at now, not at the next poll: the route existed and nothing called it (REV3
   * CLEAN1). A refused nudge is swallowed — the poll looks anyway, and it is not the person's news.
   */
  it('a nudge lands on DAORIS.DRIVER, and a refused one says nothing', async () => {
    invoke.mockImplementation(async () => null);
    const { result } = renderHook(() => useNudge());

    result.current();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'NUDGE', {});

    invoke.mockImplementation(async () => { throw new Error('no driver'); });
    expect(() => result.current()).not.toThrow();
  });

  /**
   * *Sync now* (SYNC6b) is the tick's own pass for one circle, run by the shell's driver loop — so it
   * lands on DAORIS.DRIVER naming the circle, and afterwards where it stands is asked again.
   */
  it('Sync now lands on DAORIS.DRIVER naming the circle, and the standing refetches', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ workspace: 'aurora', problem: null, notes: [] }));
    const { result } = renderHook(() => useSyncNow(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    const report = await result.current.mutateAsync('aurora');

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYNC_NOW', { payload: { workspace: 'aurora' } });
    expect(report.notes).toEqual([]);
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSync }));
  });
});
