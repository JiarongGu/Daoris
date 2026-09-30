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

import { SettingsView } from '../SettingsView';
import { DRIVER_STATE, respond, show, STATUS, WIRING } from '../test/shellHarness';

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to change
 * it. The search tier is the service's answer, over HTTP like every browser's; the intake is this
 * machine's `driver.json`, and its control is the screen's half of `daoris driver intake` (D50).
 */
describe("Daoris's own AI on Settings", () => {
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
    await screen.findByRole('button', { name: 'Permissions' });
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
   * on* draws, so the two cards cannot disagree about one account.
   */
  it('says which account an intake runs as, from the driver\'s answer, in both places it is drawn', async () => {
    intakeAdapter = 'claude-code-acp';
    // Two domains since D75, Daoris's own AI and Workspace: one answer read in each.
    const ai = show(<SettingsView notify={() => {}} section="ai" />);
    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    const here = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(here).getByText('personal')).toBeTruthy();
    ai.unmount();

    show(<SettingsView notify={() => {}} section="workspace" />);
    const there = await screen.findByRole('listitem', { name: 'an intake in default' });
    expect(within(there).getByText('personal')).toBeTruthy();
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
