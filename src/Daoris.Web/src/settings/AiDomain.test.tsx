import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
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
 * it. The search tier is the service's answer, over HTTP like every browser's; the intake is this
 * machine's `driver.json`, and its control is the screen's half of `daoris driver intake` (D50).
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

  let intakeAdapter: string | null = null;
  beforeEach(() => {
    intakeAdapter = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { adapter?: string | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_INTAKE') intakeAdapter = request?.payload?.adapter ?? null;
      // As the wire carries it: off is "", never null — the bridge leaves a null out, and a missing
      // field is how the page knows a shell older than the intake (AGT6, seen on the window).
      if (type === 'STATE' || type === 'SET_INTAKE') return { ...DRIVER_STATE, intakeAdapter: intakeAdapter ?? '' };
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') {
        return {
          adapter: 'claude-code',
          starts: [start('work', 'claude-code'), ...(intakeAdapter ? [start('intake', intakeAdapter)] : [])],
        };
      }
      return undefined;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it("states the service's own tier, verbatim, and how the model is chosen", async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    expect(screen.getByText(STATUS.note)).toBeTruthy();
    // The variable is code, as every row's terminal door is (UX5 U55).
    expect(screen.getByText('DAORIS_EMBED_MODEL').tagName).toBe('CODE');
  });

  /**
   * 🔴 Seen on the window (AGT6): with the intake off, the card offered no intake control at all —
   * the bridge left the null out, and the page read the missing field as a shell older than the
   * intake. Off is "" on the wire now, and reads as Off.
   */
  it('offers the intake control while it is off, reading as Off', async () => {
    show(<SettingsView notify={() => {}} section="ai" />);

    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    expect(trigger).toHaveTextContent('Off — declarations only');
  });

  /** A shell that answers no field has never heard of the intake: no control, not a disabled one. */
  it('offers no intake control on a shell older than the intake', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [start('work', 'claude-code')] };
      return undefined;
    });
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    // The machine's domains appear once the shell has answered: the shell is here, and old.
    await screen.findByRole('button', { name: 'Driver' });
    expect(screen.queryByRole('combobox', { name: 'the intake agent' })).toBeNull();
  });

  /**
   * D50: the screen's half of `daoris driver intake <agent>|off`. It offers the ways in this machine
   * HAS — an agent that is not installed is not a choice — and lands on the same file.
   */
  it('names an agent this machine has for the intake, over SET_INTAKE, and says what that does', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByRole('option', { name: /codex/i })).toBeNull();
    await user.click(await screen.findByRole('option', { name: /claude-code-acp/ }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: 'claude-code-acp' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('intake session on claude-code-acp')));
  });

  /**
   * As whom: the driver's own answer for the intake, per circle — the same rows *What a start runs
   * on* draws, on a workspace's Details since UX6g (`ProjectsView.test.tsx`), so the two cannot disagree about one account.
   */
  it('says which account an intake runs as, from the driver\'s answer', async () => {
    intakeAdapter = 'claude-code-acp';
    show(<SettingsView notify={() => {}} section="ai" />);
    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
  });

  it('turns the intake off as the terminal does — no agent named', async () => {
    intakeAdapter = 'claude-code-acp';
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="ai" />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    await waitFor(() => expect(trigger).toHaveTextContent('claude-code-acp'));
    trigger.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Off — declarations only' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: null } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('declarations only')));
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
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Ask Daoris runs restricted from its next start.'));

    await chip('Restricted');
    await pick('full');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    const asks = await screen.findByRole('group', { name: 'Enable full access?' });
    expect(await within(asks).findByText(i18n.t('help.mode.confirm.addsNone'))).toBeInTheDocument();
    await userEvent.click(within(asks).getByRole('button', { name: 'Enable full access' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Ask Daoris runs with full access from its next start.'));
  });

  it('offers no access on a shell older than the mode', async () => {
    helpMode = undefined;
    show(<SettingsView notify={() => {}} section="ai" />);

    expect(await screen.findByRole('combobox', { name: 'the Ask Daoris agent' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Ask Daoris’s access:/ })).toBeNull();
  });
});
