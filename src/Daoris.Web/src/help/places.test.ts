import { describe, expect, it } from 'vitest';
import { VIEWS } from '../commands';
import { SETTINGS_SECTIONS } from '../SettingsView';
import { PLACE_DOMAINS, PLACE_PARTS, PLACE_VIEWS, placeDoor } from './places';

// HELP6: the places Ask Daoris's go may name. A twin (`.claude/knowledge/twins.md`) of the driver's
// `HelpPlaces`, which judges a go before the person sees it: the tables below are the driver test's
// `The_places_are_the_pages_twin`, line for line.

describe('the places a go may name', () => {
  it('are the driver\'s table, line for line', () => {
    expect(PLACE_VIEWS).toEqual(['overview', 'sessions', 'quests', 'projects', 'map', 'convergence', 'search', 'agents', 'settings']);
    expect(PLACE_DOMAINS).toEqual(['start', 'appearance', 'ai', 'workspace', 'driver', 'permissions', 'plugins', 'browser', 'logs']);
    expect(PLACE_PARTS.map(({ within, part }) => `${within}/${part}`)).toEqual([
      'projects/add', 'projects/import', 'projects/setup',
      'start/agent', 'start/helper', 'start/repositories', 'start/driven', 'start/landing', 'start/rules',
      'workspace/wiring', 'workspace/lines', 'workspace/landing', 'workspace/sweep',
      'agents/accounts', 'agents/rules', 'agents/usage',
      'permissions/across',
    ]);
  });

  /**
   * Plugins is a view since PLUGUI1b, and a go still names Settings → `plugins` until PLUGUI1c moves it to the
   * views in this table and the driver's `HelpPlaces` together, since the twins change together (D119 §5).
   * Settings → Tools (TOOLS7) is a place a go names once TOOLS8 adds it here and to `HelpPlaces` together (D121 §4.3).
   * Workspace and Permissions left Settings with UX6g, and a go still names them until the twins move them together: the
   * door opens their new homes.
   */
  it('are every view the activity bar has and every domain Settings shows, and nothing else', () => {
    expect([...PLACE_VIEWS].sort()).toEqual(VIEWS.map(({ view }) => view).filter((view) => view !== 'plugins').sort());
    const moved = new Set(['workspace', 'permissions']);
    expect([...PLACE_DOMAINS].filter((domain) => !moved.has(domain)).sort())
      .toEqual([...SETTINGS_SECTIONS].filter((domain) => domain !== 'tools').sort());
  });

  it('open where the starters\' doors open: a domain at its card or step, a view, a drawer, a tab, a section', () => {
    expect(placeDoor({ view: 'quests' })).toEqual({ view: 'quests' });
    // UX6e2: Agents is a place the twins name, its parts the agent's page's; with no agent named, the one that has it opens.
    expect(placeDoor({ view: 'agents' })).toEqual({ view: 'agents' });
    expect(placeDoor({ view: 'agents', part: 'accounts' })).toEqual({ view: 'agents', agentPart: 'accounts' });
    expect(placeDoor({ view: 'agents', part: 'rules' })).toEqual({ view: 'agents', agentPart: 'rules' });
    expect(placeDoor({ view: 'agents', part: 'usage' })).toEqual({ view: 'agents', agentPart: 'usage' });
    // HELPSETUP1: a repository's own values are on its Setup, on the repository Repositories has chosen.
    expect(placeDoor({ view: 'projects', part: 'setup' })).toEqual({ view: 'projects', tab: 'setup' });
    expect(placeDoor({ view: 'settings', domain: 'start', part: 'helper' })).toEqual({ view: 'settings', section: 'start', anchor: 'step-helper' });
    expect(placeDoor({ view: 'projects', part: 'import' })).toEqual({ view: 'projects', drawer: 'import' });
  });

  /**
   * UX6g (D150 §3.1): Settings → Workspace and Permissions retired into a workspace's page, and a go naming one opens the
   * workspace in view's page where its part went: its remote, its defaults (a line, a landing rule, reading across), its
   * Branches; Permissions alone opens what agents may do, on the agent that takes the rules.
   */
  it("open a retired domain's part where it went: the workspace's page, or what agents may do", () => {
    expect(placeDoor({ view: 'settings', domain: 'workspace' })).toEqual({ view: 'projects', workspaceTab: 'details' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'wiring' })).toEqual({ view: 'projects', workspaceSection: 'remote' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'lines' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'landing' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'sweep' })).toEqual({ view: 'projects', workspaceTab: 'branches' });
    expect(placeDoor({ view: 'settings', domain: 'permissions' })).toEqual({ view: 'agents', agentPart: 'rules' });
    expect(placeDoor({ view: 'settings', domain: 'permissions', part: 'across' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
  });

  it('refuse a place the window does not have, rather than guess at one', () => {
    expect(placeDoor({ view: 'dashboard' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'billing' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'colours' })).toBeNull();
    expect(placeDoor({ view: 'quests', part: 'drawer' })).toBeNull();
    // UX6e2: Settings → Agents and Permissions' Proposals left Settings with UX6e; the twins no longer name them.
    expect(placeDoor({ view: 'settings', domain: 'agents' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'permissions', part: 'proposals' })).toBeNull();
    expect(placeDoor({ view: 'agents', part: 'workspaces' })).toBeNull();
  });
});
