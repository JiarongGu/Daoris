import { useMemo } from 'react';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys, NOTICES_SLICE } from '../../data/queries';
import { useAskMessages } from '../notices';
import { usePushedLive } from '../state/slices';
import {
  type Channel, type ChannelForwarded, channelOf, type ChannelPost, type ChannelPosted, elsewhereOf, forwardedOf, type LineChannel,
  lineChannelOf, postedOf,
} from '../../work/channel';
import { type AskPlan, askPlanOf } from '../../work/askPlan';
import { call } from '../call';
import { askHeld } from '../state/held';

// An ask's own doors: its channel, where its sessions and the person
// talk, read, posted to and marked read. The driver's `AsksRoutes` answers all three over the service's doors, which
// `daoris ask channel` reads the same way. Shell-only: a browser has no driver, and no channel is asked there.

/** The shapes the routes answer live with the screen's own rule (`work/channel.ts`), since a molecule may read a shape and never a bridge domain. */
export type { Channel, ChannelForwarded, ChannelItem, ChannelPost, ChannelPosted, ChannelReceipt, ElsewhereAsk } from '../../work/channel';

/**
 * Under the asks' key, so a tick, which asks the asks again (the attention band reads them beside the sessions), asks each
 * channel again too: what a session posted shows within a tick, and the unread counts follow.
 */
export const channelKey = (ask: string) => [...keys.allAsks, 'channel', ask] as const;

const readChannel = async (ask: string) => channelOf(await call('CHANNEL_READ', { ask }));

/**
 * An ask's channel as the person reads it (`CHANNEL_READ`), moving no mark. `available` says whether a driver answers at all,
 * so a browser draws no section; a null channel with `loading` false is a shell from before the channel.
 */
export const useChannel = (ask: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: channelKey(ask ?? 'none'),
    queryFn: () => readChannel(ask!),
    enabled: isAvailable && enabled && ask !== null,
    refetchOnWindowFocus: false,
  });
  return {
    channel: query.data ?? null,
    loading: query.isPending && query.fetchStatus !== 'idle',
    error: query.error,
    available: isAvailable,
  };
};

/**
 * How many items of each ask's channel are past the person's mark, for the ask's row and the list by ask. The same reads as
 * `useChannel`, under the same key, so a row's count and the page's section ask once. An ask whose channel could not be read
 * has no entry; nothing is asked in a browser.
 *
 * @remarks
 * **Pushed once live**: where the `notices` slice is live with its `messages` partition,
 * each ask's count is the partition's, the service's own for the person's mark, and no channel is read for it; an ask the partition
 * does not hold (a closed one a rail names) is still pulled. A channel is still pulled where it is read whole (`useChannel`).
 */
export const useChannelUnreads = (asks: readonly string[], { enabled = true }: { enabled?: boolean } = {}): Readonly<Record<string, number>> => {
  const { isAvailable } = useShenora();
  const live = usePushedLive(NOTICES_SLICE);
  const pushed = useAskMessages();
  const counts = useMemo(
    () => (live && pushed ? new Map(pushed.map((entry) => [entry.ask, entry.unread])) : null),
    [live, pushed],
  );
  const pulled = useQueries({
    queries: asks.map((ask) => ({
      queryKey: channelKey(ask),
      queryFn: () => readChannel(ask),
      enabled: isAvailable && enabled && !counts?.has(ask),
      refetchOnWindowFocus: false,
    })),
    combine: (results) => Object.fromEntries(
      results.flatMap((result, at): [string, number][] => (result.data ? [[asks[at], result.data.unread]] : [])),
    ),
  });
  return useMemo(() => {
    if (!counts) return pulled;
    return Object.fromEntries(asks.flatMap((ask): [string, number][] => {
      const unread = counts.get(ask) ?? pulled[ask];
      return unread === undefined ? [] : [[ask, unread]];
    }));
  }, [asks, counts, pulled]);
};

/** Under the asks' key, like each channel: a tick asks the asks again, and the teammates' asks follow. */
export const elsewhereKey = [...keys.allAsks, 'elsewhere'] as const;

/**
 * The asks the mirrored messages name that this machine does not hold (`ASKS_ELSEWHERE`): a teammate's ask has no
 * record here, and its page is its channel. None in a browser, which has no driver, and none from a shell without the route.
 */
export const useElsewhereAsks = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: elsewhereKey,
    queryFn: async () => elsewhereOf(await call<unknown>('ASKS_ELSEWHERE', {})),
    enabled: isAvailable && enabled,
    refetchOnWindowFocus: false,
  });
  return { asks: query.data ?? [], loading: query.isPending && query.fetchStatus !== 'idle', available: isAvailable };
};

/**
 * An ask's plan (`ASK_PLAN`): its parts with the quest each became, the edges between them, what the judge found and each split a
 * worker made of its own part. Under the asks' key, so a tick asks it again. Null while it is read and in a browser, which has no driver.
 */
export const useAskPlan = (ask: string | null, { enabled = true }: { enabled?: boolean } = {}): AskPlan | null => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: [...keys.allAsks, 'plan', ask ?? 'none'] as const,
    queryFn: async () => askPlanOf(await call('ASK_PLAN', { ask })),
    enabled: isAvailable && enabled && ask !== null,
    refetchOnWindowFocus: false,
  });
  return query.data ?? null;
};

/**
 * The channel of the line `session` is a part of, where that line is on no ask held here (`CHANNEL_LINE`): a chat's, a quest
 * session's, a teammate's ask's. Told by event, and under the asks' key, so a tick asks it again with each channel. Null while it is
 * read, where the line has none of its own, and in a browser.
 */
export const useLineChannel = (session: string | null, { enabled = true }: { enabled?: boolean } = {}): LineChannel | null => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.allAsks, 'line', session ?? 'none'] as const;
  const query = useQuery({
    queryKey,
    queryFn: () => askHeld('CHANNEL_LINE', client, queryKey, { session: session ?? '' }, lineChannelOf),
    enabled: isAvailable && enabled && session !== null,
    refetchOnWindowFocus: false,
  });
  return query.data ?? null;
};

/**
 * *Send* (`CHANNEL_POST`): the person's words, to one repository or everyone, answering a message or not. A refusal is an
 * answer with the service's code, which the section words, so it resolves rather than rejects. Its route publishes the asks,
 * whose key holds each channel, and the state hub asks them again in every window.
 */
export const usePostChannel = () =>
  useMutation({
    mutationFn: async ({ ask, post }: { ask: string; post: ChannelPost }): Promise<ChannelPosted> => postedOf(await call(
      'CHANNEL_POST',
      { ask, text: post.text, ...(post.to ? { to: post.to } : {}), ...(post.re ? { re: post.re } : {}) },
    )),
  });

/**
 * The person's words on a line's own channel (`CHANNEL_LINE_POST`): to everyone on it or one repository, from *At work*. A
 * refusal is an answer with the service's code, so it resolves rather than rejects. Its route publishes the asks, whose key holds each
 * line's channel, so the words show at once in every window.
 */
export const usePostLineChannel = () =>
  useMutation({
    mutationFn: async ({ session, post }: { session: string; post: ChannelPost }): Promise<ChannelPosted> => postedOf(await call(
      'CHANNEL_LINE_POST',
      { session, text: post.text, ...(post.to ? { to: post.to } : {}) },
    )),
  });

/**
 * *Send to its session* (`CHANNEL_FORWARD`): a message of the ask's channel sent on to the
 * newest session here of the line of work in `repository`, as the agent's claim passed on by the person. A refusal is an answer with
 * its code, which the section words, so it resolves rather than rejects. Its route publishes the asks, whose key holds each channel,
 * so its receipts move in every window.
 */
export const useForwardChannel = () =>
  useMutation({
    mutationFn: async ({ ask, message, repository }: { ask: string; message: string; repository: string }): Promise<ChannelForwarded> =>
      forwardedOf(await call('CHANNEL_FORWARD', { ask, message, repository })),
  });

/**
 * The person's mark moved (`CHANNEL_MARK`) through the newest item they were shown. The answer is the channel as it then
 * reads, so the unread count falls at once, in the section and in the rows, without asking again; its route publishes the
 * asks, so every other window's count falls too.
 */
export const useMarkChannel = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ ask, through }: { ask: string; through?: string | null }): Promise<Channel | null> => channelOf(
      await call('CHANNEL_MARK', { ask, ...(through ? { through } : {}) }),
    ),
    onSuccess: (channel, { ask }) => {
      if (channel) client.setQueryData(channelKey(ask), channel);
    },
  });
};
