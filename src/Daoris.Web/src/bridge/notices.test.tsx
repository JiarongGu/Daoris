import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

const { call, snapshot } = vi.hoisted(() => ({
  call: vi.fn(async (_type: string, _payload?: Record<string, unknown>) => ({ written: true })),
  snapshot: vi.fn(async (): Promise<unknown> => null),
}));
vi.mock('./call', () => ({ call }));
vi.mock('./driver', () => ({ readSlice: snapshot }));
vi.mock('@shenora/react', async (original) => ({
  ...await original<typeof import('@shenora/react')>(),
  isShenoraAvailable: () => true,
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => {},
}));

import { WorkspaceScopeProvider } from '../data/scope';
import {
  composeNotices, messageOf, noticeOf, noticesSlice, useDismissNotice, useMarkNoticesRead, useMessages, useNotices,
} from './notices';
import { pushedOf } from './state/slices';

// Done and failed as the shell pushes it (NOTIFY2f, the notification design §2.5): the `news` partition composed newest first, a
// notice of a shape the page cannot read dropped, nothing until the owner judged; and the person's read and dismissal as presses.
// Messages beside it (NOTIFY2g): the `messages` partition, keyed by ask, and the `answered` notices, which are Messages', never Done
// and failed's; each read in the window's scope.

const NEWS = {
  'ended-s1': { id: 'ended-s1', kind: 'ended', at: '2026-10-22T08:00:00.000Z', read: true, state: 'failed', workspace: 'studio' },
  'expired-p1': { id: 'expired-p1', kind: 'expired', at: '2026-10-22T09:00:00.000Z', route: 'SESSION_INPUT' },
  'answered-c1': { id: 'answered-c1', kind: 'answered', at: '2026-10-22T09:30:00.000Z', read: false, session: 'c1', repository: 'game', workspace: 'studio' },
  'answered-c2': { id: 'answered-c2', kind: 'answered', at: '2026-10-22T09:40:00.000Z', read: false, session: 'c2', repository: 'web', workspace: 'home' },
};
const NEWEST = { id: 'm2', at: '2026-10-22T09:20:00.000Z', who: 'agent', repository: 'db', line: 'Which column?', level: 'urgent', toEveryone: true };
const MESSAGES = {
  a1: { ask: 'a1', workspace: 'studio', teammate: false, unread: 2, newest: NEWEST },
  a2: { ask: 'a2', workspace: 'home', teammate: false, unread: 1, newest: { ...NEWEST, id: 'm3', at: '2026-10-22T09:25:00.000Z' } },
  '7e6f51': { ask: '7e6f51', workspace: 'studio', teammate: true, unread: 0 },
  odd: { workspace: 'studio' },
};

const wrapper = (client: QueryClient, scope: string | null = null) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}><WorkspaceScopeProvider initial={scope}>{children}</WorkspaceScopeProvider></QueryClientProvider>
);

/** A window whose `notices` slice is live with these partitions, as its snapshot installs them. */
async function live(partitions: Record<string, unknown>) {
  snapshot.mockResolvedValueOnce({
    slice: 'notices', epoch: 'e1', status: 'ready',
    partitions: Object.entries(partitions).map(([partition, data]) => ({ partition, seq: 1, status: 'ready', data })),
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  pushedOf(client).register(noticesSlice);
  await pushedOf(client).resync('notices');
  return client;
}

describe('the notices slice on the page', () => {
  it('composes the news newest first, drops what it cannot read, and holds nothing until judged', () => {
    const news = {
      'ended-s1': NEWS['ended-s1'],
      'expired-p1': NEWS['expired-p1'],
      odd: { kind: 'ended' },
    };
    expect(composeNotices(new Map([['news', news]]), false)).toBeUndefined();
    expect(composeNotices(new Map(), true)).toBeUndefined();

    const composed = composeNotices(new Map([['news', news]]), true)!;
    expect(composed.news.map((notice) => notice.id)).toEqual(['expired-p1', 'ended-s1']);
    // An absent read is unread.
    expect(composed.news[0]!.read).toBe(false);
    expect(noticeOf('x')).toBeNull();
  });

  it('composes the messages by ask, newest first, an entry it cannot read dropped, and none from a shell without the partition', () => {
    const composed = composeNotices(new Map<string, unknown>([['news', NEWS], ['messages', MESSAGES]]), true)!;
    expect(composed.messages!.map((entry) => entry.ask)).toEqual(['a2', 'a1', '7e6f51']);
    expect(composed.messages![1]).toEqual(MESSAGES.a1);
    // An older shell pushes news alone: the page then pulls each ask's channel, as before.
    expect(composeNotices(new Map([['news', NEWS]]), true)!.messages).toBeUndefined();
  });

  it('reads an arrival of a message by its id and its ask, and nothing of another shape', () => {
    expect(messageOf({ ...NEWEST, ask: 'a1', workspace: 'studio', teammate: false }))
      .toEqual({ ask: 'a1', workspace: 'studio', teammate: false, newest: NEWEST });
    // A level the page does not know reads as ordinary, as one from before the levels does.
    expect(messageOf({ ...NEWEST, ask: 'a1', workspace: 'studio', teammate: false, level: 'now' })!.newest.level).toBe('ordinary');
    expect(messageOf({ ask: 'a1' })).toBeNull();
    expect(messageOf(null)).toBeNull();
  });

  it("keeps Done and failed's notices out of Messages and the answers out of Done and failed, each in the window's scope", async () => {
    const client = await live({ news: NEWS, messages: MESSAGES });

    const done = renderHook(() => useNotices(), { wrapper: wrapper(client, 'studio') });
    await waitFor(() => expect(done.result.current.map((notice) => notice.id)).toEqual(['expired-p1', 'ended-s1']));

    const messages = renderHook(() => useMessages(), { wrapper: wrapper(client, 'studio') });
    await waitFor(() => expect(messages.result.current.asks?.map((entry) => entry.ask)).toEqual(['a1', '7e6f51']));
    expect(messages.result.current.answered.map((notice) => notice.id)).toEqual(['answered-c1']);

    // Every workspace's, where the window holds none.
    const every = renderHook(() => useMessages(), { wrapper: wrapper(client) });
    await waitFor(() => expect(every.result.current.asks?.map((entry) => entry.ask)).toEqual(['a2', 'a1', '7e6f51']));
    expect(every.result.current.answered.map((notice) => notice.id)).toEqual(['answered-c2', 'answered-c1']);
  });

  it('answers no messages where the shell pushes no such partition, so the page falls back to its pulls', async () => {
    const client = await live({ news: NEWS });
    const { result } = renderHook(() => useMessages(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.answered).toHaveLength(2));
    expect(result.current.asks).toBeUndefined();
  });

  it("presses NOTICES_READ with the ids and NOTICE_DISMISS with one, each answering whether it was written", async () => {
    const client = new QueryClient();
    const plain = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    const read = renderHook(() => useMarkNoticesRead(), { wrapper: plain });
    const dismiss = renderHook(() => useDismissNotice(), { wrapper: plain });

    expect(await read.result.current.mutateAsync(['ended-s1', 'expired-p1'])).toBe(true);
    expect(call).toHaveBeenLastCalledWith('NOTICES_READ', { ids: ['ended-s1', 'expired-p1'] });
    call.mockResolvedValueOnce({ written: false });
    expect(await dismiss.result.current.mutateAsync('ended-s1')).toBe(false);
    expect(call).toHaveBeenLastCalledWith('NOTICE_DISMISS', { id: 'ended-s1' });
    await waitFor(() => expect(dismiss.result.current.isSuccess).toBe(true));
  });
});
