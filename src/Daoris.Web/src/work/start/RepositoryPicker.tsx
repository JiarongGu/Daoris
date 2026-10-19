import { type KeyboardEvent, type ReactNode, useEffect, useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { Icon } from '../../design/ui';

/** A repository a conversation may be started in, as the composer lists it: its workspace, and whether it has a checkout here. */
export type StartRepository = { name: string; workspace: string; checkout: boolean };

/** One row of the picker: the repository, and whether it is the Recent group's (which names its workspace beside it). */
type Row = StartRepository & { recent: boolean };

/** A run of rows under one name: *Recent*, then each workspace. */
type Group = { name: string; rows: Row[] };

/** A chip's frame, the composer's three alike: a rounded token a size down from the prompt, pressed to choose. */
export const CHIP = cn(
  'inline-flex h-7 min-w-0 max-w-full items-center gap-1.5 rounded-full border border-line bg-raised px-2.5 text-small text-ink-soft',
  'cursor-pointer transition-colors duration-(--speed) hover:bg-accent-soft hover:text-ink',
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent',
  'aria-expanded:bg-accent-soft aria-expanded:text-ink',
);

/**
 * The groups the picker lists for what was typed: *Recent* first, in the order kept, then each workspace's repositories by
 * name, none twice (the entry design §1). A typed word keeps the rows whose name or workspace holds it.
 */
export function pickerGroups(repositories: readonly StartRepository[], recent: readonly string[], typed: string, recentLabel: string): Group[] {
  const word = typed.trim().toLowerCase();
  const matches = (row: StartRepository) =>
    !word || row.name.toLowerCase().includes(word) || row.workspace.toLowerCase().includes(word);
  const byName = new Map(repositories.map((row) => [row.name, row]));
  const recentRows = recent.flatMap((name) => {
    const row = byName.get(name);
    return row && matches(row) ? [{ ...row, recent: true }] : [];
  });
  const kept = new Set(recentRows.map((row) => row.name));
  const workspaces = [...new Set(repositories.map((row) => row.workspace))].sort((a, b) => a.localeCompare(b));
  const groups: Group[] = recentRows.length > 0 ? [{ name: recentLabel, rows: recentRows }] : [];
  for (const workspace of workspaces) {
    const rows = repositories
      .filter((row) => row.workspace === workspace && !kept.has(row.name) && matches(row))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((row) => ({ ...row, recent: false }));
    if (rows.length > 0) groups.push({ name: workspace, rows });
  }
  return groups;
}

/**
 * The repository chip and its picker (NEW1b, the entry design §1): searchable, *Recent* first, then each workspace's
 * repositories. One with no checkout here is listed, not choosable, and says why; one whose checkout a session holds is
 * marked busy and stays choosable, since its own tree starts beside it.
 *
 * @remarks
 * **A combobox, not a menu.** A field that narrows a list is the combobox pattern the command palette follows: the focus
 * stays in the field, the arrows move the active row (`aria-activedescendant`), Enter takes it and Escape puts the list down
 * and the focus back on the chip. A menu would take every letter typed as its own type-ahead.
 *
 * **Its foot is the caller's** (`foot`): NEW1c puts *Add repository…* and *Import a folder…* there, which open the existing
 * drawers with the draft kept. A press there closes the list first (`foot` is handed what closes it), so the drawer opens over
 * a composer with nothing floating. An import that registered several asks the list open on their workspace (`reveal`).
 */
export function RepositoryPicker({ repositories, busy = {}, recent = [], value, onChoose, foot, reveal, defaultOpen = false }: {
  repositories: readonly StartRepository[];
  /** Each repository whose checkout an active session holds, with that session. */
  busy?: Readonly<Record<string, string>>;
  /** This machine's last starts, newest first. */
  recent?: readonly string[];
  value: string | null;
  onChoose: (repository: string) => void;
  /** What the picker offers below its rows, after a rule (NEW1c), given what puts the list down: a press there closes it first. */
  foot?: (close: () => void) => ReactNode;
  /**
   * Opens the list filtered to a workspace's group (NEW1c, an import that registered several): each new `n` is one ask, and an
   * empty workspace opens it whole.
   */
  reveal?: { workspace: string; n: number } | null;
  /** Drawn open, for a story. */
  defaultOpen?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(defaultOpen);
  const [typed, setTyped] = useState('');
  const [active, setActive] = useState<string | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const chip = useRef<HTMLButtonElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const id = useId();

  const groups = pickerGroups(repositories, recent, typed, t('work.start.recent'));
  const choosable = groups.flatMap((group) => group.rows).filter((row) => row.checkout);
  // The active row follows the list, as the palette's does: one typed away is never the one Enter takes.
  const current = choosable.find((row) => row.name === active) ?? choosable[0] ?? null;
  const optionId = (row: Row) => `${id}-${row.recent ? 'recent' : 'all'}-${row.name}`;

  const close = (refocus: boolean) => {
    setOpen(false);
    setTyped('');
    if (refocus) chip.current?.focus();
  };
  const choose = (row: Row | null) => {
    if (!row?.checkout) return;
    onChoose(row.name);
    close(true);
  };
  const move = (by: number) => {
    if (choosable.length === 0) return;
    const at = current ? choosable.indexOf(current) : -1;
    // Wraps, because a list you can leave by the bottom is one you have to scroll back up.
    setActive(choosable[((at + by) % choosable.length + choosable.length) % choosable.length]!.name);
  };

  useEffect(() => {
    if (!open) return undefined;
    field.current?.focus();
    // A press anywhere else puts the list down, as a menu's does; the focus stays where that press put it.
    const away = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) close(false);
    };
    document.addEventListener('pointerdown', away);
    return () => document.removeEventListener('pointerdown', away);
    // Only opening and closing arm it.
  }, [open]);

  useEffect(() => {
    if (!reveal) return;
    setTyped(reveal.workspace);
    setActive(value);
    setOpen(true);
    // Only a new ask: the list is read, not watched.
  }, [reveal?.n]);

  useEffect(() => {
    if (!open || !current) return;
    listRef.current?.querySelector(`[id="${CSS.escape(optionId(current))}"]`)?.scrollIntoView?.({ block: 'nearest' });
  }, [open, current?.name, typed]);

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    // An input method's press is its own (IME1): its Enter accepts a candidate and its Escape drops the composition.
    if (isComposing(event)) return;
    if (event.key === 'ArrowDown') { event.preventDefault(); move(1); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); move(-1); }
    else if (event.key === 'Enter') { event.preventDefault(); choose(current); }
    else if (event.key === 'Escape') {
      // The list's Escape, and nobody else's: the frame's own keys never hear it.
      event.preventDefault();
      event.stopPropagation();
      close(true);
    } else if (event.key === 'Tab') close(false);
  };

  const label = t('work.start.repository');
  return (
    <div ref={root} className="relative min-w-0">
      <button
        ref={chip}
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={t('work.start.chip', { field: label, value: value ?? t('work.start.chooseRepository') })}
        onClick={() => {
          if (open) {
            close(false);
            return;
          }
          // Opened on the one chosen, so Enter alone keeps it.
          setActive(value);
          setOpen(true);
        }}
        className={CHIP}
      >
        <Icon name="projects" size={13} className="shrink-0 text-ink-faint" />
        <span className="min-w-0 truncate">{value ?? t('work.start.chooseRepository')}</span>
        <Icon name="chevronDown" size={12} className="shrink-0 text-ink-faint" />
      </button>

      {open && (
        <div className="absolute left-0 top-full z-30 mt-1 flex max-h-80 w-72 max-w-[calc(100vw-2rem)] flex-col overflow-hidden rounded-control border border-line bg-overlay text-small shadow-lg">
          <input
            ref={field}
            value={typed}
            onChange={(event) => { setTyped(event.target.value); setActive(null); }}
            onKeyDown={onKeyDown}
            placeholder={t('work.start.find')}
            aria-label={t('work.start.find')}
            role="combobox"
            aria-expanded
            aria-autocomplete="list"
            aria-controls={`${id}-list`}
            aria-activedescendant={current ? optionId(current) : undefined}
            className="shrink-0 border-b border-line bg-transparent px-2.5 py-2 text-body text-ink outline-none placeholder:text-ink-faint"
          />
          {groups.length === 0 && (
            <p className="m-0 px-2.5 py-2 text-small text-ink-faint">{t('work.start.nothingFound', { typed: typed.trim() })}</p>
          )}
          <div ref={listRef} id={`${id}-list`} role="listbox" aria-label={label} className="min-h-0 flex-1 overflow-y-auto p-1">
            {groups.map((group) => (
              <div key={group.name} role="group" aria-label={group.name}>
                <div aria-hidden className="px-2 pb-0.5 pt-1.5 text-meta text-ink-faint">{group.name}</div>
                {group.rows.map((row) => {
                  const lit = current !== null && optionId(current) === optionId(row);
                  return (
                    <div
                      key={optionId(row)}
                      id={optionId(row)}
                      role="option"
                      aria-selected={lit}
                      aria-disabled={!row.checkout || undefined}
                      // The field keeps the focus: a press on a row chooses it and moves nothing.
                      onMouseDown={(event) => event.preventDefault()}
                      onMouseMove={() => { if (row.checkout) setActive(row.name); }}
                      onClick={() => choose(row)}
                      className={cn(
                        'flex items-baseline gap-2 rounded-control px-2 py-1',
                        row.checkout ? 'cursor-pointer text-ink' : 'cursor-default text-ink-faint',
                        lit && 'bg-accent-soft',
                      )}
                    >
                      <span className="min-w-0 truncate">{row.name}</span>
                      {row.recent && <>{' '}<span className="shrink-0 text-meta text-ink-faint">{row.workspace}</span></>}
                      {busy[row.name] && <>{' '}<span className="shrink-0 text-meta text-ink-open">{t('work.start.busyMark')}</span></>}
                      {!row.checkout && <>{' '}<span className="ml-auto shrink-0 text-meta">{t('work.start.noCheckout')}</span></>}
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
          {foot && <div className="shrink-0 border-t border-line p-1">{foot(() => close(false))}</div>}
        </div>
      )}
    </div>
  );
}
