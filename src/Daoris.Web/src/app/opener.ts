import type { AgentPart } from '../agents/agents';
import { type AgentsMode, MACHINE, repositoryId, workspaceId } from '../agents/modes';
import type { PlaceAt, PlaceSection } from '../agents/setup/placeShell';
import type { PlanDoor } from '../person/Confirmations';
import type { View } from '../menus/commands';
import type { StarterDoor } from '../help/starters';
import type { KnowledgeMode } from '../knowledge/modes';
import type { ProjectTab, WorkspaceSection, WorkspaceTab } from '../projects/tabs';
import type { SettingsAnchor } from '../settings/SettingsView';
import { LIST_BOUNDS, type ListView } from '../work/frame/layout';

// The application's one opener: every door into a view says what it opens, and the view's chosen item takes it.

const ASK = 'ask:';

/** An ask's item: Quests' list holds asks and quests, so an item there names which. */
export const askItem = (id: string) => `${ASK}${id}`;

/** What one of Quests' items is: an ask, or a quest. */
export function questsItem(item: string): { ask: string } | { quest: string } {
  return item.startsWith(ASK) ? { ask: item.slice(ASK.length) } : { quest: item };
}

const WORKSPACE = 'workspace:';

/** A workspace's item: Repositories' list holds workspaces and repositories, so an item there names which. */
export const workspaceItem = (name: string) => `${WORKSPACE}${name}`;

/** What one of Repositories' items is: a workspace, or a repository. */
export function projectsItem(item: string): { workspace: string } | { repository: string } {
  return item.startsWith(WORKSPACE) ? { workspace: item.slice(WORKSPACE.length) } : { repository: item };
}

/**
 * A group of a view's list that a door brings into view: what waits on the person there.
 * Sessions' *Needs you* and *Failed*, by the driver's reader's names (`SessionGroupName`), and Quests' asks and
 * the quests held for the person (`questGroups`). A door naming a group names no item in it.
 */
export type OpenGroup = 'needs-you' | 'failed' | 'asks' | 'held';

/** The view whose list holds each group: a group means nothing to another view. */
const GROUP_VIEW: Readonly<Record<OpenGroup, View>> = { 'needs-you': 'sessions', failed: 'sessions', asks: 'quests', held: 'quests' };

/**
 * The composer opened by a go to New conversation: the repository it chooses and the words it opens with,
 * each only where the go named one. Neither is sent; the person presses *Start*.
 */
export type Compose = { repository?: string; sentence?: string };

/** The parts of This machine's page a door opens at: its Appointed roles row, or the All roles section. */
export type RoleDoor = 'appointed' | 'allRoles';

/**
 * What a door names besides its item: the part of a Settings domain, one of Repositories' forms, a repository page's tab
 * or a workspace page's tab and Setup section, the repository whose code map the Map opens on, Knowledge's mode, or a group of
 * Sessions' or Quests' list.
 */
export type OpenPart = {
  anchor?: SettingsAnchor; drawer?: 'add' | 'import'; drawerWorkspace?: string; tab?: ProjectTab; placeOf?: string; code?: string; agentPart?: AgentPart; agentsMode?: AgentsMode; roleSection?: RoleDoor;
  workspaceTab?: WorkspaceTab; workspaceSection?: WorkspaceSection; knowledge?: KnowledgeMode; group?: OpenGroup;
  compose?: Compose;
  /** A landed branch of the repository the door names, whose page opens with how it landed. */
  branch?: string;
  /** The repository whose place a door opens in the Agents place's Setup mode, at the section and row it names. */
  placeSection?: PlaceSection;
  placeRow?: string;
  /** The host Settings → Git's add form opens filled with: a held remote's door, Ask Daoris's screen door. */
  gitHost?: string;
};

/** What opening a view does, as a value. */
export type Opening = {
  view: View;
  /**
   * The item the view's list has chosen now, where the view has a list and the door named one. Its main area
   * shows it: a quest's record or an ask's on Quests, which open in the main area, never in a drawer.
   */
  chosen?: { view: ListView; item: string };
  /** The part of Settings' domain to bring into view, or null for its top, where the door named a domain. */
  anchor?: SettingsAnchor | null;
  /**
   * The host Settings → Git opens its add form on, or null for none, where the door named the Git
   * domain: a door into it never inherits a host another door left.
   */
  gitHost?: string | null;
  /** One of Repositories' forms, which stay drawers. */
  drawer?: 'add' | 'import';
  /**
   * The workspace the form opens filled with: Ask Daoris's go names it, and the person
   * still picks the folder there. Only with a form.
   */
  drawerWorkspace?: string;
  /** The tab a repository's page opens at, remembered for whichever repository the view shows. */
  tab?: ProjectTab;
  /** The repository whose place the Setup mode opens at: `placeOpening` names the place by the workspace the registry holds it in. */
  placeOf?: string;
  /**
   * The repository whose code map the Map opens on, where a repository's page names it. The Map has no
   * list, so this is no chosen item: it is the page one level in.
   */
  code?: string;
  /**
   * The part of an agent's page to open and bring into view: its accounts, what it may do, its usage. A
   * door naming a part and no agent opens the agent that has it.
   */
  agentPart?: AgentPart;
  /**
   * The tab a workspace's page opens at, and the section of its Setup to open: the remote, or its
   * defaults. Its doors are what Settings → Workspace and Permissions held, now Repositories → the workspace's page.
   */
  workspaceTab?: WorkspaceTab;
  workspaceSection?: WorkspaceSection;
  /**
   * The mode Knowledge opens in: a door into Search or Convergence names it, and its item is chosen in
   * that mode's list. A door into the place names none, and it opens in the mode it is left in.
   */
  knowledge?: KnowledgeMode;
  /**
   * The mode the Agents place opens in: a door into Roles names it, and a door naming an agent or a part of its page
   * names Agents. A door into the place names none, and it opens in the mode it is left in.
   */
  agentsMode?: AgentsMode;
  /** The part of This machine's page a door opens: it names the Setup mode, and the page opens at that part. */
  roleSection?: RoleDoor;
  /** The place the Setup mode opens at, by the id its list chooses it under (`repositoryId`, `workspaceId`). */
  place?: string;
  /**
   * Where a place's page opens: the section a door names, and the row of it, with the page the door came from for *Back*.
   * Set by a door naming a repository's place or by the page's own doors.
   */
  placeAt?: PlaceAt;
  /**
   * The group of Sessions' or Quests' list to bring into view: what waits on the person there,
   * with no item in it chosen. The list brings it once it has answered, then lets it go (`useBringGroup`).
   */
  group?: OpenGroup;
  /** The composer opened with these: Sessions' main area, the repository chosen and the words in. */
  compose?: Compose;
  /**
   * A landed branch's page to open on its repository's Branches, with how it landed open: the work's milestone and a
   * run's landing step name it. Page state of Repositories', so it is an event its view consumes, as a drawer is.
   */
  branch?: { repository: string; branch: string };
};

const listed =(view: View): view is View & ListView => Object.hasOwn(LIST_BOUNDS, view);

/**
 * What `open(view, item?)` does: the view, and the item its list now has chosen. A view with no list,
 * Overview or Map, keeps no chosen item. An item a view cannot show is the view's own business, as a
 * session that has gone or a domain this window lacks always is.
 *
 * `here` is the workspace in view, where there is one (the scope, or the only workspace): a door naming a workspace's page
 * and no workspace opens that one, as a door naming an agent's part and no agent opens the agent that has it.
 */
export function opening(view: View, item?: string | null, part: OpenPart = {}, here: string | null = null): Opening {
  const plan: Opening = { view };
  const workspaceTab = part.workspaceTab;
  const named = item || (view === 'projects' && (workspaceTab || part.workspaceSection) && here ? workspaceItem(here) : null);
  if (view === 'knowledge') {
    // Each mode's list keeps its own chosen item, so an item is chosen only in the mode the door names: with none it
    // could be either list's, and is never guessed at.
    if (part.knowledge) plan.knowledge = part.knowledge;
    if (named && part.knowledge) plan.chosen = { view: part.knowledge, item: named };
  } else if (named && listed(view)) plan.chosen = { view, item: named };
  // A door naming a domain opens it at the part it names, or at its top: never at a part another door left.
  if (view === 'settings' && (item || part.anchor)) plan.anchor = part.anchor ?? null;
  if (view === 'settings' && item === 'git') {
    plan.gitHost = part.gitHost ?? null;
    if (part.gitHost && !part.anchor) plan.anchor = 'git-add';
  }
  if (view === 'projects' && part.drawer) {
    plan.drawer = part.drawer;
    if (part.drawerWorkspace) plan.drawerWorkspace = part.drawerWorkspace;
  }
  if (view === 'projects' && part.tab) plan.tab = part.tab;
  // A branch is a repository's: with none named, there is no page to open it on.
  if (view === 'projects' && part.branch && item && 'repository' in projectsItem(item)) {
    plan.tab = 'branches';
    plan.branch = { repository: item, branch: part.branch };
  }
  if (view === 'projects' && workspaceTab) plan.workspaceTab = workspaceTab;
  if (view === 'projects' && part.workspaceSection) plan.workspaceSection = part.workspaceSection;
  if (view === 'map' && part.code) plan.code = part.code;
  if (view === 'agents' && part.agentPart) plan.agentPart = part.agentPart;
  if (view === 'agents') {
    const mode = part.agentsMode ?? (part.roleSection || part.placeOf ? 'setup' : named || part.agentPart ? 'agents' : undefined);
    if (mode) plan.agentsMode = mode;
    if (part.roleSection) plan.roleSection = part.roleSection;
  }
  if (part.group && GROUP_VIEW[part.group] === view) plan.group = part.group;
  if (view === 'sessions' && part.compose) plan.compose = part.compose;
  if (view === 'agents' && part.placeOf) {
    plan.placeOf = part.placeOf;
    if (part.placeSection) plan.placeAt = { section: part.placeSection, ...(part.placeRow ? { row: part.placeRow } : {}) };
  }
  return plan;
}

/**
 * **A door naming a repository's place resolves it here**: a door has the repository,
 * and the place is named by the workspace the registry holds it in. They all end in `apply`, so one resolution there holds them all.
 * Each section of a workspace's Setup has
 * its home (`WORKSPACE_HOMES`); a door naming the workspace's tab and no section stays on the tab until it goes. A door naming the tab and no repository
 * (the help room's) opens the mode, whose list chooses one. Dark, the plan is unchanged.
 *
 * `workspaceOfRepository` answers the workspace a repository sits in, as the registry holds it.
 */
export function placeOpening(plan: Opening, workspaceOfRepository: (repository: string) => string): Opening {
  if (plan.view === 'agents' && plan.placeOf) {
    const { placeOf, ...rest } = plan;
    return { ...rest, agentsMode: 'setup', place: repositoryId(workspaceOfRepository(placeOf), placeOf) };
  }
  // A door into the Setup mode names This machine's page. Appointed roles is its Roles section's row; All roles is its own
  // section; a door naming the mode alone opens the page at Roles.
  if (plan.view === 'agents' && plan.agentsMode === 'setup' && !plan.place) {
    const { roleSection: _section, ...rest } = plan;
    const at: PlaceAt = plan.roleSection === 'appointed' ? { section: 'roles', row: 'appointed' } : plan.roleSection === 'allRoles' ? { section: 'allRoles' } : { section: 'roles' };
    return { ...rest, place: MACHINE, placeAt: at };
  }
  if (plan.view !== 'projects') return plan;
  if (plan.chosen?.view !== 'projects') return plan;
  const named = projectsItem(plan.chosen.item);
  if (plan.workspaceSection && 'workspace' in named) {
    // Each section of a workspace's setup has its home on its place. The remote is Repositories', a row of its Details.
    if (plan.workspaceSection === 'remote') return { ...plan, workspaceTab: 'details' };
    return { view: 'agents', agentsMode: 'setup', place: workspaceId(named.workspace), placeAt: WORKSPACE_HOMES[plan.workspaceSection] };
  }
  return plan;
}

/** Where each section of a workspace's setup lives on the workspace's place: its section and the row of it. */
const WORKSPACE_HOMES: Record<Exclude<WorkspaceSection, 'remote'>, PlaceAt> = {
  defaults: { section: 'workflow', row: 'defaults' },
  access: { section: 'sessions', row: 'access' },
  reach: { section: 'sessions', row: 'reach' },
  git: { section: 'sessions', row: 'git' },
  supervisor: { section: 'roles', row: 'supervisor' },
};

/**
 * Where a starter's, a setup step's or Ask Daoris's door leads: its view and the
 * item it names, a Settings domain at its part, one of Repositories' forms, or a workspace's page, the one in view
 * (`here`) where it names none.
 */
export function doorOpening(door: StarterDoor, here: string | null = null): Opening {
  return opening(door.view, door.item ?? door.section, door, here);
}

/**
 * Where a press's plan door leads: the page that shows the plan the press rests on, scoped as the press is. A sweep or a sync opens
 * its workspace's Branches, a repository's sync the workspace holding it (`holding`), and either naming none the one in view (`here`);
 * a clear its workspace's Details, where *Kept on this machine* is, or its quest's or its ask's page.
 */
export function planOpening(
  door: PlanDoor, here: string | null, holding: (repository: string) => string | null,
): { opening: Opening } {
  switch (door.plan) {
    case 'sweep':
    case 'sync': {
      const workspace = door.workspace ?? (door.plan === 'sync' && door.repository ? holding(door.repository) : null);
      return { opening: opening('projects', workspace ? workspaceItem(workspace) : null, { workspaceTab: 'branches' }, here) };
    }
    case 'clear':
      return {
        opening: door.scope === 'workspace'
          ? opening('projects', workspaceItem(door.id), { workspaceTab: 'details' })
          : opening('quests', door.scope === 'ask' ? askItem(door.id) : door.id),
      };
  }
}
