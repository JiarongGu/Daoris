import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The work of an ask or a quest, as hooks (PAUSE1e, D132 §7.3): its plan, a pause, a resume and an abandon, each one route of
// DAORIS.DRIVER, which the page names only here (MOD3, MOD5).

const { invoke, available } = vi.hoisted(() => ({ invoke: vi.fn(), available: { value: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => available.value,
  getBridge: () => ({ isAvailable: available.value, invoke }),
  useShenora: () => ({ isAvailable: available.value }),
  useShenoraEvent: () => undefined,
}));

import { keys } from '../queries';
import type { WorkPlan } from '../work/pausing';
import { useAbandonWork, usePauseWork, useResumeWork, useWorkPlan, workKey } from './work';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);

const PLAN: WorkPlan = {
  scope: 'ask', id: 'a1', pausable: true, paused: null, quests: [], sessions: [], trees: [], landings: [],
  abandon: { abandonable: false, pieces: [], closes: 'a1', abandoned: null },
};

describe('the work domain', () => {
  afterEach(() => {
    invoke.mockReset();
    available.value = true;
  });

  it('asks the plan of an ask or a quest by its scope, and nothing where nothing is named', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => PLAN);

    const ask = renderHook(() => useWorkPlan({ scope: 'ask', id: 'a1' }), { wrapper: wrapper(client) });
    await waitFor(() => expect(ask.result.current.plan).toEqual(PLAN));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PLAN', { payload: { ask: 'a1' } });

    const quest = renderHook(() => useWorkPlan({ scope: 'quest', id: 'q1' }), { wrapper: wrapper(client) });
    await waitFor(() => expect(quest.result.current.plan).toEqual(PLAN));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PLAN', { payload: { quest: 'q1' } });

    invoke.mockClear();
    const none = renderHook(() => useWorkPlan(null), { wrapper: wrapper(client) });
    expect(none.result.current).toMatchObject({ plan: null, available: true });
    expect(invoke).not.toHaveBeenCalled();
  });

  /** An answer without a plan's shape is no plan: the pages offer no act on it rather than guess one. */
  it('reads an answer without a plan’s shape as no plan', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ drivable: [], holds: [] }));
    const { result } = renderHook(() => useWorkPlan({ scope: 'quest', id: 'q1' }), { wrapper: wrapper(client) });
    await waitFor(() => expect(invoke).toHaveBeenCalled());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.plan).toBeNull();
  });

  /** Under the quests' key, so a tick, which asks the quests again, and a move on a quest ask the plan again too. */
  it('keeps the plan under the quests’ key', () => {
    expect(workKey({ scope: 'ask', id: 'a1' }).slice(0, 1)).toEqual(keys.allQuests);
  });

  /** A browser has no driver (D47 §4): nothing is asked, and the pages say the terminal's commands instead. */
  it('asks nothing in a browser, and says there is no driver to ask', () => {
    available.value = false;
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useWorkPlan({ scope: 'ask', id: 'a1' }), { wrapper: wrapper(client) });
    expect(result.current).toMatchObject({ plan: null, available: false, loading: false });
    expect(invoke).not.toHaveBeenCalled();
  });

  it('pauses, resumes and abandons on DAORIS.DRIVER, the abandon with its reason and the pieces its list held', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async (_module: string, type: string) => ({ type }));
    const invalidated = vi.spyOn(client, 'invalidateQueries');

    const pause = renderHook(() => usePauseWork(), { wrapper: wrapper(client) });
    await act(() => pause.result.current.mutateAsync({ scope: 'ask', id: 'a1' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_PAUSE', expect.objectContaining({ payload: { ask: 'a1' } }));

    const resume = renderHook(() => useResumeWork(), { wrapper: wrapper(client) });
    await act(() => resume.result.current.mutateAsync({ scope: 'quest', id: 'q1' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RESUME', expect.objectContaining({ payload: { quest: 'q1' } }));

    const abandon = renderHook(() => useAbandonWork(), { wrapper: wrapper(client) });
    await act(() => abandon.result.current.mutateAsync({
      target: { scope: 'ask', id: 'a1' }, reason: 'It went the wrong way.', pieces: ['quest:q1', 'ask:a1'],
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_ABANDON', expect.objectContaining({
      payload: { ask: 'a1', reason: 'It went the wrong way.', pieces: ['quest:q1', 'ask:a1'] },
    }));

    // Each move changes what the quests, the asks, the sessions and the driver say: all asked again.
    for (const key of [keys.allQuests, keys.allAsks, keys.allSessions, keys.driver]) {
      expect(invalidated).toHaveBeenCalledWith({ queryKey: key });
    }
  });

  /** An abandon runs a sync pass and walks git per tree before it answers: the page waits as long as the host may work. */
  it('waits past the bridge’s default for an abandon and a pause', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({}));
    const abandon = renderHook(() => useAbandonWork(), { wrapper: wrapper(client) });
    await act(() => abandon.result.current.mutateAsync({ target: { scope: 'quest', id: 'q1' }, reason: 'r', pieces: [] }));
    const [, , options] = invoke.mock.calls.at(-1)!;
    expect((options as { timeoutMs?: number }).timeoutMs).toBeGreaterThan(30_000);
  });
});
