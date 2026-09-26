import i18n from '../i18n';
import type { SettingsAnchor, SettingsSection } from '../SettingsView';
import type { MenuItem } from './AppMenu';

/** A workspace as the Workspace menu lists it: its name and how many repositories it holds. */
export type MenuWorkspace = { name: string; repositories: number };

/** What choosing a menu item does, read from its id (`menuAction`). */
export type MenuAction =
  /**
   * A domain of Settings, and the part of it an item is named for, which the page brings into view
   * (UX5 U72: *Usage* opened its domain at the top, a screen above the usage).
   */
  | { kind: 'settings'; section: SettingsSection; anchor?: SettingsAnchor }
  | { kind: 'scope'; workspace: string | null }
  | { kind: 'add' }
  | { kind: 'import' }
  | { kind: 'refresh' }
  | { kind: 'language' }
  | { kind: 'about' }
  | { kind: 'none' };

/**
 * The app strip's menus (D75 §1): Daoris, Workspace and Agents are the setup domains, and each
 * setup item opens its own domain of Settings. *View* is the frame's and is not built here.
 *
 * @remarks
 * 🔴 **Before this every setup item opened the same long page at its top**: three names, one place.
 *
 * Built as data, so a desktop's menus and a browser's are asserted without mounting the application
 * over a mocked bridge, and an item's id is the act it names (`menuAction`). A browser's menus hold
 * only what a browser may know (D47 §4): no machine item is offered there, disabled or otherwise.
 *
 * The workspace list is the scope's second door, beside the status bar's switcher (WSP5): with
 * several it offers *every workspace* too, with one it names that one, and with none it says so.
 */
export function appMenus({ attached, workspaces, scope, waiting }: {
  attached: boolean;
  workspaces: readonly MenuWorkspace[];
  /** The workspace the window is scoped to, or null for every one. */
  scope: string | null;
  /** Proposals waiting on the person (PERM2), counted where they are answered. */
  waiting: number;
}): { daoris: MenuItem[]; workspace: MenuItem[]; agents: MenuItem[] } {
  const t = i18n.t.bind(i18n);
  const machine = (items: MenuItem[]) => (attached ? items : []);

  const daoris: MenuItem[] = [
    { id: 'settings:appearance', label: t('menu.settings'), icon: 'settings' },
    ...machine([
      { id: 'settings:driver', label: t('menu.driver'), icon: 'frameWork' },
      { id: 'settings:plugins', label: t('menu.plugins'), icon: 'plug' },
    ]),
    { id: 'refresh', label: t('menu.refresh'), icon: 'refresh', separated: true },
    { id: 'language', label: t('menu.language'), icon: 'languages' },
    { id: 'about', label: t('menu.about'), icon: 'info', separated: true },
  ];

  // With one workspace it IS the scope, whatever the scope says; "every" is a choice only among two.
  const only = workspaces.length === 1;
  const scopes: MenuItem[] = workspaces.length === 0
    ? [{ id: 'none', label: t('menu.workspace.none'), disabled: true }]
    : [
      ...(workspaces.length > 1
        ? [{ id: 'scope:*', label: t('scope.every', { count: workspaces.length }), checked: scope === null }]
        : []),
      ...workspaces.map((workspace) => ({
        id: `scope:${workspace.name}`,
        label: workspace.name,
        badge: workspace.repositories,
        checked: only || scope === workspace.name,
      })),
    ];

  const workspace: MenuItem[] = [
    ...scopes,
    ...machine([
      { id: 'add', label: t('menu.workspace.add'), icon: 'plus', separated: true },
      { id: 'import', label: t('menu.workspace.import'), icon: 'projects' },
      { id: 'wire', label: t('menu.workspace.wire'), icon: 'cloud' },
      { id: 'settings:workspace', label: t('menu.workspace.settings'), icon: 'settings', separated: true },
    ]),
  ];

  const agents: MenuItem[] = [
    ...machine([
      { id: 'settings:agents', label: t('menu.agents.tools'), icon: 'account' },
      { id: 'settings:permissions', label: t('menu.agents.rules'), icon: 'shield' },
      { id: 'proposals', label: t('menu.agents.proposals'), icon: 'inbox', badge: waiting },
      { id: 'usage', label: t('menu.agents.usage'), icon: 'gauge' },
    ]),
    { id: 'settings:ai', label: t('menu.agents.ai'), icon: 'search', separated: attached },
  ];

  return { daoris, workspace, agents };
}

/** What an item's id names — the one reading of the ids `appMenus` writes. */
export function menuAction(id: string): MenuAction {
  if (id.startsWith('settings:')) return { kind: 'settings', section: id.slice('settings:'.length) as SettingsSection };
  if (id.startsWith('scope:')) {
    const name = id.slice('scope:'.length);
    return { kind: 'scope', workspace: name === '*' ? null : name };
  }
  switch (id) {
    // Proposals are answered beside the rules they would change; usage sits under the accounts.
    case 'proposals': return { kind: 'settings', section: 'permissions', anchor: 'proposals' };
    case 'usage': return { kind: 'settings', section: 'agents', anchor: 'usage' };
    case 'wire': return { kind: 'settings', section: 'workspace', anchor: 'wiring' };
    case 'add': return { kind: 'add' };
    case 'import': return { kind: 'import' };
    case 'refresh': return { kind: 'refresh' };
    case 'language': return { kind: 'language' };
    case 'about': return { kind: 'about' };
    default: return { kind: 'none' };
  }
}
