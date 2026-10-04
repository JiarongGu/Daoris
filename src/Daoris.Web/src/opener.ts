import type { AgentPart } from './agents/agents';
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

/**
 * What a door names besides its item: the part of a Settings domain, one of Repositories' forms, or the repository whose
 * code map the Map opens on.
 */
export type OpenPart = { anchor?: SettingsAnchor; drawer?: 'add' | 'import'; code?: string; agentPart?: AgentPart };

/** What opening a view does, as a value. */
export type Opening = {
  view: View;
  /**
   * The item the view's list has chosen now, where the view has a list and the door named one (§3f). Its main area
   * shows it: a quest's record or an ask's on Quests since FRAME1d, which no longer opens either in a drawer.
   */
  chosen?: { view: ListView; item: string };
  /** The part of Settings' domain to bring into view, or null for its top, where the door named a domain. */
  anchor?: SettingsAnchor | null;
  /** One of Repositories' forms, which stay drawers (§3d). */
  drawer?: 'add' | 'import';
  /**
   * The repository whose code map the Map opens on (MAP3a), where a repository's page names it (FRAME1e). The Map has no
   * list (§4), so this is no chosen item: it is the page one level in.
   */
  code?: string;
  /**
   * The part of an agent's page to open and bring into view (UX6e, D150 §2.4): its accounts, what it may do, its usage. A
   * door naming a part and no agent opens the agent that has it.
   */
  agentPart?: AgentPart;
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
  // A door naming a domain opens it at the part it names, or at its top: never at a part another door left.
  if (view === 'settings' && (item || part.anchor)) plan.anchor = part.anchor ?? null;
  if (view === 'projects' && part.drawer) plan.drawer = part.drawer;
  if (view === 'map' && part.code) plan.code = part.code;
  if (view === 'agents' && part.agentPart) plan.agentPart = part.agentPart;
  return plan;
}

/**
 * Where a starter's, a setup step's or Ask Daoris's door leads (HELP1d, SETUP1a, HELP6): its view and the
 * item it names, a Settings domain at its part, or one of Repositories' forms.
 */
export function doorOpening(door: StarterDoor): Opening {
  return opening(door.view, door.item ?? door.section, door);
}
