import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReviewRow, RunReviewAnswer, RunReviewStep } from './runReviewWire';
import { Button, Inline } from '../../../design/ui';
import type { NodeWordAnswer, NodeWordAsk } from '../../../workflow/run';
import { Markdown } from '../../conversation/Markdown';
import { DecisionBar } from './DecisionBar';
import { PlanOperations, RecordRows, type ViewedMarks } from './ReviewChanges';

/** A newer revision, over the bar: announced with what changed since, read only when the person asks; an older one read is said. */
function Revisions({ answer, onRead }: { answer: RunReviewAnswer; onRead?: (attempt: number) => void }) {
  const { t } = useTranslation();
  const newer = answer.newer ?? null;
  if (!newer || !answer.record) return null;
  return (
    <div role="status" className="grid gap-1 border-b border-line bg-raised px-3 py-2 text-small">
      <p className="m-0 flex flex-wrap items-center gap-2 text-ink">
        <span>{newer.changed.length > 0 ? t('work.runReview.newer', { count: newer.changed.length }) : t('work.runReview.newerSame')}</span>
        {onRead && <Button variant="primary" className="min-h-0 px-2 py-0.5 text-small" onClick={() => onRead(newer.attempt)}>{t('work.runReview.newerRead')}</Button>}
      </p>
      <p className="m-0 text-meta text-ink-faint">{t('work.runReview.older', { revision: answer.record.revision.slice(0, 7) })}</p>
    </div>
  );
}

/** Each part's handback: its note as Markdown, its requirements with their checks, and *unchecked* where nothing checked its done. */
function Handbacks({ answer }: { answer: RunReviewAnswer }) {
  const { t } = useTranslation();
  const parts = answer.record?.parts ?? [];
  if (parts.length === 0) return null;
  const answered = answer.record?.kind === 'answer';
  return (
    <section aria-label={answered ? t('work.runReview.answer') : t('work.runReview.handbacks')} className="grid gap-3 border-b border-line px-3 py-3">
      <h3 className="m-0 text-meta uppercase tracking-[0.06em] text-ink-faint">{answered ? t('work.runReview.answer') : t('work.runReview.handbacks')}</h3>
      {parts.map((part, at) => (
        <article key={part.quest} className="grid gap-1.5">
          {parts.length > 1 && <h4 className="m-0 text-small font-semibold">{t('work.runReview.part', { part: at + 1, title: part.title })}</h4>}
          {part.note && <Markdown text={part.note} />}
          {part.requirements.length > 0 && (
            <ul className="m-0 grid list-none gap-0.5 p-0 text-small">
              {part.requirements.map((requirement) => (
                <li key={requirement.quote} className="text-ink-soft">{requirement.quote} · <span className="text-ink-faint">{requirement.check}</span></li>
              ))}
            </ul>
          )}
          {part.unchecked && <p className="m-0 text-small text-warn">{t('work.runReview.unchecked')}</p>}
        </article>
      ))}
    </section>
  );
}

/** What follows the review as the run's graph draws it: each step with its state, its outcome and its door. */
function Follows({ steps }: { steps: RunReviewStep[] }) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('work.runReview.follows')} className="grid gap-1.5 px-3 py-3">
      <h3 className="m-0 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.runReview.follows')}</h3>
      {steps.length === 0 ? <p className="m-0 text-small text-ink-soft">{t('work.runReview.followsNone')}</p> : (
        <>
          <p className="m-0 text-small text-ink-soft">{t('work.runReview.followsBefore')}</p>
          <ul className="m-0 grid list-none gap-1 p-0 text-small">
            {steps.map((step) => (
              <li key={step.node} className="grid gap-0.5">
                <span className="text-ink">
                  {t('work.runReview.step', { node: step.node, state: t(`work.runReview.state.${step.state}`, { defaultValue: step.state }) })}
                  {step.outcome && <span className="text-ink-faint"> · {step.outcome}</span>}
                </span>
                {step.door && <span className="font-mono text-meta text-ink-faint">{step.door}</span>}
                {step.words && <span className="text-meta text-ink-faint">{step.words}</span>}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}

/**
 * **A run's review**: one column, the decision bar at its top and in reach as the reader scrolls, then what each part handed back, the
 * changes drawn by the record's kind, and what follows the review.
 *
 * @remarks
 * **Drawn from the record, pinned.** A folder's and a copy's files are the record's rows, opened on what `fileBody` draws of the record's
 * versions; a plan is its operations; an answer is its handback rendered as Markdown; a branch's and a checkout's files are `gitFiles`,
 * the page's. A newer revision is announced over the bar and read only on *Read it*: what is drawn is never swapped under the reader.
 *
 * **Viewed** marks are the reader's, by a file's path and content (`viewed`), so a file a newer revision left untouched keeps its mark.
 *
 * An organism's body with no bridge: the page hands it the answer and its presses.
 */
export function RunReview({ answer, viewed, onWord, onRead, onOverrule, overruling, overruleSaid, fileBody, gitFiles }: {
  answer: RunReviewAnswer;
  viewed: ViewedMarks;
  onWord?: (ask: NodeWordAsk) => Promise<NodeWordAnswer>;
  /** Read the record at an attempt: the newer one announced. */
  onRead?: (attempt: number) => void;
  onOverrule?: (decision: string) => void;
  overruling?: boolean;
  overruleSaid?: string | null;
  /** What a folder's or a copy's row draws once opened: its two versions compared. */
  fileBody?: (row: ReviewRow) => ReactNode;
  /** A branch's or a checkout's files with their diffs. */
  gitFiles?: ReactNode;
}) {
  const { t } = useTranslation();
  const record = answer.record ?? null;
  const kind = record?.kind ?? answer.says?.kind ?? 'answer';
  return (
    <section aria-label={t('work.runReview.title')} className="flex min-h-0 flex-col overflow-y-auto">
      <div className="sticky top-0 z-10">
        <Revisions answer={answer} onRead={onRead} />
        <DecisionBar answer={answer} onWord={onWord} onOverrule={onOverrule} overruling={overruling} overruleSaid={overruleSaid} />
      </div>
      <Handbacks answer={answer} />
      {record && record.incomplete.length > 0 && (
        <section className="border-b border-line px-3 py-2 text-small">
          <p className="m-0 text-ink-soft">{t('work.runReview.incomplete')}</p>
          <ul className="m-0 list-disc pl-5 text-ink-faint">{record.incomplete.map((gap) => <li key={`${gap.code}:${gap.path ?? ''}`}><Inline text={gap.said} /></li>)}</ul>
        </section>
      )}
      <div className="border-b border-line">
        {(kind === 'folder' || kind === 'copy') && <RecordRows rows={record?.rows ?? []} viewed={viewed} body={fileBody} />}
        {kind === 'plan' && <PlanOperations system={answer.says?.system ?? record?.place ?? ''} operations={answer.operations} />}
        {(kind === 'branch' || kind === 'checkout') && gitFiles}
        {kind === 'answer' && <p className="m-0 px-3 py-2 text-small text-ink-soft">{t('work.runReview.nothing')}</p>}
      </div>
      <Follows steps={answer.follows ?? []} />
    </section>
  );
}
