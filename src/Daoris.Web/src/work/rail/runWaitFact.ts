import type { SessionGrouping } from '../groups';

type RunWait = NonNullable<SessionGrouping['waits']>[number];

/** Each wait's line by what it waits for; a word a newer host adds is said as a step that waits for the person. */
const LINES: Record<string, string> = {
  accept: 'work.rail.runWaits.accept',
  approval: 'work.rail.runWaits.approval',
  task: 'work.rail.runWaits.task',
  'go-ahead': 'work.rail.runWaits.goAhead',
  held: 'work.rail.runWaits.held',
  choose: 'work.rail.runWaits.choose',
  left: 'work.rail.runWaits.left',
};

/**
 * What a row the driver placed by its run's waits says: the step that decided its group, a wait other than an Accept where one
 * stands (which put it in Waiting on you), else its Accept, with the tip that its run's view is where it is acted on. Null where
 * no step of its run waits on the person.
 */
export function runWaitFact(grouping: SessionGrouping): { line: string; values?: Record<string, unknown>; tip: string } | null {
  const waits = grouping.waits ?? [];
  const lead: RunWait | undefined = waits.find((wait) => wait.why !== 'accept') ?? waits[0];
  if (!lead) return null;
  return { line: LINES[lead.why] ?? 'work.rail.runWaits.other', values: { node: lead.node }, tip: 'work.rail.runWaitsTip' };
}

/**
 * Whether the driver placed the row of `session` by its run's waits (`why`: `accept`, `approval`, `task`, `go-ahead`, `held`, `choose`): the
 * list opens it on its run's view, where the step is acted on.
 */
export function opensRun(groups: readonly SessionGrouping[] | undefined, session: string): boolean {
  return (groups ?? []).some((row) => row.session === session && (row.waits?.length ?? 0) > 0);
}
