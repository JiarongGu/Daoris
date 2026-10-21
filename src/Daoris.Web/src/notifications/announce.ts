import { useCallback, useEffect, useRef, useState } from 'react';
import type { ToastItem } from '../design/ui';
import type { Confirmation } from '../person/Confirmations';

/** A press's key across its two sources: a terminal's and the service's ids are not one space. */
const keyOf = (press: Pick<Confirmation, 'source' | 'id'>) => `${press.source}:${press.id}`;

/** Whether this window is the one in front of the person: visible and focused, so it, and no other, speaks (§5). */
function looking(): boolean {
  return typeof document !== 'undefined' && document.visibilityState === 'visible' && document.hasFocus();
}

/**
 * The toasts that say a press came (NOTIFY2b, D196 point 4, the notification design §3.3, §5, Fable's F1.1): a toast with *Open*
 * in the focused main window, said once, staying until the view opens or the press ends, never closed by its seven seconds.
 *
 * @remarks
 * **Said on an arrival, never a snapshot** (§5): a terminal's press on its own event (`SESSION_ATTENTION` of the kind `Confirming`,
 * told once at its ask), keyed by its id, so an event heard twice is said once; a service's press the first time the list this
 * window is told holds its id (NOTIFY2j: the shell tells every window the one list at once, so only the focused one says it), and
 * never one already waiting at this window's first read of the list, which arrived before the window did, as a terminal's does.
 * A window not focused as it hears it says nothing, and the press is counted in its bell; an arrival missed is never said later,
 * which a press may accept only because the bell's warning hue stays while it waits unshown.
 *
 * **Never over the view**: a press heard while *Notifications* is open says no toast, and the view's opening takes every one away.
 * Nothing here moves the focus, opens the view or confirms anything: the door shows the card, where only the person answers.
 */
export function usePressAnnouncements({ presses, serviceRead = true, viewOpen, words, openLabel, onOpen }: {
  /** The presses waiting now, of either source, as the confirmation channel tells them. */
  presses: Confirmation[];
  /** Whether this window has read the service's list yet: the presses its first read holds are no arrival. */
  serviceRead?: boolean;
  /** Whether *Notifications* is drawn in this window now. */
  viewOpen: boolean;
  /** A press's toast in the reader's language: its act and its session's name, never a value. */
  words: (press: Confirmation) => string;
  openLabel: string;
  /** The toast's door: *Notifications* shown at the press's card. */
  onOpen: (press: Confirmation) => void;
}): {
  /** A terminal's press's event heard, by the press's id. */
  heard: (id: string) => void;
  /** The toasts that stay, the newest last. */
  toasts: ToastItem[];
  /** The person's × on one: true where it was one of these, which is then gone. */
  close: (id: number) => boolean;
} {
  // What this window decided once for each press: said, or heard while it could not say it. Never said twice, never said later.
  const decided = useRef(new Set<string>());
  // The presses whose toast stands, each with its toast's id and whether its press has been seen waiting since.
  const [standing, setStanding] = useState<{ key: string; id: number; seen: boolean }[]>([]);
  const next = useRef(-1);
  const open = useRef(viewOpen);
  open.current = viewOpen;

  const say = useCallback((key: string) => {
    if (decided.current.has(key)) return;
    decided.current.add(key);
    if (open.current || !looking()) return;
    const id = next.current;
    next.current -= 1;
    setStanding((was) => [...was, { key, id, seen: false }]);
  }, []);

  const heard = useCallback((id: string) => say(keyOf({ source: 'pipe', id })), [say]);

  // A service's press is said the first time the list this window is told holds it, after its first read, which is a baseline; a
  // terminal's is decided by its event alone.
  const based = useRef(false);
  const waiting = presses.map(keyOf).join('\n');
  useEffect(() => {
    if (serviceRead) {
      for (const press of presses) {
        if (press.source !== 'service') continue;
        if (based.current) say(keyOf(press));
        else decided.current.add(keyOf(press));
      }
      based.current = true;
    }
    // A press seen waiting is marked so; one seen and gone since has ended, and its toast goes with it.
    const now = new Set(presses.map(keyOf));
    setStanding((was) => {
      const kept = was.filter((each) => !each.seen || now.has(each.key)).map((each) => (now.has(each.key) ? { ...each, seen: true } : each));
      return kept.length === was.length && kept.every((each, index) => each.seen === was[index]!.seen) ? was : kept;
    });
    // The keys of what waits name the change; the list is read through them.
  }, [waiting, serviceRead, say]);

  // The view opening takes every one away: it is where they all are.
  useEffect(() => {
    if (viewOpen) setStanding((was) => (was.length > 0 ? [] : was));
  }, [viewOpen]);

  const toasts: ToastItem[] = standing.flatMap((each) => {
    const press = presses.find((one) => keyOf(one) === each.key);
    return press
      ? [{ id: each.id, kind: 'error' as const, text: words(press), stays: true, door: { label: openLabel, onOpen: () => onOpen(press) } }]
      : [];
  });

  const close = (id: number) => {
    if (!standing.some((each) => each.id === id)) return false;
    setStanding((was) => was.filter((each) => each.id !== id));
    return true;
  };

  return { heard, toasts, close };
}
