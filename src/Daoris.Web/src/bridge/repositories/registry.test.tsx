import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The registry's machine half: the folder the shell names.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async () => null) }));

vi.mock('@shenora/react', () => ({ getBridge: () => ({ invoke }) }));

import { usePickFolder } from './registry';

/**
 * The folder dialog is the person's for as long as they keep it open. The bridge's default 30 seconds would give up
 * while the dialog stays up, and the folder picked after that would answer nobody, which the kit avoids for its own
 * file dialogs by letting a call wait without a limit (`timeoutMs: Infinity`).
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

  // The dialog is owned by the window whose page asked, which the first main window, put away, is not.
  it('names the window it is in, so the dialog is that window\'s', async () => {
    window.history.pushState({}, '', '/?window=main%3A2');
    try {
      const client = new QueryClient();
      const { result } = renderHook(() => usePickFolder(), {
        wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
      });

      await result.current.mutateAsync();

      expect(invoke).toHaveBeenCalledWith('DAORIS.REGISTRY', 'PICK_FOLDER', { payload: { window: 'main:2' }, timeoutMs: Infinity });
    } finally {
      window.history.pushState({}, '', '/');
    }
  });
});
