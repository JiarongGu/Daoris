import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { PlaceCastBridge, PlaceCastChange, PlaceCastView } from '../../settings/place/PlaceCast';
import type { StepHoldAnswer, StepHoldAsk, StepHoldBridge } from '../../workflow/wait/StepHold';
import { call } from '../call';
import { askHeld } from '../state/held';

// A place's cast and a step's holder, over the records `daoris settings cast` and `daoris workflow take|hand|give-back` edit: each press the
// owner's, an agent's of the same route on the owner's card in every mode. A refusal is the driver's sentence, verbatim.

const castKey = (workspace?: string | null, repository?: string | null) => ['PLACE_CAST', workspace ?? '', repository ?? ''] as const;

/** The cast an answer holds, or none while the shell holds nothing yet: its event brings the cast. */
const readPlaceCast = (answer: unknown): PlaceCastView | null =>
  typeof answer === 'object' && answer !== null && Array.isArray((answer as { roles?: unknown }).roles) ? (answer as PlaceCastView) : null;

/**
 * A place's cast as its Settings draw it: each role's holder now and why, the risk and marks, the roster and the reporting line. The shell tells
 * it by event (`PLACE_CAST`), so the page shows none until the first is held.
 */
export const usePlaceCast = (workspace?: string | null, repository?: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = castKey(workspace, repository);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('PLACE_CAST', client, queryKey, { workspace: workspace ?? null, repository: repository ?? null }, readPlaceCast),
    select: (view) => view ?? undefined,
    enabled: isAvailable && enabled,
  });
};

/** The owner's cast of a role at a place, journaled with its reason; answers the place's cast, kept as the read's. */
export const useSetPlaceCast = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: PlaceCastChange) => call('SET_PLACE_CAST', change) as Promise<PlaceCastView>,
    onSuccess: (view, change) => client.setQueryData(castKey(change.workspace, change.repository), view),
  });
};

/** A step taken, handed to another or given back: a new epoch of its tenure, its refusal an answer with its code, never thrown. */
export const useStepHold = () => useMutation({
  mutationKey: ['WORKFLOW_HOLD'],
  mutationFn: async (ask: StepHoldAsk): Promise<StepHoldAnswer> => {
    const answer = (await call('WORKFLOW_HOLD', ask)) as Partial<StepHoldAnswer> | null;
    return { done: answer?.done === true, refused: answer?.refused ?? null, message: answer?.message ?? null };
  },
});

/** The calls a place's cast section is handed, so it holds no bridge of its own: held here, handed down by the organisms that hold the bridge. */
export const placeCastBridge: PlaceCastBridge = { useCast: usePlaceCast, useSet: useSetPlaceCast };

/** The calls a step's holder presses are handed, so the door holds no bridge of its own: held here, handed down by the organisms that hold the bridge. */
export const stepHoldBridge: StepHoldBridge = { useCast: usePlaceCast, useHold: useStepHold };
