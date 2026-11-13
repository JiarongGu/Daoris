import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ReviewRow, RunReviewAnswer } from '../review/run/runReviewWire';
import { PathText } from '../../design/ui';
import { cn } from '../../lib/cn';
import type { DiffFile } from '../review/diff';
import type { DiffLayout } from '../review/PatchView';
import { ReviewFiles } from '../review/ReviewFrame';
import { RecordRows, type ViewedMarks } from '../review/run/ReviewChanges';
import { contentOf } from '../review/run/viewedMarks';

/** Past this many changed lines a file's hunks stay folded until opened, as a viewed one does. */
const FOLDED_PAST = 400;

const folded = (file: DiffFile) => file.patch === null || (file.added ?? 0) + (file.removed ?? 0) > FOLDED_PAST;

/** The commits, newest first: pressing one narrows the files to its own, pressing it again shows them all. */
function Commits({ answer, only, onOnly }: { answer: RunReviewAnswer; only: string | null; onOnly: (sha: string | null) => void }) {
  const { t } = useTranslation();
  const commits = answer.commits?.commits ?? [];
  if (commits.length === 0) return null;
  return (
    <section aria-label={t('work.outputs.commits', { count: commits.length })} className="border-b border-line px-3 py-1.5">
      <p className="m-0 flex items-baseline gap-2 text-meta uppercase tracking-[0.06em] text-ink-faint">
        {t('work.outputs.commits', { count: commits.length })}
        {only && (
          <button type="button" onClick={() => onOnly(null)} className="cursor-pointer border-0 bg-transparent p-0 text-meta normal-case tracking-normal text-accent underline">
            {t('work.outputs.commitAll')}
          </button>
        )}
      </p>
      <ul className="m-0 grid list-none gap-0.5 p-0">
        {commits.map((commit) => (
          <li key={commit.sha}>
            <button
              type="button"
              aria-pressed={only === commit.sha}
              title={t('work.outputs.commitFilter', { sha: commit.sha.slice(0, 7) })}
              onClick={() => onOnly(only === commit.sha ? null : commit.sha)}
              className={cn('flex w-full min-w-0 cursor-pointer items-baseline gap-2 rounded-control border-0 bg-transparent px-1 py-0.5 text-left text-small text-ink hover:bg-raised', only === commit.sha && 'bg-accent-soft')}
            >
              <span className="shrink-0 font-mono text-meta text-ink-faint">{commit.sha.slice(0, 7)}</span>
              <span className="min-w-0 truncate">{commit.subject}</span>
              <span className="ml-auto shrink-0 text-meta text-ink-faint">{t('work.outputs.files', { count: commit.files.length })}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}

/**
 * **The change set**: the commits, then every file's hunks in one scroll (`ReviewFiles`, unified or side by side), each marked *Viewed*
 * by its patch's content; a viewed file and one past the bound stay folded until opened, and what the work left uncommitted is named apart.
 * Where no patch was read (a folder's work, or a checkout not on this machine) the record's rows stand, each opened on `fileBody`.
 *
 * A molecule: the answer, the marks and the row body arrive as props.
 */
export function ChangeSetDisplay({ answer, viewed, fileBody }: { answer: RunReviewAnswer; viewed: ViewedMarks; fileBody?: (row: ReviewRow) => ReactNode }) {
  const { t } = useTranslation();
  const [only, setOnly] = useState<string | null>(null);
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [layout, setLayout] = useState<DiffLayout>('unified');
  const diff = answer.diff ?? null;
  const kind = answer.record?.kind ?? answer.says?.kind;
  if (!diff) {
    const rows = kind === 'branch' || kind === 'checkout' ? answer.files ?? [] : answer.record?.rows ?? [];
    return <RecordRows rows={rows} viewed={viewed} body={fileBody} />;
  }

  const narrowed = only ? new Set(answer.commits?.commits.find((commit) => commit.sha === only)?.files ?? []) : null;
  const files = narrowed ? diff.files.filter((file) => narrowed.has(file.path)) : diff.files;
  const content = Object.fromEntries(diff.files.map((file) => [file.path, contentOf(file)]));
  const seen = Object.fromEntries(diff.files.map((file) => [file.path, viewed.isViewed(file.path, content[file.path] ?? '')]));
  const opened = Object.fromEntries(diff.files.map((file) => [file.path, open[file.path] ?? !(seen[file.path] || folded(file))]));
  const uncommitted = answer.commits?.uncommitted ?? [];
  return (
    <div className="grid">
      <Commits answer={answer} only={only} onOnly={setOnly} />
      <ReviewFiles
        files={files}
        open={opened}
        viewed={seen}
        layout={layout}
        onLayout={setLayout}
        onToggle={(path) => setOpen((was) => ({ ...was, [path]: !opened[path] }))}
        onViewed={(path, next) => {
          viewed.mark(path, content[path] ?? '', next);
          setOpen((was) => ({ ...was, [path]: !next }));
        }}
        truncated={diff.truncated}
      />
      {uncommitted.length > 0 && (
        <section aria-label={t('work.outputs.uncommitted')} className="border-t border-line px-3 py-1.5">
          <p className="m-0 text-meta uppercase tracking-[0.06em] text-ink-faint">{t('work.outputs.uncommitted')}</p>
          <ul className="m-0 list-none p-0">
            {uncommitted.map((path) => <li key={path}><PathText path={path} className="text-small" /></li>)}
          </ul>
        </section>
      )}
    </div>
  );
}
