import { useTranslation } from 'react-i18next';
import { stamp } from '../format';
import { ExternalLink } from '../links';
import { Inline } from '../ui';
import type { LandedWork } from './diff';

/**
 * Where a landed session's work went, at the top of its review (REVIEW2, D113): the branch its landing made, when,
 * the pull request a plugin opened, and where that branch stands now.
 *
 * @remarks
 * **A landed session reads as landed.** Its landing tidied its tree away, so the review reads its changes from the
 * landed branch in the repository's own checkout, and says so; a branch gone since is said plainly, with what the
 * clean-up proved when it removed it and whether its work reads on the line now. Each state is its own sentence,
 * because each is a different fact about where the work is.
 *
 * **The host's words pass through where they are git's** (`detail`), since they name what git could not do.
 *
 * A molecule: the landing arrives as props.
 */
export function LandedNote({ landed, source = 'tree' }: {
  landed: LandedWork;
  /** Where the review's files were read from: the session's tree, or this branch. */
  source?: 'tree' | 'branch';
}) {
  const { t } = useTranslation();
  const reads = landed.reads;
  const removed = landed.removed;

  const sentences: string[] = [];
  if (landed.state === 'standing') {
    if (landed.detail) sentences.push(t('work.review.landed.unreadable', { detail: landed.detail }));
    else if (source === 'branch') sentences.push(t('work.review.landed.fromBranch', { repository: landed.repository }));
    else sentences.push(t('work.review.landed.treeStays'));
  } else if (landed.state === 'gone') {
    sentences.push(t('work.review.landed.gone', { repository: landed.repository }));
    if (removed) {
      sentences.push(removed.kind === 'inside'
        ? t('work.review.landed.removedInside', { where: removed.where ?? '' })
        : t('work.review.landed.removed', { where: removed.where ?? '' }));
    }
    if (reads?.kind === 'on-line' || reads?.kind === 'merged') {
      sentences.push(t('work.review.landed.readsOnLine', { where: reads.where ?? '' }));
    } else if (reads?.kind === 'differs') {
      const shown = reads.files.slice(0, 3).join(', ') + (reads.files.length > 3 ? ' …' : '');
      sentences.push(t('work.review.landed.differs', { count: reads.files.length, where: reads.where ?? '', files: shown }));
    } else if (reads?.kind === 'commits-gone') {
      sentences.push(t('work.review.landed.commitsGone'));
    } else if (reads) {
      sentences.push(t('work.review.landed.unknown', { detail: reads.detail ?? '' }));
    }
  } else if (landed.state === 'not-ours') {
    sentences.push(t('work.review.landed.notOurs'));
  } else if (landed.state === 'no-checkout') {
    sentences.push(t('work.review.landed.noCheckout', { repository: landed.repository }));
  }

  return (
    <section
      aria-label={t('work.review.landed.label')}
      // A branch name is one long word: it wraps anywhere rather than widening the side bar.
      className="grid shrink-0 gap-1 border-b border-line border-l-[3px] border-l-accent px-3 py-2 [overflow-wrap:anywhere]"
    >
      <p className="m-0 text-small text-ink">
        <Inline text={t('work.review.landed.on', { branch: landed.branch })} />
        {landed.landedAt && (
          <>
            {' '}
            <time dateTime={landed.landedAt} className="text-meta tabular-nums text-ink-faint">{stamp(landed.landedAt)}</time>
          </>
        )}
      </p>
      {/* One paragraph: the sentences are one account of where the work is. */}
      {sentences.length > 0 && <p className="m-0 text-small text-ink-soft"><Inline text={sentences.join(' ')} /></p>}
      {landed.pushed && landed.pullRequest && (
        <p className="m-0 text-small">
          <ExternalLink href={landed.pullRequest} className="text-accent underline underline-offset-2">
            {t('work.review.pullRequest')}
          </ExternalLink>
        </p>
      )}
    </section>
  );
}
