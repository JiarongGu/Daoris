import { COMMANDS, type CommandEntry } from './commands';

/** What a key handler is handed: the key, where it is, and the modifiers held. */
export type Pressed = Pick<KeyboardEvent, 'key' | 'code' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>;

/** The place each printed key stands at, for the keys whose `key` Shift or a layout changes. */
const PLACE: Record<string, string> = { '`': 'Backquote', ',': 'Comma' };

/**
 * Whether a press is the key a menu prints (UX7a, D152 §3.4): every modifier it names held and no other, Ctrl standing
 * for ⌘ too, and the key by what it says or by its place, since under Shift and Alt what `key` says differs by layout
 * (Quick Ask's L, DOCK1d) and Shift turns ` into ~ and 1 into !.
 */
export function isPress(event: Pressed, combo: string): boolean {
  const parts = combo.split('+');
  // The key itself may be `+`-free text only: none of the table's keys is the plus.
  const key = parts.pop()!;
  const mods = new Set(parts);
  if ((event.ctrlKey || event.metaKey) !== mods.has('Ctrl')) return false;
  if (event.shiftKey !== mods.has('Shift')) return false;
  if (event.altKey !== mods.has('Alt')) return false;

  if (/^F\d{1,2}$/.test(key)) return event.key === key;
  if (/^[A-Z]$/.test(key)) return event.key.toLowerCase() === key.toLowerCase() || event.code === `Key${key}`;
  if (/^\d$/.test(key)) return event.key === key || event.code === `Digit${key}`;
  return event.key === key || event.code === PLACE[key];
}

/**
 * The frame's key for a press wherever the focus is — a field, a message box, the terminal — or null: the id of the
 * table's command whose key it is and that is the frame's everywhere (the places, Settings, the regions, the terminal,
 * the console, Ask Daoris and Quick Ask, D152 §3.4).
 *
 * @remarks
 * Read by the terminal's renderer (CONSOLE4b), which leaves these to the frame as VS Code's terminal leaves its commands
 * to the workbench: a renderer that took them would send the shell a ^B and the view's list would never close. And by
 * the detached and monitor windows, which answer one each.
 */
export function frameShortcut(event: Pressed): string | null {
  for (const spec of COMMANDS) {
    for (const key of spec.keys ?? []) {
      const combo = typeof key === 'string' ? key : key.combo;
      const reach = (typeof key !== 'string' && key.reach) || spec.reach || 'everywhere';
      if (reach === 'everywhere' && isPress(event, combo)) return spec.id;
    }
  }
  return null;
}

/**
 * The command a press runs, from the table as this window reads it, or null to leave the press where it was going
 * (D152 §3.4). A field keeps what a field means, the terminal that and a shell's next line, and a command that does not
 * apply takes no key, so Ctrl+B on a view with no list, or Ctrl+F with no box to find in, does what it did before.
 */
export function commandForKey(
  entries: readonly CommandEntry[], event: Pressed, where: { field: boolean; terminal: boolean },
): CommandEntry | null {
  for (const entry of entries) {
    if (!entry.enabled) continue;
    for (const key of entry.keys) {
      if (key.reach === 'native') continue;
      if (where.terminal && key.reach !== 'everywhere') continue;
      if (where.field && key.reach === 'outside') continue;
      if (isPress(event, key.combo)) return entry;
    }
  }
  return null;
}
