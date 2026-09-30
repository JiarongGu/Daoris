import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { Icon, Tip } from '../ui';
import { cn } from '../lib/cn';

/** One terminal as its tab shows it. */
export type TerminalTab = {
  id: string;
  /** Its shell and where it started (`terminalNames`). */
  name: string;
  /** The whole of where it started, for the tip. */
  tip: string;
  ended: boolean;
};

/** A shell "+" can open, named as the person knows it. */
export type TerminalShellItem = { shell: string; name: string; default: boolean };

const ITEM = cn(
  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small text-ink-soft outline-none',
  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
);

/**
 * The terminal view's own tabs (CONSOLE4c, D96): each terminal by its shell and where it started, the
 * shown one's close after them, and "+" with the machine's shells.
 *
 * @remarks
 * **The close is after the tabs, for the tab shown**, as the console's stop is (CONSOLE3a): a tab list
 * owns tabs and nothing else, and a button inside a tab is one label to a screen reader. A middle click
 * closes any tab, as a browser's and an editor's do.
 *
 * **"+" offers only what the machine has**, the default first; with one shell there is nothing to choose,
 * and "+" opens it. Until the machine has said (`shells` null), "+" opens the default, which the module
 * decides.
 */
export function TerminalTabs({ tabs, selected, onSelect, onClose, shells, onNew }: {
  tabs: TerminalTab[];
  selected: string | null;
  onSelect: (id: string) => void;
  onClose: (id: string) => void;
  shells: TerminalShellItem[] | null;
  /** Open a new terminal; with no shell named, the machine's default. */
  onNew: (shell?: string) => void;
}) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState(false);
  const shown = tabs.find((tab) => tab.id === selected);
  const choosing = shells !== null && shells.length > 1;

  const plus = (
    <button
      type="button"
      aria-label={t('work.terminal.new')}
      onClick={choosing ? undefined : () => onNew(shells?.[0]?.shell)}
      className={cn(
        'flex h-6 w-6 shrink-0 items-center justify-center rounded-control text-ink-faint',
        'transition-colors duration-(--speed) hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
      )}
    >
      <Icon name="plus" size={14} />
    </button>
  );

  return (
    <div className="flex min-w-0 shrink-0 items-center gap-1 border-b border-line px-2 py-0.5 font-sans">
      <div role="tablist" aria-label={t('work.terminal.tabs')} className="flex min-w-0 items-center gap-0.5 overflow-x-auto [scrollbar-width:none]">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={tab.id === selected}
            aria-label={tab.ended ? `${tab.name} — ${t('work.terminal.endedMark')}` : tab.name}
            title={tab.tip}
            onClick={() => onSelect(tab.id)}
            onAuxClick={(event) => {
              if (event.button !== 1) return;
              event.preventDefault();
              onClose(tab.id);
            }}
            className={cn(
              'flex max-w-56 shrink-0 cursor-pointer items-center gap-1.5 rounded-control border-0 px-2 py-0.5 text-meta transition-colors duration-(--speed)',
              tab.id === selected ? 'bg-raised text-ink' : 'bg-transparent text-ink-faint hover:text-ink',
            )}
          >
            <Icon name="terminal" size={12} className="shrink-0" />
            <span className="min-w-0 truncate">{tab.name}</span>
            {tab.ended && <span className="shrink-0 text-ink-faint">{t('work.terminal.endedMark')}</span>}
          </button>
        ))}
      </div>
      {shown && (
        <Tip content={t('work.terminal.close', { name: shown.name })}>
          <button
            type="button"
            aria-label={t('work.terminal.close', { name: shown.name })}
            onClick={() => onClose(shown.id)}
            className="flex h-6 w-6 shrink-0 cursor-pointer items-center justify-center rounded-control border-0 bg-transparent text-ink-faint transition-colors duration-(--speed) hover:bg-raised hover:text-ink"
          >
            <Icon name="x" size={12} />
          </button>
        </Tip>
      )}
      <div className="ml-auto flex shrink-0 items-center">
        {choosing
          ? (
            <Menu.Root modal={false} open={menu} onOpenChange={setMenu}>
              <Tip content={t('work.terminal.new')}>
                <Menu.Trigger asChild>{plus}</Menu.Trigger>
              </Tip>
              <Menu.Portal>
                <Menu.Content
                  align="end"
                  sideOffset={4}
                  collisionPadding={8}
                  className="z-30 min-w-48 rounded-control border border-line bg-overlay p-1 shadow-lg"
                >
                  {shells.map((shell) => (
                    <Menu.Item key={shell.shell} onSelect={() => onNew(shell.shell)} className={ITEM}>
                      <Icon name="terminal" size={13} className="shrink-0 opacity-70" />
                      <span className="truncate">{shell.name}</span>
                      {shell.default && <span className="ml-auto pl-3 text-meta text-ink-faint">{t('work.terminal.default')}</span>}
                    </Menu.Item>
                  ))}
                </Menu.Content>
              </Menu.Portal>
            </Menu.Root>
          )
          : <Tip content={t('work.terminal.new')}>{plus}</Tip>}
      </div>
    </div>
  );
}
