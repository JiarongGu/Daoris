import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async () => null) }));

vi.mock('@shenora/react', () => ({ getBridge: () => ({ invoke }) }));

import { usePickFolder } from './registry';

// The dialog is the person's for as long as they keep it open: the call waits without a limit.
describe('picking a folder', () => {
  afterEach(() => invoke.mockClear());

  it('waits for the dialog however long it stays open', async () => {
    const client = new QueryClient();
    const { result } = renderHook(() => usePickFolder(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    await result.current.mutateAsync();

    expect(invoke).toHaveBeenCalledWith('DAORIS.REGISTRY', 'PICK_FOLDER', { timeoutMs: Infinity });
  });
});
