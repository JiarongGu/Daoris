import type { AgentPart } from '../agents/agents';
import type { View } from '../menus/commands';
import type { KnowledgeMode } from '../knowledge/modes';
import { askItem, questsItem, type OpenGroup } from '../app/opener';
import { offerItem } from '../plugins/catalog';
import type { SettingsAnchor, SettingsSection } from '../settings/SettingsView';
import type { StarterDoor } from './starters';

/**
 * A place on the window a go names, as the driver judged it: a view, a Settings domain, a part, or the one record of the view's
 * list it opens (a quest's id, an ask's `ask:<id>`, a session's id); a go to Add or Import may carry the workspace, never a folder.
 */
export type HelpPlace = {
  view: string; domain?: string | null; part?: string | null; item?: string | null; workspace?: string | null;
  /** On a go to Sessions → New conversation, the repository the composer chooses and the words it opens with. */
  repository?: string | null; sentence?: string | null;
};

/**
 * The places Ask Daoris's go may name: the views, Settings' domains, and the parts of them a door
 * already opens — the groups of Sessions' and Quests' lists that wait on the person, the setup guide's steps, a domain's
 * cards, Repositories' drawers, a repository's Setup, Branches and History and a workspace's page's tabs and sections, Knowledge's two modes, an
 * agent's page's sections — and the places a go named before they moved.
 * @remarks
 * A twin (`.claude/knowledge/twins.md`) of the driver's `HelpPlaces`, which judges a go before the person
 * sees its card and lists these places in Ask Daoris's room. They share no code; each side's test holds
 * the same table, and they change together.
 * The bar's eight places and Settings, in the bar's order. The table holds the window's views only (its
 * test holds it to the bar); Knowledge's modes are parts within it (`PLACE_PARTS`), and a kept go that
 * names a mode as a view lands through `PLACE_KEPT`.
 */
export const PLACE_VIEWS: readonly View[] = [
  'overview', 'sessions', 'quests', 'projects', 'map', 'knowledge', 'agents', 'plugins', 'settings',
];

// The guide is Get started, Plugins a place of its own; Workspace and Permissions are a workspace's page's, kept below.
export const PLACE_DOMAINS: readonly SettingsSection[] = [
  'start', 'appearance', 'driver', 'ai', 'git', 'browser', 'space', 'updates', 'wiring', 'logs',
];

/**
 * The views a go may name one item in: Quests, whose list holds quests and asks, an ask
 * named as `askItem` names it, Sessions, whose list holds this machine's sessions by id, and Plugins: a plugin by its id,
 * an offer as `offerItem`; in the bar's order. The driver's `HelpPlaces.ItemViews`.
 */
export const PLACE_ITEM_VIEWS: readonly View[] = ['sessions', 'quests', 'plugins'];

/**
 * The places a go named under an older spelling, each with the place it is now: Search and Convergence
 * are Knowledge's modes, and Settings → Plugins is the Plugins place. Settings → Workspace and
 * Permissions are a workspace's page, each old spelling with its part a row of its own, and
 * Permissions alone what agents may do; a part no row names opens nothing. The twins list none of them, and a go still
 * spelled so, kept in an earlier conversation, opens where the place went rather than nowhere.
 */
export const PLACE_KEPT: readonly { was: HelpPlace; now: HelpPlace }[] = [
  { was: { view: 'search' }, now: { view: 'knowledge', part: 'search' } },
  { was: { view: 'convergence' }, now: { view: 'knowledge', part: 'convergence' } },
  { was: { view: 'settings', domain: 'plugins' }, now: { view: 'plugins' } },
  { was: { view: 'settings', domain: 'workspace' }, now: { view: 'projects', part: 'workspace-details' } },
  { was: { view: 'settings', domain: 'workspace', part: 'wiring' }, now: { view: 'projects', part: 'workspace-remote' } },
  { was: { view: 'settings', domain: 'workspace', part: 'lines' }, now: { view: 'projects', part: 'workspace-defaults' } },
  { was: { view: 'settings', domain: 'workspace', part: 'landing' }, now: { view: 'projects', part: 'workspace-defaults' } },
  { was: { view: 'settings', domain: 'workspace', part: 'sweep' }, now: { view: 'projects', part: 'workspace-branches' } },
  { was: { view: 'settings', domain: 'permissions' }, now: { view: 'agents', part: 'rules' } },
  { was: { view: 'settings', domain: 'permissions', part: 'across' }, now: { view: 'projects', part: 'workspace-defaults' } },
];

/**
 * A workspace's page's parts: its three tabs and its two sections, each the door that opens it.
 * Prefixed, since a repository's page has three of the tabs' names.
 */
const WORKSPACE_PARTS: Readonly<Record<string, Pick<StarterDoor, 'workspaceTab' | 'workspaceSection'>>> = {
  'workspace-details': { workspaceTab: 'details' },
  'workspace-branches': { workspaceTab: 'branches' },
  'workspace-workflow': { workspaceTab: 'workflow' },
  'workspace-defaults': { workspaceSection: 'defaults' },
  'workspace-remote': { workspaceSection: 'remote' },
};

/**
 * The groups that wait on the person below Sessions and Quests, each the group of the view's
 * list it brings into view: Sessions' by the driver's reader's names, Quests' asks and held quests by its own.
 */
const GROUP_PARTS: Readonly<Record<string, OpenGroup>> = {
  'sessions/needs-you': 'needs-you',
  'sessions/failed': 'failed',
  'quests/asks': 'asks',
  'quests/held': 'held',
};

const same = (one: HelpPlace, other: HelpPlace) =>
  one.view === other.view && (one.domain ?? null) === (other.domain ?? null) && (one.part ?? null) === (other.part ?? null);

/** The parts, each within a view (Sessions, Quests, Repositories, Knowledge, Agents) or a Settings domain. */
export const PLACE_PARTS: readonly { within: string; part: string }[] = [
  // What waits on the person below Sessions and Quests, a group of the list (`GROUP_PARTS`).
  // Overview has no part, since what waits on the person leads it.
  { within: 'sessions', part: 'needs-you' }, { within: 'sessions', part: 'failed' }, { within: 'sessions', part: 'new' },
  { within: 'quests', part: 'asks' }, { within: 'quests', part: 'held' },
  // A repository's Setup, where its own values are set; a go names no repository.
  { within: 'projects', part: 'add' }, { within: 'projects', part: 'import' }, { within: 'projects', part: 'setup' },
  // A repository's Branches and History tabs, likewise on the repository Repositories has chosen.
  { within: 'projects', part: 'branches' }, { within: 'projects', part: 'history' },
  // A workspace's page's tabs and its two sections (`WORKSPACE_PARTS`).
  { within: 'projects', part: 'workspace-details' }, { within: 'projects', part: 'workspace-branches' },
  { within: 'projects', part: 'workspace-workflow' },
  { within: 'projects', part: 'workspace-defaults' }, { within: 'projects', part: 'workspace-remote' },
  // Knowledge's two modes, by the names its list's choice shows.
  { within: 'knowledge', part: 'search' }, { within: 'knowledge', part: 'convergence' },
  { within: 'start', part: 'agent' }, { within: 'start', part: 'helper' }, { within: 'start', part: 'repositories' },
  { within: 'start', part: 'setUp' }, { within: 'start', part: 'rules' },
  // The agent's page's sections a door opens; Permissions' Proposals are its What it may do.
  { within: 'agents', part: 'accounts' }, { within: 'agents', part: 'rules' }, { within: 'agents', part: 'usage' },
  // The Agents place's Roles mode, where the working roles are set.
  { within: 'agents', part: 'roles' },
];

/**
 * Where a go takes the person, as a starter's door: a domain of Settings at the card or the setup
 * step it names, a view, Sessions or Quests with a group of its list brought into view, one of Repositories' drawers, a
 * repository's Setup, Branches or History tab or a workspace's page at a tab or a section, Knowledge in a mode, or a section of an agent's page — or
 * null for a place this window does not have, which is never guessed at. Pure, so every place is an argument.
 * @remarks
 * A go opens no repository's, agent's or workspace's page by name: Repositories' Setup, Branches and History open on the repository its list
 * has chosen, an agent's part on the agent that has it, a workspace's part on the workspace in view's page,
 * and a group of Sessions or Quests with no session or quest in it chosen, as the room tells the helper. On
 * Quests it may name the one quest or ask instead of a part, and on Sessions the one session, which
 * the list chooses as any door's item. A go to Add or Import may name a workspace, the one the drawer
 * opens filled with; on any other place a workspace is ignored.
 * Knowledge with no part opens in the mode it is left in. A go spelled under an older place name opens where
 * it went (`PLACE_KEPT`).
 */
export function placeDoor(asked: HelpPlace): StarterDoor | null {
  const place = PLACE_KEPT.find(({ was }) => same(was, asked))?.now ?? asked;
  // The table holds the window's views only; a mode is a view's name only in a kept go, handled above.
  const view = PLACE_VIEWS.find((known): known is View => known === place.view);
  if (!view) return null;
  const domain = place.domain ?? null;
  const part = place.part ?? null;

  // The one quest or ask, alone in its view, named as Quests' list names it.
  const item = asked.item?.trim() || null;
  if (item !== null) {
    if (!PLACE_ITEM_VIEWS.includes(view) || domain !== null || part !== null) return null;
    // The one session, by the id Sessions' list names it by; Quests' prefixes name no session.
    if (view === 'sessions') return item.includes(':') ? null : { view, item };
    // An installed plugin by its id, or an offer as the catalogue names one; no other prefix names a plugin.
    if (view === 'plugins') {
      const offered = item.startsWith(offerItem(''));
      const id = (offered ? item.slice(offerItem('').length) : item).trim();
      return !id || id.includes(':') ? null : { view, item: offered ? offerItem(id) : id };
    }
    const named = questsItem(item);
    const id = 'ask' in named ? named.ask : named.quest;
    if (!id) return null;
    return { view, item: 'ask' in named ? askItem(id) : id };
  }

  if (domain !== null) {
    const section = view === 'settings' ? PLACE_DOMAINS.find((known) => known === domain) : undefined;
    if (!section) return null;
    if (part === null) return { view, section };
    if (!PLACE_PARTS.some((known) => known.within === section && known.part === part)) return null;
    // A setup step is found by its own id in the guide, a card by its domain's.
    const anchor = (section === 'start' ? `step-${part}` : part) as SettingsAnchor;
    return { view, section, anchor };
  }

  if (part === null) return { view };
  if (!PLACE_PARTS.some((known) => known.within === view && known.part === part)) return null;
  if (view === 'projects') {
    // The page opens the workspace in view at the tab or section, as a door naming one does (`doorOpening`).
    const workspace = WORKSPACE_PARTS[part];
    if (workspace) return { view, ...workspace };
    // A repository's own values are its place's, which names no repository here: the Setup mode's list is where one is chosen.
    if (part === 'setup') return { view: 'agents', agentsMode: 'setup' };
    if (part === 'branches' || part === 'history') return { view, tab: part };
    // Add or Import opens with the go's workspace filled; the folder stays the person's pick.
    const filled = asked.workspace?.trim();
    return { view, drawer: part as 'add' | 'import', ...(filled ? { drawerWorkspace: filled } : {}) };
  }
  if (view === 'agents') return part === 'roles' ? { view, agentsMode: 'setup' } : { view, agentPart: part as AgentPart };
  if (view === 'knowledge') return { view, knowledge: part as KnowledgeMode };
  // The composer opens, with the repository chosen and the words in where the go named them; neither is sent.
  if (view === 'sessions' && part === 'new') {
    const repository = asked.repository?.trim();
    const sentence = asked.sentence?.trim();
    return { view, compose: { ...(repository ? { repository } : {}), ...(sentence ? { sentence } : {}) } };
  }
  // The list brings the group into view; the person opens the session or quest in it they mean.
  const group = GROUP_PARTS[`${view}/${part}`];
  return group ? { view, group } : null;
}
