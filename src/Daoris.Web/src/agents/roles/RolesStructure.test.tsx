import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// COMPANYVIEW1d (the company-view design §3.3 to §3.5): Work, Accounts and By repository in the Agents place's Roles mode, over the
// driver's one read of the structure. Each section's rows, states, filters, *Needs a look* and doors; an agent's page's *Used by*.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
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

import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { CLAUDE_USE, CODEX_USE, DOORS } from '../agentsFixtures';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE } from './structureFixtures';

const doors = {
  agent: vi.fn(), repository: vi.fn(), workspace: vi.fn(), repositories: vi.fn(), plugin: vi.fn(),
};

function Place({ mode = 'roles' }: { mode?: 'roles' | 'agents' }) {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {}, roleDoors: doors });
  void mode;
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const ROSTER = { settingsPath: 'x', adapter: 'claude-code', harnesses: DOORS };

const REGISTRATIONS = REGISTRY.map((row) => ({
  repository: row.repository, adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, ...(row.workspace ? { workspace: row.workspace } : {}),
}));

const SECTION = { Work: /^Work(?!ing)/, Accounts: /^Accounts/, 'By repository': /^By repository/ } as const;
const section = (name: keyof typeof SECTION) => screen.findByRole('button', { name: SECTION[name] });

describe('The Roles mode, Work, Accounts and By repository', () => {
  let structure: 'ok' | 'noroute' | 'failed' | 'pending' = 'ok';
  const asked = () => invoke.mock.calls.filter(([, type]) => type === 'ROLE_STRUCTURE').length;

  beforeEach(() => {
    structure = 'ok';
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    i18n.changeLanguage('en');
    Object.values(doors).forEach((door) => door.mockReset());
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRATIONS) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      switch (type) {
        case 'STATE': return { ...DRIVER_STATE, drivable: DRIVER.drivable, holds: DRIVER.holds };
        case 'HARNESSES': return ROSTER;
        case 'ACCOUNTS': return { agents: [CLAUDE_USE, CODEX_USE] };
        case 'ACTORS': return { actors: [] };
        case 'ROLES': return { roles: [], works: ['build', 'design'], kept: true, suggested: [] };
        case 'STARTS': return { adapter: 'claude-code', starts: [] };
        case 'PLUGINS': return { folder: 'plugins', plugins: PLUGINS };
        case 'LINES': return LINES;
        case 'ROLE_STRUCTURE':
          if (structure === 'noroute') throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
          if (structure === 'failed') throw new Error('the structure did not read');
          if (structure === 'pending') return { pending: true };
          return STRUCTURE;
        case 'WORKFLOW_CURRENT': {
          const name = request?.payload?.repository;
          if (name === 'tools') throw new Error('no checkout');
          return {
            repository: name, workspace: 'aurora', startHolds: [], steps: [{}, {}, {}], version: 'abc', limits: {},
            start: { routes: [], landed: {}, verifications: [], integration: name === 'engine' ? { gates: null, state: 'unaccepted' } : null },
          };
        }
        default: return undefined;
      }
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  describe('Work', () => {
    const open = async () => {
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await section('Work'));
      await screen.findByRole('heading', { name: 'Work' });
      return user;
    };

    it('draws the nine works, each with the role taking it here and how many places have their own', async () => {
      await open();
      const build = await screen.findByRole('button', { name: 'Show building' });
      const row = build.closest('li')!;
      expect(within(row).getByText('Build')).toBeTruthy();
      expect(within(row).getByText(/2 places have their own/)).toBeTruthy();
      // Design takes building's role; partnering is not a rule yet.
      expect(within(screen.getByRole('button', { name: 'Show design and decisions' }).closest('li')!).getByText("Build, building's role")).toBeTruthy();
      expect(within(screen.getByRole('button', { name: 'Show partnering' }).closest('li')!).getByText('not a rule yet')).toBeTruthy();
      expect(document.querySelectorAll('[data-structure-row^="work:"]')).toHaveLength(9);
    });

    it('says the effective rule with where it was set, and an agent no choice names at account default', async () => {
      await open();
      const help = (await screen.findByRole('button', { name: 'Show helping' })).closest('li')!;
      expect(within(help).getByText('ask · agents: claude-code · up to 2 at once · 10 min · set for the workspace')).toBeTruthy();
      const brainstorm = screen.getByRole('button', { name: 'Show brainstorms' }).closest('li')!;
      expect(within(brainstorm).getByText(/dsh run at account default/)).toBeTruthy();
    });

    it('opens a work to its choices at their positions with each state and the reading it rests on', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show building' }));
      const row = screen.getByRole('button', { name: 'Hide building' }).closest('li')!;
      expect(within(row).getByText(/choice 1/)).toBeTruthy();
      expect(within(row).getByText(/codex-acp · account default · account default · cannot run: signed out · read/)).toBeTruthy();
      expect(row.textContent).toMatch(/claude-code · opus · high · can run · read/);
    });

    it.each([
      ['en', 'May call', /^every tool but .*brainstorm_give.*settings_timeout$/],
      ['zh', '可调用', /^除 .*brainstorm_give.*settings_timeout 外的所有工具$/],
    ])('names, read only, what each work may call when opened, in %s', async (language, label, buildCalls) => {
      const user = await open();
      await act(() => i18n.changeLanguage(language));
      const row = (work: string) => document.querySelector(`[data-structure-row="work:${work}"]`) as HTMLElement;
      await waitFor(() => expect(row('build')).toBeTruthy());
      for (const work of ['partner', 'build']) await user.click(within(row(work)).getAllByRole('button')[0]!);
      const calls = (work: string) => [...row(work).querySelectorAll('p')].find((p) => p.textContent?.startsWith(label))?.lastElementChild?.textContent;
      expect(calls('partner')).toMatch(/^knowledge_convergence, knowledge_get, .*, partner_answer, registry$/);
      expect(calls('build')).toMatch(buildCalls);
      expect(calls('build')).not.toContain('quest_publish');
      expect(within(row('partner')).queryByRole('textbox')).toBeNull();
    });

    it("presses 'Open its setting' to its home, Working roles for Intake's Off, and none for partnering", async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show helping' }));
      await user.click(screen.getByRole('button', { name: 'Open the setting of helping' }));
      // No workspace filtered: Repositories.
      expect(doors.repositories).toHaveBeenCalled();
      await user.click(screen.getByRole('button', { name: 'Show partnering' }));
      expect(screen.queryByRole('button', { name: 'Open the setting of partnering' })).toBeNull();
    });

    it('narrows to a workspace\'s scope and tells the workspace Setup door', async () => {
      const user = await open();
      await user.click(await screen.findByRole('combobox', { name: 'Workspace' }));
      await user.click(await screen.findByRole('option', { name: 'aurora' }));
      expect(await screen.findByText('In workspace aurora')).toBeTruthy();
      // One repository under aurora has its own build role.
      expect(within(screen.getByRole('button', { name: 'Show building' }).closest('li')!).getByText(/1 place has its own/)).toBeTruthy();
      await user.click(screen.getByRole('button', { name: 'Show helping' }));
      await user.click(screen.getByRole('button', { name: 'Open the setting of helping' }));
      expect(doors.workspace).toHaveBeenCalledWith('aurora');
    });

    it('asks the read once, whatever the filters say', async () => {
      const user = await open();
      await screen.findByRole('button', { name: 'Show building' });
      const before = asked();
      await user.type(screen.getByRole('searchbox', { name: 'Name' }), 'chat');
      expect(screen.queryByRole('button', { name: 'Show building' })).toBeNull();
      expect(screen.getByRole('button', { name: 'Show chat' })).toBeTruthy();
      expect(asked()).toBe(before);
    });

    it('keeps *Needs a look* to the works that cannot run or hold, and says nothing matches otherwise', async () => {
      const user = await open();
      await screen.findByRole('button', { name: 'Show building' });
      await user.click(screen.getByRole('checkbox', { name: 'Needs attention' }));
      const rows = [...document.querySelectorAll('[data-structure-row^="work:"]')].map((row) => row.getAttribute('data-structure-row'));
      expect(rows).toEqual(['work:review']);
      await user.type(screen.getByRole('searchbox', { name: 'Name' }), 'zzz');
      expect(screen.getByText('Nothing matches these filters.')).toBeTruthy();
      await user.click(screen.getByRole('button', { name: 'Clear the filters' }));
      expect(document.querySelectorAll('[data-structure-row^="work:"]').length).toBe(9);
    });

    it('opens and probes nothing: no account is asked and no start is walked', async () => {
      await open();
      await screen.findByRole('button', { name: 'Show building' });
      const types = invoke.mock.calls.map(([, type]) => type as string);
      expect(types.filter((type) => /READ|SIGN|PROBE|REFRESH|LOGIN/.test(type))).toEqual([]);
    });

    it.each([
      ['noroute', /Not offered by this shell/],
      ['failed', /could not be read/],
      ['pending', /Reading the roles/],
    ] as const)('says a read that is %s, never an empty list', async (state, words) => {
      structure = state;
      await open();
      expect(await screen.findByText(words)).toBeTruthy();
      expect(screen.queryByText('Nothing matches these filters.')).toBeNull();
      expect(document.querySelectorAll('[data-structure-row]')).toHaveLength(0);
    });

    it('says a catalogue that does not read on the section', async () => {
      const answer = invoke.getMockImplementation()!;
      invoke.mockImplementation(async (module: string, type: string, request?: unknown) =>
        module === 'DAORIS.DRIVER' && type === 'ROLE_STRUCTURE'
          ? { ...STRUCTURE, roles: [], catalogue: { why: 'roles.json does not read' } }
          : answer(module, type, request));
      await open();
      expect(await screen.findByRole('alert')).toHaveTextContent('every work holds its starts: roles.json does not read');
    });

    it('is spoken in Chinese', async () => {
      await i18n.changeLanguage('zh');
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await screen.findByRole('button', { name: new RegExp(`^${i18n.t('nav.roles.work')}$`) }));
      expect((await screen.findAllByRole('button', { name: /^展开/ })).length).toBe(9);
      expect(screen.getByRole('checkbox', { name: '需要留意' })).toBeTruthy();
    });
  });

  describe('Accounts', () => {
    const open = async () => {
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await section('Accounts'));
      await screen.findByRole('heading', { name: 'Accounts' });
      return user;
    };

    it('draws each agent a choice names with its accounts, when they were read and who uses it', async () => {
      await open();
      const claude = (await screen.findByRole('button', { name: 'Show Claude Code' })).closest('li')!;
      expect(within(claude).getByText(/3 accounts/)).toBeTruthy();
      expect(within(claude).getByText(/used by 7 roles/)).toBeTruthy();
      expect(within(claude).getByText(/read /)).toBeTruthy();
    });

    it('opens an agent to the roles that use it at their positions and the state of each choice', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show Codex' }));
      const row = screen.getByRole('button', { name: 'Hide Codex' }).closest('li')!;
      expect(row.textContent).toMatch(/Build · building · choice 2/);
      expect(row.textContent).toMatch(/cannot run: signed out/);
      expect(row.textContent).toMatch(/Second opinion · second opinions · choice 1/);
      expect(row.textContent).toMatch(/cooling until/);
    });

    it('says a named agent not installed and one this build does not know, each needing a look', async () => {
      const user = await open();
      await user.click(await screen.findByRole('checkbox', { name: 'Needs attention' }));
      const dsh = (await screen.findByRole('button', { name: 'Show DeepSeek Harness' })).closest('li')!;
      expect(within(dsh).getByText(/not installed/)).toBeTruthy();
      await user.click(screen.getByRole('button', { name: 'Show ghost-agent' }));
      expect(screen.getByText('A choice names it, and no agent of that name is known here.')).toBeTruthy();
    });

    it('opens the Agents mode at the agent\'s accounts from its row', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show Codex' }));
      await user.click(screen.getByRole('button', { name: 'Open Codex' }));
      expect(doors.agent).toHaveBeenCalledWith('codex');
    });

    it('narrows to one agent', async () => {
      const user = await open();
      await user.click(await screen.findByRole('combobox', { name: 'Agent' }));
      await user.click(await screen.findByRole('option', { name: 'codex' }));
      expect(document.querySelectorAll('[data-structure-row^="agent:"]')).toHaveLength(1);
    });
  });

  describe('By repository', () => {
    const open = async () => {
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await section('By repository'));
      await screen.findByRole('heading', { name: 'By repository' });
      return user;
    };

    it('draws workspaces as folded groups with the number of repositories and of those with roles of their own', async () => {
      await open();
      const aurora = await screen.findByRole('button', { name: 'Show aurora' });
      expect(aurora).toHaveAttribute('aria-expanded', 'false');
      expect(aurora.textContent).toMatch(/2 repositories · 1 with roles of their own/);
      expect(screen.queryByRole('button', { name: 'Show engine' })).toBeNull();
    });

    it('folds a repository to takes this machine\'s roles or n works differ', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show aurora' }));
      expect(within((await screen.findByRole('button', { name: 'Show engine' })).closest('li')!).getByText('1 work differs')).toBeTruthy();
      expect(within(screen.getByRole('button', { name: 'Show site' }).closest('li')!).getByText("takes this machine's roles")).toBeTruthy();
    });

    it('opens a repository to each work, its line, landing, driving, workflow, integration declaration and plugins, and asks its workflow then', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show aurora' }));
      expect(invoke.mock.calls.filter(([, type]) => type === 'WORKFLOW_CURRENT')).toHaveLength(0);
      await user.click(await screen.findByRole('button', { name: 'Show engine' }));
      const row = (await screen.findByRole('button', { name: 'Hide engine' })).closest('li')!;
      await waitFor(() => expect(row.textContent).toMatch(/3 steps/));
      expect(row.textContent).toMatch(/Engine build \(in repository engine\) · claude-code · sonnet · high/);
      expect(row.textContent).toMatch(/main, set for the repository/);
      expect(row.textContent).toMatch(/branch by plugin github-pull-request, set for the repository/);
      expect(row.textContent).toMatch(/driven/);
      expect(row.textContent).toMatch(/proposed, waiting for you to accept it/);
      expect(within(row).getByRole('button', { name: 'Open plugin GitHub pull request' })).toBeTruthy();
      expect(invoke.mock.calls.filter(([, type, request]) => type === 'WORKFLOW_CURRENT' && (request as { payload: { repository: string } }).payload.repository === 'site')).toHaveLength(0);
    });

    // COMPANYVIEW1g: a new place takes the roles above it; a role of its own is added here, then saved over SET_ROLE.
    it.each([
      ['a repository row', 'engine', 'repository'],
      ['a workspace group', 'aurora', 'workspace'],
    ])('adds a role here from %s: the editor opens in Working roles with the place filled, and Save goes out over SET_ROLE', async (_name, place, kind) => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show aurora' }));
      if (kind === 'repository') await user.click(await screen.findByRole('button', { name: 'Show engine' }));
      await user.click(await screen.findByRole('button', { name: `Add a role here · ${place}` }));

      const form = await screen.findByRole('form', { name: 'A new role' });
      expect(screen.getByRole('heading', { name: 'Working roles' })).toBeTruthy();
      expect(within(form).getByText(new RegExp(`Placed in ${kind} ${place}; the place is fixed`))).toBeTruthy();
      expect(within(form).queryByRole('textbox', { name: 'Id' })).toBeNull();

      await user.type(within(form).getByRole('textbox', { name: 'Name' }), 'Night shift');
      await user.click(within(form).getByRole('button', { name: 'Save' }));
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', {
        payload: expect.objectContaining({ change: 'set', id: 'night-shift', role: expect.objectContaining({ scope: { kind, id: place } }) }),
      }));
    });

    it('presses its doors: Setup, Workflow, the Integration tab where a declaration waits, and the plugin', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show aurora' }));
      await user.click(await screen.findByRole('button', { name: 'Show engine' }));
      await user.click(await screen.findByRole('button', { name: 'Open Setup · engine' }));
      await user.click(screen.getByRole('button', { name: 'Open Workflow · engine' }));
      expect(doors.repository).toHaveBeenCalledWith('engine', 'setup');
      expect(doors.repository).toHaveBeenCalledWith('engine', 'workflow');
      await user.click(await screen.findByRole('button', { name: 'Open Integration · engine' }));
      expect(doors.repository).toHaveBeenCalledWith('engine', 'integration');
      await user.click(screen.getByRole('button', { name: 'Open plugin GitHub pull request' }));
      expect(doors.plugin).toHaveBeenCalledWith('github-pull-request');
    });

    it('says a held start in its sentence, a workflow that did not read and a rule that does not read, never as none', async () => {
      const user = await open();
      await user.click(await screen.findByRole('button', { name: 'Show default' }));
      await user.click(await screen.findByRole('button', { name: 'Show tools' }));
      const row = (await screen.findByRole('button', { name: 'Hide tools' })).closest('li')!;
      expect(row.textContent).toMatch(/No role takes `build` here, so its starts hold/);
      expect(row.textContent).toMatch(/its rule does not read: driver.json does not read: line 4/);
      expect(row.textContent).toMatch(/not driven/);
      await waitFor(() => expect(row.textContent).toMatch(/could not be read/));
    });

    it('keeps *Needs a look* and a workspace filter to the groups that hold what needs it', async () => {
      const user = await open();
      await screen.findByRole('button', { name: 'Show aurora' });
      await user.click(screen.getByRole('combobox', { name: 'Workspace' }));
      await user.click(await screen.findByRole('option', { name: 'default' }));
      expect(screen.queryByRole('button', { name: 'Show aurora' })).toBeNull();
      expect(screen.getByRole('button', { name: 'Show default' })).toBeTruthy();
    });
  });

  describe('the list', () => {
    it('marks By repository, where a start is held, and not Work, where none is', async () => {
      show(<Place />);
      const list = await screen.findByRole('list', { name: 'Roles' });
      await waitFor(() => expect(within(list).getByRole('button', { name: /^By repository/ }).querySelector('[data-held]')).not.toBeNull());
      expect(within(list).getByRole('button', { name: SECTION.Work }).querySelector('[data-held]')).toBeNull();
      expect(within(list).getByRole('button', { name: /^By repository/ })).toHaveTextContent('a start is held');
    });
  });

  describe("an agent's page", () => {
    it('says the roles that use the agent, from the structure, and a failed read in words', async () => {
      window.localStorage.setItem('daoris.list.agents.mode', 'agents');
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await screen.findByRole('button', { name: /^Claude Code/ }));
      await user.click(await screen.findByRole('button', { name: 'Show Used by' }));
      const used = await screen.findByRole('list', { name: 'Used by' });
      expect(within(used).getAllByRole('listitem').length).toBeGreaterThanOrEqual(3);
      expect(used.textContent).toMatch(/Build · building and design and decisions · choice 1/);
    });

    it('draws no section where the shell does not offer the read', async () => {
      structure = 'noroute';
      window.localStorage.setItem('daoris.list.agents.mode', 'agents');
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await screen.findByRole('button', { name: /^Claude Code/ }));
      await screen.findByRole('button', { name: /^Hide Ways in|^Show Ways in/ });
      await waitFor(() => expect(screen.queryByRole('button', { name: 'Show Used by' })).toBeNull());
    });

    it('says a read that failed, never an empty list', async () => {
      structure = 'failed';
      window.localStorage.setItem('daoris.list.agents.mode', 'agents');
      show(<Place />);
      const user = userEvent.setup();
      await user.click(await screen.findByRole('button', { name: /^Claude Code/ }));
      await user.click(await screen.findByRole('button', { name: 'Show Used by' }));
      expect(await screen.findByRole('alert')).toHaveTextContent('what uses it could not be read');
    });
  });
});
