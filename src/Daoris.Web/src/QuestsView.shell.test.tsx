import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient } from '@tanstack/react-query';

// Quests in SHELL mode: a driven quest's stop, and the grant and retry the driver's holds offer on a
// quest's page (FRAME1d; a drawer until then). Moved from `shell.test.tsx` with MOD3: tests follow their code.

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

import { OverviewView } from './OverviewView';
import { chooseRow, QuestsView } from './test/questsView';
import { ShellSignals } from './ShellSignals';
import { keys } from './queries';
import { DRIVER_STATE, respond, show } from './test/shellHarness';

describe('a driven quest in the shell', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  it('a running session offers stop, and stop names the session', async () => {
    show(<QuestsView notify={() => {}} />);

    const page = await chooseRow('Expose a streaming budget');
    await userEvent.click(within(page).getByRole('button', { name: 'Stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });
});

/**
 * Trusting a folder the driver is holding (D73). The agent ignores a folder's own permissions until a
 * person trusts it there, so the driver holds rather than spend a session that could not take its
 * quest — and says so as a FACT beside its sentence: the folder, the agent's own file, what it held.
 * The screen offers exactly that grant, where the hold is shown, and writes it only on the press.
 */
describe('trusting a folder the driver is holding (D73)', () => {
  const HOLD = {
    folder: 'C:/somewhere/engine',
    trustFile: 'C:/somewhere/data/harnesses/claude-code/work/.claude.json',
    quest: 'abc123',
  };
  const GRANTED = {
    folder: HOLD.folder, key: 'C:/somewhere/engine', changed: true, verified: true,
    message: 'Trusted `C:/somewhere/engine` for this agent: its own `permissions.allow` applies there now, and the driver looks again.',
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/asks') ? Response.json([]) : respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'TRUST_FOLDER' ? GRANTED : DRIVER_STATE));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  const holding = (holds: unknown[] = [HOLD]) => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.untrusted, holds);
    return client;
  };

  it('a tick\'s holds land where the views read them, replaced whole', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    show(<ShellSignals notify={() => {}} />, client);

    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: [], untrusted: [HOLD] });
    expect(client.getQueryData(keys.untrusted)).toEqual([HOLD]);

    // A shell older than the grant sends no field at all: nothing is held, rather than a stale list.
    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: [] });
    expect(client.getQueryData(keys.untrusted)).toEqual([]);
  });

  it('the quest the driver holds offers the grant on its page, and writes it only on the press', async () => {
    const notify = vi.fn();
    show(<QuestsView notify={notify} />, holding());

    const page = await chooseRow('Expose a streaming budget');
    await userEvent.click(within(page).getByRole('button', { name: 'Trust this folder…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'TRUST_FOLDER', expect.anything());

    expect(within(page).getByText(HOLD.trustFile)).toBeInTheDocument();
    await userEvent.click(within(page).getByRole('button', { name: 'Trust this folder' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TRUST_FOLDER', {
      payload: { folder: HOLD.folder, trustFile: HOLD.trustFile },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(GRANTED.message, 'ok'));
  });

  /**
   * RETRY1 (D50): a quest parked by its strikes was retried only from a terminal (`daoris driver
   * retry`). The page had the other half ready, and nothing rendered it. The retry is offered where
   * the parked quest's sentence is read, and nowhere a quest is not parked.
   */
  it('a quest parked by its strikes offers the retry on its page, and retries on the press', async () => {
    const notify = vi.fn();
    const client = holding([]);
    client.setQueryData(keys.considered, [{
      quest: 'abc123', repository: 'engine', verdict: 'Exhausted',
      reason: '3 sessions failed on this quest, so it is parked. `daoris driver retry abc123` starts it again.',
    }]);
    show(<QuestsView notify={notify} />, client);

    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).getByText(/3 sessions failed on this quest/)).toBeInTheDocument();
    await userEvent.click(within(page).getByRole('button', { name: 'Try again' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'abc123' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('#abc123')));
  });

  it('a quest sitting for any other reason offers no retry', async () => {
    const client = holding([]);
    client.setQueryData(keys.considered, [{
      quest: 'abc123', repository: 'engine', verdict: 'NotDrivable', reason: 'engine is not driven on this machine.',
    }]);
    show(<QuestsView notify={() => {}} />, client);

    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('a quest nothing holds for trust offers no grant', async () => {
    show(<QuestsView notify={() => {}} />, holding([]));

    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).queryByRole('button', { name: 'Trust this folder…' })).toBeNull();
  });

  it('a held folder waits in *What needs you*, and its row opens the grant', async () => {
    const trust = vi.fn();
    show(<OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={() => {}} doors={{ trust }} />, holding());

    const band = await screen.findByRole('region', { name: 'What needs you' });
    await userEvent.click(await within(band).findByRole('button', { name: /C:\/somewhere\/engine/ }));

    expect(trust).toHaveBeenCalledWith(expect.objectContaining({
      kind: 'trust', trust: { folder: HOLD.folder, trustFile: HOLD.trustFile },
    }));
  });

  /** PERM2 (D74): a widening an agent proposed waits on the person, read from the machine's rules. */
  it('a widening the driver is holding waits in *What needs you*, and its row is a door', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'RULES'
      ? {
        path: 'C:/somewhere/data/permissions.json', defaults: [], scopes: [],
        proposals: [{
          id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
          why: 'The docs it needs are on the web.', session: 'i9n8t7k6', proposed: '2026-09-24T11:00:00Z',
        }],
      }
      : DRIVER_STATE));
    const rule = vi.fn();
    show(<OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={() => {}} doors={{ rule }} />);

    const band = await screen.findByRole('region', { name: 'What needs you' });
    await userEvent.click(await within(band).findByRole('button', { name: /allow WebFetch for every session on this machine/ }));

    expect(rule).toHaveBeenCalledWith(expect.objectContaining({ kind: 'rule', id: 'p0000002', where: 'session i9n8t7k6' }));
  });
});
