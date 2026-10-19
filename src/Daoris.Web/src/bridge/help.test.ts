import { createElement, type ReactNode } from 'react';
import { type QueryKey, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear (STATEHUB1h, D169): what the mocked shell publishes after a press reaches it, as the kit's pump would.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async (importOriginal) => {
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    ...(await importOriginal<typeof import('@shenora/react')>()),
    getBridge: () => ({ invoke: published, isAvailable: true }),
    // A shell is here, so a read asks it (HELPMODE1b's `HELP_WIRED`).
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import type { HelpProposal } from '../help/ProposalCard';
import { keys } from '../data/queries';
import { useSetHelpMode } from './driver';
import { HELP_PROPOSALS, helpModeOf, settleBound, useHelpWired, useSettleHelp, wiredAnswer, type HelpSettled } from './help';
import { useStateHub } from './stateHub';

/**
 * WSR7: Ask Daoris's sync card fetches on its first Apply and replays on its second, each as long as the screen's own
 * look and press may take; the bridge's default 30 seconds gave up on the screen's look. Every other Apply is quick.
 */
describe('how long an Apply may take', () => {
  const minutes = (ms: number | undefined) => (ms === undefined ? undefined : ms / 60_000);
  const proposal = (id: string, kind: HelpProposal['kind'], sync?: HelpProposal['sync']): HelpProposal =>
    ({ id, kind, describe: '', terminal: '', why: '', sync }) as HelpProposal;

  const client = () => {
    const made = new QueryClient();
    made.setQueryData(['help-proposals', 's1'], [
      proposal('p1', 'setting'),
      proposal('p2', 'sync', { looked: false, rows: [] }),
      proposal('p3', 'sync', {
        looked: true,
        rows: [
          { key: 'engine:main', step: 'line', moves: true, says: '' },
          { key: 'engine:daoris/s-1', step: 'replay', moves: true, says: '' },
          { key: 'engine:daoris/s-2', step: 'replay', moves: false, says: '' },
        ],
      }),
    ]);
    return made;
  };

  it('waits the bridge\'s default for every kind but bringing up to date', () => {
    expect(settleBound(client(), 'p1')).toBeUndefined();
    expect(settleBound(client(), 'nowhere')).toBeUndefined();
  });

  it('waits for the look as the screen does: a workspace\'s worth until the machine says how many it takes', () => {
    const made = client();
    expect(minutes(settleBound(made, 'p2'))).toBe(18);

    made.setQueryData(['driver', 'trees-sync-scope'], {
      repositories: [
        { repository: 'engine', workspace: 'aurora', holds: true },
        { repository: 'game', workspace: 'aurora', holds: false },
      ],
    });
    // One repository holds Daoris's branches: one round of a fetch's two minutes, and two to spare.
    expect(minutes(settleBound(made, 'p2'))).toBe(4);
  });

  it('waits for the press one replay after another, for the rows the look listed as moving', () => {
    expect(minutes(settleBound(client(), 'p3'))).toBe(12);
  });
});

/**
 * LOG1b's `proposal.settled` is the person's Apply or Not now once the proposal settled (D94). A sync card's look
 * settles nothing: the card stands for its press, and the host says so (LEFT3 c), so the look writes no line. A look
 * that found nothing to do settled the card, and is written as any Apply is.
 */
describe('what settling a proposal writes to the machine log', () => {
  const settle = async (answer: HelpSettled, apply = true) => {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? answer : undefined));
    const client = new QueryClient();
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useSettleHelp(), { wrapper });
    await act(async () => { await result.current.mutateAsync({ id: 'p11', apply }); });
    return invoke.mock.calls
      .filter(([module]) => module === 'DAORIS.LOG')
      .map(([, , request]) => (request as { payload: unknown }).payload);
  };

  it('writes proposal.settled for an Apply and a Not now that settled the proposal', async () => {
    expect(await settle({ message: 'Applied.', applied: true })).toEqual([{ event: 'proposal.settled', data: { applied: true } }]);
    expect(await settle({ message: 'Not now.' }, false)).toEqual([{ event: 'proposal.settled', data: { applied: false } }]);
    expect(await settle({ message: 'Looked: nothing to bring up to date.', applied: false, stands: false }))
      .toEqual([{ event: 'proposal.settled', data: { applied: true } }]);
  });

  it('writes nothing for a sync card\'s look, which leaves the card standing for its press', async () => {
    expect(await settle({ message: 'Looked for updates: 3 thing(s) would change.', applied: false, stands: true })).toEqual([]);
  });
});

/** HELPAPPLY1: an Apply stands for the card the person saw, so it carries that card's revision to the host. */
describe('what an Apply carries', () => {
  it('sends the revision of the card the conversation holds for the proposal', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { message: 'Applied.', applied: true } : undefined));
    const client = new QueryClient();
    client.setQueryData(['help-proposals', 's1'], [{ id: 'p13', kind: 'setting', revision: 'abc123' }]);
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useSettleHelp(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ id: 'p13', apply: true }); });

    const sent = invoke.mock.calls.find(([, type]) => type === 'HELP_APPLY');
    expect((sent?.[2] as { payload?: unknown } | undefined)?.payload).toEqual({ id: 'p13', revision: 'abc123' });
  });
});

/**
 * What an Apply or a Not now changed is published by its route (STATEHUB1h, D169), and the window's state hub asks it again, as
 * every other window's does; the hook asks nothing again itself. Each family below is one the hook used to ask by hand.
 */
describe('what an Apply reads again', () => {
  /** A client holding an answer under each key, the hub mounted beside the hook as `ShellSignals` mounts it, and an Apply made. */
  async function settled(settle: { id: string; apply: boolean }, ...held: QueryKey[]) {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { message: 'Applied.', applied: true } : undefined));
    const client = new QueryClient();
    for (const key of held) client.setQueryData(key, { held: true });
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => { useStateHub(); return useSettleHelp(); }, { wrapper });

    await act(async () => { await result.current.mutateAsync(settle); });

    return (key: QueryKey) => client.getQueryState(key)?.isInvalidated ?? false;
  }

  /**
   * ENTRY1d1: a move to a workspace changes the registry, so an Apply reads it again at once, as the Manage drawer's own
   * *Move to workspace* does, rather than waiting for the driver's watch to say it moved.
   */
  it('reads the registry again, which a move to a workspace changed', async () => {
    const invalid = await settled({ id: 'p12', apply: true }, keys.registry(null), keys.repositories(null));

    await waitFor(() => expect(invalid(keys.registry(null))).toBe(true));
    expect(invalid(keys.repositories(null))).toBe(true);
  });

  // WORKFLOW1h-d: a workflow card's Apply saved a version, so the workflow tabs (a repository's Current, a run's place),
  // which sit under the driver's key, are read again at once.
  it('reads the workflow tabs again, which a saved workflow version changed', async () => {
    const tabs = [keys.workflow('repository', 'engine'), keys.workflowRun('quest', 'q1')];
    const invalid = await settled({ id: 'p22', apply: true }, ...tabs);

    await waitFor(() => { for (const tab of tabs) expect(invalid(tab)).toBe(true); });
  });

  // HIST1f-a: a clear card's Apply removed history, so the history plan is read again, as the Clear history press does.
  it('reads the history plan again, which a clear changed', async () => {
    const plan = ['history', 'workspace', 'aurora'];
    const invalid = await settled({ id: 'p20', apply: true }, plan);

    await waitFor(() => expect(invalid(plan)).toBe(true));
  });

  // And the rest an Apply's doors may move, each read again as the hook read it by hand before (HELP6, PLUG9, HELP10).
  it('reads the proposals, the sessions, quests and asks, the agents, the plugins and the browser’s settings again', async () => {
    const moved = [[...HELP_PROPOSALS, 's1'], keys.allSessions, keys.allQuests, keys.allAsks, keys.harnesses, keys.plugins,
      keys.browserSettings, keys.sweep];
    const invalid = await settled({ id: 'p30', apply: true }, ...moved);

    await waitFor(() => { for (const key of moved) expect(invalid(key)).toBe(true); });
  });

  // A Not now settles the proposal and nothing else.
  it('reads only the proposals again after a Not now', async () => {
    const invalid = await settled({ id: 'p31', apply: false }, [...HELP_PROPOSALS, 's1'], keys.driver, keys.allSessions);

    await waitFor(() => expect(invalid([...HELP_PROPOSALS, 's1'])).toBe(true));
    expect(invalid(keys.driver)).toBe(false);
    expect(invalid(keys.allSessions)).toBe(false);
  });
});

/**
 * HELPMODE1b (D183 points 2-4): Ask Daoris's mode, set over `SET_HELP_MODE`, and what each start of a conversation handed, read
 * over `HELP_WIRED` field for field as the driver keeps it (`HelpWired.cs`), its nulls kept.
 */
describe('Ask Daoris’s mode and what it was handed', () => {
  const wrapped = () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    return { client, wrapper };
  };

  const RECEIPT = {
    at: '2026-10-19T10:00:00+00:00', start: 'new', mode: 'restricted', harness: 'claude-code-acp', version: '0.9.1',
    account: 'personal', door: 'acp', posture: 'default', permissionMode: null, folder: 'C:/home/help/room',
    tools: ['mcp__daoris-knowledge__search'],
    skills: [{ name: 'browser:look', state: 'unverified', why: null }],
    servers: [{ name: 'daoris-knowledge', plugin: null, state: 'handed', tools: ['search'], why: null }],
    rules: { allow: ['Read'], ask: [], deny: ['Bash(git push:*)'] }, guarded: true,
    checkouts: [{ name: 'engine', workspace: 'aurora', path: 'C:/src/engine' }],
    lacks: [{ kind: 'shell', name: 'shell', door: 'mode', why: 'restricted', plugin: null }],
  };

  it('reads a mode the driver does not know as restricted, as the driver does', () => {
    expect(helpModeOf('full')).toBe('full');
    expect(helpModeOf('restricted')).toBe('restricted');
    expect(helpModeOf('everything')).toBe('restricted');
    expect(helpModeOf(undefined)).toBe('restricted');
  });

  it('reads a receipt field for field, and a list or a value the answer leaves out as none', () => {
    expect(wiredAnswer({ session: 'h1', recorded: true, receipts: [RECEIPT] }, 'h1')).toEqual({
      session: 'h1', recorded: true, receipts: [RECEIPT],
    });
    const bare = wiredAnswer({ session: 'h1', recorded: true, receipts: [{ at: RECEIPT.at, mode: 'full', harness: 'codex-acp' }] }, 'h1');
    expect(bare.receipts[0]).toMatchObject({
      mode: 'full', harness: 'codex-acp', version: null, account: null, rules: null, tools: [], skills: [], servers: [],
      checkouts: [], lacks: [], guarded: false,
    });
    // A conversation from before keeps none: not recorded.
    expect(wiredAnswer({ session: 'h1', recorded: false, receipts: [] }, 'h1')).toEqual({ session: 'h1', recorded: false, receipts: [] });
    expect(wiredAnswer(undefined, 'h1')).toEqual({ session: 'h1', recorded: false, receipts: [] });
  });

  it('asks HELP_WIRED for the conversation named, and nothing with none', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HELP_WIRED' ? { session: 'h1', recorded: true, receipts: [RECEIPT] } : undefined));
    const { wrapper } = wrapped();
    const { result } = renderHook(() => useHelpWired('h1'), { wrapper });

    await waitFor(() => expect(result.current.data?.receipts).toHaveLength(1));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_WIRED', { payload: { session: 'h1' } });

    invoke.mockClear();
    renderHook(() => useHelpWired(null), { wrapper: wrapped().wrapper });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(invoke).not.toHaveBeenCalled();
  });

  it('sets the mode over SET_HELP_MODE and keeps the state it answered', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SET_HELP_MODE' ? { drivable: [], holds: [], trees: [], helpMode: 'full' } : undefined));
    const { client, wrapper } = wrapped();
    const { result } = renderHook(() => useSetHelpMode(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ mode: 'full' }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    expect(client.getQueryData<{ helpMode?: string }>(keys.driver)?.helpMode).toBe('full');
  });
});
