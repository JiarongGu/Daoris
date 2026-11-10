import { type CSSProperties, Fragment, type KeyboardEvent, type ReactNode, useEffect, useId, useRef, useState } from 'react';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';

/** A chip's frame, the composers' alike: a rounded token a size down from the prompt, pressed to choose. */
export const CHIP = cn(
  'inline-flex h-7 min-w-0 max-w-full items-center gap-1.5 rounded-full border border-line bg-raised px-2.5 text-small text-ink-soft',
  'cursor-pointer transition-colors duration-(--speed) hover:bg-accent-soft hover:text-ink',
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent',
  'aria-expanded:bg-accent-soft aria-expanded:text-ink',
);

/** One row: `id` is its own among the rows, `value` what the active row follows and a choice hands back; one may stand twice. */
export type ComboRow = { id: string; value: string; choosable: boolean; content: ReactNode; className?: string };

/**
 * A run of rows: under a name drawn and read aloud as its group's (`name`), or a sentence drawn above them (`head`) with the
 * group's name read aloud (`label`), or neither.
 */
export type ComboGroup = { key: string; name?: string; label?: string; head?: ReactNode; rows: ComboRow[] };

/** What a caller's own keys may read and move: the active row's value, what is typed, and where the caret stands. */
export type ComboKeys = { active: string | null; setActive: (value: string | null) => void; atStart: boolean; atEnd: boolean };

/**
 * A chip that opens a searchable list, the one combobox the composers' pickers share (the new conversation's repository picker
 * and Ask Daoris's focus picker): the focus stays in the field, the arrows move the active row (`aria-activedescendant`) and wrap,
 * Enter takes it and gives the focus back to the chip, Escape puts the list down, Tab puts it down and moves on, and an input
 * method's Enter and Escape are its own. A menu would take every letter typed as its own type-ahead.
 *
 * @remarks
 * The rows are the caller's, worked out from what is typed (`groups`); a caller's own keys (`onKey`) are heard first and keep
 * a key they answer. A press anywhere else puts the list down and leaves the focus where that press put it. Opening `upward`
 * suits a chip at a panel's foot: the list is no higher than the space above the chip allows.
 */
export function ChipCombobox({
  chip, chipLabel, chipClassName = CHIP, listLabel, find, groups, empty, onOpen, onChoose, onKey, foot, reveal, upward = false,
  panelClassName = 'max-h-80 w-72 max-w-[calc(100vw-2rem)]', defaultOpen = false,
}: {
  chip: ReactNode;
  chipLabel: string;
  chipClassName?: string;
  listLabel: string;
  /** The field's placeholder and name. */
  find: string;
  groups: (typed: string) => ComboGroup[];
  /** What is said where nothing matches. */
  empty: (typed: string) => string;
  /** Called as the chip opens the list; answers the value made active first. */
  onOpen?: () => string | null;
  onChoose: (value: string) => void;
  /** A caller's own keys, heard before the list's; true where it answered the key. */
  onKey?: (event: KeyboardEvent<HTMLInputElement>, keys: ComboKeys) => boolean;
  /** What the list offers below its rows, after a rule, given what puts the list down: a press there closes it first. */
  foot?: (close: () => void) => ReactNode;
  /** Opens the list with these words typed: each new `n` is one ask. */
  reveal?: { typed: string; active: string | null; n: number } | null;
  upward?: boolean;
  panelClassName?: string;
  /** Drawn open, for a story. */
  defaultOpen?: boolean;
}) {
  const [open, setOpen] = useState(defaultOpen);
  const [typed, setTyped] = useState('');
  const [active, setActive] = useState<string | null>(null);
  const [room, setRoom] = useState<number | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const chipRef = useRef<HTMLButtonElement>(null);
  const field = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);
  const id = useId();

  const shown = groups(typed);
  const choosable = shown.flatMap((group) => group.rows).filter((row) => row.choosable);
  // The active row follows the list, as the palette's does: one typed away is never the one Enter takes.
  const current = choosable.find((row) => row.value === active) ?? choosable[0] ?? null;
  const optionId = (row: ComboRow) => `${id}-${row.id}`;

  const close = (refocus: boolean) => {
    setOpen(false);
    setTyped('');
    if (refocus) chipRef.current?.focus();
  };
  const choose = (row: ComboRow | null) => {
    if (!row?.choosable) return;
    onChoose(row.value);
    close(true);
  };
  const move = (by: number) => {
    if (choosable.length === 0) return;
    const at = current ? choosable.indexOf(current) : -1;
    // Wraps, because a list you can leave by the bottom is one you have to scroll back up.
    setActive(choosable[((at + by) % choosable.length + choosable.length) % choosable.length]!.value);
  };

  useEffect(() => {
    if (!open) return undefined;
    field.current?.focus();
    const away = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) close(false);
    };
    document.addEventListener('pointerdown', away);
    return () => document.removeEventListener('pointerdown', away);
    // Only opening and closing arm it.
  }, [open]);

  useEffect(() => {
    if (!reveal) return;
    setTyped(reveal.typed);
    setActive(reveal.active);
    setOpen(true);
    // Only a new ask: the list is read, not watched.
  }, [reveal?.n]);

  useEffect(() => {
    if (!open || !current) return;
    listRef.current?.querySelector(`[id="${CSS.escape(optionId(current))}"]`)?.scrollIntoView?.({ block: 'nearest' });
  }, [open, current?.id, typed]);

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    // An input method's press is its own: its Enter accepts a candidate and its Escape drops the composition.
    if (isComposing(event)) return;
    const input = event.currentTarget;
    const caret = { start: input.selectionStart ?? 0, end: input.selectionEnd ?? 0 };
    const keys: ComboKeys = { active: current?.value ?? null, setActive, atStart: caret.end === 0, atEnd: caret.start === input.value.length };
    if (onKey?.(event, keys)) { event.preventDefault(); return; }
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

  const press = () => {
    if (open) {
      close(false);
      return;
    }
    setActive(onOpen?.() ?? null);
    // The list opened upward is no taller than the room above its chip, less a rem.
    const top = chipRef.current?.getBoundingClientRect().top ?? 0;
    setRoom(upward && top > 0 ? Math.min(320, Math.max(160, top - 16)) : null);
    setOpen(true);
  };

  const style: CSSProperties | undefined = room === null ? undefined : { maxHeight: room };
  const row = (each: ComboRow) => {
    const lit = current !== null && current.id === each.id;
    return (
      <div
        key={each.id}
        id={optionId(each)}
        role="option"
        aria-selected={lit}
        aria-disabled={!each.choosable || undefined}
        // The field keeps the focus: a press on a row chooses it and moves nothing.
        onMouseDown={(event) => event.preventDefault()}
        onMouseMove={() => { if (each.choosable) setActive(each.value); }}
        onClick={() => choose(each)}
        className={cn(
          'flex items-baseline gap-2 rounded-control px-2 py-1',
          each.choosable ? 'cursor-pointer text-ink' : 'cursor-default text-ink-faint',
          lit && 'bg-accent-soft',
          each.className,
        )}
      >
        {each.content}
      </div>
    );
  };

  return (
    <div ref={root} className="relative min-w-0">
      <button
        ref={chipRef}
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={chipLabel}
        onClick={press}
        className={chipClassName}
      >
        {chip}
      </button>

      {open && (
        <div
          style={style}
          className={cn(
            'absolute left-0 z-30 flex flex-col overflow-hidden rounded-control border border-line bg-overlay text-small shadow-lg',
            upward ? 'bottom-full mb-1' : 'top-full mt-1',
            panelClassName,
          )}
        >
          <input
            ref={field}
            value={typed}
            onChange={(event) => { setTyped(event.target.value); setActive(null); }}
            onKeyDown={onKeyDown}
            placeholder={find}
            aria-label={find}
            role="combobox"
            aria-expanded
            aria-autocomplete="list"
            aria-controls={`${id}-list`}
            aria-activedescendant={current ? optionId(current) : undefined}
            className="shrink-0 border-b border-line bg-transparent px-2.5 py-2 text-body text-ink outline-none placeholder:text-ink-faint"
          />
          {shown.every((group) => group.rows.length === 0) && (
            <p className="m-0 px-2.5 py-2 text-small text-ink-faint">{empty(typed.trim())}</p>
          )}
          <div ref={listRef} id={`${id}-list`} role="listbox" aria-label={listLabel} className="min-h-0 flex-1 overflow-y-auto p-1">
            {shown.filter((group) => group.rows.length > 0).map((group) => (group.name || group.label ? (
              <div key={group.key} role="group" aria-label={group.label ?? group.name}>
                {group.head}
                {group.name && <div aria-hidden className="px-2 pb-0.5 pt-1.5 text-meta text-ink-faint">{group.name}</div>}
                {group.rows.map(row)}
              </div>
            ) : <Fragment key={group.key}>{group.rows.map(row)}</Fragment>))}
          </div>
          {foot && <div className="shrink-0 border-t border-line p-1">{foot(() => close(false))}</div>}
        </div>
      )}
    </div>
  );
}
