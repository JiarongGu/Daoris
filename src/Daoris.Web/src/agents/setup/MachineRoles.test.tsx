import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// SETUPUX1m (the setup-UX design §2.4, This machine's page): lit, the page holds All roles, the diagram and the Suggested line, so lighting
// the flag loses none of what the dark Roles mode shows.

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

import type { RoleCatalogue, StructurePlaceAsk } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { STRUCTURE } from '../roles/structureFixtures';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { CATALOGUE, CHAIN, PLACES } from './setupFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {} });
  return (
    <>
      {/* What the application's opener does with a lit door to Appointed roles (`placeOpening`, then `apply`). */}
      <button type="button" onClick={() => modes.onPlaceAt('machine', { section: 'roles', row: 'appointed' })}>Door to Appointed</button>
      <button type="button" onClick={() => modes.onPlaceAt('machine', { section: 'allRoles' })}>Door to All roles</button>
      <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />
    </>
  );
}

const ROSTER = {
  settingsPath: 'x', adapter: 'claude-code',
  harnesses: [{
    harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe', present: true, version: 'claude 9.9.9', problem: null,
    machineDefault: null, pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
  }],
};

const SUGGESTING: RoleCatalogue = {
  ...CATALOGUE,
  suggestedChoicesDated: '2026-10-31',
  suggestedChoices: [{ role: 'build', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }] }],
};

const asks = (type: string): Record<string, unknown>[] => invoke.mock.calls
  .filter(([, called]) => called === type)
  .map(([, , options]) => (options as { payload: Record<string, unknown> }).payload);

describe('This machine\'s page, lit', () => {
  let roles: RoleCatalogue = CATALOGUE;

  beforeEach(() => {
    roles = CATALOGUE;
    window.localStorage.clear();
    window.localStorage.setItem('daoris.agents.setup', 'on');
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    // The registry names the workspace each repository sits in, which a repository's place id is made from.
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => (String(input).startsWith('/api/registry')
      ? Response.json([{ repository: 'engine', workspace: 'aurora', adopted: true, registered: true, summary: '', owns: [], accepts: [], packs: [], entries: 1 }])
      : respond(String(input)))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { chain?: StructurePlaceAsk } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE' || type === 'SET_ROLE' || type === 'SET_ROLES') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'PLACES') return PLACES((options?.payload ?? {}) as Parameters<typeof PLACES>[0]);
      if (type === 'ROLES') return roles;
      // A chain is asked of the Roles section; the whole structure, with no chain, of the diagram.
      if (type === 'ROLE_STRUCTURE') return options?.payload?.chain ? CHAIN(options.payload.chain) : STRUCTURE;
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  const openMachine = async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /This machine/ }));
    return screen.findByRole('heading', { level: 1, name: 'This machine' });
  };
  const openAllRoles = async () => {
    await openMachine();
    await userEvent.click(await screen.findByRole('button', { name: 'All roles' }));
  };

  it('draws Roles open and All roles folded to the line that says what is in it, and reads the whole catalogue only once opened', async () => {
    await openMachine();
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'true');
    const all = screen.getByRole('button', { name: 'All roles' });
    expect(all).toHaveAttribute('aria-expanded', 'false');
    expect(all).toHaveAccessibleDescription('Every role once, by where it is placed · as a list or a diagram');
    expect(screen.queryByRole('searchbox', { name: 'Search roles' })).toBeNull();
    expect(asks('ROLE_STRUCTURE').every((each) => each.chain !== undefined)).toBe(true);
  });

  it('lists every role once, the ones placed here apart from the ones placed elsewhere, and opens an elsewhere role at its place', async () => {
    await openAllRoles();
    const machineWide = within(await screen.findByRole('region', { name: 'Placed machine-wide' }));
    expect(machineWide.getByText('Build')).toBeTruthy();
    expect(machineWide.getByText('Chat')).toBeTruthy();
    const elsewhere = within(screen.getByRole('region', { name: 'Placed elsewhere' }));
    expect(elsewhere.getByText('Engine build')).toBeTruthy();
    expect(screen.getAllByText('Engine build')).toHaveLength(1);
    // A role placed elsewhere is read here: no Edit, and a door to its place.
    expect(elsewhere.queryByRole('button', { name: /^Edit/ })).toBeNull();
    await userEvent.click(elsewhere.getByRole('button', { name: 'Open Engine build at engine' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(screen.getByRole('button', { name: '← Back to This machine · All roles' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'This machine' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'All roles' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('narrows by where a role is placed and by a search', async () => {
    await openAllRoles();
    await screen.findByText('Engine build');
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search roles' }), 'engi');
    await waitFor(() => expect(screen.queryByText('Chat')).toBeNull());
    expect(screen.getByText('Engine build')).toBeTruthy();
    await userEvent.clear(screen.getByRole('searchbox', { name: 'Search roles' }));
    await userEvent.type(screen.getByRole('searchbox', { name: 'Search roles' }), 'zzz');
    expect(await screen.findByText('No role matches that.')).toBeTruthy();
    await userEvent.clear(screen.getByRole('searchbox', { name: 'Search roles' }));
    screen.getByRole('combobox', { name: 'Roles placed' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'A repository' }));
    await waitFor(() => expect(screen.queryByText('Chat')).toBeNull());
    expect(screen.getByText('Engine build')).toBeTruthy();
  });

  it('edits a role placed on this machine in place, over SET_ROLE', async () => {
    await openAllRoles();
    const machineWide = within(await screen.findByRole('region', { name: 'Placed machine-wide' }));
    await userEvent.click(machineWide.getByRole('button', { name: 'Edit Build' }));
    const editor = await screen.findByRole('form', { name: 'Build' });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(asks('SET_ROLE')).toEqual([expect.objectContaining({ change: 'set', id: 'build', revision: 0 })]));
  });

  it('draws the roles as a diagram, asking the whole structure only then, and back as a list', async () => {
    await openAllRoles();
    await screen.findByText('Engine build');
    expect(asks('ROLE_STRUCTURE').every((each) => each.chain !== undefined)).toBe(true);
    await userEvent.click(screen.getByRole('radio', { name: 'Diagram' }));
    expect(await screen.findByRole('list', { name: 'The diagram: places, roles and agents' })).toBeTruthy();
    expect(asks('ROLE_STRUCTURE').some((each) => each.chain === undefined)).toBe(true);
    await userEvent.click(screen.getByRole('radio', { name: 'List' }));
    expect(await screen.findByRole('region', { name: 'Placed machine-wide' })).toBeTruthy();
  });

  it('says what is suggested above the sections, and shows it under All roles', async () => {
    roles = SUGGESTING;
    await openMachine();
    const notes = within(await screen.findByRole('list', { name: 'Staffing to look at' }));
    expect(notes.getByText('Daoris suggests choices for 1 role')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Use these choices' })).toBeNull();

    await userEvent.click(notes.getByRole('button', { name: 'Show: Daoris suggests choices for 1 role' }));
    expect(screen.getByRole('button', { name: 'All roles' })).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(await screen.findByRole('button', { name: 'Use these choices' }));
    await waitFor(() => expect(asks('SET_ROLES')).toHaveLength(1));
  });

  it('opens at the Appointed row of Roles, or at All roles, for the doors that opened the Roles mode', async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: 'Door to Appointed' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'This machine' })).toBeTruthy();
    await waitFor(() => expect(document.activeElement).toHaveAttribute('data-place-row', 'appointed'));
    expect(screen.getByRole('button', { name: 'Roles' })).toHaveAttribute('aria-expanded', 'true');

    await userEvent.click(screen.getByRole('button', { name: 'Door to All roles' }));
    expect(screen.getByRole('button', { name: 'All roles' })).toHaveAttribute('aria-expanded', 'true');
    expect(await screen.findByRole('searchbox', { name: 'Search roles' })).toBeTruthy();
  });

  it('says nothing above the sections where nothing is suggested', async () => {
    await openMachine();
    await screen.findByRole('button', { name: 'All roles' });
    expect(screen.queryByRole('list', { name: 'Staffing to look at' })).toBeNull();
  });

  it('is named in Chinese', async () => {
    i18n.changeLanguage('zh');
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /^本机$/ }));
    const all = await screen.findByRole('button', { name: '全部角色' });
    expect(all).toHaveAccessibleDescription('每个角色一次，按所在位置 · 列表或角色图');
    await userEvent.click(all);
    expect(await screen.findByRole('searchbox', { name: '搜索角色' })).toBeTruthy();
    expect(screen.getByRole('radio', { name: '角色图' })).toBeTruthy();
  });
});
