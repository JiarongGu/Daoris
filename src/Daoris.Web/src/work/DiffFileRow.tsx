import { useTranslation } from 'react-i18next';
import type { DiffFile } from './diff';
import { Button, CheckField, Icon, Inline, Tip } from '../ui';
import { cn } from '../lib/cn';
import { type DiffLayout, PatchView } from './PatchView';

// One file in a review (SURF6). A molecule: it is handed a file and told whether it is open and
// whether it has been seen, so every state — binary, dropped patch, a 4,000-line rename — is
// reachable by passing props (components plan §2).

/** How a status reads as a mark. Never colour alone (D41 §6) — the letter is the label. */
const MARK: Record<string, { letter: string; tone: string }> = {
  added: { letter: 'A', tone: 'text-ink-done' },
  modified: { letter: 'M', tone: 'text-ink-taken' },
  deleted: { letter: 'D', tone: 'text-ink-danger' },
  renamed: { letter: 'R', tone: 'text-ink-open' },
  copied: { letter: 'C', tone: 'text-ink-open' },
};

/**
 * One changed file, with its patch beneath it.
 *
 * @remarks
 * **This is a multibuffer row, not a tree node** (IDE study §2): every file is in one scroll and
 * opens in place, because a tree beside a pane is what makes a forty-file review unfinishable.
 *
 * **`viewed` is the person's own mark**, not a state of the work. GitHub's per-file "viewed" is the
 * single thing that makes a long diff finishable, and it is cheap — one boolean per path, held by
 * the reader, never written to the record.
 *
 * **A dropped patch says so.** The bound is the host's (design §5) and the file is still listed,
 * because "this changed and you cannot read it here" is information and an absent row is not.
 *
 * **Its patch is drawn by `PatchView`** (REVIEW2): numbered, highlighted in the file's language, and
 * in the layout the pane chose — unified, or the old side beside the new.
 */
export function DiffFileRow({ file, open, viewed, layout = 'unified', onToggle, onViewed, onPreview }: {
  file: DiffFile;
  open: boolean;
  viewed: boolean;
  /** Unified, or side by side — the pane's choice, the same for every file in it. */
  layout?: DiffLayout;
  onToggle: () => void;
  onViewed: (viewed: boolean) => void;
  /**
   * Opens the file itself in the side bar's preview (PREVIEW1, D111). Absent where nothing opens one; and
   * a deleted file offers none, since it is not in the tree any more (UX5 U66).
   */
  onPreview?: () => void;
}) {
  const { t } = useTranslation();
  const mark = MARK[file.status] ?? { letter: '?', tone: 'text-ink-faint' };
  const previewable = Boolean(onPreview) && file.status !== 'deleted';

  return (
    <li className={cn('border-b border-line last:border-b-0', viewed && 'opacity-55')}>
      <div className="flex items-center gap-2 px-3 py-1">
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={open}
          className="flex min-w-0 flex-1 items-center gap-2 text-left"
        >
          <Icon name={open ? 'chevronDown' : 'chevronRight'} size={13} />
          <span
            aria-label={t(`work.review.status.${file.status}`)}
            className={cn('shrink-0 font-mono text-meta', mark.tone)}
          >
            {mark.letter}
          </span>
          {/* The path is the identity, so it truncates at the FRONT — the filename is the half a
              person is looking for, and a long directory would push it off the row. */}
          <span dir="rtl" className="truncate font-mono text-small text-ink">
            {file.path}
          </span>
        </button>

        {previewable && (
          <Tip content={t('work.preview.open', { path: file.path })}>
            <Button
              variant="ghost"
              aria-label={t('work.preview.open', { path: file.path })}
              onClick={onPreview}
              className="h-6 w-6 shrink-0 justify-center px-0"
            >
              <Icon name="read" size={13} />
            </Button>
          </Tip>
        )}

        <span className="shrink-0 font-mono text-meta tabular-nums">
          {file.added === null || file.removed === null
            // A binary file was not counted, which is not the same as counting zero.
            ? <span className="text-ink-faint">{t('work.review.binary')}</span>
            : (
              <>
                <span className="text-ink-done">+{file.added}</span>
                {' '}
                <span className="text-ink-danger">−{file.removed}</span>
              </>
            )}
        </span>

        {/* The platform's own box (UX5 U5): a native one wore the OS's accent. */}
        <CheckField
          checked={viewed}
          onChange={onViewed}
          label={t('work.review.viewed')}
          className="shrink-0 gap-1.5 text-meta text-ink-faint"
        />
      </div>

      {open && (
        file.patch
          ? (
            <div className="border-t border-line">
              <PatchView patch={file.patch} path={file.path} layout={layout} />
            </div>
          )
          : (
            <p className="m-0 border-t border-line px-3 py-2 text-small text-ink-faint">
              <Inline text={t('work.review.noPatch')} />
            </p>
          )
      )}
    </li>
  );
}
