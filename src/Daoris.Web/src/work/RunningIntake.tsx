import { useTranslation } from 'react-i18next';
import { Button } from '../ui';

/**
 * A RUNNING intake, and why it has no message box (INT4h).
 *
 * @remarks
 * **An intake is one turn.** It is framed as one prompt (INT4b §1b's trap), so a line typed into a
 * composer had nowhere true to go: the pipe door spawns it with no stdin, and on the protocol door
 * its stdin is the driver's own JSON-RPC frames — a person's line written there lands in the middle
 * of the protocol. The driver refuses such a line in its own words; this is the screen's half, which
 * offers no box at all and says so in the one line a box would have taken.
 *
 * **Where an answer goes.** An intake that cannot tell whose an ask is asks by PARKING, and then the
 * answer is on the ask (INT4g's `AwaitingIntake`). So the door here opens the ask — a look, not an
 * answer, since nothing is being asked yet.
 *
 * **Its stop is the page header's** (SESSUX1d, D126 §3.3): the session's stop has one owner, and the sentence this card
 * said beside it, that the ask stays a proposal, is what the header's stop asks with.
 *
 * A molecule: handed the ask and its door. Where nothing can act (a story, a mirrored record) it is
 * handed none, and still says why there is no box.
 */
export function RunningIntake({ ask, onOpen }: {
  /** The ask this intake serves — the id the door opens. */
  ask: string;
  /** Open the ask's record. Absent where nothing can open it. */
  onOpen?: (ask: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <section className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{t('work.intakeRunning.title', { ask })}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{t('work.intakeRunning.hint')}</p>

      {onOpen && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button onClick={() => onOpen(ask)}>{t('work.intakeRunning.open', { ask })}</Button>
        </div>
      )}
    </section>
  );
}
