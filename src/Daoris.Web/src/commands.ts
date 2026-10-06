import type { KnowledgeMode } from './knowledge/modes';
import type { ThemeChoice } from './theme';
import type { IconName } from './ui';

// The one table of verbs (UX7a, D152 §2; SURF9 before it). The menus, the palette, the key handler and the *Keyboard
// shortcuts* drawer read it, so the four never disagree: before it the menus were two lists, the palette a third and the
// keys three more, which agreed by care. Pure, so "what can I do right now" is a value a test asserts rather than a screen
// somebody has to arrange.

/** The application's views (D66): one list, the activity bar's. */
export type View =
  | 'overview' | 'sessions' | 'quests' | 'projects' | 'map' | 'knowledge' | 'agents' | 'plugins' | 'settings';

/**
 * Whose list a view shows (UX6i, D150 §2.2): Knowledge's is its mode's, Search's results or Convergence's findings, each
 * with the chosen item, the filters and the names it had as a place; any other view's is its own.
 */
export const listOf = (view: View, knowledge: KnowledgeMode): Exclude<View, 'knowledge'> | KnowledgeMode =>
  (view === 'knowledge' ? knowledge : view);

/**
 * Every view, in the activity bar's order, with the glyph the bar, the Go menu and the palette show. The bar is it
 * without Settings, which has its own gear.
 */
export const VIEWS: readonly { view: View; icon: IconName; keywords?: string; shellOnly?: boolean }[] = [
  { view: 'overview', icon: 'overview' },
  // A stream never leaves the machine that produced it (D47 §4), so a browser is shown no Sessions.
  { view: 'sessions', icon: 'frameWork', keywords: 'work watch console conversation', shellOnly: true },
  { view: 'quests', icon: 'quests' },
  // Named Repositories since NAME1b; the view's old name still finds it.
  { view: 'projects', icon: 'projects', keywords: 'projects 项目' },
  // The workspace map (MAP2, D67 §3): how the repositories are wired, read at a glance.
  { view: 'map', icon: 'map', keywords: 'map topology graph wiring repositories' },
  // Search and Convergence as one place since UX6i (D150 §2.2): two views of one index, neither with an act. Each old
  // name still finds it.
  { view: 'knowledge', icon: 'knowledge', keywords: 'knowledge search convergence entries findings 知识 搜索 同归' },
  // A place of its own since UX6e (D150 §5), before Plugins: an agent is a product with accounts, and its accounts are this
  // machine's (D47 §4), so a browser is shown no Agents, as it is shown no Sessions.
  { view: 'agents', icon: 'account', keywords: 'agents agent account accounts sign in claude codex harness 智能体 账户', shellOnly: true },
  // A view of its own since PLUGUI1b (D119 §3), after Search: a plugin is this machine's (D64; D47 §4), so a browser
  // is shown no Plugins, as it is shown no Sessions.
  { view: 'plugins', icon: 'plug', keywords: 'plugins plugin extension kit hook server 插件', shellOnly: true },
  // Everywhere since D66: a browser has appearance to set, if nothing of a machine.
  { view: 'settings', icon: 'settings', keywords: 'settings theme dark light appearance language machine remote harness account' },
];

/** The menu bar's menus (D152 §1). */
export type MenuId = 'workspace' | 'edit' | 'view' | 'go' | 'run' | 'terminal' | 'help';

/**
 * The menus in the bar's order, each with its name's key and the letter Alt opens it by (the design §3.3): VS Code's bar
 * on Windows less *Selection*, since Daoris has no editor (D55), with *Workspace* in File's place and job.
 */
export const MENUS: readonly { id: MenuId; label: string; letter: string; shell?: true }[] = [
  { id: 'workspace', label: 'menu.workspace', letter: 'W' },
  { id: 'edit', label: 'menu.edit', letter: 'E' },
  { id: 'view', label: 'menu.view', letter: 'V' },
  { id: 'go', label: 'menu.go', letter: 'G' },
  { id: 'run', label: 'menu.run', letter: 'R' },
  // A shell's alone: a terminal is this machine's (D47 §4).
  { id: 'terminal', label: 'menu.terminal', letter: 'T', shell: true },
  { id: 'help', label: 'menu.help', letter: 'H' },
];

/** A field's own acts, which Edit shows with their keys and the frame never takes (D152 §3.4). */
export type EditAct = 'undo' | 'redo' | 'cut' | 'copy' | 'paste' | 'selectAll';

/**
 * Who takes a key while the focus is in a field or the terminal (the design §3.4): `everywhere` is the frame's in both,
 * as Ctrl+B and Ctrl+J were; `fields` the frame's in a field and the shell's in the terminal (Ctrl+N, a shell's next
 * line); `outside` the field's and the shell's in them (Ctrl+K, Ctrl+F); `native` a field's own, never the frame's.
 */
export type KeyReach = 'everywhere' | 'fields' | 'outside' | 'native';

/** One key an item answers by, as its menu prints it, and who takes it where the focus is. */
export type KeyBinding = { combo: string; reach: KeyReach };

/** What the frame does with the session attended on Sessions, which only the frame can (D118 §5). */
export type FrameIntent = 'start' | 'review' | 'answer' | 'timeline' | 'console' | 'terminal' | 'newTerminal' | 'archiveEnded';

/** What is true now, which decides what applies. Every field a value, so each world is an argument. */
export type CommandState = {
  /** Whether a shell is here. False is a browser: no Sessions, no machine, no window, no terminal. */
  attached: boolean;
  /** The view in front of the person. */
  view: View;
  /** The view's list, where it has one (D118 §3a), and whether it is shown. */
  list: { shown: boolean } | null;
  panelShown: boolean;
  sideShown: boolean;
  /** Whether any view stands where it did not start (DOCK1b). */
  moved: boolean;
  /** Every workspace, with how many repositories each holds (D75). */
  workspaces: readonly { name: string; repositories: number }[];
  /** The workspace the window is scoped to, or null for every one. */
  scope: string | null;
  /** The one workspace in view: the scope, or the only one there is. */
  circle: string | null;
  /** Whether the workspace in view is wired to a remote. */
  wired: boolean;
  theme: ThemeChoice;
  language: 'en' | 'zh';
  /** Each agent installed here, by its id and its product's name: the palette goes to each one's page. */
  agents: readonly { name: string; label: string }[];
  /** Settings' domains this window offers, by id and their name's key: the palette opens each. */
  domains: readonly { id: string; label: string }[];
  /** The mode Knowledge shows, or opens in where it is not in front (UX6i): Search or Convergence. */
  knowledge: KnowledgeMode;
  /**
   * The session attended on Sessions, while it is in front: the acts its page header offers (D126 §3.2), and whether its
   * card takes an answer. Null anywhere else.
   */
  session: { acts: readonly string[]; answer: boolean } | null;
  /** The quest open on Quests, while it is in front: the acts its page header and body offer (D118 §3b). */
  quest: { acts: readonly string[] } | null;
  /** Whether a record is in the main area, whose id *Copy ID* copies. */
  record: boolean;
  /** Whether a find box is in front: the conversation's, or the view's list's. */
  find: boolean;
  /** Whether a field is there to undo, cut, paste in and select. */
  field: boolean;
};

/** What each verb runs: the application's doors, handed in, so this module reaches no hook and no bridge. */
export type CommandDoors = {
  ask: () => void;
  quest: () => void;
  scope: (workspace: string | null) => void;
  add: () => void;
  import: () => void;
  workspaceSetup: () => void;
  sync: () => void;
  wire: () => void;
  /** Settings, at a domain, or at the one its list remembers. */
  settings: (section?: string) => void;
  edit: (act: EditAct) => void;
  find: () => void;
  searchKnowledge: () => void;
  copyId: () => void;
  palette: () => void;
  toggle: (region: 'list' | 'panel' | 'right') => void;
  reset: () => void;
  frame: (intent: FrameIntent) => void;
  monitor: () => void;
  browser: () => void;
  theme: (choice: ThemeChoice) => void;
  language: (language: 'en' | 'zh') => void;
  refresh: () => void;
  go: (view: View) => void;
  /** Knowledge in one of its modes (UX6i), as a door into Search or Convergence opened that place. */
  knowledge: (mode: KnowledgeMode) => void;
  agent: (name: string) => void;
  /** A part of an agent's page, on the agent that has it (UX6e): its accounts, what it may do, its usage. */
  agentPart: (part: 'accounts' | 'rules' | 'usage') => void;
  region: (previous: boolean) => void;
  /** An act on the attended session, by its owner (`sessionActs.ts`), as its header's button presses it. */
  sessionAct: (act: string) => void;
  /** An act on the open quest, by its page's own rule, as its header's button presses it. */
  questAct: (act: string) => void;
  help: () => void;
  quickAsk: () => void;
  setup: () => void;
  shortcuts: () => void;
  update: () => void;
  about: () => void;
};

/** Translates a catalogue key, with its values: the caller owns the catalogue, and this module holds no i18n. */
export type Translate = (key: string, values?: Record<string, unknown>) => string;

/** A key, by its combination as a menu prints it, and who takes it in a field (default: the frame, everywhere). */
type KeySpec = string | { combo: string; reach?: KeyReach; browserKeeps?: true };

/**
 * One verb, once: its id, its label's key, its menu and group, its keys, the surfaces that have it, when it applies,
 * and what it runs.
 */
export type CommandSpec = {
  /** Stable and never shown: what a test, the log and the menus name it by. */
  id: string;
  menu: MenuId;
  /** Its group in the menu: a rule stands between two groups. */
  group: string;
  /** Its label's catalogue key, or the key by what is true (*Sync now* or *Wire to a remote…*, the view's list). */
  label: string | ((state: CommandState) => string);
  /** Its palette row, where it is not the label: *Go to: Quests*, *Settings: Machine log*, a gloss. */
  title?: (label: string, t: Translate) => string;
  icon?: IconName;
  keys?: readonly KeySpec[];
  /** Who takes its keys in a field and the terminal; `everywhere` unless it says. */
  reach?: KeyReach;
  /** A shell's alone: absent in a browser (D47 §4), never disabled there. */
  shell?: true;
  /** Absent by what is true, as a surface is: the view's list where the view has none (D118 §3a). */
  present?: (state: CommandState) => boolean;
  /** Not in the palette: the field's six, which act on the field the palette takes the focus from, and Commands. */
  palette?: false;
  /** Only in the palette: a door its menu reaches by its place, Knowledge's Convergence under Go › Knowledge (UX6i). */
  paletteOnly?: true;
  /** Extra words that MATCH in the palette but are not shown: *settings* finding Machine, and 中文. */
  keywords?: string;
  /** Whether it can be done now; disabled in its menu and omitted from the palette when not. */
  applies?: (state: CommandState) => boolean;
  /** Ticked in its menu: what is shown, or the one chosen. */
  checked?: (state: CommandState) => boolean;
  /** One choice among several (the place, the scope, the theme): the palette omits the one in force. */
  radio?: true;
  run: (doors: CommandDoors, state: CommandState) => void;
};

/** A family of rows the world names: each workspace, each theme, each agent. Built where the table is read. */
type CommandFamily = {
  family: string;
  menu: MenuId;
  group: string;
  rows: (state: CommandState, t: Translate) => FamilyRow[];
};

type FamilyRow = {
  id: string;
  label: string;
  title: string;
  icon?: IconName;
  badge?: number;
  enabled?: boolean;
  checked?: boolean;
  /** A shell's alone. */
  shell?: true;
  /** Only in the palette: an agent and a Settings domain are reached in the menus by their place. */
  paletteOnly?: true;
  /** Never in the palette: *No workspace yet*, a fact rather than an act. */
  menuOnly?: true;
  /** The sub-row it opens from (View's *Theme*, *Language*): its label's key. */
  submenu?: string;
  keywords?: string;
  run: (doors: CommandDoors) => void;
};

/** A group's name over it in its menu, and the tip that says what to open while its acts are off (the design §3.3). */
export const GROUP_HEADINGS: Readonly<Record<string, { label: string; tip: string }>> = {
  'run.session': { label: 'menu.run.session', tip: 'menu.run.sessionTip' },
  'run.quest': { label: 'menu.run.quest', tip: 'menu.run.questTip' },
};

/**
 * The bar's places in order, each with its key: Settings is Workspace's. Knowledge opens in the mode it was left in; its
 * Search is Edit's *Search knowledge* too, and its Convergence the palette's *Go to: Convergence* (UX6i).
 */
const GO_PLACES: readonly View[] = ['overview', 'sessions', 'quests', 'projects', 'map', 'knowledge', 'agents', 'plugins'];

const sessionAct = (act: string, label: string, icon: IconName, menu: MenuId = 'run', group = 'run.session'): CommandSpec => ({
  id: menu === 'run' ? `run.session.${act}` : `terminal.${act === 'terminal' ? 'here' : 'folder'}`,
  menu,
  group,
  label,
  icon,
  shell: true,
  applies: (state) => state.session?.acts.includes(act) ?? false,
  run: (doors) => doors.sessionAct(act),
});

const questAct = (act: string, label: string, icon: IconName, shell?: true): CommandSpec => ({
  id: `run.quest.${act}`,
  menu: 'run',
  group: 'run.quest',
  label,
  icon,
  ...(shell ? { shell } : {}),
  applies: (state) => state.quest?.acts.includes(act) ?? false,
  run: (doors) => doors.questAct(act),
});

const editAct = (act: EditAct, key: string, group: string, icon: IconName, applies?: (state: CommandState) => boolean): CommandSpec => ({
  id: `edit.${act}`,
  menu: 'edit',
  group,
  label: `menu.edit.${act}`,
  icon,
  keys: [key],
  reach: 'native',
  palette: false,
  ...(applies ? { applies } : {}),
  run: (doors) => doors.edit(act),
});

/**
 * **Every verb, once** (D152 §2), in its menu's order. A record's act calls its record's one owner and applies exactly
 * when the record's header offers it; **no act that ends or removes something has a key** (§3), since each asks once and
 * a key would leave the ask as the only guard.
 */
export const COMMANDS: readonly CommandSpec[] = [
  // Workspace (File's place and job): new things, then the scope (a family), the repositories, the workspace's setup.
  {
    id: 'workspace.newAsk', menu: 'workspace', group: 'new', label: 'menu.workspace.newAsk', icon: 'plus',
    // VS Code's *New Text File*: the new thing. A browser keeps it for a new window; the terminal for a shell's next line.
    keys: [{ combo: 'Ctrl+N', browserKeeps: true }], reach: 'fields',
    keywords: 'ask request ticket intake workspace new quest 需求 请求', run: (doors) => doors.ask(),
  },
  {
    id: 'workspace.newQuest', menu: 'workspace', group: 'new', label: 'menu.workspace.newQuest', icon: 'quests',
    keywords: 'quest publish request repository new 委托', run: (doors) => doors.quest(),
  },
  { id: 'workspace.add', menu: 'workspace', group: 'repositories', label: 'menu.workspace.add', icon: 'plus', shell: true, run: (doors) => doors.add() },
  {
    id: 'workspace.import', menu: 'workspace', group: 'repositories', label: 'menu.workspace.import', icon: 'projects', shell: true,
    run: (doors) => doors.import(),
  },
  {
    id: 'workspace.setup', menu: 'workspace', group: 'setup', label: 'menu.workspace.settings', icon: 'settings', shell: true,
    keywords: 'workspace setup defaults remote rules 配置', run: (doors) => doors.workspaceSetup(),
  },
  {
    // One place, two names by what is true (the design §3.2): the pass where the workspace in view is wired, its wiring
    // where it is not. With every workspace in scope there is none in view to sync.
    id: 'workspace.sync', menu: 'workspace', group: 'setup', label: (state) => (state.wired ? 'work.sync.now' : 'menu.workspace.wire'),
    icon: 'cloud', shell: true, keywords: 'sync remote wire push pull 同步 远端 接线',
    applies: (state) => state.circle !== null, run: (doors, state) => (state.wired ? doors.sync() : doors.wire()),
  },
  {
    id: 'workspace.settings', menu: 'workspace', group: 'settings', label: 'menu.settings', icon: 'settings', keys: ['Ctrl+,'],
    keywords: 'settings preferences theme dark light appearance language machine remote harness account 设置',
    run: (doors) => doors.settings(),
  },

  // Edit: a field's own six, shown with their keys and never taken; then Find, Search knowledge and Copy ID.
  editAct('undo', 'Ctrl+Z', 'undo', 'undo', (state) => state.field),
  editAct('redo', 'Ctrl+Y', 'undo', 'redo', (state) => state.field),
  editAct('cut', 'Ctrl+X', 'clipboard', 'cut', (state) => state.field),
  editAct('copy', 'Ctrl+C', 'clipboard', 'copy'),
  editAct('paste', 'Ctrl+V', 'clipboard', 'paste', (state) => state.field),
  editAct('selectAll', 'Ctrl+A', 'clipboard', 'selectAll', (state) => state.field),
  {
    id: 'edit.find', menu: 'edit', group: 'find', label: 'menu.edit.find', icon: 'search',
    // A field and the terminal keep Ctrl+F; a browser keeps it for its own find.
    keys: [{ combo: 'Ctrl+F', browserKeeps: true }], reach: 'outside', keywords: 'find filter search in 查找 筛选',
    applies: (state) => state.find, run: (doors) => doors.find(),
  },
  {
    id: 'edit.search', menu: 'edit', group: 'find', label: 'menu.edit.search', icon: 'search', keys: ['Ctrl+Shift+F'],
    keywords: 'search knowledge find words meaning 搜索 知识', run: (doors) => doors.searchKnowledge(),
  },
  {
    id: 'edit.copyId', menu: 'edit', group: 'id', label: 'menu.edit.copyId', icon: 'copy', keywords: 'copy id identifier 复制',
    applies: (state) => state.record, run: (doors) => doors.copyId(),
  },

  // View: the palette, the regions, the views, the windows, the look (two families), the index.
  {
    id: 'view.commands', menu: 'view', group: 'commands', label: 'menu.view.commands', icon: 'search',
    // Ctrl+K is the class's, kept as it was: a field's own inside one. Ctrl+Shift+P is VS Code's; Firefox keeps it.
    keys: [{ combo: 'Ctrl+K', reach: 'outside' }, { combo: 'Ctrl+Shift+P', browserKeeps: true }],
    palette: false, run: (doors) => doors.palette(),
  },
  {
    id: 'view.list', menu: 'view', group: 'layout', label: (state) => `layout.menu.list.${listOf(state.view, state.knowledge)}`, icon: 'layoutRail',
    keys: ['Ctrl+B'], present: (state) => state.list !== null, checked: (state) => state.list?.shown ?? false,
    keywords: 'list toggle hide show 列表', run: (doors) => doors.toggle('list'),
  },
  {
    id: 'view.panel', menu: 'view', group: 'layout', label: 'layout.menu.panel', icon: 'layoutPanel', keys: ['Ctrl+J'], shell: true,
    checked: (state) => state.panelShown, keywords: 'panel toggle hide show 面板', run: (doors) => doors.toggle('panel'),
  },
  {
    id: 'view.side', menu: 'view', group: 'layout', label: 'layout.menu.right', icon: 'layoutRight', keys: ['Ctrl+Alt+B'], shell: true,
    checked: (state) => state.sideShown, keywords: 'side bar right toggle hide show 侧栏', run: (doors) => doors.toggle('right'),
  },
  {
    id: 'view.reset', menu: 'view', group: 'layout', label: 'work.views.reset', icon: 'refresh', shell: true,
    applies: (state) => state.moved, run: (doors) => doors.reset(),
  },
  {
    id: 'view.timeline', menu: 'view', group: 'views', label: 'work.review.timelineTab', icon: 'quests', shell: true,
    keywords: 'timeline history chain 时间线', run: (doors) => doors.frame('timeline'),
  },
  {
    id: 'view.console', menu: 'view', group: 'views', label: 'work.views.console', icon: 'frameWork', keys: ['Ctrl+Shift+U'], shell: true,
    keywords: 'console output log 控制台', run: (doors) => doors.frame('console'),
  },
  {
    id: 'view.monitor', menu: 'view', group: 'windows', label: 'work.menu.monitor', icon: 'monitor', shell: true,
    keywords: 'second screen window watch live monitor', run: (doors) => doors.monitor(),
  },
  {
    id: 'view.browser', menu: 'view', group: 'windows', label: 'work.menu.browser', icon: 'browser', shell: true,
    keywords: 'browser web page sign in login ticket chrome jira', run: (doors) => doors.browser(),
  },
  {
    id: 'view.refresh', menu: 'view', group: 'index', label: 'menu.refresh', icon: 'refresh', keywords: 'reindex rescan',
    run: (doors) => doors.refresh(),
  },

  // Go: the bar's places in order, Ctrl+1 to Ctrl+8 (a browser keeps those for its tabs); then the regions in turn.
  ...GO_PLACES.map((place, index): CommandSpec => {
    const view = VIEWS.find((each) => each.view === place)!;
    return {
      id: `go.${place}`, menu: 'go', group: 'places', label: `nav.${place}`, title: (label, t) => t('command.goTo', { place: label }), icon: view.icon,
      keys: [{ combo: `Ctrl+${index + 1}`, browserKeeps: true }], ...(view.shellOnly ? { shell: true as const } : {}),
      ...(view.keywords ? { keywords: view.keywords } : {}),
      radio: true, checked: (state) => state.view === place, run: (doors) => doors.go(place),
    };
  }),
  {
    // Knowledge's Convergence (UX6i, D150 §2.2), the palette's alone: Go's row is the place, and the palette's row is the
    // door Convergence had while it was a place, under the id it had then, so the machine log still names it.
    id: 'go.convergence', menu: 'go', group: 'places', label: 'nav.convergence', title: (label, t) => t('command.goTo', { place: label }),
    icon: 'convergence', paletteOnly: true, keywords: 'convergence converge findings same lesson 同归 知识',
    radio: true, checked: (state) => state.view === 'knowledge' && state.knowledge === 'convergence',
    run: (doors) => doors.knowledge('convergence'),
  },
  {
    id: 'go.nextRegion', menu: 'go', group: 'regions', label: 'menu.go.nextRegion', keys: ['F6'], keywords: 'focus region part',
    run: (doors) => doors.region(false),
  },
  {
    id: 'go.previousRegion', menu: 'go', group: 'regions', label: 'menu.go.previousRegion', keys: ['Shift+F6'], keywords: 'focus region part',
    run: (doors) => doors.region(true),
  },

  // Run: a new session, this session's acts and this quest's, by their owners; then archiving what ended.
  {
    id: 'run.start', menu: 'run', group: 'start', label: 'menu.run.start', icon: 'plus',
    keys: [{ combo: 'Ctrl+Shift+N', browserKeeps: true }], shell: true, keywords: 'new session chat conversation start',
    run: (doors) => doors.frame('start'),
  },
  {
    // Answer… is its card's under the header (D126 §3.1), so it applies where its row's ⋯ offers it.
    id: 'run.session.answer', menu: 'run', group: 'run.session', label: 'work.act.answer', icon: 'answer', shell: true,
    applies: (state) => state.session?.answer ?? false, run: (doors) => doors.frame('answer'),
  },
  sessionAct('retry', 'work.act.retry', 'refresh'),
  sessionAct('review', 'work.head.review', 'diff'),
  sessionAct('stop', 'work.act.stop', 'stop'),
  sessionAct('detach', 'work.monitor.detach', 'external'),
  questAct('take', 'quests.detail.take', 'login'),
  questAct('done', 'quests.detail.done', 'check'),
  questAct('retry', 'quests.detail.retry', 'refresh', true),
  questAct('pause', 'quests.detail.pause', 'pause', true),
  questAct('resume', 'quests.detail.resume', 'resume', true),
  questAct('decline', 'quests.detail.decline', 'x'),
  {
    id: 'run.archiveEnded', menu: 'run', group: 'archive', label: 'work.list.archiveEnded', icon: 'archive', shell: true,
    keywords: 'archive ended sessions tidy 归档', run: (doors) => doors.frame('archiveEnded'),
  },

  // Terminal, a shell's alone (D47 §4): a new one, one at the attended session's folder, its folder, the panel's.
  {
    id: 'terminal.new', menu: 'terminal', group: 'new', label: 'menu.terminal.new', icon: 'terminal', keys: ['Ctrl+Shift+`'], shell: true,
    keywords: 'terminal shell powershell new 终端', run: (doors) => doors.frame('newTerminal'),
  },
  sessionAct('terminal', 'work.act.terminal', 'terminal', 'terminal', 'new'),
  sessionAct('openFolder', 'work.act.openFolder', 'folder', 'terminal', 'new'),
  {
    id: 'terminal.show', menu: 'terminal', group: 'show', label: 'menu.terminal.show', icon: 'terminal', keys: ['Ctrl+`'], shell: true,
    keywords: 'terminal shell show 终端', run: (doors) => doors.frame('terminal'),
  },

  // Help.
  {
    id: 'help.ask', menu: 'help', group: 'ask', label: 'help.title', title: (_, t) => t('command.help'), icon: 'help', keys: ['F1', 'Ctrl+Alt+I'],
    shell: true, keywords: 'help ask daoris setup configure how f1 问道衍 帮助', run: (doors) => doors.help(),
  },
  {
    // By the key's place, since what `key` says under Shift and Alt differs by layout (DOCK1d).
    id: 'help.quickAsk', menu: 'help', group: 'ask', label: 'menu.help.quickAsk', icon: 'help', keys: ['Ctrl+Shift+Alt+L'], shell: true,
    keywords: 'quick ask chat question daoris 快速 提问 问道衍', run: (doors) => doors.quickAsk(),
  },
  {
    id: 'help.setup', menu: 'help', group: 'learn', label: 'menu.setup', icon: 'plan',
    keywords: 'setup get started first start onboarding guide steps 配置 入门 开始使用', run: (doors) => doors.setup(),
  },
  {
    id: 'help.shortcuts', menu: 'help', group: 'learn', label: 'menu.help.shortcuts', icon: 'keyboard',
    keywords: 'keyboard shortcuts keys bindings 键盘 快捷键', run: (doors) => doors.shortcuts(),
  },
  {
    // The machine log is Settings' domain (LOG1c), so the palette names it as one.
    id: 'help.log', menu: 'help', group: 'learn', label: 'settings.domain.logs', title: (label, t) => t('command.settingsAt', { domain: label }), icon: 'read', shell: true,
    keywords: 'log machine log events 日志', run: (doors) => doors.settings('logs'),
  },
  {
    // Settings → Driver at the install's update (UPDATE1b): nothing here checks a release channel.
    id: 'help.update', menu: 'help', group: 'about', label: 'menu.help.update', icon: 'refresh', shell: true,
    keywords: 'update upgrade version install 更新', run: (doors) => doors.update(),
  },
  { id: 'help.about', menu: 'help', group: 'about', label: 'menu.about', icon: 'info', keywords: 'about version 关于', run: (doors) => doors.about() },
];

/** A verb's keys as its menu prints them, from the table: what a toggle's tip says (`LAYOUT_KEYS`). */
export function keysOf(id: string): string[] {
  return (COMMANDS.find((spec) => spec.id === id)?.keys ?? []).map((key) => (typeof key === 'string' ? key : key.combo));
}

/** The rows the world names, each placed after the verb it follows in its menu. */
const FAMILIES: readonly (CommandFamily & { after: string })[] = [
  {
    // The scope's second door, beside the status bar's switcher (WSP5): with several every workspace too, with one it is
    // the scope whatever the scope says, and with none the menu says so.
    family: 'workspace.scope', menu: 'workspace', group: 'scope', after: 'workspace.newQuest',
    rows: (state, t) => {
      if (state.workspaces.length === 0) {
        return [{ id: 'workspace.scope:none', label: t('menu.workspace.none'), title: '', enabled: false, menuOnly: true, run: () => {} }];
      }
      const only = state.workspaces.length === 1;
      const every = state.workspaces.length > 1
        ? [{
          id: 'workspace.scope:*', label: t('scope.every', { count: state.workspaces.length }),
          title: t('command.scope', { workspace: t('scope.every', { count: state.workspaces.length }) }),
          checked: state.scope === null, run: (doors: CommandDoors) => doors.scope(null),
        }]
        : [];
      return [
        ...every,
        ...state.workspaces.map((workspace) => ({
          id: `workspace.scope:${workspace.name}`, label: workspace.name, title: t('command.scope', { workspace: workspace.name }),
          badge: workspace.repositories, checked: only || state.scope === workspace.name, keywords: 'workspace scope 工作区',
          run: (doors: CommandDoors) => doors.scope(workspace.name),
        })),
      ];
    },
  },
  {
    // Each Settings domain, the palette's alone (the design §3.2); the machine log is Help's, so it is not here twice.
    family: 'workspace.domain', menu: 'workspace', group: 'settings', after: 'workspace.settings',
    rows: (state, t) => state.domains.filter((domain) => domain.id !== 'logs').map((domain) => ({
      id: `workspace.domain:${domain.id}`, label: t(domain.label), title: t('command.settingsAt', { domain: t(domain.label) }),
      icon: 'settings', paletteOnly: true, keywords: 'settings 设置', run: (doors: CommandDoors) => doors.settings(domain.id),
    })),
  },
  {
    // View's *Theme ▸*: the viewer's choice (D66), ticked as it stands.
    family: 'view.theme', menu: 'view', group: 'look', after: 'view.browser',
    rows: (state, t) => (['system', 'light', 'dark'] as const).map((choice) => ({
      id: `view.theme:${choice}`, label: t(`settings.theme.${choice}`),
      title: t('command.theme', { theme: t(`settings.theme.${choice}`) }), submenu: 'menu.view.theme',
      checked: state.theme === choice, keywords: 'theme appearance dark light system 主题 深色 浅色',
      run: (doors: CommandDoors) => doors.theme(choice),
    })),
  },
  {
    // View's *Language ▸*. With two languages the palette's row is the switch it always was.
    family: 'view.language', menu: 'view', group: 'look', after: 'view.browser',
    rows: (state, t) => (['en', 'zh'] as const).map((language) => ({
      id: `view.language:${language}`, label: t(`language.${language}`), title: t('command.language'), submenu: 'menu.language',
      checked: state.language === language, keywords: '中文 chinese english language 语言',
      run: (doors: CommandDoors) => doors.language(language),
    })),
  },
  {
    // Each agent's page (UX6e), the palette's alone: Go › Agents is the place.
    family: 'go.agent', menu: 'go', group: 'places', after: 'go.plugins',
    rows: (state, t) => state.agents.map((agent) => ({
      id: `go.agent:${agent.name}`, label: agent.label, title: t('command.goTo', { place: agent.label }), icon: 'account',
      shell: true, paletteOnly: true, keywords: 'agent account 智能体', run: (doors: CommandDoors) => doors.agent(agent.name),
    })),
  },
  {
    // What the Agents menu held besides its agents (the design §3.7), the palette's alone: each is a part of the Agents
    // place or Overview's, one press under Go there, and a menu named after one place is what D152 retired.
    family: 'go.part', menu: 'go', group: 'places', after: 'go.plugins',
    rows: (_, t) => ([
      { id: 'signIn', label: 'command.signIn', icon: 'login', keywords: 'sign in account login add 登录 账户', run: (doors: CommandDoors) => doors.agentPart('accounts') },
      { id: 'rules', label: 'command.agentRules', icon: 'shield', keywords: 'rules permissions allow 规则 权限', run: (doors: CommandDoors) => doors.agentPart('rules') },
      { id: 'proposals', label: 'command.proposals', icon: 'inbox', keywords: 'proposals rules waiting 提议', run: (doors: CommandDoors) => doors.go('overview') },
      { id: 'usage', label: 'command.usage', icon: 'gauge', keywords: 'usage tokens limits 用量', run: (doors: CommandDoors) => doors.agentPart('usage') },
    ] as const).map((row) => ({
      id: `go.part:${row.id}`, label: t(row.label), title: t(row.label), icon: row.icon, shell: true as const,
      paletteOnly: true as const, keywords: row.keywords, run: row.run,
    })),
  },
];

/** A row of the table as this window reads it now: translated, its keys this surface's, applied or not, runnable. */
export type CommandEntry = {
  id: string;
  menu: MenuId;
  group: string;
  /** What its menu says. */
  label: string;
  /** What its palette row says. */
  title: string;
  icon?: IconName;
  /** The keys this surface answers by, as its menu prints them: none a browser keeps (D152 §3.4). */
  keys: readonly KeyBinding[];
  enabled: boolean;
  checked?: boolean;
  badge?: number;
  /** In its menu: false for a row only the palette lists (an agent, a Settings domain). */
  menuItem: boolean;
  /** Listed in the palette now: in its rule, applying, and not the choice already in force. */
  palette: boolean;
  /** The sub-row it opens from: that row's label, translated. */
  submenu?: string;
  /** Its group's name over it, and the tip that says what to open while its acts are off. */
  heading?: { label: string; tip: string };
  keywords?: string;
  run: () => void;
};

const bindings = (spec: CommandSpec, attached: boolean): KeyBinding[] => (spec.keys ?? [])
  .map((key) => (typeof key === 'string' ? { combo: key } : key))
  .filter((key) => attached || !('browserKeeps' in key && key.browserKeeps))
  .map((key) => ({ combo: key.combo, reach: ('reach' in key && key.reach) || spec.reach || 'everywhere' }));

/**
 * The table as this window reads it now (D152 §2): each row translated, absent where its surface is absent, disabled
 * where the state forbids it, the families named by the world, and each `run` bound to the doors handed in.
 */
export function commandTable(state: CommandState, doors: CommandDoors, t: Translate): CommandEntry[] {
  const entries: CommandEntry[] = [];
  const heading = (group: string) => {
    const named = GROUP_HEADINGS[group];
    return named ? { heading: { label: t(named.label), tip: t(named.tip) } } : {};
  };

  const family = (after: string) => {
    for (const each of FAMILIES.filter((one) => one.after === after)) {
      for (const row of each.rows(state, t)) {
        if (row.shell && !state.attached) continue;
        const rowEnabled = row.enabled ?? true;
        entries.push({
          id: row.id,
          menu: each.menu,
          group: each.group,
          label: row.label,
          title: row.title,
          ...(row.icon ? { icon: row.icon } : {}),
          keys: [],
          enabled: rowEnabled,
          ...(row.checked !== undefined ? { checked: row.checked } : {}),
          ...(row.badge !== undefined ? { badge: row.badge } : {}),
          menuItem: !row.paletteOnly,
          palette: !row.menuOnly && rowEnabled && !row.checked,
          ...(row.submenu ? { submenu: t(row.submenu) } : {}),
          ...(row.keywords ? { keywords: row.keywords } : {}),
          run: () => row.run(doors),
        });
      }
    }
  };

  for (const spec of COMMANDS) {
    // A row absent here still holds its families' place: a browser has no Daoris's browser, and has Theme after it.
    if ((spec.shell && !state.attached) || (spec.present && !spec.present(state))) {
      family(spec.id);
      continue;
    }
    const label = t(typeof spec.label === 'function' ? spec.label(state) : spec.label);
    const enabled = spec.applies ? spec.applies(state) : true;
    const checked = spec.checked?.(state);
    entries.push({
      id: spec.id,
      menu: spec.menu,
      group: spec.group,
      label,
      title: spec.title ? spec.title(label, t) : label,
      ...(spec.icon ? { icon: spec.icon } : {}),
      keys: bindings(spec, state.attached),
      enabled,
      ...(checked !== undefined ? { checked } : {}),
      menuItem: !spec.paletteOnly,
      palette: spec.palette !== false && enabled && !(spec.radio && checked),
      ...heading(spec.group),
      ...(spec.keywords ? { keywords: spec.keywords } : {}),
      run: () => spec.run(doors, state),
    });
    family(spec.id);
  }
  return entries;
}

/** A named thing a person can do, as the palette lists it. */
export type Command = {
  /** Stable, structural, and never shown: the id is what a test and the log name. */
  id: string;
  /** What the person reads. Already translated by the caller. */
  title: string;
  /** The menu it is in, so a list of sixty is still scannable. */
  group: string;
  icon: IconName;
  /** Extra words that should MATCH but are not shown — "settings" finding Machine, and 中文. */
  keywords?: string;
  run: () => void;
};

/**
 * What the palette lists now (D152 §2): every row in its rule that applies and is not the choice in force, under its
 * menu's name, in the menus' order.
 *
 * @remarks
 * **A row is absent where its surface is absent, and where it does not apply** — never present-and-disabled, as the menus
 * keep it: a palette is a promise that what it lists can be done, and its order is by match, so a place learned there
 * means nothing. The disclosure boundary (D47 §4) is therefore kept by omission, and Playwright asserts the absence.
 */
export function paletteCommands(entries: readonly CommandEntry[], t: Translate = (key) => key): Command[] {
  const menuName = (menu: MenuId) => t(MENUS.find((each) => each.id === menu)!.label);
  return entries.filter((entry) => entry.palette).map((entry) => ({
    id: entry.id,
    title: entry.title,
    group: menuName(entry.menu),
    icon: entry.icon ?? 'execute',
    ...(entry.keywords ? { keywords: entry.keywords } : {}),
    run: entry.run,
  }));
}

/** Every key the window answers, by menu, in the reader's language: what *Keyboard shortcuts* prints (D152 §3.2). */
export function shortcutGroups(entries: readonly CommandEntry[], t: Translate): { menu: string; rows: { label: string; keys: string[] }[] }[] {
  return MENUS
    .map((menu) => ({
      menu: t(menu.label),
      rows: entries
        .filter((entry) => entry.menu === menu.id && entry.keys.length > 0)
        .map((entry) => ({ label: entry.label, keys: entry.keys.map((key) => key.combo) })),
    }))
    .filter((group) => group.rows.length > 0);
}

/**
 * Which commands match what was typed.
 *
 * @remarks
 * **Subsequence, not substring.** "oq" finds *Open quests* and "cnv" finds *Convergence* — the
 * matching every palette in the reference class does, and the reason people stop reading the list at
 * all. Ranked so a match at a word boundary beats one buried mid-word, because "se" should offer
 * *Search* before *Convergence*.
 *
 * Empty input is not a filter: it is "show me everything", which is what a palette opened by someone
 * who does not yet know what they want is for.
 */
export function matching(all: Command[], typed: string): Command[] {
  const needle = typed.trim().toLowerCase();
  if (!needle) return all;

  return all
    .map((command) => ({ command, score: score(`${command.title} ${command.keywords ?? ''}`, needle) }))
    .filter((hit) => hit.score !== null)
    .sort((a, b) => a.score! - b.score!)
    .map((hit) => hit.command);
}

/**
 * How well a haystack matches, lower being better — or null for no match at all.
 *
 * The score is the index of the last matched character, penalised for every match that did not begin
 * a word. So an exact prefix wins, initials win next, and a scatter across the middle comes last.
 */
function score(haystack: string, needle: string): number | null {
  const text = haystack.toLowerCase();
  let at = 0;
  let penalty = 0;

  for (const character of needle) {
    const found = text.indexOf(character, at);
    if (found === -1) return null;
    // A character that starts a word is what a person meant by typing initials.
    const boundary = found === 0 || /[\s./-]/.test(text[found - 1]);
    if (!boundary) penalty += 1;
    at = found + 1;
  }

  return at + penalty * 2;
}
