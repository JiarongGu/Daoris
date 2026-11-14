import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf } from '../locales/format';
import { routeKey } from '../person/Confirmations';
import { TERMINAL_TARGET_SCOPES, type TerminalActsNow, type TerminalPress, tierSetWords } from '../person/terminalActs';
import type { TerminalTier } from '../settings/machine/TerminalTierSetting';
import { StandingNote } from './StandingNote';
import { isNewSince, markOf, newestOf } from './standingSeen';

/** One press's line: its act in the window's words, what it named as a door to it, the command it is, its admission and its end. */
function PressLine({ press, onOpen }: { press: TerminalPress; onOpen: (press: TerminalPress) => void }) {
  const { t, i18n } = useTranslation();
  const act = t(routeKey(press.module, press.route), { defaultValue: press.route });
  const opens = press.target !== null && TERMINAL_TARGET_SCOPES.some((scope) => scope === press.scope);
  const outcome = press.outcome === 'failed' && press.code
    ? t('notifications.terminalTier.outcome.failedCode', { code: press.code })
    : t(`notifications.terminalTier.outcome.${press.outcome}`);
  return (
    <li data-terminal-press={press.id} className="flex flex-wrap items-baseline gap-x-2 text-small">
      <span className="text-ink">{act}</span>
      {press.target && (opens
        ? <Button variant="ghost" aria-label={t('notifications.terminalTier.openLabel', { target: press.target })} onClick={() => onOpen(press)}>{press.target}</Button>
        : <span className="text-ink-soft">{press.target}</span>)}
      {press.command && <code className="text-meta text-ink-soft">{press.command}</code>}
      <time dateTime={press.at} className="text-ink-faint">{clockOf(press.at, undefined, i18n.language)}</time>
      <span data-terminal-outcome={press.outcome} className="text-ink-faint">{outcome}</span>
    </li>
  );
}

/**
 * The terminal's tier as one compact line beside what Ask Daoris pressed: under `act` it says a terminal's press runs as the
 * person's with no card, counts what a terminal pressed and offers *Back to ask* and *Got it*. *Show* opens who turned the tier on
 * and when (a tier no change set is said as set outside Daoris) and each press newest first, kept across a restart. Under `ask` the
 * line stays while the record holds presses. Drawn only for a change or press newer than `since`, the person's last *Got it*.
 * A line, not a row: it counts toward no group and never the bell.
 *
 * A molecule: drawn from props; the application holds the tier, the record and the mark.
 */
export function TerminalTierLine({ tier, acts, since = null, busy = false, onBackToAsk, onSeen, onOpen, defaultOpen = false }: {
  tier: TerminalTier | null;
  acts: TerminalActsNow;
  since?: string | null;
  busy?: boolean;
  /** The person's press setting the tier back to ask. */
  onBackToAsk: () => void;
  onSeen?: (mark: string) => void;
  /** Opens what the press named. */
  onOpen: (press: TerminalPress) => void;
  defaultOpen?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const acting = tier === 'act';
  if (!acting && acts.presses.length === 0) return null;
  const newest = newestOf([...acts.changes.map((each) => each.at), ...acts.presses.map((each) => each.at)]);
  if (!isNewSince(newest, since)) return null;
  const set = tierSetWords(t, i18n.language, acting, acts.changes);
  return (
    <StandingNote
      dataAttribute="data-terminal-tier-line"
      label={t('notifications.terminalTier.label')}
      words={[
        t(acting ? 'notifications.terminalTier.act' : 'notifications.terminalTier.ask'),
        ...(acts.presses.length > 0 ? [t('notifications.terminalTier.count', { count: acts.presses.length })] : []),
      ]}
      actions={acting ? <Button disabled={busy} onClick={onBackToAsk}>{t('notifications.terminalTier.back')}</Button> : null}
      show={t('notifications.terminalTier.show')}
      hide={t('notifications.terminalTier.hide')}
      {...(onSeen ? { onSeen: () => onSeen(markOf(newest)) } : {})}
      defaultOpen={defaultOpen}
    >
      {set && <p className="m-0 min-w-0 text-small text-ink-soft">{set}</p>}
      {acts.presses.length > 0 && (
        <ul aria-label={t('notifications.terminalTier.list')} className="m-0 grid list-none gap-1 p-0">
          {acts.presses.map((each) => <PressLine key={each.id} press={each} onOpen={onOpen} />)}
        </ul>
      )}
    </StandingNote>
  );
}
