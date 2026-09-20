import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';

// The console is a cache over a service; TanStack Query is that cache done correctly (D42):
// deduplicated requests (the badge and the Quests view share one fetch), refetch on focus, and
// invalidation after every mutation instead of hand-rolled reload calls that each view remembered —
// or forgot — to make.

// The single source of query-key truth — invalidations included. A prefix written as a string
// literal at an invalidation site survives a rename silently; one written here does not.
export const keys = {
  status: ['status'] as const,
  repositories: ['repositories'] as const,
  registry: ['registry'] as const,
  driver: ['driver'] as const,
  /** The machine's wiring — shell-only, like the driver's state (D48 §5). */
  remotes: ['remotes'] as const,
  entry: (id: string) => ['entry', id] as const,
  convergence: (minimumSimilarity: number) => ['convergence', minimumSimilarity] as const,
  search: (q: string, localOnly: boolean) => ['search', q, localOnly] as const,
  allQuests: ['quests'] as const,
  allSessions: ['sessions'] as const,
  quests: (repository: string | null, includeClosed: boolean) =>
    ['quests', repository ?? 'all', includeClosed] as const,
  sessions: (repository: string | null, includeClosed: boolean) =>
    ['sessions', repository ?? 'all', includeClosed] as const,
};

export const useStatus = () =>
  useQuery({ queryKey: keys.status, queryFn: ({ signal }) => api.status(signal) });

export const useRepositories = () =>
  useQuery({ queryKey: keys.repositories, queryFn: ({ signal }) => api.repositories(signal) });

export const useRegistry = () =>
  useQuery({ queryKey: keys.registry, queryFn: ({ signal }) => api.registry(signal) });

export const useQuests = (repository: string | null, includeClosed: boolean) =>
  useQuery({
    queryKey: keys.quests(repository, includeClosed),
    queryFn: ({ signal }) => api.quests(repository, includeClosed, signal),
  });

/** Session records are read-only here: the controls act where a driver is attached (D46 §6). */
export const useSessions = (repository: string | null, includeClosed: boolean) =>
  useQuery({
    queryKey: keys.sessions(repository, includeClosed),
    queryFn: ({ signal }) => api.sessions(repository, includeClosed, signal),
  });

/** One document, read on demand — the Reader's fetch, cached like every other read. */
export const useEntry = (id: string | null) =>
  useQuery({
    queryKey: keys.entry(id ?? ''),
    queryFn: ({ signal }) => api.entry(id ?? '', signal),
    enabled: id !== null,
  });

export const useConvergence = (minimumSimilarity: number) =>
  useQuery({
    queryKey: keys.convergence(minimumSimilarity),
    queryFn: ({ signal }) => api.convergence(minimumSimilarity, signal),
  });

export const useSearch = (q: string, localOnly: boolean) =>
  useQuery({
    queryKey: keys.search(q, localOnly),
    queryFn: ({ signal }) => api.search(q, localOnly, signal),
    enabled: q.length >= 2,
  });

/** Everything a quest mutation can change: every quests query, and the registry's counts. */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.registry });
  };
}

export const usePublishQuest = () => {
  const invalidate = useInvalidateQuestWork();
  return useMutation({
    mutationFn: (draft: { from: string; to: string; title: string; body: string }) =>
      api.publishQuest(draft),
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
    void client.invalidateQueries({ queryKey: keys.registry });
    void client.invalidateQueries({ queryKey: keys.repositories });
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
