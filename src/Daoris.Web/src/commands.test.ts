import { describe, expect, it, vi } from 'vitest';
import {
  COMMANDS, type CommandDoors, type CommandEntry, type CommandState, commandTable, MENUS, matching, paletteCommands, shortcutGroups,
} from './commands';

// The one table (UX7a, D152 §2): every verb named once, with its menu, its key, the surfaces that have it, when it
// applies and what it runs. "What can I do right now" is a value, so every world is an argument rather than an
// arrangement: a browser, a shell on Overview, a shell on Sessions with a session attended, a quest open on Quests.

const state = (over: Partial<CommandState> = {}): CommandState => ({
  attached: true,
  view: 'overview',
  list: null,
  panelShown: true,
  sideShown: false,
  moved: false,
  workspaces: [{ name: 'work', repositories: 4 }, { name: 'forge', repositories: 2 }],
  scope: null,
  circle: null,
  wired: false,
  theme: 'system',
  language: 'en',
  agents: [],
  domains: [],
  knowledge: 'search',
  session: null,
  quest: null,
  record: false,
  find: false,
  field: false,
  ...over,
});

const doors = (): CommandDoors => ({
  ask: vi.fn(), quest: vi.fn(), scope: vi.fn(), add: vi.fn(), import: vi.fn(), workspaceSetup: vi.fn(), sync: vi.fn(),
  wire: vi.fn(), settings: vi.fn(), edit: vi.fn(), find: vi.fn(), searchKnowledge: vi.fn(), copyId: vi.fn(), palette: vi.fn(),
  toggle: vi.fn(), reset: vi.fn(), frame: vi.fn(), monitor: vi.fn(), browser: vi.fn(), theme: vi.fn(), language: vi.fn(),
  refresh: vi.fn(), go: vi.fn(), knowledge: vi.fn(), agent: vi.fn(), agentPart: vi.fn(), region: vi.fn(), sessionAct: vi.fn(), questAct: vi.fn(), help: vi.fn(),
  quickAsk: vi.fn(), setup: vi.fn(), shortcuts: vi.fn(), update: vi.fn(), about: vi.fn(),
});

/** The catalogue's key as the label, its values after it: every string is still a key a test can read. */
const t = (key: string, values?: Record<string, unknown>) => (values ? `${key}(${Object.values(values).join(',')})` : key);

const table = (over: Partial<CommandState> = {}, on = doors()) => commandTable(state(over), on, t);
const ids = (entries: readonly { id: string }[]) => entries.map((entry) => entry.id);
const inMenu = (entries: CommandEntry[], menu: string) => entries.filter((entry) => entry.menu === menu && entry.menuItem);
const entry = (entries: CommandEntry[], id: string) => entries.find((each) => each.id === id);

describe('the menu bar (D152 §1)', () => {
  it('is VS Code\'s less Selection, with Workspace in File\'s place, each with its letter', () => {
    expect(MENUS.map((menu) => menu.id)).toEqual(['workspace', 'edit', 'view', 'go', 'run', 'terminal', 'help']);
    expect(MENUS.map((menu) => menu.letter).join('')).toBe('WEVGRTH');
  });

  it('gives 30 of its items a key on a shell (the design §6.2)', () => {
    const onQuests = table({ view: 'quests', list: { shown: true } }).filter((each) => each.menuItem);
    expect(onQuests.filter((each) => each.keys.length > 0)).toHaveLength(30);
  });
});

describe('each menu (the design §3.2)', () => {
  it('Workspace: the new things, the scope, the repositories, the workspace\'s setup, Settings', () => {
    expect(ids(inMenu(table(), 'workspace'))).toEqual([
      'workspace.newAsk', 'workspace.newQuest', 'workspace.scope:*', 'workspace.scope:work', 'workspace.scope:forge',
      'workspace.add', 'workspace.import', 'workspace.setup', 'workspace.sync', 'workspace.settings',
    ]);
    expect(entry(table(), 'workspace.newAsk')?.keys.map((key) => key.combo)).toEqual(['Ctrl+N']);
    expect(entry(table(), 'workspace.settings')?.keys.map((key) => key.combo)).toEqual(['Ctrl+,']);
  });

  it('Edit: a field\'s six, Find, Search knowledge and Copy ID', () => {
    expect(ids(inMenu(table(), 'edit'))).toEqual([
      'edit.undo', 'edit.redo', 'edit.cut', 'edit.copy', 'edit.paste', 'edit.selectAll', 'edit.find', 'edit.search', 'edit.copyId',
    ]);
  });

  it('View: Commands, the regions, the views, the windows, Theme and Language, Refresh the index', () => {
    expect(ids(inMenu(table({ view: 'sessions', list: { shown: true } }), 'view'))).toEqual([
      'view.commands', 'view.list', 'view.panel', 'view.side', 'view.reset', 'view.timeline', 'view.console', 'view.monitor',
      'view.browser', 'view.theme:system', 'view.theme:light', 'view.theme:dark', 'view.language:en', 'view.language:zh',
      'view.refresh',
    ]);
    // Theme and Language each open to their side, ticked as they stand.
    const theme = table({ theme: 'dark' }).filter((each) => each.submenu === 'menu.view.theme');
    expect(theme.map((each) => [each.id, each.checked])).toEqual([
      ['view.theme:system', false], ['view.theme:light', false], ['view.theme:dark', true],
    ]);
  });

  it('Go: every place but Settings, Ctrl+1 to Ctrl+8 in the bar\'s order, then the regions', () => {
    const go = inMenu(table(), 'go');
    expect(ids(go)).toEqual([
      'go.overview', 'go.sessions', 'go.quests', 'go.projects', 'go.map', 'go.knowledge', 'go.agents', 'go.plugins',
      'go.nextRegion', 'go.previousRegion',
    ]);
    expect(go.slice(0, 8).map((each) => each.keys[0]?.combo)).toEqual(
      ['Ctrl+1', 'Ctrl+2', 'Ctrl+3', 'Ctrl+4', 'Ctrl+5', 'Ctrl+6', 'Ctrl+7', 'Ctrl+8']);
    // The place in front is ticked, as the Workspace menu ticks the scope.
    expect(entry(table({ view: 'quests' }), 'go.quests')?.checked).toBe(true);
  });

  it('Run: start, this session\'s acts, this quest\'s, archiving what ended', () => {
    expect(ids(inMenu(table(), 'run'))).toEqual([
      'run.start', 'run.session.answer', 'run.session.retry', 'run.session.review', 'run.session.stop', 'run.session.detach',
      'run.quest.take', 'run.quest.done', 'run.quest.retry', 'run.quest.pause', 'run.quest.resume', 'run.quest.decline',
      'run.archiveEnded',
    ]);
    expect(entry(table(), 'run.session.answer')?.heading).toEqual({ label: 'menu.run.session', tip: 'menu.run.sessionTip' });
    expect(entry(table(), 'run.quest.take')?.heading).toEqual({ label: 'menu.run.quest', tip: 'menu.run.questTip' });
  });

  it('Terminal and Help', () => {
    expect(ids(inMenu(table(), 'terminal'))).toEqual(['terminal.new', 'terminal.here', 'terminal.folder', 'terminal.show']);
    expect(ids(inMenu(table(), 'help'))).toEqual([
      'help.ask', 'help.quickAsk', 'help.setup', 'help.shortcuts', 'help.log', 'help.update', 'help.about',
    ]);
  });
});

describe('in a browser (D152 §3.6, D47 §4)', () => {
  const browser = table({ attached: false, view: 'quests', list: { shown: true } });

  it('holds what a browser may know: no Terminal, no session, no window, no account', () => {
    expect(ids(inMenu(browser, 'workspace'))).toEqual([
      'workspace.newAsk', 'workspace.newQuest', 'workspace.scope:*', 'workspace.scope:work', 'workspace.scope:forge',
      'workspace.settings',
    ]);
    expect(ids(inMenu(browser, 'view'))).toEqual([
      'view.commands', 'view.list', 'view.theme:system', 'view.theme:light', 'view.theme:dark', 'view.language:en',
      'view.language:zh', 'view.refresh',
    ]);
    expect(ids(inMenu(browser, 'go'))).toEqual([
      'go.overview', 'go.quests', 'go.projects', 'go.map', 'go.knowledge', 'go.nextRegion', 'go.previousRegion',
    ]);
    expect(ids(inMenu(browser, 'run'))).toEqual(['run.quest.take', 'run.quest.done', 'run.quest.decline']);
    expect(inMenu(browser, 'terminal')).toEqual([]);
    expect(ids(inMenu(browser, 'help'))).toEqual(['help.setup', 'help.shortcuts', 'help.about']);
  });

  /** A browser claims no key it keeps for itself, and shows none it does not answer (D152 §3.4). */
  it('claims no key a browser keeps: Ctrl+N, Ctrl+1–8, Ctrl+F, Ctrl+Shift+P', () => {
    expect(entry(browser, 'workspace.newAsk')?.keys).toEqual([]);
    expect(entry(browser, 'go.quests')?.keys).toEqual([]);
    expect(entry(browser, 'edit.find')?.keys).toEqual([]);
    expect(entry(browser, 'view.commands')?.keys.map((key) => key.combo)).toEqual(['Ctrl+K']);
    // What a browser does not keep it still answers: Settings, the list, the regions in turn.
    expect(entry(browser, 'workspace.settings')?.keys.map((key) => key.combo)).toEqual(['Ctrl+,']);
    expect(entry(browser, 'view.list')?.keys.map((key) => key.combo)).toEqual(['Ctrl+B']);
  });
});

describe('when an item applies (D152 §2, the design §3.3)', () => {
  it('is disabled where its surface exists and the state forbids it, and absent where the surface is absent', () => {
    // No session attended: This session's acts are in their place, and off.
    const shell = table();
    expect(entry(shell, 'run.session.stop')).toMatchObject({ enabled: false, menuItem: true, palette: false });
    // A browser has no session at all.
    expect(entry(table({ attached: false }), 'run.session.stop')).toBeUndefined();
  });

  /** A record's act in the menu is enabled exactly when its header offers it (D152 §2). */
  it('enables a session\'s acts exactly as its header offers them, and runs each through its owner', () => {
    const on = doors();
    const attended = table({ view: 'sessions', session: { acts: ['stop', 'detach', 'copy'], answer: false } }, on);
    expect(inMenu(attended, 'run').filter((each) => each.id.startsWith('run.session.')).map((each) => [each.id, each.enabled]))
      .toEqual([
        ['run.session.answer', false], ['run.session.retry', false], ['run.session.review', false], ['run.session.stop', true],
        ['run.session.detach', true],
      ]);
    entry(attended, 'run.session.stop')!.run();
    expect(on.sessionAct).toHaveBeenCalledWith('stop');
    expect(entry(attended, 'terminal.here')?.enabled).toBe(false);
  });

  it('enables a quest\'s acts exactly as its header offers them', () => {
    const on = doors();
    const open = table({ view: 'quests', quest: { acts: ['take', 'done', 'decline'] } }, on);
    expect(inMenu(open, 'run').filter((each) => each.id.startsWith('run.quest.')).map((each) => [each.id, each.enabled]))
      .toEqual([
        ['run.quest.take', true], ['run.quest.done', true], ['run.quest.retry', false], ['run.quest.pause', false],
        ['run.quest.resume', false], ['run.quest.decline', true],
      ]);
    entry(open, 'run.quest.decline')!.run();
    expect(on.questAct).toHaveBeenCalledWith('decline');
  });

  it('offers Sync now where the workspace in view is wired, and Wire to a remote… where it is not, in one place', () => {
    expect(entry(table({ circle: 'work', wired: true }), 'workspace.sync')).toMatchObject({ label: 'work.sync.now', enabled: true });
    expect(entry(table({ circle: 'work', wired: false }), 'workspace.sync')).toMatchObject({ label: 'menu.workspace.wire', enabled: true });
    expect(entry(table({ circle: null }), 'workspace.sync')?.enabled).toBe(false);
  });

  it('names the view\'s list for the view, ticked while shown, and is absent where the view has none', () => {
    expect(entry(table({ view: 'quests', list: { shown: false } }), 'view.list')).toMatchObject({
      label: 'layout.menu.list.quests', checked: false,
    });
    expect(entry(table({ view: 'map', list: null }), 'view.list')).toBeUndefined();
  });

  /** UX6i: Knowledge's list is its mode's, the result list or the finding list, as each was named when it was a place. */
  it("names Knowledge's list by what it holds in the mode in front", () => {
    expect(entry(table({ view: 'knowledge', knowledge: 'search', list: { shown: true } }), 'view.list')?.label)
      .toBe('layout.menu.list.search');
    expect(entry(table({ view: 'knowledge', knowledge: 'convergence', list: { shown: true } }), 'view.list')?.label)
      .toBe('layout.menu.list.convergence');
  });

  it('says no workspace yet, and offers no choice, with none', () => {
    const none = table({ workspaces: [] });
    expect(inMenu(none, 'workspace').find((each) => each.id.startsWith('workspace.scope'))).toMatchObject({
      id: 'workspace.scope:none', label: 'menu.workspace.none', enabled: false, palette: false,
    });
  });
});

/**
 * UX6i (D150 §2.2): Search and Convergence are one place, Knowledge, and every door either had still reaches its list: Go
 * › Knowledge (Ctrl+6, where Convergence was) opens the place in the mode it was left in, Edit › Search knowledge
 * (Ctrl+Shift+F) its Search, and the palette's *Go to: Convergence* its Convergence, in no menu, since Go's row is the
 * place.
 */
describe("Knowledge's doors", () => {
  it('Go › Knowledge is the place, at Ctrl+6, ticked whichever mode is in front', () => {
    const on = doors();
    const go = entry(table({}, on), 'go.knowledge');
    expect(go).toMatchObject({ label: 'nav.knowledge', title: 'command.goTo(nav.knowledge)', icon: 'knowledge', menuItem: true });
    expect(go?.keys.map((key) => key.combo)).toEqual(['Ctrl+6']);
    go!.run();
    expect(on.go).toHaveBeenCalledWith('knowledge');
    expect(entry(table({ view: 'knowledge', knowledge: 'convergence' }), 'go.knowledge')?.checked).toBe(true);
  });

  it('Edit › Search knowledge opens its Search', () => {
    const on = doors();
    entry(table({}, on), 'edit.search')!.run();
    expect(on.searchKnowledge).toHaveBeenCalledOnce();
  });

  it("the palette's Go to: Convergence opens its Convergence, in no menu, and not while Convergence is in front", () => {
    const on = doors();
    const row = paletteCommands(table({}, on)).find((each) => each.id === 'go.convergence');
    expect(row).toMatchObject({ title: 'command.goTo(nav.convergence)', group: 'menu.go', icon: 'convergence' });
    row!.run();
    expect(on.knowledge).toHaveBeenCalledWith('convergence');
    expect(entry(table(), 'go.convergence')?.menuItem).toBe(false);
    // Knowledge on Search still offers it; Knowledge on Convergence does not, as the place in front is not offered.
    expect(ids(paletteCommands(table({ view: 'knowledge', knowledge: 'search' })))).toContain('go.convergence');
    expect(ids(paletteCommands(table({ view: 'knowledge', knowledge: 'convergence' })))).not.toContain('go.convergence');
    // A browser has Knowledge and both its modes: the index is the service's, which every browser is given.
    expect(ids(paletteCommands(table({ attached: false })))).toEqual(expect.arrayContaining(['go.convergence', 'edit.search']));
  });
});

describe('the palette reads the same table (D152 §2)', () => {
  it('lists every menu item that applies, but the field\'s six and Commands itself', () => {
    const entries = table({ view: 'sessions', list: { shown: true }, session: { acts: ['stop', 'detach', 'terminal', 'openFolder'], answer: true }, find: true, record: true, field: true });
    const listed = new Set(ids(paletteCommands(entries)));
    const missing = entries
      .filter((each) => each.menuItem && each.enabled && !each.checked)
      .filter((each) => !listed.has(each.id))
      .map((each) => each.id);
    expect(missing).toEqual(['edit.undo', 'edit.redo', 'edit.cut', 'edit.copy', 'edit.paste', 'edit.selectAll', 'view.commands']);
  });

  it('omits what is disabled and what is chosen already: the place in front, the scope, the theme', () => {
    const listed = ids(paletteCommands(table({ view: 'quests', scope: 'work', theme: 'dark' })));
    expect(listed).not.toContain('go.quests');
    expect(listed).toContain('go.overview');
    expect(listed).not.toContain('workspace.scope:work');
    expect(listed).toContain('workspace.scope:forge');
    expect(listed).not.toContain('view.theme:dark');
    expect(listed).not.toContain('run.session.stop');
  });

  it('groups each command under its menu\'s name, and names a family\'s rows by it', () => {
    const palette = paletteCommands(table({ agents: [{ name: 'claude-code', label: 'Claude Code' }], domains: [{ id: 'driver', label: 'settings.domain.driver' }] }));
    expect(palette.find((each) => each.id === 'go.quests')).toMatchObject({ title: 'command.goTo(nav.quests)', group: 'menu.go' });
    expect(palette.find((each) => each.id === 'workspace.scope:forge')).toMatchObject({ title: 'command.scope(forge)', group: 'menu.workspace' });
    // Each agent and each Settings domain are the palette's alone: the menus reach them by their place.
    expect(palette.find((each) => each.id === 'go.agent:claude-code')).toMatchObject({ title: 'command.goTo(Claude Code)' });
    expect(palette.find((each) => each.id === 'workspace.domain:driver')).toMatchObject({ title: 'command.settingsAt(settings.domain.driver)' });
    expect(palette.find((each) => each.id === 'help.log')).toMatchObject({ title: 'command.settingsAt(settings.domain.logs)' });
  });

  /** The design §3.7: what the Agents menu held keeps a palette row, each a part of the Agents place or Overview's. */
  it('keeps the Agents menu\'s sections as palette rows on a shell, and in no menu', () => {
    const on = doors();
    const entries = table({}, on);
    const rows = paletteCommands(entries).filter((each) => each.id.startsWith('go.part:'));
    expect(rows.map((each) => [each.id, each.title])).toEqual([
      ['go.part:signIn', 'command.signIn'], ['go.part:rules', 'command.agentRules'], ['go.part:proposals', 'command.proposals'],
      ['go.part:usage', 'command.usage'],
    ]);
    expect(entries.filter((each) => each.id.startsWith('go.part:')).every((each) => !each.menuItem)).toBe(true);
    rows[1]!.run();
    rows[2]!.run();
    expect(on.agentPart).toHaveBeenCalledWith('rules');
    expect(on.go).toHaveBeenCalledWith('overview');
    expect(ids(paletteCommands(table({ attached: false }))).filter((id) => id.startsWith('go.part:'))).toEqual([]);
  });

  it('offers a browser nothing that needs this machine', () => {
    const listed = ids(paletteCommands(table({ attached: false })));
    expect(listed).not.toContain('go.sessions');
    expect(listed).not.toContain('go.plugins');
    expect(listed).not.toContain('run.start');
    expect(listed).not.toContain('view.monitor');
    expect(listed).not.toContain('help.ask');
    expect(listed).toContain('workspace.newAsk');
    expect(listed).toContain('help.setup');
  });

  it('runs what it was handed, and nothing else', () => {
    const on = doors();
    const palette = paletteCommands(table({ view: 'map' }, on));
    palette.find((each) => each.id === 'go.quests')!.run();
    palette.find((each) => each.id === 'workspace.newAsk')!.run();
    palette.find((each) => each.id === 'view.theme:dark')!.run();
    expect(on.go).toHaveBeenCalledWith('quests');
    expect(on.ask).toHaveBeenCalledOnce();
    expect(on.theme).toHaveBeenCalledWith('dark');
  });
});

describe('the keys (D152 §3)', () => {
  const combos = (entries: CommandEntry[]) => entries.flatMap((each) => each.keys.map((key) => key.combo));

  it('lists no key twice', () => {
    const all = combos(table({ view: 'quests', list: { shown: true } }));
    expect(all.filter((combo, index) => all.indexOf(combo) !== index)).toEqual([]);
  });

  /** No act that ends or removes something has a key: each asks once, and a key would leave the ask as the only guard. */
  it('gives no key to what stops, declines or deletes', () => {
    for (const spec of COMMANDS) {
      if (/stop|decline|delete|archive/i.test(spec.id)) expect(spec.keys ?? [], spec.id).toEqual([]);
    }
  });

  it('follows VS Code\'s where the meaning is the same', () => {
    const entries = table({ view: 'quests', list: { shown: true } });
    const keysOf = (id: string) => entry(entries, id)?.keys.map((key) => key.combo);
    expect(keysOf('view.commands')).toEqual(['Ctrl+K', 'Ctrl+Shift+P']);
    expect(keysOf('edit.find')).toEqual(['Ctrl+F']);
    expect(keysOf('edit.search')).toEqual(['Ctrl+Shift+F']);
    expect(keysOf('terminal.show')).toEqual(['Ctrl+`']);
    expect(keysOf('terminal.new')).toEqual(['Ctrl+Shift+`']);
    expect(keysOf('view.console')).toEqual(['Ctrl+Shift+U']);
    expect(keysOf('run.start')).toEqual(['Ctrl+Shift+N']);
    expect(keysOf('help.ask')).toEqual(['F1', 'Ctrl+Alt+I']);
  });

  it('prints every key by menu for the Keyboard shortcuts drawer, in the reader\'s language', () => {
    const groups = shortcutGroups(table({ view: 'quests', list: { shown: true } }), t);
    expect(groups.map((group) => group.menu)).toEqual(['menu.workspace', 'menu.edit', 'menu.view', 'menu.go', 'menu.run', 'menu.terminal', 'menu.help']);
    expect(groups.flatMap((group) => group.rows).length).toBe(30);
    expect(groups[0]!.rows[0]).toEqual({ label: 'menu.workspace.newAsk', keys: ['Ctrl+N'] });
  });
});

describe('matching what was typed', () => {
  const list = paletteCommands(commandTable(state({ view: 'quests' }), doors(), (key, values) => ({
    'command.goTo': `Go to: ${String(values?.place ?? '')}`,
    'nav.overview': 'Overview',
    'nav.convergence': 'Convergence',
    'nav.knowledge': 'Knowledge',
    'nav.sessions': 'Sessions',
    'menu.edit.search': 'Search knowledge',
    'menu.settings': 'Settings',
    'menu.run.start': 'Start a session…',
    'menu.refresh': 'Refresh the index',
    'command.language': 'Switch language',
  })[key] ?? key));

  it('shows everything when nothing is typed — a palette is not a filter until you type', () => {
    expect(matching(list, '')).toHaveLength(list.length);
    expect(matching(list, '   ')).toHaveLength(list.length);
  });

  it('matches a subsequence, not only a substring', () => {
    expect(matching(list, 'cnv').map((c) => c.id)).toContain('go.convergence');
    expect(matching(list, 'knw').map((c) => c.id)).toContain('go.knowledge');
    expect(matching(list, 'sas').map((c) => c.id)).toContain('run.start');
  });

  it('finds a command by a keyword it does not show', () => {
    // "theme" and "machine" are what people will type for Settings (D66); neither is in its title.
    expect(matching(list, 'theme').map((c) => c.id)).toContain('workspace.settings');
    expect(matching(list, 'machine').map((c) => c.id)).toContain('workspace.settings');
    // "get started" finds Setup (SETUP1a), in either language.
    expect(matching(list, 'get started')[0]?.id).toBe('help.setup');
    expect(matching(list, '入门').map((c) => c.id)).toContain('help.setup');
  });

  it('matches 中文 — the console is bilingual and so is the palette', () => {
    expect(matching(list, '中文').map((c) => c.id)).toContain('view.language:zh');
  });

  it('answers nothing for a needle that is in nothing, rather than everything', () => {
    expect(matching(list, 'zzzzq')).toEqual([]);
  });
});
