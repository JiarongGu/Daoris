import { useTranslation } from 'react-i18next';
import type { NotePart } from '../../data/api';
import { Button, WaitingCard } from '../../design/ui';
import { Note } from '../Note';
import { InConversation } from './WaitAnchor';

/**
 * A parked INTAKE, and where its answer is.
 *
 * @remarks
 * **The answer is on the ask, not on the session.** An intake parks when the declarations do not
 * settle whose an ask is and it asks the person rather than guess. The person answers by
 * publishing the ask to a repository or closing it, and the driver's next tick ends this record:
 * `completed` for a publish, `stopped` for a close. So the one move that answers is a door to the
 * ask's record, where publish and close already live, and `daoris ask --publish/--close`
 * are the terminal's twins.
 *
 * **Not the three moves a parked session has.** Each ends the record without answering the ask,
 * which then falls back to a proposal. *Finish* would even record `completed` for an
 * intake that published nothing, which reads later as a publish. *Decline* has nothing to decline.
 * And the box below is no session's: an intake is one turn, and a parked one has no process left, so what is written there is
 * posted on the ask's channel (ASKANSWER1), where the ask's sessions and the person read it.
 *
 * **Its stop is the page header's**. Ending the intake without answering is still a real choice
 * (the person will settle the ask later, or wants the question off the list), and the header's stop asks with the
 * sentence this card said beside it: the ask then waits as a proposal.
 *
 * A molecule: it is handed the ask and the question and reports its door. Where nothing can act
 * (a story, a mirrored record) it is handed none, and still says where the answer lives.
 */
export function AwaitingIntake({ ask, parts, onAnswer, onConversation }: {
  /** The way to the message that asked, in the conversation, where it holds it: *In the conversation*. */
  onConversation?: () => void;
  /** The ask this intake serves — the id the record's door opens. */
  ask: string;
  /** The intake's parked note's lines, Daoris's worded in the reader's language and the agent's as written (`Note`). */
  parts?: readonly NotePart[] | null;
  /** Open the ask's record, where it is answered. Absent where nothing can open it. */
  onAnswer?: (ask: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <WaitingCard title={t('work.intake.waiting')}>

      <Note parts={parts} className="mt-1.5 text-body leading-relaxed" />

      <p className="m-0 mt-2 flex flex-wrap items-center gap-x-2 text-small text-ink-faint">
        <span>{t('work.intake.hint')}</span>
        {onConversation && <InConversation onPress={onConversation} />}
      </p>

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
