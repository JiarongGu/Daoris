import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { sentence } from '../../locales/format';

/**
 * **An approval's card**:the wait's own words, which name the release, its definition, the build and its
 * source branch, then the channel the plugin allows. Where it can approve, *Approve* and *Reject* with an optional comment, made under
 * the holder's own sign-in by the plugin's approval command, which no agent is handed; otherwise *Open*, the system's page in Daoris's
 * browser, where the holder approves and the step settles on the approval observed.
 *
 * A molecule: the wait, the channel and the presses arrive as props; a refusal is said where it is pressed.
 */
export function ApprovalCard({ words, url, canApprove, onDecide, onOpen }: {
  words: string;
  url: string | null;
  canApprove: boolean;
  onDecide: (decision: 'approve' | 'reject', comment: string) => Promise<{ done: boolean; message?: string | null }>;
  onOpen: (url: string) => void;
}) {
  const { t } = useTranslation();
  const [comment, setComment] = useState('');
  const [said, setSaid] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const decide = (decision: 'approve' | 'reject') => {
    setSending(true);
    setSaid(null);
    onDecide(decision, comment.trim())
      .then((answer) => { if (!answer.done) setSaid(answer.message ?? ''); })
      .catch((error: unknown) => setSaid(sentence(error)))
      .finally(() => setSending(false));
  };
  const press = 'min-h-0 px-1.5 py-0.5 text-small';

  return (
    <div className="grid min-w-0 gap-1 rounded-control border border-line px-2 py-1.5" role="group" aria-label={t('workflow.release.approval.title')}>
      <p className="m-0 text-small text-ink [overflow-wrap:anywhere]"><Inline text={words} /></p>
      {canApprove && (
        <input aria-label={t('workflow.release.approval.comment')} placeholder={t('workflow.release.approval.comment')} value={comment}
          onChange={(event) => setComment(event.target.value)}
          className="min-h-[1.9rem] min-w-0 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body" />
      )}
      <div className="flex flex-wrap items-center gap-1.5">
        {canApprove ? (
          <>
            <Button variant="primary" disabled={sending} className={press} onClick={() => decide('approve')}>{t('workflow.release.approval.approve')}</Button>
            <Button variant="ghost" disabled={sending} className={press} onClick={() => decide('reject')}>{t('workflow.release.approval.reject')}</Button>
            <span className="text-caption text-ink-faint">{t('workflow.release.approval.yours')}</span>
          </>
        ) : url && (
          <Button variant="ghost" className={press} onClick={() => onOpen(url)}>{t('workflow.release.approval.open')}</Button>
        )}
      </div>
      {said && <p role="alert" className="m-0 text-small text-ink">{said}</p>}
    </div>
  );
}
