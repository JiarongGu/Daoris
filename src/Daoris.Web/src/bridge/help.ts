import { type InfiniteData, type QueryClient, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import { type HelpListing, joinPages, listingPage } from '../help/history';
import type { HelpProposal } from '../help/ProposalCard';
import type { HelpPlace } from '../help/places';
import { call, pressBound } from './call';
import { syncLookBound } from './lines';
import { logEvent } from './log';
import { usePollUnlessTold } from './published';

// Ask Daoris (MOD3): its conversation, the one readied ahead, and what it proposes (D89).

/**
 * Ask Daoris's conversation (HELP1a, D89): the one this machine is running, carried on, or a new one in
 * its room. Refused in the driver's words while no agent is named for it (D89: off until named).
 *
 * @remarks
 * Since HELPSTART1 the driver answers once the conversation is placed, `starting`: its room is read and its agent spawned
 * after, and the words said to it meanwhile wait as its queue, opening. A driver before it answers once it runs.
 *
 * Its route publishes the sessions it opened one among, and the state hub asks them again in every window (STATEHUB1h, D169).
 */
export const useStartHelp = () =>
  useMutation({
    mutationFn: () => call<{ sessionId: string | null; message: string; running?: boolean; starting?: boolean }>('START_HELP'),
  });

/** Which Ask Daoris conversation waits unseen for the person's first words, and which have had theirs. */
type HelpReadied = { readied: string | null; spoken: string[] };
const NOTHING_READIED: HelpReadied = { readied: null, spoken: [] };

/**
 * Ask Daoris's conversation opened as its panel showed (HELP5), until the person speaks in it: its id, or
 * null. Kept by the page and never fetched, like `useConsidered`, so the panel's two hosts (the side bar
 * and Quick Ask) read one answer, and a host drawn again finds it still there. A refetch answers what is
 * already kept, so a refresh of every query does not forget it.
 *
 * @remarks
 * A conversation spoken in is never readied again: an opening that answers late, from the other host,
 * must not hide the conversation the person is talking in.
 */
export const useHelpReadied = () => {
  const client = useQueryClient();
  const { data } = useQuery({
    queryKey: keys.helpReadied,
    queryFn: () => client.getQueryData<HelpReadied>(keys.helpReadied) ?? NOTHING_READIED,
    staleTime: Infinity,
    gcTime: Infinity,
  });
  const change = (next: (was: HelpReadied) => HelpReadied) =>
    client.setQueryData<HelpReadied>(keys.helpReadied, (was) => next(was ?? NOTHING_READIED));
  return {
    readied: data?.readied ?? null,
    /** Opened ahead: kept unseen until spoken in, unless it has been already. */
    ready: (id: string) => change((was) => (was.spoken.includes(id) ? was : { ...was, readied: id })),
    /** The person's words went to it: seen from now on. */
    spoke: (id: string) => change((was) => ({
      readied: was.readied === id ? null : was.readied,
      spoken: was.spoken.includes(id) ? was.spoken : [...was.spoken, id],
    })),
  };
};

/** Every conversation's proposals: what the state hub asks again when an Apply or a Not now publishes (STATEHUB1h, D169). */
export const HELP_PROPOSALS = ['help-proposals'] as const;

/**
 * What one conversation of Ask Daoris's proposed that waits for the person (HELP1c, D89), each already
 * judged with the route's own code: one the route would refuse never arrives here, and its agent is told
 * why. A proposal lands mid-turn, written by the connector in another process: the driver tells the windows `help` as it arrives
 * (FABRIC-EVENT5a), so a desktop window asks no more than that; a window with no bus asks again every few seconds while it runs.
 */
export const useHelpProposals = (session: string | null, live: boolean) => {
  const { isAvailable } = useShenora();
  const every = usePollUnlessTold(4000);
  return useQuery({
    queryKey: [...HELP_PROPOSALS, session],
    queryFn: async () => {
      const answer = await call<{ proposals?: unknown }>('HELP_PROPOSALS', { session: session! });
      return Array.isArray(answer?.proposals) ? answer.proposals as HelpProposal[] : [];
    },
    enabled: isAvailable && session !== null,
    refetchInterval: live ? every : false,
  });
};

/**
 * What the person's Apply did (HELP1c, HELP6): the driver's sentence, whether it was applied, where a go
 * takes the person, and the agent action an update or a pin started.
 */
export type HelpSettled = {
  message: string;
  applied?: boolean;
  /**
   * Where a go takes the person: a place, since ENTRY1f1 the one quest or ask in it (`item`), and since ENTRY1d2b the
   * workspace Add repository or Import a folder opens filled with (`workspace`), on those two parts alone.
   */
  go?: HelpPlace | null;
  harnessAction?: { harness: string; action: 'update' | 'pin' } | null;
  /** The card still stands for another press: a sync card's look, which settles nothing (LEFT3). A host before it answers none. */
  stands?: boolean;
  /** The plan is no longer the one the card showed (HELPAPPLY1): nothing was applied, `message` says what it is now, and the card stands. */
  changed?: boolean;
};

/**
 * How long an Apply may take (WSR7): a sync card's first Apply is the look, which fetches, and its second the press,
 * which replays, and each waits as long as the screen's own would; every other Apply is quick, and waits the bridge's
 * default. The proposal is the one the conversation's list holds, found by its id.
 */
export const settleBound = (client: QueryClient, id: string): number | undefined => {
  const proposal = client.getQueriesData<HelpProposal[]>({ queryKey: HELP_PROPOSALS })
    .flatMap(([, list]) => (Array.isArray(list) ? list : []))
    .find((each) => each.id === id);
  if (proposal?.kind !== 'sync') return undefined;
  return proposal.sync?.looked
    ? pressBound(proposal.sync.rows.filter((row) => row.moves).length)
    : syncLookBound(client);
};

/**
 * The revision of the plan the card for this proposal showed (HELPAPPLY1): an Apply carries it, and the host applies only a plan
 * still that one. Undefined when the conversation holds no such card, which the host refuses for the card to be drawn again.
 */
export const shownRevision = (client: QueryClient, id: string): string | undefined =>
  client.getQueriesData<HelpProposal[]>({ queryKey: HELP_PROPOSALS })
    .flatMap(([, list]) => (Array.isArray(list) ? list : []))
    .find((each) => each.id === id)?.revision;

/** The history's key: every listing under it, so a change asks each again, a delete through Sessions' own hook among them. */
export const HELP_HISTORY = ['help-conversations'] as const;
const HISTORY = HELP_HISTORY;

/** A history's pages as the one list the page shows: stable, so its answer is read again only when a page changes. */
const joined = (data: InfiniteData<HelpListing, number>): HelpListing => joinPages(data.pages);

/**
 * Ask Daoris's conversations (ASKHIST1), pinned first and then the newest, or those whose name or words hold `search`, a page
 * at a time (ASKHIST1d2): `fetchNextPage` asks for the one at the driver's `next`, and `data` is every page read, joined, each
 * conversation once. Asked only while `enabled`, and again whenever a change to one is made here.
 *
 * @remarks
 * An offset points into one order, which a change moves (D158's ASKHIST1d1 note). Asked again, an infinite query asks from
 * offset 0 and takes each next offset from the page it just read, as many pages as it held, so the rows after a change are read
 * in the new order, not half in the old. The first ask is the one a driver before pages took: no offset.
 */
export const useHelpConversations = (search: string, enabled = true) => {
  const { isAvailable } = useShenora();
  const words = search.trim();
  return useInfiniteQuery({
    queryKey: [...HISTORY, words],
    queryFn: async ({ pageParam }) => listingPage(await call<unknown>('HELP_CONVERSATIONS', {
      ...(words ? { q: words } : {}),
      ...(pageParam > 0 ? { offset: pageParam } : {}),
    })),
    initialPageParam: 0,
    getNextPageParam: (page: HelpListing) => page.next,
    select: joined,
    enabled: isAvailable && enabled,
    staleTime: 5_000,
  });
};

/**
 * Name an Ask Daoris conversation (ASKHIST1), or give it its first question back with no name. A history act's route publishes
 * the history and the conversations the panel shows, and the state hub asks them again in every window (STATEHUB1h, D169), as it
 * does for a pin and a start from an earlier conversation.
 */
export const useRenameHelp = () =>
  useMutation({
    mutationFn: (rename: { id: string; name: string | null }) =>
      call<{ session: string; name: string | null }>('HELP_RENAME', rename.name ? { id: rename.id, name: rename.name } : { id: rename.id }),
  });

/** Pin an Ask Daoris conversation to the top of its history, or unpin it (ASKHIST1). */
export const usePinHelp = () =>
  useMutation({
    mutationFn: (pin: { id: string; pinned: boolean }) => call<{ session: string; pinned: string | null }>('HELP_PIN', pin),
  });

/**
 * A new Ask Daoris conversation from an earlier one's words (ASKHIST1): the driver opens it as it opens any, setting aside the
 * one running, and hands it the earlier one's transcript with the person's first message.
 */
export const useHelpStartFrom = () =>
  useMutation({
    mutationFn: (id: string) => call<{ sessionId: string | null; message: string; from: string }>('HELP_START_FROM', { id }),
  });

/**
 * The person's Apply and Not now on a proposal: the result goes back into the conversation (D89).
 *
 * What it changed is published by its route, and the state hub asks it again in every window (STATEHUB1h, D169): a Not now the
 * proposals; an Apply every family a door it holds may move, the driver's file and what reads it (the workflow tabs' Current
 * and runs among them), an ask, a quest or a session it made or deleted, an account's settings or an agent's pin, a plugin, the
 * browser's settings or favorites, what bringing up to date moved, a repository moved to a workspace, and finished history.
 */
export const useSettleHelp = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (settle: { id: string; apply: boolean }) =>
      call<HelpSettled>(settle.apply ? 'HELP_APPLY' : 'HELP_DISMISS',
        settle.apply ? { id: settle.id, revision: shownRevision(client, settle.id) } : { id: settle.id },
        settle.apply ? { timeoutMs: settleBound(client, settle.id) } : undefined),
    // An Apply the page stopped waiting for may still be at work on the host: it is said, never pressed again.
    retry: false,
    onSuccess: (answer, settle) => {
      // Whether Ask Daoris's proposals help (LOG1b): the person's Apply or Not now, once it landed and settled the
      // proposal. A sync card's look settles nothing, and its card stands for the press that does (LEFT3).
      if (!answer?.stands) logEvent('proposal.settled', { applied: settle.apply });
    },
  });
};
