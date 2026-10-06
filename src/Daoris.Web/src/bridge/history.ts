import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import {
  type HistoryClearAnswer, historyPayload, historyPlanOf, type HistoryTarget, type HistoryUnitName,
} from '../work/history';
import { call, lookBound } from './call';

// Clearing finished history from this machine (HIST1e, D153; the history-clearing design §5, §6.1, §6.3): what a clear of a
// workspace, a closed quest's work, its failed sessions or an ask's work would take and keep, and the clear itself. The
// driver library's `HistoryClearing` answers both, which `daoris-driver history` and its `clear` verbs call too (D50).

/**
 * The shapes the routes answer live with the screen's own rule (`work/history.ts`), since a molecule may read a shape and
 * never a bridge domain.
 */
export type {
  HistoryClearAnswer, HistoryPlan, HistoryReading, HistoryReason, HistoryScopeName, HistoryTarget, HistoryUnit, HistoryUnitName,
} from '../work/history';

/**
 * Under its own root, so a tick asks none of them again: a workspace's reading walks the home's files (design §12 leaves how
 * long unmeasured), and a closed quest's plan changes only by a press, which asks it again. Each is asked while a page that
 * shows it is in front, and once more on every clear.
 */
export const historyKey = (target: HistoryTarget) => ['history', target.scope, target.id] as const;

/** The plans, every scope, which a clear makes stale. */
const ALL_HISTORY = ['history'] as const;

/**
 * The reading walks the home's files and the second press removes them, each after asking the service (§2.4, §5): either may
 * outlast the bridge's default 30 seconds on a large home, so the page waits as long as a look over one workspace may take
 * (WSR7).
 */
const HISTORY_BOUND = lookBound(1);

/**
 * The first press's answer (`HISTORY_PLAN`, design §5 step 1): every unit with what it takes and why it would stay, and for a
 * workspace the reading. Shell-only: a browser has no driver and no home (D47 §4), and `available` says so, so the pages
 * offer no clear there.
 */
export const useHistoryPlan = (target: HistoryTarget | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: target ? historyKey(target) : [...ALL_HISTORY, 'none'],
    queryFn: async () => historyPlanOf(await call<unknown>('HISTORY_PLAN', historyPayload(target!), { timeoutMs: HISTORY_BOUND })),
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
 * The second press (`HISTORY_CLEAR`, design §5 step 2): exactly the units the first press listed, each judged again by the
 * driver as it goes. A workspace's press with no unit takes its left-over files and, where it keeps no ask, its intake's
 * room. What it took leaves the quests, the asks and the sessions, so each is asked again; and every plan, which is stale
 * whether the press went or was refused for a unit that changed since the list.
 */
export const useClearHistory = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ target, units }: { target: HistoryTarget; units: readonly HistoryUnitName[] }) =>
      call<HistoryClearAnswer>(
        'HISTORY_CLEAR',
        { ...historyPayload(target), units: units.map(({ kind, id }) => ({ kind, id })) },
        { timeoutMs: HISTORY_BOUND },
      ),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allQuests });
      void client.invalidateQueries({ queryKey: keys.allAsks });
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
    onSettled: () => void client.invalidateQueries({ queryKey: ALL_HISTORY }),
  });
};
