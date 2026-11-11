import { type InfiniteData, type QueryClient, useInfiniteQuery, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { useSyncExternalStore } from 'react';
import { byTool, type ToolDoor } from '../agents/tools';
import type { Session } from '../data/api';
import { keys, useHelpSessions } from '../data/queries';
import { type HelpListing, joinPages, listingPage, sameWorkspace } from '../help/history';
import { onTold, toldGraph, toldSessions } from '../help/openDraft';
import type { HelpProposal } from '../help/ProposalCard';
import type { HelpPlace } from '../help/places';
import { type Place, placeSaid } from '../help/focus';
import type { HelpPublishedEntry } from '../help/published/published';
import { call } from './call';
import { logEvent } from './log';
import { usePollUnlessTold } from './state/published';
import { askHeld } from './state/held';
import { useHarnesses } from './agents/agents';

// Ask Daoris: its conversation, the one readied ahead, and what it proposes.

/**
 * Ask Daoris's conversation: the one this machine is running, carried on, or a new one in
 * its room. Refused in the driver's words while no agent is named for it (off until named).
 *
 * @remarks
 * The driver answers once the conversation is placed, `starting`: its room is read and its agent spawned
 * after, and the words said to it meanwhile wait as its queue, opening. A driver without this answers once it runs.
 *
 * Its route publishes the sessions it opened one among, and the state hub asks them again in every window.
 *
 * The press names the workspace its conversation is in: the caller reads the
 * window's at the press and hands it here, so a window that changes workspace while the start is under way moves nothing. None
 * is today's start, `default`'s, which the panel sends only where the machine holds no other workspace to choose.
 *
 * `finishing`: the one running is one the person finished, still winding up past the driver's wait; it is no
 * conversation to carry on, and a start asks again once it has ended.
 */
export const useStartHelp = () =>
  useMutation({
    mutationFn: ({ workspace, focus }: { workspace?: string; focus?: Place } = {}) =>
      call('START_HELP', { ...(workspace ? { workspace } : {}), ...(focus ? { focus } : {}) }) as Promise<
      { sessionId: string | null; message: string; running?: boolean; starting?: boolean; finishing?: boolean }>,
  });

/**
 * Which Ask Daoris conversation waits unseen for the person's first words, and which have had theirs; and the
 * workspace each is opened ahead in, so one opened in another workspace never takes this one's words.
 */
type HelpReadied = { readied: string | null; spoken: string[]; workspaces?: Record<string, string> };
const NOTHING_READIED: HelpReadied = { readied: null, spoken: [] };

/**
 * Ask Daoris's conversation opened as its panel shows, until the person speaks in it: its id, or
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
     * The conversation opened ahead in `workspace` that nobody has spoken in, or null: the newest where it is that
     * workspace's, else an earlier one that is. One opened ahead with no workspace said is any workspace's.
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
      spoke: (id: string) => change((was) => ({
      readied: was.readied === id ? null : was.readied,
      spoken: was.spoken.includes(id) ? was.spoken : [...was.spoken, id],
    })),
  };
};

/** Every conversation's proposals: what the state hub asks again when an Apply or a Not now publishes. */
export const HELP_PROPOSALS = ['help-proposals'] as const;

/**
 * What one conversation of Ask Daoris's proposed that waits for the person, each already
 * judged with the route's own code: one the route would refuse never arrives here, and its agent is told
 * why. A proposal lands mid-turn, written by the connector in another process: the driver tells the windows `help` as it arrives,
 * so a desktop window asks no more than that; a window with no bus asks again every few seconds while it runs.
 */
export const useHelpProposals = (session: string | null, live: boolean) => {
  const { isAvailable } = useShenora();
  const every = usePollUnlessTold(4000);
  const client = useQueryClient();
  return useQuery({
    queryKey: [...HELP_PROPOSALS, session],
    queryFn: () => readProposals(client, session!),
    enabled: isAvailable && session !== null,
    refetchInterval: live ? every : false,
  });
};

/** One conversation's proposals. Told by event: the shell answers what it holds, and its judgement of each comes as `ANSWERED`. */
const readProposals = (client: QueryClient, session: string) =>
  askHeld('HELP_PROPOSALS', client, [...HELP_PROPOSALS, session], { session }, (answer): HelpProposal[] => {
    const proposals = (answer as { proposals?: unknown } | null)?.proposals;
    return Array.isArray(proposals) ? proposals as HelpProposal[] : [];
  });

/** A proposal against a draft, as the editor reads it: the card, its conversation, the draft it is told and who made it. */
export type DraftCard = {
  proposal: HelpProposal;
  session: string;
  /** The graph the conversation is told under the card's fingerprint; null where this window keeps none. */
  base: Record<string, unknown> | null;
  maker: { agent: string; models: string[] } | null;
};

/** Who made a proposal: the conversation's agent as a person calls it, its product else its tool's else its id, and the models it reported. */
export function makerOf(session: Pick<Session, 'door' | 'ran'> | undefined, doors: readonly ToolDoor[]): DraftCard['maker'] {
  if (!session) return null;
  const found = byTool(doors).flatMap((tool) => tool.doors.map((door) => ({ tool, door }))).find((each) => each.door.door === session.door);
  return { agent: found?.door.product || found?.tool.product || session.door, models: [...(session.ran?.models ?? [])] };
}

/**
 * Ask Daoris's proposals against a draft of `place`: each conversation of this window that is told a draft asked
 * for its proposals, under the key Ask Daoris's own list reads, and the `draft` door's cards kept, the newest last, each with the graph it
 * is told and the agent and models of its conversation. Asked only while `enabled`, the editor showing.
 */
export const useDraftProposals = (place: string, enabled: boolean): DraftCard[] => {
  const { isAvailable } = useShenora();
  const every = usePollUnlessTold(4000);
  const client = useQueryClient();
  const sessions = useSyncExternalStore(onTold, toldSessions);
  const lists = useQueries({
    queries: sessions.map((session) => ({
      queryKey: [...HELP_PROPOSALS, session],
      queryFn: () => readProposals(client, session),
      enabled: isAvailable && enabled,
      refetchInterval: enabled ? every : false as const,
    })),
  });
  const found = sessions.flatMap((session, at) => (lists[at]?.data ?? [])
    .filter((proposal) => proposal.kind === 'workflow' && proposal.workflow?.draft && proposal.workflow.scope === place)
    .map((proposal) => ({ proposal, session })));
  // The roster only names the agent that made a proposal, so it is read once the editor shows and a proposal is there.
  const roster = useHarnesses({ enabled: enabled && found.length > 0 });
  const records = useHelpSessions(false, enabled && found.length > 0);
  const doors = Array.isArray(roster.data?.harnesses) ? roster.data.harnesses : [];
  return found.map(({ proposal, session }) => ({
    proposal, session, base: toldGraph(proposal.workflow!.draft!),
    maker: makerOf((records.data ?? []).find((record) => record.id === session), doors),
  }));
};

/**
 * What the person's Apply did: the driver's sentence, whether it is applied, where a go
 * takes the person, and the agent action an update or a pin started.
 */
export type HelpSettled = {
  message: string;
  applied?: boolean;
  /**
   * Where a go takes the person: a place, the one quest or ask in it (`item`), and the
   * workspace Add repository or Import a folder opens filled with (`workspace`), on those two parts alone.
   */
  go?: HelpPlace | null;
  /** The card still stands for another press: a draft's change opened in the editor, which settles nothing. A host without it answers none. */
  stands?: boolean;
  /** The plan is no longer the one the card showed: nothing is applied, `message` says what it is now, and the card stands. */
  changed?: boolean;
};

/**
 * The revision of the plan the card for this proposal shows: an Apply carries it, and the host applies only a plan
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
 * Ask Daoris's conversations, pinned first and then the newest, or those whose name or words hold `search`, a page
 * at a time: `fetchNextPage` asks for the one at the driver's `next`, and `data` is every page read, joined, each
 * conversation once. Asked only while `enabled`, and again whenever a change to one is made here.
 *
 * @remarks
 * An offset points into one order, which a change moves. Asked again, an infinite query asks from
 * offset 0 and takes each next offset from the page it just read, as many pages as it held, so the rows after a change are read
 * in the new order, not half in the old. The first ask is the one a driver without pages takes: no offset.
 *
 * `workspace` is whose conversations: that workspace's, searched and counted by the
 * driver before it takes a page, or every workspace's for null. It is part of the key, so one scope's pages never answer
 * another's.
 */
export const useHelpConversations = (search: string, enabled = true, workspace: string | null = null, about: Place | null = null) => {
  const { isAvailable } = useShenora();
  const words = search.trim();
  return useInfiniteQuery({
    queryKey: [...HISTORY, workspace ?? '*', about ? placeSaid(about) : '', words],
    queryFn: async ({ pageParam }) => listingPage(await call('HELP_CONVERSATIONS', {
      ...(workspace ? { workspace } : {}),
      ...(about ? { about } : {}),
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
 * One `HELP_WIRED` answer read as receipts, each field as the driver wrote it: a value it leaves out reads as
 * none, never as something the start is handed. An answer that does not read is nothing recorded.
 */
export function wiredAnswer(answer: unknown, session: string): HelpWired {
  const held = (answer ?? {}) as { session?: unknown; recorded?: unknown; receipts?: unknown };
  const receipts = rowsOf(held.receipts).map((receipt): HelpReceipt => {
    const rules = typeof receipt.rules === 'object' && receipt.rules !== null ? receipt.rules as Record<string, unknown> : null;
    const loaded = typeof receipt.loaded === 'object' && receipt.loaded !== null ? receipt.loaded as Record<string, unknown> : null;
    return {
      at: textOf(receipt.at) ?? '',
      start: textOf(receipt.start) ?? 'new',
      mode: textOf(receipt.mode) ?? 'restricted',
      door: textOf(receipt.door) ?? '',
      version: textOf(receipt.version),
      account: textOf(receipt.account),
      transport: textOf(receipt.transport) ?? '',
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
      loaded: loaded ? {
        tools: textsOf(loaded.tools),
        servers: rowsOf(loaded.servers).map((server) => ({ name: textOf(server.name) ?? '', status: textOf(server.status) ?? '' })),
        skills: textsOf(loaded.skills),
        plugins: textsOf(loaded.plugins),
      } : null,
    };
  });
  return { session: textOf(held.session) ?? session, recorded: held.recorded === true && receipts.length > 0, receipts };
}

/** The receipts' key: a conversation's under it. */
export const HELP_WIRED = ['help-wired'] as const;

/**
 * What each start of one Ask Daoris conversation handed its agent: its receipts, the newest first,
 * read as *What it has* opens. A read of this machine's home, desktop-only like the conversation (its paths are this machine's).
 * A start going on adds one, so it is asked again each time it is shown, and, while it is shown, each time the `help` scope is
 * published (the state hub's).
 */
export const useHelpWired = (session: string | null, enabled = true) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...HELP_WIRED, session],
    queryFn: async () => wiredAnswer(await call('HELP_WIRED', { session: session! }), session!),
    enabled: isAvailable && session !== null && enabled,
  });
};

/**
 * One `HELP_PUBLISHED` answer read as the conversation's list, each entry field for field as the driver keeps it
 * (`HelpPublished.cs`), newest first: an id the answer leaves out reads as none, an entry with no proposal or neither id as no
 * entry, and an answer that does not read as nothing published.
 */
export function publishedAnswer(answer: unknown): HelpPublishedEntry[] {
  const held = (answer ?? {}) as { published?: unknown };
  return rowsOf(held.published).flatMap((entry): HelpPublishedEntry[] => {
    const proposal = textOf(entry.proposal);
    const kind = textOf(entry.kind);
    const ask = textOf(entry.ask);
    const quest = textOf(entry.quest);
    if (!proposal || !kind || (!ask && !quest)) return [];
    return [{ at: textOf(entry.at) ?? '', proposal, kind, workspace: textOf(entry.workspace), ask, quest }];
  });
}

/** What each conversation published: a conversation's under it, read again when the `help` scope is published (an Apply adds one). */
export const HELP_PUBLISHED = ['help-published'] as const;

/**
 * What one Ask Daoris conversation's Applies published: the asks and quests by id,
 * the newest first. A read of this machine's home, desktop-only as the conversation is; nothing but an Apply moves it, and the
 * Apply publishes `help`, which asks it again, so it is never asked on a clock. Each one's state is the pushed lists'.
 */
export const useHelpPublished = (session: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...HELP_PUBLISHED, session],
    queryFn: async () => publishedAnswer(await call('HELP_PUBLISHED', { session: session! })),
    enabled: isAvailable && session !== null,
    staleTime: Infinity,
  });
};

/**
 * Name an Ask Daoris conversation, or give it its first question back with no name. A history act's route publishes
 * the history and the conversations the panel shows, and the state hub asks them again in every window, as it
 * does for a pin and a start from an earlier conversation.
 */
export const useRenameHelp = () =>
  useMutation({
    mutationFn: (rename: { id: string; name: string | null }) =>
      call('HELP_RENAME', rename.name ? { id: rename.id, name: rename.name } : { id: rename.id }) as Promise<{ session: string; name: string | null }>,
  });

export type HelpFocusAnswer =
  | { session: string; focus: Place; focusNext: Place | null; reopens?: boolean; refused?: undefined }
  | { session: string; refused: 'unknown-place' | 'not-help' | 'ended'; said: string };

/** Choose a conversation's focus at once, so a reload or another window shows it; after its first words its next words take it. */
export const useHelpFocus = () =>
  useMutation({
    mutationFn: (choice: { session: string; focus: Place }) => call('HELP_FOCUS', choice) as Promise<HelpFocusAnswer>,
  });

/** Pin an Ask Daoris conversation to the top of its history, or unpin it. */
export const usePinHelp = () =>
  useMutation({
    mutationFn: (pin: { id: string; pinned: boolean }) => call('HELP_PIN', pin) as Promise<{ session: string; pinned: string | null }>,
  });

/**
 * A new Ask Daoris conversation from an earlier one's words: the driver opens it as it opens any, setting aside the
 * one running, and hands it the earlier one's transcript with the person's first message. It is opened in the workspace the
 * window shows at the press, whatever the earlier one's is; none is `default`'s, as `useStartHelp`'s.
 */
export const useHelpStartFrom = () =>
  useMutation({
    mutationFn: ({ id, workspace }: { id: string; workspace?: string }) =>
      call('HELP_START_FROM', workspace ? { id, workspace } : { id }) as Promise<{ sessionId: string | null; message: string; from: string }>,
  });

/**
 * The person's Apply and Not now on a proposal: the result goes back into the conversation.
 *
 * What it changed is published by its route, and the state hub asks it again in every window: a Not now the
 * proposals; an Apply every family a door it holds may move, the driver's file and what reads it (the workflow tabs' Current
 * and runs among them), an ask, a quest or a session it made or deleted, an account's settings or an agent's pin, a plugin, the
 * browser's settings or favorites, what bringing up to date moved, a repository moved to a workspace, and finished history.
 */
export const useSettleHelp = () => {
  const client = useQueryClient();
  return useMutation({
    // A draft card's Apply from its editor names each change taken into the draft and each left: that settles it.
    mutationFn: (settle: { id: string; apply: boolean; taken?: readonly string[]; left?: readonly string[] }) =>
      (settle.apply
        ? call(
          'HELP_APPLY',
          {
            id: settle.id, revision: shownRevision(client, settle.id),
            ...(settle.taken ? { taken: [...settle.taken], left: [...(settle.left ?? [])] } : {}),
          })
        : call('HELP_DISMISS', { id: settle.id })) as Promise<HelpSettled>,
    // An Apply the page stopped waiting for may still be at work on the host: it is said, never pressed again.
    retry: false,
    onSuccess: (answer, settle) => {
      // Whether Ask Daoris's proposals help: the person's Apply or Not now, once it settles the proposal; a standing card has not.
      if (!answer?.stands) logEvent('proposal.settled', { applied: settle.apply });
    },
  });
};
