import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Agents domain in SHELL mode: the toolchain roster, its accounts and what they carried. Moved from
// `shell.test.tsx` with MOD4: tests follow their code. What a start in each workspace runs on is drawn in
// the Workspace domain and tested here, with the roster it resolves.

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
import { HarnessRuns } from '../harnessRuns';
import { REGISTRY, respond, serviceCalls, show, WIRING } from '../test/shellHarness';

/**
 * The toolchain roster (D49 §4, D50): which harnesses this machine has, and which accounts they hold.
 *
 * Shell-only for the sharpest reason yet — a profile HOME is a filesystem path (D47 §4) — so, like
 * the wiring above, every call must land on the bridge and none on the API.
 *
 * The property that matters most here is a negative one: **no credential is anywhere on this
 * surface.** There is nowhere to type one, nothing that reads one, and logging in spawns the
 * harness's own flow. The last test in this block is what fails if that ever stops being true.
 */
describe('the harness roster', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic',
        present: true, version: 'claude 9.9.9', problem: null,
        machineDefault: 'personal', pinned: null, managed: null, pinnable: true,
        ownLogin: 'in',
        workspaceDefaults: [{ workspace: 'orbit', profile: 'work' }],
        profiles: [
          { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
          { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
        ],
      },
      {
        harness: 'codex', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
      },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('reads the roster over the bridge and never over the service', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    // AGT1: a tool is named as a person knows it, with whose it is; one that says neither keeps its id.
    expect(screen.getByText('Claude Code')).toBeTruthy();
    expect(screen.getByText('Anthropic')).toBeTruthy();
    expect(screen.getAllByText('codex').length).toBeGreaterThan(0);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {});
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * MAP1b: what a start in each workspace would run on, beside the agents. The circles are the
   * registry's, the answer is the driver's, and the account is named the way the roster names it.
   */
  it('says what a start in each workspace would run on, asking the driver for the registry\'s circles', async () => {
    const STARTS = {
      adapter: 'claude-code',
      starts: [{
        job: 'work', workspace: 'default', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
        profile: 'personal', profileFrom: 'machine', version: 'claude 9.9.9', versionFrom: 'unset',
        commanded: false, refusal: null,
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'STARTS' ? STARTS : WIRING));
    show(<SettingsView notify={() => {}} section="workspace" />);

    const row = await screen.findByRole('listitem', { name: 'a start in default' });
    expect(within(row).getByText('personal')).toBeTruthy();
    expect(within(row).getByText("this machine's default")).toBeTruthy();
    expect(within(row).getByText('from PATH')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STARTS', { payload: { workspaces: ['default'] } });
  });

  /**
   * FRAME1g (D118 §3h; audit ST11): the domain drew nothing until the roster's first answer, so a first open
   * was a blank page under its name. It holds the card's place with skeleton rows until the roster answers.
   */
  it('holds the roster\'s place with skeleton rows on its first load, never nothing', async () => {
    let answer: (roster: typeof ROSTER) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => (type === 'HARNESSES'
      ? new Promise((resolve) => { answer = resolve; })
      : Promise.resolve(WIRING)));
    show(<SettingsView notify={() => {}} section="agents" />);

    // The machine's domains are offered once the shell answers, and the main area is drawn anew for this one.
    await screen.findByRole('heading', { level: 1, name: 'Agents' });
    const main = screen.getByRole('main');
    await waitFor(() => expect(main.querySelector('[aria-busy="true"]')).not.toBeNull());
    expect(screen.queryByText('Agents on this machine')).toBeNull();

    answer(ROSTER);
    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    expect(main.querySelector('[aria-busy="true"]')).toBeNull();
  });

  /** An absent harness names what it is and offers the action, rather than leaving a blank row. */
  it('an absent tool says so and offers its own installer', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // 🔴 Twice over, and on purpose: the TOOL says whether a person has it, and the way in says
    // whether that particular door is installed. They were one line when a door was a tool.
    expect(await screen.findAllByText('not installed')).not.toHaveLength(0);
    expect(screen.getByText(/is not on this machine's PATH/)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Install' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    });
  });

  /**
   * 🔴 A login outlives its request (2026-09-23): the host answers `started`, the sign-in sits on the
   * row while the person is in the browser, and the end arrives as news — the row then says what
   * the person can do with the account, and the panel goes.
   */
  it('a login that has started stays on its row until its end arrives as news', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    const logins = await screen.findAllByRole('button', { name: /^Sign in( again)?$/ });
    await userEvent.click(logins[1]!);

    expect(await screen.findByText('Signing in to work')).toBeTruthy();
    expect(notify).not.toHaveBeenCalled();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });

    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to work')).toBeNull());
  });

  /**
   * SIGNIN1: a sign-in outlives leaving the Agents domain. Its running action was that domain's own
   * state, and its end was heard only while it was on screen: leaving mid-login lost the code panel
   * and the sentence saying how it ended. The application holds it above every view now.
   */
  it('a sign-in outlives leaving the Agents domain: its panel is there on the way back, and its end is said away from it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const page = (section: 'agents' | 'appearance') => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <HarnessRuns notify={notify}><SettingsView notify={notify} section={section} /></HarnessRuns>
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(page('agents'));

    await userEvent.click((await screen.findAllByRole('button', { name: /^Sign in( again)?$/ }))[1]!);
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    // Away, and back: the sign-in is still running, so its panel is still on its row.
    rerender(page('appearance'));
    expect(screen.queryByText('Signing in to work')).toBeNull();
    rerender(page('agents'));
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    // Away again when it ends: the person is told wherever they are.
    rerender(page('appearance'));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });
    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
  });

  it('each profile shows its login state, and logging in names the profile', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // The tool's own home and `personal` are both logged in; `work` is the one that is not.
    expect(await screen.findAllByText('signed in')).toHaveLength(2);
    expect(screen.getByText('not signed in')).toBeTruthy();

    // Two profiles, two buttons — the second one is `work`, which is the logged-out one.
    const logins = screen.getAllByRole('button', { name: /^Sign in( again)?$/ });
    await userEvent.click(logins[1]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'work' },
    });
  });

  /**
   * 🔴 The account a person actually has — the tool's own configuration home — led nowhere: a
   * machine with no named profile read "No accounts" while its owner was logged in. It leads the
   * list now, with the login state the tool reports for its own home, and it is what sessions use
   * until a default is named. Daoris never logs into it: that is the tool's own business, and the
   * row says so instead of offering a button.
   */
  it('the tool’s own home leads the accounts, with its login state, and takes no login from here', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // Two tools, two own rows; claude-code's is first, in the roster's order.
    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('signed in')).toBeTruthy();
    expect(within(own).queryByRole('button', { name: /^Sign in( again)?$/ })).toBeNull();
    // `personal` is the machine's default, so the own home is not what sessions use — yet.
    expect(within(own).queryByText('used by sessions')).toBeNull();
    // Choosing it again names no profile: the default is CLEARED, not pointed somewhere.
    await userEvent.click(within(own).getByRole('button', { name: 'Make default' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default' },
    });
  });

  it('the tool’s own home is what sessions use when nothing is named', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null, profiles: [] }] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('used by sessions')).toBeTruthy();
    expect(screen.queryByText(/^No accounts/)).toBeNull();
  });

  /**
   * 🔴 An account is made by SIGNING IN (D66 §3). One press, no name typed first; the sign-in shows where
   * the new account will be, and the end names who signed in.
   */
  it('an account is made by signing in, and the end names who signed in', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    // No name box anywhere: the account is named by who signs in.
    await screen.findByText('claude 9.9.9');
    expect(screen.queryByPlaceholderText(/a name/)).toBeNull();

    await userEvent.click(screen.getAllByRole('button', { name: 'Sign in to another account' })[0]!);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
    expect(await screen.findByText('Signing in to another Claude Code account')).toBeTruthy();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: 'someone@example.invalid', kept: true,
      });
    });

    expect(notify).toHaveBeenCalledWith('Signed in as someone@example.invalid — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to another Claude Code account')).toBeNull());
  });

  /** A sign-in that did not finish keeps nothing, and says so rather than going quiet. */
  it('a sign-in that kept nothing says so', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Sign in to another account' }))[0]!);
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: null, kept: false,
      });
    });

    expect(notify).toHaveBeenCalledWith('Nobody was signed in, so nothing was kept.', 'error');
  });

  /** A person knows an account by who is signed in there, not by `account-2`. */
  it('lists each account by who is signed in, where the tool says', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'someone@example.invalid' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const row = (await screen.findByText('someone@example.invalid')).closest('li')!;
    // The directory's name is still there, inside its path, for a terminal.
    expect(within(row).getByText(/account-1$/)).toBeTruthy();
    expect(screen.getByText('owner@example.invalid')).toBeTruthy();
    // Two different people: neither name repeats, so neither says which it is.
    expect(screen.queryByText("this machine's own")).toBeNull();
  });

  /**
   * Seen on the installed window (POLISH3): the tool's own home and an account made in Daoris, both
   * signed in as the same person, were two rows with one bold name, which read as one fact stated
   * twice. The name that repeats says which it is.
   */
  it('says which is the tool\'s own home when an account there is also one made here', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'owner@example.invalid' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const [own, made] = (await screen.findAllByText('owner@example.invalid')).map((name) => name.closest('li')!);
    expect(within(own!).getByText("this machine's own")).toBeTruthy();
    expect(within(made!).queryByText("this machine's own")).toBeNull();
  });

  /**
   * 🔴 Remove REMOVES (D66 §3) — Forget had kept a signed-in account on disk and on the list. It
   * deletes the sign-in, so the first press only asks, and says what the second will do.
   */
  it('Remove asks once, then deletes the account', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'profile-remove', exitCode: 0 };
      // The one line the host streams for it — which a console would render, were one shown.
      if (type === 'TAIL_SESSION') {
        return {
          session: 'claude-code:profile-remove', sequence: 1, live: false, dropped: 0,
          lines: [{ sequence: 1, text: 'removed … — the account and its sign-in are gone from this machine.' }],
        };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
    expect(within(work).getByText(/deletes the account from this machine, sign-in included/)).toBeTruthy();

    await userEvent.click(within(work).getByRole('button', { name: 'Remove account' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-remove', profile: 'work' },
    });
    // An account edit is not a door's work: nothing streams under "Ways in" for it (seen on the
    // window, where the removal's one line sat under the direct door as a live console).
    await new Promise((settle) => setTimeout(settle, 50));
    expect(screen.queryByText(/its sign-in are gone/)).toBeNull();
  });

  it('Remove can be taken back before it deletes anything', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    await userEvent.click(within(work).getByRole('button', { name: 'Never mind' }));

    expect(within(work).queryByRole('button', { name: 'Remove account' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** A tool with no sign-in of its own offers neither sign-in button — the pin's rule. */
  it('a tool that cannot sign in offers no sign-in at all', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], signsIn: false }] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'Sign in to another account' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Sign in( again)?$/ })).toBeNull();
  });

  /**
   * 🔴 USE1a: Update on a pinned door answered only a refusal. The roster now says which Update each
   * door has, and a door with none offers no button — Sign in's rule, and the pin's.
   */
  it('offers no update on a door the roster gives none', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'Update' })).toBeNull();
  });

  it('offers update on a pinned door, and says it moves the pin to the newest release', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0], pinned: '0.79.0', updates: 'pin',
          managed: 'C:/somewhere/.daoris/toolchain/claude-code/0.79.0/bin/claude.exe',
        }, ROSTER.harnesses[1]],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const update = await screen.findByRole('button', { name: 'Update' });
    await userEvent.hover(update);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/newest release/);
    await userEvent.click(update);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'update' },
    });
  });

  it('offers update on an unpinned door with its own updater, and says whose updater runs', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], updates: 'tool' }, ROSTER.harnesses[1]] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.hover(await screen.findByRole('button', { name: 'Update' }));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/own updater/);
  });

  /** After "Add", the next step and what it does were nowhere: the logged-out row says both. */
  it('a signed-out account says what Sign in will do', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText(/runs the tool's own sign-in/)).toBeTruthy();
    const personal = screen.getByText('personal').closest('li')!;
    expect(within(personal).queryByText(/runs the tool's own sign-in/)).toBeNull();
  });

  /**
   * 🔴 A work account for the work circle (D49 §4) could be set from a terminal and not from here.
   * The row says which circles use it, and a circle is chosen from the ones this machine has.
   */
  it('an account can be made one workspace’s default, and the row says which circles use it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          { ...REGISTRY[0], workspace: 'orbit' },
          { ...REGISTRY[0], repository: 'game', workspace: 'lab' },
        ]);
      }
      return respond(url);
    }));
    show(<SettingsView notify={() => {}} section="agents" />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText('used in orbit')).toBeTruthy();

    // Opened from the keyboard: in this suite a pointer click leaves the Radix trigger closed, where
    // the App suite's does not, and Enter is a door the person has too.
    const trigger = await screen.findByRole('combobox', { name: 'use personal for a workspace' });
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'lab' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', profile: 'personal', workspace: 'lab' },
    });
  });

  /**
   * LOOK2c (found by LEFT3): *use for a workspace* on the tool's own row clears that workspace's account, and with a
   * machine default set its sessions then run as that default, not in the tool's own home the row names. The choice says
   * so before the press, and the press says what sessions there run as now, the fact `daoris agent profile default …
   * --clear --workspace` prints.
   */
  it('the tool’s own row says a workspace chosen there falls back to the machine’s default, and says it after', async () => {
    const notify = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], workspace: 'lab' }]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'HARNESS_ACTION' && options?.payload?.action === 'profile-default') {
        return {
          harness: 'claude-code', action: 'profile-default', exitCode: 0,
          default: { workspace: 'lab', account: 'personal', from: 'machine' },
        };
      }
      return WIRING;
    });
    show(<SettingsView notify={notify} section="agents" />);

    // Two tools, two own rows; claude-code's is first, in the roster's order.
    const trigger = (await screen.findAllByRole('combobox', { name: "use this machine's own for a workspace" }))[0]!;
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: "lab, which then runs as personal, this machine's default" }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', workspace: 'lab' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "lab names no account for claude-code now: its sessions run as this machine's default, personal."));
  });

  /** With no machine default, the tool's own row's choice is what it says, and the press says so too. */
  it('the tool’s own row, with no machine default, runs a workspace in the tool’s own home and says so', async () => {
    const notify = vi.fn();
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], workspace: 'lab' }]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESSES') return { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null }] };
      if (type === 'HARNESS_ACTION' && options?.payload?.action === 'profile-default') {
        return { harness: 'claude-code', action: 'profile-default', exitCode: 0, default: { workspace: 'lab', account: null, from: 'own' } };
      }
      return WIRING;
    });
    show(<SettingsView notify={notify} section="agents" />);

    const trigger = await screen.findByRole('combobox', { name: "use this machine's own for a workspace" });
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'lab' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'lab names no account for claude-code now: its sessions run in its own configuration home.'));
  });

  /**
   * A sign-in is the tool's to keep (D49 §4): there is no field for a token or a password, and the
   * surface says where a sign-in lives. An API key is the one exception (D67 §1), and it has its own
   * test below: one field, behind a press, on an agent that takes a key.
   */
  it('offers nowhere to put a sign-in, and says where one lives instead', async () => {
    const { container } = show(<SettingsView notify={() => {}} section="agents" />);
    await screen.findByText('claude 9.9.9');

    const card = screen.getByText('Agents on this machine').closest('section, div')!;
    expect(within(card as HTMLElement).queryByLabelText(/token|password|credential|API key/i)).toBeNull();
    expect(container.textContent).toContain('the tool stores itself');
  });

  /**
   * 🔴 An account that is an API key (AGT3, D67 §1). One password field,
   * behind a press, only on an agent that takes a key; saving sends the key once and the page is
   * told back only its last four characters.
   */
  it('takes an API key once, behind a press, and never shows it back', async () => {
    const key = 'sk-ant-api03-page-test-wxyz';
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        return { harness: 'claude-code', action: 'key-add', exitCode: 0, profile: 'account-1', key: '…wxyz' };
      }
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
        : WIRING;
    });
    const notify = vi.fn();
    const { container } = show(<SettingsView notify={notify} section="agents" />);

    // Only the agent that takes a key offers it; codex, in this roster, does not.
    await screen.findByText('claude 9.9.9');
    expect(screen.getAllByRole('button', { name: 'Add an API key' })).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Add an API key' }));
    const field = screen.getByLabelText('API key for Claude Code');
    expect(field.getAttribute('type')).toBe('password');

    fireEvent.change(field, { target: { value: key } });
    await userEvent.click(screen.getByRole('button', { name: 'Save key' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'key-add', key },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Added account-1, the API key …wxyz.'));
    // The field is gone, and the key with it.
    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(container.innerHTML).not.toContain(key);
  });

  /**
   * 🔴 Cancel is no. Its button sat untyped inside the key's form, so it was a SUBMIT: pressing it
   * closed the field and saved the key typed into it (2026-09-25, found beside the composer's twin).
   */
  it('saves nothing when the person cancels a typed key', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    fireEvent.change(screen.getByLabelText('API key for Claude Code'), { target: { value: 'sk-ant-api03-no' } });
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));

    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /**
   * AGT6 (D98): an account's own model and effort, read from the tool's own file under it and changed
   * there — the screen's half of `daoris agent settings`. The choices are the tool's, from the roster;
   * the change goes over the bridge as the keys the person changed, and nothing else.
   */
  it("sets an account's own model over the bridge, from the choices the tool offers", async () => {
    const CHOICES = { models: ['default', 'sonnet', 'opus'], efforts: ['low', 'medium', 'high', 'xhigh'] };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SET_AGENT_SETTINGS') return { harness: 'claude-code', profile: 'work', model: 'sonnet', effort: 'high', perModel: [], problem: null };
      return type === 'HARNESSES'
        ? {
          ...ROSTER,
          harnesses: [{
            ...ROSTER.harnesses[0],
            settingsChoices: CHOICES,
            profiles: [
              { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'in',
                settings: { model: 'opus', effort: 'high', perModel: [], problem: null } },
            ],
          }, ROSTER.harnesses[1]],
        }
        : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    const row = (await screen.findByText('model opus · effort high')).closest('li')!;
    await userEvent.click(within(row).getByRole('button', { name: 'Model & effort' }));
    const form = within(row).getByRole('group', { name: 'work: model and effort' });
    within(form).getByRole('combobox', { name: 'model' }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'sonnet' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_AGENT_SETTINGS', {
      payload: { harness: 'claude-code', profile: 'work', model: 'sonnet' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('work: saved — the next session reads it.'));
    expect(serviceCalls()).toEqual([]);
  });

  /** A tool whose settings Daoris does not know is offered none, said in one line (AGT6). */
  it("offers no settings for a tool whose own Daoris does not know, and says so once", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [ROSTER.harnesses[0], { ...ROSTER.harnesses[1], product: 'Codex', settingsChoices: null,
          profiles: [{ name: 'work', home: 'C:/somewhere/.daoris/harnesses/codex/work', login: 'in', settings: null }] }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText("Daoris does not know Codex's own settings, so it offers none — set its model with the tool itself.")).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Model & effort' })).toBeNull();
  });

  /** A key account reads as its handle and offers no sign-in: it is signed in by its key. */
  it('lists a key account by its handle, with no sign-in to offer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          takesKey: true,
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', key: '…wxyz' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    const row = (await screen.findByText('API key …wxyz')).closest('li')!;
    expect(within(row).queryByRole('button', { name: /^Sign in( again)?$/ })).toBeNull();
    // 🔴 Never "logged in": the tool says so for any key, a wrong one included (measured, AGT3).
    expect(within(row).getByText('unchecked')).toBeTruthy();
    expect(within(row).queryByText('signed in')).toBeNull();
    expect(within(row).getByRole('button', { name: 'Remove' })).toBeTruthy();
  });

  /** A door a plugin declared says so beside its name (D64), and the build's own say nothing. */
  it('a declared door names the plugin it came from', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [
          ...ROSTER.harnesses,
          {
            harness: 'acme-agent', present: true, version: '(not asked)', problem: null, wire: 'acp',
            machineDefault: null, pinned: null, managed: null, pinnable: false, profiles: [], plugin: 'acme.gate',
          },
        ],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('declared by plugin acme.gate')).toBeTruthy();
    expect(screen.getAllByText(/declared by plugin/)).toHaveLength(1);
  });

  /** A shell older than this surface answers something else; the rest of the page must stand. */
  it('an answer that is not a roster draws no tools, and takes the page down with it nowhere', async () => {
    invoke.mockImplementation(async () => WIRING); // no `harnesses` anywhere in it

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByRole('navigation', { name: 'Settings domains' })).toBeTruthy();
    expect(screen.queryByText('Agents on this machine')).toBeNull();
    // An answer is in, so nothing is loading any more: no skeleton left standing in for a card.
    await screen.findByRole('heading', { level: 1, name: 'Agents' });
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {}));
    await waitFor(() => expect(screen.getByRole('main').querySelector('[aria-busy="true"]')).toBeNull());
  });

  /**
   * What each account has carried (TOOL3/D57 §4). Absent entirely on a machine that has measured
   * nothing — a row of zeroes would claim sessions used nothing, when what is true is that nothing
   * was measured.
   */
  it('says nothing about usage on a machine that has measured none', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByText('Usage')).toBeNull();
  });

  /**
   * UX5 U72, seen on the window: the Agents menu's *Usage* opened this domain at its top, and the
   * usage sat a screen below it. Opened for a part, the page brings that part into view once it is
   * drawn, and says it has, so a later visit opens at the top again.
   */
  it('opens at the part a menu item names, once that part is drawn', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'USAGE') return { sessions: [], accounts: [{ harness: 'claude-code', profile: null, sessions: 1, used: 9000 }] };
      return WIRING;
    });
    const scrolled = vi.fn();
    const original = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function scroll(this: Element) { scrolled(this.id); };
    const anchored = vi.fn();
    try {
      show(<SettingsView notify={() => {}} section="agents" anchor="usage" onAnchored={anchored} />);

      await screen.findByText('Usage');
      await waitFor(() => expect(scrolled).toHaveBeenCalledWith('settings-usage'));
      expect(anchored).toHaveBeenCalled();
    } finally {
      Element.prototype.scrollIntoView = original;
    }
  });

  it('totals what each account carried, and names the one with no profile', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'USAGE') {
        return {
          sessions: [],
          accounts: [
            { harness: 'claude-code', profile: 'work', sessions: 2, used: 60000 },
            { harness: 'claude-code', profile: null, sessions: 1, used: 9000 },
          ],
        };
      }
      return WIRING;
    });

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('Usage')).toBeTruthy();
    // The unit is named beside the figure: a bare number in a column says nothing, and "context"
    // is the honest word — calling them tokens would be a claim Daoris cannot make.
    expect(screen.getByText('60,000 context')).toBeTruthy();
    // A session on the harness's own configuration home is still somebody's usage, and each
    // account is named as the list above names it (UX5 U53): it said "its own home", and a named
    // account by its directory in the accent.
    const usage = screen.getByText('Usage').parentElement!;
    expect(within(usage).getByText("this machine's own")).toBeTruthy();
    expect(within(usage).getByText('work').getAttribute('class')).not.toContain('accent');
    // Its note wraps at the card's edge with every block of the domain (D141), where it kept 65ch.
    expect(within(usage).getByText(/Measured, not billed/).className).not.toMatch(/\bmax-w-/);
    // 🔴 No price is claimed anywhere — Daoris does not know what a token costs (D24).
    expect(screen.queryByText(/[$£€]/)).toBeNull();
  });

  /**
   * The managed toolchain (TOOL2/D57), desktop half. **Absent means PATH**, and the surface says so
   * rather than leaving a blank — "Daoris manages this" and "the machine happens to have one" are
   * different facts about the same working session.
   */
  it('says a harness runs from PATH until something is pinned', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // Said on the door itself, where the pin control is — the roster's body no longer restates it.
    expect(await screen.findAllByText(/from PATH/)).not.toHaveLength(0);
    expect(screen.queryByText(/^pinned /)).toBeNull();
  });

  /**
   * UX5 U56: a way in that is not there does not run from PATH yet. It read *runs from PATH* under
   * *codex-acp is not on this machine's PATH*.
   */
  it('says a missing way in will run from PATH once it is there', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: ROSTER.harnesses.map((door) => (door.harness === 'codex' ? { ...door, pinnable: true } : door)) }
      : WIRING));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('not on PATH yet')).toBeTruthy();
    // The installed way in still runs from PATH, and says so plainly.
    expect(screen.getByText('runs from PATH')).toBeTruthy();
  });

  it('pinning installs that version and pins to it, in one action', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    // The form is behind a press now: five always-open version boxes were the widest thing on
    // the surface and almost nobody types in one.
    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const version = await screen.findByLabelText('version of claude-code to pin');
    await userEvent.type(version, '1.2.3');
    await userEvent.click(screen.getAllByRole('button', { name: 'Pin' })[0]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'pin', profile: undefined, version: '1.2.3' },
    });
  });

  it('offers nothing to press until a version is typed', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const [pin] = await screen.findAllByRole('button', { name: 'Pin' });
    expect(pin).toBeDisabled();
  });

  /**
   * 🔴 A harness that declares no package has no version for Daoris to fetch, so the control is
   * **absent** rather than present and refusing — half a control is worse than none. Found by
   * looking at the real window, where the stub adapter was offering a button whose only possible
   * outcome was a refusal.
   */
  it('offers no pin at all for a harness that cannot be pinned', async () => {
    show(<SettingsView notify={() => {}} section="agents" />);

    await screen.findByText('claude 9.9.9');
    // Only the pinnable tool offers the disclosure at all, though the roster carries two.
    expect(screen.getAllByRole('button', { name: 'Pin a version' })).toHaveLength(1);
  });

  /**
   * 🔴 A pin naming a version nobody installed **refuses every spawn**, so the surface must say that
   * rather than show the pin as though it were in force. The two fields are answered separately by
   * the host precisely so this state is renderable.
   */
  it('a pin with nothing installed at it reads as missing, not as in force', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{ ...ROSTER.harnesses[0], pinned: '9.9.9', managed: null }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText(/pinned 9\.9\.9 — not installed/)).toBeTruthy();
    // And the way back is offered, because a refusing pin is exactly when somebody wants it.
    expect(screen.getByRole('button', { name: 'Unpin' })).toBeTruthy();
  });

  it('a pin that is in force names the binary sessions actually run', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          pinned: '1.2.3',
          managed: 'C:/somewhere/.daoris/toolchain/claude-code/1.2.3/node_modules/.bin/claude',
        }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('pinned 1.2.3')).toBeTruthy();
    expect(screen.getByText(/toolchain[\\/]claude-code[\\/]1\.2\.3/)).toBeTruthy();
  });

  // The per-session PICKER moved with the start form it belongs to (`work/WorkFrame.test.tsx`).
});

/**
 * How each agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6), over the bridge: each account's
 * cool-off with *Try now*, its list with *Use*, a default the list would refuse not offered, the tool's own sign-in said while
 * starts run on it, and the terms. Each press is the terminal's `daoris agent profile order|use|ready`, on the same files.
 */
describe('how accounts are used', () => {
  const until = new Date(Date.now() + 190 * 60_000).toISOString();
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [{
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: 'personal' as string | null, pinned: null, managed: null, pinnable: true, ownLogin: 'in', signsIn: true,
      profiles: [
        { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
        { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'in' },
      ],
    }],
  };
  const MACHINE = {
    workspace: null, default: 'personal' as string | null, list: ['personal'], begins: 'personal' as string | null,
    use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [],
  };
  const ACCOUNTS = {
    agents: [{
      agent: 'claude-code', speaks: true, own: {} as Record<string, unknown>,
      accounts: [
        { name: 'personal', running: 1, cooling: { until, stated: true, window: 'weekly', seen: until, assumedZone: false, notBelieved: false } },
        { name: 'work', running: 0 },
      ],
      scopes: [MACHINE],
    }],
  };
  const answer = (roster: unknown, accounts: unknown) =>
    async (_module: string, type: string, args?: { payload?: { action?: string } }) => {
      if (type === 'HARNESSES') return roster;
      if (type === 'ACCOUNTS') return accounts;
      if (type === 'ACCOUNT_USE') return { harness: 'claude-code', action: args?.payload?.action, ended: true };
      return WIRING;
    };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('says an account cooling, and Try now ends its cool-off through the terminal\'s profile ready', async () => {
    invoke.mockImplementation(answer(ROSTER, ACCOUNTS));
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    expect(await screen.findByText(/^Cooling until .+ · in 3h \d+m · the agent said so$/)).toBeTruthy();
    expect(screen.getByText(/1 of Daoris's sessions running · nothing said yet/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'try personal now' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'ready', profile: 'personal' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^personal is offered again\./)));
  });

  it('turns Use on through the terminal\'s profile order, and offers no default the list would refuse', async () => {
    invoke.mockImplementation(answer(ROSTER, ACCOUNTS));
    show(<SettingsView notify={() => {}} section="agents" />);

    await userEvent.click(await screen.findByRole('checkbox', { name: 'use work for This machine' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'order', accounts: ['personal', 'work'] },
    });
    // `work` is outside the machine's list, so *Make default* is offered only on the tool's own row, which clears it.
    expect(screen.getAllByRole('button', { name: 'Make default' })).toHaveLength(1);
    expect(screen.getByText(/^Each account's own plan and terms apply\./)).toBeTruthy();
    expect(screen.queryByText(/^Sessions run on your own sign-in/)).toBeNull();
  });

  it('says starts run on the tool\'s own sign-in while the machine names no account, with its cool-off', async () => {
    const shared = { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0]!, machineDefault: null, ownAccount: 'someone@example.invalid' }] };
    const cooling = {
      agents: [{
        ...ACCOUNTS.agents[0]!,
        own: { cooling: { until, stated: false, window: null, seen: until, assumedZone: false, notBelieved: false } },
        scopes: [{ ...MACHINE, default: null, list: [], begins: null }],
      }],
    };
    invoke.mockImplementation(answer(shared, cooling));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText(/^Sessions run on your own sign-in, someone@example\.invalid: .+ It is cooling until /)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'try someone@example.invalid now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'ready', own: true },
    });
  });

  /**
   * TOOL6e: each scope says which account its next start takes and why, with what holds the others, the account named as
   * a person knows it; and the tool's own sign-in, offered again since its cool-off ended, says so on its row.
   */
  it('says which account the next start takes and why, and since when an account is offered again', async () => {
    const offered = new Date(Date.now() - 58 * 60_000).toISOString();
    const next = {
      agents: [{
        ...ACCOUNTS.agents[0]!,
        own: { offered },
        scopes: [{
          ...MACHINE, list: ['personal', 'work'],
          next: { account: 'work', reason: 'onlyReady', over: null, when: null, others: [{ account: 'personal', hold: 'cooling', until }] },
        }],
      }],
    };
    const named = { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0]!, profiles: [ROSTER.harnesses[0]!.profiles[0]!, { ...ROSTER.harnesses[0]!.profiles[1]!, account: 'work@example.invalid' }] }] };
    invoke.mockImplementation(answer(named, next));
    show(<SettingsView notify={() => {}} section="agents" />);

    expect(await screen.findByText('The next start takes work@example.invalid: it is the only account here that is ready.')).toBeTruthy();
    expect(screen.getByText(/^personal is cooling until .+\.$/)).toBeTruthy();
    expect(screen.getByText(/^Claude Code's own sign-in, the account it uses at your terminal, carries none of these starts/)).toBeTruthy();
    expect(screen.getByText(/^offered again since /)).toBeTruthy();
  });

  /** A refusal reads as the terminal's, in the reader's language (REV2): the host's code, from the catalogue. */
  it('says a refused edit in the catalogue\'s words', async () => {
    const answered = answer(ROSTER, ACCOUNTS);
    invoke.mockImplementation(async (module: string, type: string) => {
      if (type === 'ACCOUNT_USE') {
        throw Object.assign(new Error('refused'), {
          code: 'ACCOUNT_SCOPE_DEFAULT', parameters: { agent: 'claude-code', account: 'personal' },
        });
      }
      return answered(module, type);
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="agents" />);

    await userEvent.click(await screen.findByRole('checkbox', { name: 'use work for This machine' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      expect.stringMatching(/^This machine's default for claude-code is personal, and the list would not hold it/), 'error'));
  });
});
