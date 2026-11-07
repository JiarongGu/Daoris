import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { cn } from '../../lib/cn';
import { Icon, Tip } from '../../design/ui';
import { choiceChip, chosenLine } from '../actors';

/**
 * **The choice its actor ran, on the head**: a quiet `status` chip, *choice n · why*
 * where a later choice than the first ran, *your pick* where the person picked the agent, and nothing otherwise, so a start on its
 * first choice reads as it did. Where the line is narrow the chip keeps the number and the sentence moves into an ⓘ. Text, not a
 * control; *Details* says the rest.
 */
export function ChoiceChip({ session, narrow, className }: { session: Pick<Session, 'choice' | 'chosen'>; narrow?: boolean; className?: string }) {
  const { t } = useTranslation();
  const word = choiceChip(t, session);
  if (word === null) return null;
  const sentence = narrow && word !== choiceChip(t, { choice: session.choice }) ? chosenLine(t, session.chosen) : null;
  return (
    <span
      data-choice={session.choice ?? undefined}
      className={cn('inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-control border border-line px-1.5 text-small text-ink-soft', className)}
    >
      {sentence === null ? word : choiceChip(t, { choice: session.choice })}
      {sentence !== null && (
        <Tip content={sentence}>
          <button type="button" aria-label={sentence} className="inline-flex cursor-help items-center border-0 bg-transparent p-0 text-ink-soft">
            <Icon name="info" size={13} />
          </button>
        </Tip>
      )}
    </span>
  );
}
