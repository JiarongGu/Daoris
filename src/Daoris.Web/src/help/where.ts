import type { Session } from '../api';
import type { View } from '../commands';
import type { KnowledgeMode } from '../knowledge/modes';
import type { SettingsSection } from '../SettingsView';
import { projectsItem, questsItem } from '../opener';
import { offerItem } from '../plugins/catalog';
import { answeredPark } from '../ui';
import type { ChoiceStanding } from '../work/listPanes';
import type { ViewId } from '../work/placements';

/**
 * What is on the screen, as Ask Daoris is told it (HELP1b, D89): the view, the workspace in scope, the
 * settings domain on Settings, and the session attended on Sessions with what a parked one asks.
 */
export type HelpWhere = {
  view: View;
  /** The workspace in scope, or null for every workspace. */
  workspace: string | null;
  settings?: SettingsSection | null;
  /** Knowledge's mode on Knowledge (UX6i): which of its two lists the person is looking at. */
  knowledge?: KnowledgeMode | null;
  /**
   * `answered` is a park the person answered (ANSWER1f), read by `answeredPark`: the same session goes on with the answer
   * at the driver's next look, so nothing about it waits on the person.
   */
  session?: { id: string; repository: string; state: string; note?: string | null; answered?: boolean } | null;
  /** Where Sessions' views stand and which region is showing (HELP2): the helper cannot see the window. */
  layout?: { right: readonly ViewId[]; panel: readonly ViewId[]; rightShown: boolean; panelShown: boolean } | null;
  /**
   * The item the view has chosen, by the id a go names (FRAME1i-a, D118): a quest's bare id, an ask as `ask:<id>`, a
   * repository's or a workspace's or an agent's name, a plugin's id. `closed` is a choice that is done, still shown.
   */
  item?: { kind: ItemKind; id: string; closed?: boolean } | null;
};

/** The kinds an item may be, one row each; a view that gains items (PLUGUI1h's offers) adds a row, not a shape. */
const ITEM_KINDS = ['quest', 'ask', 'repository', 'workspace', 'agent', 'plugin'] as const;
export type ItemKind = (typeof ITEM_KINDS)[number];

/**
 * The item a view has chosen, as the preface names it: only while the screen shows it — a choice `live` is said, one
 * `closed` is said as closed, and a `gone`, `unread` or missing standing, or no choice, is unsaid. Knowledge and Settings
 * keep what they say today, and a plugin's offer is unsaid (PLUGUI1h).
 */
export function itemOf(view: View, chosen: string | null | undefined, standing: ChoiceStanding | undefined): HelpWhere['item'] {
  if (!chosen || (standing !== 'live' && standing !== 'closed')) return null;
  const closed = standing === 'closed' ? { closed: true } : {};
  switch (view) {
    case 'quests': {
      const named = questsItem(chosen);
      return 'ask' in named ? { kind: 'ask', id: chosen, ...closed } : { kind: 'quest', id: named.quest, ...closed };
    }
    case 'projects': {
      const named = projectsItem(chosen);
      return 'workspace' in named ? { kind: 'workspace', id: named.workspace, ...closed } : { kind: 'repository', id: named.repository, ...closed };
    }
    case 'agents': return { kind: 'agent', id: chosen, ...closed };
    case 'plugins': return chosen.startsWith(offerItem('')) ? null : { kind: 'plugin', id: chosen, ...closed };
    default: return null;
  }
}

const VIEW_NAMES: Record<ViewId, string> = {
  timeline: 'the timeline', review: 'the review', workflow: 'the workflow', ask: 'Ask Daoris', console: 'the console',
  terminal: 'the terminal',
};

/** "the timeline, the review and Ask Daoris", or "nothing". */
function listed(views: readonly ViewId[]): string {
  const names = views.map((view) => VIEW_NAMES[view]);
  if (names.length === 0) return 'nothing';
  return names.length === 1 ? names[0]! : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

// The window's own names (NAME1b), so the agent names a place the person can find.
const VIEWS: Record<View, string> = {
  overview: 'Overview', sessions: 'Sessions', quests: 'Quests', projects: 'Repositories',
  map: 'Map', knowledge: 'Knowledge', agents: 'Agents', plugins: 'Plugins', settings: 'Settings',
};

// Knowledge's two modes, as its list's choice names them (UX6i).
const MODES: Record<KnowledgeMode, string> = { search: 'Search', convergence: 'Convergence' };

// Seven since UX6j (D150 §2.3): the guide is Get started again, and Plugins is a place, named among the views.
const DOMAINS: Record<SettingsSection, string> = {
  start: 'Get started', appearance: 'Appearance', ai: 'AI features', driver: 'Driver', tools: 'Tools', browser: 'Browser',
  logs: 'Machine log',
};

/** The attended session as the preface names it — or null when none is attended, or it is not in the list. */
export function attendedOf(id: string | null, sessions: readonly Session[]): HelpWhere['session'] {
  const session = id ? sessions.find((row) => row.id === id) : undefined;
  return session
    ? { id: session.id, repository: session.repository, state: session.state, note: session.note ?? null, answered: answeredPark(session) }
    : null;
}

/** How much of what a session says the preface carries: enough to name the question, not the analysis. */
const SAYS = 280;

/**
 * Where the person is, as one line the agent is handed ahead of their words.
 *
 * @remarks
 * **Agent-facing, so it is not translated**: the person never reads it as chrome, and one spelling is
 * what the agent learns to read. **Only what the screen already shows** (D47 §4): chrome the page
 * renders, never a machine path. Pure, so every screen is an argument.
 */
export function prefaceOf(where: HelpWhere): string {
  const place = where.view === 'settings' && where.settings
    ? `Settings → ${DOMAINS[where.settings]}`
    : where.view === 'knowledge' && where.knowledge
      ? `the ${VIEWS.knowledge} view, showing ${MODES[where.knowledge]}`
      : `the ${VIEWS[where.view]} view`;
  const parts = [place, where.workspace ? `workspace \`${where.workspace}\`` : 'every workspace'];

  if (where.item) {
    parts.push(`looking at ${where.item.kind} \`${where.item.id}\`${where.item.closed ? ', which is closed' : ''}`);
  }

  const session = where.view === 'sessions' ? where.session : null;
  if (session) {
    // An answered park still reads `awaiting-person` for up to one look (ANSWER1f): named as the list's *answered*.
    const state = session.answered
      ? "answered: the same session goes on with the person's answer at the driver's next look"
      : session.state === 'awaiting-person' ? 'waiting on the person' : session.state;
    parts.push(`attending session \`${session.id}\` in \`${session.repository}\`, which is ${state}`);
  }

  let said = `Where the person is now: ${parts.join(', ')}.`;
  const note = session?.note?.replace(/\s+/g, ' ').trim();
  if (note) said += ` It says: "${note.length > SAYS ? `${note.slice(0, SAYS).trimEnd()}…` : note}".`;

  // Where the views stand (HELP2), on every view since the frame is on every view (DOCK1a). Absent where
  // there is no frame at all — a browser — which is the caller's to say by leaving it out.
  const layout = where.layout;
  if (layout) {
    const region = (views: readonly ViewId[], shown: boolean, open: string, shut: string) =>
      views.length === 0 ? listed(views) : `${listed(views)}, and is ${shown ? open : shut}`;
    said += ` The right side bar holds ${region(layout.right, layout.rightShown, 'open', 'closed')};`
      + ` the panel holds ${region(layout.panel, layout.panelShown, 'showing', 'hidden')}.`;
  }
  return said;
}
