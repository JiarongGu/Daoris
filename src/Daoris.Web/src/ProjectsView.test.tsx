import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Repositories in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER and DAORIS.REGISTRY modules. Browser
// mode needs no twin suite: the absence of these controls there is asserted by their queries never
// firing (an unstubbed fetch throws). Moved from `shell.test.tsx` with MOD3: tests follow their code.
// Since FRAME1e the view is a list pane and a page (D118 §2): the repositories are the list, and what a
// card held is the chosen repository's page, so each test chooses the repository it reads.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { code } from './test/code';
import { DRIVER_STATE, REGISTRY, REPOSITORIES, respond, serviceCalls, show, WIRING } from './test/shellHarness';
import {
  chooseRepository, chooseWorkspace, openSetup, ProjectsView, repositoryList, repositoryMain, repositoryRow,
} from './test/projectsView';

const CHOSEN = 'daoris.list.projects.chosen';
const TAB = 'daoris.list.projects.tab';

afterEach(() => window.localStorage.clear());

describe('the shell-attached platform', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  it('projects grow the per-machine driver controls, landing on DAORIS.DRIVER', async () => {
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Driving');

    await userEvent.click(await within(page).findByLabelText('Drive on this machine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'engine', drivable: true },
    });
  });

  /**
   * Seen on the installed window, 2026-09-24, on a machine whose registrations had all been retired:
   * Projects was its header over a blank page. The fixture always has a repository, so no story or
   * test had ever rendered this, and it is the first thing a new installation shows. Since FRAME1e the
   * list says it, and offers its ＋'s act where the eye already is.
   */
  it('a machine with no repository says so, and offers the one act that changes it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json([]) : respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    expect(await within(repositoryList()).findByText('No repository is registered yet')).toBeInTheDocument();
    expect(within(repositoryList()).getByText(code(/daoris import/))).toBeInTheDocument();
    // The list's ＋ and its empty state's: the second is where the eye already is.
    expect(within(repositoryList()).getAllByRole('button', { name: 'Add repository' })).toHaveLength(2);
  });

  /**
   * D118 §2 (audit PR1): the repositories are a list, the adopted first and then *Registered, not adopted*,
   * each a row a person chooses; the cards that opened nothing but *Manage* are gone, and what a card said
   * is the chosen repository's page.
   */
  it('lists the adopted repositories, then the registered ones not adopted, each a row to choose', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([...REGISTRY, { ...REGISTRY[0], repository: 'lone', adopted: false, summary: undefined }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    // A group per workspace (UX6g, D150 §4.1): a row that names none is in `default`, its head first, then its adopted.
    const head = await within(repositoryList()).findByRole('heading', { level: 3, name: 'default 2 repositories' });
    const outside = within(repositoryList()).getByRole('heading', { name: 'Registered, not adopted (1)' });
    expect(head.compareDocumentPosition(await repositoryRow('engine'))).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect((await repositoryRow('engine')).compareDocumentPosition(outside)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    // A row says the repository and its one line: its summary, content shown as it is.
    expect(within(await repositoryRow('engine')).getByText('the engine')).toBeInTheDocument();
    // With nothing chosen, the main area says how to choose and offers the ＋.
    expect(within(repositoryMain()).getByText('Choose a repository')).toBeInTheDocument();
    expect(within(repositoryMain()).getByRole('button', { name: 'Add repository' })).toBeInTheDocument();
  });

  /** D118 §3f (audit PR9): the chosen repository is the list's memory, so a relaunch reopens it. */
  it('remembers the repository chosen, and reopens it', async () => {
    show(<ProjectsView notify={() => {}} />);
    const row = await repositoryRow('engine');
    await chooseRepository('engine');

    expect(window.localStorage.getItem(CHOSEN)).toBe('engine');
    expect(within(row).getAllByRole('button')[0]).toHaveAttribute('aria-current', 'true');

    cleanup();
    show(<ProjectsView notify={() => {}} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
  });

  /** §3b: a repository the list chose and the registry no longer holds says so, rather than a blank page. */
  it('says a repository chosen now has gone, once the registry has answered without it', async () => {
    show(<ProjectsView notify={() => {}} door="retired-one" />);

    expect(await within(repositoryMain()).findByText('This repository is no longer here')).toBeInTheDocument();
  });

  // UX6b (design §1 rule 6): a remembered repository the registry no longer holds opens nothing chosen.
  it('opens with nothing chosen on a remembered repository that has gone, and forgets it', async () => {
    window.localStorage.setItem(CHOSEN, 'retired-one');
    show(<ProjectsView notify={() => {}} />);

    await waitFor(() => expect(repositoryMain()).toHaveTextContent('Choose a repository'));
    expect(repositoryMain()).not.toHaveTextContent('This repository is no longer here');
    expect(window.localStorage.getItem(CHOSEN)).toBeNull();
  });

  /** The page's header holds its acts (D118 §3b): *Manage* on a shell, and the door to its code map. */
  it("heads a repository's page with Manage and a door to its code map", async () => {
    const onOpenCode = vi.fn();
    show(<ProjectsView notify={() => {}} onOpenCode={onOpenCode} />);
    const page = await chooseRepository('engine');
    const head = within(page).getByRole('heading', { level: 1, name: 'engine' }).closest('header')!;

    expect(within(head).getByRole('button', { name: 'Manage' })).toBeInTheDocument();
    await userEvent.click(within(head).getByRole('button', { name: 'Open code map' }));
    expect(onOpenCode).toHaveBeenCalledWith('engine');
  });

  it('hold appears only once a repository is drivable — a hold on nothing is noise', async () => {
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Driving');

    await within(page).findByLabelText('Drive on this machine');
    expect(within(page).queryByLabelText('Hold')).not.toBeInTheDocument();
  });

  it('holding a drivable repository lands on DAORIS.DRIVER with its own payload key', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'] }));
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Driving');

    await userEvent.click(await within(page).findByLabelText('Hold'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', {
      payload: { repository: 'engine', held: true },
    });
  });

  /**
   * KNOWUSE1b (D135 §3): a repository's standing answer on its page, beside the driver's other choices for it, in the
   * person's words as this machine keeps them; edited and cleared over the same file the terminal edits, on DAORIS.DRIVER.
   */
  it("shows a repository's standing answer on its page, and edits and clears it on DAORIS.DRIVER", async () => {
    invoke.mockImplementation(async () => ({
      ...DRIVER_STATE, standing: [{ repository: 'Engine', says: 'dev writes allowed; prod only on a yes', at: '2026-10-03T09:00:00Z' }],
    }));
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Sessions');

    expect(await within(page).findByText('dev writes allowed; prod only on a yes')).toBeInTheDocument();
    await userEvent.click(within(page).getByRole('button', { name: 'Edit' }));
    const field = within(page).getByRole('textbox', { name: 'Standing answer' });
    await userEvent.clear(field);
    await userEvent.type(field, 'dev only');
    await userEvent.click(within(page).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STANDING', { payload: { repository: 'engine', says: 'dev only' } });

    await userEvent.click(within(page).getByRole('button', { name: 'Clear' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STANDING', { payload: { repository: 'engine' } });
  });

  /**
   * LANG1c (D142 point 7): a repository's session language on its page, beside its standing answer, as the driver's lines
   * resolve it and named from its table; set and cleared over the same file the terminal edits, on DAORIS.DRIVER. A shell
   * older than it answers no table, and nothing is offered.
   */
  it("shows a repository's session language on its page, and sets it on DAORIS.DRIVER", async () => {
    const table = [{ code: 'en', name: 'English' }, { code: 'zh', name: 'Simplified Chinese (简体中文)' }];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'LINES'
      ? { lines: [], languages: [{ repository: 'engine', workspace: 'aurora', language: 'zh', name: 'Simplified Chinese (简体中文)', source: 'workspace' }] }
      : { ...DRIVER_STATE, workspaceLanguages: [{ workspace: 'aurora', language: 'zh' }], languageTable: table }));
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Sessions');

    expect(await within(page).findByText('Simplified Chinese (简体中文), from this workspace.')).toBeInTheDocument();
    // Its workspace's, so it offers to be set for this repository, which opens the table.
    await userEvent.click(within(page).getByRole('button', { name: 'Set for this repository' }));
    const field = within(page).getByRole('combobox', { name: 'The session language for engine' });
    field.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'English' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_LANGUAGE', { payload: { repository: 'engine', language: 'en' } });
  });

  it('offers no session language on a shell older than it', async () => {
    invoke.mockImplementation(async () => DRIVER_STATE);
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    const page = await openSetup('Driving');

    await within(page).findByLabelText('Drive on this machine');
    // Neither a language nor a standing answer is answered, so Sessions has nothing to hold and is absent.
    expect(within(page).queryByRole('button', { name: 'Sessions' })).not.toBeInTheDocument();
    expect(within(page).queryByRole('combobox', { name: 'The session language for engine' })).not.toBeInTheDocument();
  });

  /**
   * The rail's words for a repository's standing on this machine (RepositoryGroup), on its row and its page's
   * head: held outranks *drives here*, and a registration with no checkout here says so.
   */
  it("marks a repository's standing on this machine, on its row and its page", async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'], holds: ['engine'] }));
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          { ...REGISTRY[0], root: 'C:/somewhere/engine' },
          { ...REGISTRY[0], repository: 'mirrored', summary: 'a teammate’s' },
        ]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    const engine = await repositoryRow('engine');
    expect(await within(engine).findByText('held')).toBeInTheDocument();
    expect(within(engine).queryByText('drives here')).toBeNull();
    expect(within(engine).queryByText('not on this machine')).toBeNull();
    expect(within(await repositoryRow('mirrored')).getByText('not on this machine')).toBeInTheDocument();

    const page = await chooseRepository('engine');
    const head = within(page).getByRole('heading', { level: 1, name: 'engine' }).closest('header')!;
    expect(within(head).getByText('held')).toBeInTheDocument();
  });
});

/**
 * UX6f (D150 §4.2, §3.1): a repository's setup on its page. Its page is Details and Setup, the tab remembered for the
 * view; Setup holds its line, how its work lands, whether agents outside it read it, what its sessions also write into and
 * Claude Code's rules for it, each set over the same file its terminal twin edits, on DAORIS.DRIVER. What only Setup
 * reads is asked only while Setup shows.
 */
describe("a repository's Setup on its page (UX6f)", () => {
  const LINES = {
    lines: [{ repository: 'engine', workspace: 'aurora', branch: 'main', source: 'workspace' }],
    landings: [{ repository: 'engine', workspace: 'aurora', form: 'merge', source: 'default' }],
    languages: [],
  };
  const ACROSS = {
    repositories: [
      { repository: 'engine', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] },
      { repository: 'game', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] },
      { repository: 'tools', workspace: 'forge', checkout: true, read: true, source: 'default', writesTo: [] },
    ],
  };
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [],
    scopes: [{ scope: 'repository', name: 'engine', allow: ['Bash(npm run test:*)'], ask: [], deny: [] }],
  };
  const PLUGINS = {
    folder: 'C:/somewhere/data/plugins',
    plugins: [{ id: 'github-pull-request', enabled: true, points: ['work/land'] }],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'LINES') return LINES;
      if (type === 'ACROSS') return ACROSS;
      if (type === 'RULES') return RULES;
      if (type === 'PLUGINS') return PLUGINS;
      return { ...DRIVER_STATE, drivable: ['engine'], workspaceLines: [{ workspace: 'aurora', branch: 'main' }] };
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('remembers the tab for the view, and asks reading across only while Setup shows', async () => {
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    expect(within(repositoryMain()).getByRole('tab', { name: 'Details' })).toHaveAttribute('aria-selected', 'true');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'ACROSS', expect.anything());

    await openSetup();
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACROSS', expect.anything()));
    expect(window.localStorage.getItem(TAB)).toBe('setup');

    cleanup();
    show(<ProjectsView notify={() => {}} />);
    await screen.findByRole('heading', { level: 1, name: 'engine' });
    expect(within(repositoryMain()).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
  });

  it("shows its line on Details read-only, whose door opens Setup at Line and landing", async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    expect(within(page).queryByRole('textbox')).toBeNull();
    await userEvent.click(await within(page).findByRole('button', { name: 'Change in Setup' }));
    expect(within(repositoryMain()).getByRole('button', { name: 'Line and landing' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('sets its line and how its work lands, each on DAORIS.DRIVER, and says what changed', async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseRepository('engine');
    await openSetup('Line and landing');
    const work = within(repositoryMain()).getByRole('region', { name: 'Line and landing' });

    const [line, landing] = within(work).getAllByRole('button', { name: 'Set for this repository' });
    await userEvent.click(line!);
    await userEvent.type(within(work).getByRole('textbox', { name: 'The line for engine' }), 'develop{Enter}');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_LINE', { payload: { repository: 'engine', branch: 'develop' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('In engine, work grows from `develop`')));

    // Every control waits while a change is on its way.
    await vi.waitFor(() => expect(landing).toBeEnabled());
    await userEvent.click(landing!);
    await userEvent.click(within(work).getByRole('radio', { name: 'Branch' }));
    // The line's field is still open beside it, since this bridge answers the line as its workspace's still.
    await userEvent.click(within(work).getAllByRole('button', { name: 'Save' }).at(-1)!);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_LANDING', {
      payload: { repository: 'engine', form: 'branch', pattern: 'feature/{quest}-{slug}' },
    });
  });

  it("sets whether agents outside it read it, what its sessions also write into, and removes one of its rules", async () => {
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('engine');
    // A rule of its own opens Reach on its own, once reading across has answered.
    await openSetup();
    const reach = await within(repositoryMain()).findByRole('region', { name: 'Reach' });

    await userEvent.click(within(reach).getByRole('button', { name: 'Set for this repository' }));
    await userEvent.click(within(reach).getByRole('radio', { name: 'Off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_READ_ACROSS', { payload: { repository: 'engine', read: false } });

    const user = userEvent.setup();
    within(reach).getByRole('combobox', { name: 'let engine write into…' }).focus();
    await user.keyboard('{Enter}');
    // Its own workspace's alone: `tools` is another workspace's.
    expect((await screen.findAllByRole('option')).map((option) => option.textContent)).toEqual(['game']);
    await user.click(screen.getByRole('option', { name: 'game' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_WRITE_ACROSS', { payload: { repository: 'engine', to: 'game', allow: true } });

    await userEvent.click(within(reach).getByRole('button', { name: 'remove Bash(npm run test:*)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', {
      payload: { action: 'remove', rule: 'Bash(npm run test:*)', scope: 'repository', name: 'engine' },
    });
  });
});

/**
 * UX6g (D150 §4.1, §4.3, §3.1): a workspace's page, where Settings → Workspace and Permissions went. Its row heads its group
 * in the list; its page holds Details (its repositories, what a start runs on, its accounts), Branches (the clean-up and
 * bringing up to date) and Setup (its defaults, its remote, its rules), each set over the same file its terminal twin
 * edits. The wiring rides DAORIS.REMOTES and never the service. Moved here from the retired domains' suites.
 */
describe("a workspace's page (UX6g)", () => {
  const REGISTRY_TWO = [
    { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
    { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
    { repository: 'tools', adopted: false, registered: true, owns: [], accepts: [], packs: [], entries: 0, workspace: 'forge' },
  ];
  const STATE = {
    ...DRIVER_STATE, drivable: ['engine'], workspaceLines: [], workspaceLandings: [], workspaceLanguages: [], workspaceReadAcross: [],
    languageTable: [{ code: 'en', name: 'English' }, { code: 'zh', name: 'Simplified Chinese (简体中文)' }],
  };
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [{
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: 'personal', pinned: null, managed: null, pinnable: true, ownLogin: 'in',
      profiles: [{ name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' }],
    }],
  };
  const STARTS = {
    adapter: 'claude-code',
    starts: [
      {
        job: 'work', workspace: 'aurora', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
        profile: 'personal', profileFrom: 'machine', version: 'claude 9.9.9', versionFrom: 'unset', commanded: false, refusal: null,
      },
      {
        job: 'intake', workspace: 'aurora', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
        profile: 'personal', profileFrom: 'machine', version: 'claude 9.9.9', versionFrom: 'unset', commanded: false, refusal: null,
      },
    ],
  };
  const SWEEP = {
    branches: [
      { repository: 'engine', workspace: 'aurora', branch: 'daoris/s-one', hasTree: false, kind: 'empty', commits: 0, removable: true },
      { repository: 'tools', workspace: 'forge', branch: 'daoris/s-two', hasTree: false, kind: 'empty', commits: 0, removable: true },
    ],
    landed: [],
  };
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [],
    scopes: [{ scope: 'workspace', name: 'aurora', allow: ['Bash(make:*)'], ask: [], deny: [] }],
  };
  const ACROSS = { repositories: [{ repository: 'engine', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] }] };
  const WIRED = 'workspace:aurora';
  const WORKSPACE_TAB = 'daoris.list.projects.workspaceTab';

  /** The machine, answered over the bridge; `over` names a type's answer of its own. */
  const machine = (over: Record<string, unknown> = {}) => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRY_TWO) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (type in over) {
        const answer = over[type];
        if (answer instanceof Error) throw answer;
        return answer;
      }
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') return STARTS;
      if (type === 'ACCOUNTS') return { agents: [] };
      if (type === 'SWEEP_PLAN') return SWEEP;
      if (type === 'TREES_SYNC_SCOPE') return { repositories: [{ repository: 'engine', workspace: 'aurora', holds: true }] };
      if (type === 'RULES' || type === 'RULE_ACTION') return RULES;
      if (type === 'ACROSS') return ACROSS;
      if (type === 'PLUGINS') return { folder: 'C:/somewhere/data/plugins', plugins: [] };
      if (type === 'LINES') return { lines: [], landings: [], languages: [] };
      return STATE;
    });
  };

  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /** §4.1: a group per workspace, its head a door to the workspace's page, remembered as the list's chosen item. */
  it("groups the list by workspace, and opens a workspace's page from its head, remembered", async () => {
    machine();
    show(<ProjectsView notify={() => {}} />);

    const page = await chooseWorkspace('aurora');
    expect(within(page).getByText('2 repositories · syncs with aurora.example.com')).toBeInTheDocument();
    expect(window.localStorage.getItem(CHOSEN)).toBe(WIRED);
    expect(within(page).getByRole('tab', { name: 'Details' })).toHaveAttribute('aria-selected', 'true');

    cleanup();
    show(<ProjectsView notify={() => {}} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
  });

  /** MAP1b, moved here from Settings → Workspace: the driver's answer for this workspace's starts, its intake's among them. */
  it('says on Details what a start in it runs on, its intake included, asking the driver for this workspace alone', async () => {
    machine();
    const onOpenAgent = vi.fn();
    show(<ProjectsView notify={() => {}} onOpenAgent={onOpenAgent} />);
    const page = await chooseWorkspace('aurora');

    const row = await within(page).findByRole('listitem', { name: 'a start in aurora' });
    expect(within(row).getByText('personal')).toBeInTheDocument();
    expect(within(row).getByText("this machine's default")).toBeInTheDocument();
    expect(within(page).getByRole('listitem', { name: 'an intake in aurora' })).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STARTS', { payload: { workspaces: ['aurora'] } });

    // Its repositories are doors, and its accounts' agent opens its page, where they are set (UX6e).
    expect(within(page).getByRole('listitem', { name: 'Claude Code' })).toHaveTextContent("this machine's: personal");
    await userEvent.click(within(page).getByRole('button', { name: 'Open Claude Code' }));
    expect(onOpenAgent).toHaveBeenCalledWith('claude-code');
    await userEvent.click(within(page).getByRole('button', { name: 'Open game' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'game' })).toBeInTheDocument();
  });

  /** WSR2, WSR1, LANG1c, READ1: a workspace's defaults, each the screen's half of `daoris driver <verb> --workspace`. */
  it('sets its defaults on Setup, each on DAORIS.DRIVER, and says what changed', async () => {
    machine();
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Setup' }));
    expect(window.localStorage.getItem(WORKSPACE_TAB)).toBe('setup');
    const head = await within(repositoryMain()).findByRole('button', { name: 'Defaults' });
    await userEvent.click(head);
    const defaults = within(repositoryMain()).getByRole('region', { name: 'Defaults' });

    await userEvent.click(within(defaults).getAllByRole('button', { name: 'Set for this workspace' })[0]!);
    await userEvent.type(within(defaults).getByRole('textbox', { name: 'The line for aurora' }), 'develop{Enter}');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_LINE', { payload: { workspace: 'aurora', branch: 'develop' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('In aurora, work grows from `develop`')));

    // The bridge answers no line set, so the field stays open; *Never mind* closes it, and the rows keep their order.
    await userEvent.click(within(defaults).getByRole('button', { name: 'Never mind' }));
    const user = userEvent.setup();
    await vi.waitFor(() => expect(within(defaults).getAllByRole('button', { name: 'Set for this workspace' })[2]).toBeEnabled());
    await user.click(within(defaults).getAllByRole('button', { name: 'Set for this workspace' })[2]!);
    within(defaults).getByRole('combobox', { name: 'The session language for aurora' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Simplified Chinese (简体中文)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_LANGUAGE', { payload: { workspace: 'aurora', language: 'zh' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Sessions in aurora write to you in Simplified Chinese (简体中文) now. A session already running keeps what it was handed.'));

    await vi.waitFor(() => expect(within(defaults).getAllByRole('button', { name: 'Set for this workspace' }).at(-1)).toBeEnabled());
    await user.click(within(defaults).getAllByRole('button', { name: 'Set for this workspace' }).at(-1)!);
    await user.click(within(defaults).getByRole('radio', { name: 'Off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_READ_ACROSS', { payload: { workspace: 'aurora', read: false } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(
      'aurora: its checkout is read by no agent outside it. A session already running keeps what it began with.'));
  });

  /**
   * The machine's wiring (D48 §5, D50): which deployment serves the workspace here. Shell-only and more strictly than the
   * rest: the service has no route onto it, so every call lands on the bridge and none on the API, and a key goes in and
   * comes back only as its audit prefix.
   */
  it('reads and unwires the remote over the bridge and never the service, showing only the key\'s audit prefix', async () => {
    machine();
    const notify = vi.fn();
    const { container } = show(<ProjectsView notify={notify} door={WIRED} section="remote" />);

    const remote = await screen.findByRole('region', { name: 'Remote and reach' });
    expect(await within(remote).findByText('https://aurora.example.com')).toBeInTheDocument();
    expect(within(remote).getByText('dk_abcd1234…')).toBeInTheDocument();
    expect(container.textContent).not.toContain('dk_abcd1234wxyz');
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'STATE', {});

    await userEvent.click(within(remote).getByRole('button', { name: 'Unwire' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'REMOVE', { payload: { workspace: 'aurora' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing at the deployment changed')));
    // Only what every browser is given goes over the service: the registry and the index's counts the list reads.
    expect(serviceCalls().filter((url) => !url.startsWith('/api/repositories'))).toEqual([]);
  });

  it('wires a workspace from its header, and lets the key go once it has landed', async () => {
    machine({ SET: { ...WIRING, remotes: [...WIRING.remotes, { workspace: 'forge', url: 'https://forge.example.com', key: 'dk_forg…' }] } });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    const page = await chooseWorkspace('forge');

    await userEvent.click(await within(page).findByRole('button', { name: 'Wire to a remote…' }));
    const remote = within(repositoryMain()).getByRole('region', { name: 'Remote and reach' });
    await userEvent.type(within(remote).getByLabelText('Deployment'), 'https://forge.example.com');
    await userEvent.type(within(remote).getByLabelText('Key'), 'dk_forgekey0000');
    await userEvent.click(within(remote).getByRole('button', { name: 'Wire' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'SET', {
      payload: { workspace: 'forge', url: 'https://forge.example.com', key: 'dk_forgekey0000' },
    });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('forge is wired on this machine.'));
    expect(within(repositoryMain()).queryByLabelText('Key')).toBeNull();
  });

  /** PERM1 (D72): Claude Code's rules for the workspace, where Settings → Permissions listed them (UX6g). */
  it("removes one of Claude Code's rules for the workspace, as the terminal's action", async () => {
    machine();
    show(<ProjectsView notify={() => {}} door={WIRED} section="remote" />);

    const remote = await screen.findByRole('region', { name: 'Remote and reach' });
    await userEvent.click(await within(remote).findByRole('button', { name: 'remove Bash(make:*)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', {
      payload: { action: 'remove', rule: 'Bash(make:*)', scope: 'workspace', name: 'aurora' },
    });
  });

  /**
   * WSR3, WSR6 (D88, D112): the clean-up and bringing up to date, moved here from Settings → Workspace. The clean-up lists
   * this workspace's session branches alone, and its press removes only what it listed; a look reaches the network as
   * the person, so it is asked by its own press, never on opening the tab.
   */
  it('cleans up only this workspace\'s branches on Branches, and looks for updates only when asked', async () => {
    machine({ SWEEP: { results: [], landed: [], removed: 1 } });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    const branches = within(repositoryMain());
    expect(await branches.findByText('daoris/s-one')).toBeInTheDocument();
    expect(branches.queryByText('daoris/s-two')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC_PLAN', expect.anything());
    expect(await branches.findByRole('button', { name: 'Look for updates' })).toBeInTheDocument();

    await userEvent.click(branches.getByRole('button', { name: /^Clean up/ }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SWEEP', { payload: { only: ['engine:daoris/s-one'] } });
  });

  /**
   * LAND3b (D102's LAND3 note): a failed or superseded attempt's branch, which no clean-up takes, is discarded from
   * Branches as `daoris-driver trees remove <branch> --repository <name> --force` discards it: offered beside the row the
   * driver says it may be, asked once, pressed forced, and the list read again.
   */
  it("discards a failed attempt's branch from Branches after asking once, as the terminal's forced removal", async () => {
    const failed = {
      repository: 'engine', workspace: 'aurora', branch: 'daoris/s-failed', hasTree: false, kind: 'unlanded', commits: 2,
      detail: 'a1b2c3d the work\ne4f5a6b more work', removable: false, discardable: true,
    };
    machine({
      SWEEP_PLAN: { branches: [...SWEEP.branches, failed], landed: [] },
      DISCARD_SESSION_BRANCH: { repository: 'engine', branch: 'daoris/s-failed', done: true, message: 'removed the session branch `daoris/s-failed` from `engine`.' },
    });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    const row = await within(repositoryMain()).findByRole('listitem', { name: 'daoris/s-failed' });
    expect(within(repositoryMain()).getByRole('listitem', { name: 'daoris/s-one' })).not.toHaveTextContent('Discard branch…');
    await userEvent.click(within(row).getByRole('button', { name: 'Discard branch…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', expect.anything());
    const ask = within(repositoryMain()).getByRole('group', { name: 'discard daoris/s-failed' });
    expect(ask).toHaveTextContent('Discards daoris/s-failed in engine, and with it 2 commits no branch of yours holds. Nothing brings them back.');
    const asked = invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length;

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'DISCARD_SESSION_BRANCH', {
      payload: { repository: 'engine', branch: 'daoris/s-failed', force: true },
    });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Discarded `daoris/s-failed` in engine.'));
    await vi.waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'SWEEP_PLAN').length).toBeGreaterThan(asked));
    // UXFIX2: the ask closes once the discard landed, never on the press.
    await vi.waitFor(() => expect(within(repositoryMain()).queryByRole('group', { name: 'discard daoris/s-failed' })).toBeNull());
    // Discarding one branch is not the clean-up's press.
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SWEEP', expect.anything());
  });

  /**
   * UXFIX2 (the second-opinion review, `Sweep.tsx:218`): a branch the driver kept is said inside the ask, in the driver's
   * own sentence, word for word, and the ask stays open; it was a toast after the ask had closed on the press.
   */
  it('says a branch the driver kept inside the discard’s ask, and keeps the ask open', async () => {
    const failed = {
      repository: 'engine', workspace: 'aurora', branch: 'daoris/s-failed', hasTree: false, kind: 'unlanded', commits: 2,
      removable: false, discardable: true,
    };
    machine({
      SWEEP_PLAN: { branches: [...SWEEP.branches, failed], landed: [] },
      DISCARD_SESSION_BRANCH: { repository: 'engine', branch: 'daoris/s-failed', done: false, message: '`daoris/s-failed` is checked out in a tree a session uses, so it was kept.' },
    });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    const row = await within(repositoryMain()).findByRole('listitem', { name: 'daoris/s-failed' });
    await userEvent.click(within(row).getByRole('button', { name: 'Discard branch…' }));
    const ask = within(repositoryMain()).getByRole('group', { name: 'discard daoris/s-failed' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));

    expect(await within(ask).findByRole('alert')).toHaveTextContent('daoris/s-failed is checked out in a tree a session uses, so it was kept.');
    expect(within(repositoryMain()).getByRole('group', { name: 'discard daoris/s-failed' })).toBeInTheDocument();
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('so it was kept'), 'error');
  });

  /**
   * WSR6, WSR7: a look takes what the driver takes, every repository holding Daoris's branches here; this workspace's page
   * lists and brings up to date its own rows alone, each waiting as long as the host may work.
   */
  it("brings up to date only this workspace's rows of what the look listed", async () => {
    const plan = {
      lines: [
        { repository: 'engine', workspace: 'aurora', line: 'main', kind: 'fast-forward', commits: 1, moves: true },
        { repository: 'tools', workspace: 'forge', line: 'main', kind: 'fast-forward', commits: 2, moves: true },
      ],
      rebases: [{ repository: 'engine', workspace: 'aurora', branch: 'daoris/s-step', landed: false, kind: 'replay', onto: 'main', commits: 1, replays: true }],
      deletes: [],
    };
    machine({ TREES_SYNC_PLAN: plan, TREES_SYNC: { lines: [], rebases: [], deletes: [], changed: 2 } });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Look for updates' }));
    const updates = within(await screen.findByRole('region', { name: 'Updates' }));
    expect(await updates.findByRole('group', { name: 'engine' })).toBeInTheDocument();
    expect(updates.queryByRole('group', { name: 'tools' })).toBeNull();
    await userEvent.click(updates.getByRole('button', { name: 'Bring up to date (2)' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC', expect.objectContaining({
      payload: { only: ['engine:main', 'engine:daoris/s-step'] },
    }));
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('2 of 2 done. What did not happen is still listed, with why.'));
  });

  /** WSR7: a look the page stopped waiting for is said in the section, where it was asked, not only in a toast. */
  it('says on Branches that it stopped waiting for a look', async () => {
    machine({ TREES_SYNC_PLAN: Object.assign(new Error('DAORIS.DRIVER.TREES_SYNC_PLAN timed out'), { code: 'TIMEOUT' }) });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Look for updates' }));
    const updates = within(screen.getByRole('region', { name: 'Updates' }));
    expect((await updates.findByRole('alert')).textContent).toMatch(/stopped waiting before the look answered/);
    expect(notify).not.toHaveBeenCalledWith(expect.stringMatching(/did not answer in time/), 'error');
  });

  /** §3b, UX6b: a workspace no repository here is in now says so, and a remembered one opens nothing chosen. */
  it('says a workspace chosen now has gone, and opens with nothing chosen on a remembered one that went', async () => {
    machine();
    show(<ProjectsView notify={() => {}} door="workspace:retired" />);
    expect(await within(repositoryMain()).findByText('This workspace is no longer here')).toBeInTheDocument();

    cleanup();
    window.localStorage.setItem(CHOSEN, 'workspace:retired');
    show(<ProjectsView notify={() => {}} />);
    await vi.waitFor(() => expect(repositoryMain()).toHaveTextContent('Choose a repository'));
    expect(window.localStorage.getItem(CHOSEN)).toBeNull();
  });
});

/**
 * Managing the machine's repositories (D48 §7). Three properties are worth a test rather than a
 * comment, because each fails silently: the machine path comes from the SHELL and never from the page,
 * retiring says what it does not do, and the two kinds of update stay visibly apart.
 */
describe('the shell-attached registry management', () => {
  const PICKED = {
    path: 'D:/repos/borealis', name: 'borealis', exists: true, adopted: true, git: true,
    summary: 'The aurora.', owns: ['the sky'], accepts: ['a quest'], packs: [], join: false,
    shareKnowledge: false, uses: ['engine'],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'DELETE') {
        return Response.json({
          repository: 'engine', retired: true,
          message: 'Nothing was deleted: its files, its history and its doctrine are its own.',
        });
      }
      if (url === '/api/registry' && init?.method === 'POST') {
        return Response.json({ repository: 'borealis', workspace: 'aurora' });
      }
      if (url === '/api/registry/import' && init?.method === 'POST') {
        return Response.json({
          folder: 'D:/repos/borealis', imported: 2, repositories: ['aurora', 'dusk'], workspace: 'borealis',
          message: 'Registered 2 from D:/repos/borealis into workspace `borealis`: aurora, dusk.',
        });
      }
      if (url.includes('/workspace')) return Response.json({ repository: 'engine', workspace: 'tools' });
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'PICK_FOLDER' ? PICKED : DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('adding a repository takes its path from the shell, never from the page', async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);

    // The list's ＋ is the act (D118 §2): one kind, so the press is the act itself.
    await userEvent.click(await within(repositoryList()).findByRole('button', { name: 'Add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REGISTRY', 'PICK_FOLDER', { timeoutMs: Infinity });
    expect(await screen.findByText('D:/repos/borealis')).toBeInTheDocument();

    // UX5 U38: the move leads and *never mind* follows, as in every other drawer; this one was reversed.
    const register = screen.getByRole('button', { name: 'Register' });
    expect(register.compareDocumentPosition(screen.getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(register);

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toMatchObject({
      repository: 'borealis', root: 'D:/repos/borealis', adopted: true,
      // What its manifest says it uses travels with the rest of the declaration (MANAGE1, D91).
      domain: { summary: 'The aurora.', owns: ['the sky'], accepts: ['a quest'], uses: ['engine'] },
    });
    // Once registered, the list has it chosen, as an installed plugin is (D119 §3.1).
    await vi.waitFor(() => expect(window.localStorage.getItem(CHOSEN)).toBe('borealis'));
  });

  /**
   * Registered is addressable; adopted is disciplined (D70) — so a folder with no manifest is added as
   * what it is. The register door used to store every row adopted, which would have driven one over
   * the pipe door with no connector and no sentence saying why.
   */
  it('adding a folder that has not adopted registers it as not adopted, and declares nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'PICK_FOLDER'
        ? { ...PICKED, adopted: false, summary: undefined, owns: [], accepts: [] }
        : DRIVER_STATE);
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await within(repositoryList()).findByRole('button', { name: 'Add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Register' }));

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    const body = JSON.parse(String(posted![1]!.body));
    expect(body).toMatchObject({ repository: 'borealis', root: 'D:/repos/borealis', adopted: false });
    expect(body.domain).toBeUndefined();
  });

  /**
   * Setting a folder up as a workspace (D77, FG4): `daoris import <folder> --workspace <name>`'s
   * screen twin. The Workspace menu's import used to state no workspace at all, so a folder set up from
   * the window landed in `default`. The folder's name is offered, since that is what setting a folder up
   * as a workspace means; the service's sentence comes back verbatim.
   */
  it("importing a folder names the workspace its repositories land in, offering the folder's name", async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} importRequested onImportOpened={() => {}} />);

    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
    expect(await within(drawer).findByText('D:/repos/borealis')).toBeInTheDocument();
    expect(within(drawer).getByLabelText('Workspace')).toHaveValue('borealis');

    // The move leads and *never mind* follows, as in every drawer (UX5 U38).
    const importIt = within(drawer).getByRole('button', { name: 'Import' });
    expect(importIt.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(importIt);

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry/import' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toEqual({ folder: 'D:/repos/borealis', workspace: 'borealis' });
    expect(notify).toHaveBeenCalledWith('Registered 2 from D:/repos/borealis into workspace `borealis`: aurora, dusk.');
  });

  /** D118 §2: *Import a folder* is the list's ⋯, beside the ＋ that adds one. */
  it("opens the import from the list's ⋯", async () => {
    const user = userEvent.setup();
    show(<ProjectsView notify={() => {}} />);

    // A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway.
    (await within(repositoryList()).findByRole('button', { name: 'More actions' })).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Import a folder…' }));

    expect(await screen.findByRole('dialog', { name: 'Import a folder as a workspace' })).toBeInTheDocument();
  });

  /** Emptied, the import names none — and an import that names none still moves nobody (D48 §2). */
  it('an import with its workspace emptied names none', async () => {
    show(<ProjectsView notify={() => {}} importRequested onImportOpened={() => {}} />);

    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
    await userEvent.clear(await within(drawer).findByLabelText('Workspace'));
    await userEvent.click(within(drawer).getByRole('button', { name: 'Import' }));

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry/import' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toEqual({ folder: 'D:/repos/borealis' });
  });

  /**
   * The one thing a person must be able to trust about a remove button. The service composes the
   * sentence; the panel says it before the click, and the answer repeats it after.
   */
  it('retiring says what it does not do, before and after', async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    expect(within(drawer).getByText(/Nothing is deleted/)).toBeInTheDocument();

    // Two clicks, deliberately: the first is not the destructive one.
    await userEvent.click(within(drawer).getByRole('button', { name: 'Retire' }));
    // UX5 U38: the move, then *never mind*, as every other drawer puts them; this pair was reversed.
    const confirm = within(drawer).getByRole('button', { name: 'Retire repository' });
    expect(confirm.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(confirm);

    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registry/engine', { method: 'DELETE' });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing was deleted'));
    // Retired by the person's own press, so the page goes back to choosing, not to *gone*.
    expect(await within(repositoryMain()).findByText('Choose a repository')).toBeInTheDocument();
    expect(window.localStorage.getItem(CHOSEN)).toBeNull();
  });

  /**
   * UX5 U37: *owns* and *accepts* were edited in monospace, beside a summary in the body face, and
   * they become chips in the body face. They are phrases, not code, so they are written as phrases.
   */
  it('edits a declaration in the face it is read in', async () => {
    // The declaration is edited only where its repository has a root on this machine.
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], root: 'C:/somewhere/engine' }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');

    for (const field of ['Summary', 'Owns', 'Accepts']) {
      expect(within(drawer).getByLabelText(field)).not.toHaveClass('font-mono');
    }
  });

  /**
   * MANAGE1: the registry replaces a row's `uses` with the declaration's (D91), and silence there is
   * none, since `connect` and the driver's sync send `uses` only when there is one. Saving re-registered
   * with a domain that carried no `uses`, so the repository's dependencies left the registry until its
   * next `connect`, while the file just written still held them. The re-registration says what the file
   * says, from the shell's answer to the write.
   */
  it('saving a declaration re-registers with what the written file says it uses', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/registry' && init?.method === 'POST') return Response.json({ repository: 'engine', workspace: 'default' });
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], root: 'C:/somewhere/engine' }]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'WRITE_DECLARATION'
        ? {
          path: 'C:/somewhere/engine', name: 'engine', exists: true, adopted: true, git: true,
          summary: 'the engine', owns: ['the runtime'], accepts: [], packs: [], join: false, shareKnowledge: false,
          uses: ['tools'],
        }
        : DRIVER_STATE);
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Write daoris.json' }));

    await vi.waitFor(() => expect(notify).toHaveBeenCalled());
    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body)).domain).toEqual({
      summary: 'the engine', owns: ['the runtime'], accepts: [], uses: ['tools'],
    });
  });

  // The CONSOLE's tests moved to `work/WorkFrame.test.tsx` with the console itself (D55): one home
  // for the stream, and the quest's page keeps the record summary plus a door into it.

  /**
   * Provenance is served, not implied (D48 §6). A person looking at a repository's knowledge must be
   * able to see which commit it came from — freshness they have to assume is exactly what the rule
   * exists to replace.
   */
  it('projects name the commit a deployment was fed from', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    expect(await within(page).findByText(/c0ffee12/)).toBeTruthy();
    // Relative time beside it: "how stale is this" is the question being answered.
    expect(within(page).getByText(/c0ffee12 · /)).toBeTruthy();
  });

  /**
   * One sentence for one fact. On the deployed family, a repository present in the index with a
   * count of zero read "0 entries · 0 local · 0 canonical", one absent from it read "nothing
   * indexed yet", and one outside the family read "—" — three renderings of "the index holds
   * nothing of this", on one page. Since FRAME1e an adopted repository's count is on its page, and one
   * not adopted says its count on its row, where nothing else does.
   */
  it('says "nothing indexed yet" the same way whether a count is zero, missing, or outside', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          ...REGISTRY,
          { ...REGISTRY[0], repository: 'blank', summary: 'indexed, holding nothing' },
          { ...REGISTRY[0], repository: 'unseen', summary: 'never indexed' },
          { ...REGISTRY[0], repository: 'stranger', adopted: false },
          { ...REGISTRY[0], repository: 'lone', adopted: false },
          { ...REGISTRY[0], repository: 'many', adopted: false },
        ]);
      }
      if (url.startsWith('/api/repositories')) {
        return Response.json([
          ...REPOSITORIES,
          { name: 'blank', total: 0, local: 0, canonical: 0, workspace: 'default' },
          { name: 'lone', total: 1, local: 1, canonical: 0, workspace: 'default' },
          { name: 'many', total: 1234, local: 1234, canonical: 0, workspace: 'default' },
        ]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    // A count of one is singular — "1 entries" was on the deployed page too — and a count in the
    // thousands keeps its separator, so the plural form and the formatting are two parameters.
    expect(within(await repositoryRow('lone')).getByText('1 entry indexed read-only')).toBeTruthy();
    expect(within(await repositoryRow('many')).getByText('1,234 entries indexed read-only')).toBeTruthy();
    expect(within(await repositoryRow('stranger')).getByText('nothing indexed yet')).toBeTruthy();

    expect(within(await chooseRepository('engine')).getByText('1 entry · 1 local · 0 canonical')).toBeTruthy();
    for (const name of ['blank', 'unseen']) {
      expect(within(await chooseRepository(name)).getByText('nothing indexed yet')).toBeTruthy();
    }
    expect(screen.queryByText(/^0 entries/)).not.toBeInTheDocument();
    expect(screen.queryByText('—')).not.toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (POLISH4): a chip that wrapped fell back under its row's label, because
   * the label and the chips were one flowing line. The labels are a column of their own now.
   */
  it("keeps a project row's label apart from its chips, so a wrapped chip lines up with the first", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([{ ...REGISTRY[0], owns: ['the runtime', 'the public API'], accepts: ['a failing case'] }]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    const owns = within(page).getByText('Owns');
    expect(owns.tagName).toBe('DT');
    expect(owns.nextElementSibling?.tagName).toBe('DD');
    expect(within(owns.nextElementSibling as HTMLElement).getByText('the public API')).toBeTruthy();
  });

  /** The join steps were all monospace, prose included; the commands are code in a sentence. */
  it('sets the join steps as a sentence with its commands as code', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([...REGISTRY, { ...REGISTRY[0], repository: 'lone', adopted: false }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('lone');

    const init = within(page).getByText(code('daoris init'));
    expect(init.tagName).toBe('CODE');
    expect(init.parentElement).not.toHaveClass('font-mono');
    // It wraps at the page's edge with every block of it (D141): a 65ch sentence beside blocks that ran
    // the pane gave the page two edges.
    expect(init.parentElement!.className).not.toMatch(/\bmax-w-/);
  });

  /** Re-wiring is a row on this machine; it must not touch the repository's tracked file. */
  it('re-wiring edits one row and writes no file', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    await userEvent.clear(within(drawer).getByLabelText('Workspace'));
    await userEvent.type(within(drawer).getByLabelText('Workspace'), 'tools');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Move to workspace' }));

    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      '/api/registry/engine/workspace',
      expect.objectContaining({ method: 'POST' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.REGISTRY', 'WRITE_DECLARATION', expect.anything());
  });
});

// CONVERSATIONS moved to `work/WorkFrame.test.tsx` with the surface that starts and holds them
// (design §3): starting a session belongs where its result appears, not in the registry's view.

/**
 * INT3c: an unadopted repository with a root here is drivable over the protocol door (D70), so the
 * screen offers what `daoris driver` already can: the same driving row an adopter's page carries
 * (D50). One with no root here has nowhere to start, and is offered nothing. On a machine whose
 * door is direct, the page says a quest there will sit — the planner's own answer, said before it.
 */
describe('an unadopted repository on this machine (INT3c)', () => {
  const WITH_OUTSIDERS = [
    ...REGISTRY,
    { repository: 'newbie', adopted: false, addressable: true, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
    { repository: 'elsewhere', adopted: false, addressable: false, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
  ];
  const roster = (adapter: string) => ({
    settingsPath: 'C:/somewhere/data/harnesses.json',
    adapter,
    harnesses: [
      { harness: 'claude-code', present: true, wire: 'pipe', version: '2.1.281', problem: null, machineDefault: null, pinned: null, managed: null, pinnable: true, profiles: [] },
      { harness: 'claude-code-acp', present: true, wire: 'acp', accountOf: 'claude-code', version: '0.79.0', problem: null, machineDefault: null, pinned: null, managed: null, pinnable: true, profiles: [] },
    ],
  });
  const machine = (adapter: string) => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      if (type === 'HARNESSES') return roster(adapter);
      return { ...DRIVER_STATE, adapter };
    });
  };
  const DIRECT_NOTE = 'This machine drives on a direct agent, so a quest here sits, saying why, until it drives on a protocol one.';

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(WITH_OUTSIDERS) : respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('offers the driving row to one with a root here, landing on DAORIS.DRIVER', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('newbie');
    const page = await openSetup('Driving');

    await userEvent.click(await within(page).findByLabelText('Drive on this machine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'newbie', drivable: true },
    });
    // A protocol-door machine can carry it, so nothing is added beside the choice.
    expect(within(page).queryByText(DIRECT_NOTE)).toBeNull();
    // None of adoption's own acts, like managing its declaration, which writes into the repository.
    expect(within(page).queryByRole('button', { name: 'Manage' })).toBeNull();
  });

  it('says a quest there will sit, on a machine whose door is direct', async () => {
    machine('claude-code');
    show(<ProjectsView notify={() => {}} />);
    await chooseRepository('newbie');
    const page = await openSetup('Driving');

    expect(await within(page).findByText(DIRECT_NOTE)).toBeInTheDocument();
    expect(within(page).getByLabelText('Drive on this machine')).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U36: the group opened with an eight-line paragraph above its one row, read once and
   * scrolled past every visit after, the essay a settings page was before its rows (§4, `SettingRow`).
   * It leads with one line now, and the reasoning is on the info glyph, where a row keeps its why.
   */
  it('leads with one line, and keeps its reasoning on the info glyph', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('newbie');

    expect(within(page).getByText(/Registered here without a manifest/)).toBeInTheDocument();
    expect(within(page).queryByText(/^Adoption is a repository's own act/)).toBeNull();
    expect(within(page).getByRole('note', { name: /Adoption is a repository's own act/ })).toBeInTheDocument();
  });

  it('offers nothing to drive for one with no root here — there is nowhere to start it', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('elsewhere');

    expect(await within(page).findByText('Adoption steps')).toBeInTheDocument();
    await openSetup();
    expect(within(repositoryMain()).queryByRole('button', { name: 'Driving' })).toBeNull();
    expect(within(repositoryMain()).queryByLabelText('Drive on this machine')).toBeNull();
  });
});
