import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { type LandingConflict, type LandsCommit, type LeftOutCommit, useSay } from '../bridge/shell';
import { Button, Inline } from '../design/ui';

/** Why one commit is left out, in the page's words. */
const whyOf = (t: TFunction, commit: LeftOutCommit) => {
  if (commit.why === 'content') return t('work.review.leftOut.content');
  if (commit.why === 'empty') return t('work.review.leftOut.empty');
  return commit.on ? t('work.review.leftOut.patch', { commit: commit.on }) : t('work.review.leftOut.patchUnpaired');
};

/**
 * What a press would leave out because the line holds it, and how many it would replay onto the line (LANDSYNC1): said before the
 * press in the review's foot and in the head's ask. Nothing where the line holds none of the tree's commits, which land as made.
 */
export function LeftOutLines({ leftOut, lands }: { leftOut?: LeftOutCommit[] | null; lands?: LandsCommit[] | null }) {
  const { t } = useTranslation();
  if (!leftOut?.length) return null;
  return (
    <div className="grid gap-1 text-small text-ink-soft">
      <p className="m-0">{t('work.review.leavesOut', { count: leftOut.length })}</p>
      <ul className="m-0 grid gap-0.5 pl-4">
        {leftOut.map((each) => (
          <li key={each.commit}>
            <code className="text-meta">{each.commit}</code> {each.subject}
            {' · '}<Inline text={whyOf(t, each)} />
          </li>
        ))}
      </ul>
      {lands && lands.length > 0 && <p className="m-0 text-ink-faint">{t('work.review.replays', { count: lands.length })}</p>}
    </div>
  );
}

/**
 * Where Accept would be while replaying the work onto the line conflicts (D87's amendment point 5): git's lines, the words its session
 * is asked to rebase with, shown before the press, and the one press that says them through D137's say door (`useSay`), as the
 * person's own words. Accept is offered again once the tree changes, when the plan reads it again.
 */
export function RebaseAsk({ session, conflict }: { session: string; conflict: LandingConflict }) {
  const { t } = useTranslation();
  const say = useSay();
  const [said, setSaid] = useState<string | null>(null);
  return (
    <div className="grid gap-1 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
      <p className="m-0"><Inline text={t('work.review.conflict', { says: conflict.says })} /></p>
      <p className="m-0 text-ink-faint">{t('work.review.rebaseWords')}</p>
      <blockquote className="m-0 whitespace-pre-wrap border-l border-line pl-2"><Inline text={conflict.words} /></blockquote>
      {said ? <p className="m-0">{said}</p> : (
        <div>
          <Button
            variant="primary"
            disabled={say.isPending}
            onClick={() => say.mutate({ id: session, text: conflict.words }, {
              onSuccess: (answer) => setSaid(answer.sent ? t('work.review.rebaseSaid') : t('work.review.rebaseNotSaid')),
              onError: () => setSaid(t('work.review.rebaseNotSaid')),
            })}
          >
            {say.isPending ? t('work.review.askingRebase') : t('work.review.askRebase')}
          </Button>
        </div>
      )}
    </div>
  );
}
