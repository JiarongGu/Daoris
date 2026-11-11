import { createElement, type ReactNode } from 'react';
import { type QueryKey, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear: what the mocked shell publishes after a press reaches it, as the kit's pump would.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async (importOriginal) => {
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    ...(await importOriginal<typeof import('@shenora/react')>()),
    getBridge: () => ({ invoke: published, isAvailable: true }),
    // A shell is here, so a read asks it (`HELP_WIRED`).
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import type { HelpProposal } from '../help/ProposalCard';
import { draftToTell, noteTold } from '../help/openDraft';
import { keys } from '../data/queries';
import { useSetHelpMode } from './driver';
import {
  HELP_PROPOSALS, HELP_PUBLISHED, helpModeOf, makerOf, publishedAnswer, useDraftProposals, useHelpPublished, useHelpWired, useSettleHelp,
  wiredAnswer, type HelpSettled,
} from './help';
import { useStateHub } from './state/stateHub';

/**
 * `proposal.settled` is the person's Apply or Not now once the proposal settles. A sync card's look
 * settles nothing: the card stands for its press, and the host says so, so the look writes no line. A look
 * that finds nothing to do settles the card, and is written as any Apply is.
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

/** An Apply stands for the card the person saw, so it carries that card's revision to the host. */
describe('what an Apply carries', () => {
  it('sends the revision of the card the conversation holds for the proposal', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { message: 'Applied.', applied: true } : undefined));
    const client = new QueryClient();
    client.setQueryData(['help-proposals', 's1'], [{ id: 'p13', kind: 'role', revision: 'abc123' }]);
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useSettleHelp(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ id: 'p13', apply: true }); });

    const sent = invoke.mock.calls.find(([, type]) => type === 'HELP_APPLY');
    expect((sent?.[2] as { payload?: unknown } | undefined)?.payload).toEqual({ id: 'p13', revision: 'abc123' });
  });

  /** A draft card's Apply from its editor names each change taken into the draft and each left, which settles it. */
  it("sends the editor's changes taken and left beside the revision", async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { message: 'Applied.', applied: true } : undefined));
    const client = new QueryClient();
    client.setQueryData(['help-proposals', 's1'], [{ id: 'p14', kind: 'workflow', revision: 'def456' }]);
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useSettleHelp(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ id: 'p14', apply: true, taken: ['step:+find'], left: ['way:+g'] }); });

    const sent = invoke.mock.calls.find(([, type]) => type === 'HELP_APPLY');
    expect((sent?.[2] as { payload?: unknown } | undefined)?.payload).toEqual({ id: 'p14', revision: 'def456', taken: ['step:+find'], left: ['way:+g'] });
  });
});

/** The proposals against a draft the editor of a place reads, from the conversations told one. */
describe('the proposals against an open draft', () => {
  const graph = { schemaVersion: 2, entries: ['work'], end: 'work', nodes: [{ id: 'work', kind: 'claimed' }], wires: [] };
  const card = (id: string, scope: string, draft: string | null): HelpProposal => ({
    id, kind: 'workflow', describe: '', terminal: '', why: '',
    workflow: { id: '', name: '', version: 0, scope, diff: [], yourPart: [], outward: [], needs: [], appliesTo: '', choose: false, draft, changes: [], proposed: graph },
  });

  it("asks each conversation told a draft for its proposals, and keeps the place's draft cards with the graph each was told", async () => {
    const sent = draftToTell({ place: 'engine', graph }, undefined)!;
    noteTold('s9', sent);
    invoke.mockReset();
    invoke.mockImplementation(async (module: string, type: string, request: { payload: { session?: string } }) => {
      if (module !== 'DAORIS.DRIVER' || type !== 'HELP_PROPOSALS') return undefined;
      return request.payload.session === 's9'
        ? { session: 's9', proposals: [card('p1', 'engine', sent.fingerprint), card('p2', 'docs', sent.fingerprint), card('p3', 'engine', null), { ...card('p4', 'engine', null), kind: 'clear' }] }
        : null;
    });
    const client = new QueryClient();
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useDraftProposals('engine', true), { wrapper });

    await waitFor(() => expect(result.current.map((each) => each.proposal.id)).toEqual(['p1']));
    expect(result.current[0]).toMatchObject({ session: 's9', base: graph });
    expect(invoke.mock.calls.filter(([, type]) => type === 'HELP_PROPOSALS').map(([, , request]) => (request as { payload: { session: string } }).payload.session))
      .toEqual(expect.arrayContaining(['s9']));
  });

  it('asks nothing while the editor is not shown', async () => {
    invoke.mockReset();
    const client = new QueryClient();
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    renderHook(() => useDraftProposals('engine', false), { wrapper });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(invoke.mock.calls.filter(([, type]) => type === 'HELP_PROPOSALS')).toEqual([]);
  });

  it('names who made it by the agent a person calls it and the models its conversation reported', () => {
    const doors = [{ door: 'claude', product: 'Claude Code' }] as unknown as Parameters<typeof makerOf>[1];
    expect(makerOf({ door: 'claude', ran: { models: ['claude-opus-5-5'], more: 0, entries: [], entriesMore: 0 } }, doors))
      .toEqual({ agent: 'Claude Code', models: ['claude-opus-5-5'] });
    expect(makerOf({ door: 'codex', ran: null }, [])).toEqual({ agent: 'codex', models: [] });
    expect(makerOf(undefined, [])).toBeNull();
  });
});

/**
 * What an Apply or a Not now changes is published by its route, and the window's state hub asks it again, as
 * every other window's does; the hook asks nothing again itself. Each family below is one the hook would otherwise ask by hand.
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

  // A workflow card's Apply saves a version, so the workflow tabs under the driver's key are read again at once.
  it('reads the workflow tabs again, which a saved workflow version changed', async () => {
    const tabs = [keys.workflow('repository', 'engine'), keys.workflowRun('quest', 'q1')];
    const invalid = await settled({ id: 'p22', apply: true }, ...tabs);

    await waitFor(() => { for (const tab of tabs) expect(invalid(tab)).toBe(true); });
  });

  // A clear card's Apply removes history, so the history plan is read again, as the Clear history press does.
  it('reads the history plan again, which a clear changed', async () => {
    const plan = ['history', 'workspace', 'aurora'];
    const invalid = await settled({ id: 'p20', apply: true }, plan);

    await waitFor(() => expect(invalid(plan)).toBe(true));
  });

  // And the rest an Apply's doors may move, each read again.
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
 * Ask Daoris's mode, set over `SET_HELP_MODE`, and what each start of a conversation handed, read
 * over `HELP_WIRED` field for field as the driver keeps it (`HelpWired.cs`), its nulls kept.
 */
describe('Ask Daoris’s mode and what it was handed', () => {
  const wrapped = () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    return { client, wrapper };
  };

  const RECEIPT = {
    at: '2026-10-19T10:00:00+00:00', start: 'new', mode: 'restricted', door: 'claude-code-acp', version: '0.9.1',
    account: 'personal', transport: 'protocol', posture: 'default', permissionMode: null, folder: 'X:/daoris/help',
    tools: ['mcp__daoris-knowledge__search'],
    skills: [{ name: 'browser:look', state: 'unverified', why: null }],
    servers: [{ name: 'daoris-knowledge', plugin: null, state: 'handed', tools: ['search'], why: null }],
    rules: { allow: ['Read'], ask: [], deny: ['Bash(git push:*)'] }, guarded: true,
    checkouts: [{ name: 'engine', workspace: 'aurora', path: 'C:/src/engine' }],
    lacks: [{ kind: 'shell', name: 'shell', door: 'mode', why: 'restricted', plugin: null }],
    loaded: { tools: ['Read'], servers: [{ name: 'daoris-knowledge', status: 'connected' }], skills: ['browser:look'], plugins: [] },
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
    const bare = wiredAnswer({ session: 'h1', recorded: true, receipts: [{ at: RECEIPT.at, mode: 'full', door: 'codex-acp' }] }, 'h1');
    expect(bare.receipts[0]).toMatchObject({
      mode: 'full', door: 'codex-acp', version: null, account: null, rules: null, tools: [], skills: [], servers: [],
      checkouts: [], lacks: [], guarded: false, loaded: null,
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

  it('sets the mode over SET_HELP_MODE and answers the state, the driver’s entry left to its push', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'SET_HELP_MODE' ? { drivable: [], holds: [], trees: [], helpMode: 'full' } : undefined));
    const { client, wrapper } = wrapped();
    const { result } = renderHook(() => useSetHelpMode(), { wrapper });

    let answered: { helpMode?: string } | undefined;
    await act(async () => { answered = await result.current.mutateAsync({ mode: 'full' }); });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HELP_MODE', { payload: { mode: 'full' } });
    expect(answered?.helpMode).toBe('full');
    expect(client.getQueryData(keys.driver)).toBeUndefined();
  });
});

/**
 * What a conversation's Applies published, read over `HELP_PUBLISHED` as the driver
 * keeps it (`HelpPublished.cs`), newest first, an id its answer leaves out read as none, and no answer read as none.
 */
describe('what a conversation published', () => {
  const wrapped = () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    return { client, wrapper };
  };

  it('reads each entry field for field, and one with no proposal or neither id as nothing', () => {
    expect(publishedAnswer({
      session: 'h1',
      published: [
        { at: '2026-10-22T10:00:00+00:00', proposal: 'p2', kind: 'setup', workspace: 'work', ask: 'a2', quest: 'q2' },
        { at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: 'a1' },
        { at: '2026-10-22T08:00:00+00:00', proposal: 'p0', kind: 'ask' },
        { at: '2026-10-22T08:00:00+00:00', kind: 'ask', ask: 'a0' },
      ],
    })).toEqual([
      { at: '2026-10-22T10:00:00+00:00', proposal: 'p2', kind: 'setup', workspace: 'work', ask: 'a2', quest: 'q2' },
      { at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: 'a1', quest: null },
    ]);
    expect(publishedAnswer(undefined)).toEqual([]);
    expect(publishedAnswer({})).toEqual([]);
  });

  it('asks HELP_PUBLISHED for the conversation named, under the help family, and nothing with none', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HELP_PUBLISHED'
      ? { session: 'h1', published: [{ at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', ask: 'a1' }] }
      : undefined));
    const { client, wrapper } = wrapped();
    const { result } = renderHook(() => useHelpPublished('h1'), { wrapper });

    await waitFor(() => expect(result.current.data).toHaveLength(1));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HELP_PUBLISHED', { payload: { session: 'h1' } });
    expect(client.getQueryData([...HELP_PUBLISHED, 'h1'])).toHaveLength(1);

    invoke.mockClear();
    renderHook(() => useHelpPublished(null), { wrapper: wrapped().wrapper });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(invoke).not.toHaveBeenCalled();
  });
});
