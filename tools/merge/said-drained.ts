/**
 * What the drop of a candidate ended. The runner owns the run's processes, so it ends them (within the drop poll) and records what
 * it ended; this waits for that record, and says it. A candidate with no run in progress had none to end.
 * Only the runner's take of the drop (`draining`, at its next ask) is waited for on the clock: one that took it always records what
 * it ended, however long ending its trees takes, so that is waited for while the runner runs.
 */

type Candidate = { tip: string; state: string; run?: { pid?: number } };
type Drained = { tip: string; at: string };
type Queue = { drained?: Drained | null; draining?: Drained | null };

export type DrainSeams = {
  read: () => Queue;
  alive: (pid: number) => boolean;
  line: (drained: Drained) => string;
  sleep: (ms: number) => Promise<void>;
  waitMs: number;
  pollMs: number;
  span: (ms: number) => string;
  logFile: string;
  say: (line: string) => void;
};

export async function saidDrained(seams: DrainSeams, candidate: Candidate, asked: string): Promise<void> {
  const runner = candidate.run?.pid;
  const running = ['running', 'bisecting', 'forming'].includes(candidate.state) && typeof runner === 'number' && seams.alive(runner);
  if (!running) {
    seams.say(`  ${candidate.tip.slice(0, 7)} had no run in progress (${candidate.state}), so there was nothing to end`);
    return;
  }
  const until = Date.now() + seams.waitMs;
  for (;;) {
    const { drained, draining } = seams.read();
    if (drained?.tip === candidate.tip && drained.at >= asked) {
      seams.say(`  the runner (pid ${runner}) ${seams.line(drained)}, and takes the next queued work`);
      return;
    }
    const took = draining?.tip === candidate.tip && draining.at >= asked;
    if (!seams.alive(runner) || (!took && Date.now() >= until)) break;
    await seams.sleep(100);
  }
  const every = seams.pollMs < 1000 ? `${seams.pollMs} ms` : seams.span(seams.pollMs);
  seams.say(`  the runner (pid ${runner}) has not yet said it ended ${candidate.tip.slice(0, 7)}'s run; it asks every ${every}, and ${seams.logFile} says when it has`);
}
