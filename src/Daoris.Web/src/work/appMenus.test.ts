import { describe, expect, it, vi } from 'vitest';
import '../i18n';
import i18n from '../i18n';
import { type CommandState, commandTable, MENUS } from '../commands';
import { load, measure } from '../../scripts/names-check.mjs';
import { menuRows } from './appMenus';

// The menu bar's rows (UX7a, D152 §2), built from the one table: as data, so a desktop's menus and a browser's are
// asserted without mounting the application over a mocked bridge.

const state = (over: Partial<CommandState> = {}): CommandState => ({
  attached: true, view: 'quests', list: { shown: true }, panelShown: true, sideShown: false, moved: false,
  workspaces: [{ name: 'work', repositories: 4 }, { name: 'forge', repositories: 2 }], scope: null, circle: 'work', wired: true,
  theme: 'dark', language: 'en', agents: [], domains: [], knowledge: 'search', session: null, quest: null, record: false, find: false,
  field: false, refreshing: false, ...over,
});
const doors = new Proxy({}, { get: () => vi.fn() }) as never;
const entries = (over: Partial<CommandState> = {}) => commandTable(state(over), doors, i18n.t.bind(i18n));
const labels = (rows: { label: string }[]) => rows.map((row) => row.label);

describe('the menus, row by row', () => {
  it('holds 65 items with the install\'s two workspaces, every place but Settings under Go (the design §6.2)', () => {
    const all = entries();
    const rows = MENUS.flatMap((menu) => menuRows(all, menu.id));
    expect(rows).toHaveLength(65);
    expect(rows.filter((row) => row.shortcut)).toHaveLength(30);
  });

  it('rules off each group, and names Run\'s two record groups with the tip that says what to open', () => {
    const run = menuRows(entries(), 'run');
    expect(labels(run)).toEqual([
      'Start a session…', 'Answer…', 'Try again', 'Review', 'Stop…', 'Open in its own window',
      'Take', 'Mark done', 'Try again', 'Pause…', 'Resume', 'Decline…', 'Archive what ended…',
    ]);
    expect(run.map((row) => Boolean(row.separated))).toEqual([
      false, true, false, false, false, false, true, false, false, false, false, false, true,
    ]);
    expect(run[1]!.heading).toEqual({ label: 'This session', tip: 'Open a session on Sessions to act on it here' });
    expect(run[6]!.heading).toEqual({ label: 'This quest', tip: 'Open a quest on Quests to act on it here' });
    // Nothing in front: the acts are in their place, and off.
    expect(run.slice(1, 12).every((row) => row.disabled)).toBe(true);
  });

  it('prints each item\'s key at its right, as VS Code\'s menus do', () => {
    const workspace = menuRows(entries(), 'workspace');
    expect(workspace.find((row) => row.id === 'workspace.newAsk')).toMatchObject({ label: 'New ask…', shortcut: 'Ctrl+N' });
    expect(workspace.find((row) => row.id === 'workspace.settings')).toMatchObject({ label: 'Settings', shortcut: 'Ctrl+,' });
    expect(workspace.find((row) => row.id === 'workspace.scope:work')).toMatchObject({ badge: 4 });
    expect(menuRows(entries(), 'view').find((row) => row.id === 'view.commands')).toMatchObject({ shortcut: 'Ctrl+K' });
  });

  it('opens Theme and Language to their side, each a radio group ticked as it stands', () => {
    const view = menuRows(entries(), 'view');
    const theme = view.find((row) => row.label === 'Theme');
    expect(theme?.sub?.map((row) => [row.label, Boolean(row.checked)])).toEqual([['System', false], ['Light', false], ['Dark', true]]);
    const language = view.find((row) => row.label === 'Language');
    expect(language?.sub?.map((row) => [row.label, Boolean(row.checked)])).toEqual([['English', true], ['中文', false]]);
    expect(labels(view)).toEqual([
      'Commands', 'Quest list', 'Panel', 'Side bar', 'Reset view locations', 'Timeline', 'Console', 'Monitor window',
      "Daoris's browser", 'Theme', 'Language', 'Refresh the index',
    ]);
  });

  it('holds only what a browser may know in a browser', () => {
    const browser = entries({ attached: false });
    expect(labels(menuRows(browser, 'workspace'))).toEqual(['New ask…', 'New quest…', 'Every workspace · 2', 'work', 'forge', 'Settings']);
    expect(menuRows(browser, 'terminal')).toEqual([]);
    expect(labels(menuRows(browser, 'help'))).toEqual(['Setup', 'Keyboard shortcuts', 'About Daoris']);
    // A key a browser keeps is not printed where it is not answered.
    expect(menuRows(browser, 'go').find((row) => row.label === 'Quests')?.shortcut).toBeUndefined();
  });

  it('names the menus in 中文 as Windows does, each in the nav budget, and every item in the menu budget', async () => {
    const { glossary, en, zh } = load();
    const nav = glossary.kinds.nav.budget!;
    for (const menu of MENUS) {
      expect(measure(en[menu.label], 'en', glossary.measure), menu.label).toBeLessThanOrEqual(nav.en);
      expect(measure(zh[menu.label], 'zh', glossary.measure), menu.label).toBeLessThanOrEqual(nav.zh);
    }
    for (const language of ['en', 'zh'] as const) {
      await i18n.changeLanguage(language);
      const all = commandTable(state(), doors, i18n.t.bind(i18n));
      for (const row of MENUS.flatMap((menu) => menuRows(all, menu.id)).flatMap((row) => [row, ...(row.sub ?? [])])) {
        expect(measure(row.label, language, glossary.measure), row.label).toBeLessThanOrEqual(language === 'en' ? 24 : 10);
      }
    }
    await i18n.changeLanguage('en');
    expect(zh['menu.workspace']).toBe('工作区');
  });
});
