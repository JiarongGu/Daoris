import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The registry's machine half: the folder the shell names (MOD3, beside the domain it calls).

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async () => null) }));

vi.mock('@shenora/react', () => ({ getBridge: () => ({ invoke }) }));

import { usePickFolder } from './registry';

/**
 * SHEN2: the folder dialog is the person's for as long as they keep it open. The bridge gave up after its default
 * 30 seconds while the dialog stayed up, and the folder picked after that answered nobody, which is the defect the kit
 * fixed for its own file dialogs in 0.19 by letting a call wait without a limit (`timeoutMs: Infinity`).
 */
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
