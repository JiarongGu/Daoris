import type { TFunction } from 'i18next';
import { answerFor } from './AnswerAlone';
import { gitAsksFor } from './GitAsk';
import { heldFor } from './PermissionAsks';
import { type Wait, type WaitFacts, waitsOf } from './waits';
import { waitName } from './WaitAnchor';

/** A step of the session's run that waits on the person, as Sessions placed it (`SessionGrouping.waits`): its run, node, code and since. */
export type StepWait = { run: string; node: string; why: string; role?: string | null; since?: string | null };

/** A wait the dock holds: one of `waitsOf`'s, or a step of its run held for the person or left failed. */
export type DockWait = Wait | ({ kind: 'step' } & StepWait);

/** One line of the dock: its key, its wait, since when it waits (the session's last move where nothing says sooner) and its subagent. */
export type DockLine = { key: string; wait: DockWait; since: string; child?: string };

/**
 * **The census**: for each kind the dock holds, the reason the driver's reader gives a session that holds it, and whether the
 * reader reads that kind yet (places it under Needs you by itself). A kind it does not read yet is the page's alone: the state line
 * reads *Waiting on you* by the dock, and the window's log names the disagreement until the driver reads it too.
 * A new kind cannot be docked without a line here: the type holds every kind.
 */
export const DOCK_CENSUS: Readonly<Record<DockWait['kind'], { reason: string; read: boolean }>> = {
  question: { reason: 'question', read: true },
  intake: { reason: 'question', read: true },
  goAhead: { reason: 'question', read: true },
  permission: { reason: 'permission', read: false },
  gitAsk: { reason: 'git-ask', read: false },
  answerAlone: { reason: 'answer-alone', read: false },
  rule: { reason: 'rule', read: false },
  helper: { reason: 'helper', read: false },
  handOver: { reason: 'hand-over', read: false },
  accept: { reason: 'review', read: false },
  rebase: { reason: 'review', read: false },
  opinion: { reason: 'review', read: false },
  review: { reason: 'review', read: false },
  discard: { reason: 'review', read: false },
  step: { reason: 'review', read: true },
};

/**
 * The reader's reason for a run's step waits, as `SessionGrouping.WaitedOn` gives it: the first wait that is neither an Accept nor
 * a step left failed, else the person's review where an Accept waits, else the left step.
 */
export function stepReason(whys: readonly string[]): string {
  return whys.find((why) => why !== 'accept' && why !== 'left') ?? (whys.includes('accept') ? 'review' : 'left');
}

/** The reasons the reader gives a session it places under Needs you for a wait the dock holds: one of them with nothing docked disagrees. */
export const DOCKED_REASONS: ReadonlySet<string> = new Set(['question', 'review', 'left', 'approval', 'task', 'go-ahead', 'held', 'choose', 'actor', 'vacant', 'pass']);

const keyOf = (wait: DockWait): string => {
  switch (wait.kind) {
    case 'intake': return `intake:${wait.ask}`;
    case 'goAhead': return `goAhead:${wait.number}`;
    case 'permission': return `permission:${wait.request}`;
    case 'gitAsk':
    case 'rule':
    case 'helper':
    case 'handOver': return `${wait.kind}:${wait.id}`;
    case 'step': return `step:${wait.run}/${wait.node}`;
    default: return wait.kind;
  }
};

/** Since when a wait waits, where its own row says; else null, and the session's last move stands for it. */
function sinceOf(wait: DockWait, facts: WaitFacts): string | null | undefined {
  const { session } = facts;
  switch (wait.kind) {
    case 'permission': return heldFor(facts.permissions?.waits ?? [], session.id).find((held) => held.request === wait.request)?.since;
    case 'gitAsk': return gitAsksFor(facts.gitAsks?.waits ?? [], session.id).find((ask) => ask.id === wait.id)?.since;
    case 'answerAlone': return answerFor(facts.answers?.waits ?? [], session.id)?.since;
    case 'helper': return facts.helpers?.asks?.find((row) => row.id === wait.id)?.at;
    case 'handOver': return facts.handOver?.ask?.at;
    case 'step': return wait.since;
    default: return null;
  }
}

/** The subagent that raised a wait, where its row names one: a tool call a subagent's own session asked the person for. */
function childOf(wait: DockWait, facts: WaitFacts): string | undefined {
  if (wait.kind !== 'permission') return undefined;
  return heldFor(facts.permissions?.waits ?? [], facts.session.id).find((held) => held.request === wait.request)?.child || undefined;
}

const at = (iso: string) => {
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? Number.POSITIVE_INFINITY : ms;
};

/**
 * **What the dock holds**: every wait the person settles, `waitsOf`'s and the run's steps held for the person or left failed, oldest
 * first (a tie keeps the order they were read in), each keyed once. The session's last move dates a wait whose row says no since.
 */
export function dockOf(facts: WaitFacts, steps: readonly StepWait[] = []): DockLine[] {
  const waits: DockWait[] = [...waitsOf(facts), ...steps.map((step) => ({ kind: 'step' as const, ...step }))];
  const seen = new Set<string>();
  const lines: DockLine[] = [];
  for (const wait of waits) {
    const key = keyOf(wait);
    if (seen.has(key)) continue;
    seen.add(key);
    const child = childOf(wait, facts);
    lines.push({ key, wait, since: sinceOf(wait, facts) || facts.session.updated, ...(child ? { child } : {}) });
  }
  return lines
    .map((line, order) => ({ line, order }))
    .sort((a, b) => at(a.line.since) - at(b.line.since) || a.order - b.order)
    .map(({ line }) => line);
}

/** A dock wait's name, as its line, the state line and a teammate's line say it. */
export function dockName(t: TFunction, wait: DockWait): string {
  if (wait.kind !== 'step') return waitName(t, wait);
  return wait.why === 'accept' ? t('work.wait.name.stepAccept') : t('work.wait.name.step', { node: wait.node });
}
