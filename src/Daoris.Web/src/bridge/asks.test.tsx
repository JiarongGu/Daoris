import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// An ask's channel, as hooks (SESSONE2c, D167): its read, the person's post and their mark, each one route of DAORIS.DRIVER,
// which the page names only here (MOD3, MOD5).

const { invoke, available } = vi.hoisted(() => ({ invoke: vi.fn(), available: { value: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => available.value,
  getBridge: () => ({ isAvailable: available.value, invoke }),
  useShenora: () => ({ isAvailable: available.value }),
  useShenoraEvent: () => undefined,
}));

import { keys } from '../queries';
import { channelKey, useChannel, useChannelUnreads, useMarkChannel, usePostChannel } from './asks';

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

  it('asks the channel again after a post that was kept', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(channelKey('a1'), CHANNEL);
    const spy = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ posted: true, message: CHANNEL.items[0] }));
    const { result } = renderHook(() => usePostChannel(), { wrapper: wrapper(client) });

    await act(async () => { await result.current.mutateAsync({ ask: 'a1', post: { text: 'hi' } }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'CHANNEL_POST', { payload: { ask: 'a1', text: 'hi' } });
    expect(spy).toHaveBeenCalledWith({ queryKey: channelKey('a1') });
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
