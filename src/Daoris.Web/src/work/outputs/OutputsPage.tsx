import { type ReactNode, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReviewRow, RunReviewAnswer } from '../review/run/runReviewWire';
import { useRunReview, useRunReviewFile, useSessionRunReview } from '../../bridge/shell';
import { sentence } from '../../locales/format';
import { RecordFileView } from '../review/run/RecordFileView';
import { useViewedMarks } from '../review/run/viewedMarks';
import { OutputsView } from './OutputsView';

/** The attempt to read next: the one asked, or, asked for the newest, the one first answered, so a newer one is never swapped in. */
export function pinnedAttempt(asked: number, answer: RunReviewAnswer | undefined): number {
  return asked !== 0 ? asked : answer?.record?.attempt ?? 0;
}

/** A row's file at the record's two versions, compared line by line, or why it is not drawn. */
function RecordFileDiff({ run, attempt, row }: { run: string; attempt: number; row: ReviewRow }) {
  const { t } = useTranslation();
  const before = useRunReviewFile(run, attempt, row.path, 'before', { enabled: Boolean(row.before) });
  const after = useRunReviewFile(run, attempt, row.path, 'after', { enabled: Boolean(row.after) });
  const refused = (row.before ? before.data?.refused : null) ?? (row.after ? after.data?.refused : null);
  if (refused) return <p className="m-0 px-3 pb-2 text-small text-ink-faint">{t(`work.outputs.fileRefused.${refused}`, { defaultValue: refused })}</p>;
  if ((row.before && before.isPending) || (row.after && after.isPending)) {
    return <p className="m-0 px-3 pb-2 text-small text-ink-faint">{t('work.outputs.fileReading')}</p>;
  }
  return <RecordFileView path={row.path} before={before.data?.text ?? null} after={after.data?.text ?? null} />;
}

/**
 * **The Outputs view's page, `outputs/<run>`**: a run's outputs read by its run, or by the session in view; a session no run governs reads
 * its own changes (`fallback`). The attempt first answered is pinned: *Read rN* or the picker names another.
 */
export function OutputsPage({ run, session, attempt: opened = 0, fallback }: { run: string | null; session: string | null; attempt?: number; fallback?: ReactNode }) {
  const { t } = useTranslation();
  const bySession = useSessionRunReview(run ? null : session);
  const named = run ?? (bySession.data && !bySession.data.refused ? bySession.data.run : null);
  const [asked, setAsked] = useState(opened);
  useEffect(() => setAsked(opened), [named, opened]);
  const review = useRunReview(named, asked);
  const pinned = pinnedAttempt(asked, review.data);
  useEffect(() => { if (pinned !== asked) setAsked(pinned); }, [pinned, asked]);
  const viewed = useViewedMarks();

  // Until the session's read names its run, and where none governs it, the session's own changes stand.
  if (!named) return <>{fallback}</>;
  const answer = review.data;
  if (!answer) return <p className="m-0 p-3 text-small text-ink-faint">{review.error ? sentence(review.error) : t('work.outputs.reading')}</p>;
  if (answer.refused) return <p className="m-0 p-3 text-small text-ink-faint">{t('work.outputs.notRun')}</p>;
  const record = answer.record ?? null;
  return (
    <OutputsView
      answer={answer}
      viewed={viewed}
      onRead={setAsked}
      fileBody={record ? (row) => <RecordFileDiff run={record.run} attempt={record.attempt} row={row} /> : undefined}
    />
  );
}
