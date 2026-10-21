import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// This Daoris's read as a hook (WIRING1b, the this-Daoris design §6): the driver's `SYSTEM_WIRING`, shell-only, keyed under
// `['system', 'wiring']`, its measure asked only when the page asks it, and read again while a walk is under way.

const { invoke, shell } = vi.hoisted(() => ({ invoke: vi.fn(), shell: { available: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => shell.available,
  getBridge: () => ({ isAvailable: shell.available, invoke }),
  useShenora: () => ({ isAvailable: shell.available }),
  useShenoraEvent: () => undefined,
}));

import { keys } from '../data/queries';
import { useSystemSpace, useSystemWiring, wiringAgainIn, type SystemWiring } from './system';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
const fresh = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });
const wiring = (measuring: boolean) => ({ schema: 1, reader: 'window', measuring, measured: null }) as unknown as SystemWiring;

describe('the system domain', () => {
  afterEach(() => {
    vi.useRealTimers();
    invoke.mockReset();
    shell.available = true;
  });

  it('reads the wiring under its key, asking no measure', async () => {
    invoke.mockImplementation(async () => wiring(false));
    const client = fresh();

    const { result } = renderHook(() => useSystemWiring(), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.query.data?.schema).toBe(1));
    expect(keys.systemWiring).toEqual(['system', 'wiring']);
    expect(client.getQueryData(keys.systemWiring)).toEqual(wiring(false));
    expect(invoke).toHaveBeenCalledTimes(1);
    expect(invoke.mock.calls[0][1]).toBe('SYSTEM_WIRING');
    expect(invoke.mock.calls[0][2]?.payload?.measure).toBeUndefined();
  });

  it('asks a browser nothing: the wiring is this machine’s', () => {
    shell.available = false;

    const { result } = renderHook(() => useSystemWiring(), { wrapper: wrapper(fresh()) });

    expect(result.current.available).toBe(false);
    expect(invoke).not.toHaveBeenCalled();
  });

  it('asks for the sizes only when the page asks, and keeps what it answered', async () => {
    invoke.mockImplementation(async (_module: string, _route: string, request?: { payload?: { measure?: boolean } }) =>
      wiring(Boolean(request?.payload?.measure)));
    const client = fresh();
    const { result } = renderHook(() => useSystemWiring(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.query.data).toBeDefined());

    await act(async () => {
      await result.current.measure();
    });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYSTEM_WIRING', { payload: { measure: true } });
    expect(client.getQueryData<SystemWiring>(keys.systemWiring)?.measuring).toBe(true);
  });

  /** A measure's answer that was read while the walk ran must not restore `measuring` over the read that found it ended. */
  it('keeps a newer read over a late measure answer', async () => {
    let answerMeasure: (answer: SystemWiring) => void = () => undefined;
    invoke.mockImplementation(async (_module: string, _route: string, request?: { payload?: { measure?: boolean } }) =>
      request?.payload?.measure ? new Promise<SystemWiring>((resolve) => { answerMeasure = resolve; }) : wiring(false));
    const client = fresh();
    const { result } = renderHook(() => useSystemWiring(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.query.data).toBeDefined());

    let measured: Promise<SystemWiring> = Promise.resolve(wiring(false));
    act(() => { measured = result.current.measure(); });
    await new Promise((resolve) => setTimeout(resolve, 5));
    // The walk ended and was told: the read that found it so lands before the measure's own answer.
    await act(async () => { await client.invalidateQueries({ queryKey: keys.systemWiring }); });
    await act(async () => { answerMeasure(wiring(true)); await measured; });

    expect(client.getQueryData<SystemWiring>(keys.systemWiring)?.measuring).toBe(false);
  });

  /** FABRIC-EVENT5b: a window that hears the driver waits for its `wiring` event; one with no bus reads on the clock as before. */
  it('reads again every two seconds while a walk is under way only where no event tells it', () => {
    expect(wiringAgainIn(wiring(true), false)).toBe(2_000);
    expect(wiringAgainIn(wiring(false), false)).toBe(false);
    expect(wiringAgainIn(undefined, false)).toBe(false);
    expect(wiringAgainIn(wiring(true), true)).toBe(30_000);
    expect(wiringAgainIn(wiring(false), true)).toBe(false);
  });

  it('does not read a walk again by the clock in a desktop window, and reads it when told', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    invoke.mockImplementation(async () => wiring(true));
    const client = fresh();
    const { result } = renderHook(() => useSystemWiring(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.query.data?.measuring).toBe(true));

    await act(async () => {
      await vi.advanceTimersByTimeAsync(7_000);
    });
    expect(invoke).toHaveBeenCalledTimes(1);

    invoke.mockImplementation(async () => wiring(false));
    await act(async () => {
      await client.invalidateQueries({ queryKey: keys.systemWiring });
    });
    await waitFor(() => expect(result.current.query.data?.measuring).toBe(false));
    vi.useRealTimers();
  });

  // KEEP1c (the cleanup design §4.4): the Disk space card's read, under `['system', 'space']`, counting only what the page asks.
  it('reads the disk space under its key asking no count, and counts the kinds asked or every kind', async () => {
    const space = { schema: 1, volume: null, volumeWhy: 'unreadable', kinds: [], run: null, held: null };
    invoke.mockImplementation(async () => space);
    const client = fresh();

    const { result } = renderHook(() => useSystemSpace(), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.query.data?.schema).toBe(1));
    expect(keys.systemSpace).toEqual(['system', 'space']);
    expect(invoke.mock.calls[0][1]).toBe('SYSTEM_SPACE');
    expect(invoke.mock.calls[0][2]?.payload).toBeUndefined();

    await act(async () => { await result.current.count(['logs']); });
    await act(async () => { await result.current.count(); });
    expect(invoke.mock.calls.slice(1).map((call) => call[2]?.payload)).toEqual([{ measure: ['logs'] }, { measure: true }]);
  });
});
