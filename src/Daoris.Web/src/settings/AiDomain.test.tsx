import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Daoris's own AI in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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

import i18n from '../locales/i18n';
import { SettingsView } from './SettingsView';
import { DRIVER_STATE, respond, show, STATUS, WIRING } from '../test/shellHarness';

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to change
 * it. The search tier is the service's answer, over HTTP like every browser's; Intake is this machine's `driver.json`, an actor's
 * row since MODELROLE1d, its editor the screen's half of `daoris driver intake` (D50).
 */
describe('AI features on Settings', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe',
        present: true, version: 'claude 9.9.9', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [],
        profiles: [{ name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' }],
      },
      {
        harness: 'claude-code-acp', product: 'Claude Code', maker: 'Anthropic', wire: 'acp', accountOf: 'claude-code',
        present: true, version: '0.9.1', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
      },
      {
        harness: 'codex', product: 'Codex', maker: 'OpenAI', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
      },
    ],
  };
  const start = (job: 'work' | 'intake', adapter: string) => ({
    job, workspace: 'default', adapter, owner: 'claude-code', product: 'Claude Code',
    profile: 'personal', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
    commanded: false, refusal: null,
  });

  // MODELROLE1d (D205 §7): Intake is a row of *Actors*, its order set in its editor over SET_ACTOR, off its own words.
  let intake: { agent: string }[] = [];
  const actors = () => ({
    actors: [
      { id: 'build', state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] },
      { id: 'intake', state: intake.length > 0 ? 'set' : 'off', choices: intake.map((choice, i) => ({ ...choice, from: String(i + 1) })) },
      { id: 'ask-daoris', state: 'off', choices: [] },
      { id: 'chat', state: 'before', choices: [{ agent: 'claude-code', from: 'before' }] },
    ],
  });
  beforeEach(() => {
    intake = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { actor?: string; choices?: { agent: string }[] | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ACTOR' && request?.payload?.actor === 'intake') intake = request.payload.choices ?? [];
      if (type === 'STATE' || type === 'SET_ACTOR') return { ...DRIVER_STATE, intakeAdapter: intake[0]?.agent ?? '' };
      if (type === 'ACTORS') return actors();
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') {
        return {
          adapter: 'claude-code',
          starts: [start('work', 'claude-code'), ...(intake[0] ? [start('intake', intake[0].agent)] : [])],
        };
      }
      return undefined;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  const rowOf = async (name: string) => (await screen.findByText(name, { selector: 'span' })).closest<HTMLElement>('[data-setting-row]')!;

  it("states the service's own tier, verbatim, and how the model is chosen", async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    expect(screen.getByText(STATUS.note)).toBeTruthy();
    // The variable is code, as every row's terminal door is (UX5 U55).
    expect(screen.getByText('DAORIS_EMBED_MODEL').tagName).toBe('CODE');
  });

  /** The Intake card folded into its row (MODELROLE1d): off in its own words, with what that does under it. */
  it('says an Intake that is off in its own words, on its actor row, with what that does', async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    const row = await rowOf('Intake');
    expect(within(row).getByText('Off — declarations only')).toBeTruthy();
    expect(within(row).getByText(/answered from the workspace's declarations/)).toBeTruthy();
    expect(screen.queryByRole('combobox', { name: 'the intake agent' })).toBeNull();
  });

  /**
   * D50: the screen's half of `daoris driver intake <agent>|off`, now Intake's one-choice order over SET_ACTOR. Its editor offers
   * the ways in this machine HAS, and lands on the same file.
   */
  it("sets Intake's order over SET_ACTOR, and says what an intake runs as, from the driver's answer", async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit' }));
    const editor = screen.getByRole('form', { name: 'Intake: your order' });
    expect(within(editor).getByText('Running work never moves to another choice.')).toBeTruthy();
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACTOR', {
      payload: { actor: 'intake', choices: [{ agent: 'claude-code', model: null, effort: null }] },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Intake's order is saved; new work takes it."));
  });

  /**
   * As whom: the driver's own answer for the intake, per circle, under Intake's row: the same rows *What a start runs on* draws,
   * so the two cannot disagree about one account.
   */
  it("says which account an intake runs as, from the driver's answer, under Intake's row", async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
  });

  it('turns Intake off as the terminal does: an empty order', async () => {
    intake = [{ agent: 'claude-code-acp' }];
    show(<SettingsView notify={() => {}} section="ai" />);
    const user = userEvent.setup();

    await user.click(within(await rowOf('Intake')).getByRole('button', { name: 'Edit' }));
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
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    await screen.findByRole('button', { name: 'Driver' });
    expect(screen.queryByText('Intake')).toBeNull();
  });
});

/**
 * HELPMODE1b (D183 point 2; the modes design §5): Ask Daoris's access under Settings → AI features, beside its agent, the same chip
 * the panel holds: this machine's choice, restricted set at once and full access only on its confirmation, over `SET_HELP_MODE`.
 */
describe('Ask Daoris’s access on Settings', () => {
  let helpMode: string | undefined = 'restricted';
  beforeEach(() => {
    helpMode = 'restricted';
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { mode?: string } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_HELP_MODE') helpMode = request?.payload?.mode;
      if (type === 'STATE' || type === 'SET_HELP_MODE') {
        return { ...DRIVER_STATE, helperAdapter: 'claude-code-acp', intakeAdapter: '', ...(helpMode ? { helpMode } : {}) };
      }
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [] };
      if (type === 'PLUGINS') return { folder: 'plugins', plugins: [] };
      if (type === 'ACTORS') return { actors: [{ id: 'ask-daoris', state: 'set', choices: [{ agent: 'claude-code-acp', from: '1' }] }] };
      return undefined;
    });
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  const chip = (mode: string) => screen.findByRole('button', { name: i18n.t('help.mode.trigger', { mode }) });
  async function pick(mode: 'restricted' | 'full') {
    screen.getByRole('button', { name: (name) => name.startsWith(i18n.t('help.mode.trigger', { mode: '' })) }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitemradio', { name: i18n.t(`help.mode.option.${mode}`) }));
  }

  it.each(['en', 'zh'])('offers it beside Ask Daoris’s agent in %s, restricted until full access is chosen', async (language) => {
    await i18n.changeLanguage(language);
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await chip(i18n.t('help.mode.restricted'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('settings.ai.helpMode.label'))).toBeInTheDocument();
    expect(screen.getByText(i18n.t('settings.ai.helpMode.tierRestricted'))).toBeInTheDocument();
  });

  it('sets restricted at once, and full access only on its confirmation, saying each', async () => {
    helpMode = 'full';
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await chip('Full access');
    expect(screen.getByText('Full access: Ask Daoris acts on this machine itself, without Apply.')).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp with full access/)).toBeInTheDocument();
    await pick('restricted');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'restricted' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Ask Daoris runs restricted from each conversation’s next turn.'));

    await chip('Restricted');
    await pick('full');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    const asks = await screen.findByRole('group', { name: 'Enable full access?' });
    expect(await within(asks).findByText(i18n.t('help.mode.confirm.addsNone'))).toBeInTheDocument();
    await userEvent.click(within(asks).getByRole('button', { name: 'Enable full access' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Ask Daoris runs with full access from each conversation’s next turn.'));
  });

  // ACCESSASK1 (the UI/UX design R3, §3.4): full access's refusal is said inside its ask, which stays open to be pressed again; a
  // switch back refused is said under the chip. Neither is a toast.
  it('says a refusal where it was pressed: inside full access’s ask, and under the chip for a switch back', async () => {
    const refusing = invoke.getMockImplementation()!;
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { mode?: string } }) => {
      if (type === 'SET_HELP_MODE') throw new Error(`The driver could not write the mode ${request?.payload?.mode}.`);
      return refusing(module, type, request);
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await chip('Restricted');
    await pick('full');
    const asks = await screen.findByRole('group', { name: 'Enable full access?' });
    await userEvent.click(within(asks).getByRole('button', { name: 'Enable full access' }));
    expect(await within(asks).findByRole('alert')).toHaveTextContent('The driver could not write the mode full.');
    expect(screen.getByRole('group', { name: 'Enable full access?' })).toBe(asks);
    expect(within(asks).getByRole('button', { name: 'Enable full access' })).toBeEnabled();
    expect(notify).not.toHaveBeenCalled();

    cleanup();
    helpMode = 'full';
    show(<SettingsView notify={notify} section="ai" />);
    await chip('Full access');
    await pick('restricted');
    const row = (await chip('Full access')).closest<HTMLElement>('[data-setting-row]')!;
    expect(await within(row).findByRole('alert')).toHaveTextContent('The driver could not write the mode restricted.');
    expect(notify).not.toHaveBeenCalled();
  });

  it('offers no access on a shell older than the mode', async () => {
    helpMode = undefined;
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText(/Ask Daoris runs on claude-code-acp/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ask Daoris’s access:/ })).toBeNull();
  });
});

/**
 * HELPMODE1c2b (D183's note; the modes design §4.1): restricted mode's configured access under Ask Daoris's access, its
 * housekeeping row live: what the driver's state says is chosen, each operation pressed over `SET_HELP_GRANT` one at a time, and
 * one the driver does not act on yet never sent. HELPMODE1c3: the chosen settings live the same way, and the browser's row read
 * only from its plugin's approval, its door leading to the plugin's tab. HELPMODE1c4: the checkouts live too, each readable checkout
 * the driver serves a box by its repository's name, on until the person turns it off.
 */
describe('Ask Daoris’s configured access on Settings', () => {
  let chosen: string[] = [];
  let settings: string[] = [];
  let checkouts: string[] = [];
  let grants = true;
  let pending = false;
  beforeEach(() => {
    chosen = [];
    settings = [];
    checkouts = ['engine', 'game'];
    grants = true;
    pending = false;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: {
      payload?: { grant?: string; operation?: string; chosen?: boolean };
    }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_HELP_GRANT') {
        const operation = request?.payload?.operation ?? '';
        const grant = request?.payload?.grant;
        const was = grant === 'settings' ? settings : grant === 'checkouts' ? checkouts : chosen;
        const now = request?.payload?.chosen ? [...was, operation] : was.filter((each) => each !== operation);
        if (grant === 'settings') settings = now;
        else if (grant === 'checkouts') checkouts = now;
        else chosen = now;
      }
      if (type === 'STATE' || type === 'SET_HELP_GRANT') {
        return {
          ...DRIVER_STATE, helperAdapter: 'claude-code-acp', intakeAdapter: '', helpMode: 'restricted',
          ...(grants
            ? {
              helpGrants: [
                { id: 'housekeeping', chosen, built: ['inspect', 'clean-up', 'discard-branch'], pending },
                { id: 'settings', chosen: settings, built: ['notify', 'language', 'timeout', 'cap', 'cooloff'], pending: false },
                { id: 'checkouts', chosen: checkouts, built: ['engine', 'game'], pending: false },
                { id: 'browser', chosen: ['reads', 'browser_click'], pending: false, changed: false, plugin: 'in-app-browser' },
              ],
            }
            : {}),
        };
      }
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [] };
      return undefined;
    });
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  const row = (id: string) => screen.getByRole('listitem', { name: i18n.t(`help.grants.name.${id}`) });
  async function open(id: string, door: string) {
    const grant = await screen.findByRole('listitem', { name: i18n.t(`help.grants.name.${id}`) });
    await userEvent.click(within(grant).getByRole('button', {
      name: i18n.t('help.grants.doorFor', { door: i18n.t(`help.grants.door.${door}`), grant: i18n.t(`help.grants.name.${id}`) }),
    }));
    return grant;
  }
  const box = (id: string, operation: string) =>
    within(row(id)).getByRole('checkbox', { name: id === 'checkouts' ? operation : i18n.t(`help.grants.op.${id}.${operation}`) });

  it.each(['en', 'zh'])('draws housekeeping as the driver holds it, and a press chooses one operation, in %s', async (language) => {
    await i18n.changeLanguage(language);
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await open('housekeeping', 'operations');
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.state.off'))).toBeInTheDocument();
    expect(box('housekeeping', 'inspect')).not.toBeChecked();

    await userEvent.click(box('housekeeping', 'inspect'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', {
      payload: { grant: 'housekeeping', operation: 'inspect', chosen: true },
    });
    await waitFor(() => expect(box('housekeeping', 'inspect')).toBeChecked());
    expect(within(row('housekeeping')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      i18n.t('settings.ai.grants.granted', { operation: i18n.t('help.grants.op.housekeeping.inspect') })));

    await userEvent.click(box('housekeeping', 'inspect'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', {
      payload: { grant: 'housekeeping', operation: 'inspect', chosen: false },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      i18n.t('settings.ai.grants.revoked', { operation: i18n.t('help.grants.op.housekeeping.inspect') })));
  });

  it('never sends what the driver does not act on yet: dropping recovery refs', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await open('housekeeping', 'operations');
    expect(box('housekeeping', 'drop-recovery')).toBeDisabled();

    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', expect.anything());
  });

  // HELPMODE1c4: the checkouts the driver serves, each on until turned off, a press naming its repository as written.
  it.each(['en', 'zh'])('draws the checkouts the driver serves, and a press turns one off by its name, in %s', async (language) => {
    await i18n.changeLanguage(language);
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await open('checkouts', 'manage');
    expect(within(row('checkouts')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(box('checkouts', 'engine')).toBeChecked();
    await userEvent.click(box('checkouts', 'engine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', {
      payload: { grant: 'checkouts', operation: 'engine', chosen: false },
    });
    await waitFor(() => expect(box('checkouts', 'engine')).not.toBeChecked());
    expect(box('checkouts', 'game')).toBeChecked();
    await waitFor(() => expect(notify).toHaveBeenCalledWith(i18n.t('settings.ai.grants.revoked', { operation: 'engine' })));
  });

  // HELPMODE1c3: the chosen settings, the five ordinary ones, each granted one at a time as housekeeping's operations are.
  it.each(['en', 'zh'])('draws the chosen settings as the driver holds them, and a press grants one setting, in %s', async (language) => {
    await i18n.changeLanguage(language);
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    await open('settings', 'settings');
    expect(within(row('settings')).getByText(i18n.t('help.grants.state.off'))).toBeInTheDocument();
    await userEvent.click(box('settings', 'cooloff'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', {
      payload: { grant: 'settings', operation: 'cooloff', chosen: true },
    });
    await waitFor(() => expect(box('settings', 'cooloff')).toBeChecked());
    expect(within(row('settings')).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      i18n.t('settings.ai.grants.granted', { operation: i18n.t('help.grants.op.settings.cooloff') })));
  });

  // HELPMODE1c3 (the modes design §4.1's decided note): the browser's row is its plugin's approval, read only, and its door the
  // plugin's own tab, the one place it is changed; nothing is chosen for it here.
  it('draws the browser from its plugin’s approval, and its door opens the plugin’s tab', async () => {
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="ai" onGo={onGo} />);

    const browser = await screen.findByRole('listitem', { name: i18n.t('help.grants.name.browser') });
    expect(within(browser).getByText(i18n.t('help.grants.state.on'))).toBeInTheDocument();
    expect(within(browser).queryAllByRole('checkbox')).toEqual([]);
    await userEvent.click(within(browser).getByRole('button', {
      name: i18n.t('help.grants.doorFor', { door: i18n.t('help.grants.door.manage'), grant: i18n.t('help.grants.name.browser') }),
    }));

    expect(onGo).toHaveBeenCalledWith({ view: 'plugins', item: 'in-app-browser' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_GRANT', expect.anything());
  });

  // ACCESSSWAP1 (D183 point 2): the driver says the grant is pending while a conversation running started with another choice; its
  // next turn takes it, and the mark clears once that conversation's agent has ended.
  it.each(['en', 'zh'])('marks housekeeping pending while the driver says a running conversation has another choice, in %s', async (language) => {
    await i18n.changeLanguage(language);
    pending = true;
    show(<SettingsView notify={() => {}} section="ai" />);

    const grant = await screen.findByRole('listitem', { name: i18n.t('help.grants.name.housekeeping') });
    expect(within(grant).getByText(i18n.t('help.grants.pending'))).toHaveAttribute('title', i18n.t('help.grants.pendingWhy'));
  });

  it('draws no pending mark where the driver says none', async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    const grant = await screen.findByRole('listitem', { name: i18n.t('help.grants.name.housekeeping') });
    expect(within(grant).queryByText(i18n.t('help.grants.pending'))).toBeNull();
  });

  it('draws no configured access on a shell older than the grant', async () => {
    grants = false;
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText(i18n.t('settings.ai.helpMode.tierRestricted'))).toBeInTheDocument();
    expect(screen.queryByText(i18n.t('help.grants.title'))).toBeNull();
  });
});

/**
 * MODELROLE1c (D205 §7): the actors under AI features, each order folded to one line as `ACTORS` answers it, and Build's editor,
 * whose Save sends the whole order over `SET_ACTOR`; a refusal is the driver's sentence under its Save, the editor still open.
 */
describe('Actors on AI features', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe',
        present: true, version: 'claude 9.9.9', problem: null, machineDefault: null,
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
  let refuse = false;

  beforeEach(() => {
    refuse = false;
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ACTOR' && refuse) throw new Error('build takes at most 4 choices, not 5.');
      if (type === 'STATE' || type === 'SET_ACTOR') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'ACTORS') return ACTORS;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [] };
      return undefined;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('folds each actor to one line, as before where it reads today\'s settings', async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('Actors')).toBeTruthy();
    expect(screen.getAllByText('claude-code · account default · account default · as before').length).toBe(2);
    expect(screen.getByText('all at once: claude-code · account default · account default · as before')).toBeTruthy();
    expect(screen.getByText('models for the agents a rule names: as before')).toBeTruthy();
    // Build's, Intake's, Ask Daoris's and Chat's editors (MODELROLE1c, MODELROLE1d): the others' come with row e.
    expect(screen.getAllByRole('button', { name: 'Edit' })).toHaveLength(4);
    // Intake and Ask Daoris off in their own words (D205 §2.1).
    expect(screen.getByText('Off — declarations only')).toBeTruthy();
    expect(screen.getByText('Off — starters only')).toBeTruthy();
  });

  it("saves Build's order over SET_ACTOR, one choice's model named", async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: 'Edit' }))[0]!);
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
    refuse = true;
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);
    const user = userEvent.setup();

    await user.click((await screen.findAllByRole('button', { name: 'Edit' }))[0]!);
    const editor = screen.getByRole('form', { name: 'Build: your order' });
    await user.type(within(editor).getByRole('textbox', { name: "choice 1's model" }), 'opus');
    await user.click(within(editor).getByRole('button', { name: 'Save' }));

    expect(await within(editor).findByRole('alert')).toHaveTextContent('at most 4 choices');
    expect(within(editor).getByRole('textbox', { name: "choice 1's model" })).toHaveValue('opus');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('saved'));
  });

  /** D205 §7 (round 4): Chat's and Ask Daoris's editors say what a running conversation does; every other actor's says it never moves. */
  it.each(['en', 'zh'])("says in Chat's and Ask Daoris's editors that a conversation goes on with the next choice, in %s", async (language) => {
    await i18n.changeLanguage(language);
    show(<SettingsView notify={() => {}} section="ai" />);
    const user = userEvent.setup();
    const edits = await screen.findAllByRole('button', { name: i18n.t('settings.ai.actors.edit') });

    for (const [at, id, words] of [[2, 'ask-daoris', 'goesOn'], [3, 'chat', 'goesOn'], [1, 'intake', 'neverMoves']] as const) {
      await user.click(edits[at]!);
      const editor = screen.getByRole('form', {
        name: i18n.t('settings.ai.actors.editorTitle', { actor: i18n.t(`settings.ai.actors.name.${id}`) }),
      });
      expect(within(editor).getByText(i18n.t(`settings.ai.actors.${words}`))).toBeTruthy();
      await user.click(within(editor).getByRole('button', { name: i18n.t('common.cancel') }));
    }
    await i18n.changeLanguage('en');
  });
});
