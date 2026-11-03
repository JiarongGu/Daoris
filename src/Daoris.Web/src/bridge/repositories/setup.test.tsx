import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// AGENTSPLACE1j (the Agents-place design §4): a place's plan is read with its ask, and its apply sends the plan's ask, its digest and the
// acceptances chosen. Seen failing first.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  isShenoraAvailable: () => true,
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import type { PlaceAsk } from '../../agents/setup/placePlan';
import { usePlaceApply, usePlacePlan } from './setup';

const ASK: PlaceAsk = { repositories: ['engine'], preset: 'pull-request', opinion: false, look: true, adoption: true, builds: 'service', drive: true };

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{children}</QueryClientProvider>
);

describe("a place's plan and apply", () => {
  afterEach(() => invoke.mockReset());

  it('reads the plan with the ask and answers what the driver planned', async () => {
    invoke.mockResolvedValueOnce({ asked: ASK, repositories: [], changes: [], refusals: [], terminal: 'daoris place engine', applies: true, digest: 'a'.repeat(64) });
    const { result } = renderHook(() => usePlacePlan(ASK), { wrapper });
    await waitFor(() => expect(result.current.plan?.digest).toBe('a'.repeat(64)));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLACE_PLAN', expect.objectContaining({ payload: ASK }));
  });

  it('asks nothing while it is not wanted', () => {
    renderHook(() => usePlacePlan(ASK, { enabled: false }), { wrapper });
    expect(invoke).not.toHaveBeenCalled();
  });

  it('applies by the plan\'s digest and the acceptances chosen, and answers a refusal as an answer', async () => {
    const digest = 'b'.repeat(64);
    invoke.mockResolvedValueOnce({ done: false, refused: 'moved', message: 'the plan you read no longer holds', digest, operations: [] });
    const { result } = renderHook(() => usePlaceApply(), { wrapper });
    const applied = await result.current.mutateAsync({ ...ASK, digest, accept: [{ repository: 'engine', purpose: 'integration' }] });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'PLACE_APPLY', expect.objectContaining({
      payload: { ...ASK, digest, accept: [{ repository: 'engine', purpose: 'integration' }] },
    }));
    expect([applied.done, applied.refused]).toEqual([false, 'moved']);
  });
});
