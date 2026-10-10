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
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
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
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, chosen, onChoose: setChosen, notify, part, onAnchored });
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

  /** ROSTER1 (§5.3): an account's ⋯ reads that one account again, on the press, and nothing else. */
  it('reads one account again from its own ⋯, naming that account alone', async () => {
    place();
    await screen.findByText('claude 9.9.9');

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Read again' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'HARNESSES', { payload: { refresh: true, agent: 'claude-code', profile: 'work' } }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', { payload: { refresh: true, agent: 'claude-code' } });
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
    // `work` reads signed out, and a workspace's default names it, so it holds work and wears open's hue. Beside the word,
    // the time alone (D152 §4.2): the column and the list's head say what it is.
    expect(within(work).getByText('signed out')).toBeTruthy();
    expect(within(work).getByText(/\d{2}:\d{2}$/)).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'spare' })).getByText('never read')).toBeTruthy();
    // Your own sign-in is asked only at a press (TOOL6g): unknown, never read, never signed in.
    const own = screen.getByRole('listitem', { name: 'Your own sign-in' });
    expect(within(own).getByText('unknown')).toBeTruthy();
    expect(within(own).getByText('never read')).toBeTruthy();
  });

  /**
   * D152 §4.2: an account reads unknown on the install while a list held it, offered nothing on its row, and the owner pressed
   * the header's sign-in and made a new account (ACCT1). Unknown gets *Read*, that account alone; signed out gets *Sign in*,
   * to that account; each is loud where a list holds it.
   */
  it('gives each row the one act its state asks for: Read when unknown, Sign in when signed out, to that account', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          profiles: [...ROSTER.harnesses[0]!.profiles!, { name: 'spare', home: 'C:/somewhere/.daoris/harnesses/claude-code/spare', login: 'unknown', read: null }],
        }],
      }
      : WIRING));
    place();

    const spare = await screen.findByRole('listitem', { name: 'spare' });
    await userEvent.click(within(spare).getByRole('button', { name: 'Read spare' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'HARNESSES', { payload: { refresh: true, agent: 'claude-code', profile: 'spare' } }));

    const work = screen.getByRole('listitem', { name: 'work' });
    expect(within(work).queryByRole('button', { name: 'Read work' })).toBeNull();
    await userEvent.click(within(work).getByRole('button', { name: 'Sign in to work' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'work' },
    }));
  });

  /**
   * AGENTS2: every agent this build knows is listed, installed or not. One not installed says so and carries its Install
   * beside the row, and the list has no ⋯ to unfold it: Codex went unseen behind one.
   */
  it('lists every agent this build knows, installed or not, with no fold for those not installed', async () => {
    place();

    const list = await screen.findByRole('list', { name: 'Agents' });
    expect(within(list).getByText('2 accounts · 1 signed out')).toBeTruthy();
    expect(within(list).getByText('codex')).toBeTruthy();
    expect(within(list).getByText('not installed')).toBeTruthy();
    const install = within(list).getByRole('button', { name: 'Install codex' });
    expect(install.closest('button[aria-current]')).toBeNull();
    expect(within(list).queryByRole('button', { name: 'Install Claude Code' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'More actions' })).toBeNull();
    expect(screen.queryByRole('menuitemcheckbox', { name: /not installed/ })).toBeNull();
  });

  /**
   * AGENTS2: the row's Install opens the agent and runs its own installer there, once, so it streams under its ways in as
   * the page's own Install does; the row's right-click offers the same press (CTX1).
   */
  it('installs an agent from its row: the agent opens and its own installer runs once', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'codex', action: 'install', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    place();
    render(<ContextMenus doors={{ copy: vi.fn() }} />);

    const list = await screen.findByRole('list', { name: 'Agents' });
    rightClick(within(list).getByText('codex'));
    expect(await menuActs('Actions for codex')).toContain('Install');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    await userEvent.click(within(list).getByRole('button', { name: 'Install codex' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'codex' })).toBeTruthy();
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    }));
    expect(invoke.mock.calls.filter(([, type]) => type === 'HARNESS_ACTION')).toHaveLength(1);
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

    // D152 §4.6: its page heads with its own installer, the one *Ways in* runs too.
    const head = screen.getByRole('heading', { level: 1, name: 'codex' }).closest('header')!;
    await userEvent.click(within(head).getByRole('button', { name: 'Install' }));
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
    await userEvent.click(within(work).getByRole('button', { name: 'Sign in to work' }));

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

    await userEvent.click(within(await screen.findByRole('listitem', { name: 'work' })).getByRole('button', { name: 'Sign in to work' }));
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
    expect(within(personal).queryByRole('button', { name: /^Sign in/ })).toBeNull();
    const menu = await more('personal');
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Sign in again' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'personal' },
    });
  });

  /**
   * 🔴 The account a person actually has — the tool's own configuration home — is the last row (D152 §4.3), named *Your own
   * sign-in*, its explanation on its ⓘ; Daoris never signs into it: its ⋯ names no sign-in, and *Use by default* clears the
   * machine's default.
   */
  it('your own sign-in is the last row, takes no sign-in from here, and Use by default clears the default', async () => {
    place();

    const own = await screen.findByRole('listitem', { name: 'Your own sign-in' });
    const rows = within(own.closest('ul')!).getAllByRole('listitem');
    expect(rows.at(-1)).toBe(own);
    expect(within(own).getByText('signed in')).toBeTruthy();
    expect(within(own).queryByRole('button', { name: /^Sign in/ })).toBeNull();
    expect(within(own).getByRole('note', { name: /^Sessions run on your own sign-in/ })).toBeTruthy();
    const menu = await more('Your own sign-in');
    expect(within(menu).queryByRole('menuitem', { name: /Sign in/ })).toBeNull();
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'Use by default' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default' },
    });
  });

  /** D152 §4.3: *Runs for* says the workspaces that start on your own sign-in because they name no account; no chip. */
  it('says which workspaces run on your own sign-in when nothing is named', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null, profiles: [] }] }
      : WIRING));
    place();

    const own = await screen.findByRole('listitem', { name: 'Your own sign-in' });
    expect(within(own).getByText('default')).toBeTruthy();
    expect(screen.queryByText('used by sessions')).toBeNull();
  });

  /**
   * 🔴 An account is made by SIGNING IN (D66 §3), from *Add an account…* (D152 §4.5): where an account reads signed out, its
   * own *Sign in* is offered first, so the lists that hold it keep it (ACCT1); *Add a new account* starts the sign-in; its
   * end names who signed in and asks the account's name, who signed in offered, and the lists it joins.
   */
  it('Add an account… offers a signed-out account’s own sign-in first, then makes a new one and asks its name and lists', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await screen.findByText('claude 9.9.9');
    await userEvent.click(screen.getByRole('button', { name: 'Add an account…' }));
    const back = screen.getByRole('region', { name: 'Add an account' });
    expect(within(back).getByText(/^Signing an account back in\? Use its row/)).toBeTruthy();
    expect(within(back).getByRole('button', { name: 'Sign in to work' })).toBeTruthy();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());

    await userEvent.click(within(back).getByRole('button', { name: 'Add a new account' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
    expect(await screen.findByText('Signing in to another Claude Code account')).toBeTruthy();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'acct-1a2b3c4d', exitCode: 0, problem: null,
        account: 'someone@example.invalid', kept: true, places: [],
      });
    });

    expect(notify).toHaveBeenCalledWith(
      "Signed in as someone@example.invalid. No list holds it yet, so no start runs on it: its agent's page asks where it runs.");
    await waitFor(() => expect(screen.queryByText('Signing in to another Claude Code account')).toBeNull());
    const asking = screen.getByRole('region', { name: 'Add an account' });
    expect(within(asking).getByText('Signed in as someone@example.invalid.')).toBeTruthy();
    expect((within(asking).getByRole('textbox', { name: /Its name/ }) as HTMLInputElement).value).toBe('someone@example.invalid');
  });

  /** With no account signed out or unknown there is nobody to bring back: *Add an account…* starts the sign-in at once. */
  it('Add an account… starts the sign-in at once where no account reads signed out or unknown', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], profiles: [ROSTER.harnesses[0]!.profiles![0]!] }] }
        : WIRING;
    });
    place();

    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
  });

  /**
   * D152 §4.5's last step: the name in its field is kept (ACCT2's `profile-rename`), then the lists ticked are joined
   * (ACCT1's `profile-join`, null for this machine's list), each the terminal's twin, and the question goes.
   */
  it('keeps the new account’s name and puts it in the lists ticked, at the add flow’s end', async () => {
    const ACCOUNTS = {
      agents: [{
        agent: 'claude-code', speaks: true, own: {},
        accounts: [{ name: 'personal', running: 0 }, { name: 'work', running: 0 }],
        scopes: [
          { workspace: null, default: 'personal', list: [], begins: 'personal', use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [] },
          { workspace: 'orbit', default: 'work', list: ['work'], begins: 'work', use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [] },
        ],
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') {
        const action = options?.payload?.action;
        if (action === 'profile-rename') return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-1a2b3c4d', name: 'spare' };
        if (action === 'profile-join') {
          return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-1a2b3c4d', places: [{ workspace: 'orbit', list: true, default: false }] };
        }
        return { harness: 'claude-code', action, started: true };
      }
      if (type === 'ACCOUNTS') return ACCOUNTS;
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a new account' }));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'acct-1a2b3c4d', exitCode: 0, problem: null,
        account: 'spare@example.invalid', kept: true, places: [],
      });
    });

    const asking = await screen.findByRole('region', { name: 'Add an account' });
    const name = within(asking).getByRole('textbox', { name: /Its name/ });
    await userEvent.clear(name);
    await userEvent.type(name, 'spare');
    expect(within(asking).getByRole('button', { name: 'Add to the lists' })).toBeDisabled();
    await userEvent.click(within(asking).getByRole('checkbox', { name: 'orbit' }));
    // The terminal's twin under the buttons, a word a box (`CodeText`), so read whole from the code span.
    const twins = [...asking.querySelectorAll('code')].map((code) => code.textContent);
    expect(twins).toEqual([
      'daoris agent profile rename claude-code acct-1a2b3c4d spare', 'daoris agent profile join claude-code acct-1a2b3c4d orbit',
    ]);
    await userEvent.click(within(asking).getByRole('button', { name: 'Add to the lists' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'acct-1a2b3c4d', join: ['orbit'] },
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-rename', profile: 'acct-1a2b3c4d', name: 'spare' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('spare runs work in orbit now.'));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Add an account' })).toBeNull());
  });

  /** ACCTEDIT1: the add flow's last step, refused, stays up with the name and the lists as they were and says why in it. */
  it('keeps the add flow’s last step up with its name and lists when its rename is refused, and says why in it', async () => {
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') {
        const action = options?.payload?.action;
        if (action === 'profile-rename') {
          throw Object.assign(new Error('refused'), {
            code: 'DRIVER_REFUSED', parameters: { message: '`work` already names `work` — each `claude-code` account has a name of its own.' },
          });
        }
        return { harness: 'claude-code', action, started: true };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a new account' }));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'acct-1a2b3c4d', exitCode: 0, problem: null,
        account: 'spare@example.invalid', kept: true, places: [],
      });
    });
    const asking = await screen.findByRole('region', { name: 'Add an account' });
    const name = within(asking).getByRole('textbox', { name: /Its name/ });
    await userEvent.clear(name);
    await userEvent.type(name, 'work');
    await userEvent.click(within(asking).getByRole('button', { name: 'Not now' }));

    expect(await within(asking).findByRole('alert')).toHaveProperty(
      'textContent', 'work already names work — each claude-code account has a name of its own.');
    expect(within(asking).getByRole('textbox', { name: /Its name/ })).toHaveProperty('value', 'work');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');
  });

  /** *Not now* joins nothing, keeps the name in the field, and the account says *no workspace* with *Use in a workspace…*. */
  it('Not now leaves the new account in no list, which its row says, with Use in a workspace… as its act', async () => {
    let roster: unknown = ROSTER;
    const ACCOUNTS = {
      agents: [{
        agent: 'claude-code', speaks: true, own: {}, accounts: [],
        scopes: [{ workspace: null, default: 'personal', list: ['personal'], begins: 'personal', use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [] }],
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') {
        const action = options?.payload?.action;
        if (action === 'profile-rename') return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-1a2b3c4d', name: 'spare@example.invalid' };
        if (action === 'profile-join') return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-1a2b3c4d', places: [{ workspace: null, list: true, default: false }] };
        return { harness: 'claude-code', action, started: true };
      }
      if (type === 'ACCOUNTS') return ACCOUNTS;
      return type === 'HARNESSES' ? roster : WIRING;
    });
    place();

    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a new account' }));
    roster = {
      ...ROSTER,
      harnesses: [{
        ...ROSTER.harnesses[0],
        profiles: [...ROSTER.harnesses[0]!.profiles!, {
          name: 'acct-1a2b3c4d', home: 'C:/somewhere/.daoris/harnesses/claude-code/acct-1a2b3c4d', login: 'in',
          account: 'spare@example.invalid', read: READ, places: [], nowhere: true,
        }],
      }],
    };
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'acct-1a2b3c4d', exitCode: 0, problem: null,
        account: 'spare@example.invalid', kept: true, places: [],
      });
    });

    const asking = await screen.findByRole('region', { name: 'Add an account' });
    await userEvent.click(within(asking).getByRole('button', { name: 'Not now' }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Add an account' })).toBeNull());
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.objectContaining({
      payload: expect.objectContaining({ action: 'profile-join' }),
    }));

    // A fresh id never leads its row: who signed in stands for it until the person names it.
    const spare = await screen.findByRole('listitem', { name: 'spare@example.invalid' });
    expect(within(spare).getByText('no workspace')).toBeTruthy();
    await userEvent.click(within(spare).getByRole('button', { name: 'Use spare@example.invalid in a workspace…' }));
    const where = within(spare).getByRole('region', { name: 'Where spare@example.invalid runs work' });
    await userEvent.click(within(where).getByRole('checkbox', { name: "This machine's list" }));
    await userEvent.click(within(where).getByRole('button', { name: 'Add to the lists' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'acct-1a2b3c4d', join: [null] },
    }));
  });

  it('a sign-in that kept nothing says so', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an account…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Add a new account' }));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: null, kept: false,
      });
    });

    expect(notify).toHaveBeenCalledWith('Nobody was signed in, so nothing was kept.', 'error');
  });

  /**
   * CODEXACCT1: Codex's one door is `codex-acp`, which signs its accounts in with `codex login` (the roster's `signsIn`, its
   * agent's flow, AGT7), so Agents → Codex offers *Add an account…* as the terminal's `daoris agent login codex --new` does:
   * a signed-out account's own sign-in first, then a new account on the door, whose end asks its name, since Codex names
   * nobody. Its own sign-in reads as `codex login status` answered it.
   */
  it('Agents → Codex adds an account through its one door, as the terminal does, and asks its name at the end', async () => {
    const CODEX = {
      settingsPath: ROSTER.settingsPath,
      adapter: 'claude-code',
      harnesses: [{
        harness: 'codex-acp', accountOf: 'codex', product: 'Codex', maker: 'OpenAI', wire: 'acp',
        present: true, version: 'codex-acp 1.12.0', problem: null, machineDefault: null, pinned: null, managed: null, pinnable: true,
        signsIn: true, takesKey: false, ownLogin: 'in', ownRead: READ, workspaceDefaults: [],
        profiles: [{ name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/codex/account-1', login: 'out', read: READ }],
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'codex-acp', action: 'login-new', started: true, profile: 'acct-5e6f7a8b' };
      return type === 'HARNESSES' ? CODEX : WIRING;
    });
    const notify = vi.fn();
    place(notify, { first: 'codex' });

    expect(await screen.findByRole('heading', { level: 1, name: 'Codex' })).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'Your own sign-in' })).getByText('signed in')).toBeTruthy();
    // No key: Daoris holds none Codex is measured to take (D67 §1).
    expect(screen.queryByRole('button', { name: 'Add an API key' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Add an account…' }));
    const back = screen.getByRole('region', { name: 'Add an account' });
    expect(within(back).getByRole('button', { name: 'Sign in to account-1' })).toBeTruthy();

    await userEvent.click(within(back).getByRole('button', { name: 'Add a new account' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex-acp', action: 'login-new' },
    });
    expect(await screen.findByText('Signing in to another Codex account')).toBeTruthy();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'codex-acp', action: 'login-new', profile: 'acct-5e6f7a8b', exitCode: 0, problem: null,
        account: null, kept: true, places: [],
      });
    });

    expect(notify).toHaveBeenCalledWith(
      "Signed in — the tool did not say who, so the account is listed as acct-5e6f7a8b. No list holds it yet: its agent's page asks where it runs.");
    const asking = await screen.findByRole('region', { name: 'Add an account' });
    expect(within(asking).getByText('Signed in — the tool did not say who.')).toBeTruthy();
    expect(within(asking).getByRole('textbox', { name: /Its name/ })).toBeTruthy();
  });

  /**
   * D152 §4.2 and ACCT2: one name leads, the account's, the person's where they gave one, with who signed in beside it where
   * the two differ, never one as title and the other faint; a fresh id the person never chose never leads.
   */
  it('leads each row with the account’s name, who signed in beside it, and never a fresh id', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'someone@example.invalid' },
            { name: 'acct-0a1b2c3d', displayName: 'lab', home: 'C:/somewhere/.daoris/harnesses/claude-code/acct-0a1b2c3d', login: 'in', account: 'lab@example.invalid' },
            { name: 'acct-9f8e7d6c', home: 'C:/somewhere/.daoris/harnesses/claude-code/acct-9f8e7d6c', login: 'in', account: 'new@example.invalid' },
          ],
        }],
      }
      : WIRING));
    place();

    const old = await screen.findByRole('listitem', { name: 'account-1' });
    expect(within(old).getByText('someone@example.invalid')).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'lab' })).getByText('lab@example.invalid')).toBeTruthy();
    expect(screen.getByRole('listitem', { name: 'new@example.invalid' })).toBeTruthy();
    expect(screen.queryByText('acct-9f8e7d6c')).toBeNull();
    expect(within(screen.getByRole('listitem', { name: 'Your own sign-in' })).getByText('owner@example.invalid')).toBeTruthy();
  });

  /** ACCT2's rename, from an account's ⋯: the person's name, sent as `daoris agent profile rename` sends it, and said once. */
  it('renames an account from its ⋯, and an emptied name gives it none', async () => {
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') {
        const name = options?.payload?.name;
        return { harness: 'claude-code', action: 'profile-rename', exitCode: 0, profile: 'work', name: name === 'work' ? null : name };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Rename…' }));
    const form = within(screen.getByRole('listitem', { name: 'work' })).getByRole('form', { name: 'Rename work' });
    await userEvent.type(within(form).getByRole('textbox', { name: 'Its name' }), 'office');
    await userEvent.click(within(form).getByRole('button', { name: 'Save the name' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-rename', profile: 'work', name: 'office' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('work is called office now.'));
  });

  /**
   * ACCTEDIT1: a rename stays open until its act lands. Refused, it keeps the name the person typed and says why under it,
   * whole, as a refused start is said in its form (UX5 U68); a retry that lands closes it, and *Never mind* is the one other
   * way out.
   */
  it('keeps a refused rename open with the name typed, says why in it, and closes once a retry lands', async () => {
    let refuse = true;
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESS_ACTION') {
        if (refuse) {
          refuse = false;
          throw Object.assign(new Error('refused'), {
            code: 'DRIVER_REFUSED',
            parameters: { message: '`office` already names `personal` — each `claude-code` account has a name of its own, and an id is one.' },
          });
        }
        return { harness: 'claude-code', action: 'profile-rename', exitCode: 0, profile: 'work', name: options?.payload?.name };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Rename…' }));
    const row = screen.getByRole('listitem', { name: 'work' });
    await userEvent.type(within(row).getByRole('textbox', { name: 'Its name' }), 'office');
    await userEvent.click(within(row).getByRole('button', { name: 'Save the name' }));

    const form = within(row).getByRole('form', { name: 'Rename work' });
    expect(await within(form).findByRole('alert')).toHaveProperty(
      'textContent', 'office already names personal — each claude-code account has a name of its own, and an id is one.');
    expect(within(form).getByRole('textbox', { name: 'Its name' })).toHaveProperty('value', 'office');
    // Said where it was asked, not as a corner toast as well.
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await userEvent.click(within(form).getByRole('button', { name: 'Save the name' }));
    await waitFor(() => expect(within(row).queryByRole('form', { name: 'Rename work' })).toBeNull());
    expect(notify).toHaveBeenCalledWith('work is called office now.');
    expect(invoke.mock.calls.filter(([, type]) => type === 'HARNESS_ACTION')).toHaveLength(2);
  });

  it('a refused rename taken back with Never mind closes, and opens again with no refusal under it', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        throw Object.assign(new Error('refused'), { code: 'DRIVER_REFUSED', parameters: { message: 'not a name a terminal can type.' } });
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    place();

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Rename…' }));
    const row = screen.getByRole('listitem', { name: 'work' });
    await userEvent.type(within(row).getByRole('textbox', { name: 'Its name' }), 'o');
    await userEvent.click(within(row).getByRole('button', { name: 'Save the name' }));
    expect(await within(row).findByRole('alert')).toBeTruthy();
    await userEvent.click(within(row).getByRole('button', { name: 'Never mind' }));
    expect(within(row).queryByRole('form', { name: 'Rename work' })).toBeNull();

    // Asked again, it opens on the account's name as it is, with nothing said under it.
    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Rename…' }));
    expect(within(row).getByRole('textbox', { name: 'Its name' })).toHaveProperty('value', '');
    expect(within(row).queryByRole('alert')).toBeNull();
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
    // UXFIX2: the ask closes once the removal answered, never on the press.
    await waitFor(() => expect(within(work).queryByRole('group', { name: 'remove work' })).toBeNull());
    // An account edit is not a door's work: nothing streams under *Ways in* for it.
    await new Promise((settle) => setTimeout(settle, 50));
    expect(screen.queryByText(/its sign-in are gone/)).toBeNull();
  });

  /**
   * UXFIX2 (the second-opinion review): the remove's ask is the one inline confirmation. What it deletes takes the focus
   * and describes *Remove account*; a refused removal is said inside the ask, word for word, which stays open, where it
   * closed on the press and refused in a toast.
   */
  it('Remove… takes the focus to what it deletes, and says a refused removal inside its ask', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        throw Object.assign(new Error('refused'), {
          code: 'DRIVER_REFUSED', parameters: { message: 'work is the default of aurora; choose another first.' },
        });
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Remove…' }));
    const work = screen.getByRole('listitem', { name: 'work' });
    const says = within(work).getByText(/deletes the account from this machine, sign-in included/);
    await waitFor(() => expect(says).toHaveFocus());
    expect(within(work).getByRole('button', { name: 'Remove account' })).toHaveAccessibleDescription(says.textContent!);

    await userEvent.click(within(work).getByRole('button', { name: 'Remove account' }));
    const ask = within(work).getByRole('group', { name: 'remove work' });
    expect(await within(ask).findByRole('alert')).toHaveTextContent('work is the default of aurora; choose another first.');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('choose another first'), 'error');
  });

  it('Remove… can be taken back before it deletes anything, giving the focus back to the account’s ⋯', async () => {
    place();

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Remove…' }));
    const work = screen.getByRole('listitem', { name: 'work' });
    await waitFor(() => expect(within(work).getByText(/deletes the account from this machine/)).toHaveFocus());
    await userEvent.click(within(work).getByRole('button', { name: 'Never mind' }));

    expect(within(work).queryByRole('button', { name: 'Remove account' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
    expect(screen.getByRole('button', { name: 'More for work' })).toHaveFocus();
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
   * 🔴 A work account for the work circle (D49 §4): the row's *Runs for* says the workspaces that run on it, and a workspace's
   * default is chosen from the ones this machine has, in the account's ⋯.
   */
  it('an account can be made one workspace’s default, and the row says which workspaces run on it', async () => {
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
    expect(within(work).getByText('orbit')).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'personal' })).getByText('this machine')).toBeTruthy();

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

    await userEvent.click(within(await more('Your own sign-in')).getByRole('menuitem', { name: 'Clear the default in orbit' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', workspace: 'orbit' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "orbit names no account for claude-code now: its sessions run as this machine's default, personal."));
  });

  /**
   * ACCTNAME1 (D152 §4.2, D125's ACCT2 note): a sign-in's end and a default's edit say the account by the person's name,
   * looked up on the roster as it is said, never who signed in or its id.
   */
  it('says a sign-in’s end and a default’s edit by the account’s name', async () => {
    const notify = vi.fn();
    const named = {
      ...ROSTER,
      harnesses: [{
        ...ROSTER.harnesses[0],
        profiles: [
          ...ROSTER.harnesses[0]!.profiles!,
          { name: 'acct-3f9c1a2b', displayName: 'lab', home: 'C:/somewhere/.daoris/harnesses/claude-code/acct-3f9c1a2b', login: 'out', account: 'you@lab.example', read: READ },
        ],
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'HARNESSES') return named;
      if (type === 'HARNESS_ACTION' && options?.payload?.action === 'login') return { harness: 'claude-code', action: 'login', started: true };
      if (type === 'HARNESS_ACTION' && options?.payload?.action === 'profile-default') {
        return { harness: 'claude-code', action: 'profile-default', exitCode: 0, default: { workspace: null, account: 'acct-3f9c1a2b', from: 'machine' } };
      }
      return WIRING;
    });
    place(notify);

    const lab = await screen.findByRole('listitem', { name: /^lab/ });
    await userEvent.click(within(lab).getByRole('button', { name: 'Sign in to lab' }));
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'acct-3f9c1a2b', account: 'you@lab.example', exitCode: 0, problem: null,
      });
    });
    expect(notify).toHaveBeenCalledWith('lab is signed in — sessions can run as it.');

    await userEvent.click(within(await more('lab')).getByRole('menuitem', { name: 'Use by default' }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('This machine runs claude-code as lab by default.'));
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
    // Said where it was added, in the step that asks its name (ACCTUX3), rather than in a toast.
    const asking = await screen.findByRole('region', { name: 'Add an API key' });
    expect(within(asking).getByText('Added the API key …wxyz.')).toBeTruthy();
    expect(notify).not.toHaveBeenCalled();
    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(container.innerHTML).not.toContain(key);
  });

  /**
   * ACCTUX3: the key's field emptied and closed on the press, so a key the driver refused was gone with the place to say why,
   * the failure ACCTEDIT1 fixed for a rename. Refused, the field stays with the key in it and says why; a retry that lands
   * closes it.
   */
  it('keeps a refused key in its field, says why in it, and closes once a retry lands', async () => {
    const key = 'sk-ant-api03-page-test-wxyz';
    let refuse = true;
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        if (refuse) {
          throw Object.assign(new Error('refused'), {
            code: 'DRIVER_REFUSED', parameters: { message: '`claude-code` takes no key while a sign-in runs.' },
          });
        }
        return { harness: 'claude-code', action: 'key-add', exitCode: 0, profile: 'acct-9c8d7e6f', key: '…wxyz' };
      }
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
        : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    fireEvent.change(screen.getByLabelText('API key for Claude Code'), { target: { value: key } });
    await userEvent.click(screen.getByRole('button', { name: 'Save key' }));

    const form = screen.getByLabelText('API key for Claude Code').closest('form')!;
    expect(await within(form).findByRole('alert')).toHaveProperty('textContent', 'claude-code takes no key while a sign-in runs.');
    expect(screen.getByLabelText('API key for Claude Code')).toHaveProperty('value', key);
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    refuse = false;
    await userEvent.click(within(form).getByRole('button', { name: 'Save key' }));
    await waitFor(() => expect(screen.queryByLabelText('API key for Claude Code')).toBeNull());
    expect(await screen.findByRole('region', { name: 'Add an API key' })).toBeTruthy();
  });

  /**
   * ACCTUX3, the UX7 design §4.5: *a key added asks the same question* as a sign-in's last step, its name and the lists it
   * joins; it ended in a toast, and the account in no list. Left empty, it is called by its key's handle.
   */
  it('asks a key added its name and lists, and keeps them, as a sign-in’s last step does', async () => {
    const ACCOUNTS = {
      agents: [{
        agent: 'claude-code', speaks: true, own: {},
        accounts: [{ name: 'personal', running: 0 }, { name: 'work', running: 0 }],
        scopes: [
          { workspace: null, default: 'personal', list: [], begins: 'personal', use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [] },
          { workspace: 'orbit', default: 'work', list: ['work'], begins: 'work', use: { use: 'goal', keep: null, early: true, near: 90 }, unknown: [], problem: null, near: [] },
        ],
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: Record<string, unknown> }) => {
      if (type === 'ACCOUNTS') return ACCOUNTS;
      if (type === 'HARNESS_ACTION') {
        const action = options?.payload?.action;
        if (action === 'key-add') return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-9c8d7e6f', key: '…wxyz' };
        if (action === 'profile-rename') return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-9c8d7e6f', name: 'spare-key' };
        if (action === 'profile-join') {
          return { harness: 'claude-code', action, exitCode: 0, profile: 'acct-9c8d7e6f', places: [{ workspace: 'orbit', list: true, default: false }] };
        }
      }
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
        : WIRING;
    });
    const notify = vi.fn();
    place(notify);

    await userEvent.click(await screen.findByRole('button', { name: 'Add an API key' }));
    fireEvent.change(screen.getByLabelText('API key for Claude Code'), { target: { value: 'sk-ant-api03-page-test-wxyz' } });
    await userEvent.click(screen.getByRole('button', { name: 'Save key' }));

    const asking = await screen.findByRole('region', { name: 'Add an API key' });
    const name = within(asking).getByRole('textbox', { name: /Its name/ });
    expect(name).toHaveProperty('value', '');
    expect(name).toHaveProperty('placeholder', 'API key …wxyz');
    expect(within(asking).queryByText(/Signed in/)).toBeNull();
    await userEvent.type(name, 'spare-key');
    await userEvent.click(within(asking).getByRole('checkbox', { name: 'orbit' }));
    await userEvent.click(within(asking).getByRole('button', { name: 'Add to the lists' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'acct-9c8d7e6f', join: ['orbit'] },
    }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-rename', profile: 'acct-9c8d7e6f', name: 'spare-key' },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('spare-key runs work in orbit now.'));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Add an API key' })).toBeNull());
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
    await waitFor(() => expect(within(row).queryByRole('group', { name: 'work: model and effort' })).toBeNull());
  });

  /**
   * ACCTUX3: the editor closed on the press, so a save the driver refused lost the choice with the place to say why, the
   * failure ACCTEDIT1 fixed for a rename. Refused, it stays open with the choice made and says why under it.
   */
  it("keeps a refused model save open with the choice made, and says why under it", async () => {
    const CHOICES = { models: ['default', 'sonnet', 'opus'], efforts: ['low', 'medium', 'high', 'xhigh'] };
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SET_AGENT_SETTINGS') {
        throw Object.assign(new Error('refused'), {
          code: 'DRIVER_REFUSED', parameters: { message: '`work`\'s settings file could not be written.' },
        });
      }
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

    expect(await within(row).findByRole('alert')).toHaveProperty('textContent', "work's settings file could not be written.");
    expect(within(row).getByRole('group', { name: 'work: model and effort' })).toBeTruthy();
    expect(within(form).getByRole('combobox', { name: 'model' })).toHaveTextContent('sonnet');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');
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
    expect(within(usage).getByText('Your own sign-in')).toBeTruthy();
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

    expect(await screen.findAllByRole('button', { name: 'Install' })).not.toHaveLength(0);
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
    expect(await within(personal).findByText('cooling')).toBeTruthy();
    // The hold's end, never a reset the column cannot know was reported (ACCTUX1).
    expect(within(personal).getByText(/^until /)).toBeTruthy();
    expect(within(personal).queryByText(/^resets /)).toBeNull();
    // *Now* says what runs on it; *nothing said yet* and *0 sessions* are not said (D152 §2 rule 4).
    expect(within(personal).getByText('1 session')).toBeTruthy();
    expect(within(screen.getByRole('main')).queryByText(/nothing said yet|0 sessions|sessions running/)).toBeNull();
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
    await userEvent.keyboard('{Escape}');
    // The plans and terms fold to one line at the list's foot, which opens the paragraph (D152 §4.4).
    expect(screen.queryByText(/^Each account's own plan and terms apply\./)).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: "Each account's own plan and terms" }));
    expect(screen.getByText(/^Each account's own plan and terms apply\./)).toBeTruthy();
    expect(screen.queryByText(/^Sessions run on your own sign-in/)).toBeNull();
  });

  /** D152 §4.2: an account's ⋯ puts it in a list that does not hold it, and takes it out of one that does. */
  it('adds an account to a list and takes it out of one from its ⋯, each the terminal’s door', async () => {
    invoke.mockImplementation(async (module: string, type: string, args?: { payload?: { action?: string } }) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: args?.payload?.action, exitCode: 0, profile: 'work', places: [] };
      return answer(ROSTER, { agents: [{ ...ACCOUNTS.agents[0]!, scopes: [{ ...MACHINE, list: ['personal', 'work'] }, { ...MACHINE, workspace: 'orbit', default: null, list: ['personal'], begins: 'personal' }] }] })(module, type, args);
    });
    place();

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: "Add to orbit's list" }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-join', profile: 'work', join: ['orbit'] },
    });
    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: "Remove from this machine's list" }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'order', accounts: ['personal'] },
    });
  });

  /**
   * ACCTEDIT1: a row's *Use in a workspace…* stays open until its join lands. Refused, it keeps the lists ticked and says why
   * under them, whole; a retry that lands closes it.
   */
  it('keeps a refused Use in a workspace… open with its lists ticked, says why in it, and closes once a retry lands', async () => {
    const nowhere = {
      agents: [{ ...ACCOUNTS.agents[0]!, scopes: [MACHINE, { ...MACHINE, workspace: 'orbit', list: ['personal'] }] }],
    };
    let refuse = true;
    invoke.mockImplementation(async (module: string, type: string, args?: { payload?: { action?: string } }) => {
      if (type === 'HARNESS_ACTION') {
        if (refuse) {
          refuse = false;
          throw Object.assign(new Error('refused'), {
            code: 'DRIVER_REFUSED',
            parameters: { message: '`orbit` names no `claude-code` account or list of its own, so its starts take this machine\'s list.' },
          });
        }
        // Its default there: the toast still marks no place first, since *first* is a next start the join's answer never names (ACCTUX4b).
        return { harness: 'claude-code', action: 'profile-join', exitCode: 0, profile: 'work', places: [{ workspace: 'orbit', list: true, default: true }] };
      }
      return answer(ROSTER, nowhere)(module, type, args);
    });
    const notify = vi.fn();
    place(notify);

    const work = await screen.findByRole('listitem', { name: 'work' });
    await userEvent.click(await within(work).findByRole('button', { name: 'Use work in a workspace…' }));
    const where = within(work).getByRole('region', { name: 'Where work runs work' });
    await userEvent.click(within(where).getByRole('checkbox', { name: 'orbit' }));
    await userEvent.click(within(where).getByRole('button', { name: 'Add to the lists' }));

    expect(await within(where).findByRole('alert')).toHaveProperty(
      'textContent', 'orbit names no claude-code account or list of its own, so its starts take this machine\'s list.');
    expect(within(where).getByRole('checkbox', { name: 'orbit' }).getAttribute('aria-checked')).toBe('true');
    expect(notify).not.toHaveBeenCalledWith(expect.anything(), 'error');

    await userEvent.click(within(where).getByRole('button', { name: 'Add to the lists' }));
    await waitFor(() => expect(within(work).queryByRole('region', { name: 'Where work runs work' })).toBeNull());
    expect(notify).toHaveBeenCalledWith('work runs work in orbit now.');
  });

  /**
   * ACCTQUOTE1 (D125's ACCTQUOTE1 note): a twin is a command a person pastes into whichever shell they have, so a workspace
   * with a space is in double quotes, a name a shell can take is printed, and one no spelling holds in every shell (`R&D`) is
   * a placeholder rather than a command that runs something else.
   */
  it('spells each argument of the terminal twins for a shell, a name none can take a placeholder', async () => {
    const spaced = {
      agents: [{ ...ACCOUNTS.agents[0]!, scopes: [MACHINE, { ...MACHINE, workspace: 'my team', list: ['personal'] }] }],
    };
    invoke.mockImplementation(answer(ROSTER, spaced));
    place();

    const work = await screen.findByRole('listitem', { name: 'work' });
    await userEvent.click(await within(work).findByRole('button', { name: 'Use work in a workspace…' }));
    const where = within(work).getByRole('region', { name: 'Where work runs work' });
    await userEvent.click(within(where).getByRole('checkbox', { name: 'my team' }));
    expect([...where.querySelectorAll('code')].map((each) => each.textContent))
      .toEqual(['daoris agent profile join claude-code work "my team"']);
    await userEvent.click(within(where).getByRole('button', { name: 'Never mind' }));

    await userEvent.click(within(await more('work')).getByRole('menuitem', { name: 'Rename…' }));
    const form = within(work).getByRole('form', { name: 'Rename work' });
    const twin = () => [...form.querySelectorAll('code')].map((each) => each.textContent);
    await userEvent.type(within(form).getByRole('textbox', { name: 'Its name' }), 'R&D');
    expect(twin()).toEqual(['daoris agent profile rename claude-code work <name>']);
    await userEvent.clear(within(form).getByRole('textbox', { name: 'Its name' }));
    await userEvent.type(within(form).getByRole('textbox', { name: 'Its name' }), "O'Brien");
    expect(twin()).toEqual(['daoris agent profile rename claude-code work "O\'Brien"']);
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

    // Its explanation is its row's ⓘ, kept whole, one press away (D152 §4.3), and no longer a callout before the accounts.
    const own = await screen.findByRole('listitem', { name: 'Your own sign-in' });
    expect(within(own).getByRole('note', { name: /^Sessions run on your own sign-in, someone@example\.invalid: .+ It is cooling until / })).toBeTruthy();
    expect(within(own).getByText('cooling')).toBeTruthy();
    await userEvent.click(within(own).getByRole('button', { name: 'try Your own sign-in now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACCOUNT_USE', {
      payload: { harness: 'claude-code', action: 'ready', own: true },
    });
  });

  /**
   * CODEXUSE3: what your own sign-in last said of its windows, read at a press, is its row's second line, as an account's
   * reading is on its row; and its ⋯ reads it again with the agent's accounts, since the driver has no read of it alone.
   */
  it('says what your own sign-in last said of its windows, and reads it again from its ⋯', async () => {
    const reset = new Date(Date.now() + 3 * 3_600_000).toISOString();
    const week = new Date(Date.now() + 100 * 3_600_000).toISOString();
    const seen = new Date(Date.now() - 20 * 60_000).toISOString();
    const said = {
      agents: [{
        ...ACCOUNTS.agents[0]!,
        own: {
          said: {
            seen,
            windows: [
              { window: 'session', used: 0.01, reset, standing: null, credits: false, seen },
              { window: 'weekly', used: 0.15, reset: week, standing: null, credits: false, seen },
            ],
          },
        },
      }],
    };
    invoke.mockImplementation(answer(ROSTER, said));
    place();

    const own = await screen.findByRole('listitem', { name: 'Your own sign-in' });
    // A cell a window (ACCTUX4): its name, then its exact share.
    expect((await within(own).findByText('1% used')).closest('[data-window]')).toHaveAttribute('data-window', 'session');
    expect(within(own).getByText('15% used').closest('[data-window]')).toHaveAttribute('data-window', 'weekly');
    // It is none of the accounts: their rows say only their own readings, here none, so each window unknown and no share.
    const work = screen.getByRole('listitem', { name: 'work' });
    expect(within(work).queryByText(/% used/)).toBeNull();
    expect(within(work).getAllByText('unknown')).toHaveLength(2);

    // The press reads its windows again, so the accounts' files are asked again once it answers, not at the next tick.
    const accountsAsked = () => invoke.mock.calls.filter(([, type]) => type === 'ACCOUNTS').length;
    const before = accountsAsked();
    await userEvent.click(within(await more('Your own sign-in')).getByRole('menuitem', { name: 'Read again' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'HARNESSES', { payload: { refresh: true, agent: 'claude-code' } }));
    await waitFor(() => expect(accountsAsked()).toBeGreaterThan(before));
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
    // An account is named by its name everywhere on the page (ACCT2), an old `account-N`-like id where the person gave none.
    expect(await screen.findByText('The next start takes work: it is the only account here that is ready.')).toBeTruthy();
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
