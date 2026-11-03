// A repository's reconcile with the shared doctrine as the driver answers it (D230 point 2; `RECONCILE_PLAN` and `RECONCILE_APPLY`): the
// shapes the place page's note draws and the reader that takes the answer defensively. The bridge leaves a null out.

/** How the canon the doctrine tool carries stands to the one the checkout synced from. */
export type ReconcileUpdate = 'behind' | 'differs';

/** What the doctrine tool's `status --json` said of the checkout, its verdicts and counts as the driver read them. */
export type ReconcileStatus = {
  synced: boolean;
  update?: ReconcileUpdate;
  carried?: string;
  locked?: string;
  changed: number;
  added: number;
  retired: number;
  reopened: number;
  newerLock?: string;
};

export type ReconcileRefusal = { code: string; sentence: string };

export type ReconcilePlanned = {
  repository: string;
  /** The canon moved or a keep reopened: the page offers the press. */
  offered: boolean;
  status?: ReconcileStatus;
  refusals: ReconcileRefusal[];
  pressable: boolean;
  title?: string;
};

export type ReconcileApplied = { done: boolean; refused?: string | null; message: string; quest?: string | null; ask?: string | null };

const count = (value: unknown): number => (typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : 0);

const text = (value: unknown): string | undefined => (typeof value === 'string' && value !== '' ? value : undefined);

/** The answer read field by field; one that is no plan (an older shell's, or the shell's pending) is none. */
export function readReconcilePlan(answer: unknown): ReconcilePlanned | null {
  if (typeof answer !== 'object' || answer === null || typeof (answer as { repository?: unknown }).repository !== 'string') return null;
  const plan = answer as Record<string, unknown>;
  const status = typeof plan.status === 'object' && plan.status !== null ? plan.status as Record<string, unknown> : null;
  const update = status?.update === 'behind' || status?.update === 'differs' ? status.update : undefined;
  return {
    repository: plan.repository as string,
    offered: plan.offered === true,
    ...(status
      ? {
        status: {
          synced: status.synced === true,
          ...(update ? { update } : {}),
          ...(text(status.carried) ? { carried: text(status.carried) } : {}),
          ...(text(status.locked) ? { locked: text(status.locked) } : {}),
          changed: count(status.changed), added: count(status.added), retired: count(status.retired), reopened: count(status.reopened),
          ...(text(status.newerLock) ? { newerLock: text(status.newerLock) } : {}),
        },
      }
      : {}),
    refusals: Array.isArray(plan.refusals)
      ? plan.refusals.filter((each): each is ReconcileRefusal =>
        typeof each === 'object' && each !== null && typeof (each as ReconcileRefusal).code === 'string' && typeof (each as ReconcileRefusal).sentence === 'string')
      : [],
    pressable: plan.pressable === true,
    ...(text(plan.title) ? { title: text(plan.title) } : {}),
  };
}
