import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf } from '../locales/format';
import { routeKey } from '../person/Confirmations';
import { type HelpPressNow, type StandingConfirmationNow, helpReverse } from '../person/standing';
import { StandingNote } from './StandingNote';
import { isNewSince, markOf, newestOf } from './standingSeen';
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
  const act = press.step
    ? t('notifications.helpPressed.step', {
      act: t(`notifications.helpPressed.stepAct.${press.step.act}`, { defaultValue: press.step.act }), node: press.step.node, run: press.target ?? '',
    })
    : t(routeKey(press.module, press.route), { defaultValue: press.route });
  const reverse = press.asked ? null : helpReverse(press);
  return (
    <li data-help-press={press.id} className="flex flex-wrap items-center justify-between gap-2 text-small">
      <span className="flex min-w-0 flex-wrap items-baseline gap-x-2">
        <span className="text-ink">{act}</span>
        {press.target && !press.step && <span className="text-ink-soft">{press.target}</span>}
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
      {press.why && (
        <p data-help-why="" className="m-0 basis-full text-ink-soft">{press.why}</p>
      )}
      {press.receipt && (
        <div className="basis-full">
          <PressReceiptLine receipt={press.receipt} {...(onOpenTarget ? { onOpen: onOpenTarget } : {})} />
        </div>
      )}
    </li>
  );
}

/**
 * What Ask Daoris pressed for the person this run, as one compact line beside the steward's: a press its conversation made in full
 * access runs at once with no card, so it is counted here, never silent. *Show* opens the presses newest first, each with its act,
 * place (*outside its focus* where it lay beyond its conversation's focus), time, outcome, conversation and, for a pause, *Resume*;
 * the presses it asked on a card in restricted mode stand apart under *Asked you*. Drawn only for a press newer than `since`, the
 * person's last *Got it*. A line, not a row: it counts toward no group and never the bell.
 *
 * A molecule: drawn from props; the application holds the presses and the mark.
 */
export function HelpPressLine({ standing, since = null, busy = false, onOpen, onReverse, onOpenTarget, onSeen, defaultOpen = false }: {
  standing: StandingConfirmationNow | null;
  since?: string | null;
  defaultOpen?: boolean;
  /** A reverse press on its way. */
  busy?: boolean;
  /** Opens the conversation that pressed it. */
  onOpen: (press: HelpPressNow) => void;
  /** The owner's own reverse press, the one `helpReverse` names. */
  onReverse: (press: HelpPressNow) => void;
  /** Goes to what a press made: the repository's page or a quest; with none, a receipt is words alone. */
  onOpenTarget?: (scope: ReceiptScope, id: string) => void;
  onSeen?: (mark: string) => void;
}) {
  const { t } = useTranslation();
  const id = useId();
  const presses = standing?.helpPresses ?? [];
  if (presses.length === 0) return null;
  const newest = newestOf(presses.map((each) => each.at));
  if (!isNewSince(newest, since)) return null;
  const ran = presses.filter((each) => !each.asked);
  const asked = presses.filter((each) => each.asked);
  return (
    <StandingNote
      dataAttribute="data-help-press-line"
      label={t('notifications.helpPressed.label')}
      words={[
        ...(ran.length > 0 ? [t('notifications.helpPressed.line', { count: ran.length })] : []),
        ...(asked.length > 0 ? [t('notifications.helpPressed.askedLine', { count: asked.length })] : []),
      ]}
      show={t('notifications.helpPressed.show')}
      hide={t('notifications.helpPressed.hide')}
      {...(onSeen ? { onSeen: () => onSeen(markOf(newest)) } : {})}
      defaultOpen={defaultOpen}
    >
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
    </StandingNote>
  );
}
