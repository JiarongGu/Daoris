import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import * as Dialog from '@radix-ui/react-dialog';
import { type Command, matching } from '../commands';
import { Icon } from '../ui';
import { cn } from '../lib/cn';

/**
 * The command palette (SURF9): everything the application can do, by name.
 *
 * @remarks
 * **It exists because the activity bar lost its labels.** SURF10 replaced a 15rem labelled sidebar
 * with a 48px icon rail and named the palette as where that discoverability would be paid back; this
 * is the payment. It is also the only affordance that scales past roughly seven top-level domains,
 * and Daoris now has Manage's six plus Work plus Review.
 *
 * **Presentational.** It is handed the commands and told whether it is open — so a browser's list, a
 * shell's list and a no-matches list are all reachable by passing props, and the registry that
 * decides what is in them is a pure function tested on its own.
 *
 * **The list is never empty of meaning**: no match says so rather than showing a blank panel, because
 * an empty box reads as broken where a sentence reads as an answer.
 *
 * D41 §6 throughout: `role="dialog"` with `aria-modal` and ESC via Radix, focus moved to the input on
 * open, the active option tracked with `aria-activedescendant` so a screen reader follows the arrow
 * keys, and every row a real hit target.
 *
 * **What is typed can be asked** (DOCK1d): where Ask Daoris is here, the last row asks it the words
 * typed, whether or not a command matched — the command center's one question, as VS Code's palette
 * offers its chat the words it could not match.
 */
export function CommandPalette({ open, commands, onClose, onAsk }: {
  open: boolean;
  commands: Command[];
  onClose: () => void;
  /** Asks Ask Daoris the words typed (DOCK1d); absent where there is no Ask Daoris. */
  onAsk?: (question: string) => void;
}) {
  const { t } = useTranslation();
  const [typed, setTyped] = useState('');
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLUListElement>(null);

  // Every opening starts fresh. A palette that remembers what you typed last time is one you have to
  // clear before you can use it.
  useEffect(() => {
    if (open) {
      setTyped('');
      setActive(0);
    }
  }, [open]);

  const hits = matching(commands, typed);
  const question = typed.trim();
  // The asking row, last, as a command of its own so the arrows and Enter treat it like any other.
  const ask: Command | null = onAsk && question
    ? { id: 'ask', title: t('palette.ask', { typed: question }), group: t('help.title'), icon: 'help', run: () => onAsk(question) }
    : null;
  const rows = ask ? [...hits, ask] : hits;
  // The selection follows the list rather than the other way round: typing narrows it, and an index
  // left pointing past the end would run the wrong thing on Enter.
  const chosen = Math.min(active, Math.max(0, rows.length - 1));

  const move = (by: number) => {
    if (rows.length === 0) return;
    // Wraps, because a list you can leave by the bottom is one you have to scroll back up.
    setActive(((chosen + by) % rows.length + rows.length) % rows.length);
  };

  const run = (command: Command | undefined) => {
    if (!command) return;
    // Closed BEFORE running: a command that opens a drawer or switches frames should not have to
    // think about the palette still being over it.
    onClose();
    command.run();
  };

  useEffect(() => {
    listRef.current?.querySelector('[data-active="true"]')?.scrollIntoView({ block: 'nearest' });
  }, [chosen, typed]);

  return (
    <Dialog.Root open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <Dialog.Portal>
        {/* Between the frame's bars — see `ui.tsx`'s drawer scrim and `tokens.test.ts`. It is also
            what VS Code does: its title bar stays live while quick-open is up. */}
        <Dialog.Overlay className="fixed bottom-6 left-12 right-0 top-9 z-20 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          // Radix writes the role and traps focus but not this attribute (see `Drawer`).
          aria-modal="true"
          // Near the top rather than centred: the list grows downward and a centred panel jumps as
          // it does.
          className="fixed left-1/2 top-[12vh] z-20 flex max-h-[60vh] w-[min(34rem,92vw)] -translate-x-1/2 flex-col overflow-hidden rounded-overlay border border-line bg-overlay shadow-[0_12px_48px_rgb(15_12_8/0.22)] focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]"
        >
          <Dialog.Title className="sr-only">{t('palette.title')}</Dialog.Title>

          <input
            autoFocus
            value={typed}
            onChange={(event) => { setTyped(event.target.value); setActive(0); }}
            onKeyDown={(event) => {
              if (event.key === 'ArrowDown') { event.preventDefault(); move(1); }
              else if (event.key === 'ArrowUp') { event.preventDefault(); move(-1); }
              else if (event.key === 'Enter') { event.preventDefault(); run(rows[chosen]); }
            }}
            placeholder={t(onAsk ? 'palette.placeholderAsk' : 'palette.placeholder')}
            aria-label={t('palette.title')}
            // The combobox pattern: an input that filters a listbox, with the active option
            // announced. Without the role this input, the list and the dialog title all answer to
            // the same accessible name and none of them is findable.
            role="combobox"
            aria-expanded={rows.length > 0}
            aria-autocomplete="list"
            aria-controls="palette-list"
            aria-activedescendant={rows[chosen] ? `palette-${rows[chosen].id}` : undefined}
            className="shrink-0 border-b border-line bg-transparent px-3.5 py-2.5 text-body text-ink outline-none placeholder:text-ink-faint"
          />

          {hits.length === 0 && (
            <p className="m-0 px-3.5 pb-1 pt-4 text-small text-ink-faint">
              {t('palette.nothing', { typed: question })}
            </p>
          )}
          {rows.length > 0 && (
              <ul
                id="palette-list"
                ref={listRef}
                role="listbox"
                aria-label={t('palette.title')}
                className="m-0 min-h-0 flex-1 list-none overflow-y-auto p-1"
              >
                {rows.map((command, index) => (
                  // The asking row is set apart from the commands: it is a question, not something done.
                  <li key={command.id} className={command === ask && hits.length > 0 ? 'mt-1 border-t border-line pt-1' : undefined}>
                    <button
                      type="button"
                      id={`palette-${command.id}`}
                      role="option"
                      aria-selected={index === chosen}
                      data-active={index === chosen}
                      // Pointer, not hover-with-focus: moving the mouse should select, so the thing
                      // Enter runs is always the thing under the cursor.
                      onMouseMove={() => setActive(index)}
                      onClick={() => run(command)}
                      className={cn(
                        'flex w-full items-center gap-2.5 rounded-control px-2.5 py-1.5 text-left text-body',
                        index === chosen ? 'bg-accent-soft text-ink' : 'text-ink-soft',
                      )}
                    >
                      <Icon name={command.icon} size={15} />
                      <span className="min-w-0 flex-1 truncate">{command.title}</span>
                      <span className="shrink-0 text-meta text-ink-faint">{command.group}</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
