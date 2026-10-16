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
import { useSystemWiring, wiringAgainIn, type SystemWiring } from './system';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
const fresh = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });
const wiring = (measuring: boolean) => ({ schema: 1, reader: 'window', measuring, measured: null }) as unknown as SystemWiring;

describe('the system domain', () => {
  afterEach(() => {
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

  it('reads again while a walk is under way, and not once it has ended', () => {
    expect(wiringAgainIn(wiring(true))).toBe(2_000);
    expect(wiringAgainIn(wiring(false))).toBe(false);
    expect(wiringAgainIn(undefined)).toBe(false);
  });
});
