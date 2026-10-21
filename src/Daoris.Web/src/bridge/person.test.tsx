import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';

// The person's door over the bridge (PERSONDOOR1f, RPC1c; D156 points 2 and 4): the key the shell holds, kept in memory; what a
// terminal asked the window to confirm, read as it mounts and kept current by the shell's telling; and the person's answer.

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

import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { holdPersonKey, holdsPersonKey } from '../data/api';
import {
  type PipeConfirmations, useAnswerConfirmation, useConfirmationShown, usePersonKey, useServiceConfirmations,
  useTerminalConfirmations,
} from './person';

const wrapper = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
};

const ARCHIVE = {
  id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_ARCHIVE',
  fields: [{ name: '/ids/0', type: 'string', value: 's1a2b3c4' }, { name: '/archived', type: 'boolean', value: 'true' }],
  asker: { process: 'daoris-driver', pid: 4242 }, asked: '2026-10-20T09:00:00Z', expires: '2026-10-20T09:02:00Z',
};
const DELETE = { ...ARCHIVE, id: 'c2', route: 'SESSION_DELETE', fields: [{ name: '/id', type: 'string', value: 's5e6f7a8' }] };

describe("the person's door over the bridge", () => {
  afterEach(async () => {
    invoke.mockReset();
    eventHandlers.clear();
    vi.unstubAllGlobals();
    await holdPersonKey(null);
  });

  it('reads what waits as it mounts, then keeps each newer telling and never an older one', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'PERSON_CONFIRMATIONS' ? { seq: 2, waiting: [ARCHIVE] } : null));
    const { result } = renderHook(() => useTerminalConfirmations(), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.map((each) => each.id)).toEqual(['c1']));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PERSON_CONFIRMATIONS', {});
    expect(result.current[0]).toMatchObject({ source: 'pipe', route: 'SESSION_ARCHIVE', asker: { process: 'daoris-driver', pid: 4242 } });

    // The query's observers hear a write on a later tick, so what a telling moved is waited for.
    const tell = (told: PipeConfirmations) => act(() => { eventHandlers.get('DAORIS.CONFIRMATIONS')!(told); });
    tell({ seq: 3, waiting: [ARCHIVE, DELETE] });
    await waitFor(() => expect(result.current.map((each) => each.id)).toEqual(['c1', 'c2']));
    // An older telling, delivered late, is not kept: the newer one after it is what shows.
    tell({ seq: 1, waiting: [] });
    tell({ seq: 4, waiting: [DELETE] });
    await waitFor(() => expect(result.current.map((each) => each.id)).toEqual(['c2']));
    tell({ seq: 5, waiting: [] });
    await waitFor(() => expect(result.current).toEqual([]));
  });

  it("answers a terminal's press by its confirmation's id, and says one that no longer waits by its code", async () => {
    invoke.mockResolvedValueOnce({ answered: true, confirmed: true })
      .mockResolvedValueOnce({ answered: false, why: 'gone', message: 'This no longer waits.' });
    const { result } = renderHook(() => useAnswerConfirmation(), { wrapper: wrapper() });

    const pipe = { source: 'pipe' as const, id: 'c1', route: 'SESSION_ARCHIVE', fields: [], expires: ARCHIVE.expires };
    expect(await result.current.mutateAsync({ waiting: pipe, confirm: true })).toEqual({ answered: true });
    expect(await result.current.mutateAsync({ waiting: pipe, confirm: false }))
      .toEqual({ answered: false, why: 'gone', message: 'This no longer waits.' });
    expect(invoke.mock.calls).toEqual([
      ['DAORIS.DRIVER', 'PERSON_CONFIRM', { payload: { id: 'c1', confirm: true } }],
      ['DAORIS.DRIVER', 'PERSON_CONFIRM', { payload: { id: 'c1', confirm: false } }],
    ]);
  });

  // CONFIRMSEEN1: the window's word that a card is in front of the person, by the confirmation's id; the service's counts its own.
  it("says a terminal's press is shown by its confirmation's id, and reads which are shown", async () => {
    invoke.mockResolvedValueOnce({ shown: true }).mockResolvedValueOnce({ shown: false, why: 'gone', message: 'This no longer waits.' });
    const { result } = renderHook(() => useConfirmationShown(), { wrapper: wrapper() });

    const pipe = { source: 'pipe' as const, id: 'c1', route: 'SESSION_ARCHIVE', fields: [], expires: ARCHIVE.expires };
    expect(await result.current.mutateAsync(pipe)).toBe(true);
    expect(await result.current.mutateAsync(pipe)).toBe(false);
    expect(await result.current.mutateAsync({ ...pipe, source: 'service' })).toBe(false);
    expect(invoke.mock.calls).toEqual([
      ['DAORIS.DRIVER', 'PERSON_CONFIRM_SHOWN', { payload: { id: 'c1' } }],
      ['DAORIS.DRIVER', 'PERSON_CONFIRM_SHOWN', { payload: { id: 'c1' } }],
    ]);

    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PERSON_CONFIRMATIONS'
      ? { seq: 1, waiting: [{ ...ARCHIVE, shownAt: '2026-10-20T09:01:00Z' }, DELETE] }
      : null));
    const read = renderHook(() => useTerminalConfirmations(), { wrapper: wrapper() });
    await waitFor(() => expect(read.result.current.map((each) => [each.id, each.shown])).toEqual([['c1', true], ['c2', false]]));
  });

  it("answers the service's confirmation at its own door, with the key, and says its refusal in the host's sentence", async () => {
    await holdPersonKey(async () => 'k'.repeat(43));
    const fetcher = vi.fn()
      .mockResolvedValueOnce(Response.json({ confirmation: { id: 'h1', state: 'confirmed' }, message: 'Confirmed.' }))
      .mockResolvedValueOnce(Response.json({ error: 'This confirmation no longer waits: it expired.' }, { status: 409 }));
    vi.stubGlobal('fetch', fetcher);
    const { result } = renderHook(() => useAnswerConfirmation(), { wrapper: wrapper() });

    const service = {
      source: 'service' as const, id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict", fields: [], expires: ARCHIVE.expires,
    };
    expect(await result.current.mutateAsync({ waiting: service, confirm: true })).toEqual({ answered: true });
    expect(await result.current.mutateAsync({ waiting: service, confirm: false }))
      .toEqual({ answered: false, why: null, message: 'This confirmation no longer waits: it expired.' });
    const calls = fetcher.mock.calls as [string, RequestInit][];
    expect(calls.map(([path, init]) => [path, init.method, (init.headers as Record<string, string>)['Daoris-Person']])).toEqual([
      ['/api/confirmations/h1/confirm', 'POST', 'k'.repeat(43)],
      ['/api/confirmations/h1/refuse', 'POST', 'k'.repeat(43)],
    ]);
  });

  // NOTIFY2j: what waits at the service's door, told by the shell to every window, so no window polls.
  const HELD = {
    id: 'h1', state: 'waiting', method: 'POST', path: '/api/asks', route: '/api/asks', act: 'an ask', values: [],
    fields: [{ name: '/sentence', type: 'string', value: 'Rename it.' }], asked: '2026-10-22T12:00:00Z', expires: '2026-10-22T12:02:00Z',
  };
  const OTHER = { ...HELD, id: 'h2', act: "a quest's deletion" };

  it("reads what waits at the service as the shell tells it, keeps each newer telling, and never polls the service", async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SERVICE_CONFIRMATIONS' ? { seq: 2, waiting: [HELD] } : null));
    const fetcher = vi.fn();
    vi.stubGlobal('fetch', fetcher);
    const { result } = renderHook(() => useServiceConfirmations(true), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.waiting.map((each) => each.id)).toEqual(['h1']));
    expect(result.current.read).toBe(true);
    expect(result.current.waiting[0]).toMatchObject({ source: 'service', route: 'POST /api/asks', act: 'an ask', fields: HELD.fields });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SERVICE_CONFIRMATIONS', {});

    const tell = (told: unknown) => act(() => { eventHandlers.get('DAORIS.SERVICE_CONFIRMATIONS')!(told); });
    tell({ seq: 3, waiting: [HELD, OTHER] });
    await waitFor(() => expect(result.current.waiting.map((each) => each.id)).toEqual(['h1', 'h2']));
    // An older telling, delivered late, is not kept.
    tell({ seq: 1, waiting: [] });
    tell({ seq: 4, waiting: [OTHER] });
    await waitFor(() => expect(result.current.waiting.map((each) => each.id)).toEqual(['h2']));
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("keeps the poll of the service's list where the shell is older than the telling, and lists none without the key", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SERVICE_CONFIRMATIONS') throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
      return null;
    });
    const fetcher = vi.fn(async () => Response.json([HELD]));
    vi.stubGlobal('fetch', fetcher);
    const { result } = renderHook(() => useServiceConfirmations(true), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.waiting.map((each) => each.id)).toEqual(['h1']));
    expect((fetcher.mock.calls as unknown as [string][]).map(([path]) => path)).toContain('/api/confirmations');

    invoke.mockClear();
    const none = renderHook(() => useServiceConfirmations(false), { wrapper: wrapper() });
    expect(none.result.current).toEqual({ waiting: [], read: false });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SERVICE_CONFIRMATIONS', {});
  });

  it('asks the shell for the key once it answers, and holds it in memory for the writes', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PERSON_KEY' ? { key: 'k'.repeat(43) } : null));

    const { result } = renderHook(() => usePersonKey(), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.held).toBe(true));
    expect(holdsPersonKey()).toBe(true);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PERSON_KEY', {});
  });

  it('holds none where the shell answers none, as it does before it mints one', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PERSON_KEY' ? { key: null } : null));

    const { result } = renderHook(() => usePersonKey(), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.settled).toBe(true));
    expect(result.current.held).toBe(false);
    expect(holdsPersonKey()).toBe(false);
  });
});
