import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// COMPANYVIEW1f (the company-view design §4, *Moving between them*): the Roles mode's diagram, one read and one hook with the list.
// The toggle keeps the selection and the filters; choosing a node of another kind moves the section, so *Show as list* lands on its
// row; a node's detail is its row's molecule and its *Edit* the same editor; a row's ⋯ offers *On the diagram*. A unit test's DOM
// measures nothing, so the diagram is its list here, the same nodes in the same order; the drawing is held by the measured check.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { CLAUDE_USE, CODEX_USE, DOORS } from '../agentsFixtures';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, WORKS } from './structureFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {}, roleDoors: {} });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const REGISTRATIONS = REGISTRY.map((row) => ({
  repository: row.repository, adopted: true, registered: true, owns: [], accepts: [], packs: [], entries: 1, ...(row.workspace ? { workspace: row.workspace } : {}),
}));

const STANDING = {
  offered: false, on: false, presses: [], built: [], unwritten: false, used: 0, held: [],
  appointmentsOffered: true, supervisorOffered: false, stewardOffered: true, stewardActsOffered: [], stewardActs: [], stewardDesk: [],
  appointments: [{
    role: 'steward', scope: 'machine', holder: 'terminal', since: '2026-10-31T08:00:00.000Z', dormant: true, answers: 0, holderId: null, acts: [], setup: false,
  }],
  holders: [],
};

const SECTION = { 'Working roles': /^Working roles/, Work: /^Work(?!ing)/, Accounts: /^Accounts/, 'By repository': /^By repository/, 'Appointed roles': /^Appointed roles/ } as const;
const sectionPress = (name: keyof typeof SECTION) => screen.findByRole('button', { name: SECTION[name] });
/** The list's chosen section, by its name and any line under it. */
const chosenSection = () => screen.getAllByRole('button', { current: true }).map((button) => button.textContent).join(' | ');
const node = (name: string) => screen.getByRole('listitem', { name });
const detail = () => within(screen.getByRole('complementary', { name: 'What is chosen' }));

async function diagramFrom(section: keyof typeof SECTION) {
  show(<Place />);
  const user = userEvent.setup();
  await user.click(await sectionPress(section));
  await user.click(await screen.findByRole('button', { name: 'Show as diagram' }));
  await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
  return user;
}

describe('The Roles mode, the diagram', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRATIONS) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      switch (type) {
        case 'STATE': return { ...DRIVER_STATE, drivable: DRIVER.drivable, holds: DRIVER.holds, standingConfirmation: STANDING };
        case 'HARNESSES': return { settingsPath: 'x', adapter: 'claude-code', harnesses: DOORS };
        case 'ACCOUNTS': return { agents: [CLAUDE_USE, CODEX_USE] };
        case 'ACTORS': return { actors: [] };
        case 'ROLES': return { roles: STRUCTURE.roles, works: WORKS, kept: true, suggested: [] };
        case 'STARTS': return { adapter: 'claude-code', starts: [] };
        case 'PLUGINS': return { folder: 'plugins', plugins: PLUGINS };
        case 'LINES': return LINES;
        case 'ROLE_STRUCTURE': return STRUCTURE;
        case 'WORKFLOW_CURRENT': return { repository: 'engine', workspace: 'aurora', startHolds: [], steps: [{}, {}], version: 'abc', limits: {}, start: null };
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

  it('offers Show as diagram in the head, and back Show as list, keeping the filters', async () => {
    show(<Place />);
    const user = userEvent.setup();
    await user.click(await sectionPress('Work'));
    await screen.findByRole('button', { name: 'Show building' });
    await user.type(screen.getByRole('searchbox', { name: 'Name' }), 'engine');
    await user.click(screen.getByRole('button', { name: 'Show as diagram' }));
    const drawn = await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
    // The filter holds on the diagram: the role it names, where it is placed, and who staffs it.
    expect(screen.getByRole('searchbox', { name: 'Name' })).toHaveValue('engine');
    const names = within(drawn).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'));
    expect(names).toEqual(['place:machine', 'place:aurora/engine', 'role:engine-build', 'agent:claude-code']);
    await user.click(screen.getByRole('button', { name: 'Show as list' }));
    expect(await screen.findByRole('searchbox', { name: 'Name' })).toHaveValue('engine');
    expect(screen.queryByRole('list', { name: 'The diagram: places, roles and agents' })).toBeNull();
  });

  it("says each node's edges in words, a choice that cannot run with its reading, and a place counted in its workspace", async () => {
    await diagramFrom('Work');
    const build = node('Build');
    expect(within(build).getByRole('button', { name: /^Codex, choice 2: cannot run: signed out · read/ })).toBeTruthy();
    expect(within(build).getByRole('button', { name: /^Claude Code, choice 1: can run/ })).toBeTruthy();
    expect(node('aurora').textContent).toContain("1 repository takes this machine's roles");
    // A reader hears every edge in: Claude Code is leaned on by each role naming it.
    const claude = node('Claude Code');
    expect(document.getElementById(claude.getAttribute('aria-describedby')!)!.textContent).toMatch(/From choice 1 of Build; .*as before of Chat/);
  });

  it('moves the section with a node of another kind, its detail is its row, and Show as list lands on that row', async () => {
    const user = await diagramFrom('Work');
    await user.click(node('Codex'));
    expect(chosenSection()).toContain('Accounts');
    expect(node('Codex')).toHaveAttribute('aria-current', 'true');
    expect(detail().getByRole('button', { name: 'Hide Codex' })).toBeTruthy();
    // Every role leaning on it is lit, and only those.
    expect(node('Build')).toHaveAttribute('data-lit');
    expect(node('Second opinion')).toHaveAttribute('data-lit');
    expect(node('Chat')).not.toHaveAttribute('data-lit');
    await user.click(screen.getByRole('button', { name: 'Show as list' }));
    expect(await screen.findByRole('heading', { name: 'Accounts' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Hide Codex' })).toBeTruthy();
  });

  it("lights a place's path, inherited where it takes this machine's roles, and its detail says each work's answer", async () => {
    const user = await diagramFrom('Accounts');
    await user.click(node('engine'));
    expect(chosenSection()).toContain('By repository');
    expect(node('Engine build')).toHaveAttribute('data-lit');
    expect(node('Chat')).toHaveAttribute('data-lit');
    expect(node('This Daoris')).toHaveAttribute('data-lit');
    expect(node('tools')).not.toHaveAttribute('data-lit');
    expect(detail().getByRole('button', { name: 'Hide engine' })).toBeTruthy();
    expect(detail().getByText(/Engine build \(in repository engine\)/)).toBeTruthy();
  });

  it("opens a role's detail on its catalogue row, whose Edit is the same editor the list holds", async () => {
    const user = await diagramFrom('Accounts');
    await user.click(node('Build'));
    expect(chosenSection()).toContain('Working roles');
    await user.click(detail().getByRole('button', { name: 'Edit Build' }));
    expect(detail().getByRole('form', { name: 'Build' })).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Show as list' }));
    // The list's catalogue holds the same editor, open.
    expect(await screen.findByRole('form', { name: 'Build' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Edit Build' })).toBeNull();
  });

  it("offers On the diagram in a row's ⋯, and chooses its node there", async () => {
    show(<Place />);
    const user = userEvent.setup();
    await user.click(await sectionPress('Accounts'));
    await user.click(await screen.findByRole('button', { name: 'More for Codex' }));
    await user.click(await screen.findByRole('menuitem', { name: 'On the diagram' }));
    await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
    expect(node('Codex')).toHaveAttribute('aria-current', 'true');
    expect(chosenSection()).toContain('Accounts');
  });

  it('selects the Appointed roles section with an appointed node, still on the diagram, its appointment dashed while dormant', async () => {
    const user = await diagramFrom('Work');
    const steward = node('Steward');
    expect(document.getElementById(steward.getAttribute('aria-describedby')!)!.textContent).toMatch(/appointed at This Daoris, dormant until appointed again/);
    await user.click(steward);
    expect(chosenSection()).toContain('Appointed roles');
    expect(screen.getByRole('list', { name: 'The diagram: places, roles and agents' })).toBeTruthy();
    expect(await detail().findByRole('group', { name: /^Steward · This Daoris/ })).toBeTruthy();
  });

  it('is spoken in Chinese', async () => {
    await i18n.changeLanguage('zh');
    show(<Place />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: new RegExp(`^${i18n.t('nav.roles.work')}$`) }));
    await user.click(await screen.findByRole('button', { name: '以角色图显示' }));
    expect(await screen.findByRole('list', { name: '角色图：位置、角色与智能体' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '以列表显示' })).toBeTruthy();
  });
});
