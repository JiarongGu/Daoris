import { useTranslation } from 'react-i18next';
import { Button, WaitingCard } from '../ui';

/**
 * A parked INTAKE, and where its answer is (INT4g).
 *
 * @remarks
 * **The answer is on the ask, not on the session.** An intake parks when the declarations did not
 * settle whose an ask is and it asked the person rather than guess (INT4b). The person answers by
 * publishing the ask to a repository or closing it, and the driver's next tick ends this record:
 * `completed` for a publish, `stopped` for a close. So the one move that answers is a door to the
 * ask's record, where publish and close already live, and `daoris-driver ask --publish/--close`
 * are the terminal's twins (D50).
 *
 * **Not the three moves a parked session has.** Each ended the record without answering the ask,
 * which then fell back to a proposal (found by INT4d). *Finish* even recorded `completed` for an
 * intake that published nothing, which reads later as a publish. *Decline* has nothing to decline.
 * And there is no box to answer in: an intake is one turn, and a parked one has no process left.
 *
 * **Stop stays, saying what it means.** Ending the intake without answering is a real choice (the
 * person will settle the ask later, or wants the question off the rail), and the ask then waits as a
 * proposal. The surface says so, because a stop that looked like an answer is the defect this fixes.
 *
 * A molecule: it is handed the ask and the question and reports two moves. Where nothing can act
 * (a story, a mirrored record) it is handed neither, and still says where the answer lives.
 */
export function AwaitingIntake({ ask, note, pending = false, onAnswer, onStop }: {
  /** The ask this intake serves — the id the record's door opens. */
  ask: string;
  /** The intake's own parked note, rendered word for word. */
  note?: string | null;
  /** A stop is in flight. The door is never held: opening a record changes nothing. */
  pending?: boolean;
  /** Open the ask's record, where it is answered. Absent where nothing can open it. */
  onAnswer?: (ask: string) => void;
  /** End the intake without answering. Absent where nothing can reach the process's machine. */
  onStop?: () => void;
}) {
  const { t } = useTranslation();

  return (
    <WaitingCard title={t('work.intake.waiting')}>

      {note && (
        <p className="m-0 mt-1.5 whitespace-pre-wrap text-body leading-relaxed">{note}</p>
      )}

      <p className="m-0 mt-2 text-small text-ink-faint">{t('work.intake.hint')}</p>

      {(onAnswer || onStop) && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          {onAnswer && (
            <Button variant="primary" onClick={() => onAnswer(ask)}>
              {t('work.intake.answer', { ask })}
            </Button>
          )}
          {onStop && (
            <Button variant="danger" disabled={pending} onClick={onStop}>
              {t('work.awaiting.stop')}
            </Button>
          )}
        </div>
      )}

      {onStop && <p className="m-0 mt-2 text-small text-ink-faint">{t('work.intake.stopMeans')}</p>}
    </WaitingCard>
  );
}
