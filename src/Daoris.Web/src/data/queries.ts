import { useCallback, useMemo } from 'react';
import {
  keepPreviousData, type QueryClient, type QueryKey, useMutation, useQueries, useQuery, useQueryClient, type UseQueryOptions,
} from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { notPushed, readPushed, usePushedLive } from '../bridge/state/slices';
import {
  api, type Ask, type AskAction, HELP_REPOSITORY, type Quest, type QuestAction, type Session, SESSION_ACTIVE, type SetUpRef, type SubmissionRef,
  type SyncStanding, type Verdict,
} from './api';
import { useScope } from './scope';
import { workspaceOf, workspacesOf } from './workspaces';

// The console is a cache over a service; TanStack Query is that cache done correctly:
// deduplicated requests (the badge and the Quests view share one fetch), refetch on focus, and
// invalidation after every mutation instead of hand-rolled reload calls that each view remembered —
// or forgot — to make.

import { keys } from './queryKeys';

export { keys };

export const useStatus = () =>
  useQuery({ queryKey: keys.status, queryFn: ({ signal }) => api.status(signal) });

/** Where the shell's sync slice is read (its spec is `syncSlice` in `bridge/driver`): a partition per wired circle. */
export const SYNC_SLICE = 'sync';

/** A circle's name as the service finds its remote (C#'s `OrdinalIgnoreCase`): each character upper-cased where it maps to one. */
const remoteName = (name: string) => [...name].map((character) => {
  const upper = character.toUpperCase();
  return [...upper].length === 1 ? upper : character;
}).join('');

/**
 * One circle's standing read from every wired circle's, as the shell pushes them, the way `/api/sync` answers it: the circle
 * named as asked, trimmed (`Workspaces.Normalize`), its record found as the service finds the circle's remote; a circle the shell holds
 * no record of has no remote here, which the route answers `wired: false` and nothing else, its null fields left out as the host does.
 */
export function standingOf(all: unknown, workspace: string): SyncStanding {
  const asked = workspace.trim() || 'default';
  const held = typeof all === 'object' && all !== null && !Array.isArray(all)
    ? Object.entries(all).find(([name]) => remoteName(name) === remoteName(asked))?.[1] as unknown
    : undefined;
  return typeof held === 'object' && held !== null && !Array.isArray(held)
    ? { ...(held as SyncStanding), workspace: asked }
    : { workspace: asked, wired: false, ahead: 0, behind: [], conflicts: [] };
}

/**
 * Where one circle stands with its remote: ahead, behind, in conflict, when it last synced.
 * Over HTTP from this machine's own host, so a browser on this machine reads it too — only *Sync now*
 * needs the shell. Null asks nothing: "every circle" has no single standing to show.
 *
 * In the shell the circle's is read from every wired circle's entry with a stable `select`, from memory: never stale and
 * never asked on a focus, and no tick or pass reaches it. A plain browser, the stories' and e2e's harness, pulls it from the service.
 */
export const useSyncStanding = (workspace: string | null) => {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const select = useCallback((all: unknown) => standingOf(all, workspace ?? ''), [workspace]);
  const read: UseQueryOptions<unknown, Error, SyncStanding, readonly unknown[]> = isAvailable
    ? {
      queryKey: keys.syncEvery,
      queryFn: () => readPushed<unknown>(client, SYNC_SLICE),
      select, enabled: workspace !== null, staleTime: Infinity, refetchOnWindowFocus: false,
    }
    : {
      queryKey: keys.sync(workspace ?? '*'),
      queryFn: ({ signal }) => api.syncStanding(workspace!, signal),
      enabled: workspace !== null,
    };
  return useQuery(read);
};

export const useRepositories = () => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.repositories(workspace),
    queryFn: ({ signal }) => api.repositories(workspace, signal),
  });
};

/**
 * The registry — the window's scope by default, or the whole machine's for a choice that is the
 * machine's whatever the window shows: Settings. Scoped, a window on one circle offered a rule,
 * an account's default and a start only that circle's workspaces and repositories.
 */
export const useRegistry = (reach: 'window' | 'machine' = 'window') => {
  const { workspace: scoped } = useScope();
  const workspace = reach === 'machine' ? null : scoped;
  return useQuery({
    queryKey: keys.registry(workspace),
    queryFn: ({ signal }) => api.registry(workspace, signal),
  });
};

/** A repository's code map — asked only once a person opens one. */
export const useCodeMap = (repository: string | null) =>
  useQuery({
    queryKey: keys.codeMap(repository ?? ''),
    queryFn: ({ signal }) => api.codeMap(repository!, signal),
    enabled: repository !== null,
  });

/**
 * The circles this deployment holds — the registry unscoped, because it is the authority
 * and the one reader that must see every workspace is the switcher itself. A row
 * that names none counts as `default`, so one such family stays one circle.
 */
export const useWorkspaces = () =>
  useQuery({
    queryKey: keys.registry(null),
    queryFn: ({ signal }) => api.registry(null, signal),
    select: (rows) => workspacesOf(rows),
  });

/**
 * Every workspace with how many repositories it holds, for the Workspace menu. The same
 * unscoped registry answer `useWorkspaces` reads, so the two can never disagree about which exist.
 */
export const useWorkspaceHoldings = () =>
  useQuery({
    queryKey: keys.registry(null),
    queryFn: ({ signal }) => api.registry(null, signal),
    select: (rows) => {
      const members = new Map<string, string[]>();
      for (const row of rows) {
        const name = workspaceOf(row);
        members.set(name, [...(members.get(name) ?? []), row.repository]);
      }
      return [...members].sort(([a], [b]) => a.localeCompare(b))
        .map(([name, names]) => ({ name, repositories: names.length, members: names.sort() }));
    },
  });

/**
 * The asks, as the shell pushes them (its spec is `asksSlice` in `bridge/sessions/quests`): the widest list, every
 * workspace, closed and done included, under the key the pull of that list reads, from which each view's list is read.
 */
export const ASKS_SLICE = 'asks';

/** The quests, as the shell pushes them (PUSH1d3; its spec is `questsSlice` in `bridge/sessions/quests`), as the asks are. */
export const QUESTS_SLICE = 'quests';

/**
 * A list the shell pushes (PUSH1d2, PUSH1d3) as one view reads it. In the shell, the widest entry its pushes write, from memory: never
 * stale, never asked on a focus, the view's list derived by `select`. A plain browser, the stories' and e2e's harness, pulls the view's
 * own list from the service under its own key.
 */
function pushedList<T>(
  client: QueryClient, shell: boolean, slice: string, widest: QueryKey,
  own: { key: QueryKey; pull: (signal: AbortSignal) => Promise<T[]> },
  select: (all: T[]) => T[],
): UseQueryOptions<T[], Error, T[]> {
  return shell
    ? { queryKey: widest, queryFn: () => readPushed<T[]>(client, slice), select, staleTime: Infinity, refetchOnWindowFocus: false }
    : { queryKey: own.key, queryFn: ({ signal }) => own.pull(signal) };
}

/**
 * One view's asks read from the widest the shell pushes (PUSH1d3), as the service filters `/api/asks`: the workspace as the service
 * compares it, and neither a closed nor a done one unless the closed are asked for (`Asks.ListAsync`), in the order given.
 */
export function asksIn(all: Ask[], includeClosed: boolean, workspace: string | null): Ask[] {
  if (includeClosed && workspace === null) return all;
  const named = workspace === null ? null : circle(workspace);
  return all.filter((ask) => (named === null || circle(ask.workspace) === named)
    && (includeClosed || (ask.state !== 'Closed' && ask.state !== 'Done')));
}

/**
 * One view's quests read from the widest the shell pushes (PUSH1d3), as the service filters `/api/quests` (`Quests.ListAsync`): the
 * receiver named exactly, the workspace as the service compares it, and, unless the closed are asked for, the outstanding alone (open,
 * taken, or a done a departure holds for the person's yes), in the order given.
 */
export function questsIn(all: Quest[], repository: string | null, includeClosed: boolean, workspace: string | null): Quest[] {
  if (repository === null && includeClosed && workspace === null) return all;
  const named = workspace === null ? null : circle(workspace);
  return all.filter((quest) => (repository === null || quest.to === repository)
    && (named === null || circle(quest.workspace ?? '') === named)
    && (includeClosed || quest.status === 'Open' || quest.status === 'Taken' || quest.held === true));
}

/**
 * Asks, scoped like every other cross-repository read — an ask is made in a circle. The asks in scope; `enabled` false asks
 * nothing, for a reader that needs them only sometimes (a park's go-aheads). Where the shell pushes them, read from the one
 * entry its pushes write, so neither a tick nor a focus asks them again.
 */
export const useAsks = (includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const select = useCallback((all: Ask[]) => asksIn(all, includeClosed, workspace), [includeClosed, workspace]);
  return useQuery({
    ...pushedList(
      client, isAvailable, ASKS_SLICE, keys.asks(true, null),
      { key: keys.asks(includeClosed, workspace), pull: (signal) => api.asks(includeClosed, workspace, signal) },
      select,
    ),
    enabled,
  });
};

/**
 * Asking, publishing an ask and closing one. Each can publish a quest, so the quests move with the
 * asks — and they are asked again whether or not the door said yes: a named receiver that refused
 * still leaves the ask kept, with its proposal, which the list must then show. Where the shell
 * pushes them, the change is heard on the service's change stream and pushed.
 */
function useAskChange<TVariables>(fn: (variables: TVariables) => Promise<AskAction>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allAsks, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
    },
  });
}

/**
 * Deleting a record made by mistake: the quests, the asks and where each circle stands all move
 * with it — a delete of an ask takes its quests, and a shared quest's delete is one more thing ahead.
 * Asked again whatever the door said, since a refused delete may still have found the record moved.
 */
function useDelete(fn: (id: string) => ReturnType<typeof api.deleteQuest>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      // Where the shell pushes the quests and the asks, the delete is heard on the change stream and pushed.
      void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allAsks, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
      // Where the shell pushes the standings, a delete's quest write is heard on the change stream and pushed.
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
}

export const useDeleteQuest = () => useDelete((id) => api.deleteQuest(id));
export const useDeleteAsk = () => useDelete((id) => api.deleteAsk(id));

export const useAsk = () => useAskChange((body: Parameters<typeof api.ask>[0]) => api.ask(body));
export const usePublishAsk = () =>
  useAskChange(({ id, to }: { id: string; to: string }) => api.publishAsk(id, to));
export const useCloseAsk = () =>
  useAskChange(({ id, reason }: { id: string; reason: string }) => api.closeAsk(id, reason));
/**
 * The person answers a go-ahead on their ask: every session on the ask is handed it at its next start. `goesOn` false sends
 * no park on with it: a stale go-ahead's refusal runs nothing.
 */
export const useAnswerGoAhead = () =>
  useAskChange(({ id, number, approved, words, goesOn }: { id: string; number: number; approved: boolean; words?: string; goesOn?: boolean }) =>
    api.answerGoAhead(id, number, approved, words, goesOn));

/**
 * The quests in scope; `enabled` false asks nothing, for a reader that needs them only sometimes (a session branch's title).
 * Where the shell pushes them, read from the one entry its pushes write, so neither a tick nor a focus asks them again.
 */
export const useQuests = (repository: string | null, includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const select = useCallback(
    (all: Quest[]) => questsIn(all, repository, includeClosed, workspace), [repository, includeClosed, workspace]);
  return useQuery({
    ...pushedList(
      client, isAvailable, QUESTS_SLICE, keys.quests(null, true, null),
      { key: keys.quests(repository, includeClosed, workspace), pull: (signal) => api.quests(repository, includeClosed, workspace, signal) },
      select,
    ),
    enabled,
  });
};

/** Every row a list holds: the widest read's own selection, stable, so its answer is read again only when the list moves. */
const everyRow = <T,>(rows: T[]) => rows;

/**
 * Every quest this machine's lists hold, closed included, whatever the window's workspace: what an Ask Daoris
 * conversation published is followed wherever it is asked. Where the shell pushes the quests, the one entry its pushes
 * write, from memory, never asked again; in the harness that widest list's own pull. `enabled` false asks nothing.
 */
export const useEveryQuest = (enabled: boolean) => {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const widest = keys.quests(null, true, null);
  return useQuery({
    ...pushedList(client, isAvailable, QUESTS_SLICE, widest, { key: widest, pull: (signal) => api.quests(null, true, null, signal) }, everyRow),
    enabled,
  });
};

/** Every ask, closed and done included, whatever the window's workspace, as `useEveryQuest` reads the quests. */
export const useEveryAsk = (enabled: boolean) => {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const widest = keys.asks(true, null);
  return useQuery({
    ...pushedList(client, isAvailable, ASKS_SLICE, widest, { key: widest, pull: (signal) => api.asks(true, null, signal) }, everyRow),
    enabled,
  });
};

/**
 * One quest's moves, in the order its history replays: read only for the quest named, which its page names
 * once its flowchart is opened, as the trace is read only when opened; `null` names none and asks nothing. Pulled in a
 * browser and in the window alike: the service's door answers both, and a host without it answers no moves (`null`).
 */
export const useQuestMoves = (id: string | null) =>
  useQuery({
    queryKey: keys.questMoves(id ?? ''),
    queryFn: ({ signal }) => api.questMoves(id!, signal),
    enabled: id !== null,
  });

/**
 * The session records the shell pushes (its spec is `sessionsSlice` in `bridge/sessions/sessions`): the widest list, every
 * repository, closed included, every workspace, under the key the pull of that list reads, from which each view's list is read.
 */
export const SESSIONS_SLICE = 'sessions';

/** The call each running session is making, as the shell pushes it (its spec is `callsSlice` in `bridge/sessions/sessions`). */
export const CALLS_SLICE = 'calls';

/**
 * The session list's groups and entries, as the shell pushes them (its spec is `groupsSlice` in `bridge/sessions/sessions`): what
 * `SESSION_GROUPS` answers for every session, under that pull's widest key.
 */
export const GROUPS_SLICE = 'groups';

/** What sessions consume, as the shell pushes it (its spec is `usageSlice` in `bridge/agents/agents`): `USAGE`'s answer. */
export const USAGE_SLICE = 'usage';

/**
 * How each agent's accounts are used, as the shell pushes it (its spec is `accountsSlice` in `bridge/agents/accounts`):
 * `ACCOUNTS`' answer, a partition per agent, with the verdicts .NET judges beside each reading (its curve and whether it is over it).
 */
export const ACCOUNTS_SLICE = 'accounts';

/**
 * Who needs the person, as the shell pushes it (its spec is `attentionSlice` in `bridge/attention`): *What needs you*'s rows
 * and what the Sessions badge counts, judged in .NET.
 */
export const ATTENTION_SLICE = 'attention';

/** Done and failed, as the shell pushes it (its spec is `noticesSlice` in `bridge/notices`): the notices' record, judged in .NET. */
export const NOTICES_SLICE = 'notices';

/** The service's own name for a workspace asked of it (`Workspaces.Normalize`), its ASCII letters folded as its `NOCASE` folds them. */
const circle = (workspace: string) => (workspace.trim() || 'default').replace(/[A-Z]/g, (letter) => letter.toLowerCase());

/**
 * Whether a record of `workspace` is in a window's scope, as the service filters a list by it (`circle`): every record where the window
 * looks at every circle. *What needs you*'s pushed rows are filtered by it as the lists they were judged from are.
 */
export const inCircle = (workspace: string | null | undefined, scope: string | null): boolean =>
  scope === null || circle(workspace ?? '') === circle(scope);

/**
 * One view's list read from the widest the shell pushes, as the service filters `/api/sessions`: the repository named
 * exactly, the workspace as the service compares it, the open ones alone unless the closed are asked for, in the order given. The
 * widest asked for is the list itself.
 */
export function sessionsIn(all: Session[], repository: string | null, includeClosed: boolean, workspace: string | null): Session[] {
  if (repository === null && includeClosed && workspace === null) return all;
  const named = workspace === null ? null : circle(workspace);
  return all.filter((session) => (repository === null || session.repository === repository)
    && (named === null || circle(session.workspace ?? '') === named)
    && (includeClosed || SESSION_ACTIVE.has(session.state)));
}

/**
 * In the shell every list is read from the one entry its pushes write, from memory: never stale and never asked on a focus.
 * A plain browser, the stories' and e2e's harness, pulls each list from the service under its own key.
 */
function useSessionsRead(
  repository: string | null, includeClosed: boolean, workspace: string | null,
): { read: UseQueryOptions<Session[], Error, Session[]> } {
  const client = useQueryClient();
  const { isAvailable } = useShenora();
  const select = useCallback(
    (all: Session[]) => sessionsIn(all, repository, includeClosed, workspace), [repository, includeClosed, workspace]);
  return isAvailable
    ? {
      read: {
        queryKey: keys.sessions(null, true, null),
        queryFn: () => readPushed<Session[]>(client, SESSIONS_SLICE),
        select, staleTime: Infinity, refetchOnWindowFocus: false,
      },
    }
    : {
      read: {
        queryKey: keys.sessions(repository, includeClosed, workspace),
        queryFn: ({ signal }) => api.sessions(repository, includeClosed, workspace, signal),
      },
    };
}

/** Session records are read-only here: the controls act where a driver is attached. */
/** The sessions in scope; `enabled` false asks nothing, for a reader that needs them only sometimes (a review's title). */
export const useSessions = (repository: string | null, includeClosed: boolean, enabled = true) => {
  const { workspace } = useScope();
  const { read } = useSessionsRead(repository, includeClosed, workspace);
  return useQuery({ ...read, enabled });
};

/** Every session, ended ones included, whatever the window's workspace: who works a quest an Ask Daoris conversation published. */
export const useEverySession = (enabled: boolean) => {
  const { read } = useSessionsRead(null, true, null);
  return useQuery({ ...read, enabled });
};

/** A parked line's checkout let go for others: the ledger clears the mark, and the pushed sessions carry the record back. */
export const useReleasePlace = () => useMutation({ mutationFn: (session: string) => api.releasePlace(session) });

/**
 * A mark that moves when any quest's status, any ask's state or its quests, or any session's state moves, read from
 * the slices the shell pushes and only while all three are pushed: never a pull, so nothing is asked on its account. Null while
 * they are not pushed, or not `enabled`. What a go card's sentence says of its quest, ask or session moves with it, so the card
 * is drawn again at the push rather than its Apply finding a plan other than the one shown.
 */
export function useStandingsMark(enabled: boolean): string | null {
  const live = [usePushedLive(QUESTS_SLICE), usePushedLive(ASKS_SLICE), usePushedLive(SESSIONS_SLICE)].every(Boolean);
  const quests = useEveryQuest(enabled && live);
  const asks = useEveryAsk(enabled && live);
  const sessions = useEverySession(enabled && live);
  return useMemo(() => {
    if (!enabled || !live || !quests.data || !asks.data || !sessions.data) return null;
    return [
      ...quests.data.map((quest) => `q:${quest.id}:${quest.status}:${quest.held === true}`),
      ...asks.data.map((ask) => `a:${ask.id}:${ask.state}:${ask.quests.length}`),
      ...sessions.data.map((session) => `s:${session.id}:${session.state}`),
    ].join(' ');
  }, [enabled, live, quests.data, asks.data, sessions.data]);
}

/**
 * The second opinions on this machine, asked only where the sessions list holds a reviewer (`enabled`). A 404 (a
 * host that is not this machine's) or any error answers nothing, and no join is claimed.
 */
export const useOpinions = (enabled: boolean) => useQuery({
  queryKey: keys.opinions,
  queryFn: ({ signal }) => api.opinions(signal),
  enabled,
  retry: false,
});

/**
 * Ask Daoris's conversations, across every workspace: it belongs to none, so the scope a
 * person is working in must not hide the conversation beside it.
 */
export const useHelpSessions = (
  /**
   * How often to ask again while a conversation the person wrote to is still to go on: its record moves to working
   * as the driver takes the words up, which a desktop window is told as `sessions`, so the caller gives it only
   * to a window with no bus. False asks only when the list is stale.
   */
  refetchInterval: number | false = false,
  /** False asks nothing: a reader that needs a conversation's record only once there is one to read (the proposal bar). */
  enabled = true,
) => {
  // Pushed in the shell: its record's every move is pushed, so it is never asked again on a clock.
  const { isAvailable } = useShenora();
  const { read } = useSessionsRead(HELP_REPOSITORY, true, null);
  return useQuery({ ...read, refetchInterval: isAvailable ? false : refetchInterval, enabled });
};

/**
 * How a view asks a knowledge read: only while it is in front, since the application holds Search and
 * Convergence on every view; and, where a new question replaces the last, holding the last answer while the new one
 * is on its way, so the list is never blanked between two answers.
 */
export type KnowledgeAsk = { enabled?: boolean; holding?: boolean };

/** One document, read on demand — an entry's page, cached like every other read. */
export const useEntry = (id: string | null, enabled = true) =>
  useQuery({
    queryKey: keys.entry(id ?? ''),
    queryFn: ({ signal }) => api.entry(id ?? '', signal),
    enabled: enabled && id !== null,
  });

/** Several documents, each read on demand and cached as one entry: a finding's entries, read whole. */
export const useEntries = (ids: readonly string[], enabled = true) =>
  useQueries({
    queries: ids.map((id) => ({
      queryKey: keys.entry(id),
      queryFn: ({ signal }: { signal: AbortSignal }) => api.entry(id, signal),
      enabled,
    })),
  });

export const useConvergence = (minimumSimilarity: number, { enabled = true, holding = false }: KnowledgeAsk = {}) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.convergence(minimumSimilarity, workspace),
    queryFn: ({ signal }) => api.convergence(minimumSimilarity, workspace, signal),
    enabled,
    placeholderData: holding ? keepPreviousData : undefined,
  });
};

export const useSearch = (q: string, localOnly: boolean, { enabled = true, holding = false }: KnowledgeAsk = {}) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.search(q, localOnly, workspace),
    queryFn: ({ signal }) => api.search(q, localOnly, workspace, signal),
    enabled: enabled && q.length >= 2,
    placeholderData: holding ? keepPreviousData : undefined,
  });
};

/**
 * Everything a quest mutation can change: every quests query, the registry's counts, and the asks — an
 * ask is done once every quest it became has closed, which the service derives on each read, so
 * closing the last one changes what the asks list says. Where the shell pushes the quests and the asks, the change is heard
 * on the service's change stream and pushed, and only what is read beneath them is asked again.
 */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    void client.invalidateQueries({ queryKey: keys.allAsks, predicate: notPushed(client) });
  };
}

export const usePublishQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: (draft: Parameters<typeof api.publishQuest>[0]) => api.publishQuest(draft),
    onSuccess: invalidate,
  });
};

export const useRespondQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: 'take' | 'done' | 'decline'; reason: string | null }) =>
      api.respondQuest(id, action, reason),
    onSuccess: invalidate,
  });
};

/**
 * The person's yes to a done's departure from what they required: the service publishes the step the
 * departure held and a quest waiting on it goes on, so the quests and the asks are read again, as a respond's are.
 */
export const useAcceptQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({ mutationFn: (id: string) => api.acceptQuest(id), onSuccess: invalidate });
};

/**
 * The person's done: their words to the service's own door for it, from the quest's page and from
 * a finish at a checkpoint. A chain's next step may be published and the ask it came from may be done now, so the quests and
 * the asks are read again, as a respond's are.
 */
/**
 * What a review's press moves: the quests (a held step lets go, a set-up step is published), the asks (the
 * person's words are kept there), and every landing's plan, whose gate it answers, with the sessions' groups that
 * read the same gate. Asked again whatever the door said: a stale verdict is refused because the record moved.
 */
function useReviewChange<TVariables>(fn: (variables: TVariables) => Promise<QuestAction>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      // The quests and the asks, where the shell pushes them: the change stream tells their owner.
      void client.invalidateQueries({ queryKey: keys.allQuests, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allAsks, predicate: notPushed(client) });
      void client.invalidateQueries({ queryKey: keys.allLandings });
      // What is read under the sessions' key, but the records where the shell pushes them: their pushes write them.
      void client.invalidateQueries({ queryKey: keys.allSessions, predicate: notPushed(client) });
    },
  });
}

/**
 * The person's verdict on a review: `reviewed` or `not-yet` on the set-up the view drew, or a skip
 * of the review of a quest's work. The host refuses one whose set-up a newer one follows, in its sentence.
 */
export const useReviewVerdict = () => useReviewChange(
  ({ id, verdict, words, setUp }: { id: string; verdict: Verdict; words?: string | null; setUp?: SetUpRef | null }) =>
    api.reviewQuest(id, verdict, words, setUp));

/** The person's *Set it up in `<environment>`*: a set-up step following a done quest. */
export const usePublishSetUpStep = () => useReviewChange(
  ({ id, environment }: { id: string; environment: string }) => api.setUpStep(id, environment));

/** The person's review choice on an ask, or its intake's proposal applied. */
export const useChooseAskWorkflow = () =>
  useAskChange(({ id, kind, workflow }: { id: string; kind: string; workflow?: string | null }) => api.chooseAskWorkflow(id, kind, workflow));
export const useChooseAskReview = () =>
  useAskChange(({ id, choice, words }: { id: string; choice: string; words?: string }) => api.chooseAskReview(id, choice, words));

/**
 * The person's reopen of a sealed submission: the step it published is withdrawn or held, so the quests
 * and the asks are read again, as a respond's are.
 */
export const useReopenQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, submission, note }: { id: string; submission: SubmissionRef; note: string | null }) =>
      api.reopenQuest(id, submission, note),
    onSuccess: invalidate,
  });
};

/** The person's word to go on with a step its source's reopen held: its hold goes, and the driver carries it on. */
export const useContinueQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({ mutationFn: (id: string) => api.continueQuest(id), onSuccess: invalidate });
};

export const usePersonDone = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string | null }) => api.personDone(id, note),
    onSuccess: invalidate,
  });
};

/**
 * Retire a machine the circle's remote still counts, and read where the circle stands again where it is pulled. Where
 * the shell pushes the standings, the retirement moves no family of the change stream: the caller's pass (*Sync now*'s) pushes it.
 */
export const useRetireMachine = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ workspace, machine }: { workspace: string; machine: string }) => api.retireMachine(workspace, machine),
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
};

/** Dismiss one conflict — and where each circle stands moves with it. */
export const useDismissConflict = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, machine, sequence }: { id: string; machine: string; sequence: number }) =>
      api.dismissConflict(id, machine, sequence),
    onSuccess: () => {
      invalidate();
      // Where the shell pushes the standings, the dismissal's quest write is heard on the change stream and pushed.
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
};

/**
 * Bring this machine's lost take forward, invalidating as a dismissal does. Read again on a refusal too:
 * a bring-forward that loses again is refused, and it leaves the quest a conflict it did not have (the fresh take's).
 */
export const useForwardConflict = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, machine, sequence }: { id: string; machine: string; sequence: number }) =>
      api.forwardConflict(id, machine, sequence),
    onSettled: () => {
      invalidate();
      void client.invalidateQueries({ queryKey: keys.allSync, predicate: notPushed(client) });
    },
  });
};

/** What a re-scan can change: the index and what reads it — never a quest, a session, or the tick's own answers. */
const FED_BY_THE_INDEX = new Set<unknown>([
  keys.status[0], keys.allRepositories[0], keys.allRegistry[0], 'code-map', 'entry', 'convergence', 'search',
]);

export const useRefreshIndex = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => api.refresh(),
    // 🔴 Only what the index feeds. Invalidating everything would take the tick-written answers
    // too, whose "fetch" is an empty list, so each refresh would wipe why a quest is sitting and the trust
    // holds until the next tick, and run `git` again for an open review.
    onSuccess: () => void client.invalidateQueries({ predicate: (query) => FED_BY_THE_INDEX.has(query.queryKey[0]) }),
  });
};

/**
 * The registration lifecycle.
 *
 * A registration change moves who is on the map, what each is wired to, and what the index will read
 * next — so all three invalidate the same broad set rather than each guessing which views care.
 */
function useInvalidateRegistry() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    void client.invalidateQueries({ queryKey: keys.allRepositories });
  };
}

/**
 * Import a folder's repositories (*Import a folder…*), into a named workspace when one is given,
 * then re-read what the registry holds.
 */
export const useImportFolder = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: ({ folder, workspace }: { folder: string; workspace?: string }) => api.importFolder(folder, workspace),
    onSuccess: invalidate,
  });
};

export const useRegisterRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: (body: Parameters<typeof api.registerRepository>[0]) => api.registerRepository(body),
    onSuccess: invalidate,
  });
};

export const useWireRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: ({ repository, workspace }: { repository: string; workspace: string }) =>
      api.wireRepository(repository, workspace),
    onSuccess: invalidate,
  });
};

export const useRetireRepository = () => {
  const invalidate = useInvalidateRegistry();
  return useMutation({
    mutationFn: (repository: string) => api.retireRepository(repository),
    onSuccess: invalidate,
  });
};
