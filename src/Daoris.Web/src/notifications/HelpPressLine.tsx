import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf } from '../locales/format';
import { routeKey } from '../person/Confirmations';
import { type HelpPressNow, type StandingConfirmationNow, helpReverse } from '../person/standing';
import { PressReceiptLine, type ReceiptScope } from '../help/receipt/PressReceiptLine';

/** One press's line: its act in the window's own words, what it named, when, how it ended, and its conversation and reverse press. */
function PressLine({ press, busy, onOpen, onReverse, onOpenTarget }: {
  press: HelpPressNow;
  busy: boolean;
  onOpen: (press: HelpPressNow) => void;
  onReverse: (press: HelpPressNow) => void;
  onOpenTarget?: (scope: ReceiptScope, id: string) => void;
}) {
  const { t, i18n } = useTranslation();
  const act = t(routeKey(press.module, press.route), { defaultValue: press.route });
  const reverse = press.asked ? null : helpReverse(press);
  return (
    <li data-help-press={press.id} className="flex flex-wrap items-center justify-between gap-2 text-small">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span className="text-ink">{act}</span>
        {press.target && <span className="text-ink-soft">{press.target}</span>}
        {press.place && press.place.kind !== 'machine' && (
          <span data-help-place="" className="text-ink-soft">
            {t(press.beyond ? 'notifications.helpPressed.beyond' : 'notifications.helpPressed.in', {
              place: press.place.kind === 'workspace' ? press.place.workspace : press.place.repository,
            })}
          </span>
        )}
        <time dateTime={press.at} className="text-ink-faint">{clockOf(press.at, undefined, i18n.language)}</time>
        <span data-help-outcome={press.outcome} className="text-ink-faint">{t(`notifications.helpPressed.outcome.${press.outcome}`)}</span>
      </span>
      <span className="flex flex-wrap items-center gap-1">
        <Button variant="ghost" aria-label={t('notifications.helpPressed.openLabel', { act })} onClick={() => onOpen(press)}>
          {t('notifications.helpPressed.open')}
        </Button>
        {reverse && (
          <Button disabled={busy} aria-label={t('notifications.helpPressed.reverseLabel', { act })} onClick={() => onReverse(press)}>
            {t(`notifications.helpPressed.reverse.${reverse}`)}
          </Button>
        )}
      </span>
      {press.receipt && (
        <div className="basis-full">
          <PressReceiptLine receipt={press.receipt} {...(onOpenTarget ? { onOpen: onOpenTarget } : {})} />
        </div>
      )}
    </li>
  );
}

/**
 * What Ask Daoris pressed for the person this run, in *Notifications*' group beside the steward's line: a press its conversation made
 * in full access runs at once with no card, so it is listed here, never silent. *Ask Daoris pressed n acts for you since Daoris
 * started*, opening to the presses newest first, each in the window's own words for the act, its target, the place it named (*outside
 * its focus* where it lay beyond its conversation's focus at the press), its time, whether it is done, a press to open its conversation
 * and, where its route has one, the owner's reverse press (a pause's *Resume*). The presses it asked
 * the person on a card in restricted mode stand apart under *Asked you*, each with its card's state and no reverse, the person's
 * answer being theirs. A line, not a row: no notice per press, so it counts toward no group's count and never the bell's.
 *
 * A molecule: drawn from props; the application holds the presses.
 */
export function HelpPressLine({ standing, busy = false, onOpen, onReverse, onOpenTarget, defaultOpen = false }: {
  standing: StandingConfirmationNow | null;
  defaultOpen?: boolean;
  /** A reverse press on its way. */
  busy?: boolean;
  /** Opens the conversation that pressed it. */
  onOpen: (press: HelpPressNow) => void;
  /** The owner's own reverse press, the one `helpReverse` names. */
  onReverse: (press: HelpPressNow) => void;
  /** Goes to what a press made: the repository's page or a quest; with none, a receipt is words alone. */
  onOpenTarget?: (scope: ReceiptScope, id: string) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  const presses = standing?.helpPresses ?? [];
  if (presses.length === 0) return null;
  const ran = presses.filter((each) => !each.asked);
  const asked = presses.filter((each) => each.asked);
  return (
    <section data-help-press-line="" aria-label={t('notifications.helpPressed.label')} className="grid gap-2 rounded-control border border-line border-l-[3px] border-l-warn bg-sunken px-3 py-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="grid min-w-0 gap-0.5">
          {ran.length > 0 && <p className="m-0 min-w-0 text-small text-ink-soft">{t('notifications.helpPressed.line', { count: ran.length })}</p>}
          {asked.length > 0 && <p className="m-0 min-w-0 text-small text-ink-soft">{t('notifications.helpPressed.askedLine', { count: asked.length })}</p>}
        </div>
        <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-presses`} onClick={() => setOpen((was) => !was)}>
          {t(open ? 'notifications.helpPressed.hide' : 'notifications.helpPressed.show')}
        </Button>
      </div>
      {open && (
        <div id={`${id}-presses`} className="grid gap-2">
          {ran.length > 0 && (
            <ul aria-label={t('notifications.helpPressed.list')} className="m-0 grid list-none gap-1 p-0">
              {ran.map((each) => <PressLine key={each.id} press={each} busy={busy} onOpen={onOpen} onReverse={onReverse} {...(onOpenTarget ? { onOpenTarget } : {})} />)}
            </ul>
          )}
          {asked.length > 0 && (
            <div className="grid gap-1">
              <p id={`${id}-asked`} className="m-0 text-meta text-ink-faint">{t('notifications.helpPressed.askedList')}</p>
              <ul aria-labelledby={`${id}-asked`} className="m-0 grid list-none gap-1 p-0">
                {asked.map((each) => <PressLine key={each.id} press={each} busy={busy} onOpen={onOpen} onReverse={onReverse} {...(onOpenTarget ? { onOpenTarget } : {})} />)}
              </ul>
            </div>
          )}
        </div>
      )}
    </section>
  );
}
