import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import { passageSteps, turnLines, workSentence } from './workFacts';

/**
 * A session's row as work: one status line of whom it waits on, at which step of how many. A molecule: what it says is handed
 * in, and it draws nothing where nothing is known.
 *
 * @remarks
 * **One line however much runs**: the card's height never grows with a run's steps or a session's turns. Each step with its mark
 * and each turn beneath it (what woke it, its agent, its account, its tokens) lead the line's tip, one to a line, as the
 * line's full text.
 */
export function SessionWork({ session, grouping, accountName }: {
  session: Session;
  grouping?: SessionGrouping | null;
  /** The name the person gave an account, where they gave one. */
  accountName?: (id: string) => string;
}) {
  const { t } = useTranslation();
  const sentence = workSentence(t, session, grouping);
  if (!sentence) return null;
  const steps = passageSteps(grouping).map((step) => t(`work.passage.mark.${step.mark in MARKS ? step.mark : 'next'}`, { node: step.node }));
  // One turn says nothing a row does not already.
  const turns = (session.turns?.length ?? 0) > 1 ? turnLines(t, session.turns, accountName) : [];
  const tip = [...steps, ...turns].join('\n');

  return (
    <span className="mt-0.5 block font-mono text-meta text-ink-faint">
      <span title={tip || undefined} className="block truncate">{sentence}</span>
    </span>
  );
}

const MARKS: Record<string, true> = { passed: true, current: true, next: true, failed: true };
