import type { AgentPart } from '../agents/agents';
import { type AgentsMode, repositoryId, type RoleSection, workspaceId } from '../agents/modes';
import type { PlaceAsk } from '../agents/setup/placePlan';
import type { PlaceAt, PlaceSection } from '../agents/setup/placeShell';
import type { PlanDoor } from '../person/Confirmations';
import type { View } from '../menus/commands';
import type { StarterDoor } from '../help/starters';
import type { KnowledgeMode } from '../knowledge/modes';
import type { ProjectTab, WorkspaceSection, WorkspaceTab } from '../projects/tabs';
import type { SettingsAnchor } from '../settings/SettingsView';
import { LIST_BOUNDS, type ListView } from '../work/frame/layout';

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
 * A group of a view's list that a door brings into view (ENTRY1b, D161's ENTRY1 note): what waits on the person there.
 * Sessions' *Waiting on you* and *To review*, by the driver's reader's names (`SessionGroupName`), and Quests' asks and
 * the quests held for the person (`questGroups`). A door naming a group names no item in it.
 */
export type OpenGroup = 'you' | 'review' | 'asks' | 'held';

/** The view whose list holds each group: a group means nothing to another view. */
const GROUP_VIEW: Readonly<Record<OpenGroup, View>> = { you: 'sessions', review: 'sessions', asks: 'quests', held: 'quests' };

/**
 * The composer opened by a go to New conversation (NEW1d-b, D162): the repository it chooses and the words it opens with,
 * each only where the go named one. Neither is sent; the person presses *Start*.
 */
export type Compose = { repository?: string; sentence?: string };

/**
 * What a door names besides its item: the part of a Settings domain, one of Repositories' forms, a repository page's tab
 * or a workspace page's tab and Setup section, the repository whose code map the Map opens on, Knowledge's mode, or a group of
 * Sessions' or Quests' list.
 */
export type OpenPart = {
  anchor?: SettingsAnchor; drawer?: 'add' | 'import'; drawerWorkspace?: string; tab?: ProjectTab; code?: string; agentPart?: AgentPart; agentsMode?: AgentsMode; roleSection?: RoleSection;
  workspaceTab?: WorkspaceTab; workspaceSection?: WorkspaceSection; knowledge?: KnowledgeMode; group?: OpenGroup;
  compose?: Compose;
  /** A landed branch of the repository the door names, whose page opens with how it landed (FLOWCHART1c). */
  branch?: string;
  /** The quick set-up Ask Daoris proposed for the place the door names (AGENTSPLACE1k): the place's *Set up…* opens on it. */
  setUp?: PlaceAsk;
  /** The section of a place's page a door into the repository opens, and its row (SETUPUX1h): lit, `placeOpening` carries them to the page. */
  placeSection?: PlaceSection;
  placeRow?: string;
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
   * The workspace the form opens filled with (ENTRY1d2b, D161's ENTRY1d note): Ask Daoris's go names it, and the person
   * still picks the folder there. Only with a form.
   */
  drawerWorkspace?: string;
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
  /**
   * The mode the Agents place opens in (COMPANYVIEW1b): a door into Roles names it, and a door naming an agent or a part of its page
   * names Agents. A door into the place names none, and it opens in the mode it was left in.
   */
  agentsMode?: AgentsMode;
  /** The Roles section a door opens (COMPANYVIEW1e): it names Roles' mode, and the section is chosen in Roles' list. */
  roleSection?: RoleSection;
  /** The place the Setup mode opens at (AGENTSPLACE1f), by the id its list chooses it under (`repositoryId`, `workspaceId`). */
  place?: string;
  /**
   * Where a place's page opens (SETUPUX1h): the section a door names, and the row of it, with the page the door came from for *Back*.
   * Set by a door into a repository's page or by the page's own doors; the Workflow tab names Workflow.
   */
  placeAt?: PlaceAt;
  /**
   * The group of Sessions' or Quests' list to bring into view (ENTRY1b, D161's ENTRY1 note): what waits on the person there,
   * with no item in it chosen. The list brings it once it has answered, then lets it go (`useBringGroup`).
   */
  group?: OpenGroup;
  /** The composer opened with these (NEW1d-b): Sessions' main area, the repository chosen and the words in. */
  compose?: Compose;
  /**
   * A landed branch's page to open on its repository's Branches, with how it landed open (FLOWCHART1c): the work's milestone and a
   * run's landing step name it. Page state of Repositories', so it is an event its view consumes, as a drawer is.
   */
  branch?: { repository: string; branch: string };
  /**
   * The set-up the Agents place's Setup opens on (AGENTSPLACE1k), for the place it chooses. Dark, the Details it names are all a door
   * opens, and the ask is not carried.
   */
  setUp?: PlaceAsk;
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
    const mode = part.agentsMode ?? (part.roleSection ? 'setup' : named || part.agentPart ? 'agents' : undefined);
    if (mode) plan.agentsMode = mode;
    if (part.roleSection) plan.roleSection = part.roleSection;
  }
  if (part.group && GROUP_VIEW[part.group] === view) plan.group = part.group;
  if (view === 'sessions' && part.compose) plan.compose = part.compose;
  if (view === 'projects' && part.setUp) plan.setUp = part.setUp;
  if (view === 'projects' && part.placeSection) plan.placeAt = { section: part.placeSection, ...(part.placeRow ? { row: part.placeRow } : {}) };
  return plan;
}

/**
 * **Every door into a repository's Setup tab or its Workflow tab, once the Setup mode is lit, opens the place's page** (AGENTSPLACE1f and
 * 1h, the Agents-place design §6): What needs you's declaration wait, Get started's and the help room's steps, Settings' doors, a Roles
 * section's door to the workflow and a workflow step's. They all end in
 * `apply`, so one redirect there holds them all, where a census of the doors cannot miss one. A workspace's *Sessions' access* is the
 * workspace place's too; the rest of a workspace's Setup stays its tab until its own row moves. A door naming the tab and no repository
 * (the help room's) opens the mode, whose list chooses one. Dark, the plan is unchanged.
 *
 * `workspaceOfRepository` answers the workspace a repository sits in, as the registry holds it.
 */
export function placeOpening(plan: Opening, lit: boolean, workspaceOfRepository: (repository: string) => string): Opening {
  if (!lit || plan.view !== 'projects') return plan;
  // AGENTSPLACE1k: Ask Daoris's set-up card opens the place's page with its ask, a repository's or the workspace's the repositories are in.
  if (plan.setUp && plan.chosen?.view === 'projects') {
    const named = projectsItem(plan.chosen.item);
    const place = 'repository' in named ? repositoryId(workspaceOfRepository(named.repository), named.repository) : workspaceId(named.workspace);
    return { view: 'agents', agentsMode: 'setup', place, setUp: plan.setUp };
  }

  // The help room's door names the tab and no repository: the Setup mode's list is where one is chosen.
  const placed = plan.tab === 'setup' || plan.tab === 'workflow';
  if (placed && !plan.chosen) return { view: 'agents', agentsMode: 'setup' };
  if (plan.chosen?.view !== 'projects') return plan;
  const named = projectsItem(plan.chosen.item);
  if (placed && 'repository' in named) {
    // SETUPUX1h: a door names the page's section and row; the Workflow tab's names Workflow.
    const at: PlaceAt | undefined = plan.placeAt ?? (plan.tab === 'workflow' ? { section: 'workflow' } : undefined);
    return { view: 'agents', agentsMode: 'setup', place: repositoryId(workspaceOfRepository(named.repository), named.repository), ...(at ? { placeAt: at } : {}) };
  }
  if (plan.workspaceTab === 'setup' && plan.workspaceSection === 'access' && 'workspace' in named) {
    return { view: 'agents', agentsMode: 'setup', place: workspaceId(named.workspace) };
  }
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

/**
 * Where a press's plan door leads (CONFIRMWAIT1c, the confirm-wait design §8.1, §15.3 item 13): the page that shows the plan the
 * press rests on, as it stands now, scoped as the press is. A land opens its session at its review (`review`, the Work frame's
 * intent); a set-up its repository's Details, where its Set up is drawn; a sweep or a sync its workspace's Branches, a repository's
 * sync the workspace holding it (`holding`), and either naming none the one in view (`here`); a clear its workspace's Details,
 * where *Kept on this machine* is, or its quest's or its ask's page.
 */
export function planOpening(
  door: PlanDoor, here: string | null, holding: (repository: string) => string | null,
): { opening: Opening; review?: true } {
  switch (door.plan) {
    case 'land':
      return { opening: opening('sessions', door.session), review: true };
    case 'setup':
      return { opening: opening('projects', door.repository, { tab: 'details' }) };
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
