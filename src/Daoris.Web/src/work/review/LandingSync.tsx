import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';

// Declared here, structurally equal to bridge/sessions/trees.ts's shapes, so these molecules import no bridge domain.
/** One commit a landing leaves out because the line already holds it: short id, subject, why, and the line's commit with the same patch. */
export type LeftOutCommit = { commit: string; subject: string; why: string; on?: string | null };
export type LandsCommit = { commit: string; subject: string };
/** A replay that conflicts with the line: git's lines, and the words its session is asked to rebase with. */
export type LandingConflict = { says: string; words: string };
/** The press that says the rebase words to the session: true where they were sent, false or a rejection where not. */
export type SayRebase = (words: string) => Promise<boolean>;

/**
 * Where replaying the work onto the line conflicts: git's lines, the words its session is asked to rebase with, and the one press
 * that says them through the say door (`useSay`), as the person's own words.
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
