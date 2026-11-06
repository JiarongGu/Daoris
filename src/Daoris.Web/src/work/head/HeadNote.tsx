import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Session } from '../../data/api';
import { Button, PathText } from '../../design/ui';
import { DiscardBranchAsk, type SweepBranch } from '../../settings/Sweep';
import type { Answered } from '../InlineConfirm';
import { Note } from '../Note';
import { noteBlocks, noteLines } from '../noteLines';

/** How much of the record's note the head shows before the rest is a press away. */
const NOTE_LINES = 3;

/**
 * The record's note about how the session stands (LANG1b): Daoris's lines worded in the reader's language, someone's
 * words as written, a record from before as it was kept. Long is judged on what is shown, a block at least a line each.
 */
export function Said({ parts }: { parts?: Session['noteParts'] }) {
  const { t, i18n } = useTranslation();
  const [whole, setWhole] = useState(false);
  const blocks = noteBlocks(t, noteLines(t, { parts }, i18n.language));
  const shown = blocks.reduce((count, block) => count + block.text.split('\n').length, 0);
  const long = blocks.reduce((count, block) => count + block.text.length, 0) > 280 || shown > NOTE_LINES;
  return (
    <div className="grid justify-items-start gap-1">
      <Note
        parts={parts}
        clamp={whole ? undefined : NOTE_LINES}
        className="w-full text-body leading-relaxed text-ink-soft"
      />
      {long && (
        <Button variant="ghost" className="px-0 py-0 text-small" onClick={() => setWhole((was) => !was)}>
          {t(whole ? 'work.head.noteLess' : 'work.head.noteMore')}
        </Button>
      )}
    </div>
  );
}

/**
 * What its own tree left, and whether the person has anything to do about it (SESS1 S10, SESS2 H4). A failed or superseded
 * attempt's branch whose tree is gone is discarded here (LAND3b, D102's LAND3 note), asking once under the line: no clean-up
 * takes its commits, and the review has no tree left to discard. While the tree is here, the review's Discard serves.
 */
export function Left({ branch, onReview, onDiscard, discarding = false }: {
  branch: SweepBranch;
  onReview?: () => void;
  onDiscard?: (answered: Answered) => void;
  discarding?: boolean;
}) {
  const { t } = useTranslation();
  const [asking, setAsking] = useState(false);
  const theirs = branch.kind === 'unlanded' || branch.kind === 'dirty';
  const discardable = Boolean(onDiscard && !branch.hasTree && branch.discardable);
  return (
    <>
      <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small">
        <span className="text-ink-faint">{t('work.head.itsWork')}</span>
        <span className={theirs ? 'text-ink-open' : 'text-ink-soft'}>{landed(t, branch)}</span>
        {/* Whole, as a branch row's name is (UXFIX4b): it wraps after its separators on this line's own row, never cut. */}
        <PathText path={branch.branch} className="min-w-0 text-meta text-ink-faint" />
        {theirs && onReview && (
          <Button className="px-2 py-0.5 text-small" onClick={onReview}>{t('work.head.review')}</Button>
        )}
        {discardable && !asking && (
          <Button variant="danger" className="px-2 py-0.5 text-small" disabled={discarding} onClick={() => setAsking(true)}>
            {t('settings.sweep.discard')}
          </Button>
        )}
      </p>
      {/* Open until the discard answers, a branch the driver kept said inside it (UXFIX2). */}
      {discardable && asking && (
        <DiscardBranchAsk branch={branch} busy={discarding} onDiscard={onDiscard!} onClose={() => setAsking(false)} />
      )}
    </>
  );
}

/** Whether a session's branch landed, in a few words for the head (D88's proof). */
function landed(t: (key: string, options?: Record<string, unknown>) => string, branch: SweepBranch): string {
  switch (branch.kind) {
    case 'empty': return t('work.head.landed.empty', { line: branch.where ?? '' });
    case 'landed': return branch.where ? t('work.head.landed.on', { where: branch.where }) : t('work.head.landed.somewhere');
    case 'unlanded': return t('work.head.landed.not', { count: branch.commits });
    case 'dirty': return t('work.head.landed.dirty');
    default: return t('work.head.landed.inUse');
  }
}
