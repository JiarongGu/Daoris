import { type InfiniteData, type QueryClient, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import { type HelpListing, joinPages, listingPage, sameWorkspace } from '../help/history';
import type { HelpProposal } from '../help/ProposalCard';
import type { HelpPlace } from '../help/places';
import { call, pressBound } from './call';
import { syncLookBound } from './repositories/lines';
import { logEvent } from './log';
import { usePollUnlessTold } from './state/published';

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
 *
 * The press names the workspace its conversation is in (ASKSCOPE1d, D158's ASKSCOPE1 note, point 1): the caller reads the
 * window's at the press and hands it here, so a window that changes workspace while the start is under way moves nothing. None
 * is today's start, `default`'s, which the panel sends only where the machine holds no other workspace to choose.
 */
export const useStartHelp = () =>
  useMutation({
    mutationFn: (workspace?: string) => call<{ sessionId: string | null; message: string; running?: boolean; starting?: boolean }>(
      'START_HELP', workspace ? { workspace } : undefined),
  });

/**
 * Which Ask Daoris conversation waits unseen for the person's first words, and which have had theirs; and, since ASKSCOPE1d, the
 * workspace each was opened ahead in, so one opened in another workspace never takes this one's words.
 */
type HelpReadied = { readied: string | null; spoken: string[]; workspaces?: Record<string, string> };
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
  const held = data ?? NOTHING_READIED;
  const opened = held.workspaces ?? {};
  const unspoken = [...new Set([...(held.readied ? [held.readied] : []), ...Object.keys(opened)])]
    .filter((id) => !held.spoken.includes(id));
  return {
    readied: held.readied,
    /**
     * The conversation opened ahead in `workspace` that nobody has spoken in (ASKSCOPE1d), or null: the newest where it is that
     * workspace's, else an earlier one that is. One opened ahead with no workspace said is any workspace's, as before.
     */
    readiedIn: (workspace: string): string | null => {
      const fits = (id: string) => opened[id] === undefined || sameWorkspace(opened[id], workspace);
      if (held.readied && !held.spoken.includes(held.readied) && fits(held.readied)) return held.readied;
      return unspoken.filter((id) => opened[id] !== undefined && fits(id)).at(-1) ?? null;
    },
    /** Every conversation opened ahead that nobody has spoken in, whatever its workspace: none is shown as a workspace's newest. */
    unspoken,
    /** Opened ahead in `workspace`: kept unseen until spoken in, unless it has been already. */
    ready: (id: string, workspace?: string) => change((was) => (was.spoken.includes(id) ? was : {
      ...was, readied: id, ...(workspace === undefined ? {} : { workspaces: { ...was.workspaces, [id]: workspace } }),
    })),
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
 *
 * `workspace` is whose conversations (ASKSCOPE1d, D158's ASKSCOPE1 note, point 6): that workspace's, searched and counted by the
 * driver before it takes a page, or every workspace's for null. It is part of the key, so one scope's pages never answer
 * another's.
 */
export const useHelpConversations = (search: string, enabled = true, workspace: string | null = null) => {
  const { isAvailable } = useShenora();
  const words = search.trim();
  return useInfiniteQuery({
    queryKey: [...HISTORY, workspace ?? '*', words],
    queryFn: async ({ pageParam }) => listingPage(await call<unknown>('HELP_CONVERSATIONS', {
      ...(workspace ? { workspace } : {}),
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

// The mode's and the receipts' shapes live in `help/access/wired`, where the panel's molecules take them (the presentational boundary).
import type { HelpMode, HelpReceipt, HelpWired, HelpWiredCheckout, HelpWiredLack, HelpWiredRules, HelpWiredServer, HelpWiredSkill } from '../help/access/wired';
export type { HelpMode, HelpReceipt, HelpWired, HelpWiredCheckout, HelpWiredLack, HelpWiredRules, HelpWiredServer, HelpWiredSkill };

/** A mode as the driver reads one (`HelpModes`): `full` only when it says so, anything else restricted, the default. */
export const helpModeOf = (value: unknown): HelpMode => (value === 'full' ? 'full' : 'restricted');

const textOf = (value: unknown): string | null => (typeof value === 'string' ? value : null);
const textsOf = (value: unknown): string[] => (Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : []);
const rowsOf = (value: unknown): Record<string, unknown>[] =>
  (Array.isArray(value) ? value.filter((each): each is Record<string, unknown> => typeof each === 'object' && each !== null) : []);

/**
 * One `HELP_WIRED` answer read as receipts (HELPMODE1b), each field as the driver wrote it: a value it leaves out reads as
 * none, never as something the start was handed. An answer that does not read is nothing recorded.
 */
export function wiredAnswer(answer: unknown, session: string): HelpWired {
  const held = (answer ?? {}) as { session?: unknown; recorded?: unknown; receipts?: unknown };
  const receipts = rowsOf(held.receipts).map((receipt): HelpReceipt => {
    const rules = typeof receipt.rules === 'object' && receipt.rules !== null ? receipt.rules as Record<string, unknown> : null;
    return {
      at: textOf(receipt.at) ?? '',
      start: textOf(receipt.start) ?? 'new',
      mode: textOf(receipt.mode) ?? 'restricted',
      harness: textOf(receipt.harness) ?? '',
      version: textOf(receipt.version),
      account: textOf(receipt.account),
      door: textOf(receipt.door) ?? '',
      posture: textOf(receipt.posture),
      permissionMode: textOf(receipt.permissionMode),
      folder: textOf(receipt.folder) ?? '',
      tools: textsOf(receipt.tools),
      skills: rowsOf(receipt.skills).map((skill) => ({ name: textOf(skill.name) ?? '', state: textOf(skill.state) ?? '', why: textOf(skill.why) })),
      servers: rowsOf(receipt.servers).map((server) => ({
        name: textOf(server.name) ?? '', plugin: textOf(server.plugin), state: textOf(server.state) ?? '',
        tools: Array.isArray(server.tools) ? textsOf(server.tools) : null, why: textOf(server.why),
      })),
      rules: rules ? { allow: textsOf(rules.allow), ask: textsOf(rules.ask), deny: textsOf(rules.deny) } : null,
      guarded: receipt.guarded === true,
      checkouts: rowsOf(receipt.checkouts).map((checkout) => ({
        name: textOf(checkout.name) ?? '', workspace: textOf(checkout.workspace) ?? '', path: textOf(checkout.path) ?? '',
      })),
      lacks: rowsOf(receipt.lacks).map((lack) => ({
        kind: textOf(lack.kind) ?? '', name: textOf(lack.name) ?? '', door: textOf(lack.door) ?? '', why: textOf(lack.why) ?? '',
        plugin: textOf(lack.plugin),
      })),
    };
  });
  return { session: textOf(held.session) ?? session, recorded: held.recorded === true && receipts.length > 0, receipts };
}

/** The receipts' key: a conversation's under it. */
export const HELP_WIRED = ['help-wired'] as const;

/**
 * What each start of one Ask Daoris conversation handed its agent (HELPMODE1b, D183 point 4): its receipts, the newest first,
 * read as *What it has* opens. A read of this machine's home, desktop-only like the conversation (its paths are this machine's).
 * A start going on adds one, so it is asked again each time it is shown, and, while it is shown, each time the `help` scope is
 * published (the state hub's, HELPWIREDLIVE1).
 */
export const useHelpWired = (session: string | null, enabled = true) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...HELP_WIRED, session],
    queryFn: async () => wiredAnswer(await call<unknown>('HELP_WIRED', { session: session! }), session!),
    enabled: isAvailable && session !== null && enabled,
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
 * one running, and hands it the earlier one's transcript with the person's first message. It is opened in the workspace the
 * window shows at the press (ASKSCOPE1d), whatever the earlier one's was; none is `default`'s, as `useStartHelp`'s.
 */
export const useHelpStartFrom = () =>
  useMutation({
    mutationFn: ({ id, workspace }: { id: string; workspace?: string }) =>
      call<{ sessionId: string | null; message: string; from: string }>('HELP_START_FROM', workspace ? { id, workspace } : { id }),
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
