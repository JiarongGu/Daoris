import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { Refused } from './InlineConfirm';

/**
 * **A line an account's limit cut off, waiting** (LIMITRESUME1d, D206 §3.7): one line under the page header, and at its foot, saying
 * when it resumes by itself and on which account, which resume comes (its own conversation, or a new session handed the brief), and a
 * reset that did not lift the limit. Beside it the person's ways out: *Go on now with…* (another agent or account; on Ask Daoris a new
 * conversation from this one), *Pause* for a driven line (the pause of its work, since its last record has ended and has no stop to
 * make), *Stop* for a conversation that runs or *Stop waiting* for one that has ended, and the account's *Try now*.
 *
 * @remarks
 * A molecule: the sentence and each press arrive as props, and a press absent is not drawn. The words are `limitWaitSaid`'s, the one
 * rule the head, the foot, the row and the quest's page read.
 */
export function LimitWaitLine({ lines, busy = false, refusal = null, onGoOnNow, onPause, onStop, onStopWaiting, onTryNow, className }: {
  /** The wait in words (`limitWaitSaid`). */
  lines: readonly string[];
  /** A press on its way: the presses wait for it. */
  busy?: boolean;
  /** What a press came to where it was refused, in the catalogue's words or the driver's. */
  refusal?: string | null;
  /** *Go on now with…*: D163's hand-over ask, or Ask Daoris's new conversation from this one. */
  onGoOnNow?: () => void;
  /** *Pause quest…*: a driven line's pause of its work (PAUSE1b). */
  onPause?: () => void;
  /** *Stop…*: a conversation that still runs. */
  onStop?: () => void;
  /** *Stop waiting*: an ended conversation held by its limit waits no more. */
  onStopWaiting?: () => void;
  /** *Try now*: the account it waits for is offered again now (D125 §2.3). */
  onTryNow?: () => void;
  className?: string;
}) {
  const { t } = useTranslation();
  return (
    <div
      role="group"
      aria-label={t('work.limit.label')}
      className={`flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1.5 border-l-[3px] border-line-strong pl-2.5 text-small text-ink-soft ${className ?? ''}`}
    >
      <p className="m-0 min-w-0 wrap-anywhere">{lines.join(' ')}</p>
      {onGoOnNow && <Button variant="primary" disabled={busy} onClick={onGoOnNow}>{t('work.limit.goOnNow')}</Button>}
      {onTryNow && <Button variant="ghost" disabled={busy} onClick={onTryNow}>{t('harness.cooling.tryNow')}</Button>}
      {onPause && <Button variant="ghost" disabled={busy} onClick={onPause}>{t('work.act.pauseQuest')}</Button>}
      {onStop && <Button variant="ghost" disabled={busy} onClick={onStop}>{t('work.act.stop')}</Button>}
      {onStopWaiting && <Button variant="ghost" disabled={busy} onClick={onStopWaiting}>{t('work.limit.stopWaiting')}</Button>}
      {refusal && <Refused sentence={refusal} />}
    </div>
  );
}
