import type { AgentPart } from '../agents/agents';
import type { View } from '../commands';
import type { KnowledgeMode } from '../knowledge/modes';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { StarterDoor } from './starters';

/** A place on the window a go names (HELP6), as the driver judged it: a view, a Settings domain, a part. */
export type HelpPlace = { view: string; domain?: string | null; part?: string | null };

/**
 * The places Ask Daoris's go may name (HELP6): the views, Settings' domains, and the parts of them a door
 * already opens — the setup guide's steps, a domain's cards, Repositories' drawers, a repository's Setup and a
 * workspace's page's tabs and sections, Knowledge's two modes, an agent's page's sections — and the places a go named
 * before they moved.
 *
 * @remarks
 * A twin (`.claude/knowledge/twins.md`) of the driver's `HelpPlaces`, which judges a go before the person
 * sees its card and lists these places in Ask Daoris's room. They share no code; each side's test holds
 * the same table, and they change together.
 *
 * UX6i2a (D150 §2): the bar's eight places and Settings, in the bar's order. The table holds the window's views only (its
 * test holds it to the bar); Knowledge's modes are parts within it (`PLACE_PARTS`), and a go kept from before UX6i that
 * named a mode as a view lands through `PLACE_KEPT`.
 */
export const PLACE_VIEWS: readonly View[] = [
  'overview', 'sessions', 'quests', 'projects', 'map', 'knowledge', 'agents', 'plugins', 'settings',
];

// UX6i2a: the guide is Get started since UX6j, and Plugins left Settings for its place. UX6g2b: Workspace and Permissions
// left it for a workspace's page with UX6g, and are kept below.
export const PLACE_DOMAINS: readonly SettingsSection[] = ['start', 'appearance', 'ai', 'driver', 'browser', 'logs'];

/**
 * The places a go named before they moved, each with the place it is now (UX6i2a, D150 §2): Search and Convergence
 * became Knowledge's modes with UX6i, and Settings → Plugins the Plugins place with UX6j. UX6g2b: Settings → Workspace and
 * Permissions retired into a workspace's page with UX6g (D150 §3.1), each old spelling with its part a row of its own, and
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
 * A workspace's page's parts (UX6g2b, D150 §4.3): its four tabs and its Setup's two sections, each the door that opens it.
 * Prefixed, since a repository's page has three of the tabs' names.
 */
const WORKSPACE_PARTS: Readonly<Record<string, Pick<StarterDoor, 'workspaceTab' | 'workspaceSection'>>> = {
  'workspace-details': { workspaceTab: 'details' },
  'workspace-branches': { workspaceTab: 'branches' },
  'workspace-workflow': { workspaceTab: 'workflow' },
  'workspace-setup': { workspaceTab: 'setup' },
  'workspace-defaults': { workspaceSection: 'defaults' },
  'workspace-remote': { workspaceSection: 'remote' },
};

const same = (one: HelpPlace, other: HelpPlace) =>
  one.view === other.view && (one.domain ?? null) === (other.domain ?? null) && (one.part ?? null) === (other.part ?? null);

/** The parts, each within a view (Repositories, Knowledge, Agents) or a Settings domain. */
export const PLACE_PARTS: readonly { within: string; part: string }[] = [
  // HELPSETUP1: a repository's Setup (UX6f, D150 §4.2), where its own values are set; a go names no repository.
  { within: 'projects', part: 'add' }, { within: 'projects', part: 'import' }, { within: 'projects', part: 'setup' },
  // UX6g2b (D161 §3, D150 §4.3): a workspace's page's four tabs and its Setup's two sections (`WORKSPACE_PARTS`).
  { within: 'projects', part: 'workspace-details' }, { within: 'projects', part: 'workspace-branches' },
  { within: 'projects', part: 'workspace-workflow' }, { within: 'projects', part: 'workspace-setup' },
  { within: 'projects', part: 'workspace-defaults' }, { within: 'projects', part: 'workspace-remote' },
  // UX6i2a: Knowledge's two modes (UX6i), by the names its list's choice shows.
  { within: 'knowledge', part: 'search' }, { within: 'knowledge', part: 'convergence' },
  { within: 'start', part: 'agent' }, { within: 'start', part: 'helper' }, { within: 'start', part: 'repositories' },
  { within: 'start', part: 'driven' }, { within: 'start', part: 'landing' }, { within: 'start', part: 'rules' },
  // UX6e2: the agent's page's sections a door opens (D150 §5.2); Permissions' Proposals are its What it may do.
  { within: 'agents', part: 'accounts' }, { within: 'agents', part: 'rules' }, { within: 'agents', part: 'usage' },
];

/**
 * Where a go takes the person, as a starter's door (HELP6): a domain of Settings at the card or the setup
 * step it names, a view, one of Repositories' drawers, a repository's Setup or a workspace's page at a tab or a section,
 * Knowledge in a mode, or a section of an agent's page — or null for a place this window does not have, which is never
 * guessed at. Pure, so every place is an argument.
 *
 * @remarks
 * A go names no item: Repositories' Setup opens on the repository its list has chosen, an agent's part on the agent
 * that has it (UX6e), and a workspace's part on the workspace in view's page (UX6g2b), as the room tells the helper.
 * Knowledge with no part opens in the mode it was left in (UX6i). A go spelled as a place was before it moved opens where
 * it went (`PLACE_KEPT`).
 */
export function placeDoor(asked: HelpPlace): StarterDoor | null {
  const place = PLACE_KEPT.find(({ was }) => same(was, asked))?.now ?? asked;
  // The table holds the window's views only; a mode is a view's name only in a kept go, handled above.
  const view = PLACE_VIEWS.find((known): known is View => known === place.view);
  if (!view) return null;
  const domain = place.domain ?? null;
  const part = place.part ?? null;

  if (domain !== null) {
    const section = view === 'settings' ? PLACE_DOMAINS.find((known) => known === domain) : undefined;
    if (!section) return null;
    if (part === null) return { view, section };
    if (!PLACE_PARTS.some((known) => known.within === section && known.part === part)) return null;
    // A setup step is found by its own id in the guide (SETUP1a), a card by its domain's.
    const anchor = (section === 'start' ? `step-${part}` : part) as SettingsAnchor;
    return { view, section, anchor };
  }

  if (part === null) return { view };
  if (!PLACE_PARTS.some((known) => known.within === view && known.part === part)) return null;
  if (view === 'projects') {
    // The page opens the workspace in view at the tab or section, as a door naming one does (`doorOpening`).
    const workspace = WORKSPACE_PARTS[part];
    if (workspace) return { view, ...workspace };
    return part === 'setup' ? { view, tab: 'setup' } : { view, drawer: part as 'add' | 'import' };
  }
  if (view === 'agents') return { view, agentPart: part as AgentPart };
  if (view === 'knowledge') return { view, knowledge: part as KnowledgeMode };
  return null;
}
