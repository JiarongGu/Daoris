import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { PlaceApplied, PlaceApplyAsk, PlaceAsk, PlacePlanned } from '../../agents/setup/placePlan';
import { call } from '../call';

// A place's quick set-up, the one door that adopts a repository (WORKFLOWCLEAN1 1f): the plan, the twin of
// `daoris-driver place … --plan`, and the apply, the twin of `--apply`. The driver library's `PlacePlans` and `PlaceApplies` answer both,
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
 * and after, every refusal at once, the reach, the work driving releases and each declaration, the twin of `daoris-driver place … --plan`.
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
