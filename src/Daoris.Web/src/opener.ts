import type { AgentPart } from './agents/agents';
import type { View } from './commands';
import type { StarterDoor } from './help/starters';
import type { KnowledgeMode } from './knowledge/modes';
import type { ProjectTab, WorkspaceSection, WorkspaceTab } from './projects/tabs';
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

const WORKSPACE = 'workspace:';

/** A workspace's item: Repositories' list holds workspaces and repositories, so an item there names which (UX6g, §4.1). */
export const workspaceItem = (name: string) => `${WORKSPACE}${name}`;

/** What one of Repositories' items is: a workspace, or a repository. */
export function projectsItem(item: string): { workspace: string } | { repository: string } {
  return item.startsWith(WORKSPACE) ? { workspace: item.slice(WORKSPACE.length) } : { repository: item };
}

/**
 * What a door names besides its item: the part of a Settings domain, one of Repositories' forms, a repository page's tab
 * or a workspace page's tab and Setup section, the repository whose code map the Map opens on, or Knowledge's mode.
 */
export type OpenPart = {
  anchor?: SettingsAnchor; drawer?: 'add' | 'import'; tab?: ProjectTab; code?: string; agentPart?: AgentPart;
  workspaceTab?: WorkspaceTab; workspaceSection?: WorkspaceSection; knowledge?: KnowledgeMode;
};

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
   * The tab a repository's page opens at (UX6f, D150 §4.2): Settings' doors into a repository's own value open its Setup.
   * The tab is the view's, remembered for whichever repository it shows.
   */
  tab?: ProjectTab;
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
  /**
   * The tab a workspace's page opens at (UX6g, D150 §4.3), and the section of its Setup to open: the remote, or its
   * defaults. Its doors are what Settings → Workspace and Permissions held, now Repositories → the workspace's page.
   */
  workspaceTab?: WorkspaceTab;
  workspaceSection?: WorkspaceSection;
  /**
   * The mode Knowledge opens in (UX6i, D150 §2.2): a door into Search or Convergence names it, and its item is chosen in
   * that mode's list. A door into the place names none, and it opens in the mode it was left in.
   */
  knowledge?: KnowledgeMode;
};

const listed =(view: View): view is View & ListView => Object.hasOwn(LIST_BOUNDS, view);

/**
 * What `open(view, item?)` does: the view, and the item its list now has chosen. A view with no list,
 * Overview or Map, keeps no chosen item (§4). An item a view cannot show is the view's own business, as a
 * session that has gone or a domain this window lacks always was.
 *
 * `here` is the workspace in view, where there is one (the scope, or the only workspace): a door naming a workspace's page
 * and no workspace opens that one (UX6g), as a door naming an agent's part and no agent opens the agent that has it.
 */
export function opening(view: View, item?: string | null, part: OpenPart = {}, here: string | null = null): Opening {
  const plan: Opening = { view };
  // A section is a part of Setup, so a door naming one opens Setup.
  const workspaceTab = part.workspaceTab ?? (part.workspaceSection ? 'setup' : undefined);
  const named = item || (view === 'projects' && workspaceTab && here ? workspaceItem(here) : null);
  if (view === 'knowledge') {
    // Each mode's list keeps its own chosen item, so an item is chosen only in the mode the door names: with none it
    // could be either list's, and is never guessed at.
    if (part.knowledge) plan.knowledge = part.knowledge;
    if (named && part.knowledge) plan.chosen = { view: part.knowledge, item: named };
  } else if (named && listed(view)) plan.chosen = { view, item: named };
  // A door naming a domain opens it at the part it names, or at its top: never at a part another door left.
  if (view === 'settings' && (item || part.anchor)) plan.anchor = part.anchor ?? null;
  if (view === 'projects' && part.drawer) plan.drawer = part.drawer;
  if (view === 'projects' && part.tab) plan.tab = part.tab;
  if (view === 'projects' && workspaceTab) plan.workspaceTab = workspaceTab;
  if (view === 'projects' && part.workspaceSection) plan.workspaceSection = part.workspaceSection;
  if (view === 'map' && part.code) plan.code = part.code;
  if (view === 'agents' && part.agentPart) plan.agentPart = part.agentPart;
  return plan;
}

/**
 * Where a starter's, a setup step's or Ask Daoris's door leads (HELP1d, SETUP1a, HELP6): its view and the
 * item it names, a Settings domain at its part, one of Repositories' forms, or a workspace's page, the one in view
 * (`here`) where it names none.
 */
export function doorOpening(door: StarterDoor, here: string | null = null): Opening {
  return opening(door.view, door.item ?? door.section, door, here);
}
