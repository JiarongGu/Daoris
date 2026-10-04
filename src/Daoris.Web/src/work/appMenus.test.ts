import { describe, expect, it } from 'vitest';
import '../i18n';
import { appMenus, menuAction } from './appMenus';

// D75: the app strip's menus are the setup domains. Built as data, so both a desktop's and a
// browser's menus are asserted without mounting the application over a mocked bridge.

const TWO = [{ name: 'aurora', repositories: 4 }, { name: 'tools', repositories: 2 }];
const ids = (items: { id: string }[]) => items.map((item) => item.id);

describe('the menus by domain', () => {
  it('opens each setup item at its own domain on the desktop', () => {
    const menus = appMenus({ attached: true, workspaces: TWO, scope: null, waiting: 1 });

    // *Set up Daoris* leads (SETUP1a, D97): the steps a machine needs, in order.
    // *Plugins* opens the Plugins view (PLUGUI1b, D119 §5), where it opened Settings → Plugins.
    expect(ids(menus.daoris)).toEqual([
      'settings:start', 'settings:appearance', 'settings:driver', 'view:plugins', 'refresh', 'language', 'about',
    ]);
    expect(menus.daoris.find((item) => item.id === 'view:plugins')).toMatchObject({ label: 'Plugins', icon: 'plug' });
    expect(menus.daoris[0]).toMatchObject({ label: 'Setup' });
    // D150 §2.4 (UX6e): signing in, what agents may do and usage are on an agent's page; no Settings → Agents.
    expect(ids(menus.agents)).toEqual([
      'agents:signIn', 'agents:rules', 'proposals', 'usage', 'settings:ai',
    ]);
    // The proposals waiting on the person are counted where they are answered.
    expect(menus.agents.find((item) => item.id === 'proposals')?.badge).toBe(1);
  });

  /** D150 §2.4: each agent the place lists opens its page, ahead of the acts. */
  it('lists each agent, which opens its page', () => {
    const menus = appMenus({
      attached: true, workspaces: TWO, scope: null, waiting: 0,
      agents: [{ name: 'claude-code', label: 'Claude Code' }, { name: 'codex', label: 'Codex' }],
    });

    expect(ids(menus.agents).slice(0, 3)).toEqual(['agent:claude-code', 'agent:codex', 'agents:signIn']);
    expect(menus.agents[0]).toMatchObject({ label: 'Claude Code', icon: 'account' });
    expect(menuAction('agent:codex')).toEqual({ kind: 'agents', agent: 'codex' });
    // A browser has no agents to list (D47 §4).
    expect(ids(appMenus({ attached: false, workspaces: TWO, scope: null, waiting: 0, agents: [{ name: 'codex', label: 'Codex' }] }).agents))
      .toEqual(['settings:ai']);
  });

  /** D150 §2.4: the workspaces, the two drawers, and *This workspace's setup*, its page at Setup since UX6g. */
  it('lists every workspace with what it holds, marks the scope, and holds the workspace acts', () => {
    const menus = appMenus({ attached: true, workspaces: TWO, scope: 'tools', waiting: 0 });

    expect(ids(menus.workspace)).toEqual(['scope:*', 'scope:aurora', 'scope:tools', 'add', 'import', 'workspace:setup']);
    expect(menus.workspace.find((item) => item.id === 'workspace:setup')).toMatchObject({ label: "This workspace's setup", separated: true });
    expect(menus.workspace.find((item) => item.id === 'scope:tools')?.checked).toBe(true);
    expect(menus.workspace.find((item) => item.id === 'scope:*')?.checked).toBe(false);
    expect(menus.workspace.find((item) => item.id === 'scope:aurora')?.badge).toBe(4);
  });

  it('offers no choice of scope with one workspace, and says there is none with none', () => {
    const one = appMenus({ attached: true, workspaces: [TWO[0]!], scope: null, waiting: 0 });
    expect(ids(one.workspace).filter((id) => id.startsWith('scope:'))).toEqual(['scope:aurora']);
    expect(one.workspace[0]?.checked).toBe(true);

    const none = appMenus({ attached: true, workspaces: [], scope: null, waiting: 0 });
    expect(none.workspace[0]).toMatchObject({ id: 'none', disabled: true, label: 'No workspace yet' });
    expect(ids(none.workspace)).toContain('add');
  });

  it('holds only what a browser may know in a browser', () => {
    const menus = appMenus({ attached: false, workspaces: TWO, scope: null, waiting: 3 });

    // Get started is a browser's too, holding the one step it can know (D47 §4).
    expect(ids(menus.daoris)).toEqual(['settings:start', 'settings:appearance', 'refresh', 'language', 'about']);
    expect(ids(menus.workspace)).toEqual(['scope:*', 'scope:aurora', 'scope:tools']);
    expect(ids(menus.agents)).toEqual(['settings:ai']);
  });

  it('reads an item as the act it names', () => {
    expect(menuAction('settings:driver')).toEqual({ kind: 'settings', section: 'driver' });
    // An item named for a part opens at that part (UX5 U72). Since UX6e (D150 §2.4) the proposals are Overview's rule
    // rows, and signing in, what agents may do and usage are on the page of the agent that has each.
    expect(menuAction('proposals')).toEqual({ kind: 'view', view: 'overview' });
    expect(menuAction('usage')).toEqual({ kind: 'agents', part: 'usage' });
    expect(menuAction('agents:rules')).toEqual({ kind: 'agents', part: 'rules' });
    expect(menuAction('agents:signIn')).toEqual({ kind: 'agents', part: 'accounts' });
    // The workspace in view's page at Setup (UX6g), where its defaults, its remote and its rules are.
    expect(menuAction('workspace:setup')).toEqual({ kind: 'workspace', tab: 'setup' });
    expect(menuAction('scope:*')).toEqual({ kind: 'scope', workspace: null });
    expect(menuAction('scope:aurora')).toEqual({ kind: 'scope', workspace: 'aurora' });
    expect(menuAction('import')).toEqual({ kind: 'import' });
    expect(menuAction('settings:start')).toEqual({ kind: 'settings', section: 'start' });
    // A view the menu opens: Plugins, a view of its own since PLUGUI1b (D119 §5).
    expect(menuAction('view:plugins')).toEqual({ kind: 'view', view: 'plugins' });
  });
});
