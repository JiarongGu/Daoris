import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../design/ui';
import { clockOf } from '../locales/format';
import { routeKey } from '../person/Confirmations';
import { TERMINAL_TARGET_SCOPES, type TerminalActsNow, type TerminalPress, tierSetWords } from '../person/terminalActs';
import type { TerminalTier } from '../settings/machine/TerminalTierSetting';

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
 * The terminal's tier in *Notifications*, beside what Ask Daoris pressed: while a press from a terminal on this machine runs at once as
 * the person's with no card (`act`), it says so, who turned it on and when (from the tier's record; a tier no change of the record set
 * is said as set outside Daoris), and offers *Back to ask* in one press. Under it, each press a terminal made under act, newest first,
 * kept across a restart: its act, what it named as a door to it, the command it is, its admission and its end (not done
 * with its code, its end not known, or interrupted by a stop). Under `ask` the line stays while the record holds presses, saying who
 * set it back. A line, not a row: it counts toward no group and never the bell.
 *
 * A molecule: drawn from props; the application holds the tier and the record.
 */
export function TerminalTierLine({ tier, acts, busy = false, onBackToAsk, onOpen, defaultOpen = false }: {
  tier: TerminalTier | null;
  acts: TerminalActsNow;
  busy?: boolean;
  /** The person's press setting the tier back to ask. */
  onBackToAsk: () => void;
  /** Opens what the press named. */
  onOpen: (press: TerminalPress) => void;
  defaultOpen?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const id = useId();
  const acting = tier === 'act';
  if (!acting && acts.presses.length === 0) return null;
  const set = tierSetWords(t, i18n.language, acting, acts.changes);
  return (
    <section data-terminal-tier-line="" aria-label={t('notifications.terminalTier.label')} className="grid gap-2 rounded-control border border-line border-l-[3px] border-l-warn bg-sunken px-3 py-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="grid min-w-0 gap-0.5">
          <p className="m-0 min-w-0 text-small text-ink">{t(acting ? 'notifications.terminalTier.act' : 'notifications.terminalTier.ask')}</p>
          {set && <p className="m-0 min-w-0 text-small text-ink-soft">{set}</p>}
          {acts.presses.length > 0 && <p className="m-0 min-w-0 text-small text-ink-soft">{t('notifications.terminalTier.count', { count: acts.presses.length })}</p>}
        </div>
        <span className="flex flex-wrap items-center gap-1">
          {acting && <Button disabled={busy} onClick={onBackToAsk}>{t('notifications.terminalTier.back')}</Button>}
          {acts.presses.length > 0 && (
            <Button variant="ghost" aria-expanded={open} aria-controls={`${id}-presses`} onClick={() => setOpen((was) => !was)}>
              {t(open ? 'notifications.terminalTier.hide' : 'notifications.terminalTier.show')}
            </Button>
          )}
        </span>
      </div>
      {open && acts.presses.length > 0 && (
        <ul id={`${id}-presses`} aria-label={t('notifications.terminalTier.list')} className="m-0 grid list-none gap-1 p-0">
          {acts.presses.map((each) => <PressLine key={each.id} press={each} onOpen={onOpen} />)}
        </ul>
      )}
    </section>
  );
}
