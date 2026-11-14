import type { TFunction } from 'i18next';
import type { Session } from '../../data/api';
import { figure } from '../../locales/format';
import type { SessionGrouping } from '../groups';

// A session listed as work: its run's steps, who it waits on, one sentence of where it stands, and its turns beneath it. Pure,
// so every state a row could be in is an argument; the row draws what these say.

/** One step of the run's bound version as the row draws it. */
export type PassageStep = { node: string; kind: string; mark: 'passed' | 'current' | 'next' | 'failed' | string };

/** One turn of the session as the service answers it, across its line. */
export type SessionTurn = NonNullable<Session['turns']>[number];

/** Who a session waits on: the person, a step of its workflow, its agent at work, or its start. Null for an ended one waiting on none. */
export type WaitsOn = 'you' | 'step' | 'agent' | 'start' | null;

/** The steps of the run it stands for, in the graph's order; none where it stands for no run. */
export function passageSteps(grouping: SessionGrouping | null | undefined): PassageStep[] {
  return grouping?.passage?.steps ?? [];
}

/**
 * Who it waits on. The person only where the reader put it in *needs you*, which a check never does; a step where its turn ended and a
 * step of its workflow runs; its agent while it works; its start while it is queued.
 */
export function waitsOn(session: Session, grouping: SessionGrouping | null | undefined): WaitsOn {
  if (grouping?.group === 'needs-you') return 'you';
  if (session.state === 'awaiting-step') return 'step';
  if (session.state === 'working' || session.state === 'starting') return 'agent';
  if (session.state === 'queued') return 'start';
  return null;
}

/** One sentence of where it stands: whom it waits on, at which step of how many where it stands for a run. Null where nothing waits. */
export function workSentence(t: TFunction, session: Session, grouping: SessionGrouping | null | undefined): string | null {
  const who = waitsOn(session, grouping);
  if (!who) return null;
  const steps = passageSteps(grouping);
  const at = steps.findIndex((step) => step.mark === 'current' || step.mark === 'failed');
  const where = at >= 0 ? { node: steps[at].node, n: at + 1, total: steps.length } : null;
  return where
    ? t(`work.passage.at.${who}`, where)
    : t(`work.passage.waits.${who}`);
}

/** The words for what woke a turn; a cause a newer host adds is said as Daoris's. */
const CAUSES: ReadonlySet<string> = new Set(['start', 'step', 'person', 'limit', 'curve', 'agent', 'daoris']);

/**
 * Its turns beneath it, oldest first: the number, what woke it (and the step that did), its agent, its account by the name the person
 * gave it, and the input and output tokens its door reported for it, where it reported them.
 */
export function turnLines(
  t: TFunction, turns: readonly SessionTurn[] | null | undefined, accountName: (id: string) => string = (id) => id,
): string[] {
  return (turns ?? []).map((turn) => {
    const cause = t(`work.passage.cause.${CAUSES.has(turn.cause) ? turn.cause : 'daoris'}`);
    return [
      t('work.passage.turn', { n: turn.number }),
      turn.node ? t('work.passage.causeAt', { cause, node: turn.node }) : cause,
      turn.agent ?? null,
      turn.account ? accountName(turn.account) : null,
      turn.input != null ? t('work.passage.tokensIn', { tokens: figure(turn.input) }) : null,
      turn.output != null ? t('work.passage.tokensOut', { tokens: figure(turn.output) }) : null,
    ].filter(Boolean).join(' · ');
  });
}
