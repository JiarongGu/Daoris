import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { PlaceApplied, PlaceApplyAsk, PlaceAsk, PlacePlanned } from '../../agents/setup/placePlan';
import type { SetupPressed, SetupShown } from '../../projects/setup/SetUpForAgents';
import { call } from '../call';

// A repository's *Set up for agents* (LAYOUT8, D117 §6.1, §6.5; D170 point 1): the press's plan, the twin of
// `daoris-driver setup <repository> --plan`, and the press. The driver library's `SetupPress` answers both, which the
// terminal and Ask Daoris's `setup` card call too (D50).

/** The shapes the routes answer live with the molecule that draws them, since a molecule may read a shape and never a bridge domain. */
export type { SetupPressed, SetupShown } from '../../projects/setup/SetUpForAgents';

/**
 * The plan reads the line with git and asks the `node` and the `daoris` a child finds for their versions, each within 15
 * seconds (`SetupWorld.Patience`), then the service's doors: past the bridge's default 30 seconds on a loaded machine, so the
 * page waits two minutes, as long as the press may take, which reads it again.
 */
const SETUP_BOUND = 2 * 60_000;

/**
 * Under its own root: a plan changes by a press, a line moved or a tool installed, never by a tick, so the hub asks it nothing.
 * It is read again each time the person asks to see it, the last answer shown meanwhile.
 */
export const setupKey = (repository: string) => ['setup', repository] as const;

/**
 * What a press would do (`SETUP_PLAN`): what was read on the line, every refusal with its code, the rule it adds, how its
 * work lands, the agent that carries it and the quest's whole text. Asked only once the person asks to see it, since it
 * starts git and two programs. Shell-only: a browser has no driver (D47 §4).
 */
export const useSetupPlan = (repository: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: repository ? setupKey(repository) : ['setup', 'none'],
    queryFn: () => call<SetupShown>('SETUP_PLAN', { repository: repository! }, { timeoutMs: SETUP_BOUND }),
    enabled: isAvailable && enabled && repository !== null,
    refetchOnWindowFocus: false,
  });
  return {
    plan: query.data ?? null,
    /** Reading, with no answer yet. */
    reading: query.isFetching,
    error: query.error,
    /** Read it again: the person's *Look again*, after fixing what it refused. */
    again: () => void query.refetch(),
  };
};

/**
 * The press (`SETUP_PRESS`): the plan read again now, then the rule into the repository's scope and one ask to it, as the
 * person's. A refusal is an answer, with nothing published or added. What it moved, the asks, the quests and the rules, the
 * route's `STATE_CHANGED` asks again in every window (D169).
 */
export const useSetupPress = () =>
  useMutation({
    mutationFn: (repository: string) => call<SetupPressed>('SETUP_PRESS', { repository }, { timeoutMs: SETUP_BOUND }),
  });

/** Under their own root, like `setupKey`: a place's plan changes by a press or an ask, never by a tick. */
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
