import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { cn } from '../../lib/cn';
import type { SessionGrouping } from '../groups';
import { passageSteps, turnLines, workSentence } from './workFacts';

/** Each mark's sign, never hue alone: its words are its label. */
const SIGNS: Record<string, string> = { passed: '✓', current: '●', next: '○', failed: '✕' };

/**
 * A session's row as work: its run's steps passed, current and to come, one sentence of whom it waits on, and its turns beneath
 * it. A molecule: what it says is handed in, and it draws nothing where nothing is known.
 */
export function SessionWork({ session, grouping, accountName }: {
  session: Session;
  grouping?: SessionGrouping | null;
  /** The name the person gave an account, where they gave one. */
  accountName?: (id: string) => string;
}) {
  const { t } = useTranslation();
  const steps = passageSteps(grouping);
  const sentence = workSentence(t, session, grouping);
  // One turn says nothing a row does not already; a session that went on draws each beneath it.
  const turns = (session.turns?.length ?? 0) > 1 ? turnLines(t, session.turns, accountName) : [];
  if (steps.length === 0 && !sentence && turns.length === 0) return null;

  return (
    <span className="mt-0.5 block font-mono text-meta text-ink-faint">
      {steps.length > 0 && (
        <span role="list" aria-label={t('work.passage.label', { workflow: grouping?.passage?.workflow ?? '' })} className="flex flex-wrap gap-x-1.5">
          {steps.map((step) => (
            <span
              key={step.node}
              role="listitem"
              title={t(`work.passage.mark.${SIGNS[step.mark] ? step.mark : 'next'}`, { node: step.node })}
              className={cn(step.mark === 'current' && 'text-ink', step.mark === 'failed' && 'text-warn')}
            >
              <span aria-hidden="true">{SIGNS[step.mark] ?? SIGNS.next}</span>
              <span className="sr-only">{t(`work.passage.mark.${SIGNS[step.mark] ? step.mark : 'next'}`, { node: step.node })}</span>
              <span aria-hidden="true">{step.node}</span>
            </span>
          ))}
        </span>
      )}
      {sentence && <span className="block truncate">{sentence}</span>}
      {turns.length > 0 && (
        <span role="list" aria-label={t('work.passage.turns')} className="block">
          {turns.map((line, at) => <span key={at} role="listitem" className="block truncate">{line}</span>)}
        </span>
      )}
    </span>
  );
}
