import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The Agents place in SHELL mode (UX6e, D150 §5): the list of agents and an agent's page, its accounts with their last known
// state and when each was read, and every act on it, over the mocked bridge. Moved from Settings → Agents' suite with the
// cards it tested (`settings/AgentsDomain.test.tsx` until UX6e), its assertions read on the page they are on now.

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

import { HarnessRuns } from '../harnessRuns';
import { REGISTRY, respond, serviceCalls, show, WIRING } from '../test/shellHarness';
import { useListPanes } from '../work/listPanes';
import { ViewFrame } from '../work/ViewFrame';
import type { AgentPart } from './agents';
import { useAgentsView } from './AgentsView';

/** The place as the application holds it: its list and its main area in a frame, the list's memory its own. */
function Place({ notify, first = 'claude-code', part = null, onAnchored }: {
  notify: (message: string, tone?: 'ok' | 'error') => void;
  first?: string | null;
  part?: AgentPart | null;
  onAnchored?: () => void;
}) {
  const lists = useListPanes();
  const [chosen, setChosen] = useState<string | null>(first);
  const [filters, setFilters] = useState<Record<string, unknown>>({});
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, chosen, onChoose: setChosen, filters, onFilters: setFilters, notify, part, onAnchored });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const place = (notify: (message: string, tone?: 'ok' | 'error') => void = () => {}, options: Partial<Parameters<typeof Place>[0]> = {}) =>
  show(<Place notify={notify} {...options} />);

/** Open a folded section by its name (D150 §1 rule 4). */
const unfold = async (section: string) => userEvent.click(await screen.findByRole('button', { name: `Show ${section}` }));

/** An account's ⋯, opened from the keyboard: in this suite a pointer click leaves a Radix trigger closed. */
async function more(account: string) {
  const trigger = await screen.findByRole('button', { name: `More for ${account}` });
  trigger.focus();
  await userEvent.keyboard('{Enter}');
  return screen.findByRole('menu');
}

const READ = '2026-10-04T10:42:00.000Z';

/**
 * The toolchain roster (D49 §4, D50) on the Agents place: which agents this machine has, and the accounts they hold.
 *
 * Shell-only for the sharpest reason yet — a profile HOME is a filesystem path (D47 §4) — so every call must land on the
 * bridge and none on the API. **No credential is anywhere on this surface**: there is nowhere to type one but an API key's
 * field, behind a press, and logging in spawns the agent's own flow.
 */
describe('the Agents place', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic',
        present: true, version: 'claude 9.9.9', problem: null,
        machineDefault: 'personal', pinned: null, managed: null, pinnable: true,
        ownLogin: 'in', ownRead: READ,
        workspaceDefaults: [{ workspace: 'orbit', profile: 'work' }],
        profiles: [
          { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in', read: READ },
          { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out', read: READ },
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
    place();

    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    // AGT1: an agent is named as a person knows it, with whose it is.
    expect(screen.getByRole('heading', { level: 1, name: 'Claude Code' })).toBeTruthy();
    expect(within(screen.getByRole('main')).getByText('Anthropic')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {});
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * 🔴 D150 §5.3: opening the place starts no process and asks no account. It reads the roster as the frame holds it and
   * the accounts' files; only *Read again* asks, on the press, one agent's accounts.
   */
  it('asks no account when it opens, and reads this agent’s accounts again only on Read again', async () => {
    place();
    await screen.findByText('claude 9.9.9');

    const asked = () => invoke.mock.calls.filter(([, type]) => type === 'HARNESSES' || type === 'HARNESS_ACTION');
    expect(asked()).toEqual([['DAORIS.DRIVER', 'HARNESSES', {}]]);

    await userEvent.click(screen.getByRole('button', { name: 'Read again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', { payload: { refresh: true, agent: 'claude-code' } });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** Each account says its last known state with when it was read, and one nothing answered was never read (§5.3). */
  it('says each account’s state as last known and when it was read', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0], ownLogin: 'unknown', ownRead: null,
          profiles: [...ROSTER.harnesses[0]!.profiles!, { name: 'spare', home: 'C:/somewhere/.daoris/harnesses/claude-code/spare', login: 'unknown', read: null }],
        }],
      }
      : WIRING));
    place();

    const work = await screen.findByRole('listitem', { name: 'work' });
    // `work` reads signed out, and a workspace's default names it, so it holds work and wears open's hue.
    expect(within(work).getByText('signed out')).toBeTruthy();
    expect(within(work).getByText(/^read \d{2}:\d{2}|^read \w+/)).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'spare' })).getByText('never read')).toBeTruthy();
    // The tool's own sign-in is asked only at a press (TOOL6g): unknown, never read, never signed in.
    const own = screen.getByRole('listitem', { name: "The tool's own sign-in" });
    expect(within(own).getByText('unknown')).toBeTruthy();
    expect(within(own).getByText('never read')).toBeTruthy();
  });

  it('lists each agent once, with its accounts in a phrase, and hides those not installed until asked', async () => {
    place();

    const list = await screen.findByRole('list', { name: 'Agents' });
    expect(within(list).getByText('2 accounts · 1 signed out')).toBeTruthy();
    expect(within(list).queryByText('codex')).toBeNull();

    // The list's ⋯ shows the agents not installed (§5.1), the one not installed saying so.
    const trigger = screen.getByRole('button', { name: 'More actions' });
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitemcheckbox', { name: 'Show agents not installed' }));
    expect(await within(list).findByText('codex')).toBeTruthy();
    expect(within(list).getByText('not installed')).toBeTruthy();
  });

  /**
   * FRAME1g (D118 §3h): the page drew nothing until the roster's first answer. It holds its place with skeleton rows until
   * the roster answers.
   */
  it('holds the page’s place with skeleton rows on its first load, never nothing', async () => {
    let answer: (roster: typeof ROSTER) => void = () => {};
    invoke.mockImplementation((_module: string, type: string) => (type === 'HARNESSES'
      ? new Promise((resolve) => { answer = resolve; })
      : Promise.resolve(WIRING)));
    place();

    const main = await screen.findByRole('main');
    await waitFor(() => expect(main.getAttribute('aria-busy')).toBe('true'));

    answer(ROSTER);
    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    expect(screen.getByRole('main').getAttribute('aria-busy')).toBeNull();
  });

  /** An agent not installed says so and offers its own installer, under *Ways in*. */
  it('an agent not installed says so and offers its own installer', async () => {
    place(() => {}, { first: 'codex' });

    expect(await screen.findAllByText('not installed')).not.toHaveLength(0);
    // Its install waits on the person, so its ways in are open, never folded away.
    expect(screen.getByRole('button', { name: 'Hide Ways in' })).toBeTruthy();
    expect(screen.getByText(/is not on this machine's PATH/)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Install' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    });
  });

  /**
   * 🔴 A login outlives its request (2026-09-23): the host answers `started`, the sign-in sits on the row while the person
   * is in the browser, and the end arrives as news — the row then says what the person can do, and the panel goes.
   */
  it('a login that has started stays on its row until its end arrives as news', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    const work = await screen.findByRole('listitem', { name: 'work' });
    await userEvent.click(within(work).getByRole('button', { name: 'Sign in' }));

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
   * SIGNIN1: a sign-in outlives leaving the place. Its running action is held above every view, so its panel is there on
   * the way back, and its end is said wherever the person is.
   */
  it('a sign-in outlives leaving the place: its panel is there on the way back, and its end is said away from it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const page = (here: boolean) => (
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <HarnessRuns notify={notify}>{here ? <Place notify={notify} /> : <p>elsewhere</p>}</HarnessRuns>
        </Tooltip.Provider>
      </QueryClientProvider>
    );
    const { rerender } = render(page(true));

    await userEvent.click(within(await screen.findByRole('listitem', { name: 'work' })).getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    rerender(page(false));
    expect(screen.queryByText('Signing in to work')).toBeNull();
    rerender(page(true));
    expect(await screen.findByText('Signing in to work')).toBeTruthy();

    rerender(page(false));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });
    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
  });

  it('a signed-in account offers its sign-in again in its ⋯, and a signed-out one Sign in on its row', async () => {
    place();

    const personal = await screen.findByRole('listitem', { name: 'personal' });
    expect(within(personal).getByText('signed in')).toBeTruthy();
    expect(within(personal).queryByRole('button', { name: 'Sign in' })).toBeNull();
    const menu = await more('personal');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Sign in again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'personal' },
    });
  });

  /**
   * 🔴 The account a person actually has — the tool's own configuration home — leads the accounts, with its state, and
   * Daoris never signs into it: its ⋯ names no sign-in, and *Use by default* clears the machine's default.
   */
  it('the tool’s own sign-in leads the accounts, takes no sign-in from here, and Use by default clears the default', async () => {
    place();

    const own = await screen.findByRole('listitem', { name: "The tool's own sign-in" });
    expect(within(own).getByText('signed in')).toBeTruthy();
    expect(within(own).queryByRole('button', { name: 'Sign in' })).toBeNull();
    // `personal` is the machine's default, so the own sign-in is not what sessions use — yet.
    expect(within(own).queryByText('used by sessions')).toBeNull();
    const menu = await more("The tool's own sign-in");
    expect(within(menu).queryByRole('menuitem', { name: /Sign in/ })).toBeNull();
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Use by default' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default' },
    });
  });

  it('the tool’s own sign-in is what sessions use when nothing is named', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null, profiles: [] }] }
      : WIRING));
    place();

    const own = await screen.findByRole('listitem', { name: "The tool's own sign-in" });
    expect(within(own).getByText('used by sessions')).toBeTruthy();
  });

  /** 🔴 An account is made by SIGNING IN (D66 §3): one press, no name typed first; the end names who signed in. */
  it('an account is made by signing in, and the end names who signed in', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

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

  it('a sign-in that kept nothing says so', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Sign in to another account' }))[0]!);
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: null, kept: false,
      });
    });

    expect(notify).toHaveBeenCalledWith('Nobody was signed in, so nothing was kept.', 'error');
  });

  /** A person knows an account by who is signed in there, not by `account-2`; the directory is beside it. */
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
    place();

    const row = await screen.findByRole('listitem', { name: 'someone@example.invalid' });
    expect(within(row).getByText('account-1')).toBeTruthy();
    expect(screen.getByRole('listitem', { name: 'owner@example.invalid' })).toBeTruthy();
  });

  /** 🔴 Remove REMOVES (D66 §3): it deletes the sign-in, so its first press only asks, and says what the second will do. */
  it('Remove… asks once, then deletes the account', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'profile-remove', exitCode: 0 };
      if (type === 'TAIL_SESSION') {
        return {
          session: 'claude-code:profile-remove', sequence: 1, live: false, dropped: 0,
          lines: [{ sequence: 1, text: 'removed … — the account and its sign-in are gone from this machine.' }],
        };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    place();

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Remove…' }));
    const work = screen.getByRole('listitem', { name: 'work' });
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
    expect(within(work).getByText(/deletes the account from this machine, sign-in included/)).toBeTruthy();

    await userEvent.click(within(work).getByRole('button', { name: 'Remove account' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-remove', profile: 'work' },
    });
    // An account edit is not a door's work: nothing streams under *Ways in* for it.
    await new Promise((settle) => setTimeout(settle, 50));
    expect(screen.queryByText(/its sign-in are gone/)).toBeNull();
  });

  it('Remove… can be taken back before it deletes anything', async () => {
    place();

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Remove…' }));
    const work = screen.getByRole('listitem', { name: 'work' });
    await userEvent.click(within(work).getByRole('button', { name: 'Never mind' }));

    expect(within(work).queryByRole('button', { name: 'Remove account' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** An agent with no sign-in of its own offers neither sign-in. */
  it('an agent that cannot sign in offers no sign-in at all', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], signsIn: false }] }
      : WIRING));
    place();

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'Sign in to another account' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull();
  });

  /** 🔴 USE1a: a door the roster gives no Update offers none. */
  it('offers no update on a door the roster gives none', async () => {
    place();

    await unfold('Ways in');
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
    place();

    await unfold('Ways in');
    const update = screen.getByRole('button', { name: 'Update' });
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
    place();

    await unfold('Ways in');
    await userEvent.hover(screen.getByRole('button', { name: 'Update' }));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(/own updater/);
  });

  /**
   * 🔴 A work account for the work circle (D49 §4): the row says which workspaces may run on it, and a workspace's default
   * is chosen from the ones this machine has, in the account's ⋯.
   */
  it('an account can be made one workspace’s default, and the row says which workspaces may run on it', async () => {
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
    place();

    const work = await screen.findByRole('listitem', { name: 'work' });
    expect(within(work).getByText('orbit may run on it')).toBeTruthy();

    await userEvent.click(within(await more('personal')).getByRole('menuitem', { name: 'Use by default in lab' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', profile: 'personal', workspace: 'lab' },
    });
  });

  /**
   * LOOK2c (found by LEFT3): clearing a workspace's default from the tool's own row runs it on the machine's default where
   * one is set, and the press says so, as `daoris agent profile default … --clear --workspace` prints.
   */
  it('clearing a workspace’s default from the tool’s own row says what its sessions run as now', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'HARNESS_ACTION' && options?.payload?.action === 'profile-default') {
        return {
          harness: 'claude-code', action: 'profile-default', exitCode: 0,
          default: { workspace: 'orbit', account: 'personal', from: 'machine' },
        };
      }
      return WIRING;
    });
    place(notify);

    await userEvent.click(within(await more("The tool's own sign-in")).getByRole('menuitem', { name: 'Clear the default in orbit' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', workspace: 'orbit' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "orbit names no account for claude-code now: its sessions run as this machine's default, personal."));
  });

  /**
   * A sign-in is the tool's to keep (D49 §4): there is no field for a token or a password. An API key is the one exception
   * (D67 §1), behind a press, on an agent that takes a key.
   */
  it('offers nowhere to put a sign-in', async () => {
    place();
    await screen.findByText('claude 9.9.9');

    expect(within(screen.getByRole('main')).queryByLabelText(/token|password|credential|API key/i)).toBeNull();
  });

  /** 🔴 An account that is an API key (AGT3, D67 §1): one password field, behind a press; the page is told only its handle. */
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
    const { container } = place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    const field = screen.getByLabelText('API key for Claude Code');
    expect(field.getAttribute('type')).toBe('password');

    fireEvent.change(field, { target: { value: key } });
    await userEvent.click(screen.getByRole('button', { name: 'Save key' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'key-add', key },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Added account-1, the API key …wxyz.'));
    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(container.innerHTML).not.toContain(key);
  });

  /** 🔴 Cancel is no: an untyped button inside the key's form was a SUBMIT, and saved what it closed. */
  it('saves nothing when the person cancels a typed key', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
      : WIRING));
    place();

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    fireEvent.change(screen.getByLabelText('API key for Claude Code'), { target: { value: 'sk-ant-api03-no' } });
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));

    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** AGT6 (D98): an account's own model and effort, from the choices the tool offers, under *Model and effort*. */
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
    place(notify);

    await unfold('Model and effort');
    const row = screen.getByText('model opus · effort high').closest('li')!;
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

  /** A section appears only where the agent has that concept (§5.1): no settings Daoris does not know. */
  it("offers no model and effort for an agent whose settings Daoris does not know", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{ ...ROSTER.harnesses[1], present: true, version: '0.1', product: 'Codex', settingsChoices: null,
          profiles: [{ name: 'work', home: 'C:/somewhere/.daoris/harnesses/codex/work', login: 'in', settings: null }] }],
      }
      : WIRING));
    place(() => {}, { first: 'codex' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Codex' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Show Model and effort' })).toBeNull();
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
    place();

    const row = await screen.findByRole('listitem', { name: 'API key …wxyz' });
    expect(within(row).queryByRole('button', { name: 'Sign in' })).toBeNull();
    // 🔴 Never signed in: the tool says so for any key, a wrong one included (measured, AGT3).
    expect(within(row).getByText('unchecked')).toBeTruthy();
    expect(within(row).queryByText('signed in')).toBeNull();
    const menu = await more('API key …wxyz');
    expect(within(menu).queryByRole('menuitem', { name: 'Sign in again' })).toBeNull();
    expect(within(menu).getByRole('menuitem', { name: 'Remove…' })).toBeTruthy();
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
    place(() => {}, { first: 'acme-agent' });

    await unfold('Ways in');
    expect(screen.getByText('declared by plugin acme.gate')).toBeTruthy();
    expect(screen.getAllByText(/declared by plugin/)).toHaveLength(1);
  });

  /** A shell older than this place answers something else; the page lists nothing and stands. */
  it('an answer that is not a roster lists no agents, and takes the page down with it nowhere', async () => {
    invoke.mockImplementation(async () => WIRING);
    place();

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {}));
    expect(await screen.findByText('Choose an agent')).toBeTruthy();
    expect(screen.getByRole('main').getAttribute('aria-busy')).toBeNull();
  });

  /** Usage says nothing measured as nothing, never a row of zeroes (D57). */
  it('says usage is nothing measured on a machine that has measured none', async () => {
    place();

    expect(await screen.findByText('nothing measured yet')).toBeTruthy();
    await unfold('Usage');
    expect(screen.queryByText(/context$/)).toBeNull();
  });

  /**
   * UX5 U72 and D150 §2.4: the Agents menu's *Usage* opens the agent's page at its usage, open and in view once it is
   * drawn, and says it has, so a later visit opens at the top again.
   */
  it('opens at the part a door names, once that part is drawn', async () => {
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
      place(() => {}, { part: 'usage', onAnchored: anchored });

      await waitFor(() => expect(scrolled).toHaveBeenCalledWith('agents-usage'));
      expect(anchored).toHaveBeenCalled();
      expect(await screen.findByText('9,000 context')).toBeTruthy();
    } finally {
      Element.prototype.scrollIntoView = original;
    }
  });

  /** A door naming what agents may do and no agent opens the agent Daoris hands the rules file (D150 §2.4). */
  it('opens the agent that takes the rules for a door that names what agents may do', async () => {
    const RULES = {
      path: 'C:/somewhere/.daoris/permissions.json',
      defaults: [{ id: 'tree-guard', list: 'deny', rules: [], why: 'keeps writes in the tree', on: true }],
      scopes: [{ scope: 'machine', allow: ['Bash(npm test)'], ask: [], deny: [] }],
      proposals: [],
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') {
        return { ...ROSTER, harnesses: [ROSTER.harnesses[1], { ...ROSTER.harnesses[0], takesRules: true }] };
      }
      if (type === 'RULES') return RULES;
      return WIRING;
    });
    place(() => {}, { first: null, part: 'rules' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Claude Code' })).toBeTruthy();
    expect(await screen.findByRole('button', { name: 'Hide What it may do' })).toBeTruthy();
    expect(screen.getByText('Bash(npm test)')).toBeTruthy();
  });

  it('totals what each account carried, and names the one with no account of Daoris’s own', async () => {
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
    place();

    expect(await screen.findByText('3 sessions measured')).toBeTruthy();
    await unfold('Usage');
    expect(screen.getByText('60,000 context')).toBeTruthy();
    const usage = screen.getByRole('region', { name: 'Usage' });
    expect(within(usage).getByText("The tool's own sign-in")).toBeTruthy();
    expect(within(usage).getByText(/Measured, not billed/).className).not.toMatch(/\bmax-w-/);
    // 🔴 No price is claimed anywhere — Daoris does not know what a token costs (D24).
    expect(screen.queryByText(/[$£€]/)).toBeNull();
  });

  /** The managed toolchain (TOOL2/D57): absent means PATH, and the door says so rather than a blank. */
  it('says a door runs from PATH until something is pinned', async () => {
    place();

    await unfold('Ways in');
    expect(screen.getByText('runs from PATH')).toBeTruthy();
    expect(screen.queryByText(/^pinned /)).toBeNull();
  });

  /** UX5 U56: a way in that is not there does not run from PATH yet. */
  it('says a missing way in will run from PATH once it is there', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: ROSTER.harnesses.map((door) => (door.harness === 'codex' ? { ...door, pinnable: true } : door)) }
      : WIRING));
    place(() => {}, { first: 'codex' });

    expect(await screen.findByText('not on PATH yet')).toBeTruthy();
  });

  it('pinning installs that version and pins to it, in one action', async () => {
    place();

    await unfold('Ways in');
    await userEvent.click(screen.getByRole('button', { name: 'Pin a version' }));
    const version = await screen.findByLabelText('version of claude-code to pin');
    await userEvent.type(version, '1.2.3');
    await userEvent.click(screen.getByRole('button', { name: 'Pin' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'pin', profile: undefined, version: '1.2.3' },
    });
  });

  it('offers nothing to press until a version is typed', async () => {
    place();

    await unfold('Ways in');
    await userEvent.click(screen.getByRole('button', { name: 'Pin a version' }));
    expect(screen.getByRole('button', { name: 'Pin' })).toBeDisabled();
  });

  /** 🔴 A door that declares no package has no version to fetch, so the control is absent rather than refusing. */
  it('offers no pin at all for a door that cannot be pinned', async () => {
    place(() => {}, { first: 'codex' });

    expect(await screen.findByRole('button', { name: 'Install' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Pin a version' })).toBeNull();
  });

  /** 🔴 A pin naming a version nobody installed refuses every spawn, so the page says so, with the way back. */
  it('a pin with nothing installed at it reads as missing, not as in force', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], pinned: '9.9.9', managed: null }] }
      : WIRING));
    place();

    await unfold('Ways in');
    expect(screen.getByText(/pinned 9\.9\.9 — not installed/)).toBeTruthy();
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
    place();

    await unfold('Ways in');
    expect(screen.getByText('pinned 1.2.3')).toBeTruthy();
    expect(screen.getByText(/toolchain[\\/]claude-code[\\/]1\.2\.3/)).toBeTruthy();
  });
});

/**
 * What it may do (PERM1, PERM2; D150 §3.1): Daoris's defaults, the rules for every session on this machine and the
 * proposals, on the page of the agent Daoris hands the rules file, each press the terminal's `daoris agent rules`. Moved
 * here from Settings → Permissions' suite with UX6e.
 */
describe('what it may do', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [{
      harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', present: true, version: 'claude 9.9.9', problem: null,
      machineDefault: null, pinned: null, managed: null, pinnable: true, ownLogin: 'in', takesRules: true, profiles: [],
    }],
  };
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [
      { id: 'connector', list: 'allow', rules: ['mcp__daoris-knowledge__quest_respond'], why: 'The connector.', on: true },
      { id: 'no-push', list: 'deny', rules: ['Bash(git push:*)'], why: "A push stays the person's (D37).", on: true },
    ],
    scopes: [
      { scope: 'machine', allow: ['Bash(npm test:*)'], ask: [], deny: [] },
      { scope: 'repository', name: 'engine', allow: ['Bash(make:*)'], ask: [], deny: [] },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('holds the defaults and the rules for every session, folded to a line, and leaves a repository’s to its own home', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'RULES' ? RULES : WIRING));
    place();

    expect(await screen.findByText("Daoris's 2 defaults on · 1 rule of yours")).toBeTruthy();
    await unfold('What it may do');
    expect(screen.getByRole('listitem', { name: 'no-push' })).toBeTruthy();
    expect(within(screen.getByRole('list', { name: 'Every session on this machine' })).getByText('Bash(npm test:*)')).toBeTruthy();
    expect(screen.queryByText('Bash(make:*)')).toBeNull();
  });

  it('a default switched lands on the bridge as the action a terminal has', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'RULES' || type === 'RULE_ACTION' ? RULES : WIRING));
    const notify = vi.fn();
    place(notify);

    await unfold('What it may do');
    await userEvent.click(within(screen.getByRole('listitem', { name: 'no-push' })).getByRole('checkbox'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'default', id: 'no-push', on: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('The default no-push is off on this machine.'));
  });

  /** PERM2 (D74): a proposal waiting opens the section, and the person's answer lands as `daoris agent rules accept` would. */
  it('a proposal waiting opens the section, and one accepted lands on the bridge by its id', async () => {
    const proposed = {
      ...RULES,
      proposals: [{
        id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
        why: 'The docs it needs are on the web.', session: 'i9n8t7k6', proposed: '2026-09-24T11:00:00Z',
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'RULES' || type === 'RULE_PROPOSAL' ? proposed : WIRING));
    const notify = vi.fn();
    place(notify);

    const row = await screen.findByRole('listitem', { name: 'proposal #p0000002' });
    await userEvent.click(within(row).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Accepted: allow WebFetch for every session on this machine. Sessions started from now on are handed it.'));
  });

  /** A section appears only where the agent has that concept (§5.1): no rules where Daoris hands the agent none. */
  it('is absent on an agent Daoris hands no rules file', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesRules: false }] }
      : type === 'RULES' ? RULES : WIRING));
    place();

    await screen.findByRole('heading', { level: 1, name: 'Claude Code' });
    expect(screen.queryByRole('button', { name: 'Show What it may do' })).toBeNull();
  });
});

/**
 * How each agent's accounts are used (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6), on the agent's page: each
 * account's cool-off with *Try now*, its list with *Use*, a default the list would refuse not offered, the tool's own
 * sign-in said while starts run on it, and the terms. Each press is the terminal's `daoris agent profile order|use|ready`.
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
    place(notify);

    const personal = await screen.findByRole('listitem', { name: 'personal' });
    expect(await within(personal).findByText(/^cooling until /)).toBeTruthy();
    expect(within(personal).getByText(/1 of Daoris's sessions running · nothing said yet/)).toBeTruthy();
    await userEvent.click(within(personal).getByRole('button', { name: 'try personal now' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'ready', profile: 'personal' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^personal is offered again\./)));
  });

  it('turns Use on through the terminal\'s profile order, and offers no default the list would refuse', async () => {
    invoke.mockImplementation(answer(ROSTER, ACCOUNTS));
    place();

    await unfold('How accounts are used');
    await userEvent.click(await screen.findByRole('checkbox', { name: 'use work for This machine' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'order', accounts: ['personal', 'work'] },
    });
    // `work` is outside the machine's list, so *Use by default* is not offered on it.
    expect(within(await more('work')).queryByRole('menuitem', { name: 'Use by default' })).toBeNull();
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
    place();

    expect(await screen.findByText(/^Sessions run on your own sign-in, someone@example\.invalid: .+ It is cooling until /)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'try someone@example.invalid now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'ready', own: true },
    });
  });

  /** TOOL6e: each scope says which account its next start takes and why, with what holds the others. */
  it('says which account the next start takes and why', async () => {
    const next = {
      agents: [{
        ...ACCOUNTS.agents[0]!,
        scopes: [{
          ...MACHINE, list: ['personal', 'work'],
          next: { account: 'work', reason: 'onlyReady', over: null, when: null, others: [{ account: 'personal', hold: 'cooling', until }] },
        }],
      }],
    };
    const named = { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0]!, profiles: [ROSTER.harnesses[0]!.profiles[0]!, { ...ROSTER.harnesses[0]!.profiles[1]!, account: 'work@example.invalid' }] }] };
    invoke.mockImplementation(answer(named, next));
    place();

    await unfold('How accounts are used');
    expect(await screen.findByText('The next start takes work@example.invalid: it is the only account here that is ready.')).toBeTruthy();
    expect(screen.getByText(/^personal is cooling until .+\.$/)).toBeTruthy();
    expect(screen.getByText(/^Claude Code's own sign-in, the account it uses at your terminal, carries none of these starts/)).toBeTruthy();
  });

  /** Each workspace on this machine's accounts or its own (D130 §3.2), moved here from Settings (D150 §3.1). */
  it('says each workspace on this machine’s accounts or its own, folded to a line', async () => {
    const own = {
      agents: [{
        ...ACCOUNTS.agents[0]!,
        scopes: [MACHINE, { ...MACHINE, workspace: 'orbit', default: 'work', list: ['work'], begins: 'work' }],
      }],
    };
    invoke.mockImplementation(answer(ROSTER, own));
    place();

    expect(await screen.findByText("default: this machine's · orbit: its own (work)")).toBeTruthy();
    await unfold('Workspaces');
    expect(screen.getByRole('radiogroup', { name: 'Accounts it may use · orbit' })).toBeTruthy();
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
    place(notify);

    await unfold('How accounts are used');
    await userEvent.click(await screen.findByRole('checkbox', { name: 'use work for This machine' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      expect.stringMatching(/^This machine's default for claude-code is personal, and the list would not hold it/), 'error'));
  });
});
