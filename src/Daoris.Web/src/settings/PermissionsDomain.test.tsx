import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Permissions domain in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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
import { respond, show, WIRING } from '../test/shellHarness';

/**
 * What agents may do (PERM1, D72): the machine's `permissions.json` over the bridge, and every change
 * the screen's half of `daoris agent rules` (D50). Shaped as the wire carries it — the machine's scope
 * with no `name` and a clean file with no `problem`, because the bridge leaves a null out.
 */
describe('the rules card', () => {
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [
      { id: 'connector', list: 'allow', rules: ['mcp__daoris-knowledge__quest_respond'], why: 'The connector.', on: true },
      { id: 'no-push', list: 'deny', rules: ['Bash(git push:*)'], why: "A push stays the person's (D37).", on: true },
    ],
    scopes: [
      { scope: 'machine', allow: [], ask: [], deny: [] },
      { scope: 'repository', name: 'engine', allow: ['Bash(make:*)'], ask: [], deny: [] },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_ACTION' ? RULES : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the defaults and each scope from the machine\'s own file, asking no service', async () => {
    show(<SettingsView notify={() => {}} section="permissions" />);

    expect(await screen.findByText('C:/somewhere/data/permissions.json')).toBeTruthy();
    expect(screen.getByRole('listitem', { name: 'no-push' })).toBeTruthy();
    expect(within(screen.getByRole('list', { name: 'repository engine' })).getByText('Bash(make:*)')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULES', {});
  });

  it('a default switched and a rule removed land on the bridge as the actions a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const push = await screen.findByRole('listitem', { name: 'no-push' });
    await userEvent.click(within(push).getByRole('checkbox'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'default', id: 'no-push', on: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('The default no-push is off on this machine.'));

    await userEvent.click(screen.getByRole('button', { name: 'remove Bash(make:*)' }));
    expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'remove', rule: 'Bash(make:*)', scope: 'repository', name: 'engine' } });
  });

  /** PERM2 (D74): the person's answer to an agent's proposal lands as `daoris agent rules accept|decline` would. */
  it('a proposal accepted on the screen lands on the bridge by its id, and says what changed', async () => {
    const proposed = {
      ...RULES,
      proposals: [{
        id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
        why: 'The docs it needs are on the web.', session: 'i9n8t7k6', proposed: '2026-09-24T11:00:00Z',
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_PROPOSAL' ? proposed : WIRING));
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const row = await screen.findByRole('listitem', { name: 'proposal #p0000002' });
    await userEvent.click(within(row).getByRole('button', { name: 'accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Accepted: allow WebFetch for every session on this machine. Sessions started from now on are handed it.'));
  });

  /** A shell older than the rules answers something else to a question it never heard: no card, no blank page. */
  it('an older shell gets no card', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'RULES' ? undefined : WIRING));
    show(<SettingsView notify={() => {}} section="permissions" />);

    // The machine's domains appear once the shell has answered, and Permissions is the one open.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Permissions' })).toHaveAttribute('aria-current', 'page'));
    expect(screen.queryByText('The rules file')).toBeNull();
    expect(screen.queryByText('Reading and writing across repositories')).toBeNull();
  });
});

/**
 * READ1 (D107): reading and writing across, the screen's half of `daoris driver across` (D50) — each change
 * lands on the bridge as the verb a terminal has, and what the driver answered is what the card shows.
 */
describe('the reading and writing across card', () => {
  const ACROSS = {
    repositories: [
      { repository: 'engine', workspace: 'default', checkout: true, read: true, source: 'default', writesTo: [] },
      { repository: 'plugins', workspace: 'default', checkout: true, read: true, source: 'default', writesTo: ['engine'] },
    ],
  };
  const STATE = { drivable: [], holds: [], running: [], workspaceReadAcross: [] };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'ACROSS') return ACROSS;
      if (type === 'STATE' || type === 'SET_READ_ACROSS' || type === 'SET_WRITE_ACROSS') return STATE;
      return WIRING;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('switches a checkout\'s reading off, as `daoris driver across <repository> read off` does, and says what it means', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const engine = await screen.findByRole('radiogroup', { name: "Reading engine's checkout" });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACROSS', {});
    await userEvent.click(within(engine).getByRole('radio', { name: 'off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_READ_ACROSS', { payload: { repository: 'engine', read: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'engine: its checkout is read by no agent outside it. A session already running keeps what it began with.'));
  });

  it('takes a relationship back, as `daoris driver across <repository> write-to <other> --clear` does', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    await userEvent.click(await screen.findByRole('button', { name: 'stop plugins writing into engine' }));
    expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SET_WRITE_ACROSS', { payload: { repository: 'plugins', to: 'engine', allow: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Sessions in plugins no longer write into engine; a change needed there is a quest again.'));
  });
});
