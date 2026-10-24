import { useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, ChoiceRow, Chip } from '../../design/ui';
import { knownBound, type ParkQuestion, type SupervisorBound } from './parkQuestion';

/** Each bound's words on the card (SUPERVISOR1b, the supervisor design §4.1), the agent's marks among them. */
const BOUND_WORDS = {
  'go-ahead': 'work.park.bound.goAhead',
  'sign-in': 'work.park.bound.signIn',
  access: 'work.park.bound.access',
  spending: 'work.park.bound.spending',
  delete: 'work.park.bound.delete',
  'repository-asks': 'work.park.bound.repositoryAsks',
  preference: 'work.park.bound.preference',
  unrecorded: 'work.park.bound.unrecorded',
  contradicts: 'work.park.bound.contradicts',
  instructed: 'work.park.bound.instructed',
} as const satisfies Record<SupervisorBound, string>;

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
 *   is said once. Each row is the UI design's `ChoiceRow`: a native radio in the row's `<label>`, drawn in the platform's palette, its
 *   name by `aria-labelledby` so the reason stays out of it.
 * - Without `onSelect` the rows are listed with no radio, as the head's card lists them until its answer is opened.
 * - **Whose question it is** (SUPERVISOR1b, the supervisor design §4.2): under the question, *Asked of you alone* with the agent's mark
 *   in words; and above the answers, where the supervisor left it to the person, *The supervisor left this to you* with the bound in
 *   words and each reason with its source, the owner's to read. A mark or bound this build has no words for is said without naming it.
 *   The answers stay the person's to press: nothing is selected for them.
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
  const left = question.left;
  const leftWords = left
    ? knownBound(left.bound) ? t('work.park.left', { bound: t(BOUND_WORDS[left.bound]) }) : t('work.park.leftUnnamed')
    : null;

  const clear = () => {
    onSelect?.(null);
    group.current?.querySelector<HTMLInputElement>('input')?.focus();
  };

  return (
    <div className="grid gap-1.5">
      <p id={asked} className="m-0 whitespace-pre-wrap break-words text-body text-ink">{question.question}</p>
      {question.owner && (
        <p className="m-0 break-words text-small text-ink-soft">
          {knownBound(question.owner) ? t('work.park.mine', { bound: t(BOUND_WORDS[question.owner]) }) : t('work.park.mineUnnamed')}
        </p>
      )}
      {left && (
        <div role="note" aria-labelledby={`${id}-left`} className="grid gap-1 rounded-control border border-line bg-sunken px-3 py-2">
          <span id={`${id}-left`} className="break-words text-small font-semibold text-ink">{leftWords}</span>
          {left.reasons.length > 0 && (
            <ul className="m-0 grid list-none gap-1 p-0">
              {left.reasons.map((reason, index) => (
                <li key={index} className="grid gap-0.5">
                  <span className="break-words text-small text-ink">{reason.says}</span>
                  <span className="break-words text-meta text-ink-faint">{t('work.park.source', { source: reason.source })}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
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
            if (!choosing) {
              return (
                <div key={choice.label} role="listitem" className="flex min-w-0 rounded-control border border-line bg-raised px-3 py-2">
                  {body}
                </div>
              );
            }
            return (
              <ChoiceRow
                key={choice.label}
                name={`${id}-choice`}
                value={choice.label}
                checked={chosen}
                onChange={() => onSelect?.(choice.label)}
                labelledBy={[`${at}-label`, recommended && `${at}-recommended`, choice.does && `${at}-does`].filter(Boolean).join(' ')}
                describedBy={why ? `${at}-why` : undefined}
              >
                {body}
              </ChoiceRow>
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
