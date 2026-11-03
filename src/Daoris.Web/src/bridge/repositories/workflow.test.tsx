import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// FABRIC-CUTOVER1f (D190 point 6, the cutover design §5.2): the person's skip and stop of a graph run's node, the page's half of
// `daoris workflow skip|stop`. Seen failing first.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async (): Promise<unknown> => null) }));

vi.mock('@shenora/react', async (original) => ({
  ...(await original<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke }),
  useShenora: () => ({ isAvailable: true }),
}));

import { waitFor } from '@testing-library/react';
import { keys } from '../../data/queries';
import { acceptGatesPress, useAcceptIntegration, useRunNodeWord, useSetLanded, useVerify, useWorkflowCurrent } from './workflow';

// SETUPUX1o: the place's chart asks the effective graph of the route the person chose, and a read of another route is another answer.
describe("a repository's Current asked for a route", () => {
  afterEach(() => invoke.mockReset());
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
  );

  it('sends the route beside the repository, and sends none when none is asked, as before', async () => {
    invoke.mockResolvedValue({ repository: 'engine', steps: [] });
    const { result, rerender } = renderHook(({ route }: { route?: string }) => useWorkflowCurrent({ repository: 'engine' }, { route }), {
      wrapper, initialProps: { route: 'kind:docs' } as { route?: string },
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'WORKFLOW_CURRENT', { payload: { repository: 'engine', route: 'kind:docs' } });

    rerender({ route: undefined });
    await waitFor(() => expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'WORKFLOW_CURRENT', { payload: { repository: 'engine' } }));
  });

  it('keeps each route under a key of its own, under the prefix the writes ask again by', () => {
    expect(keys.workflow('repository', 'engine', 'landed')).not.toEqual(keys.workflow('repository', 'engine'));
    expect(keys.workflow('repository', 'engine', 'landed').slice(0, 3)).toEqual(keys.workflow('repository', '').slice(0, 3));
  });
});

// FABRIC-PIPELINE1d part 5: *Verify…*, `daoris gate verify`'s twin, binds a person's verification of a branch or a commit.
describe("the person's verification of a branch or a commit", () => {
  afterEach(() => invoke.mockReset());
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
  );

  it('sends the repository and the branch or the commit, and answers what the driver bound or why it refused', async () => {
    invoke.mockResolvedValueOnce({ repository: 'engine', done: true, refused: null, message: 'verify: `engine` at a1b2c3d (docs/fix): run v-1.' });
    const { result } = renderHook(() => useVerify(), { wrapper });

    const bound = await result.current.mutateAsync({ repository: 'engine', branch: 'docs/fix' });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'WORKFLOW_VERIFY', { payload: { repository: 'engine', branch: 'docs/fix' } });
    expect(bound.done).toBe(true);

    invoke.mockResolvedValueOnce({ repository: 'engine', done: false, refused: 'refused', message: 'verify: the gates are not accepted yet.' });
    const refused = await result.current.mutateAsync({ repository: 'engine', commit: 'a1b2c3d' });
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'WORKFLOW_VERIFY', { payload: { repository: 'engine', commit: 'a1b2c3d' } });
    expect([refused.refused, refused.message]).toEqual(['refused', 'verify: the gates are not accepted yet.']);
  });
});

// AGENTSPLACE1a0: *Accept the gates* sends the blob the page showed, so the driver can refuse a tip that moved.
describe("the person's Accept the gates", () => {
  afterEach(() => invoke.mockReset());
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
  );

  it('builds the press from the route and the tip it showed, and sends the blob', async () => {
    const tip = { line: 'main', commit: 'aa11bb2', gates: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' };
    expect(acceptGatesPress('engine', { workflow: 'web-verify', tip }))
      .toEqual({ repository: 'engine', workflow: 'web-verify', gates: tip.gates });
    // A tip not read sends no blob, as an older page did.
    expect(acceptGatesPress('engine', { workflow: 'web-verify', tip: null })).toEqual({ repository: 'engine', workflow: 'web-verify' });

    invoke.mockResolvedValueOnce({ start: null, lowered: [] });
    const { result } = renderHook(() => useSetLanded(), { wrapper });
    await result.current.mutateAsync(acceptGatesPress('engine', { workflow: 'web-verify', tip }));
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'WORKFLOW_LANDED', { payload: { repository: 'engine', workflow: 'web-verify', gates: tip.gates } });
  });
});

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
