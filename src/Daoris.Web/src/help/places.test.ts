import { describe, expect, it } from 'vitest';
import { VIEWS } from '../commands';
import { askItem } from '../opener';
import { SETTINGS_SECTIONS } from '../SettingsView';
import { PLACE_DOMAINS, PLACE_ITEM_VIEWS, PLACE_KEPT, PLACE_PARTS, PLACE_VIEWS, placeDoor, type HelpPlace } from './places';

// HELP6: the places Ask Daoris's go may name. A twin (`.claude/knowledge/twins.md`) of the driver's
// `HelpPlaces`, which judges a go before the person sees it: the tables below are the driver test's
// `The_places_are_the_pages_twin`, line for line.

const spelled = ({ view, domain, part }: HelpPlace) => [view, domain, part].filter((each) => each != null).join('/');

describe('the places a go may name', () => {
  it('are the driver\'s table, line for line', () => {
    expect(PLACE_VIEWS).toEqual(['overview', 'sessions', 'quests', 'projects', 'map', 'knowledge', 'agents', 'plugins', 'settings']);
    expect(PLACE_DOMAINS).toEqual(['start', 'appearance', 'ai', 'driver', 'browser', 'logs']);
    expect(PLACE_PARTS.map(({ within, part }) => `${within}/${part}`)).toEqual([
      'sessions/waiting', 'sessions/review', 'quests/asks', 'quests/held',
      'projects/add', 'projects/import', 'projects/setup',
      'projects/workspace-details', 'projects/workspace-branches', 'projects/workspace-workflow',
      'projects/workspace-setup', 'projects/workspace-defaults', 'projects/workspace-remote',
      'knowledge/search', 'knowledge/convergence',
      'start/agent', 'start/helper', 'start/repositories', 'start/driven', 'start/landing', 'start/rules',
      'agents/accounts', 'agents/rules', 'agents/usage',
    ]);
    expect(PLACE_KEPT.map(({ was, now }) => `${spelled(was)} → ${spelled(now)}`)).toEqual([
      'search → knowledge/search', 'convergence → knowledge/convergence', 'settings/plugins → plugins',
      'settings/workspace → projects/workspace-details', 'settings/workspace/wiring → projects/workspace-remote',
      'settings/workspace/lines → projects/workspace-defaults', 'settings/workspace/landing → projects/workspace-defaults',
      'settings/workspace/sweep → projects/workspace-branches', 'settings/permissions → agents/rules',
      'settings/permissions/across → projects/workspace-defaults',
    ]);
    // ENTRY1f1: the views a go may name an item in, and how an ask's item is told from a quest's (the driver's `AskItem`).
    // ENTRY1f2: Sessions too.
    expect(PLACE_ITEM_VIEWS).toEqual(['sessions', 'quests']);
    expect(askItem('')).toBe('ask:');
  });

  /**
   * UX6i2a (D150 §2): the views are the bar's, Knowledge and Plugins among them, and Settings at its foot. Settings → Tools
   * (TOOLS7) is a place a go names once TOOLS8 adds it here and to `HelpPlaces` together (D121 §4.3). Workspace and
   * Permissions left Settings with UX6g, and since UX6g2b the twins name neither: a go still spelled so is kept.
   */
  it('are every view the activity bar has and every domain Settings shows, and nothing else', () => {
    expect([...PLACE_VIEWS].sort()).toEqual(VIEWS.map(({ view }) => view as string).sort());
    expect([...PLACE_DOMAINS].sort()).toEqual([...SETTINGS_SECTIONS].filter((domain) => domain !== 'tools').sort());
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
    // UX6i2a: Knowledge alone opens as it was left, a part in that mode (UX6i); Plugins is a place with no parts.
    expect(placeDoor({ view: 'knowledge' })).toEqual({ view: 'knowledge' });
    expect(placeDoor({ view: 'knowledge', part: 'convergence' })).toEqual({ view: 'knowledge', knowledge: 'convergence' });
    expect(placeDoor({ view: 'knowledge', part: 'search' })).toEqual({ view: 'knowledge', knowledge: 'search' });
    expect(placeDoor({ view: 'plugins' })).toEqual({ view: 'plugins' });
  });

  /**
   * UX6g2b (D161 §3, D150 §4.3): a workspace's page's tabs and its Setup's two sections are Repositories' parts, prefixed
   * where a repository's page shares their names. A go names no workspace, so each opens the workspace in view's page, as
   * a door naming a tab or a section does; `setup` alone stays a repository's.
   */
  it("open a workspace's page at its tab or its Setup's section", () => {
    expect(placeDoor({ view: 'projects', part: 'workspace-details' })).toEqual({ view: 'projects', workspaceTab: 'details' });
    expect(placeDoor({ view: 'projects', part: 'workspace-branches' })).toEqual({ view: 'projects', workspaceTab: 'branches' });
    expect(placeDoor({ view: 'projects', part: 'workspace-workflow' })).toEqual({ view: 'projects', workspaceTab: 'workflow' });
    expect(placeDoor({ view: 'projects', part: 'workspace-setup' })).toEqual({ view: 'projects', workspaceTab: 'setup' });
    expect(placeDoor({ view: 'projects', part: 'workspace-defaults' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'projects', part: 'workspace-remote' })).toEqual({ view: 'projects', workspaceSection: 'remote' });
    expect(placeDoor({ view: 'projects', part: 'setup' })).toEqual({ view: 'projects', tab: 'setup' });
    expect(placeDoor({ view: 'projects', part: 'defaults' })).toBeNull();
    expect(placeDoor({ view: 'projects', part: 'workspace-colours' })).toBeNull();
  });

  /**
   * ENTRY1b (D161's ENTRY1 note): a go reaches what waits on the person below Sessions and Quests, a group of the view's
   * list it brings into view: Sessions' *Waiting on you* and *To review*, by the driver's reader's names, and Quests' asks
   * and the quests held for the person. It names no session or quest, and Overview, which what waits leads, has no part.
   */
  it('open Sessions or Quests with a group that waits on the person brought into view', () => {
    expect(placeDoor({ view: 'sessions', part: 'waiting' })).toEqual({ view: 'sessions', group: 'you' });
    expect(placeDoor({ view: 'sessions', part: 'review' })).toEqual({ view: 'sessions', group: 'review' });
    expect(placeDoor({ view: 'quests', part: 'asks' })).toEqual({ view: 'quests', group: 'asks' });
    expect(placeDoor({ view: 'quests', part: 'held' })).toEqual({ view: 'quests', group: 'held' });
    expect(placeDoor({ view: 'sessions' })).toEqual({ view: 'sessions' });
    expect(placeDoor({ view: 'sessions', part: 'you' })).toBeNull();
    expect(placeDoor({ view: 'sessions', part: 'working' })).toBeNull();
    expect(placeDoor({ view: 'quests', part: 'open' })).toBeNull();
    expect(placeDoor({ view: 'overview', part: 'waiting' })).toBeNull();
  });

  /**
   * ENTRY1f1 (D161's ENTRY1f note): a go on Quests may name the one quest or ask that waits, as the driver judged it against
   * the machine's records; the door names it as Quests' list does, a quest by its id and an ask as `askItem`'s, and the
   * application's one opener chooses it there. An item is Quests' alone, with no part beside it, as the driver holds.
   */
  it('open the one quest or ask a go names on Quests, as its list names it', () => {
    expect(placeDoor({ view: 'quests', item: 'q1a2b3c4' })).toEqual({ view: 'quests', item: 'q1a2b3c4' });
    expect(placeDoor({ view: 'quests', domain: null, part: null, item: 'ask:a1b2c3d4' })).toEqual({ view: 'quests', item: askItem('a1b2c3d4') });
    expect(placeDoor({ view: 'quests', item: null })).toEqual({ view: 'quests' });
    expect(placeDoor({ view: 'quests', part: 'held', item: 'q1a2b3c4' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'start', item: 'q1a2b3c4' })).toBeNull();
    expect(placeDoor({ view: 'overview', item: 'q1a2b3c4' })).toBeNull();
    expect(placeDoor({ view: 'quests', item: 'ask:' })).toBeNull();
  });

  /**
   * ENTRY1f2 (D161's ENTRY1f note): a go on Sessions may name one session, by the id the driver judged against the machine's
   * own records and handed on as the record spells it; the door names it as Sessions' list does, and the application's one
   * opener chooses it there. An item alone in its view, as on Quests; Quests' prefixes are no session's.
   */
  it('open the one session a go names on Sessions, as its list names it', () => {
    expect(placeDoor({ view: 'sessions', item: 's1a2b3c4' })).toEqual({ view: 'sessions', item: 's1a2b3c4' });
    expect(placeDoor({ view: 'sessions', domain: null, part: null, item: ' s1a2b3c4 ' })).toEqual({ view: 'sessions', item: 's1a2b3c4' });
    expect(placeDoor({ view: 'sessions', part: 'waiting', item: 's1a2b3c4' })).toBeNull();
    expect(placeDoor({ view: 'sessions', item: 'ask:a1b2c3d4' })).toBeNull();
    expect(placeDoor({ view: 'sessions', item: 'session:s1a2b3c4' })).toBeNull();
  });

  /**
   * ENTRY1d2b (D161's ENTRY1d note): a go to Add repository or Import a folder may carry the workspace the driver judged, and
   * the drawer opens with it filled; the folder stays the person's pick. Every other place ignores it, as the driver holds.
   */
  it("open Add or Import with the go's workspace filled, and ignore it anywhere else", () => {
    expect(placeDoor({ view: 'projects', part: 'add', workspace: 'work' })).toEqual({ view: 'projects', drawer: 'add', drawerWorkspace: 'work' });
    expect(placeDoor({ view: 'projects', domain: null, part: 'import', item: null, workspace: ' work ' }))
      .toEqual({ view: 'projects', drawer: 'import', drawerWorkspace: 'work' });
    expect(placeDoor({ view: 'projects', part: 'add', workspace: null })).toEqual({ view: 'projects', drawer: 'add' });
    expect(placeDoor({ view: 'projects', part: 'import', workspace: '  ' })).toEqual({ view: 'projects', drawer: 'import' });
    expect(placeDoor({ view: 'projects', part: 'setup', workspace: 'work' })).toEqual({ view: 'projects', tab: 'setup' });
    expect(placeDoor({ view: 'projects', part: 'workspace-details', workspace: 'work' })).toEqual({ view: 'projects', workspaceTab: 'details' });
    expect(placeDoor({ view: 'projects', workspace: 'work' })).toEqual({ view: 'projects' });
    expect(placeDoor({ view: 'quests', item: 'q1a2b3c4', workspace: 'work' })).toEqual({ view: 'quests', item: 'q1a2b3c4' });
    expect(placeDoor({ view: 'settings', domain: 'start', part: 'repositories', workspace: 'work' }))
      .toEqual({ view: 'settings', section: 'start', anchor: 'step-repositories' });
  });

  /**
   * UX6g (D150 §3.1): Settings → Workspace and Permissions retired into a workspace's page, and a go naming one opens the
   * workspace in view's page where its part went: its remote, its defaults (a line, a landing rule, reading across), its
   * Branches; Permissions alone opens what agents may do, on the agent that takes the rules. UX6g2b: each is a kept row of
   * the twin, the old spelling with its part, and one no row names opens nothing.
   */
  it("open a retired domain's part where it went: the workspace's page, or what agents may do", () => {
    expect(placeDoor({ view: 'settings', domain: 'workspace' })).toEqual({ view: 'projects', workspaceTab: 'details' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'wiring' })).toEqual({ view: 'projects', workspaceSection: 'remote' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'lines' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'landing' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'sweep' })).toEqual({ view: 'projects', workspaceTab: 'branches' });
    expect(placeDoor({ view: 'settings', domain: 'permissions' })).toEqual({ view: 'agents', agentPart: 'rules' });
    expect(placeDoor({ view: 'settings', domain: 'permissions', part: 'across' })).toEqual({ view: 'projects', workspaceSection: 'defaults' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'workspace-defaults' })).toBeNull();
  });

  /**
   * UX6i2a (D150 §2): a go spelled as a place was before it moved, kept in an earlier conversation, opens where the place
   * went rather than nowhere: Settings → Plugins the Plugins place (UX6j), Search and Convergence Knowledge in that mode
   * (UX6i). None had parts, so one spelled with a part opens nothing.
   */
  it('open a place that moved where it went', () => {
    expect(placeDoor({ view: 'settings', domain: 'plugins' })).toEqual({ view: 'plugins' });
    expect(placeDoor({ view: 'settings', domain: 'plugins', part: 'kit' })).toBeNull();
    expect(placeDoor({ view: 'search' })).toEqual({ view: 'knowledge', knowledge: 'search' });
    expect(placeDoor({ view: 'convergence', domain: null, part: null })).toEqual({ view: 'knowledge', knowledge: 'convergence' });
    expect(placeDoor({ view: 'convergence', part: 'findings' })).toBeNull();
    expect(placeDoor({ view: 'search', domain: 'ai' })).toBeNull();
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
    // UX6i2a: Knowledge's parts are its two modes, and Plugins has none.
    expect(placeDoor({ view: 'knowledge', part: 'findings' })).toBeNull();
    expect(placeDoor({ view: 'plugins', part: 'kit' })).toBeNull();
  });
});
