import i18n from '../i18n';
import { type CommandDoors, type CommandEntry, type CommandState, commandTable, type Translate } from '../commands';

// The world the menu bar's stories and the palette's stand in (UX7a): the install's two workspaces, an agent, the
// domains a shell's Settings has, each record group as its page would offer it. Every press does nothing.

/** A shell on Quests, scoped to every workspace, nothing attended and no quest open. */
export const MENU_STATE: CommandState = {
  attached: true,
  view: 'quests',
  list: { shown: true },
  panelShown: true,
  sideShown: false,
  moved: false,
  workspaces: [{ name: 'work', repositories: 4 }, { name: 'forge', repositories: 2 }],
  scope: null,
  circle: null,
  wired: false,
  theme: 'system',
  language: 'en',
  agents: [{ name: 'claude-code', label: 'Claude Code' }],
  domains: [
    { id: 'start', label: 'settings.domain.start' }, { id: 'appearance', label: 'settings.domain.appearance' },
    { id: 'ai', label: 'settings.domain.ai' }, { id: 'driver', label: 'settings.domain.driver' },
    { id: 'tools', label: 'settings.domain.tools' }, { id: 'plugins', label: 'settings.domain.plugins' },
    { id: 'browser', label: 'settings.domain.browser' }, { id: 'logs', label: 'settings.domain.logs' },
  ],
  session: null,
  quest: null,
  record: false,
  find: true,
  field: false,
};

/** A session attended on Sessions, working: its header offers its stop and its own window; its card takes no answer. */
export const ATTENDED: Partial<CommandState> = {
  view: 'sessions', session: { acts: ['stop', 'detach', 'openFolder', 'terminal', 'copy'], answer: false }, record: true,
};

/** An open quest on Quests: *Take*, *Mark done* and *Decline…*, as its header offers them. */
export const OPEN_QUEST: Partial<CommandState> = { quest: { acts: ['take', 'done', 'decline', 'copy'] }, record: true };

/** A browser: no machine, no session, no window, no terminal (D47 §4). */
export const IN_A_BROWSER: Partial<CommandState> = { attached: false, agents: [], domains: MENU_STATE.domains.slice(0, 3) };

const nothing = new Proxy({}, { get: () => () => {} }) as CommandDoors;

/** The table in a world, in a language's words. */
export function menuWorld(over: Partial<CommandState> = {}, t: Translate = i18n.t.bind(i18n) as Translate): CommandEntry[] {
  return commandTable({ ...MENU_STATE, ...over }, nothing, t);
}
