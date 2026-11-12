import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReviewRow, RunReviewAnswer } from './runReviewWire';
import { useOverruleSupervisor } from '../../../bridge/sessions/supervision';
import { useRunNodeWord, useRunReview, useRunReviewFile, useSessionRunReview } from '../../../bridge/shell';
import { sentence } from '../../../locales/format';
import { RecordFileView } from './RecordFileView';
import { RunReview } from './RunReview';
import { useViewedMarks } from './viewedMarks';

/** A row's file at the record's two versions, compared line by line (a Markdown file rendered), or why it is not drawn. */
function RecordFileDiff({ run, attempt, row }: { run: string; attempt: number; row: ReviewRow }) {
  const { t } = useTranslation();
  const before = useRunReviewFile(run, attempt, row.path, 'before', { enabled: Boolean(row.before) });
  const after = useRunReviewFile(run, attempt, row.path, 'after', { enabled: Boolean(row.after) });
  const refused = (row.before ? before.data?.refused : null) ?? (row.after ? after.data?.refused : null);
  if (refused) return <p className="m-0 px-3 pb-2 text-small text-ink-faint">{t(`work.runReview.fileRefused.${refused}`, { defaultValue: refused })}</p>;
  if ((row.before && before.isPending) || (row.after && after.isPending)) {
    return <p className="m-0 px-3 pb-2 text-small text-ink-faint">{t('work.runReview.fileReading')}</p>;
  }
  return <RecordFileView path={row.path} before={before.data?.text ?? null} after={after.data?.text ?? null} />;
}

/** The bar's *Overrule*, through the session the supervisor's decision names. */
function useOverrule(answer: RunReviewAnswer | undefined) {
  const overrule = useOverruleSupervisor(answer?.supervisor?.session ?? '');
  const [said, setSaid] = useState<string | null>(null);
  return {
    busy: overrule.isPending,
    said,
    press: answer?.supervisor ? (decision: string) => overrule.mutate(decision, {
      onSuccess: (answered) => setSaid(answered.done ? null : answered.state),
      onError: (error) => setSaid(sentence(error)),
    }) : undefined,
  };
}

/**
 * **The review page, `review/<run>`**: a run's review read by its run, or by the session in view; a session no run governs reads its own
 * changes (`fallback`). The attempt read is never swapped under the reader: *Read it* names the newer one.
 */
export function RunReviewPage({ run, session, fallback }: { run: string | null; session: string | null; fallback?: ReactNode }) {
  const { t } = useTranslation();
  const bySession = useSessionRunReview(run ? null : session);
  const named = run ?? (bySession.data && !bySession.data.refused ? bySession.data.run : null);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => setAttempt(0), [named]);
  const review = useRunReview(named, attempt);
  const word = useRunNodeWord();
  const viewed = useViewedMarks();
  const overrule = useOverrule(review.data);

  // Until the session's read names its run, and where none governs it, the session's own changes stand.
  if (!named) return <>{fallback}</>;
  const answer = review.data;
  if (!answer) {
    return <p className="m-0 p-3 text-small text-ink-faint">{review.error ? sentence(review.error) : t('work.runReview.reading')}</p>;
  }
  if (answer.refused) return <p className="m-0 p-3 text-small text-ink-faint">{t('work.runReview.notRun')}</p>;
  const record = answer.record ?? null;
  return (
    <RunReview
      answer={answer}
      viewed={viewed}
      onWord={(ask) => word.mutateAsync(ask)}
      onRead={setAttempt}
      onOverrule={overrule.press}
      overruling={overrule.busy}
      overruleSaid={overrule.said}
      fileBody={record ? (row) => <RecordFileDiff run={record.run} attempt={record.attempt} row={row} /> : undefined}
    />
  );
}
