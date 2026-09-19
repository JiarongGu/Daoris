import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './api';

// The console is a cache over a service; TanStack Query is that cache done correctly (D42):
// deduplicated requests (the badge and the Quests view share one fetch), refetch on focus, and
// invalidation after every mutation instead of hand-rolled reload calls that each view remembered —
// or forgot — to make.

export const keys = {
  status: ['status'] as const,
  repositories: ['repositories'] as const,
  registry: ['registry'] as const,
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

/** Everything a quest mutation can change: every quests query, and the registry's counts. */
function useInvalidateQuestWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: ['quests'] });
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
    mutationFn: () => api.refresh() as Promise<{ entries: number; repositories: number }>,
    // A re-scan can change anything the index feeds.
    onSuccess: () => void client.invalidateQueries(),
  });
};
