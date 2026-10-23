import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';

// The window's splash, page side (SPLASHDATA1): the main window's page says, once, that its first screen has its data,
// and the window lifts its splash then rather than as the page's browser is made.

const { closeSplash } = vi.hoisted(() => ({ closeSplash: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  class WindowCommands {
    closeSplash = closeSplash;
  }
  return { ...actual, WindowCommands };
});

import { SPLASH_QUIET_MS, isFirstScreen, useSplashLift } from './splash';

/** An answer the test gives when it chooses. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}

const pause = (ms: number) => new Promise((done) => setTimeout(done, ms));

/**
 * A first screen: the shell's answer, and a view's that waits on it, as the frame's queries do. The lift is asked
 * before either, as `App` asks it: its first look sees nothing fetching yet.
 */
function Screen({ first, second }: { first: Promise<string>; second: Promise<string> }) {
  useSplashLift();
  const shell = useQuery({ queryKey: ['shell'], queryFn: () => first });
  useQuery({ queryKey: ['view'], queryFn: () => second, enabled: shell.data !== undefined });
  return null;
}

function mount(first: Promise<string>, second: Promise<string>) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Screen first={first} second={second} />
    </QueryClientProvider>,
  );
  return client;
}

afterEach(() => {
  cleanup();
  closeSplash.mockClear();
});

describe('the splash lifts once the first screen has its data', () => {
  it('says nothing while the data is on its way, and says it once the last of it has come', async () => {
    const first = deferred<string>();
    const second = deferred<string>();
    mount(first.promise, second.promise);

    await act(() => pause(SPLASH_QUIET_MS * 3));
    expect(closeSplash).not.toHaveBeenCalled();

    // The shell's answer, and the view's question it lets start: still not the screen's data.
    await act(async () => first.resolve('shell'));
    await act(() => pause(SPLASH_QUIET_MS * 3));
    expect(closeSplash).not.toHaveBeenCalled();

    await act(async () => second.resolve('view'));
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
  });

  it('takes a failure for the screen\'s answer too: the screen then says what failed', async () => {
    const first = deferred<string>();
    mount(first.promise, Promise.resolve('never asked'));

    await act(async () => first.reject(new Error('the host did not answer')));
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
  });

  it('says it once, however much is fetched after', async () => {
    const client = mount(Promise.resolve('shell'), Promise.resolve('view'));
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));

    await act(() => client.invalidateQueries());
    await act(() => pause(SPLASH_QUIET_MS * 3));
    expect(closeSplash).toHaveBeenCalledTimes(1);
  });

  it('names the first screen by the queries it draws, and only those', () => {
    expect(isFirstScreen(['status'])).toBe(true);
    expect(isFirstScreen(['sessions', 'groups', '*'])).toBe(true);
    expect(isFirstScreen(['driver'])).toBe(true);
    expect(isFirstScreen(['driver', 'lines'])).toBe(false);
    expect(isFirstScreen(['harnesses'])).toBe(false);
  });

  it('loses nothing where no window is here: the call rejects and the page goes on', async () => {
    closeSplash.mockImplementationOnce(() => Promise.reject(new Error('NO_ROUTE')));
    mount(Promise.resolve('shell'), Promise.resolve('view'));

    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
  });
});

/**
 * A start's first screen (STARTFAST1b): the frame's status and the Sessions list's groups, which waits on it, beside the
 * harnesses' probes and the lines, which queue behind the driver's first looks and are no part of what the screen draws.
 */
function StartScreen({ status, groups, probes, lines }: Record<'status' | 'groups' | 'probes' | 'lines', Promise<string>>) {
  useSplashLift();
  const shell = useQuery({ queryKey: ['status'], queryFn: () => status });
  useQuery({ queryKey: ['sessions', 'groups', '*'], queryFn: () => groups, enabled: shell.data !== undefined });
  useQuery({ queryKey: ['harnesses'], queryFn: () => probes });
  useQuery({ queryKey: ['driver', 'lines'], queryFn: () => lines });
  return null;
}

describe('the splash lifts on the first screen\'s own data (STARTFAST1b)', () => {
  it('says it once the first screen has answered, while the probes and the lines still load', async () => {
    const status = deferred<string>();
    const groups = deferred<string>();
    const never = new Promise<string>(() => {});
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <StartScreen status={status.promise} groups={groups.promise} probes={never} lines={never} />
      </QueryClientProvider>,
    );

    await act(() => pause(SPLASH_QUIET_MS * 3));
    expect(closeSplash).not.toHaveBeenCalled();

    // The frame's answer, and the list's question it lets start: the list is still on its way.
    await act(async () => status.resolve('status'));
    await act(() => pause(SPLASH_QUIET_MS * 3));
    expect(closeSplash).not.toHaveBeenCalled();

    await act(async () => groups.resolve('groups'));
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
    expect(client.isFetching({ queryKey: ['harnesses'] })).toBe(1);
  });

  it('is not held by a tick asking an answered query again', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    let asked = 0;
    function Ticking() {
      useSplashLift();
      // Answered once, then each later ask never answers: a refetch the tick started keeps the screen's answer.
      useQuery({ queryKey: ['driver'], queryFn: () => (asked++ === 0 ? Promise.resolve('state') : new Promise<string>(() => {})) });
      useQuery({ queryKey: ['harnesses'], queryFn: () => new Promise<string>(() => {}) });
      return null;
    }
    render(
      <QueryClientProvider client={client}>
        <Ticking />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(client.getQueryData(['driver'])).toBe('state'));
    void client.invalidateQueries({ queryKey: ['driver'] });
    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
  });
});
