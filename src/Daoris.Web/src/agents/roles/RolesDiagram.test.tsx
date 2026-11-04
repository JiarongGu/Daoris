import { useEffect, useState } from 'react';
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
import { PLACES } from '../setup/setupFixtures';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, WORKS } from './structureFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  // The diagram is This machine's All roles section, as a door to it opens it.
  const { onPlaceAt } = modes;
  useEffect(() => {
    onPlaceAt('machine', { section: 'allRoles' });
  }, [onPlaceAt]);
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

/** The steward as the catalogue holds it: an appointed role not yet held by anyone. */
const STEWARD = {
  id: 'steward', state: 'seed', seeded: true, name: { en: 'Steward', zh: '管家' }, takes: [], scopeKind: 'machine', revision: 0,
  hold: { kind: 'steward', acts: [], holder: null },
};
const node = (name: string) => screen.getByRole('listitem', { name });
const detail = () => within(screen.getByRole('complementary', { name: 'What is chosen' }));

async function openDiagram() {
  show(<Place />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('radio', { name: 'Diagram' }));
  await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
  return user;
}

describe('The Roles mode, the diagram', () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRATIONS) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: object }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      switch (type) {
        case 'PLACES': return PLACES((request?.payload ?? {}) as Parameters<typeof PLACES>[0]);
        case 'STATE': return { ...DRIVER_STATE, drivable: DRIVER.drivable, holds: DRIVER.holds, standingConfirmation: STANDING };
        case 'HARNESSES': return { settingsPath: 'x', adapter: 'claude-code', harnesses: DOORS };
        case 'ACCOUNTS': return { agents: [CLAUDE_USE, CODEX_USE] };
        case 'ROLES': return { roles: [...STRUCTURE.roles, STEWARD], works: WORKS, kept: true, suggested: [] };
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

  it('offers the Diagram beside the List in All roles, remembers the choice, and back to the list', async () => {
    show(<Place />);
    const user = userEvent.setup();
    expect(await screen.findByRole('radio', { name: 'List' })).toBeChecked();
    await user.click(screen.getByRole('radio', { name: 'Diagram' }));
    const drawn = await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
    expect(window.localStorage.getItem('daoris.roles.view')).toBe('diagram');
    const names = within(drawn).getAllByRole('listitem').map((item) => item.getAttribute('data-flow-node'));
    expect(names).toEqual(expect.arrayContaining(['place:machine', 'place:aurora/engine', 'role:engine-build', 'agent:claude-code']));
    await user.click(screen.getByRole('radio', { name: 'List' }));
    expect(await screen.findByRole('button', { name: 'Edit Build' })).toBeTruthy();
    expect(screen.queryByRole('list', { name: 'The diagram: places, roles and agents' })).toBeNull();
    expect(window.localStorage.getItem('daoris.roles.view')).toBeNull();
  });

  it("says each node's edges in words, a choice that cannot run with its reading, and a place counted in its workspace", async () => {
    await openDiagram();
    const build = node('Build');
    expect(within(build).getByRole('button', { name: /^Codex, choice 2: cannot run: signed out · read/ })).toBeTruthy();
    expect(within(build).getByRole('button', { name: /^Claude Code, choice 1: can run/ })).toBeTruthy();
    expect(node('aurora').textContent).toContain("1 repository takes this machine's roles");
    // A reader hears every edge in: Claude Code is leaned on by each role naming it.
    const claude = node('Claude Code');
    expect(document.getElementById(claude.getAttribute('aria-describedby')!)!.textContent).toMatch(/From choice 1 of Build; .*as before of Chat/);
  });

  it('chooses a node of another kind, its detail is its row, and the roles leaning on it are lit', async () => {
    const user = await openDiagram();
    await user.click(node('Codex'));
    expect(node('Codex')).toHaveAttribute('aria-current', 'true');
    expect(detail().getByRole('button', { name: 'Hide Codex' })).toBeTruthy();
    // Every role leaning on it is lit, and only those.
    expect(node('Build')).toHaveAttribute('data-lit');
    expect(node('Second opinion')).toHaveAttribute('data-lit');
    expect(node('Chat')).not.toHaveAttribute('data-lit');
  });

  it("lights a place's path, inherited where it takes this machine's roles, and its detail says each work's answer", async () => {
    const user = await openDiagram();
    await user.click(node('engine'));
    expect(node('Engine build')).toHaveAttribute('data-lit');
    expect(node('Chat')).toHaveAttribute('data-lit');
    expect(node('This Daoris')).toHaveAttribute('data-lit');
    expect(node('tools')).not.toHaveAttribute('data-lit');
    expect(detail().getByRole('button', { name: 'Hide engine' })).toBeTruthy();
    expect(detail().getByText(/Engine build \(in repository engine\)/)).toBeTruthy();
  });

  it("opens a role's detail on its catalogue row, whose Edit is the same editor the list holds", async () => {
    const user = await openDiagram();
    await user.click(node('Build'));
    await user.click(detail().getByRole('button', { name: 'Edit Build' }));
    expect(detail().getByRole('form', { name: 'Build' })).toBeTruthy();
    await user.click(screen.getByRole('radio', { name: 'List' }));
    // The list's catalogue holds the same editor, open.
    expect(await screen.findByRole('form', { name: 'Build' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Edit Build' })).toBeNull();
  });

  it("offers On the diagram in a row's ⋯, and chooses its node there", async () => {
    show(<Place />);
    const user = userEvent.setup();
    await screen.findByRole('button', { name: 'Edit Build' });
    (await screen.findByRole('button', { name: 'More for Build' })).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'On the diagram' }));
    await screen.findByRole('list', { name: 'The diagram: places, roles and agents' });
    expect(node('Build')).toHaveAttribute('aria-current', 'true');
  });

  it('chooses an appointed node, still on the diagram, its appointment dashed while dormant', async () => {
    const user = await openDiagram();
    const steward = node('Steward');
    expect(document.getElementById(steward.getAttribute('aria-describedby')!)!.textContent).toMatch(/appointed at This Daoris, dormant until appointed again/);
    await user.click(steward);
    expect(screen.getByRole('list', { name: 'The diagram: places, roles and agents' })).toBeTruthy();
    expect(steward).toHaveAttribute('aria-current', 'true');
  });

  it('is spoken in Chinese', async () => {
    await i18n.changeLanguage('zh');
    show(<Place />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('radio', { name: i18n.t('agents.setup.allRoles.diagram') }));
    expect(await screen.findByRole('list', { name: '角色图：位置、角色与智能体' })).toBeTruthy();
    expect(screen.getByRole('radio', { name: i18n.t('agents.setup.allRoles.list') })).toBeTruthy();
  });
});
