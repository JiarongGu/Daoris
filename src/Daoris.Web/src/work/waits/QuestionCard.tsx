import { useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Chip } from '../../design/ui';
import { cn } from '../../lib/cn';
import type { ParkQuestion } from './parkQuestion';

/**
 * **A park's question, each answer a row that reads whole** (QUESTIONUI1a; D208, the supervisor design §8.1-§8.2): the agent's words as
 * written, then each answer it offered as one row, its label, under it *If you choose this:* and what the agent does on it, and on the
 * recommended row *recommended* and the agent's reason. Drawn in the box (`ParkAnswer`) and in the head's card (`AwaitingPerson`).
 *
 * @remarks
 * - **A choice, never a send.** With `onSelect` the rows are a native radio group, labelled by the question: Tab reaches it once,
 *   arrows move and select, Space selects. Nothing is selected at first: the recommendation is marked, never preselected, so an answer
 *   is never the agent's by default (D52). The answer is sent by the field and *Send* beside the card, the box's own or the head card's
 *   (the design §8.4): the card holds no words and no send.
 * - **The person's pick apart from the agent's**: the recommended row wears the accent's chip; the selected row its border and wash.
 * - **Cleared by its own press** (Codex's round-2 finding 15): a native radio cannot be unselected, so *Clear choice* returns the group
 *   to none, the focus to its first row, and the words go alone.
 * - **Its accessible name is its label, *recommended* where it is, and what it does**; the reason is its description, so *recommended*
 *   is said once. The `<label>` wraps the row for the press; `aria-labelledby` keeps the reason out of the name.
 * - Without `onSelect` the rows are listed with no radio, as the head's card lists them until its answer is opened.
 *
 * A molecule: props in, a selection out (D55). The frame holds the selection beside the words, tied to the question's id.
 */
export function QuestionCard({ question, selected = null, onSelect }: {
  question: ParkQuestion;
  /** The answer selected, by its label, or null for none. */
  selected?: string | null;
  /** Select an answer by its label, or none; absent, the answers are listed with no radio. */
  onSelect?: (label: string | null) => void;
}) {
  const { t } = useTranslation();
  const id = useId();
  const group = useRef<HTMLDivElement>(null);
  const asked = `${id}-question`;
  const choosing = onSelect !== undefined && question.choices.length > 0;

  const clear = () => {
    onSelect?.(null);
    group.current?.querySelector<HTMLInputElement>('input[type="radio"]')?.focus();
  };

  return (
    <div className="grid gap-1.5">
      <p id={asked} className="m-0 whitespace-pre-wrap break-words text-body text-ink">{question.question}</p>
      {question.choices.length > 0 && (
        <div
          ref={group}
          role={choosing ? 'radiogroup' : 'list'}
          aria-labelledby={choosing ? asked : undefined}
          className="grid gap-1.5"
        >
          {question.choices.map((choice, index) => {
            const recommended = question.recommend === index + 1;
            const why = recommended ? question.why : null;
            const at = `${id}-${index}`;
            const chosen = selected === choice.label;
            const body = (
              <span className="grid min-w-0 flex-1 gap-0.5">
                <span className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
                  <span id={`${at}-label`} className="break-words text-body font-semibold text-ink">{choice.label}</span>
                  {recommended && <span id={`${at}-recommended`}><Chip accent>{t('work.park.recommended')}</Chip></span>}
                </span>
                {choice.does && (
                  <span id={`${at}-does`} className="break-words text-small text-ink-soft">{t('work.park.ifChosen', { does: choice.does })}</span>
                )}
                {why && <span id={`${at}-why`} className="break-words text-small text-ink-soft">{t('work.park.why', { why })}</span>}
              </span>
            );
            const row = 'flex min-w-0 items-start gap-2 rounded-control border px-3 py-2';
            if (!choosing) {
              return <div key={choice.label} role="listitem" className={cn(row, 'border-line bg-raised')}>{body}</div>;
            }
            return (
              <label
                key={choice.label}
                className={cn(
                  row,
                  'cursor-pointer has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent/60',
                  chosen ? 'border-accent bg-accent-soft' : 'border-line bg-raised hover:border-line-strong',
                )}
              >
                <input
                  type="radio"
                  name={`${id}-choice`}
                  value={choice.label}
                  checked={chosen}
                  onChange={() => onSelect?.(choice.label)}
                  aria-labelledby={[`${at}-label`, recommended && `${at}-recommended`, choice.does && `${at}-does`].filter(Boolean).join(' ')}
                  aria-describedby={why ? `${at}-why` : undefined}
                  className="mt-1 size-4 shrink-0 cursor-pointer accent-accent outline-none"
                />
                {body}
              </label>
            );
          })}
        </div>
      )}
      {choosing && selected !== null && (
        <span>
          <Button variant="ghost" onClick={clear} className="h-6 px-1.5 text-meta">{t('work.park.clear')}</Button>
        </span>
      )}
      {question.by && <p className="m-0 break-words text-small text-ink-faint">{t('work.park.by', { by: question.by })}</p>}
    </div>
  );
}
