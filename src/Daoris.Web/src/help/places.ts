import type { View } from '../commands';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { StarterDoor } from './starters';

/** A place on the window a go names (HELP6), as the driver judged it: a view, a Settings domain, a part. */
export type HelpPlace = { view: string; domain?: string | null; part?: string | null };

/**
 * The places Ask Daoris's go may name (HELP6): the views, Settings' domains, and the parts of them a door
 * already opens — the setup guide's steps, a domain's cards, Projects' drawers.
 *
 * @remarks
 * A twin (`.claude/knowledge/twins.md`) of the driver's `HelpPlaces`, which judges a go before the person
 * sees its card and lists these places in Ask Daoris's room. They share no code; each side's test holds
 * the same table, and they change together.
 */
export const PLACE_VIEWS: readonly View[] = ['overview', 'sessions', 'quests', 'projects', 'map', 'convergence', 'search', 'settings'];

export const PLACE_DOMAINS: readonly (SettingsSection | 'agents' | 'workspace' | 'permissions')[] = [
  'start', 'appearance', 'ai', 'workspace', 'driver', 'agents', 'permissions', 'plugins', 'browser', 'logs',
];

/**
 * The places the twin still names in Settings that moved to the Agents place (UX6e, D150 §3.1): Settings → Agents, its
 * Usage, and Permissions' Proposals, which are what an agent may do. A go naming one opens its new home, so the room's
 * places hold while the driver's twin still spells them the old way; the twins change together when it does.
 *
 * And Settings → Workspace and Permissions, which retired into a workspace's page with UX6g (D150 §3.1): a go naming one
 * opens the workspace in view's page where its part went, and Permissions alone opens what agents may do.
 */
const MOVED: readonly { domain: string; part: string | null; door: StarterDoor }[] = [
  { domain: 'agents', part: null, door: { view: 'agents' } },
  { domain: 'agents', part: 'usage', door: { view: 'agents', agentPart: 'usage' } },
  { domain: 'permissions', part: 'proposals', door: { view: 'agents', agentPart: 'rules' } },
  { domain: 'workspace', part: null, door: { view: 'projects', workspaceTab: 'details' } },
  { domain: 'workspace', part: 'wiring', door: { view: 'projects', workspaceSection: 'remote' } },
  { domain: 'workspace', part: 'lines', door: { view: 'projects', workspaceSection: 'defaults' } },
  { domain: 'workspace', part: 'landing', door: { view: 'projects', workspaceSection: 'defaults' } },
  { domain: 'workspace', part: 'sweep', door: { view: 'projects', workspaceTab: 'branches' } },
  { domain: 'permissions', part: null, door: { view: 'agents', agentPart: 'rules' } },
  { domain: 'permissions', part: 'across', door: { view: 'projects', workspaceSection: 'defaults' } },
];

/** The parts, each within a view (Projects) or a Settings domain. */
export const PLACE_PARTS: readonly { within: string; part: string }[] = [
  { within: 'projects', part: 'add' }, { within: 'projects', part: 'import' },
  { within: 'start', part: 'agent' }, { within: 'start', part: 'helper' }, { within: 'start', part: 'repositories' },
  { within: 'start', part: 'driven' }, { within: 'start', part: 'landing' }, { within: 'start', part: 'rules' },
  { within: 'workspace', part: 'wiring' }, { within: 'workspace', part: 'lines' }, { within: 'workspace', part: 'landing' },
  { within: 'workspace', part: 'sweep' },
  { within: 'agents', part: 'usage' }, { within: 'permissions', part: 'proposals' },
  // HELP10: the card READ1 built, found by its own `settings-across`.
  { within: 'permissions', part: 'across' },
];

/**
 * Where a go takes the person, as a starter's door (HELP6): a domain of Settings at the card or the setup
 * step it names, a view, or one of Projects' drawers — or null for a place this window does not have,
 * which is never guessed at. Pure, so every place is an argument.
 */
export function placeDoor(place: HelpPlace): StarterDoor | null {
  const view = PLACE_VIEWS.find((known) => known === place.view);
  if (!view) return null;
  const domain = place.domain ?? null;
  const part = place.part ?? null;

  if (domain !== null) {
    const section = view === 'settings' ? PLACE_DOMAINS.find((known) => known === domain) : undefined;
    if (!section) return null;
    const moved = MOVED.find((each) => each.domain === section && each.part === part);
    if (moved) return moved.door;
    if (section === 'agents' || section === 'workspace' || section === 'permissions') return null;
    if (part === null) return { view, section };
    if (!PLACE_PARTS.some((known) => known.within === section && known.part === part)) return null;
    // A setup step is found by its own id in the guide (SETUP1a), a card by its domain's.
    const anchor = (section === 'start' ? `step-${part}` : part) as SettingsAnchor;
    return { view, section, anchor };
  }

  if (part === null) return { view };
  if (!PLACE_PARTS.some((known) => known.within === view && known.part === part)) return null;
  return view === 'projects' ? { view, drawer: part as 'add' | 'import' } : null;
}
