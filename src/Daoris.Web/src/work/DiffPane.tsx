import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { useSessionDiff } from '../shell';
import { EmptyState, SkeletonRows } from '../ui';
import { DiffFileRow } from './DiffFileRow';

/**
 * Review: what the session actually did (SURF6, design §5).
 *
 * @remarks
 * **A multibuffer, not a tree beside a pane** (IDE study §2): every changed file is one row in one
 * scroll and opens in place. That is the shape that makes a forty-file review finishable, and the
 * per-file *viewed* mark is the other half of it.
 *
 * **It is read-only, and deliberately so.** The evidence string said that work happened; this says
 * what it was. The two acts on a reviewed session — accepting it and sending it back — are the
 * person's and live outside this pane, so a surface built to *show* the work cannot change it.
 *
 * **A refusal renders verbatim.** "No tree here", "no range recorded" and "git could not read it"
 * are three different facts about why there is nothing to show, and only the host knows which.
 *
 * Desktop-only, structurally: the hook is gated on the bridge, so in a browser this never asks.
 */
export function DiffPane({ session }: { session: string | null }) {
  const { t } = useTranslation();
  const diff = useSessionDiff(session);

  // Per-reader, per-session, and never written down: which files this person has opened and which
  // they have ticked off. Keyed by path, reset by attending a different session.
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [viewed, setViewed] = useState<Record<string, boolean>>({});
  const [shown, setShown] = useState<string | null>(null);
  if (shown !== session) {
    setShown(session);
    setOpen({});
    setViewed({});
  }

  if (!session) {
    return (
      <EmptyState
        icon="diff"
        headline={t('work.review.none.headline')}
        body={t('work.review.none.body')}
      />
    );
  }

  if (diff.isPending) return <div className="p-3"><SkeletonRows rows={4} /></div>;

  if (diff.error) {
    return (
      <p className="m-0 px-3 py-4 text-small text-ink-soft">{sentence(diff.error)}</p>
    );
  }

  const files = diff.data?.files ?? [];
  const done = files.filter((file) => viewed[file.path]).length;

  if (files.length === 0) {
    return (
      <EmptyState
        icon="check"
        headline={t('work.review.empty.headline')}
        body={t('work.review.empty.body')}
      />
    );
  }

  return (
    <section className="flex min-h-0 flex-col">
      <header className="flex shrink-0 items-baseline gap-2 border-b border-line px-3 py-1.5">
        <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">
          {t('work.review.files', { count: files.length })}
        </span>
        {done > 0 && (
          <span className="text-meta tabular-nums text-ink-faint">
            {t('work.review.progress', { done, total: files.length })}
          </span>
        )}
        {/* The range, stated: a review that does not say what it is measured from is an opinion. */}
        <span className="ml-auto truncate font-mono text-meta text-ink-faint">
          {t('work.review.since', { base: diff.data!.base.slice(0, 8) })}
        </span>
      </header>

      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {files.map((file) => (
          <DiffFileRow
            key={file.path}
            file={file}
            open={open[file.path] ?? false}
            viewed={viewed[file.path] ?? false}
            onToggle={() => setOpen((was) => ({ ...was, [file.path]: !was[file.path] }))}
            onViewed={(next) => setViewed((was) => ({ ...was, [file.path]: next }))}
          />
        ))}
      </ul>

      {/* The bound is the host's sentence, shown rather than summarised — it names where the rest is. */}
      {diff.data?.truncated && (
        <p className="m-0 shrink-0 border-t border-line px-3 py-2 text-meta text-ink-faint">
          {diff.data.truncated}
        </p>
      )}
    </section>
  );
}
