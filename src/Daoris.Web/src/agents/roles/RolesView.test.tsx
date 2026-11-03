import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// COMPANYVIEW1b (the company-view design §1, §3, §3.1): the Agents place's Roles mode, beside Agents, switched by the list's head. Its
// five sections, Working roles the one built, and the working roles' presses reaching their one door (`SET_ROLE`, ONEWAY1a) from here.
// The roles' tests moved from AI features' suite with the card they test (`settings/AiDomain.test.tsx` until COMPANYVIEW1b).

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
import { useAgentsMode, useAgentsView } from '../AgentsView';

/** The place as the application holds it on a desktop: both modes, the list's memory its own. */
function Place({ notify = () => {} }: { notify?: (message: string, tone?: 'ok' | 'error') => void }) {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const ROSTER = {
  settingsPath: 'C:/somewhere/.daoris/harnesses.json',
  adapter: 'claude-code',
  harnesses: [
    {
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe',
      present: true, version: 'claude 9.9.9', problem: null, machineDefault: null,
      pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
    },
    {
      harness: 'claude-code-acp', product: 'Claude Code', maker: 'Anthropic', wire: 'acp', accountOf: 'claude-code',
      present: true, version: '0.9.1', problem: null, machineDefault: null,
      pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
    },
  ],
};

const before = (id: string, agent?: string) => ({
  id, state: agent || id === 'opinion' || id === 'partner' || id === 'helper' ? 'before' : 'off',
  choices: agent ? [{ agent, from: 'before' }] : [],
});
const STAFFING = [
  before('build', 'claude-code'), before('intake'), before('opinion'), before('helper'), before('partner'),
  before('brainstorm', 'claude-code'), before('ask-daoris'), before('chat', 'claude-code'),
];

/** The catalogue with each row carrying its staffing, as `ROLES` answers it. */
const staffed = <T extends { roles: { id: string }[] }>(catalogue: T, staffing: readonly { id: string }[] = STAFFING) => ({
  ...catalogue, roles: catalogue.roles.map((row) => ({ ...row, staffing: staffing.find((each) => each.id === row.id) ?? null })),
});

const ROLES = {
  roles: [{ id: 'build', state: 'seed', seeded: true, name: { en: 'Build', zh: '开发' }, takes: ['build'], scopeKind: 'machine', revision: 0 }],
  works: ['build', 'design'],
  kept: true,
  suggested: [{ id: 'designer', name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

/** A seeded role as `ROLES` answers it with no record of its own: named by its actor's words. */
const seeded = (id: string) => ({ id, state: 'seed', seeded: true, takes: [id], scopeKind: 'machine', revision: 0 });

/** The catalogue with the seeded roles whose editors these tests open. */
const SEEDED = { ...ROLES, roles: ['build', 'intake', 'ask-daoris', 'chat'].map(seeded), works: ['build', 'intake', 'ask-daoris', 'chat'], suggested: [] };

/** Open the Roles mode from the list's head. */
const openRoles = async () => userEvent.click(await screen.findByRole('radio', { name: 'Setup' }));

describe('The Agents place, Roles mode', () => {
  let refuseRole = false;
  let catalogue: 'roles' | 'seeded' | 'none' = 'roles';

  beforeEach(() => {
    refuseRole = false;
    catalogue = 'roles';
    window.localStorage.clear();
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ROLE' && refuseRole) throw new Error('`build` may not be off: a role must take it on the machine.');
      if (type === 'STATE' || type === 'SET_ROLE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'ROLES') return catalogue === 'roles' ? staffed(ROLES) : catalogue === 'seeded' ? staffed(SEEDED) : undefined;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [] };
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('switches from the list\'s head to Roles and back, remembering the mode for the place', async () => {
    show(<Place />);
    const user = userEvent.setup();
    expect(await screen.findByRole('radio', { name: 'Agents' })).toBeChecked();
    // The Agents mode is as it was: the list of agents.
    expect(await screen.findByRole('list', { name: 'Agents' })).toBeTruthy();

    await openRoles();
    expect(screen.getByRole('radio', { name: 'Setup' })).toBeChecked();
    expect(await screen.findByRole('list', { name: 'Roles' })).toBeTruthy();
    expect(window.localStorage.getItem('daoris.list.agents.mode')).toBe('setup');

    await user.click(screen.getByRole('radio', { name: 'Agents' }));
    expect(await screen.findByRole('list', { name: 'Agents' })).toBeTruthy();
    expect(window.localStorage.getItem('daoris.list.agents.mode')).toBeNull();
  });

  it('opens in the mode it was left in', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    show(<Place />);
    expect(await screen.findByRole('radio', { name: 'Setup' })).toBeChecked();
  });

  it('lists the five sections, every one built', async () => {
    show(<Place />);
    await openRoles();
    const list = await screen.findByRole('list', { name: 'Roles' });
    const names = within(list).getAllByRole('button').map((button) => button.querySelector('span')?.textContent);
    expect(names).toEqual(['Working roles', 'Appointed roles', 'Work', 'Accounts', 'By repository']);
    expect(within(list).queryAllByText('Comes later.')).toHaveLength(0);
    // It opens on Working roles: the card of its roles stands in the main area.
    expect(await screen.findByRole('heading', { name: 'Working roles' })).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Edit Build' })).toBeTruthy();
  });

  it('opens another section in place of Working roles, and remembers the section last left', async () => {
    show(<Place />);
    const user = userEvent.setup();
    await openRoles();
    await user.click(await screen.findByRole('button', { name: /^Appointed roles/ }));
    expect(await screen.findByRole('heading', { name: 'Appointed roles' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Edit Build' })).toBeNull();
    expect(window.localStorage.getItem('daoris.list.roles.chosen')).toBe('appointed');

    cleanup();
    show(<Place />);
    // A new window opens on the section left.
    expect(await screen.findByRole('heading', { name: 'Appointed roles' })).toBeTruthy();
    expect(screen.getByRole('button', { name: /^Appointed roles/ })).toHaveAttribute('aria-current', 'true');
  });

  it('draws no role and edits none on a shell with no catalogue, since each order is its role row\'s', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'none';
    show(<Place />);

    expect(await screen.findByText(/Not offered by this shell/)).toBeTruthy();
    expect(screen.queryByText('Actors')).toBeNull();
    expect(screen.queryAllByText('claude-code · account default · account default · as before')).toHaveLength(0);
    expect(screen.queryAllByRole('button', { name: /^Edit / })).toHaveLength(0);
  });

  it("reads each order from the role read alone, its row's staffing", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'seeded';
    show(<Place />);

    expect((await screen.findAllByText('claude-code · account default · account default · as before')).length).toBeGreaterThan(0);
    expect(invoke.mock.calls.map((call) => call[1])).not.toContain('ACTORS');
  });

  it("ONEWAY1a: saves Build's order over SET_ROLE, one choice's model named", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'seeded';
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    const editor = screen.getByRole('form', { name: 'Build' });
    expect(within(editor).getByRole('note', { name: /Running work never moves to another choice\./ })).toBeTruthy();
    await user.type(within(editor).getByRole('textbox', { name: "choice 1's model" }), 'opus');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', {
      payload: {
        change: 'set', id: 'build', revision: 0,
        role: expect.objectContaining({ takes: ['build'], scope: { kind: 'machine', id: null } }),
        choices: [{ agent: 'claude-code', model: 'opus', effort: null }],
      },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(i18n.t('settings.ai.roles.saved', { role: 'Build' })));
    await waitFor(() => expect(screen.queryByRole('form', { name: 'Build' })).toBeNull());
  });

  it('says a refusal under its Save and keeps the editor open as it was (R3)', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'seeded';
    refuseRole = true;
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    const editor = screen.getByRole('form', { name: 'Build' });
    await user.type(within(editor).getByRole('textbox', { name: "choice 1's model" }), 'opus');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent('may not be off');
    expect(within(editor).getByRole('textbox', { name: "choice 1's model" })).toHaveValue('opus');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('saved'));
  });

  /** The editor is the mode's, held above the card (D168 point 3): a refresh of the roles read does not close it. */
  it('keeps an open editor while the roles are read again', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'seeded';
    show(<Place />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    await user.type(screen.getByRole('textbox', { name: "choice 1's model" }), 'opus');
    expect(screen.getByRole('textbox', { name: "choice 1's model" })).toHaveValue('opus');
    expect(screen.getByRole('form', { name: 'Build' })).toBeTruthy();
  });

  /** Chat's and Ask Daoris's editors say what a running conversation does; every other role's says it never moves. */
  it.each(['en', 'zh'])("says in Chat's and Ask Daoris's editors that a conversation goes on with the next choice, in %s", async (language) => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    catalogue = 'seeded';
    await i18n.changeLanguage(language);
    show(<Place />);
    const user = userEvent.setup();
    await screen.findAllByRole('button', { name: i18n.t('settings.ai.roles.editOf', { role: i18n.t('settings.ai.role.name.build') }) });

    for (const [id, words] of [['ask-daoris', 'goesOn'], ['chat', 'goesOn'], ['intake', 'neverMoves']] as const) {
      await user.click(screen.getByRole('button', { name: i18n.t('settings.ai.roles.editOf', { role: i18n.t(`settings.ai.role.name.${id}`) }) }));
      const editor = screen.getByRole('form', { name: i18n.t(`settings.ai.role.name.${id}`) });
      expect(within(editor).getByRole('note', { name: new RegExp(i18n.t(`settings.ai.role.${words}`).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')) })).toBeTruthy();
      await user.click(within(editor).getByRole('button', { name: i18n.t('common.cancel') }));
    }
    await i18n.changeLanguage('en');
  });

  it('adds the suggested Designer over SET_ROLE and says so', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Add the Designer' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', {
      payload: {
        change: 'set', id: 'designer', revision: 0,
        role: { name: { en: 'Designer', zh: '设计师' }, takes: ['design'], scope: { kind: 'machine', id: null } },
        choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }],
      },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Designer is saved; new work takes it.'));
  });

  // AGENTSPLACE1g2: *Use these choices* is one press over SET_ROLES, said once; a refusal is the driver's sentence and every role is refused.
  describe.each([[false], [true]])('the suggested choices pressed, refused %s', (refused) => {
    it('goes out as one SET_ROLES and says so, or says the driver\'s sentence', async () => {
      window.localStorage.setItem('daoris.list.agents.mode', 'setup');
      const answer = invoke.getMockImplementation()!;
      invoke.mockImplementation(async (module: string, type: string, request?: unknown) => {
        if (type === 'ROLES') {
          return {
            ...ROLES, suggested: [], suggestedChoicesDated: '2026-10-31',
            suggestedChoices: [{ role: 'build', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }] }],
          };
        }
        if (type === 'SET_ROLES' && refused) throw new Error('`build` changed since it was read, so nothing was written; read it again.');
        if (type === 'SET_ROLES') return DRIVER_STATE;
        return answer(module, type, request);
      });
      const notify = vi.fn();
      show(<Place notify={notify} />);
      const user = userEvent.setup();

      await user.click(await screen.findByRole('button', { name: 'Use these choices' }));

      expect(invoke.mock.calls.filter(([, type]) => type === 'SET_ROLES' || type === 'SET_ROLE').map(([, type, options]) => [type, options])).toEqual([
        ['SET_ROLES', {
          payload: {
            changes: [{
              change: 'set', id: 'build', revision: 0,
              role: { name: { en: 'Build', zh: '开发' }, takes: ['build'], scope: { kind: 'machine', id: null } },
              choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }],
            }],
          },
        }],
      ]);
      if (refused) {
        expect(await screen.findByText(/nothing was written; read it again/)).toBeTruthy();
        expect(notify).not.toHaveBeenCalled();
      } else await waitFor(() => expect(notify).toHaveBeenCalledWith('Saved together: Build; new work takes them.'));
    });
  });

  it('tells a failed read of the roles from a shell that offers none', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    const answer = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: unknown) => {
      if (type === 'ROLES') throw new Error('the store did not read');
      return answer(module, type, request);
    });
    show(<Place />);

    expect(await screen.findByText(/could not be read/)).toBeTruthy();
    expect(screen.queryByText(/Not offered by this shell/)).toBeNull();
  });

  it("says the driver's refusal of a removal under the role's row", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    refuseRole = true;
    show(<Place />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Remove Build' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', expect.anything());
    await user.click(await screen.findByRole('button', { name: 'Remove role' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', { payload: { change: 'remove', id: 'build', revision: 0 } });
    expect(await screen.findByText(/a role must take it on the machine/)).toBeTruthy();
  });

  /** Ask Daoris's row says what it runs on and how, under its order (it said so beside its access on AI features before COMPANYVIEW1b). */
  it("says under Ask Daoris's row what it runs on, and with full access where the driver says so", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    const answer = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: unknown) => {
      if (module === 'DAORIS.DRIVER' && type === 'STATE') return { ...DRIVER_STATE, helpMode: 'full' };
      if (module === 'DAORIS.DRIVER' && type === 'ROLES') {
        const staffing = { id: 'ask-daoris', state: 'set', choices: [{ agent: 'claude-code-acp', from: '1' }] };
        return { ...ROLES, roles: [{ id: 'ask-daoris', state: 'seed', seeded: true, name: { en: 'Ask Daoris', zh: '问道衍' }, takes: ['ask-daoris'], scopeKind: 'machine', revision: 0, staffing }] };
      }
      return answer(module, type, request);
    });
    show(<Place />);

    expect(await screen.findByText(/runs on claude-code-acp with full access/)).toBeTruthy();
  });

  it('asks the driver nothing of roles while the Agents mode is in front', async () => {
    show(<Place />);
    await screen.findByRole('list', { name: 'Agents' });
    const asked = invoke.mock.calls.map((call) => call[1]);
    expect(asked).not.toContain('ROLES');
  });
});

/** Intake's row (MODELROLE1d, D205 §7): its order set in its editor over SET_ROLE (ONEWAY1a), off in its own words, with what an intake runs as. */
describe('Intake in the Roles mode', () => {
  const start = (job: 'work' | 'intake', adapter: string) => ({
    job, workspace: 'default', adapter, owner: 'claude-code', product: 'Claude Code',
    profile: 'personal', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
    commanded: false, refusal: null,
  });
  let intake: { agent: string }[] = [];
  const actors = () => ({
    actors: [
      { id: 'build', state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] },
      { id: 'intake', state: intake.length > 0 ? 'set' : 'off', choices: intake.map((choice, i) => ({ ...choice, from: String(i + 1) })) },
      { id: 'ask-daoris', state: 'off', choices: [] },
      { id: 'chat', state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] },
    ],
  });
  const rowOf = async (name: string) => (await screen.findByText(name, { selector: 'span' })).closest<HTMLElement>('[data-setting-row]')!;

  beforeEach(() => {
    intake = [];
    window.localStorage.clear();
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { id?: string; choices?: { agent: string }[] | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ROLE' && request?.payload?.id === 'intake') intake = request.payload.choices ?? [];
      if (type === 'STATE' || type === 'SET_ROLE') return { ...DRIVER_STATE, intakeAdapter: intake[0]?.agent ?? '' };
      if (type === 'ROLES') return staffed(SEEDED, actors().actors);
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') {
        return { adapter: 'claude-code', starts: [start('work', 'claude-code'), ...(intake[0] ? [start('intake', intake[0].agent)] : [])] };
      }
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('says an Intake that is off in its own words, on its row, with what that does', async () => {
    show(<Place />);

    const row = await rowOf('Intake');
    expect(within(row).getByText(/Off — declarations only/)).toBeTruthy();
    expect(within(row).getByText(/answered from the workspace's declarations/)).toBeTruthy();
  });

  it("ONEWAY1a: sets Intake's order over SET_ROLE", async () => {
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit Intake' }));
    const editor = screen.getByRole('form', { name: 'Intake' });
    expect(within(editor).getByRole('note', { name: /Running work never moves to another choice\./ })).toBeTruthy();
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', {
      payload: expect.objectContaining({ change: 'set', id: 'intake', choices: [{ agent: 'claude-code', model: null, effort: null }] }),
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(i18n.t('settings.ai.roles.saved', { role: 'Intake' })));
  });

  it("says which account an intake runs as, from the driver's answer, under Intake's row", async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<Place />);

    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
  });

  it('ONEWAY1a: turns Intake off as the terminal does, by the role door: an empty order', async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<Place />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit Intake' }));
    await user.click(within(screen.getByRole('form', { name: 'Intake' })).getByRole('button', { name: 'Off' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', { payload: expect.objectContaining({ change: 'set', id: 'intake', choices: [] }) });
  });

  /** A shell that answers no actors has never heard of them: no row, not a disabled one. */
  it('offers no Intake row on a shell older than the actors', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [start('work', 'claude-code')] };
      return undefined;
    });
    show(<Place />);

    await screen.findByRole('heading', { name: 'Working roles' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ROLES', expect.anything()));
    expect(screen.queryByText('Intake')).toBeNull();
  });
});
