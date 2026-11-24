import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import type { CheckBudgetsView } from '../settings/repository/CheckBudgets';
import { call, type Payload } from './call';

/**
 * A repository's check budgets on this machine, as the driver reads them from the home's record: each gate's timeout and where it comes
 * from, and the values the repository's declaration no longer holds (`CHECK_BUDGETS`). A read; `enabled` holds it back until the section
 * it serves is drawn. Nothing wakes it when the merge tool records a green run, so it is asked again whenever the page is.
 */
export const useCheckBudgets = (repository: string, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.checkBudgets(repository),
    queryFn: () => call('CHECK_BUDGETS', { repository }) as Promise<CheckBudgetsView>,
    enabled: isAvailable && enabled && repository !== '',
  });
};

/**
 * One change to a repository's check budgets, the same `daoris settings checks` makes: the person's alone, judged by the one door, the
 * driver's refusal its sentence verbatim. The answer is the repository's record, which replaces what the page read.
 */
export const useSetCheckBudgets = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: Payload<'SET_CHECK_BUDGETS'>) => call('SET_CHECK_BUDGETS', change) as Promise<CheckBudgetsView>,
    onSuccess: (view, change) => { if (change.repository) client.setQueryData(keys.checkBudgets(change.repository), view); },
  });
};
