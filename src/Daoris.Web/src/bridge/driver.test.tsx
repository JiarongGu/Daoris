import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';

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
import { useDriverReady, useNudge, useSyncNow } from './driver';
import { useLines } from './lines';

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

  /**
   * LOOK2a: Settings → Workspace, opened as the shell started, asked for the lines at once, was refused *still coming
   * up*, and kept that answer until *Bring up to date* happened to ask again. The driver says when its service is up,
   * and everything it refused as not ready is asked again then — under its own key or not — while an answer refused
   * for a reason of its own is left as it was.
   */
  it('what the driver refused while it came up is asked again when it says it is up', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const notReady = () => Object.assign(new Error('still coming up'), { code: 'DRIVER_NOT_READY' });
    let up = false;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (!up) throw notReady();
      return type === 'LINES'
        ? { lines: [{ repository: 'engine', workspace: 'aurora', branch: 'main', source: 'checkout' }], landings: [] }
        : null;
    });
    const review = vi.fn(async () => {
      if (!up) throw notReady();
      return 'the review';
    });
    const gone = vi.fn(async () => { throw Object.assign(new Error('gone'), { code: 'SESSION_TREE_GONE' }); });
    const { result } = renderHook(() => {
      useDriverReady();
      return {
        lines: useLines(),
        review: useQuery({ queryKey: ['sessions', 's-1', 'diff'], queryFn: review }),
        gone: useQuery({ queryKey: ['sessions', 's-2', 'diff'], queryFn: gone }),
      };
    }, { wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider> });

    await waitFor(() => expect(result.current.lines.isError).toBe(true));
    await waitFor(() => expect(result.current.review.isError && result.current.gone.isError).toBe(true));

    up = true;
    eventHandlers.get('DAORIS.DRIVER_READY')!({ ready: true });

    await waitFor(() => expect(result.current.lines.data?.lines).toHaveLength(1));
    await waitFor(() => expect(result.current.review.data).toBe('the review'));
    expect(gone).toHaveBeenCalledTimes(1);
  });
});
