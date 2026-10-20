import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { HelpMode } from './wired';
import { Button, Icon, Menu, Prose } from '../../design/ui';
import { cn } from '../../lib/cn';
import { chipOf } from './modes';

// Ask Daoris's access, chosen from its chip (HELPMODE1b, D183 point 2; the modes design §1, §5): beside the panel's composer, and in
// Settings → AI features beside its agent. The chip and the confirmation are apart so each holder places the confirmation where it
// has room; `useModeChoice` is the one rule both follow. Its press is the holder's: this file presses nothing.

/** The order the chip's menu offers the modes in: the default first. */
const MODES: readonly HelpMode[] = ['restricted', 'full'];

/**
 * The rule of a pick (design §5): restricted is set at once; full access asks once, inline, and is set only by its own press. A
 * pick of the mode already chosen does nothing.
 */
export function useModeChoice(selected: HelpMode, onChoose: (mode: HelpMode) => void) {
  const [confirming, setConfirming] = useState(false);
  return {
    confirming,
    pick: (mode: HelpMode) => {
      if (mode === selected) {
        setConfirming(false);
      } else if (mode === 'full') {
        setConfirming(true);
      } else {
        setConfirming(false);
        onChoose(mode);
      }
    },
    enable: () => {
      setConfirming(false);
      onChoose('full');
    },
    cancel: () => setConfirming(false),
  };
}

/**
 * Ask Daoris's access chip (design §1): the mode the conversation in view runs under, its record's, or the mode chosen before any
 * start; a switch waiting for the conversation's next turn reads *Restricted · full access next turn* (ACCESSSWAP1), until its next
 * words resume it under the mode chosen and its record says that mode. Full access, now or next, wears the
 * warning colour. Its menu offers the two modes, the chosen one ticked; Radix gives it the keyboard. Given a door to restricted
 * mode's configured access, it offers *Configure restricted access…* under them (HELPMODE1c1, design §5).
 */
export function HelpModeChip({ selected, running = null, busy = false, open, onPick, onConfigure }: {
  /** The mode chosen for this machine: what each conversation's next turn takes. */
  selected: HelpMode;
  /** The mode the conversation in view runs under, its record's; null before a start, and in Settings. */
  running?: HelpMode | null;
  /** A change on its way. */
  busy?: boolean;
  /** Changed to a new number to open the menu: a lack's door to the mode (design §6). */
  open?: number;
  onPick: (mode: HelpMode) => void;
  /** The door to restricted mode's configured access (HELPMODE1c1); none offers no row. */
  onConfigure?: () => void;
}) {
  const { t } = useTranslation();
  const [shown, setShown] = useState(false);
  // A pick of full access hands the focus to its confirmation, not back to the chip.
  const handing = useRef(false);
  useEffect(() => {
    if (open) setShown(true);
  }, [open]);

  const { now, next } = chipOf(selected, running);
  const says = next === null ? t(`help.mode.${now}`) : t(next === 'full' ? 'help.mode.toFull' : 'help.mode.toRestricted');
  const warn = now === 'full' || next === 'full';

  return (
    <Menu.Root open={shown} onOpenChange={setShown}>
      <Menu.Trigger asChild>
        <button
          type="button"
          disabled={busy}
          aria-label={t('help.mode.trigger', { mode: says })}
          className={cn(
            'inline-flex h-7 min-w-0 shrink items-center gap-1.5 rounded-control border px-2 text-small',
            'transition-colors duration-(--speed) disabled:opacity-60',
            'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
            warn
              ? 'border-warn bg-warn/10 text-ink'
              : 'border-line text-ink-soft hover:enabled:border-line-strong hover:enabled:text-ink data-[state=open]:text-ink',
          )}
        >
          <Icon name={warn ? 'conflict' : 'shield'} size={13} className={cn('shrink-0', warn ? 'text-warn' : 'text-ink-faint')} />
          <span className="min-w-0 truncate">{says}</span>
          <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
        </button>
      </Menu.Trigger>
      <Menu.Content
        align="start"
        className="min-w-60"
        onCloseAutoFocus={(event) => {
          if (!handing.current) return;
          handing.current = false;
          event.preventDefault();
        }}
      >
        <Menu.Label>{t('help.mode.title')}</Menu.Label>
        <Menu.RadioGroup
          value={selected}
          onValueChange={(value) => {
            const mode = value === 'full' ? 'full' : 'restricted';
            handing.current = mode === 'full' && selected !== 'full';
            onPick(mode);
          }}
        >
          {MODES.map((mode) => (
            <Menu.RadioItem key={mode} value={mode}>
              <span className={cn('flex-1', mode === 'full' && 'text-ink')}>{t(`help.mode.option.${mode}`)}</span>
            </Menu.RadioItem>
          ))}
        </Menu.RadioGroup>
        {onConfigure && (
          <>
            <Menu.Separator />
            <Menu.Item tick={false} onSelect={onConfigure}>{t('help.mode.configure')}</Menu.Item>
          </>
        )}
      </Menu.Content>
    </Menu.Root>
  );
}

/**
 * Full access asked once (design §5): the design's sentence, the servers it would add that restricted mode withholds, and when it
 * takes effect, with *Enable full access* and *Never mind*. It takes the focus as it opens, so the keyboard reads it first.
 */
export function FullAccessConfirm({ adds, busy = false, onEnable, onCancel }: {
  /** The servers full access would add, by name; null while they are being read. */
  adds?: readonly string[] | null;
  busy?: boolean;
  onEnable: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    box.current?.focus();
  }, []);

  return (
    <div
      ref={box}
      role="group"
      tabIndex={-1}
      aria-label={t('help.mode.confirm.label')}
      className="grid min-w-0 gap-1.5 border-l-[3px] border-warn bg-raised px-3 py-2 focus:outline-none"
    >
      <Prose className="m-0 text-small text-ink">{t('help.mode.confirm.says')}</Prose>
      {adds === null || adds === undefined ? (
        <p className="m-0 text-small text-ink-faint">{t('help.mode.confirm.addsReading')}</p>
      ) : adds.length === 0 ? (
        <p className="m-0 text-small text-ink-soft">{t('help.mode.confirm.addsNone')}</p>
      ) : (
        <div className="grid gap-1">
          <p className="m-0 text-small text-ink-soft">{t('help.mode.confirm.adds')}</p>
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0">
            {adds.map((name) => (
              <li key={name}><code className="text-meta">{name}</code></li>
            ))}
          </ul>
        </div>
      )}
      <p className="m-0 text-small text-ink-soft">{t('help.mode.confirm.when')}</p>
      <div className="flex flex-wrap gap-2">
        <Button className="border-warn" disabled={busy} onClick={onEnable}>
          <Icon name="conflict" size={13} className="text-warn" />
          {t('help.mode.confirm.enable')}
        </Button>
        <Button variant="ghost" disabled={busy} onClick={onCancel}>{t('common.cancel')}</Button>
      </div>
    </div>
  );
}
