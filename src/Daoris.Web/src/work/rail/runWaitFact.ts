import type { SessionGrouping } from '../groups';

type RunWait = NonNullable<SessionGrouping['waits']>[number];

/**
 * What a row the driver placed by its run's waits says, one way whoever holds the step: the step that decided its group (a wait other
 * than an Accept where one stands, which put it in Waiting on you, else its Accept), waiting on its role, held by the person, with the
 * tip that its run's view is where it is acted on. A part left says what never ran first. Null where no step of its run waits on the person.
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

/**
 * Whether the driver placed the row of `session` by its run's waits (`why`: `accept`, `approval`, `task`, `go-ahead`, `held`, `choose`,
 * `actor`, `vacant`): the list opens it on its run's view, where the step is acted on.
 */
export function opensRun(groups: readonly SessionGrouping[] | undefined, session: string): boolean {
  return (groups ?? []).some((row) => row.session === session && (row.waits?.length ?? 0) > 0);
}
