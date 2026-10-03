import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence, span } from '../format';
import { cn } from '../lib/cn';
import { Button, EmptyState, type IconName, Segmented } from '../ui';
import { type DiffFile, REVIEW_BOUND_MINUTES, type ReviewKnown } from './diff';
import { DiffFileRow } from './DiffFileRow';
import type { DiffLayout } from './PatchView';

// The review's frame (REVIEW4): what the page knows of a session while git reads its range, the skeleton of what is
// coming, its words with how long, and a refusal worded from its code with the person's next move. Molecules: every
// state arrives as props, so a read of a minute and each refusal are reached without a bridge (components plan §2).

/** How long a read runs before the review says for how long: a moment passes unremarked, a wait does not. */
export const SLOW_MS = 2_000;

/**
 * Milliseconds since `since`, counted once a second while it is set; null where nothing is being read.
 *
 * @remarks React's own clock, not data: the molecule stays inside the presentational boundary. A start handed in from
 * before this pane appeared (a return to a read still running) counts from that start.
 */
function useElapsed(since: number | null | undefined): number | null {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    if (since === null || since === undefined) return;
    setNow(Date.now());
    const timer = setInterval(() => setNow(Date.now()), 1_000);
    return () => clearInterval(timer);
  }, [since]);
  return since === null || since === undefined ? null : Math.max(0, now - since);
}

/** Words that say what is being read, and, past {@link SLOW_MS}, for how long — whole seconds, so the count steps once a second. */
function Reading({ words, since, className }: { words: string; since?: number | null; className?: string }) {
  const { i18n } = useTranslation();
  const elapsed = useElapsed(since);
  return (
    <span role="status" className={className}>
      {words}
      {elapsed !== null && elapsed >= SLOW_MS && (
        <>
          {' '}
          <span className="tabular-nums">{span(Math.floor(elapsed / 1000) * 1000, i18n.language)}</span>
        </>
      )}
    </span>
  );
}

/**
 * The review's head: what the record says, there from the first frame (REVIEW4) — the session's title, its repository,
 * its own branch, the commit it is measured from and the commits it reported.
 *
 * @remarks
 * **The range is stated** (SURF6): a review that does not say what it is measured from is an opinion. The record's
 * base stands until the answer names the one git measured from (WSR6 may have moved it), which the pane hands in.
 *
 * **A newer read in flight is said here**, beside the facts, while the last answer below it is held dimmed (D41's
 * loading rule): the head is the one line every loaded state shares.
 */
export function ReviewHead({ title, repository, branch, base, commits, refreshing }: ReviewKnown & {
  /** When a newer read began, while one is on its way and the last answer is still shown. */
  refreshing?: number | null;
}) {
  const { t } = useTranslation();
  const facts = [
    repository ? <span key="repository" className="text-ink-soft">{repository}</span> : null,
    branch ? <span key="branch" className="font-mono wrap-anywhere">{branch}</span> : null,
    base ? <span key="base" className="font-mono">{t('work.review.since', { base: base.slice(0, 8) })}</span> : null,
    commits ? <span key="commits" className="tabular-nums">{t('work.review.commits', { count: commits })}</span> : null,
  ].filter((fact) => fact !== null);
  const reading = refreshing !== null && refreshing !== undefined;
  if (!title && facts.length === 0 && !reading) return null;

  return (
    <header className="grid shrink-0 gap-0.5 border-b border-line px-3 py-2">
      {/* Two lines at most, whole on hover: a quest's title can run to a paragraph, as the session's head holds it. */}
      {title && <p className="m-0 line-clamp-2 text-small font-semibold text-ink" title={title}>{title}</p>}
      {(facts.length > 0 || reading) && (
        <p className="m-0 flex flex-wrap items-baseline gap-x-1.5 gap-y-0.5 text-meta text-ink-faint">
          {facts.flatMap((fact, index) => (index === 0
            ? [fact]
            : [<span key={`between-${index}`} aria-hidden>·</span>, fact]))}
          {reading && <Reading words={t('work.review.readingAgain')} since={refreshing} className="ml-auto" />}
        </p>
      )}
    </header>
  );
}

/**
 * What the review read: the count and how many the reader has viewed, the layout, every file in one scroll (a
 * multibuffer, IDE study §2) and the host's bound, said as it said it. The reader's marks arrive as props: which file
 * is open and which viewed are the pane's, kept per session and never written down.
 */
export function ReviewFiles({ files, open, viewed, layout, onLayout, onToggle, onViewed, onPreview, truncated }: {
  files: DiffFile[];
  open: Record<string, boolean>;
  viewed: Record<string, boolean>;
  layout: DiffLayout;
  onLayout: (layout: DiffLayout) => void;
  onToggle: (path: string) => void;
  onViewed: (path: string, viewed: boolean) => void;
  /** Opens one file's preview in the side bar (PREVIEW1, D111). */
  onPreview?: (path: string) => void;
  /** What the bound dropped, in the host's words. */
  truncated?: string | null;
}) {
  const { t } = useTranslation();
  const done = files.filter((file) => viewed[file.path]).length;
  return (
    <>
      <header className="flex shrink-0 flex-wrap items-baseline gap-x-2 gap-y-1 border-b border-line px-3 py-1.5">
        <span className="text-meta uppercase tracking-[0.06em] text-ink-faint">
          {t('work.review.files', { count: files.length })}
        </span>
        {done > 0 && (
          <span className="text-meta tabular-nums text-ink-faint">
            {t('work.review.progress', { done, total: files.length })}
          </span>
        )}
        {/* The range it is measured from is the head's, above: said once, from the first frame (REVIEW4). */}
        <span className="ml-auto">
          <Segmented
            label={t('work.review.layout')}
            value={layout}
            options={[
              { value: 'unified', label: t('work.review.unified') },
              { value: 'split', label: t('work.review.split') },
            ]}
            onChange={onLayout}
          />
        </span>
      </header>

      <ul className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-0">
        {files.map((file) => (
          <DiffFileRow
            key={file.path}
            file={file}
            open={open[file.path] ?? false}
            viewed={viewed[file.path] ?? false}
            layout={layout}
            onToggle={() => onToggle(file.path)}
            onViewed={(next) => onViewed(file.path, next)}
            onPreview={onPreview ? () => onPreview(file.path) : undefined}
          />
        ))}
      </ul>

      {/* The bound is the host's sentence, shown rather than summarised — it names where the rest is. */}
      {truncated && (
        <p className="m-0 shrink-0 border-t border-line px-3 py-2 text-meta text-ink-faint">{truncated}</p>
      )}
    </>
  );
}

/** One placeholder's tone: static two-tone, never a shimmer (D41 §3), as `SkeletonRows` draws. */
const BAR = 'block shrink-0 rounded-[4px] bg-accent-soft opacity-55';

/** The paths' widths, so the rows read as a list of files rather than a block. */
const PATHS = ['62%', '48%', '74%', '40%', '56%'];

/** The first file's patch: a gutter and its lines. */
const LINES = ['78%', '64%', '86%', '52%', '70%', '44%'];

/**
 * What a review will hold, drawn before it holds it (REVIEW4): file rows with their mark, path and counts, and the
 * first opened on a patch. For the eye alone; the words above it are what a reader hears.
 */
export function ReviewSkeleton() {
  return (
    <div data-skeleton="review" aria-hidden="true" className="min-h-0 flex-1 overflow-hidden">
      {PATHS.map((path, index) => (
        <div key={index} data-skeleton="file" className="border-b border-line">
          <div className="flex items-center gap-2 px-3 py-2">
            <i className={cn(BAR, 'h-3 w-3')} />
            <i className={cn(BAR, 'h-3 w-2.5')} />
            <i className={cn(BAR, 'h-3')} style={{ width: path }} />
            <i className={cn(BAR, 'ml-auto h-3 w-10')} />
          </div>
          {index === 0 && (
            <div data-skeleton="patch" className="grid gap-1.5 border-t border-line px-3 py-2">
              {LINES.map((line, at) => (
                <div key={at} className="flex items-center gap-3">
                  <i className={cn(BAR, 'h-2.5 w-5')} />
                  <i className={cn(BAR, 'h-2.5')} style={{ width: line }} />
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

/**
 * A first read on its way (REVIEW4, D41's loading rule): its words on the line the count will take — what it is
 * reading and, after a moment, for how long — and the files and the patch as a skeleton beneath. Never a spinner in
 * the middle of a blank pane.
 */
export function ReviewReading({ repository, since }: {
  repository?: string | null;
  /** When the read began: the host's one read for this session, which a return to the review waits on too. */
  since?: number | null;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <p className="m-0 shrink-0 border-b border-line px-3 py-1.5 text-meta text-ink-soft">
        <Reading words={repository ? t('work.review.reading', { repository }) : t('work.review.readingAny')} since={since} />
      </p>
      <ReviewSkeleton />
    </div>
  );
}

/**
 * Each code the review's route refuses with (`DriverModule.Trees.cs`) and the bridge's own, with its glyph and whether
 * reading again can change it (REVIEW4). A record with no tree here, a tree gone with no landing, a record with no range
 * are facts a second read answers the same; git failing, a driver still starting and a wait that ran out are not.
 */
export const REVIEW_REFUSALS = {
  SESSION_NOT_REVIEWABLE: { icon: 'monitor', again: false },
  SESSION_TREE_GONE: { icon: 'diff', again: false },
  SESSION_NO_BASE: { icon: 'info', again: false },
  SESSION_RANGE_UNREADABLE: { icon: 'failure', again: true },
  DRIVER_NOT_READY: { icon: 'refresh', again: true },
  TIMEOUT: { icon: 'failure', again: true },
} as const satisfies Record<string, { icon: IconName; again: boolean }>;

type ReviewRefusal = keyof typeof REVIEW_REFUSALS;

const isReviewRefusal = (code: unknown): code is ReviewRefusal =>
  typeof code === 'string' && Object.prototype.hasOwnProperty.call(REVIEW_REFUSALS, code);

/**
 * Why there is no review, and what the person can do next (REVIEW4): what happened as a headline, why in the code's
 * own sentence (the catalogue's, so 中文 reads it too), and the next move — where the work is, what reads it instead, or
 * *Read again* where a second read can answer otherwise.
 *
 * @remarks
 * **Never a blank, never a raw message.** A code this page does not know yet is the catalogue's *refused without
 * saying why* and may be read again; an error with no code at all is the catalogue's *failed without saying why*,
 * since its message is a program's words, not Daoris's.
 */
export function ReviewFailed({ error, machine, onRetry, retrying = false }: {
  error: unknown;
  /** The machine the session ran on, where the record says it was not this one. */
  machine?: string | null;
  onRetry?: () => void;
  /** A read again is on its way. */
  retrying?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const raw = (error as { code?: unknown } | null)?.code;
  const code = isReviewRefusal(raw) ? raw : null;
  const look = code ? REVIEW_REFUSALS[code] : { icon: 'failure' as const, again: true };
  const why = typeof raw === 'string' && raw ? sentence(error, i18n.language) : t('errors.UNKNOWN_ERROR');
  const next = code === 'SESSION_NOT_REVIEWABLE' && machine
    ? t('work.review.failed.machine', { machine })
    : t(`work.review.failed.next.${code ?? 'other'}`, { minutes: REVIEW_BOUND_MINUTES });

  return (
    <EmptyState
      icon={look.icon}
      headline={t(`work.review.failed.headline.${code ?? 'other'}`)}
      body={t('work.review.failed.join', { first: why, second: next })}
      action={look.again && onRetry
        ? <Button disabled={retrying} onClick={onRetry}>{t('work.review.readAgain')}</Button>
        : undefined}
    />
  );
}
