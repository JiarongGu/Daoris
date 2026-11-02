import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { ConvertApplied, ConvertApplyAsk, ConvertAsk, ConvertPlanned, ConvertUndoPlan } from '../../agents/setup/convertPlan';
import { call } from '../call';

// The conversion of the old workflow records (WORKFLOWCLEAN1 1e): the plan, the twin of `daoris-driver workflow convert --plan`, and the person's
// two presses, the apply and the undo. The driver library's `ConversionPlans` and `ConversionApplies` answer them, which the terminal calls too (D50).

/** The plan reads the registry, the quests and every saved workflow: the page waits two minutes, as long as the apply may take. */
const CONVERT_BOUND = 2 * 60_000;

/** Under one root, so a press reads the plan and the undo's plan again. */
export const convertPlanKey = (ask: ConvertAsk) => ['convert-plan', ask] as const;

function useConvertRead<T>(ask: ConvertAsk, enabled: boolean) {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: convertPlanKey(ask),
    queryFn: () => call<T>('CONVERT_PLAN', ask, { timeoutMs: CONVERT_BOUND }),
    enabled: isAvailable && enabled,
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
  };
}

/**
 * The conversion's plan (`CONVERT_PLAN`; the cleanup design §4.4) for the row's two choices: every operation, what stays, and the digest a press names.
 * It writes nothing. The last answer stays shown while the next is read, so a choice does not blank the page. Shell-only: a browser has no driver (D47 §4).
 */
export const useConvertPlan = (choices: ConvertAsk, { enabled = true }: { enabled?: boolean } = {}) => useConvertRead<ConvertPlanned>(choices, enabled);

/** The undo's plan (`CONVERT_PLAN {undo}`, §4.5): read only where the page asks, since the page never reads it on every place. */
export const useConvertUndoPlan = ({ enabled }: { enabled: boolean }) => useConvertRead<ConvertUndoPlan>({ undo: true }, enabled);

/**
 * The apply (`CONVERT_APPLY`, the person's own press): the plan's digest and the choices. A refusal is an answer with nothing written, and a stale
 * digest is refused `moved` with the plan read again. The plans are read again after it.
 */
export const useConvertApply = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (ask: ConvertApplyAsk) => call<ConvertApplied>('CONVERT_APPLY', ask, { timeoutMs: CONVERT_BOUND }),
    onSettled: () => client.invalidateQueries({ queryKey: ['convert-plan'] }),
  });
};

/** The undo (`CONVERT_UNDO`, the person's own press): the undo plan's digest. */
export const useConvertUndo = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (ask: { digest: string }) => call<ConvertApplied>('CONVERT_UNDO', ask, { timeoutMs: CONVERT_BOUND }),
    onSettled: () => client.invalidateQueries({ queryKey: ['convert-plan'] }),
  });
};
