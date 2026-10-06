import type { AgentPart } from '../agents/agents';
import type { View } from '../commands';
import type { KnowledgeMode } from '../knowledge/modes';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { StarterDoor } from './starters';

/** A place on the window a go names (HELP6), as the driver judged it: a view, a Settings domain, a part. */
export type HelpPlace = { view: string; domain?: string | null; part?: string | null };

/**
 * The places Ask Daoris's go may name (HELP6): the views, Settings' domains, and the parts of them a door
 * already opens — the setup guide's steps, a domain's cards, Repositories' drawers and a repository's Setup, an
 * agent's page's sections.
 *
 * @remarks
 * A twin (`.claude/knowledge/twins.md`) of the driver's `HelpPlaces`, which judges a go before the person
 * sees its card and lists these places in Ask Daoris's room. They share no code; each side's test holds
 * the same table, and they change together.
 */
export const PLACE_VIEWS: readonly (Exclude<View, 'knowledge'> | KnowledgeMode)[] = [
  'overview', 'sessions', 'quests', 'projects', 'map', 'convergence', 'search', 'agents', 'settings',
];

/**
 * The views the twin still names that became Knowledge's two modes with UX6i (D150 §2.2): a go naming one opens Knowledge
 * in that mode, so the room's places hold while the driver's twin still spells them apart, and a go kept in an earlier
 * conversation lands on the place rather than nowhere; the twins change together when it moves.
 */
const MODES: Readonly<Record<KnowledgeMode, StarterDoor>> = {
  search: { view: 'knowledge', knowledge: 'search' },
  convergence: { view: 'knowledge', knowledge: 'convergence' },
};

/** The domains the twin still names that left Settings: for a workspace's page (UX6g), and for the Plugins place (UX6j). */
type RetiredDomain = 'workspace' | 'permissions' | 'plugins';

export const PLACE_DOMAINS: readonly (SettingsSection | RetiredDomain)[] = [
  'start', 'appearance', 'ai', 'workspace', 'driver', 'permissions', 'plugins', 'browser', 'logs',
];

const RETIRED: readonly RetiredDomain[] = ['workspace', 'permissions', 'plugins'];
const retired = (domain: SettingsSection | RetiredDomain): domain is RetiredDomain =>
  (RETIRED as readonly string[]).includes(domain);

/**
 * The places the twin still names in Settings that retired into a workspace's page with UX6g (D150 §3.1): Settings →
 * Workspace, its wiring, lines, landing and clean-up, and Permissions with its reading across. A go naming one opens the
 * workspace in view's page where its part went, and Permissions alone what agents may do, so the room's places hold
 * while the driver's twin still spells them the old way; the twins change together when it does. Settings → Plugins
 * retired into the Plugins place with UX6j (D150 §2.3, D119 §5), and a go naming it, or one kept in an earlier
 * conversation, opens the place rather than nowhere.
 */
const MOVED: readonly { domain: string; part: string | null; door: StarterDoor }[] = [
  { domain: 'workspace', part: null, door: { view: 'projects', workspaceTab: 'details' } },
  { domain: 'workspace', part: 'wiring', door: { view: 'projects', workspaceSection: 'remote' } },
  { domain: 'workspace', part: 'lines', door: { view: 'projects', workspaceSection: 'defaults' } },
  { domain: 'workspace', part: 'landing', door: { view: 'projects', workspaceSection: 'defaults' } },
  { domain: 'workspace', part: 'sweep', door: { view: 'projects', workspaceTab: 'branches' } },
  { domain: 'permissions', part: null, door: { view: 'agents', agentPart: 'rules' } },
  { domain: 'permissions', part: 'across', door: { view: 'projects', workspaceSection: 'defaults' } },
  { domain: 'plugins', part: null, door: { view: 'plugins' } },
];

/** The parts, each within a view (Repositories, Agents) or a Settings domain. */
export const PLACE_PARTS: readonly { within: string; part: string }[] = [
  // HELPSETUP1: a repository's Setup (UX6f, D150 §4.2), where its own values are set; a go names no repository.
  { within: 'projects', part: 'add' }, { within: 'projects', part: 'import' }, { within: 'projects', part: 'setup' },
  { within: 'start', part: 'agent' }, { within: 'start', part: 'helper' }, { within: 'start', part: 'repositories' },
  { within: 'start', part: 'driven' }, { within: 'start', part: 'landing' }, { within: 'start', part: 'rules' },
  { within: 'workspace', part: 'wiring' }, { within: 'workspace', part: 'lines' }, { within: 'workspace', part: 'landing' },
  { within: 'workspace', part: 'sweep' },
  // UX6e2: the agent's page's sections a door opens (D150 §5.2); Permissions' Proposals are its What it may do.
  { within: 'agents', part: 'accounts' }, { within: 'agents', part: 'rules' }, { within: 'agents', part: 'usage' },
  // HELP10: the card READ1 built, found by its own `settings-across`.
  { within: 'permissions', part: 'across' },
];

/**
 * Where a go takes the person, as a starter's door (HELP6): a domain of Settings at the card or the setup
 * step it names, a view, one of Repositories' drawers or a repository's Setup, or a section of an agent's page —
 * or null for a place this window does not have, which is never guessed at. Pure, so every place is an argument.
 *
 * @remarks
 * A go names no item: Repositories' Setup opens on the repository its list has chosen, an agent's part on the agent
 * that has it (UX6e), and a retired domain's part on the workspace in view's page (UX6g), as the room tells the helper.
 * Search and Convergence open Knowledge in that mode (UX6i), and have no parts.
 */
export function placeDoor(place: HelpPlace): StarterDoor | null {
  const named = PLACE_VIEWS.find((known) => known === place.view);
  if (!named) return null;
  const domain = place.domain ?? null;
  const part = place.part ?? null;
  if (named === 'search' || named === 'convergence') return domain === null && part === null ? MODES[named] : null;
  const view: View = named;

  if (domain !== null) {
    const section = view === 'settings' ? PLACE_DOMAINS.find((known) => known === domain) : undefined;
    if (!section) return null;
    const moved = MOVED.find((each) => each.domain === section && each.part === part);
    if (moved) return moved.door;
    if (retired(section)) return null;
    if (part === null) return { view, section };
    if (!PLACE_PARTS.some((known) => known.within === section && known.part === part)) return null;
    // A setup step is found by its own id in the guide (SETUP1a), a card by its domain's.
    const anchor = (section === 'start' ? `step-${part}` : part) as SettingsAnchor;
    return { view, section, anchor };
  }

  if (part === null) return { view };
  if (!PLACE_PARTS.some((known) => known.within === view && known.part === part)) return null;
  if (view === 'projects') return part === 'setup' ? { view, tab: 'setup' } : { view, drawer: part as 'add' | 'import' };
  if (view === 'agents') return { view, agentPart: part as AgentPart };
  return null;
}
