import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import {
  type HistoryClearAnswer, historyPayload, historyPlanOf, type HistoryTarget, type HistoryUnitName,
} from '../../work/history/history';
import { call, lookBound } from '../call';

// Clearing finished history from this machine: what a clear of a
// workspace, a closed quest's work, its failed sessions or an ask's work would take and keep, and the clear itself. The
// driver library's `HistoryClearing` answers both, which `daoris history` and its `clear` verbs call too.

/**
 * The shapes the routes answer live with the screen's own rule (`work/history/history.ts`), since a molecule may read a shape and
 * never a bridge domain.
 */
export type {
  HistoryClearAnswer, HistoryPlan, HistoryReading, HistoryReason, HistoryScopeName, HistoryTarget, HistoryUnit, HistoryUnitName,
} from '../../work/history/history';

/**
 * Under its own root, so a tick asks none of them again: a workspace's reading walks the home's files, and a closed quest's plan changes only by a press, which asks it again. Each is asked while a page that
 * shows it is in front, and once more on every clear.
 */
export const historyKey = (target: HistoryTarget) => ['history', target.scope, target.id] as const;

/** The plans, every scope, which a clear makes stale. */
export const ALL_HISTORY =['history'] as const;

/**
 * The reading walks the home's files and the second press removes them, each after asking the service: either may
 * outlast the bridge's default 30 seconds on a large home, so the page waits as long as a look over one workspace may take.
 */
const HISTORY_BOUND = lookBound(1);

/**
 * The first press's answer (`HISTORY_PLAN`): every unit with what it takes and why it would stay, and for a
 * workspace the reading. Shell-only: a browser has no driver and no home, and `available` says so, so the pages
 * offer no clear there.
 */
export const useHistoryPlan = (target: HistoryTarget | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: target ? historyKey(target) : [...ALL_HISTORY, 'none'],
    queryFn: async () => historyPlanOf(await call('HISTORY_PLAN', historyPayload(target!), { timeoutMs: HISTORY_BOUND })),
    enabled: isAvailable && enabled && target !== null,
    refetchOnWindowFocus: false,
  });
  return {
    plan: query.data ?? null,
    /** Asking, with no answer yet: nothing is offered until the plan says what may go. */
    loading: query.isPending && query.fetchStatus !== 'idle',
    error: query.error,
    available: isAvailable,
  };
};

/**
 * The second press (`HISTORY_CLEAR`): exactly the units the first press listed, each judged again by the
 * driver as it goes. A workspace's press with no unit takes its left-over files and, where it keeps no ask, its intake's
 * room. What it took leaves the quests, the asks and the sessions, and every plan is stale: the route's `STATE_CHANGED`
 * asks each again, in every window.
 *
 * @remarks
 * The plans are asked again here too, without cancelling the hub's read: a press refused for a unit that changed since the
 * list is an error, which publishes nothing, and its plan is stale all the same.
 */
export const useClearHistory = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ target, units }: { target: HistoryTarget; units: readonly HistoryUnitName[] }) =>
      call(
        'HISTORY_CLEAR',
        { ...historyPayload(target), units: units.map(({ kind, id }) => ({ kind, id })) },
        { timeoutMs: HISTORY_BOUND },
      ) as Promise<HistoryClearAnswer>,
    onSettled: () => void client.invalidateQueries({ queryKey: ALL_HISTORY }, { cancelRefetch: false }),
  });
};
