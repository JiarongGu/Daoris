import type { View } from './commands';
import type { StarterDoor } from './help/starters';
import type { SettingsAnchor } from './SettingsView';
import { LIST_BOUNDS, type ListView } from './work/layout';

// The application's one opener (D118 §3i): every door into a view says what it opens — the palette, a
// status item, a row of *What needs you*, a map's detail, a notification, Ask Daoris's go — and the view's
// chosen item takes it. Planned here, as a value, and applied by `App`, which holds what it changes.

const ASK = 'ask:';

/** An ask's item: Quests' list holds asks and quests, so an item there names which (D118 §2). */
export const askItem = (id: string) => `${ASK}${id}`;

/** What one of Quests' items is: an ask, or a quest. */
export function questsItem(item: string): { ask: string } | { quest: string } {
  return item.startsWith(ASK) ? { ask: item.slice(ASK.length) } : { quest: item };
}

/** What a door names besides its item: the part of a Settings domain, or one of Repositories' forms. */
export type OpenPart = { anchor?: SettingsAnchor; drawer?: 'add' | 'import' };

/** What opening a view does, as a value. */
export type Opening = {
  view: View;
  /** The item the view's list has chosen now, where the view has a list and the door named one (§3f). */
  chosen?: { view: ListView; item: string };
  /** A quest's record, which Quests opens in its drawer until its list and main area (FRAME1d). */
  quest?: string;
  /** An ask's record, likewise. */
  ask?: string;
  /** The part of Settings' domain to bring into view, or null for its top, where the door named a domain. */
  anchor?: SettingsAnchor | null;
  /** One of Repositories' forms, which stay drawers (§3d). */
  drawer?: 'add' | 'import';
};

const listed = (view: View): view is View & ListView => Object.hasOwn(LIST_BOUNDS, view);

/**
 * What `open(view, item?)` does: the view, and the item its list now has chosen. A view with no list,
 * Overview or Map, keeps no chosen item (§4). An item a view cannot show is the view's own business, as a
 * session that has gone or a domain this window lacks always was.
 */
export function opening(view: View, item?: string | null, part: OpenPart = {}): Opening {
  const plan: Opening = { view };
  if (item && listed(view)) plan.chosen = { view, item };
  if (item && view === 'quests') Object.assign(plan, questsItem(item));
  // A door naming a domain opens it at the part it names, or at its top: never at a part another door left.
  if (view === 'settings' && (item || part.anchor)) plan.anchor = part.anchor ?? null;
  if (view === 'projects' && part.drawer) plan.drawer = part.drawer;
  return plan;
}

/**
 * Where a starter's, a setup step's or Ask Daoris's door leads (HELP1d, SETUP1a, HELP6): its view and the
 * item it names, a Settings domain at its part, or one of Repositories' forms.
 */
export function doorOpening(door: StarterDoor): Opening {
  return opening(door.view, door.item ?? door.section, door);
}
