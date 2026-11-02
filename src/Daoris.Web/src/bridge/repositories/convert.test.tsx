import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// WORKFLOWCLEAN1g-i (the cleanup design §4.4, §4.5): the conversion's plan is read with the row's two choices, the undo's plan only where it is
// asked for, and the two presses send the plan's digest.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  isShenoraAvailable: () => true,
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { DEFAULT_CHOICES } from '../../agents/setup/convertPlan';
import { useConvertApply, useConvertPlan, useConvertUndo, useConvertUndoPlan } from './convert';

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{children}</QueryClientProvider>
);

describe("the conversion's plan and presses", () => {
  afterEach(() => invoke.mockReset());

  it('reads the plan with the two choices', async () => {
    invoke.mockResolvedValueOnce({ digest: 'a'.repeat(64), empty: false });
    const { result } = renderHook(() => useConvertPlan(DEFAULT_CHOICES), { wrapper });
    await waitFor(() => expect(result.current.plan?.digest).toBe('a'.repeat(64)));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CONVERT_PLAN', expect.objectContaining({ payload: DEFAULT_CHOICES }));
  });

  it('reads the undo\'s plan only where it is wanted', async () => {
    const off = renderHook(() => useConvertUndoPlan({ enabled: false }), { wrapper });
    expect(invoke).not.toHaveBeenCalled();
    off.unmount();
    invoke.mockResolvedValueOnce({ undo: true, digest: 'u'.repeat(64), refusal: null, lines: [] });
    const { result } = renderHook(() => useConvertUndoPlan({ enabled: true }), { wrapper });
    await waitFor(() => expect(result.current.plan?.digest).toBe('u'.repeat(64)));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CONVERT_PLAN', expect.objectContaining({ payload: { undo: true } }));
  });

  it('applies by the plan\'s digest and the choices, and answers a refusal as an answer', async () => {
    const digest = 'b'.repeat(64);
    invoke.mockResolvedValueOnce({ done: false, refused: 'moved', message: 'the plan you read no longer holds', digest, operations: [] });
    const { result } = renderHook(() => useConvertApply(), { wrapper });
    const applied = await result.current.mutateAsync({ ...DEFAULT_CHOICES, digest });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'CONVERT_APPLY', expect.objectContaining({ payload: { ...DEFAULT_CHOICES, digest } }));
    expect([applied.done, applied.refused]).toEqual([false, 'moved']);
  });

  it('undoes by the undo plan\'s digest', async () => {
    invoke.mockResolvedValueOnce({ done: true, message: 'undone', digest: 'c'.repeat(64), operations: [] });
    const { result } = renderHook(() => useConvertUndo(), { wrapper });
    await result.current.mutateAsync({ digest: 'c'.repeat(64) });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'CONVERT_UNDO', expect.objectContaining({ payload: { digest: 'c'.repeat(64) } }));
  });
});
