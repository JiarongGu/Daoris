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
import {
  channelKey, elsewhereKey, useChannel, useElsewhereAsks, useChannelUnreads, useForwardChannel, useMarkChannel, usePostChannel,
} from './asks';
import { useStateHub } from '../state/stateHub';
import { pushedOf } from '../state/slices';
import { noticesSlice } from '../notices';

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

  /**
   * NOTIFY2g (the notification design §2.5): once the `notices` slice is live with its `messages` partition, each ask's count is the
   * partition's, the service's own, with no CHANNEL_READ per ask; an ask the partition does not hold (a closed one a rail names) is
   * still pulled, and a slice without the partition pulls every one as before.
   */
  it('reads the counts from the messages partition once live, with no CHANNEL_READ per ask, and pulls where it is not', async () => {
    const live = async (partitions: Record<string, unknown>) => {
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      invoke.mockImplementation(async (_module: string, route: string, { payload }: { payload: { ask?: string; slice?: string } }) => (
        route === 'STATE_SLICE'
          ? {
            slice: 'notices', epoch: 'e1', status: 'ready',
            partitions: Object.entries(partitions).map(([partition, data]) => ({ partition, seq: 1, status: 'ready', data })),
          }
          : { ...CHANNEL, ask: payload.ask, unread: 9 }));
      pushedOf(client).register(noticesSlice);
      await pushedOf(client).resync('notices');
      invoke.mockClear();
      return client;
    };
    const messages = {
      a1: { ask: 'a1', workspace: 'default', teammate: false, unread: 3 },
      a2: { ask: 'a2', workspace: 'default', teammate: false, unread: 0 },
    };

    const pushed = await live({ news: {}, messages });
    const { result } = renderHook(() => useChannelUnreads(['a1', 'a2']), { wrapper: wrapper(pushed) });
    await waitFor(() => expect(result.current).toEqual({ a1: 3, a2: 0 }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_READ', expect.anything());

    // A closed ask the partition does not hold is read from its channel.
    const closed = renderHook(() => useChannelUnreads(['a1', 'a9']), { wrapper: wrapper(pushed) });
    await waitFor(() => expect(closed.result.current).toEqual({ a1: 3, a9: 9 }));
    expect(invoke).toHaveBeenCalledTimes(1);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_READ', { payload: { ask: 'a9' } });

    // A shell that pushes no messages partition: every ask's channel is pulled, as before.
    const older = await live({ news: {} });
    const pulled = renderHook(() => useChannelUnreads(['a1', 'a2']), { wrapper: wrapper(older) });
    await waitFor(() => expect(pulled.result.current).toEqual({ a1: 9, a2: 9 }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_READ', { payload: { ask: 'a1' } });
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
   * COWORK1d (the message-urgency design §4.2): the person's *Send to its session* is CHANNEL_FORWARD with the message and the
   * repository whose line of work it goes to; the answer's code is read, and one of another shape is a refusal said `other`.
   */
  it('sends a message on to its session on CHANNEL_FORWARD and reads the answer’s code', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ sent: false, code: 'no-part', session: null }));
    const { result } = renderHook(() => useForwardChannel(), { wrapper: wrapper(client) });

    let forwarded;
    await act(async () => { forwarded = await result.current.mutateAsync({ ask: 'a1', message: 'm1', repository: 'web' }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_FORWARD', { payload: { ask: 'a1', message: 'm1', repository: 'web' } });
    expect(forwarded).toEqual({ sent: false, code: 'no-part', session: null });

    invoke.mockImplementation(async () => ({ something: 'else' }));
    await act(async () => { forwarded = await result.current.mutateAsync({ ask: 'a1', message: 'm1', repository: 'web' }); });
    expect(forwarded).toEqual({ sent: false, code: 'other', session: null });
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
