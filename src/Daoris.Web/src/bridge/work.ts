import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { AbandonAnswer, PauseAnswer, ResumeAnswer, WorkPlan, WorkTarget } from '../work/pausing';
import { call, lookBound } from './call';

// The work of an ask or a quest on this machine (MOD3; PAUSE1e, D132 §7.3): what it holds and what a pause and an abandon
// would do with each piece, a pause, a resume and an abandon. The driver library's `WorkPausing` and `WorkAbandoning`
// answer each, which `daoris-driver ask --pause|--resume|--abandon` and `quest pause|resume|abandon` call too (D50).

/**
 * The shapes the routes answer live with the screen's own rule (`work/pausing.ts`), since a molecule may read a shape and
 * never a bridge domain.
 */
export type {
  AbandonAnswer, AbandonEntry, PauseAnswer, ResumeAnswer, WorkPlan, WorkScopeName, WorkTarget,
} from '../work/pausing';

/**
 * Under the quests' key, so whatever asks the quests again asks the plan again: a tick, which may have stopped or started a
 * session of the work, and every move on a quest. Each answer walks git in the work's trees, so it is asked only while a
 * page that shows it is in front.
 */
export const workKey = (target: WorkTarget) => [...keys.allQuests, 'work', target.scope, target.id] as const;

/** The route's payload: the ask or the quest, by its scope's name. */
const named = (target: WorkTarget) => ({ [target.scope]: target.id });

/**
 * A pause stops sessions another Daoris process runs through its request folder, waited for ten seconds each (SESSUX1g),
 * and an abandon runs one sync pass per wired workspace and walks git per tree before it answers: each may outlast the
 * bridge's default 30 seconds, so the page waits as long as a look over one workspace may take (WSR7).
 */
const WORK_BOUND = lookBound(1);

/**
 * An answer read as a plan only where it has a plan's shape: its pieces' lists and the abandon's half. Anything else (a host
 * that answers the route with something older) is no plan, and the pages offer no act on it rather than guess one.
 */
const planOf = (answer: unknown): WorkPlan | null => {
  const plan = answer as Partial<WorkPlan> | null;
  return plan && Array.isArray(plan.quests) && Array.isArray(plan.sessions) && Array.isArray(plan.trees)
    && typeof plan.abandon === 'object' && plan.abandon !== null && Array.isArray(plan.abandon.pieces)
    ? { landings: [], paused: null, ...plan } as WorkPlan
    : null;
};

/**
 * The plan of an ask's work or a quest's (`WORK_PLAN`, design §1, §2.1, §3.2): every piece, what a pause and an abandon
 * would do with each and why, its own pause and its last abandon. Shell-only: a browser has no driver to ask (D47 §4), and
 * `available` says so, so the pages name the terminal's commands instead of offering acts nothing can carry out.
 */
export const useWorkPlan = (target: WorkTarget | null, { enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const query = useQuery({
    queryKey: target ? workKey(target) : [...keys.allQuests, 'work', 'none'],
    queryFn: async () => planOf(await call<unknown>('WORK_PLAN', named(target!))),
    enabled: isAvailable && enabled && target !== null,
    refetchOnWindowFocus: false,
  });
  return {
    plan: query.data ?? null,
    /** Asking, with no answer yet: the acts wait rather than offer what a plan has not said. */
    loading: query.isPending && query.fetchStatus !== 'idle',
    error: query.error,
    available: isAvailable,
  };
};

/** Everything a move on a work changes: its quests (the plan among them), the asks, the sessions and the driver's state. */
function useInvalidateWork() {
  const client = useQueryClient();
  return () => {
    void client.invalidateQueries({ queryKey: keys.allQuests });
    void client.invalidateQueries({ queryKey: keys.allAsks });
    void client.invalidateQueries({ queryKey: keys.allSessions });
    void client.invalidateQueries({ queryKey: keys.driver });
  };
}

/** *Pause…*'s press (`WORK_PAUSE`, design §2.1): what it stopped, and what it could not reach. */
export const usePauseWork = () => {
  const invalidate = useInvalidateWork();
  return useMutation({
    mutationFn: (target: WorkTarget) => call<PauseAnswer>('WORK_PAUSE', named(target), { timeoutMs: WORK_BOUND }),
    onSuccess: invalidate,
  });
};

/** *Resume*'s press (`WORK_RESUME`, design §2.4): what it released, and what still holds each quest. */
export const useResumeWork = () => {
  const invalidate = useInvalidateWork();
  return useMutation({
    mutationFn: (target: WorkTarget) => call<ResumeAnswer>('WORK_RESUME', named(target), { timeoutMs: WORK_BOUND }),
    onSuccess: invalidate,
  });
};

/**
 * *Abandon…*'s second press (`WORK_ABANDON`, design §3.1, §3.4): exactly the pieces its first press listed, with the
 * person's reason, each judged again by the host as it goes.
 */
export const useAbandonWork = () => {
  const invalidate = useInvalidateWork();
  return useMutation({
    mutationFn: ({ target, reason, pieces }: { target: WorkTarget; reason: string; pieces: readonly string[] }) =>
      call<AbandonAnswer>('WORK_ABANDON', { ...named(target), reason, pieces: [...pieces] }, { timeoutMs: WORK_BOUND }),
    onSuccess: invalidate,
  });
};
