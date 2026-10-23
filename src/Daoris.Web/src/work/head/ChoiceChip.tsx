import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { cn } from '../../lib/cn';
import { choiceChip } from '../actors';

/**
 * **The choice its actor ran, on the head** (D205 §4.6 item 2, §7: MODELROLE1c2): a quiet `status` chip, *choice n* where a later
 * choice than the first ran, *your pick* where the person picked the agent, and nothing otherwise, so a start on its first choice reads
 * as it did. Text, not a control; *Details* says the rest.
 */
export function ChoiceChip({ session, className }: { session: Pick<Session, 'choice'>; className?: string }) {
  const { t } = useTranslation();
  const word = choiceChip(t, session);
  if (word === null) return null;
  return (
    <span
      data-choice={session.choice ?? undefined}
      className={cn('inline-flex shrink-0 items-center whitespace-nowrap rounded-control border border-line px-1.5 text-small text-ink-soft', className)}
    >
      {word}
    </span>
  );
}
