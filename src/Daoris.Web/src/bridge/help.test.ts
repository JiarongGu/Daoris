import { createElement, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
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
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { keys } from '../data/queries';
import { useSetHelpMode } from './driver';
import {
  HELP_PUBLISHED, helpModeOf, publishedAnswer, useHelpPublished, useHelpWired, wiredAnswer,
} from './help';


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
