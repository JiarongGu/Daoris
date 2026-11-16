import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { type DoorRead, doorSays } from './lineSupervision';

/**
 * An appointed role's answer at a door that waits on the person (a session's memory kept or dropped, a memory end decided, a departure
 * accepted), with the role and its agent, the time it takes effect and *Overrule* while it is held, the window in which it is theirs to
 * take back.
 *
 * @remarks
 * The card stands only while it can be pressed: an answer no longer held is not drawn, so it never lingers as a notice.
 * A molecule: handed the answer and its door; where nothing can act it is handed none, and still says the answer.
 */
export function SupervisorDoorCard({ door, onOverrule, busy = false, said = null }: {
  door: DoorRead | null;
  /** Take the answer back, by its id. Absent where nothing can. */
  onOverrule?: (decision: string) => void;
  busy?: boolean;
  /** Why the last overrule did not take, in the bridge's words. */
  said?: string | null;
}) {
  const { t } = useTranslation();
  if (!door || door.state !== 'held') return null;
  const title = t('work.supervision.door.title');
  return (
    <section aria-label={title} className="rounded-card border border-line bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold">{title}</h3>
      <p className="m-0 mt-1.5 text-small text-ink-soft">{doorSays(t, door)}</p>
      {onOverrule && (
        <div className="mt-2.5 flex flex-wrap gap-2">
          <Button disabled={busy} onClick={() => onOverrule(door.id)}>{t('work.supervision.overrule')}</Button>
        </div>
      )}
      {said && <p role="status" className="m-0 mt-1.5 text-small text-warn">{said}</p>}
    </section>
  );
}
