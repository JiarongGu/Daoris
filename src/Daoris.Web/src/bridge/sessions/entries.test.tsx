import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// A line of work's one Accept and its ask's (ACCEPTALL1, D167's 2026-10-15 amendment), as hooks: each plan read only once its ask
// is open, and each press one route of DAORIS.DRIVER, which the page names only here (MOD3, MOD5).

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { useAskLanding, useEntryLanding, useLandAsk, useLandEntry } from './entries';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);

const client = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });

describe('the entries domain', () => {
  afterEach(() => {
    invoke.mockReset();
    heard.clear();
  });

  it("reads an entry's plan on ENTRY_LANDING once asked, and nothing before", async () => {
    invoke.mockImplementation(async () => ({ entry: 'ask:a3/engine', repository: 'engine', form: 'branch', target: 'feature/x', parts: [] }));

    const closed = renderHook(() => useEntryLanding('ask:a3/engine', false), { wrapper: wrapper(client()) });
    expect(closed.result.current.data).toBeUndefined();
    expect(invoke).not.toHaveBeenCalled();

    const open = renderHook(() => useEntryLanding('ask:a3/engine', true), { wrapper: wrapper(client()) });
    await waitFor(() => expect(open.result.current.data?.target).toBe('feature/x'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ENTRY_LANDING', { payload: { entry: 'ask:a3/engine' } });
  });

  it("reads an ask's plans on ASK_LANDING once asked", async () => {
    invoke.mockImplementation(async () => ({ ask: 'a3', entries: [] }));

    const open = renderHook(() => useAskLanding('a3', true), { wrapper: wrapper(client()) });

    await waitFor(() => expect(open.result.current.data?.entries).toEqual([]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_LANDING', { payload: { ask: 'a3' } });
  });

  it('presses LAND_ENTRY with the parts left out, and none where none were', async () => {
    invoke.mockImplementation(async () => ({ entry: 'ask:a3/engine', repository: 'engine', done: true, message: 'Landed 2 part(s).', parts: [] }));
    const { result } = renderHook(() => useLandEntry(), { wrapper: wrapper(client()) });

    await act(() => result.current.mutateAsync({ entry: 'ask:a3/engine', parts: 2, leaveOut: ['s1'] }));
    await act(() => result.current.mutateAsync({ entry: 'ask:a3/engine', parts: 2, leaveOut: [] }));

    expect(invoke).toHaveBeenNthCalledWith(1, 'DAORIS.DRIVER', 'LAND_ENTRY',
      { payload: { entry: 'ask:a3/engine', leaveOut: ['s1'] }, timeoutMs: expect.any(Number) });
    expect(invoke).toHaveBeenNthCalledWith(2, 'DAORIS.DRIVER', 'LAND_ENTRY',
      { payload: { entry: 'ask:a3/engine' }, timeoutMs: expect.any(Number) });
  });

  it('presses LAND_ASK, waiting as long as each part may take', async () => {
    invoke.mockImplementation(async () => ({ ask: 'a3', done: true, repositories: [] }));
    const { result } = renderHook(() => useLandAsk(), { wrapper: wrapper(client()) });

    await act(() => result.current.mutateAsync({ ask: 'a3', parts: 3, leaveOut: [] }));

    const [, , options] = invoke.mock.calls[0]! as [string, string, { payload: unknown; timeoutMs: number }];
    expect(invoke.mock.calls[0]![1]).toBe('LAND_ASK');
    expect(options.payload).toEqual({ ask: 'a3' });
    expect(options.timeoutMs).toBeGreaterThan(3 * 60_000);
  });
});
