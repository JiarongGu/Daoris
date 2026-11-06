import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';

/** What the head says of a hold: the terminal that has it, as the shell tells it. */
export type TerminalHeld = { asker?: { process?: string | null; pid?: number | null } | null };

/**
 * A conversation a terminal holds (RPC1g1, D238 point 2, the terminal-conversation design §3.1): said on its head with the terminal's
 * process, and the person's door to take it back.
 *
 * @remarks
 * **What the hold is.** The person confirmed, once, that a terminal speaks for them in this conversation: its words, its turn stops and
 * its finish run with no further card until it lets go. The window is never a holder, so its own box says words here as anywhere.
 *
 * **Let go of the terminal** ends the hold; the conversation goes on in Daoris, and the terminal is told the window took it back.
 *
 * A molecule: handed the hold and its door. Where nothing can act it is handed none, and still says who holds it.
 */
export function TerminalHold({ hold, onLetGo, busy = false, said = null }: {
  hold: TerminalHeld;
  /** Take the conversation back from the terminal. Absent where nothing can. */
  onLetGo?: () => void;
  busy?: boolean;
  /** Why the last let-go did not happen, in the bridge's words. */
  said?: string | null;
}) {
  const { t } = useTranslation();
  const asker = hold.asker;
  const who = asker?.process && asker.pid != null
    ? t('work.head.heldBy', { process: asker.process, pid: asker.pid })
    : asker?.pid != null ? t('work.head.heldByPid', { pid: asker.pid }) : t('work.head.heldByUnknown');

  return (
    <section aria-label={t('work.head.held')} className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{t('work.head.held')}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{who}</p>
      {onLetGo && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button disabled={busy} onClick={onLetGo}>{t('work.head.letGo')}</Button>
        </div>
      )}
      {said && <p role="status" className="m-0 mt-1.5 text-small text-warn">{said}</p>}
    </section>
  );
}
