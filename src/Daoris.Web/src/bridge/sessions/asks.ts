import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { type Channel, channelOf, type ChannelPost, type ChannelPosted, elsewhereOf, postedOf } from '../../work/channel';
import { call } from '../call';

// An ask's own doors (SESSONE2c, D167 points 6 to 8; the co-working design §2.4): its channel, where its sessions and the person
// talk, read, posted to and marked read. The driver's `AsksRoutes` answers all three over the service's doors, which
// `daoris-driver ask channel` reads the same way (D50). Shell-only: a browser has no driver, and no channel is asked there.

/** The shapes the routes answer live with the screen's own rule (`work/channel.ts`), since a molecule may read a shape and never a bridge domain. */
export type { Channel, ChannelItem, ChannelPost, ChannelPosted, ElsewhereAsk } from '../../work/channel';

/**
 * Under the asks' key, so a tick, which asks the asks again (the attention band reads them beside the sessions), asks each
 * channel again too: what a session posted shows within a tick, and the unread counts follow.
 */
export const channelKey = (ask: string) => [...keys.allAsks, 'channel', ask] as const;

const readChannel = async (ask: string) => channelOf(await call<unknown>('CHANNEL_READ', { ask }));

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
 */
export const useChannelUnreads = (asks: readonly string[], { enabled = true }: { enabled?: boolean } = {}): Readonly<Record<string, number>> => {
  const { isAvailable } = useShenora();
  return useQueries({
    queries: asks.map((ask) => ({
      queryKey: channelKey(ask),
      queryFn: () => readChannel(ask),
      enabled: isAvailable && enabled,
      refetchOnWindowFocus: false,
    })),
    combine: (results) => Object.fromEntries(
      results.flatMap((result, at): [string, number][] => (result.data ? [[asks[at], result.data.unread]] : [])),
    ),
  });
};

/** Under the asks' key, like each channel: a tick asks the asks again, and the teammates' asks follow. */
export const elsewhereKey = [...keys.allAsks, 'elsewhere'] as const;

/**
 * The asks the mirrored messages name that this machine does not hold (`ASKS_ELSEWHERE`, SESSONE2h): a teammate's ask has no
 * record here, and its page is its channel. None in a browser, which has no driver, and none from a shell from before the route.
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
 * *Send* (`CHANNEL_POST`): the person's words, to one repository or everyone, answering a message or not. A refusal is an
 * answer with the service's code, which the section words, so it resolves rather than rejects. Its route publishes the asks,
 * whose key holds each channel, and the state hub asks them again in every window (STATEHUB1h, D169).
 */
export const usePostChannel = () =>
  useMutation({
    mutationFn: async ({ ask, post }: { ask: string; post: ChannelPost }): Promise<ChannelPosted> => postedOf(await call<unknown>(
      'CHANNEL_POST',
      { ask, text: post.text, ...(post.to ? { to: post.to } : {}), ...(post.re ? { re: post.re } : {}) },
    )),
  });

/**
 * The person's mark moved (`CHANNEL_MARK`) through the newest item they were shown. The answer is the channel as it then
 * reads, so the unread count falls at once, in the section and in the rows, without asking again; its route publishes the
 * asks, so every other window's count falls too (STATEHUB1h, D169).
 */
export const useMarkChannel = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async ({ ask, through }: { ask: string; through?: string | null }): Promise<Channel | null> => channelOf(
      await call<unknown>('CHANNEL_MARK', { ask, ...(through ? { through } : {}) }),
    ),
    onSuccess: (channel, { ask }) => {
      if (channel) client.setQueryData(channelKey(ask), channel);
    },
  });
};
