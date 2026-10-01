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
    expect(ids(menus.agents)).toEqual([
      'settings:agents', 'settings:permissions', 'proposals', 'usage', 'settings:ai',
    ]);
    // The proposals waiting on the person are counted where they are answered.
    expect(menus.agents.find((item) => item.id === 'proposals')?.badge).toBe(1);
  });

  it('lists every workspace with what it holds, marks the scope, and holds the workspace acts', () => {
    const menus = appMenus({ attached: true, workspaces: TWO, scope: 'tools', waiting: 0 });

    expect(ids(menus.workspace)).toEqual([
      'scope:*', 'scope:aurora', 'scope:tools', 'add', 'import', 'wire', 'settings:workspace',
    ]);
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
    expect(menuAction('settings:permissions')).toEqual({ kind: 'settings', section: 'permissions' });
    // An item named for a part of a domain opens at that part (UX5 U72): *Usage* opened Agents &
    // accounts at its top, a screen above what it named.
    expect(menuAction('proposals')).toEqual({ kind: 'settings', section: 'permissions', anchor: 'proposals' });
    expect(menuAction('usage')).toEqual({ kind: 'settings', section: 'agents', anchor: 'usage' });
    expect(menuAction('wire')).toEqual({ kind: 'settings', section: 'workspace', anchor: 'wiring' });
    expect(menuAction('scope:*')).toEqual({ kind: 'scope', workspace: null });
    expect(menuAction('scope:aurora')).toEqual({ kind: 'scope', workspace: 'aurora' });
    expect(menuAction('import')).toEqual({ kind: 'import' });
    expect(menuAction('settings:start')).toEqual({ kind: 'settings', section: 'start' });
    // A view the menu opens: Plugins, a view of its own since PLUGUI1b (D119 §5).
    expect(menuAction('view:plugins')).toEqual({ kind: 'view', view: 'plugins' });
  });
});
