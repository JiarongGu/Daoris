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

import { useAcceptIntegration, useRunNodeWord } from './workflow';

// INTEGRATE1d part 3 (the integration design §17.7): the person's acceptance of a repository's integration declaration.
describe("the person's acceptance of an integration declaration", () => {
  afterEach(() => invoke.mockReset());
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
  );

  it('sends the repository and the blob the person read, or a clear, and answers the driver as it did', async () => {
    invoke.mockResolvedValue({ start: null, done: false, refused: 'moved', message: 'engine: another declaration.' });
    const { result } = renderHook(() => useAcceptIntegration(), { wrapper });

    const answer = await result.current.mutateAsync({ repository: 'engine', gates: 'abc123' });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'INTEGRATION_GATES', { payload: { repository: 'engine', gates: 'abc123' } });
    expect(answer.refused).toBe('moved');

    await result.current.mutateAsync({ repository: 'engine', clear: true });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'INTEGRATION_GATES', { payload: { repository: 'engine', clear: true } });
  });
});

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
