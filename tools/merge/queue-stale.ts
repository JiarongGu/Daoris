/**
 * The queue entries a dead process left stuck. A candidate that says it runs while neither its run's process nor the queue's runner
 * is alive is stale, and is re-run (a runner started resumes it); a queued request or a candidate whose tips main already holds
 * has landed, and is cleared into the landed record rather than waited on.
 */

type Member = { branch: string; tip: string; request?: string; handedBack?: unknown; [other: string]: unknown };
export type QueueRecord = {
  runner?: { pid: number } | null;
  candidate?: { state: string; run?: { pid?: number }; members: Member[]; [other: string]: unknown } | null;
  queued: Member[];
  landed?: unknown[];
  [other: string]: unknown;
};

const RUNNING = ['running', 'bisecting', 'forming'];

/** Why the candidate's run is stale, or null where it is not stale: it runs, or its runner does, or it makes no claim to run. */
export function staleRun(queue: QueueRecord, alive: (pid: number) => boolean): string | null {
  const candidate = queue.candidate;
  if (!candidate || !RUNNING.includes(candidate.state)) return null;
  const run = candidate.run?.pid;
  if (typeof run === 'number' && alive(run)) return null;
  if (queue.runner && alive(queue.runner.pid)) return null;
  const who = typeof run === 'number' ? `its run (pid ${run})` : 'its run';
  return `the candidate says ${candidate.state}, but ${who} has ended and the queue's runner${queue.runner ? ` (pid ${queue.runner.pid})` : ''} is not running`;
}

/** The queue with each request main already holds moved to `landed`, and the branches it cleared. */
export function clearMerged(queue: QueueRecord, merged: (tip: string) => boolean, at: string): { queue: QueueRecord; cleared: string[] } {
  const landedOf = (each: Member) => ({ branch: each.branch, tip: each.tip, request: each.request, commit: each.tip, at });
  const waiting = queue.queued.filter((each) => !merged(each.tip));
  const gone = queue.queued.filter((each) => merged(each.tip));
  const candidate = queue.candidate ?? null;
  const live = candidate?.members.filter((each) => !each.handedBack) ?? [];
  const ended = candidate !== null && live.length > 0 && live.every((each) => merged(each.tip));
  const cleared = [...gone, ...(ended ? live : [])];
  if (cleared.length === 0) return { queue, cleared: [] };
  return {
    queue: { ...queue, queued: waiting, candidate: ended ? null : candidate, landed: [...(queue.landed ?? []), ...cleared.map(landedOf)].slice(-200) },
    cleared: cleared.map((each) => each.branch),
  };
}
