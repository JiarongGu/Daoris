import { createContext, type ReactNode, useContext } from 'react';
import { type HelpPlace, placeDoor } from './places';
import type { StarterDoor } from './starters';

/** A go link's address before its fields, as the driver's room spells it (`HelpRoomPlaces.Link`). */
export const GO_LINK = 'daoris:go';

const FIELDS = ['domain', 'part', 'item', 'workspace', 'repository', 'sentence'] as const;

/**
 * The place a go link names, `daoris:go?view=…&domain=…`, each field as Ask Daoris's room lists it, or null for any other address
 * and for one with no view. Whether the window has the place is `placeDoor`'s to say.
 */
export function goPlace(href: string | null | undefined): HelpPlace | null {
  if (!href?.startsWith(`${GO_LINK}?`)) return null;
  const fields = new URLSearchParams(href.slice(GO_LINK.length + 1));
  const view = fields.get('view')?.trim();
  if (!view) return null;
  const place: HelpPlace = { view };
  for (const field of FIELDS) {
    const value = fields.get(field)?.trim();
    if (value) place[field] = value;
  }
  return place;
}

/**
 * What opens a go link's place in the window: Ask Daoris's panel provides it, as its starters' doors open theirs. Context, so the
 * Markdown deep in a conversation needs nothing threaded through it; with none provided a go link is words alone.
 */
export const GoOpener = createContext<((door: StarterDoor) => void) | null>(null);

/**
 * A go link in an agent's words: a press that takes the person to the place it names and changes nothing. One whose place this
 * window does not have, or with nothing to open it, is its words alone, never a link that leaves the window.
 */
export function GoLink({ href, children }: { href: string | undefined; children: ReactNode }) {
  const open = useContext(GoOpener);
  const place = goPlace(href);
  const door = place ? placeDoor(place) : null;
  if (!open || !door) return <>{children}</>;
  return (
    <button type="button" className="cursor-pointer border-0 bg-transparent p-0 font-[inherit] text-accent underline underline-offset-2"
      onClick={() => open(door)}>
      {children}
    </button>
  );
}
