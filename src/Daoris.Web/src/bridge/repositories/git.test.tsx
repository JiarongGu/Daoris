import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The Git reads as hooks (GIT1d-b, D147): each is the driver's own read, shell-only, and a commit-keyed one is cached by its commit.

const { invoke, shell } = vi.hoisted(() => ({ invoke: vi.fn(), shell: { available: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => shell.available,
  getBridge: () => ({ isAvailable: shell.available, invoke }),
  useShenora: () => ({ isAvailable: shell.available }),
  useShenoraEvent: () => undefined,
}));

import { keys } from '../../data/queries';
import {
  gitRefusal, useCheckoutBranch, useGitBlame, useGitBranches, useGitCommit, useGitCompare, useGitGraph, useGitHistory,
  type GitAnswer, type GitCommitDetail,
} from './git';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);
const fresh = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });
const whole = 'a'.repeat(40);
const other = 'b'.repeat(40);

const answered = <T,>(value: T): GitAnswer<T> => ({ value, refusal: null, problem: null, commands: ['git log'], remembered: false });

describe('the git domain', () => {
  afterEach(() => {
    invoke.mockReset();
    shell.available = true;
  });

  it('asks the branch list with its scope, and nothing the scope leaves out', async () => {
    invoke.mockImplementation(async () => ({ repositories: [], apart: [], sessionsUnread: false }));

    const { result } = renderHook(() => useGitBranches({ repository: 'daoris' }), { wrapper: wrapper(fresh()) });

    await waitFor(() => expect(result.current.query.data).toEqual({ repositories: [], apart: [], sessionsUnread: false }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_BRANCHES', { payload: { repository: 'daoris' } });
  });

  it('asks each read of its route with its payload', async () => {
    invoke.mockImplementation(async () => answered({}));
    const client = fresh();

    const graph = renderHook(() => useGitGraph('daoris', [whole, other], { page: 1, sinceParted: true }), { wrapper: wrapper(client) });
    const commit = renderHook(() => useGitCommit('daoris', whole), { wrapper: wrapper(client) });
    const history = renderHook(() => useGitHistory('daoris', whole, 'a/b.ts', 2), { wrapper: wrapper(client) });
    const blame = renderHook(() => useGitBlame('daoris', whole, 'a/b.ts'), { wrapper: wrapper(client) });
    const compare = renderHook(() => useGitCompare('daoris', whole, other, true), { wrapper: wrapper(client) });

    await waitFor(() => {
      for (const each of [graph, commit, history, blame, compare]) expect(each.result.current.query.data).toBeDefined();
    });
    const sent = (route: string) => invoke.mock.calls.find((c) => c[1] === route)?.[2];
    expect(sent('GIT_GRAPH')).toEqual({ payload: { repository: 'daoris', tips: [whole, other], page: 1, sinceParted: true } });
    expect(sent('GIT_COMMIT')).toEqual({ payload: { repository: 'daoris', commit: whole } });
    expect(sent('GIT_HISTORY')).toEqual({ payload: { repository: 'daoris', commit: whole, path: 'a/b.ts', page: 2 } });
    expect(sent('GIT_BLAME')).toEqual({ payload: { repository: 'daoris', commit: whole, path: 'a/b.ts' } });
    expect(sent('GIT_COMPARE')).toEqual({ payload: { repository: 'daoris', left: whole, right: other, everything: true } });
  });

  it('sends the workspace with each one-repository read when given (GIT1d-c)', async () => {
    invoke.mockImplementation(async () => answered({}));
    const client = fresh();

    const graph = renderHook(() => useGitGraph('daoris', [whole], { workspace: 'aurora' }), { wrapper: wrapper(client) });
    const commit = renderHook(() => useGitCommit('daoris', whole, 'aurora'), { wrapper: wrapper(client) });
    const history = renderHook(() => useGitHistory('daoris', whole, 'a/b.ts', 0, 'aurora'), { wrapper: wrapper(client) });
    const blame = renderHook(() => useGitBlame('daoris', whole, 'a/b.ts', 'aurora'), { wrapper: wrapper(client) });
    const compare = renderHook(() => useGitCompare('daoris', whole, other, false, 'aurora'), { wrapper: wrapper(client) });

    await waitFor(() => {
      for (const each of [graph, commit, history, blame, compare]) expect(each.result.current.query.data).toBeDefined();
    });
    for (const route of ['GIT_GRAPH', 'GIT_COMMIT', 'GIT_HISTORY', 'GIT_BLAME', 'GIT_COMPARE']) {
      const call = invoke.mock.calls.find((c) => c[1] === route);
      expect(call?.[2].payload.workspace, route).toBe('aurora');
    }
  });

  it('keeps one repository name in two workspaces apart', async () => {
    invoke.mockImplementation(async () => answered({}));
    const client = fresh();

    const a = renderHook(() => useGitCommit('engine', whole, 'aurora'), { wrapper: wrapper(client) });
    const b = renderHook(() => useGitCommit('engine', whole, 'borealis'), { wrapper: wrapper(client) });
    await waitFor(() => {
      expect(a.result.current.query.data).toBeDefined();
      expect(b.result.current.query.data).toBeDefined();
    });

    expect(invoke).toHaveBeenCalledTimes(2);
    expect(client.getQueryData(keys.gitCommit('engine', whole, 'aurora'))).toBeDefined();
    expect(client.getQueryData(keys.gitCommit('engine', whole, 'borealis'))).toBeDefined();
  });

  it('asks the checkout its branch, fresh each time, and null where it is detached (NEW1e-a)', async () => {
    invoke.mockImplementation(async () => ({ branch: 'main' }));
    const client = fresh();

    const { result } = renderHook(() => useCheckoutBranch({ repository: 'daoris', workspace: 'aurora' }), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.query.data).toEqual({ branch: 'main' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHECKOUT_BRANCH', { payload: { repository: 'daoris', workspace: 'aurora' } });
    expect(client.getQueryData(keys.checkoutBranch('daoris', 'aurora'))).toEqual({ branch: 'main' });
    expect(client.getQueryState(keys.checkoutBranch('daoris', 'aurora'))?.isInvalidated).toBe(false);
    // staleTime 0: a second mount asks again, which is how the chip follows a branch the person switched elsewhere.
    renderHook(() => useCheckoutBranch({ repository: 'daoris', workspace: 'aurora' }), { wrapper: wrapper(client) });
    await waitFor(() => expect(invoke).toHaveBeenCalledTimes(2));
  });

  it('asks no checkout branch without a repository, or in a browser', () => {
    renderHook(() => useCheckoutBranch({ repository: null }), { wrapper: wrapper(fresh()) });
    shell.available = false;
    const { result } = renderHook(() => useCheckoutBranch({ repository: 'daoris' }), { wrapper: wrapper(fresh()) });

    expect(result.current.available).toBe(false);
    expect(invoke).not.toHaveBeenCalled();
  });

  it('asks no branch list while it is switched off', () => {
    renderHook(() => useGitBranches({ repository: 'daoris', enabled: false }), { wrapper: wrapper(fresh()) });

    expect(invoke).not.toHaveBeenCalled();
  });

  it('keeps a commit by its commit, so a second look asks nothing', async () => {
    const detail = { commit: whole, parents: [], subject: 's', message: 'm' } as unknown as GitCommitDetail;
    invoke.mockImplementation(async () => answered(detail));
    const client = fresh();

    const first = renderHook(() => useGitCommit('daoris', whole), { wrapper: wrapper(client) });
    await waitFor(() => expect(first.result.current.query.data?.value).toEqual(detail));
    const again = renderHook(() => useGitCommit('daoris', whole), { wrapper: wrapper(client) });

    expect(again.result.current.query.data?.value).toEqual(detail);
    expect(invoke).toHaveBeenCalledTimes(1);
    expect(client.getQueryData(keys.gitCommit('daoris', whole, ''))).toBeDefined();
  });

  it('asks nothing until what it reads is named', () => {
    renderHook(() => useGitCommit('daoris', null), { wrapper: wrapper(fresh()) });
    renderHook(() => useGitGraph('daoris', []), { wrapper: wrapper(fresh()) });
    renderHook(() => useGitBlame('daoris', whole, null), { wrapper: wrapper(fresh()) });

    expect(invoke).not.toHaveBeenCalled();
  });

  it('asks nothing in a browser', () => {
    shell.available = false;
    const { result } = renderHook(() => useGitBranches(), { wrapper: wrapper(fresh()) });

    expect(result.current.available).toBe(false);
    expect(invoke).not.toHaveBeenCalled();
  });

  it('hands a refusal on as its code, and an answer as none', async () => {
    const refused: GitAnswer<null> = { value: null, refusal: 'GIT_NOT_A_COMMIT', problem: 'no', commands: [], remembered: false };
    invoke.mockImplementation(async () => refused);

    const { result } = renderHook(() => useGitCommit('daoris', whole), { wrapper: wrapper(fresh()) });

    await waitFor(() => expect(result.current.query.data).toEqual(refused));
    expect(gitRefusal(refused)).toEqual({ code: 'GIT_NOT_A_COMMIT', problem: 'no' });
    expect(gitRefusal(answered({}))).toBeNull();
    expect(gitRefusal(undefined)).toBeNull();
  });
});
