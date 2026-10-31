import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// COMPANYVIEW1b (the company-view design §1, §3, §3.1): the Agents place's Roles mode, beside Agents, switched by the list's head. Its
// five sections, Working roles the one built, and the working roles' presses reaching their doors (`SET_ROLE`, `SET_ACTOR`) from here.
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
const ACTORS = {
  actors: [
    before('build', 'claude-code'), before('intake'), before('opinion'), before('helper'), before('partner'),
    before('brainstorm', 'claude-code'), before('ask-daoris'), before('chat', 'claude-code'),
  ],
};

const ROLES = {
  roles: [{ id: 'build', state: 'seed', seeded: true, nameEn: 'Build', nameZh: '开发', takes: ['build'], scopeKind: 'machine', revision: 0 }],
  works: ['build', 'design'],
  kept: true,
  suggested: [{ id: 'designer', nameEn: 'Designer', nameZh: '设计师', takes: ['design'], scopeKind: 'machine', choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }] }],
};

/** Open the Roles mode from the list's head. */
const openRoles = async () => userEvent.click(await screen.findByRole('radio', { name: 'Roles' }));

describe('The Agents place, Roles mode', () => {
  let refuseActor = false;
  let refuseRole = false;
  let catalogue = true;

  beforeEach(() => {
    refuseActor = false;
    refuseRole = false;
    catalogue = true;
    window.localStorage.clear();
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ACTOR' && refuseActor) throw new Error('build takes at most 4 choices, not 5.');
      if (type === 'SET_ROLE' && refuseRole) throw new Error('`build` may not be off: a role must take it on the machine.');
      if (type === 'STATE' || type === 'SET_ACTOR' || type === 'SET_ROLE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'ACTORS') return ACTORS;
      if (type === 'ROLES') return catalogue ? ROLES : undefined;
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
    expect(screen.getByRole('radio', { name: 'Roles' })).toBeChecked();
    expect(await screen.findByRole('list', { name: 'Roles' })).toBeTruthy();
    expect(window.localStorage.getItem('daoris.list.agents.mode')).toBe('roles');

    await user.click(screen.getByRole('radio', { name: 'Agents' }));
    expect(await screen.findByRole('list', { name: 'Agents' })).toBeTruthy();
    expect(window.localStorage.getItem('daoris.list.agents.mode')).toBeNull();
  });

  it('opens in the mode it was left in', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    show(<Place />);
    expect(await screen.findByRole('radio', { name: 'Roles' })).toBeChecked();
  });

  it('lists the five sections, Working roles built and the other four saying they come later', async () => {
    show(<Place />);
    await openRoles();
    const list = await screen.findByRole('list', { name: 'Roles' });
    const names = within(list).getAllByRole('button').map((button) => button.querySelector('span')?.textContent);
    expect(names).toEqual(['Working roles', 'Appointed roles', 'Work', 'Accounts', 'By repository']);
    expect(within(list).getAllByText('Comes later.')).toHaveLength(4);
    // It opens on Working roles: the card of its roles stands in the main area.
    expect(await screen.findByRole('heading', { name: 'Working roles' })).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Edit Build' })).toBeTruthy();
  });

  it('shows a section not built as its name and one quiet line, and remembers the section last left', async () => {
    show(<Place />);
    const user = userEvent.setup();
    await openRoles();
    await user.click(await screen.findByRole('button', { name: /^Accounts/ }));
    expect(await screen.findByRole('heading', { name: 'Accounts' })).toBeTruthy();
    expect(screen.getByText('This section is not built yet.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Edit Build' })).toBeNull();
    expect(window.localStorage.getItem('daoris.list.roles.chosen')).toBe('accounts');

    cleanup();
    show(<Place />);
    // A new window opens on the section left.
    expect(await screen.findByRole('heading', { name: 'Accounts' })).toBeTruthy();
    expect(screen.getByRole('button', { name: /^Accounts/ })).toHaveAttribute('aria-current', 'true');
  });

  it('folds each role to one line, as before where it reads today\'s settings', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    catalogue = false;
    show(<Place />);

    expect(await screen.findByText(/Not offered by this shell/)).toBeTruthy();
    expect(screen.queryByText('Actors')).toBeNull();
    expect(screen.getAllByText('claude-code · account default · account default · as before').length).toBe(2);
    expect(screen.getAllByRole('button', { name: /^Edit / })).toHaveLength(4);
    expect(screen.getByText('Off — declarations only')).toBeTruthy();
    expect(screen.getByText('Off — starters only')).toBeTruthy();
  });

  it("saves Build's order over SET_ACTOR, one choice's model named", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    catalogue = false;
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    const editor = screen.getByRole('form', { name: 'Build: your order' });
    expect(within(editor).getByText('Running work never moves to another choice.')).toBeTruthy();
    await user.type(within(editor).getByRole('textbox', { name: "choice 1's model" }), 'opus');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACTOR', {
      payload: { actor: 'build', choices: [{ agent: 'claude-code', model: 'opus', effort: null }] },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Build's order is saved; new work takes it."));
    await waitFor(() => expect(screen.queryByRole('form', { name: 'Build: your order' })).toBeNull());
  });

  it('says a refusal under its Save and keeps the editor open as it was (R3)', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    catalogue = false;
    refuseActor = true;
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    const editor = screen.getByRole('form', { name: 'Build: your order' });
    await user.type(within(editor).getByRole('textbox', { name: "choice 1's model" }), 'opus');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent('at most 4 choices');
    expect(within(editor).getByRole('textbox', { name: "choice 1's model" })).toHaveValue('opus');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('saved'));
  });

  /** The editor is the mode's, held above the card (D168 point 3): a refresh of the roles read does not close it. */
  it('keeps an open editor while the roles are read again', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    catalogue = false;
    show(<Place />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Edit Build' }));
    await user.type(screen.getByRole('textbox', { name: "choice 1's model" }), 'opus');
    expect(screen.getByRole('textbox', { name: "choice 1's model" })).toHaveValue('opus');
    expect(screen.getByRole('form', { name: 'Build: your order' })).toBeTruthy();
  });

  /** Chat's and Ask Daoris's editors say what a running conversation does; every other role's says it never moves. */
  it.each(['en', 'zh'])("says in Chat's and Ask Daoris's editors that a conversation goes on with the next choice, in %s", async (language) => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    catalogue = false;
    await i18n.changeLanguage(language);
    show(<Place />);
    const user = userEvent.setup();
    await screen.findAllByRole('button', { name: i18n.t('settings.ai.roles.editOf', { role: i18n.t('settings.ai.actors.name.build') }) });

    for (const [id, words] of [['ask-daoris', 'goesOn'], ['chat', 'goesOn'], ['intake', 'neverMoves']] as const) {
      await user.click(screen.getByRole('button', { name: i18n.t('settings.ai.roles.editOf', { role: i18n.t(`settings.ai.actors.name.${id}`) }) }));
      const editor = screen.getByRole('form', {
        name: i18n.t('settings.ai.actors.editorTitle', { actor: i18n.t(`settings.ai.actors.name.${id}`) }),
      });
      expect(within(editor).getByText(i18n.t(`settings.ai.actors.${words}`))).toBeTruthy();
      await user.click(within(editor).getByRole('button', { name: i18n.t('common.cancel') }));
    }
    await i18n.changeLanguage('en');
  });

  it('adds the suggested Designer over SET_ROLE and says so', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
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

  it('tells a failed read of the roles from a shell that offers none', async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
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
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    refuseRole = true;
    show(<Place />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Remove Build' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ROLE', { payload: { change: 'remove', id: 'build', revision: 0 } });
    expect(await screen.findByText(/a role must take it on the machine/)).toBeTruthy();
  });

  /** Ask Daoris's row says what it runs on and how, under its order (it said so beside its access on AI features before COMPANYVIEW1b). */
  it("says under Ask Daoris's row what it runs on, and with full access where the driver says so", async () => {
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    const answer = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: unknown) => {
      if (module === 'DAORIS.DRIVER' && type === 'STATE') return { ...DRIVER_STATE, helpMode: 'full' };
      if (module === 'DAORIS.DRIVER' && type === 'ROLES') {
        return { ...ROLES, roles: [{ id: 'ask-daoris', state: 'seed', seeded: true, nameEn: 'Ask Daoris', nameZh: '问道衍', takes: ['ask-daoris'], scopeKind: 'machine', revision: 0 }] };
      }
      if (module === 'DAORIS.DRIVER' && type === 'ACTORS') {
        return { actors: [{ id: 'ask-daoris', state: 'set', choices: [{ agent: 'claude-code-acp', from: '1' }] }] };
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
    expect(asked).not.toContain('ACTORS');
  });
});

/** Intake's row (MODELROLE1d, D205 §7): its order set in its editor over SET_ACTOR, off in its own words, with what an intake runs as. */
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
    window.localStorage.setItem('daoris.list.agents.mode', 'roles');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { actor?: string; choices?: { agent: string }[] | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ACTOR' && request?.payload?.actor === 'intake') intake = request.payload.choices ?? [];
      if (type === 'STATE' || type === 'SET_ACTOR') return { ...DRIVER_STATE, intakeAdapter: intake[0]?.agent ?? '' };
      if (type === 'ACTORS') return actors();
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
    expect(within(row).getByText('Off — declarations only')).toBeTruthy();
    expect(within(row).getByText(/answered from the workspace's declarations/)).toBeTruthy();
  });

  it("sets Intake's order over SET_ACTOR", async () => {
    const notify = vi.fn();
    show(<Place notify={notify} />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit Intake' }));
    const editor = screen.getByRole('form', { name: 'Intake: your order' });
    expect(within(editor).getByText('Running work never moves to another choice.')).toBeTruthy();
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACTOR', {
      payload: { actor: 'intake', choices: [{ agent: 'claude-code', model: null, effort: null }] },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Intake's order is saved; new work takes it."));
  });

  it("says which account an intake runs as, from the driver's answer, under Intake's row", async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<Place />);

    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
  });

  it('turns Intake off as the terminal does: an empty order', async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<Place />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit Intake' }));
    await user.click(within(screen.getByRole('form', { name: 'Intake: your order' })).getByRole('button', { name: 'Off' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACTOR', { payload: { actor: 'intake', choices: [] } });
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
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACTORS', expect.anything()));
    expect(screen.queryByText('Intake')).toBeNull();
  });
});
