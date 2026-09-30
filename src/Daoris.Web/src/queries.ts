import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type AskAction, HELP_REPOSITORY } from './api';
import { useScope } from './scope';
import { workspaceOf, workspacesOf } from './workspaces';

// The console is a cache over a service; TanStack Query is that cache done correctly (D42):
// deduplicated requests (the badge and the Quests view share one fetch), refetch on focus, and
// invalidation after every mutation instead of hand-rolled reload calls that each view remembered —
// or forgot — to make.

// The single source of query-key truth — invalidations included. A prefix written as a string
// literal at an invalidation site survives a rename silently; one written here does not.
// Every cross-repository key carries the workspace scope (WSP5; D48 §4), so a circle's answers never
// serve another's from the cache. `*` is "every circle" — the door's own meaning of an absent argument.
// The `all*` prefixes are what invalidation addresses: whatever the scope, a change moves them all.
export const keys = {
  status: ['status'] as const,
  /** The driver's last verdict per open quest — written by the tick, never fetched (its own root, so no invalidation reaches it). */
  considered: ['considered'] as const,
  /** The starts the last tick held for the agent's trust (D73) — written by the tick, like `considered`. */
  untrusted: ['untrusted'] as const,
  /** Ask Daoris's conversation opened ahead of the person (HELP5) — kept by the page, never fetched. */
  helpReadied: ['help-readied'] as const,
  allRepositories: ['repositories'] as const,
  repositories: (workspace: string | null) => ['repositories', workspace ?? '*'] as const,
  allRegistry: ['registry'] as const,
  registry: (workspace: string | null) => ['registry', workspace ?? '*'] as const,
  codeMap: (repository: string) => ['code-map', repository] as const,
  /** Where each circle stands with its remote (SYNC6a) — refetched by every tick, which runs a pass. */
  allSync: ['sync'] as const,
  sync: (workspace: string) => ['sync', workspace] as const,
  driver: ['driver'] as const,
  /** The machine's wiring — shell-only, like the driver's state (D48 §5). */
  remotes: ['remotes'] as const,
  browserSettings: ['browser-settings'] as const,
  lines: ['driver', 'lines'] as const,
  /** How each repository's checkout stands for reading and writing across (D107) — shell-only, like the lines. */
  across: ['driver', 'across'] as const,
  allLandings: ['driver', 'landing'] as const,
  sweep: ['driver', 'sweep'] as const,
  landing: (session: string) => ['driver', 'landing', session] as const,
  /** Whether a session's landed branch can be handed to a landing plugin now (WSR5b). */
  handOff: (session: string) => ['driver', 'hand', session] as const,
  /** This machine's harnesses and the accounts they run as — shell-only too (D49 §4). */
  harnesses: ['harnesses'] as const,
  // Under the roster's key, so everything that invalidates the roster asks the wiring again: an
  // account added, removed or chosen changes what a start would take (MAP1b).
  allStarts: ['harnesses', 'starts'] as const,
  starts: (workspaces: string[]) => ['harnesses', 'starts', ...workspaces] as const,
  /** What sessions consumed — shell-only, because per-account usage names a profile (TOOL3). */
  usage: ['usage'] as const,
  /** This machine's plugins — shell-only, since a plugin's folder is a machine path (D64). */
  plugins: ['plugins'] as const,
  /** The machine log read back (LOG1c) — shell-only: the log never leaves the machine (D94). */
  allMachineLog: ['machine-log'] as const,
  machineLog: (since: string, source: string, event: string, level: string) =>
    ['machine-log', since, source, event, level] as const,
  rules: ['rules'] as const,
  /** One session's landed work, read off the checkout — shell-only for the console's reason (SURF6). */
  diff: (session: string) => ['diff', session] as const,
  /** The files in one session's tree, for `@` (CONV4d) — shell-only for the same reason. */
  treeFiles: (session: string) => ['tree-files', session] as const,
  /** One file in a session's tree, read for its preview (PREVIEW1) — shell-only for the same reason. */
  treeFile: (session: string, path: string) => ['tree-file', session, path] as const,
  /** One conversation's model and effort as its agent offers them (AGT6b) — shell-only: it is a live process's. */
  sessionOptions: (session: string) => ['session-options', session] as const,
  /** What the person first said in each session, from this machine's record (RAIL1) — shell-only. */
  allOpenings: ['openings'] as const,
  openings: (ids: string[]) => ['openings', ...ids] as const,
  /** What sessions said, searched on this machine's record (RAIL1) — shell-only. */
  sessionSearch: (q: string, session?: string) => ['session-search', q, session ?? ''] as const,
  entry: (id: string) => ['entry', id] as const,
  convergence: (minimumSimilarity: number, workspace: string | null) =>
    ['convergence', minimumSimilarity, workspace ?? '*'] as const,
  search: (q: string, localOnly: boolean, workspace: string | null) =>
    ['search', q, localOnly, workspace ?? '*'] as const,
  allQuests: ['quests'] as const,
  allSessions: ['sessions'] as const,
  /**
   * Where each of these sessions' work is now, its tree or its landing (LOOK2b) — shell-only, read off this machine's
   * files. Under the sessions' key, so whatever asks the listing again (a landing, a clean-up, a tick) asks this too.
   */
  sessionsWhere: (ids: string[]) => ['sessions', 'where', ...ids] as const,
  allAsks: ['asks'] as const,
  asks: (includeClosed: boolean, workspace: string | null) => ['asks', includeClosed, workspace ?? '*'] as const,
  quests: (repository: string | null, includeClosed: boolean, workspace: string | null) =>
    ['quests', repository ?? 'all', includeClosed, workspace ?? '*'] as const,
  sessions: (repository: string | null, includeClosed: boolean, workspace: string | null) =>
    ['sessions', repository ?? 'all', includeClosed, workspace ?? '*'] as const,
};

export const useStatus = () =>
  useQuery({ queryKey: keys.status, queryFn: ({ signal }) => api.status(signal) });

/**
 * Where one circle stands with its remote (SYNC6a): ahead, behind, in conflict, when it last synced.
 * Over HTTP from this machine's own host, so a browser on this machine reads it too — only *Sync now*
 * needs the shell. Null asks nothing: "every circle" has no single standing to show.
 */
export const useSyncStanding = (workspace: string | null) =>
  useQuery({
    queryKey: keys.sync(workspace ?? '*'),
    queryFn: ({ signal }) => api.syncStanding(workspace!, signal),
    enabled: workspace !== null,
  });

export const useRepositories = () => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.repositories(workspace),
    queryFn: ({ signal }) => api.repositories(workspace, signal),
  });
};

/**
 * The registry — the window's scope by default, or the whole machine's for a choice that is the
 * machine's whatever the window shows: Settings (REV3). Scoped, a window on one circle offered a rule,
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

/** A repository's code map (MAP3a) — asked only once a person opens one. */
export const useCodeMap = (repository: string | null) =>
  useQuery({
    queryKey: keys.codeMap(repository ?? ''),
    queryFn: ({ signal }) => api.codeMap(repository!, signal),
    enabled: repository !== null,
  });

/**
 * The circles this deployment holds — the registry unscoped, because it is the authority (D48 §3)
 * and the one reader that must see every workspace is the switcher itself. A row from a host older
 * than workspaces names none and counts as `default`, so one such family stays one circle.
 */
export const useWorkspaces = () =>
  useQuery({
    queryKey: keys.registry(null),
    queryFn: ({ signal }) => api.registry(null, signal),
    select: (rows) => workspacesOf(rows),
  });

/**
 * Every workspace with how many repositories it holds, for the Workspace menu (D75). The same
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

/** Asks (D65 §1a), scoped like every other cross-repository read — an ask is made in a circle. */
export const useAsks = (includeClosed: boolean) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.asks(includeClosed, workspace),
    queryFn: ({ signal }) => api.asks(includeClosed, workspace, signal),
  });
};

/**
 * Asking, publishing an ask and closing one. Each can publish a quest, so the quests move with the
 * asks — and they are asked again whether or not the door said yes: a named receiver that refused
 * still leaves the ask kept, with its proposal (INT4a), which the list must then show.
 */
function useAskChange<TVariables>(fn: (variables: TVariables) => Promise<AskAction>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allQuests });
    },
  });
}

/**
 * Deleting a record made by mistake (D95): the quests, the asks and where each circle stands all move
 * with it — a delete of an ask takes its quests, and a shared quest's delete is one more thing ahead.
 * Asked again whatever the door said, since a refused delete may still have found the record moved.
 */
function useDelete(fn: (id: string) => ReturnType<typeof api.deleteQuest>) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allRegistry });
      void client.invalidateQueries({ queryKey: keys.allSync });
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

export const useQuests = (repository: string | null, includeClosed: boolean) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.quests(repository, includeClosed, workspace),
    queryFn: ({ signal }) => api.quests(repository, includeClosed, workspace, signal),
  });
};

/** Session records are read-only here: the controls act where a driver is attached (D46 §6). */
export const useSessions = (repository: string | null, includeClosed: boolean) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.sessions(repository, includeClosed, workspace),
    queryFn: ({ signal }) => api.sessions(repository, includeClosed, workspace, signal),
  });
};

/**
 * Ask Daoris's conversations (HELP1a, D89), across every workspace: it belongs to none, so the scope a
 * person is working in must not hide the conversation beside it.
 */
export const useHelpSessions = () =>
  useQuery({
    queryKey: keys.sessions(HELP_REPOSITORY, true, null),
    queryFn: ({ signal }) => api.sessions(HELP_REPOSITORY, true, null, signal),
  });

/** One document, read on demand — the Reader's fetch, cached like every other read. */
export const useEntry = (id: string | null) =>
  useQuery({
    queryKey: keys.entry(id ?? ''),
    queryFn: ({ signal }) => api.entry(id ?? '', signal),
    enabled: id !== null,
  });

export const useConvergence = (minimumSimilarity: number) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.convergence(minimumSimilarity, workspace),
    queryFn: ({ signal }) => api.convergence(minimumSimilarity, workspace, signal),
  });
};

export const useSearch = (q: string, localOnly: boolean) => {
  const { workspace } = useScope();
  return useQuery({
    queryKey: keys.search(q, localOnly, workspace),
    queryFn: ({ signal }) => api.search(q, localOnly, workspace, signal),
    enabled: q.length >= 2,
  });
};

/**
 * Everything a quest mutation can change: every quests query, the registry's counts, and the asks — an
 * ask is done once every quest it became has closed (USE1c), which the service derives on each read, so
 * closing the last one changes what the asks list says.
 */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.allRegistry });
    void client.invalidateQueries({ queryKey: keys.allAsks });
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
 * Answer a driven session that parked to ask the person (STANDDOWN2): its record ends with their
 * words and the quest it holds is carried on in the same tree at the driver's next tick.
 */
export const useAnswerSession = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, answer }: { id: string; answer: string | null }) => api.answerSession(id, answer),
    onSuccess: () => {
      invalidate();
      void client.invalidateQueries({ queryKey: keys.allSessions });
    },
  });
};

/** Dismiss one conflict (SYNC6c) — and where each circle stands moves with it. */
export const useDismissConflict = () => {
  const invalidate = useInvalidateQuestWork();
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, machine, sequence }: { id: string; machine: string; sequence: number }) =>
      api.dismissConflict(id, machine, sequence),
    onSuccess: () => {
      invalidate();
      void client.invalidateQueries({ queryKey: keys.allSync });
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
    // 🔴 Only what the index feeds (REV3). Everything was invalidated once — the tick-written answers
    // too, whose "fetch" is an empty list, so each refresh wiped why a quest is sitting and the trust
    // holds until the next tick, and ran `git` again for an open review.
    onSuccess: () => void client.invalidateQueries({ predicate: (query) => FED_BY_THE_INDEX.has(query.queryKey[0]) }),
  });
};

/**
 * The registration lifecycle (D48 §3/§7).
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
 * Import a folder's repositories (D75's *Import a folder…*), into a named workspace when one is given
 * (D77), then re-read what the registry holds.
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
