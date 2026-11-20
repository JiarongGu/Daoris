import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import { passageSteps, turnLines, workSentence } from './workFacts';

/** A session's row as work: one status line, whatever runs; its steps and turns are the line's tip. */
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
  const turns = (session.turns?.length ?? 0) > 1 ? turnLines(t, session.turns, accountName) : [];
  const tip = [...steps, ...turns].join('\n');

  return (
    <span className="mt-0.5 block font-mono text-caption text-ink-faint">
      <span title={tip || undefined} className="block truncate">{sentence}</span>
    </span>
  );
}

const MARKS: Record<string, true> = { passed: true, current: true, next: true, failed: true };
