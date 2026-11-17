/**
 * The merge's fast tier as a plan: the kind of run each gate is in a scoped choice (`scopedKind`, which the queue's choice and
 * its preview read), and what a builder's preflight runs of that choice (`fastPlan`). A merge lands on the fast tier; the
 * Process and git halves, the rehearsals and the browser specs run on main after it, in the CI run the install waits for. The
 * queue still runs a half that holds a known red, but a builder never runs a half (`.claude/agents/branch-worker.md`), so the
 * preflight defers every one, a known red's included, whatever path reaches it: a branch that changes `daoris.gates.json`
 * reaches every gate and still runs only the fast ones.
 */
import { BLOCKING } from './reach.ts';
import { dotnetComponent, isGitHalf, isProcessGate, isProcessLane, isStageRehearsal } from './plan.ts';

/** How a scoped choice runs a gate: `cheap` in every candidate, `fast` (a .NET fast half) and `node` where reached, the rest deferred. */
export type ScopedKind = 'cheap' | 'git' | 'process' | 'rehearsal' | 'browser' | 'fast' | 'node';

/** What the kind reads of a gate: its name and what it runs. */
type Named = { name: string; run: string };

/** A gate a choice runs, keeps or defers, with why. */
export type Chosen = { name: string; why: string };

export type ScopedSet = { run: Chosen[]; kept: Chosen[]; deferred: Chosen[] };

/** The kind of run a gate is in a scoped choice, read from what it runs, so a renamed gate or a new half keeps its kind. */
export function scopedKind(gate: Named): ScopedKind {
  if (BLOCKING.includes(gate.name)) return 'cheap';
  if (isGitHalf(gate)) return 'git';
  if (isProcessGate(gate)) return 'process';
  if (isProcessLane(gate) || isStageRehearsal(gate) || /^npm run rehearse/.test(gate.run.trim())) return 'rehearsal';
  if (['web', 'web-measured'].includes(gate.name)) return 'browser';
  if (dotnetComponent(gate.run)) return 'fast';
  return 'node';
}

/** The kinds the fast tier never runs before a landing, each as its deferral says it. */
const NOT_FAST: Partial<Record<ScopedKind, string>> = {
  process: 'a Process half', git: 'the git half', rehearsal: 'a rehearsal or the tools\' end-to-end tier', browser: 'a browser spec',
};

/**
 * Of a scoped choice's set, what a preflight runs: every gate the set runs but the kinds the fast tier defers, which join the
 * set's own deferred with why. `kindOf` names a gate's kind (`scopedKind` of its declaration).
 */
export function fastPlan(set: ScopedSet, kindOf: (name: string) => ScopedKind): ScopedSet {
  const run: Chosen[] = [];
  const deferred = [...set.deferred];
  for (const entry of set.run) {
    const kind = NOT_FAST[kindOf(entry.name)];
    if (kind) deferred.push({ name: entry.name, why: `${kind}: the merge's fast tier defers it to the CI run on main, and a preflight never runs one, a known red's included` });
    else run.push(entry);
  }
  return { run, kept: set.kept, deferred };
}
