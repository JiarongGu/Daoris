import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Button, Inline } from '../design/ui';

// Declared here, structurally equal to bridge/sessions/trees.ts's shapes, so these molecules import no bridge domain (LANDSYNC1w).
/** One commit a landing leaves out because the line already holds it: short id, subject, why, and the line's commit with the same patch. */
export type LeftOutCommit = { commit: string; subject: string; why: string; on?: string | null };
/** One commit a landing would replay onto the line. */
export type LandsCommit = { commit: string; subject: string };
/** A replay that conflicts with the line: git's lines, and the words its session is asked to rebase with. */
export type LandingConflict = { says: string; words: string };
/** The press that says the rebase words to the session: true where they were sent, false or a rejection where not. */
export type SayRebase = (words: string) => Promise<boolean>;

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
export function RebaseAsk({ conflict, onSay }: { conflict: LandingConflict; onSay: SayRebase }) {
  const { t } = useTranslation();
  const [pending, setPending] = useState(false);
  const [said, setSaid] = useState<string | null>(null);
  const press = () => {
    setPending(true);
    onSay(conflict.words).then(
      (sent) => setSaid(sent ? t('work.review.rebaseSaid') : t('work.review.rebaseNotSaid')),
      () => setSaid(t('work.review.rebaseNotSaid')),
    );
  };
  return (
    <div className="grid gap-1 border-l-[3px] border-warn pl-2 text-small text-ink-soft">
      <p className="m-0"><Inline text={t('work.review.conflict', { says: conflict.says })} /></p>
      <p className="m-0 text-ink-faint">{t('work.review.rebaseWords')}</p>
      <blockquote className="m-0 whitespace-pre-wrap border-l border-line pl-2"><Inline text={conflict.words} /></blockquote>
      {said ? <p className="m-0">{said}</p> : (
        <div>
          <Button variant="primary" disabled={pending} onClick={press}>
            {pending ? t('work.review.askingRebase') : t('work.review.askRebase')}
          </Button>
        </div>
      )}
    </div>
  );
}
