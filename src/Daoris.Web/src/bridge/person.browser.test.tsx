import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';

// A browser holds no person key, and a service confirmation's Confirm and Refuse take one, so the service's list is not read for it: a card
// nobody in that window could answer is never drawn.

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => false,
  getBridge: () => ({ isAvailable: false, invoke: vi.fn() }),
  useShenora: () => ({ isAvailable: false }),
  useShenoraEvent: () => {},
}));

import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useServiceConfirmations } from './person';

describe("the service's confirmations in a browser", () => {
  it('reads none from the service, which a browser could not answer', async () => {
    const fetcher = vi.fn(async () => new Response('[]', { status: 200, headers: { 'content-type': 'application/json' } }));
    vi.stubGlobal('fetch', fetcher);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;

    const { result } = renderHook(() => useServiceConfirmations(true), { wrapper });
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(result.current).toEqual({ waiting: [], read: false });
    expect(fetcher).not.toHaveBeenCalled();
  });
});
