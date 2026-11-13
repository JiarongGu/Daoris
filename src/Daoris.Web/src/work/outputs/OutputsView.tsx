import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { ReviewOutput, ReviewRow, RunReviewAnswer, RunReviewStep } from '../review/run/runReviewWire';
import { Button, Inline } from '../../design/ui';
import { Markdown } from '../conversation/Markdown';
import { PlanOperations, type ViewedMarks } from '../review/run/ReviewChanges';
import { ChangeSetDisplay } from './ChangeSetDisplay';

/** The order outputs are read in: what changed first, then a plan, then what was said of it, then what has no display here. */
const DISPLAY_ORDER = ['change-set', 'resource-plan', 'document'];

const rank = (output: ReviewOutput) => {
  const at = DISPLAY_ORDER.indexOf(output.display);
  return at < 0 ? DISPLAY_ORDER.length : at;
};

/** An output's name as its heading and the banner say it: its type's word, a handback by its part's title. */
export function outputName(t: TFunction, output: Pick<ReviewOutput, 'type' | 'title'>): string {
  if (output.type === 'handback') return t('work.outputs.handbackOf', { title: output.title ?? '' });
  return t(`work.outputs.type.${output.type}`, { defaultValue: output.title ?? output.type });
}

/** A newer revision, over the outputs: what moved since, by output, read only on *Read rN*; what is drawn is never swapped. */
function Newer({ answer, onRead }: { answer: RunReviewAnswer; onRead?: (attempt: number) => void }) {
  const { t } = useTranslation();
  const newer = answer.newer ?? null;
  if (!newer || !answer.record) return null;
  const known = answer.outputs ?? [];
  const names = (newer.outputs ?? []).map((id) => {
    const output = known.find((each) => each.id === id);
    return output ? outputName(t, output) : outputName(t, { type: id.split(':')[0] ?? id, title: null });
  });
  return (
    <div role="status" className="grid gap-1 border-b border-line bg-raised px-3 py-2 text-small">
      <p className="m-0 flex flex-wrap items-center gap-2 text-ink">
        <span>{names.length > 0 ? t('work.outputs.newer', { attempt: newer.attempt, changed: names.join(', ') }) : t('work.outputs.newerSame', { attempt: newer.attempt })}</span>
        {onRead && <Button variant="primary" className="min-h-0 px-2 py-0.5 text-small" onClick={() => onRead(newer.attempt)}>{t('work.outputs.newerRead', { attempt: newer.attempt })}</Button>}
      </p>
      <p className="m-0 text-meta text-ink-faint">{t('work.outputs.older', { attempt: answer.record.attempt, revision: answer.record.revision.slice(0, 7) })}</p>
    </div>
  );
}

/** A part's handback as a document: its note rendered, its requirements with their checks, *unchecked* where nothing checked its done. */
function HandbackDisplay({ answer, part }: { answer: RunReviewAnswer; part: string | null | undefined }) {
  const { t } = useTranslation();
  const handed = answer.record?.parts.find((each) => each.quest === part);
  if (!handed) return null;
  return (
    <div className="grid gap-1.5 px-3 pb-3">
      {handed.note && <Markdown text={handed.note} />}
      {handed.requirements.length > 0 && (
        <ul className="m-0 grid list-none gap-0.5 p-0 text-small">
          {handed.requirements.map((requirement) => (
            <li key={requirement.quote} className="text-ink-soft">{requirement.quote} · <span className="text-ink-faint">{requirement.check}</span></li>
          ))}
        </ul>
      )}
      {handed.unchecked && <p className="m-0 text-small text-warn">{t('work.outputs.unchecked')}</p>}
    </div>
  );
}

/** What an output of a display this window does not know holds, as it was given. */
function RawDisplay({ output }: { output: ReviewOutput }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-1 px-3 pb-3">
      <p className="m-0 text-small text-ink-faint">{t('work.outputs.raw')}</p>
      <pre className="m-0 overflow-x-auto rounded-control bg-raised p-2 font-mono text-meta text-ink-soft">{JSON.stringify(output, null, 2)}</pre>
    </div>
  );
}

/** The steps an Accept may start, each as it stands: dimmed until reached, its outcome and door once it has one. */
function AfterTheAccept({ steps }: { steps: RunReviewStep[] }) {
  const { t } = useTranslation();
  return (
    <section aria-label={t('work.outputs.afterAccept')} className="grid gap-1.5 px-3 py-3">
      <h3 className="m-0 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.outputs.afterAccept')}</h3>
      {steps.length === 0 ? <p className="m-0 text-small text-ink-soft">{t('work.outputs.followsNone')}</p> : (
        <>
          <p className="m-0 text-small text-ink-soft">{t('work.outputs.wouldFollow')}</p>
          <ul className="m-0 grid list-none gap-1 p-0 text-small">
            {steps.map((step) => (
              <li key={step.node} className={step.state === 'blocked' ? 'grid gap-0.5 opacity-60' : 'grid gap-0.5'}>
                <span className="text-ink">
                  {t('work.outputs.step', { node: step.node, state: t(`work.outputs.state.${step.state}`, { defaultValue: step.state }) })}
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
 * **A run's outputs**: what its review record presents under review at the revision read, each drawn by its display, then what an Accept
 * may start. The Accept itself is the card's, in the session's conversation; this view has none.
 *
 * @remarks
 * **Pinned.** Everything under review is the record's at the attempt read; a newer revision is a banner naming the outputs that moved, read
 * only on *Read rN*, and the revision picker reads any earlier one. **Displays** are a small vocabulary: a change set (commits and every
 * file's hunks), a document (a handback), a resource plan, and the raw fallback for one this window does not know.
 *
 * An organism's body with no bridge: the page hands it the answer and the reads.
 */
export function OutputsView({ answer, viewed, onRead, fileBody }: {
  answer: RunReviewAnswer;
  viewed: ViewedMarks;
  /** Read the record at an attempt: the newer one announced, or one picked. */
  onRead?: (attempt: number) => void;
  /** What a record's row draws once opened, where no patch was read. */
  fileBody?: (row: ReviewRow) => ReactNode;
}) {
  const { t } = useTranslation();
  const record = answer.record ?? null;
  const attempt = record?.attempt ?? answer.standing ?? 0;
  const latest = Math.max(attempt, answer.standing ?? 0, answer.newer?.attempt ?? 0);
  const outputs = [...(answer.outputs ?? [])].sort((one, two) => rank(one) - rank(two));
  const fileCount = answer.diff?.files.length ?? answer.files?.length ?? record?.rows.length ?? 0;

  const display = (output: ReviewOutput): ReactNode => {
    switch (output.display) {
      case 'change-set': return <ChangeSetDisplay answer={answer} viewed={viewed} fileBody={fileBody} />;
      case 'document': return <HandbackDisplay answer={answer} part={output.part} />;
      case 'resource-plan': return <PlanOperations system={answer.says?.system ?? record?.place ?? ''} operations={answer.operations} />;
      default: return <RawDisplay output={output} />;
    }
  };

  return (
    <section aria-label={t('work.outputs.title')} className="flex min-h-0 flex-col overflow-y-auto">
      <header className="sticky top-0 z-10 flex flex-wrap items-center gap-2 border-b border-line bg-page px-3 py-1.5">
        <h2 className="m-0 text-body font-semibold">{t('work.outputs.title')}</h2>
        {record && latest > 0 && (
          <select
            aria-label={t('work.outputs.revisionPick')}
            value={attempt}
            disabled={!onRead}
            onChange={(event) => onRead?.(Number(event.target.value))}
            className="ml-auto rounded-control border border-line-strong bg-raised px-1.5 py-0.5 font-mono text-meta text-ink"
          >
            {Array.from({ length: latest }, (_, at) => latest - at).map((each) => (
              <option key={each} value={each}>{t('work.outputs.revisionOption', { attempt: each })}{each === attempt ? ` · ${record.revision.slice(0, 7)}` : ''}</option>
            ))}
          </select>
        )}
      </header>
      <Newer answer={answer} onRead={onRead} />
      <section aria-label={t('work.outputs.underReview', { attempt })} className="grid border-b border-line">
        <h3 className="m-0 px-3 pt-2 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.outputs.underReview', { attempt })}</h3>
        {outputs.length === 0 && <p className="m-0 px-3 py-2 text-small text-ink-soft">{t('work.outputs.nothing')}</p>}
        {outputs.map((output) => (
          <article key={output.id} className="grid gap-1 border-t border-line first-of-type:border-t-0">
            <h4 className="m-0 flex flex-wrap items-baseline gap-2 px-3 pt-2 text-small font-semibold text-ink">
              {outputName(t, output)}
              {output.display === 'change-set' && (
                <span className="text-meta font-normal text-ink-faint">
                  {answer.commits ? `${t('work.outputs.commits', { count: answer.commits.commits.length })} · ` : ''}{t('work.outputs.files', { count: fileCount })}
                </span>
              )}
            </h4>
            {display(output)}
          </article>
        ))}
        {record && record.incomplete.length > 0 && (
          <div className="border-t border-line px-3 py-2 text-small">
            <p className="m-0 text-ink-soft">{t('work.outputs.incomplete')}</p>
            <ul className="m-0 list-disc pl-5 text-ink-faint">{record.incomplete.map((gap) => <li key={`${gap.code}:${gap.path ?? ''}`}><Inline text={gap.said} /></li>)}</ul>
          </div>
        )}
      </section>
      <AfterTheAccept steps={answer.follows ?? []} />
    </section>
  );
}
