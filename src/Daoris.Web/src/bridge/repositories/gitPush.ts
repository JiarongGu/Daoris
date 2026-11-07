import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../../data/queries';
import { call } from '../call';
import { askHeld } from '../state/held';
import { readPlanOf, type GitRefAnswer } from './git';

// The person's push of one ref to origin: the one push Daoris makes, an act of the driver's journal
// under a grant bound to origin's push address, the ref, its old commit and its new one.

/**
 * Whether *Push* and *Push tag* are offered on a repository's Branches: the driver's git doors installed, an origin with one push address,
 * none of its settings that run another program. `reason` says why not in a word (`doors`, `no-origin`, `widened`, `unread`); `address` is
 * origin's push address as the driver redacts it; `dangerous` is the place's Dangerous value (`allow` or `deny`), which decides a tag at the
 * door whatever the press; `command` is what a person types, its ref left as `<commit>:<ref>`.
 */
export type GitPushPlan = { offered: boolean; reason: string | null; why: string | null; address: string | null; dangerous: string; command: string };

/** What `GIT_PUSH` answers: a ref act's answer, and in `notes` what the push step said where it did not finish. */
export type GitPushAnswer = GitRefAnswer & { notes?: string | null };

const readPushPlan = readPlanOf<GitPushPlan>((value) => ({
  offered: value.offered === true, reason: value.reason ?? null, why: value.why ?? null, address: value.address ?? null,
  dangerous: value.dangerous ?? 'deny', command: value.command ?? '',
}));

/** The push's plan, read when the Branches tab opens and again after a push. */
export const useGitPushPlan = (repository: string | null, workspace: string) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = [...keys.gitChanges(repository ?? '', workspace), 'push-plan'] as const;
  return useQuery({
    queryKey,
    queryFn: () => askHeld('GIT_PUSH_PLAN', client, queryKey, { repository, ...(workspace ? { workspace } : {}) }, readPushPlan),
    enabled: isAvailable && Boolean(repository),
    staleTime: 0,
    refetchOnMount: 'always',
  });
};

/** *Push* a branch or *Push tag* at the commit the page showed; the driver refuses a ref that has moved. A push has no Undo. */
export const useGitPush = (repository: string, workspace: string) => {
  const client = useQueryClient();
  const push = useMutation({
    mutationFn: (ask: { kind: 'branch' | 'tag'; name: string; at: string }) =>
      call('GIT_PUSH', { repository, ...(workspace ? { workspace } : {}), ...ask }) as Promise<GitPushAnswer>,
    onSuccess: () => client.invalidateQueries({ queryKey: ['git'] }),
  });
  return push.mutateAsync;
};
