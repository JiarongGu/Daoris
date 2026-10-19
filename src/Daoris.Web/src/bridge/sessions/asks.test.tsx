import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// An ask's channel, as hooks (SESSONE2c, D167): its read, the person's post and their mark, each one route of DAORIS.DRIVER,
// which the page names only here (MOD3, MOD5).

const { invoke, available, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  available: { value: true },
  // The state hub's ear (STATEHUB1h, D169): what the mocked shell publishes after a press reaches it, as the kit's pump would.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => available.value,
    getBridge: () => ({ isAvailable: available.value, invoke: published }),
    useShenora: () => ({ isAvailable: available.value }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { keys } from '../../data/queries';
import { channelKey, elsewhereKey, useChannel, useElsewhereAsks, useChannelUnreads, useMarkChannel, usePostChannel } from './asks';
import { useStateHub } from '../state/stateHub';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);

const CHANNEL = {
  ask: 'a1', entry: 'person', unread: 2, mark: null,
  items: [{ kind: 'message', at: '2026-10-14T09:02:00Z', id: 'm1', person: false, repository: 'db', text: 'Which column?' }],
};

describe('the asks domain', () => {
  afterEach(() => {
    invoke.mockReset();
    heard.clear();
    available.value = true;
  });

  it('reads an ask’s channel on CHANNEL_READ, and nothing where no ask is named', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => CHANNEL);

    const read = renderHook(() => useChannel('a1'), { wrapper: wrapper(client) });
    await waitFor(() => expect(read.result.current.channel?.unread).toBe(2));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_READ', { payload: { ask: 'a1' } });

    invoke.mockClear();
    const none = renderHook(() => useChannel(null), { wrapper: wrapper(client) });
    expect(none.result.current).toMatchObject({ channel: null, available: true });
    expect(invoke).not.toHaveBeenCalled();
  });

  /** A shell from before the channel answers something else, which is no channel. */
  it('reads an answer of another shape as no channel', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ drivable: [] }));
    const { result } = renderHook(() => useChannel('a1'), { wrapper: wrapper(client) });
    await waitFor(() => expect(invoke).toHaveBeenCalled());
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.channel).toBeNull();
  });

  /** Under the asks' key, so a tick, which asks the asks again, asks every channel again too. */
  it('keeps each channel under the asks’ key', () => {
    expect(channelKey('a1').slice(0, 1)).toEqual(keys.allAsks);
  });

  /** One read an ask, the same key as the section's, so a row's count and the page's section ask it once. */
  it('answers the unread of several asks from the same reads as their pages', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async (_module: string, _route: string, { payload }: { payload: { ask: string } }) => (
      { ...CHANNEL, ask: payload.ask, unread: payload.ask === 'a1' ? 3 : 0 }));

    const { result } = renderHook(() => useChannelUnreads(['a1', 'a2']), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current).toEqual({ a1: 3, a2: 0 }));
    expect(client.getQueryData(channelKey('a1'))).toMatchObject({ unread: 3 });
  });

  /** SESSONE2h: the asks held elsewhere are one read of the driver, under the asks' key; a shell before it answers none. */
  it('lists the asks held elsewhere on ASKS_ELSEWHERE, and none from a shell that answers something else', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const held = { id: '7e6f51', workspace: 'default', origins: ['key-a'], latest: '2026-10-15T09:00:00Z', messages: 2, unread: 1 };
    invoke.mockImplementation(async () => ({ asks: [held] }));

    const listed = renderHook(() => useElsewhereAsks(), { wrapper: wrapper(client) });
    await waitFor(() => expect(listed.result.current.asks).toEqual([held]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASKS_ELSEWHERE', { payload: {} });
    expect(elsewhereKey.slice(0, 1)).toEqual(keys.allAsks);

    const older = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ drivable: [] }));
    const none = renderHook(() => useElsewhereAsks(), { wrapper: wrapper(older) });
    await waitFor(() => expect(none.result.current.loading).toBe(false));
    expect(none.result.current.asks).toEqual([]);
  });

  it('asks nothing in a browser, which has no driver and so no channel', () => {
    available.value = false;
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useChannelUnreads(['a1']), { wrapper: wrapper(client) });
    expect(result.current).toEqual({});
    expect(invoke).not.toHaveBeenCalled();
  });

  it('posts the person’s words on CHANNEL_POST with to and re, and reads the answer’s code', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ posted: false, code: 'bad-to' }));
    const { result } = renderHook(() => usePostChannel(), { wrapper: wrapper(client) });

    let posted;
    await act(async () => { posted = await result.current.mutateAsync({ ask: 'a1', post: { text: 'hi', to: 'db', re: 'm1' } }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_POST', { payload: { ask: 'a1', text: 'hi', to: 'db', re: 'm1' } });
    expect(posted).toEqual({ posted: false, code: 'bad-to', message: null });
  });

  /**
   * STATEHUB1h (D169): the post's route publishes the asks, whose key holds each channel, so the window's hub asks the channel
   * again, as every other window's does; the hook asks nothing again itself.
   */
  it('asks the channel again after a post, through what its route publishes', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(channelKey('a1'), CHANNEL);
    invoke.mockImplementation(async () => ({ posted: true, message: CHANNEL.items[0] }));
    const alone = renderHook(() => usePostChannel(), { wrapper: wrapper(client) });

    await act(async () => { await alone.result.current.mutateAsync({ ask: 'a1', post: { text: 'hi' } }); });
    await act(() => new Promise((settled) => { setTimeout(settled, 10); }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_POST', { payload: { ask: 'a1', text: 'hi' } });
    // No hub mounted: nothing asks the channel again.
    expect(client.getQueryState(channelKey('a1'))?.isInvalidated).toBe(false);

    const { result } = renderHook(() => { useStateHub(); return usePostChannel(); }, { wrapper: wrapper(client) });
    await act(async () => { await result.current.mutateAsync({ ask: 'a1', post: { text: 'hi again' } }); });

    await waitFor(() => expect(client.getQueryState(channelKey('a1'))?.isInvalidated).toBe(true));
  });

  it('moves the person’s mark on CHANNEL_MARK and keeps the channel as it then reads', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ ...CHANNEL, unread: 0, mark: '2026-10-14T09:02:00Z' }));
    const { result } = renderHook(() => useMarkChannel(), { wrapper: wrapper(client) });

    await act(async () => { await result.current.mutateAsync({ ask: 'a1', through: '2026-10-14T09:02:00Z' }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_MARK', { payload: { ask: 'a1', through: '2026-10-14T09:02:00Z' } });
    expect(client.getQueryData(channelKey('a1'))).toMatchObject({ unread: 0, mark: '2026-10-14T09:02:00Z' });
  });
});
