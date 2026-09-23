import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type AskAction } from './api';
import { useScope } from './scope';

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
  /** This machine's harnesses and the accounts they run as — shell-only too (D49 §4). */
  harnesses: ['harnesses'] as const,
  // Under the roster's key, so everything that invalidates the roster asks the wiring again: an
  // account added, removed or chosen changes what a start would take (MAP1b).
  starts: (workspaces: string[]) => ['harnesses', 'starts', ...workspaces] as const,
  /** What sessions consumed — shell-only, because per-account usage names a profile (TOOL3). */
  usage: ['usage'] as const,
  /** This machine's plugins — shell-only, since a plugin's folder is a machine path (D64). */
  plugins: ['plugins'] as const,
  /** One session's landed work, read off the checkout — shell-only for the console's reason (SURF6). */
  diff: (session: string) => ['diff', session] as const,
  entry: (id: string) => ['entry', id] as const,
  convergence: (minimumSimilarity: number, workspace: string | null) =>
    ['convergence', minimumSimilarity, workspace ?? '*'] as const,
  search: (q: string, localOnly: boolean, workspace: string | null) =>
    ['search', q, localOnly, workspace ?? '*'] as const,
  allQuests: ['quests'] as const,
  allSessions: ['sessions'] as const,
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

export const useRegistry = () => {
  const { workspace } = useScope();
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
    select: (rows) => [...new Set(rows.map((row) => row.workspace ?? 'default'))].sort(),
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

/** Everything a quest mutation can change: every quests query, and the registry's counts. */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.allRegistry });
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

export const useRefreshIndex = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => api.refresh(),
    // A re-scan can change anything the index feeds.
    onSuccess: () => void client.invalidateQueries(),
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
