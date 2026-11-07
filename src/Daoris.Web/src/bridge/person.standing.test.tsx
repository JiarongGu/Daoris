import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The roles' judge over the bridge: STATE's `standingConfirmation` read field by field, and the window's start of a role's terminal.

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear: what the mocked shell publishes after a press reaches it, as the kit's pump would; and the shell's events.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { keys } from '../data/queries';
import { standingOf, useStartRoleTerminal } from './person';

const wrapped = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(keys.driver, { held: true });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  return { wrapper };
};

describe('the standing confirmation over the bridge', () => {
  afterEach(() => {
    invoke.mockReset();
    heard.clear();
  });

  it('reads STATE’s standingConfirmation field by field, a null the bridge left out read as none', () => {
    expect(standingOf({})).toEqual({ holds: [], holders: [], stewardActs: [], stewardDesk: [] });
    // The roles a terminal holds this run, each read field by field; one with no role, kind or holder left out.
    expect(standingOf({
      holders: [{ id: 'h1', since: '2026-10-24T14:02:00Z', roles: ['steward', 'supervisor'] }, { roles: ['steward'] }],
      holds: [
        { role: 'steward', kind: 'steward', holder: 'h1', since: '2026-10-24T14:02:00Z', answers: 2 },
        { role: 'supervisor-engine', kind: 'supervisor', holder: 'h1' }, { role: 'supervisor' }, 'engine',
      ],
    })).toMatchObject({
      holders: [{ id: 'h1', since: '2026-10-24T14:02:00Z', roles: ['steward', 'supervisor'] }],
      holds: [
        { role: 'steward', kind: 'steward', holder: 'h1', since: '2026-10-24T14:02:00Z', answers: 2 },
        { role: 'supervisor-engine', kind: 'supervisor', holder: 'h1', since: null, answers: 0 },
      ],
    });
    // A shell serving no pipe says none.
    expect(standingOf(undefined)).toBeNull();
    expect(standingOf(null)).toBeNull();
    expect(standingOf([])).toBeNull();
  });

  // This run's steward acts newest first, each read field by field; a missing list reads as none.
  it("reads this run's steward acts field by field", () => {
    expect(standingOf({
      stewardActs: [
        { id: 'a2', act: 'stop', repository: null, quest: 'q1', session: 's1', at: '2026-10-25T10:02:00Z', standing: true, waiting: true },
        { id: 'a1', act: 'drive', repository: 'engine', at: '2026-10-25T10:01:00Z', standing: 'yes' },
        { act: 'hold', at: '2026-10-25T10:00:00Z' }, { id: 'a0', at: '2026-10-25T10:00:00Z' }, 'drive',
      ],
    })).toMatchObject({
      stewardActs: [
        { id: 'a2', act: 'stop', repository: null, quest: 'q1', session: 's1', at: '2026-10-25T10:02:00Z', standing: true, waiting: true },
        { id: 'a1', act: 'drive', repository: 'engine', quest: null, session: null, at: '2026-10-25T10:01:00Z', standing: false, waiting: false },
      ],
    });
    expect(standingOf({})).toMatchObject({ stewardActs: [], holds: [] });
  });

  // The steward's desk, each request read field by field, its words as the caller wrote them; one with a
  // field missing or a state this page does not know left out, and a missing desk read as empty.
  it("reads the steward's desk field by field", () => {
    expect(standingOf({
      stewardOffered: true,
      stewardDesk: [
        { id: 'r2', act: 'stop', target: 's1', why: 'it loops <b>', filed: '2026-10-25T10:02:00Z', state: 'declined', closed: '2026-10-25T10:03:00Z', reason: 'the owner stops it' },
        { id: 'r1', act: 'drive', target: 'engine', why: 'next row', filed: '2026-10-25T10:01:00Z', state: 'open' },
        { id: 'r0', act: 'drive', target: 'engine', why: 'x', filed: '2026-10-25T10:00:00Z', state: 'granted' },
        { id: 'r9', act: 'drive', target: 'engine', filed: '2026-10-25T10:00:00Z', state: 'open' }, 'drive',
      ],
    })?.stewardDesk).toEqual([
      { id: 'r2', act: 'stop', target: 's1', why: 'it loops <b>', filed: '2026-10-25T10:02:00Z', state: 'declined', closed: '2026-10-25T10:03:00Z', answeredBy: null, reason: 'the owner stops it' },
      { id: 'r1', act: 'drive', target: 'engine', why: 'next row', filed: '2026-10-25T10:01:00Z', state: 'open', closed: null, answeredBy: null, reason: null },
    ]);
    expect(standingOf({ stewardOffered: true })?.stewardDesk).toEqual([]);
  });

  it("starts a role's terminal by its catalogue id, a living holder's id or its own where named", async () => {
    invoke.mockResolvedValue({ done: true });
    const { wrapper } = wrapped();
    const { result } = renderHook(() => useStartRoleTerminal(), { wrapper });

    await result.current.mutateAsync({ role: 'steward' });
    await result.current.mutateAsync({ role: 'supervisor-engine', share: 'h1' });
    expect(invoke.mock.calls.map(([, type, request]) => [type, request])).toEqual([
      ['START_ROLE_TERMINAL', { payload: { role: 'steward' } }],
      ['START_ROLE_TERMINAL', { payload: { role: 'supervisor-engine', share: 'h1' } }],
    ]);
  });

});
