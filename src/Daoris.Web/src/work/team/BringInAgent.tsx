import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import { isComposing } from '../../lib/composing';

/** The structures' doors (COWORKUX1f, the co-worker design §6.2, §3.2), each its own door's words and rule. */
export const BRING_IN_DOORS = ['question', 'piece', 'opinion', 'goOn', 'alongside'] as const;

export type BringInDoor = (typeof BRING_IN_DOORS)[number];

/**
 * One choice of the dialog: its door, and what choosing it does, or why the rules turn it off. A choice with neither is not drawn, since
 * nothing on this session could open it.
 */
export type BringInChoice = { door: BringInDoor; onChoose?: () => void; off?: string | null; note?: string };

/**
 * *Bring in another agent…* (COWORKUX1f, the co-worker design §6.2): one dialog whose choices are the structures' doors, each opening its
 * own door's ask, judged there by its own rule and admission as now. A choice the rules turn off is shown with its reason, never hidden.
 */
export function BringInAgent({ choices, onClose }: { choices: readonly BringInChoice[]; onClose: () => void }) {
  const { t } = useTranslation();
  const shown = BRING_IN_DOORS
    .map((door) => choices.find((choice) => choice.door === door))
    .filter((choice): choice is BringInChoice => Boolean(choice && (choice.onChoose || choice.off || choice.note)));
  return (
    <section
      role="dialog"
      aria-labelledby="bring-in-title"
      data-bring-in
      className="grid gap-2 rounded-control border border-line bg-raised p-3"
      onKeyDown={(event) => { if (isComposing(event)) return; if (event.key === 'Escape') onClose(); }}
    >
      <h3 id="bring-in-title" className="m-0 text-small font-semibold">{t('work.team.bringIn.title')}</h3>
      <p className="m-0 text-small text-ink-soft">{t('work.team.bringIn.lead')}</p>
      <ul className="m-0 grid list-none gap-1 p-0">
        {shown.map((choice) => choice.note ? (
          // A way that needs no press: the sentence says what already happens, so no button stands in for it.
          <li key={choice.door} data-door={choice.door} className="min-w-0 text-small text-ink-soft wrap-anywhere">
            <span className="font-semibold text-ink">{t(`work.team.door.${choice.door}`)}</span> {choice.note}
          </li>
        ) : (
          <li key={choice.door} data-door={choice.door} className="flex min-w-0 flex-wrap items-baseline gap-x-2">
            <Button
              disabled={!choice.onChoose || Boolean(choice.off)}
              onClick={() => {
                onClose();
                choice.onChoose?.();
              }}
            >
              {t(`work.team.door.${choice.door}`)}
            </Button>
            {choice.off && <span className="min-w-0 text-small text-ink-faint wrap-anywhere">{choice.off}</span>}
          </li>
        ))}
      </ul>
      <div>
        <Button variant="ghost" onClick={onClose}>{t('work.team.bringIn.cancel')}</Button>
      </div>
    </section>
  );
}
