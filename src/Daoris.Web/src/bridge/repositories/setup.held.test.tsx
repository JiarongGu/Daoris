import { afterEach, describe, expect, it, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';

// WEBHELD1: PLACE_PLAN is told by event, so the shell may answer `{ pending: true }` before the plan; the page reads that as no plan yet.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));
vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { planOf } from '../../agents/setup/placeFixtures';
import { type PlaceAsk, suggestionsOf } from '../../agents/setup/placePlan';
import { usePlacePlan } from './setup';

const ask: PlaceAsk = { repositories: ['engine'], preset: 'in-place', opinion: false, look: false, adoption: false, drive: false };

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe('a place’s plan read by event (WEBHELD1)', () => {
  afterEach(() => invoke.mockReset());

  it('reads the shell’s pending answer as no plan, which the page’s reads take as none', async () => {
    invoke.mockResolvedValue({ pending: true });
    const { result } = renderHook(() => usePlacePlan(ask), { wrapper: wrapper() });
    await waitFor(() => expect(invoke).toHaveBeenCalled());
    await waitFor(() => expect(result.current.reading).toBe(false));
    expect(result.current.plan).toBeNull();
    expect(suggestionsOf(result.current.plan)).toBeNull();
    expect(invoke.mock.calls[0]![2]).toMatchObject({ payload: { held: expect.any(String) } });
  });

  it('reads an answered plan as the plan', async () => {
    const plan = planOf(ask);
    invoke.mockResolvedValue(plan);
    const { result } = renderHook(() => usePlacePlan(ask), { wrapper: wrapper() });
    await waitFor(() => expect(result.current.plan).toEqual(plan));
  });
});
