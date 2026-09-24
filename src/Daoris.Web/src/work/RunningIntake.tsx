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
 * answer, since nothing is being asked yet — and the stop the composer used to carry stays, saying
 * what it leaves: the ask as a proposal.
 *
 * A molecule: handed the ask and two moves. Where nothing can act (a story, a mirrored record) it is
 * handed neither, and still says why there is no box.
 */
export function RunningIntake({ ask, pending = false, onOpen, onStop }: {
  /** The ask this intake serves — the id the door opens. */
  ask: string;
  /** A stop is in flight. The door is never held: opening a record changes nothing. */
  pending?: boolean;
  /** Open the ask's record. Absent where nothing can open it. */
  onOpen?: (ask: string) => void;
  /** Cut the intake off. Absent where nothing can reach the process's machine. */
  onStop?: () => void;
}) {
  const { t } = useTranslation();

  return (
    <section className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{t('work.intakeRunning.title', { ask })}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{t('work.intakeRunning.hint')}</p>

      {(onOpen || onStop) && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          {onOpen && (
            <Button onClick={() => onOpen(ask)}>{t('work.intakeRunning.open', { ask })}</Button>
          )}
          {onStop && (
            <Button variant="danger" disabled={pending} onClick={onStop}>
              {t('work.awaiting.stop')}
            </Button>
          )}
        </div>
      )}

      {onStop && <p className="m-0 mt-2 text-small text-ink-faint">{t('work.intake.stopMeans')}</p>}
    </section>
  );
}
