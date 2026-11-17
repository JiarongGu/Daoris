import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { PlaceCastChange, PlaceCastView } from '../../settings/place/PlaceCast';
import type { StepHoldAnswer, StepHoldAsk } from '../../workflow/wait/StepHold';
import { call } from '../call';

// A place's cast and a step's holder, over the records `daoris settings cast` and `daoris workflow take|hand|give-back` edit: each press the
// owner's, an agent's of the same route on the owner's card in every mode. A refusal is the driver's sentence, verbatim.

const castKey = (workspace?: string | null, repository?: string | null) => ['PLACE_CAST', workspace ?? '', repository ?? ''] as const;

/** A place's cast as its Settings draw it: each role's holder now and why, the risk and marks, the roster and the reporting line. */
export const usePlaceCast = (workspace?: string | null, repository?: string | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: castKey(workspace, repository),
    queryFn: () => call('PLACE_CAST', { workspace: workspace ?? null, repository: repository ?? null }) as Promise<PlaceCastView>,
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
