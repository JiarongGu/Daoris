import { describe, expect, it } from 'vitest';
import { askItem, doorOpening, opening, planOpening, projectsItem, questsItem, workspaceItem } from './opener';

// One opener (D118 §3i): every door into a view says what it opens, and the view's chosen item takes it.
// Pure, so what each door does is an assertion, and `App` only applies it.

describe('the opener', () => {
  it('goes to a view, and opens nothing in it where the door named nothing', () => {
    expect(opening('overview')).toEqual({ view: 'overview' });
    expect(opening('quests')).toEqual({ view: 'quests' });
    expect(opening('sessions', null)).toEqual({ view: 'sessions' });
  });

  it('chooses the session a door names, in Sessions\' list', () => {
    expect(opening('sessions', 's1a2b3c4')).toEqual({ view: 'sessions', chosen: { view: 'sessions', item: 's1a2b3c4' } });
  });

  /** FRAME1d: Quests' main area shows the item its list chose, so the choice is the whole of what a door does. */
  it('chooses the quest a door names, and nothing more: no drawer is asked for', () => {
    expect(opening('quests', 'abc123')).toEqual({ view: 'quests', chosen: { view: 'quests', item: 'abc123' } });
  });

  it('names an ask as an ask, since Quests\' list holds asks and quests', () => {
    expect(askItem('7c1e9a04b2d5')).toBe('ask:7c1e9a04b2d5');
    expect(questsItem('ask:7c1e9a04b2d5')).toEqual({ ask: '7c1e9a04b2d5' });
    expect(questsItem('abc123')).toEqual({ quest: 'abc123' });
    expect(opening('quests', askItem('7c1e9a04b2d5'))).toEqual({
      view: 'quests', chosen: { view: 'quests', item: 'ask:7c1e9a04b2d5' },
    });
  });

  it('opens Settings at the domain a door names, and at the part where it names one', () => {
    expect(opening('settings', 'driver')).toEqual({
      view: 'settings', chosen: { view: 'settings', item: 'driver' }, anchor: null,
    });
    expect(opening('settings', 'start', { anchor: 'step-landing' })).toEqual({
      view: 'settings', chosen: { view: 'settings', item: 'start' }, anchor: 'step-landing',
    });
    // The gear names no domain: Settings opens on the one it had, at its top as it was.
    expect(opening('settings')).toEqual({ view: 'settings' });
  });

  it('chooses the repository a door names, and opens one of Repositories\' forms where it names one', () => {
    expect(opening('projects', 'engine')).toEqual({ view: 'projects', chosen: { view: 'projects', item: 'engine' } });
    expect(opening('projects', null, { drawer: 'add' })).toEqual({ view: 'projects', drawer: 'add' });
    expect(opening('projects', null, { drawer: 'import' })).toEqual({ view: 'projects', drawer: 'import' });
  });

  /** ENTRY1d2b (D161's ENTRY1d note): Ask Daoris's go opens Add or Import with its workspace filled, and only a form takes it. */
  it('opens one of Repositories\' forms with the workspace a door fills it with, and nothing else with it', () => {
    expect(opening('projects', null, { drawer: 'add', drawerWorkspace: 'work' })).toEqual({ view: 'projects', drawer: 'add', drawerWorkspace: 'work' });
    expect(opening('projects', null, { drawer: 'import', drawerWorkspace: 'work' }))
      .toEqual({ view: 'projects', drawer: 'import', drawerWorkspace: 'work' });
    expect(opening('projects', null, { drawerWorkspace: 'work' })).toEqual({ view: 'projects' });
    expect(opening('sessions', null, { drawer: 'add', drawerWorkspace: 'work' })).toEqual({ view: 'sessions' });
  });

  /** NEW1d-b: a door to the composer carries its repository and words, on Sessions alone. */
  it('opens the composer with the repository and words a door carries, on Sessions alone', () => {
    expect(opening('sessions', null, { compose: { repository: 'engine', sentence: 'fix it' } }))
      .toEqual({ view: 'sessions', compose: { repository: 'engine', sentence: 'fix it' } });
    expect(opening('sessions', null, { compose: {} })).toEqual({ view: 'sessions', compose: {} });
    expect(opening('projects', null, { compose: { sentence: 'x' } })).toEqual({ view: 'projects' });
  });

  /** PLUGUI1b (D119 §3.1): a plugin by its id, and one of Daoris's own plugins as an offer, since the two may share an id. */
  it('chooses the plugin or the offer a door names, in Plugins\' list', () => {
    expect(opening('plugins', 'acme.gate')).toEqual({ view: 'plugins', chosen: { view: 'plugins', item: 'acme.gate' } });
    expect(opening('plugins', 'offer:in-app-browser')).toEqual({
      view: 'plugins', chosen: { view: 'plugins', item: 'offer:in-app-browser' },
    });
    expect(opening('plugins')).toEqual({ view: 'plugins' });
  });

  /** FRAME1e: a repository's page opens its code map, the Map's page one level in, which is no chosen item (§4). */
  it('opens the Map on the code map a door names, and only the Map', () => {
    expect(opening('map', null, { code: 'engine' })).toEqual({ view: 'map', code: 'engine' });
    expect(opening('projects', 'engine', { code: 'engine' })).toEqual({ view: 'projects', chosen: { view: 'projects', item: 'engine' } });
  });

  /** UX6f (D150 §4.2): a door into a repository's setup opens its page at Setup; a tab is Repositories' alone. */
  it('opens a repository at the tab a door names, and names no tab for another view', () => {
    expect(opening('projects', 'engine', { tab: 'setup' })).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'engine' }, tab: 'setup',
    });
    expect(opening('projects', null, { tab: 'setup' })).toEqual({ view: 'projects', tab: 'setup' });
    expect(opening('quests', 'abc123', { tab: 'setup' })).toEqual({ view: 'quests', chosen: { view: 'quests', item: 'abc123' } });
    expect(doorOpening({ view: 'projects', item: 'engine', tab: 'setup' })).toEqual(opening('projects', 'engine', { tab: 'setup' }));
  });

  /**
   * FLOWCHART1c: a door into how a landed branch landed (the work's milestone, a run's landing step) opens its repository at
   * Branches with that branch's page; it names the branch with its repository, and nothing on any other view.
   */
  it('opens a landed branch\'s page on its repository\'s Branches, where the door names the repository', () => {
    expect(opening('projects', 'engine', { branch: 'review/q6-asset-cache' })).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'engine' }, tab: 'branches',
      branch: { repository: 'engine', branch: 'review/q6-asset-cache' },
    });
    expect(opening('projects', null, { branch: 'review/q6-asset-cache' })).toEqual({ view: 'projects' });
    expect(opening('quests', 'abc123', { branch: 'review/q6-asset-cache' })).toEqual({ view: 'quests', chosen: { view: 'quests', item: 'abc123' } });
  });

  /** UX6g (D150 §4.1): Repositories' list holds workspaces and repositories, so an item there names which. */
  it('names a workspace as a workspace, since Repositories\' list holds both', () => {
    expect(workspaceItem('aurora')).toBe('workspace:aurora');
    expect(projectsItem('workspace:aurora')).toEqual({ workspace: 'aurora' });
    expect(projectsItem('engine')).toEqual({ repository: 'engine' });
    expect(opening('projects', workspaceItem('aurora'))).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'workspace:aurora' },
    });
  });

  /**
   * UX6g (D150 §4.3, §2.4): a door into a workspace's page names its tab and the Setup section to open; one that names no
   * workspace opens the workspace in view, and with none in view, Repositories with nothing chosen.
   */
  it('opens a workspace at the tab and section a door names, the workspace in view where it names none', () => {
    expect(opening('projects', workspaceItem('aurora'), { workspaceTab: 'branches' })).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'workspace:aurora' }, workspaceTab: 'branches',
    });
    // A section is a part of Setup, so naming one opens Setup.
    expect(opening('projects', null, { workspaceSection: 'remote' }, 'forge')).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'workspace:forge' }, workspaceTab: 'setup', workspaceSection: 'remote',
    });
    expect(opening('projects', null, { workspaceTab: 'setup' }, null)).toEqual({ view: 'projects', workspaceTab: 'setup' });
    // A repository a door names is never traded for the workspace in view.
    expect(opening('projects', 'engine', { tab: 'setup' }, 'forge')).toEqual({
      view: 'projects', chosen: { view: 'projects', item: 'engine' }, tab: 'setup',
    });
    expect(opening('quests', null, { workspaceTab: 'setup' }, 'forge')).toEqual({ view: 'quests' });
    expect(doorOpening({ view: 'projects', workspaceTab: 'setup', workspaceSection: 'defaults' }, 'forge')).toEqual(
      opening('projects', null, { workspaceSection: 'defaults' }, 'forge'));
  });

  /**
   * FRAME1f: an entry by its id in Search's list, and a finding by its entries in Convergence's. Since UX6i (D150 §2.2)
   * both are Knowledge's, and a door names the mode with the item: each mode's list keeps its own chosen item.
   */
  it('chooses the entry or the finding a door names, on Knowledge in its mode', () => {
    expect(opening('knowledge', 'game:.claude/knowledge/world-streaming.md', { knowledge: 'search' })).toEqual({
      view: 'knowledge', knowledge: 'search', chosen: { view: 'search', item: 'game:.claude/knowledge/world-streaming.md' },
    });
    expect(opening('knowledge', 'engine:a.md\ngame:a.md', { knowledge: 'convergence' })).toEqual({
      view: 'knowledge', knowledge: 'convergence', chosen: { view: 'convergence', item: 'engine:a.md\ngame:a.md' },
    });
  });

  /** UX6i: a door into Search or Convergence names the mode; one into the place names none, and it opens as it was left. */
  it('opens Knowledge in the mode a door names, or as it was left where it names none', () => {
    expect(opening('knowledge', null, { knowledge: 'convergence' })).toEqual({ view: 'knowledge', knowledge: 'convergence' });
    expect(opening('knowledge')).toEqual({ view: 'knowledge' });
    // An item with no mode could be either list's, so nothing is chosen rather than a guess.
    expect(opening('knowledge', 'engine:a.md')).toEqual({ view: 'knowledge' });
    // A mode means nothing to another view.
    expect(opening('quests', null, { knowledge: 'search' })).toEqual({ view: 'quests' });
    expect(doorOpening({ view: 'knowledge', knowledge: 'search' })).toEqual(opening('knowledge', null, { knowledge: 'search' }));
  });

  /**
   * ENTRY1b (D161's ENTRY1 note): a door into what waits on the person names a group of Sessions' or Quests' list, which
   * the view brings into view, and no item in it. A group means nothing to another view, Overview's among them.
   */
  it('opens Sessions or Quests with the group a door names, and only there', () => {
    expect(opening('sessions', null, { group: 'you' })).toEqual({ view: 'sessions', group: 'you' });
    expect(opening('sessions', null, { group: 'review' })).toEqual({ view: 'sessions', group: 'review' });
    expect(opening('quests', null, { group: 'asks' })).toEqual({ view: 'quests', group: 'asks' });
    expect(opening('quests', null, { group: 'held' })).toEqual({ view: 'quests', group: 'held' });
    expect(opening('quests', null, { group: 'you' })).toEqual({ view: 'quests' });
    expect(opening('sessions', null, { group: 'held' })).toEqual({ view: 'sessions' });
    expect(opening('overview', null, { group: 'you' })).toEqual({ view: 'overview' });
    expect(doorOpening({ view: 'sessions', group: 'you' })).toEqual(opening('sessions', null, { group: 'you' }));
  });

  it('keeps no chosen item for a view with no list', () => {
    expect(opening('map', 'engine')).toEqual({ view: 'map' });
    expect(opening('overview', 'abc123')).toEqual({ view: 'overview' });
  });

  it('opens what a starter\'s or a setup step\'s door names, its item included', () => {
    expect(doorOpening({ view: 'quests', item: 'abc123' })).toEqual(opening('quests', 'abc123'));
    expect(doorOpening({ view: 'sessions', item: 's1a2b3c4' })).toEqual(opening('sessions', 's1a2b3c4'));
    expect(doorOpening({ view: 'settings', section: 'start', anchor: 'step-helper' })).toEqual(
      opening('settings', 'start', { anchor: 'step-helper' }));
    expect(doorOpening({ view: 'projects', drawer: 'add' })).toEqual({ view: 'projects', drawer: 'add' });
    expect(doorOpening({ view: 'projects', drawer: 'import', drawerWorkspace: 'work' }))
      .toEqual({ view: 'projects', drawer: 'import', drawerWorkspace: 'work' });
    expect(doorOpening({ view: 'sessions' })).toEqual({ view: 'sessions' });
  });
});

// CONFIRMWAIT1c (the confirm-wait design §8.1, §15.3 item 13): a press's card opens the page its plan is shown on, scoped to the
// press's own session, repository, workspace, quest or ask.
describe("a press's plan door", () => {
  const holding = (repository: string) => ({ game: 'studio', tools: 'forge' } as Record<string, string>)[repository] ?? null;

  it("opens a land's session at its review", () => {
    expect(planOpening({ plan: 'land', session: '3f2a91c4' }, 'forge', holding))
      .toEqual({ opening: opening('sessions', '3f2a91c4'), review: true });
  });

  it("opens a set-up's repository at its Details, where its Set up is drawn", () => {
    expect(planOpening({ plan: 'setup', repository: 'game' }, 'forge', holding))
      .toEqual({ opening: opening('projects', 'game', { tab: 'details' }) });
  });

  it("opens a sweep's workspace at its Branches, or the one in view where it names none", () => {
    expect(planOpening({ plan: 'sweep', workspace: 'studio' }, 'forge', holding))
      .toEqual({ opening: opening('projects', workspaceItem('studio'), { workspaceTab: 'branches' }) });
    expect(planOpening({ plan: 'sweep', workspace: null }, 'forge', holding))
      .toEqual({ opening: opening('projects', workspaceItem('forge'), { workspaceTab: 'branches' }) });
  });

  it("opens a sync's workspace at its Branches, a repository's by the workspace holding it", () => {
    expect(planOpening({ plan: 'sync', workspace: 'studio', repository: null }, 'forge', holding))
      .toEqual({ opening: opening('projects', workspaceItem('studio'), { workspaceTab: 'branches' }) });
    expect(planOpening({ plan: 'sync', workspace: null, repository: 'game' }, 'forge', holding))
      .toEqual({ opening: opening('projects', workspaceItem('studio'), { workspaceTab: 'branches' }) });
  });

  it("opens a clear's workspace at its Details, its quest's page or its ask's", () => {
    expect(planOpening({ plan: 'clear', scope: 'workspace', id: 'studio' }, 'forge', holding))
      .toEqual({ opening: opening('projects', workspaceItem('studio'), { workspaceTab: 'details' }) });
    expect(planOpening({ plan: 'clear', scope: 'quest', id: '7eb1d7ba71fe' }, 'forge', holding))
      .toEqual({ opening: opening('quests', '7eb1d7ba71fe') });
    expect(planOpening({ plan: 'clear', scope: 'ask', id: '77ea1a000000' }, 'forge', holding))
      .toEqual({ opening: opening('quests', askItem('77ea1a000000')) });
  });
});
