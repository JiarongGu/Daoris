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

import { SPLASH_QUIET_MS, useSplashLift } from './splash';

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

  it('loses nothing where no window is here: the call rejects and the page goes on', async () => {
    closeSplash.mockImplementationOnce(() => Promise.reject(new Error('NO_ROUTE')));
    mount(Promise.resolve('shell'), Promise.resolve('view'));

    await waitFor(() => expect(closeSplash).toHaveBeenCalledTimes(1));
  });
});
