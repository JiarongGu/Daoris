import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { PlaceApplied, PlaceApplyAsk, PlaceAsk, PlacePlanned } from '../../agents/setup/placePlan';
import { type ReconcileApplied, readReconcilePlan } from '../../agents/setup/reconcilePlan';
import { call } from '../call';
import { askHeld } from '../state/held';

export type { ReconcileApplied, ReconcilePlanned } from '../../agents/setup/reconcilePlan';

// A place's quick set-up, the one door that adopts a repository (WORKFLOWCLEAN1 1f): the plan, the twin of
// `daoris place … --plan`, and the apply, the twin of `--apply`. The driver library's `PlacePlans` and `PlaceApplies` answer both,
// which the terminal and Ask Daoris's `setup_propose` card call too (D50).

/**
 * The plan reads each line with git as an object and the service's registry and quests: past the bridge's default 30 seconds on a loaded
 * machine, so the page waits two minutes, as long as the apply may take, which reads it again.
 */
const SETUP_BOUND = 2 * 60_000;

/** Under their own root: a place's plan changes by a press or an ask, never by a tick. */
export const placePlanKey = (ask: PlaceAsk) => ['place-plan', ask] as const;

/**
 * A place's plan (`PLACE_PLAN`, AGENTSPLACE1j; the Agents-place design §4): the parts applied to a copy of the configuration and read before
 * and after, every refusal at once, the reach, the work driving releases and each declaration, the twin of `daoris place … --plan`.
 * It writes and probes nothing. The last answer stays shown while the next is read (`stale`), so a tick does not blank the page; it is read
 * again each time it is looked at, since a line or a rule may have moved. Shell-only: a browser has no driver (D47 §4).
 */
export const usePlacePlan = (ask: PlaceAsk | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: ask ? placePlanKey(ask) : ['place-plan', 'none'],
    queryFn: () => call<PlacePlanned>('PLACE_PLAN', ask!, { timeoutMs: SETUP_BOUND }),
    enabled: isAvailable && enabled && ask !== null,
    refetchOnWindowFocus: false,
    staleTime: 0,
    placeholderData: keepPreviousData,
  });
  return {
    plan: query.data ?? null,
    reading: query.isFetching,
    /** The plan shown answers an earlier ask than the one made. */
    stale: query.isPlaceholderData,
    error: query.error,
    again: () => void query.refetch(),
  };
};

/**
 * The apply (`PLACE_APPLY`, the person's own press, AGENTSPLACE1b3): the plan's ask, its digest and the acceptances chosen. A refusal is an
 * answer with nothing written, and a stale digest is refused `moved`. The plans are read again after it, since a value, a quest and an
 * acceptance each changed; what it moved, the route's `STATE_CHANGED` asks again in every window (D169).
 */
export const usePlaceApply = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (ask: PlaceApplyAsk) => call<PlaceApplied>('PLACE_APPLY', ask, { timeoutMs: SETUP_BOUND }),
    onSettled: () => client.invalidateQueries({ queryKey: ['place-plan'] }),
  });
};

/** Under its own root, as a place's plan is: a reconcile's plan changes by its press or the repository's own sync, never by a tick. */
export const reconcilePlanKey = (repository: string) => ['reconcile-plan', repository] as const;

/**
 * A repository's reconcile planned (`RECONCILE_PLAN`, D230 point 2): what the doctrine tool's status said of its checkout, whether that offers
 * the press, and the set-up press's own refusals. Told by event (NOBLOCK1), since it runs the doctrine tool; read each time the place's page
 * shows it, an older shell's none read as nothing offered. Shell-only.
 */
export const useReconcilePlan = (repository: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: repository ? reconcilePlanKey(repository) : ['reconcile-plan', 'none'],
    queryFn: () => askHeld('RECONCILE_PLAN', client, reconcilePlanKey(repository!), { repository: repository! }, readReconcilePlan),
    enabled: isAvailable && enabled && repository !== null && repository !== '',
    refetchOnWindowFocus: false,
    staleTime: 0,
    retry: false,
  });
};

/**
 * The reconcile's press (`RECONCILE_APPLY`, the person's own): the repository's own session asked to reconcile it, its rule added first. A
 * refusal is an answer with nothing asked. The plan is read again after it; what it moved, the route's `STATE_CHANGED` asks again (D169).
 */
export const useReconcileApply = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (repository: string) => call<ReconcileApplied>('RECONCILE_APPLY', { repository }, { timeoutMs: SETUP_BOUND }),
    onSettled: () => client.invalidateQueries({ queryKey: ['reconcile-plan'] }),
  });
};
