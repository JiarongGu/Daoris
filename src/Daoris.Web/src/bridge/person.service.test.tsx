import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';

// ONEWAY1n-i (the oneway design §4 M4): the service's confirmations wait with no clock, so the host no longer answers `expires`. A
// confirmation without it is still listed, its time none, as a terminal's press is.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useServiceConfirmations } from './person';

const wrapper = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
};

/** A confirmation as the host answers it now: no `expires`, and the session that asked where one did. */
const WAITING = {
  id: 'h1', state: 'waiting', method: 'POST', path: '/api/asks', route: '/api/asks', act: 'an ask', values: [],
  fields: [{ name: '/sentence', type: 'string', value: 'Rename it.' }], asked: '2026-11-03T09:00:00Z', session: 's1',
};

describe("the service's confirmations with no clock", () => {
  afterEach(() => {
    invoke.mockReset();
  });

  it('lists one the host answers without a time, its time none', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SERVICE_CONFIRMATIONS' ? { seq: 1, waiting: [WAITING, { ...WAITING, id: 'h2', state: 'made', status: 200 }] } : null));
    const { result } = renderHook(() => useServiceConfirmations(true), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.waiting.map((each) => each.id)).toEqual(['h1']));
    expect(result.current.waiting[0]).toMatchObject({ source: 'service', route: 'POST /api/asks', act: 'an ask', expires: null });
  });
});
