import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// FABRIC-CUTOVER1f (D190 point 6, the cutover design §5.2): the person's skip and stop of a graph run's node, the page's half of
// `daoris-driver workflow skip|stop`. Seen failing first.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
}));

import { useRunNodeWord } from './workflow';

describe("the person's word on a run's node", () => {
  afterEach(() => invoke.mockReset());

  it('sends the run, its node, the act and the words, and answers what the driver kept or why it refused, its code kept', async () => {
    invoke.mockResolvedValueOnce({ run: 'q1', node: 'go', act: 'skip', done: false, refused: 'worded', message: 'workflow: already has your word.' });
    const client = new QueryClient();
    const { result } = renderHook(() => useRunNodeWord(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    const answer = await result.current.mutateAsync({ run: 'q1', node: 'go', act: 'skip', words: 'nobody raises it' });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q1', node: 'go', act: 'skip', words: 'nobody raises it' } });
    expect(answer).toEqual({ run: 'q1', node: 'go', act: 'skip', done: false, refused: 'worded', message: 'workflow: already has your word.' });
  });

  it('leaves out words the person did not give', async () => {
    invoke.mockResolvedValueOnce({ run: 'q1', node: 'go', act: 'stop', done: true });
    const client = new QueryClient();
    const { result } = renderHook(() => useRunNodeWord(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    expect((await result.current.mutateAsync({ run: 'q1', node: 'go', act: 'stop', words: null })).done).toBe(true);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE', { payload: { run: 'q1', node: 'go', act: 'stop' } });
  });
});
