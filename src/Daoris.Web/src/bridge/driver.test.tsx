import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { getBridge } from '@shenora/react';

// The driver's own calls, as hooks.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { keys } from '../data/queries';
import { useDriverReady, useNudge, usePlaces, useRoleStructure, useRoleStructureChain, useSyncNow } from './driver';

describe('the driver domain', () => {
  afterEach(() => {
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  /**
   * A publish is looked at now, not at the next poll. A refused nudge is swallowed — the poll looks anyway, and it is not the person's news.
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
   * *Sync now* is the tick's own pass for one circle, run by the shell's driver loop — so it
   * lands on DAORIS.DRIVER naming the circle. A pass that answers is published by the route, so the state
   * hub asks where it stands again in every window; one refused as an error publishes nothing, and the hook
   * asks again itself, since a pass that failed part way may have recorded where it stood.
   */
  it('Sync now lands on DAORIS.DRIVER naming the circle, and the standing refetches after a failure', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ workspace: 'aurora', problem: null, notes: [] }));
    const { result } = renderHook(() => useSyncNow(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    const report = await result.current.mutateAsync('aurora');

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYNC_NOW', { payload: { workspace: 'aurora' } });
    expect(report.notes).toEqual([]);
    expect(invalidate).not.toHaveBeenCalled();

    invoke.mockImplementation(async () => { throw Object.assign(new Error('no remote'), { code: 'DRIVER_REFUSED' }); });
    await expect(result.current.mutateAsync('aurora')).rejects.toThrow('no remote');
    // Passing over the standings where the shell pushes them: the pass's end pushes what it moved (PUSH1a2).
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSync, predicate: expect.any(Function) }));
  });

  /**
   * Settings → Workspace, opened as the shell starts, asks for the lines at once, is refused *still coming
   * up*, and would keep that answer until *Bring up to date* happened to ask again. The driver says when its service is up,
   * and everything it refused as not ready is asked again then — under its own key or not — while an answer refused
   * for a reason of its own is left alone.
   */
  it('what the driver refused while it came up is asked again when it says it is up', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const notReady = () => Object.assign(new Error('still coming up'), { code: 'DRIVER_NOT_READY' });
    let up = false;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (!up) throw notReady();
      return type === 'PLACES'
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
        // A read the page makes itself, under the driver's key: the lines are pushed, and are not asked again by this.
        lines: useQuery({ queryKey: [...keys.driver, 'places-read'], queryFn: () => getBridge().invoke<{ lines: unknown[] }>('DAORIS.DRIVER', 'PLACES') }),
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

  /**
   * The roles' structure is asked by event naming its places and works, under the driver's key, and read field by field,
   * a part the shell left out read as none.
   */
  it('the roles structure is asked by event naming its places', async () => {
    const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({
      works: ['build'],
      places: [{ workspace: 'aurora', works: [{ work: 'build', role: { id: 'build' }, choices: [{ agent: 'claude-code', from: '1', state: { code: 'not-read' } }] }, { work: 'help', held: 'held' }] }],
    }));
    const { result } = renderHook(() => useRoleStructure([{ workspace: 'aurora' }], ['build', 'help']), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.data?.places).toHaveLength(1));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ROLE_STRUCTURE', {
      payload: { places: [{ workspace: 'aurora' }], works: ['build', 'help'], held: expect.any(String) },
    });
    const [build, help] = result.current.data!.places[0].works;
    expect(build.choices[0].state.code).toBe('not-read');
    expect(help.choices).toEqual([]);
    expect(result.current.data?.agents).toEqual([]);
    expect(client.getQueryCache().findAll({ queryKey: [...keys.driver, 'roles', 'structure'] })).toHaveLength(1);

    invoke.mockImplementation(async () => ({ works: ['build'], places: [{ workspace: 'aurora', repository: 'engine', works: [] }, { workspace: 'aurora', works: [] }, { works: [] }], agents: [] }));
    const chained = renderHook(() => useRoleStructureChain({ workspace: 'aurora', repository: 'engine' }, ['build']), { wrapper: wrapper(client) });
    await waitFor(() => expect(chained.result.current.data?.places).toHaveLength(3));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ROLE_STRUCTURE', {
      payload: { chain: { workspace: 'aurora', repository: 'engine' }, works: ['build'], held: expect.any(String) },
    });
  });

  /** The places by event, and a part the shell left out read as none. */
  it('the places are asked by event with their page and fold', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    invoke.mockImplementation(async () => ({
      machine: { driven: false, held: false, trees: false, checkout: false, adopted: false },
      workspaces: [{ place: { workspace: 'aurora', driven: false }, driven: 1, notDriven: 41 }],
      repositories: [{ workspace: 'aurora', repository: 'engine', driven: true, values: { line: { value: 'release', source: 'here' } } }],
      matching: 1,
    }));
    const { result } = renderHook(() => usePlaces({ limit: 20, fold: 'aurora' }), { wrapper });

    await waitFor(() => expect(result.current.data?.repositories).toHaveLength(1));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLACES', { payload: { limit: 20, fold: 'aurora', held: expect.any(String) } });
    expect(result.current.data?.machine.works).toEqual([]);
    expect(result.current.data?.workspaces[0]).toMatchObject({ driven: 1, notDriven: 41 });
    expect(result.current.data?.repositories[0].needs).toEqual([]);
    expect(result.current.data?.undriven).toBeNull();
  });
});
