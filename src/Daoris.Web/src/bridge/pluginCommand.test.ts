import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement, type ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const { call } = vi.hoisted(() => ({
  call: vi.fn(async (_type: string, _payload?: Record<string, unknown>, _options?: unknown): Promise<unknown> => ({})),
}));
vi.mock('./call', () => ({ call }));

import { roundOf, usePluginCommand } from './pluginCommand';

// One round of a plugin's command from the window: the press as `PLUGIN_COMMAND` takes it, and the round read as the driver answers
// it, defensively, so a stranger answer never draws a field that asks nothing or a run that never ends.

const wrapper = ({ children }: { children: ReactNode }) =>
  createElement(QueryClientProvider, { client: new QueryClient({ defaultOptions: { mutations: { retry: false } } }) }, children);

describe("a plugin command's round", () => {
  it('sends the press whole to PLUGIN_COMMAND, waiting longer than a round may take', async () => {
    call.mockResolvedValueOnce({ run: 'a'.repeat(32), said: ['Open the page and enter the code.'], ask: { name: 'go-on', prompt: 'Signed in?', masked: false }, exit: 0 });
    const { result } = renderHook(() => usePluginCommand(), { wrapper });
    const press = { id: 'acme-boards', command: 'login', args: {}, answers: {}, place: 'machine', language: 'zh' };
    const round = await result.current.mutateAsync(press);
    expect(call).toHaveBeenCalledWith('PLUGIN_COMMAND', press, expect.objectContaining({ timeoutMs: expect.any(Number) }));
    expect((call.mock.calls[0]![2] as { timeoutMs: number }).timeoutMs).toBeGreaterThan(60_000);
    expect(round.run).toBe('a'.repeat(32));
    expect(round.ask).toEqual({ name: 'go-on', prompt: 'Signed in?', masked: false });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it('reads an answer defensively: lines that are no text dropped, a masked ask kept masked, then and data as sent', () => {
    expect(roundOf({
      run: 'b'.repeat(32), said: ['one', 2, null, 'two'], ask: { name: 'token', prompt: 'Token', masked: true },
      open: 'https://sign-in.example/device', code: 'ABCD-EFGH', then: 'stay', data: { signedIn: true }, exit: 0,
    })).toEqual({
      run: 'b'.repeat(32), said: ['one', 'two'], ask: { name: 'token', prompt: 'Token', masked: true },
      open: 'https://sign-in.example/device', code: 'ABCD-EFGH', then: 'stay', data: { signedIn: true }, exit: 0,
    });
  });

  it('ends a round that asks nothing and says no exit as not done, and drops an ask with no name', () => {
    expect(roundOf({ said: 'not a list', ask: { prompt: 'Nameless' } })).toEqual({
      run: '', said: [], ask: null, open: null, code: null, then: null, data: null, exit: 1,
    });
    expect(roundOf(null).exit).toBe(1);
  });
});
