import { useTranslation } from 'react-i18next';
import type { NotePart } from '../api';
import { Button, WaitingCard } from '../ui';
import { Note } from './Note';

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
 * **Its stop is the page header's** (SESSUX1d, D126 §3.3). Ending the intake without answering is still a real choice
 * (the person will settle the ask later, or wants the question off the list), and the header's stop asks with the
 * sentence this card said beside it: the ask then waits as a proposal.
 *
 * A molecule: it is handed the ask and the question and reports its door. Where nothing can act
 * (a story, a mirrored record) it is handed none, and still says where the answer lives.
 */
export function AwaitingIntake({ ask, note, parts, onAnswer }: {
  /** The ask this intake serves — the id the record's door opens. */
  ask: string;
  /** The intake's parked note: its English, shown as kept where it has no parts. */
  note?: string | null;
  /** Its lines (LANG1a), Daoris's worded in the reader's language and the agent's as written (LANG1b, `Note`). */
  parts?: readonly NotePart[] | null;
  /** Open the ask's record, where it is answered. Absent where nothing can open it. */
  onAnswer?: (ask: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <WaitingCard title={t('work.intake.waiting')}>

      <Note note={note} parts={parts} className="mt-1.5 text-body leading-relaxed" />

      <p className="m-0 mt-2 text-small text-ink-faint">{t('work.intake.hint')}</p>

      {onAnswer && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button variant="primary" onClick={() => onAnswer(ask)}>
            {t('work.intake.answer', { ask })}
          </Button>
        </div>
      )}
    </WaitingCard>
  );
}
