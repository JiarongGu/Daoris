import type { SessionGrouping } from '../groups';

type RunWait = NonNullable<SessionGrouping['waits']>[number];

/**
 * What a row placed by its run's waits says, whoever holds the step: the step that decided its group, with the tip that its run's view
 * is where it is acted on. Null where no step of its run waits on the person.
 */
export function runWaitFact(grouping: SessionGrouping): { line: string; values?: Record<string, unknown>; tip: string } | null {
  const waits = grouping.waits ?? [];
  const lead: RunWait | undefined = waits.find((wait) => wait.why !== 'accept') ?? waits[0];
  if (!lead) return null;
  return {
    line: lead.why === 'left' ? 'work.rail.runWaits.left' : 'work.rail.runWaits.on',
    values: { node: lead.node, role: lead.role ?? '' },
    tip: 'work.rail.runWaitsTip',
  };
}

/** Whether the driver placed the row of `session` by its run's waits: the list opens it on its run's view, where the step is acted on. */
export function opensRun(groups: readonly SessionGrouping[] | undefined, session: string): boolean {
  return (groups ?? []).some((row) => row.session === session && (row.waits?.length ?? 0) > 0);
}
