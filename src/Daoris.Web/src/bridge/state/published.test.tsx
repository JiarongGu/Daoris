import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// What the driver publishes is not read on a clock: a window that hears the driver's
// `STATE_CHANGED` waits for it, and a window with no bus keeps its interval.

const { invoke, shell } = vi.hoisted(() => ({ invoke: vi.fn(), shell: { available: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => shell.available,
  getBridge: () => ({ isAvailable: shell.available, invoke }),
  useShenora: () => ({ isAvailable: shell.available }),
  useShenoraEvent: () => undefined,
}));

import { pollUnlessTold, usePollUnlessTold } from './published';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
const fresh = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });

describe('reading what the driver publishes', () => {
  afterEach(() => {
    invoke.mockReset();
    shell.available = true;
    vi.useRealTimers();
  });

  it('keeps the interval where no bus tells, and only the slow reconciliation where one does', () => {
    expect(pollUnlessTold(false, 1_500)).toBe(1_500);
    expect(pollUnlessTold(true, 1_500)).toBe(30_000);
  });

  it('is the desktop window that hears: a plain browser has no bus and polls as before', () => {
    shell.available = true;
    expect(renderHook(() => usePollUnlessTold(4_000), { wrapper: wrapper(fresh()) }).result.current).toBe(30_000);

    shell.available = false;
    expect(renderHook(() => usePollUnlessTold(4_000), { wrapper: wrapper(fresh()) }).result.current).toBe(4_000);
  });
});
