import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The one owner of each act on a session (SESSUX1d, D126 §3.1): whichever door pressed it, the row's ⋯ or the page
// header, the act runs here. Over a mocked bridge, as an organism is held (components plan §3).

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => undefined,
}));

import type { Session } from '../api';
import { type SessionDoors, useSessionActs } from './sessionActs';

const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: [], notify: true, strikes: 3, forgiven: {} };

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'failed', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z', tree: 'C:/somewhere/engine', ...over,
});

function acts(doors?: SessionDoors) {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  const { result } = renderHook(() => useSessionActs({ notify, doors }), { wrapper });
  return { result, notify };
}

const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });

describe('the acts on a session', () => {
  afterEach(() => invoke.mockReset());

  it('tries a parked quest again by its quest, and says the mark it made', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, retried: { quest: 'abc123', did: 'marked', session: null } }));
    const { result, notify } = acts();

    act(() => result.current.run('retry', { session: session() }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^#abc123 will be tried again/)));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RETRY_QUEST', { payload: { quest: 'abc123' } });
  });

  it('says a stop released, in the sentence the quest’s page says', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, retried: { quest: 'abc123', did: 'released', session: 's1a2b3c4' } }));
    const { result, notify } = acts();

    act(() => result.current.run('retry', { session: session({ state: 'stopped' }) }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Released #abc123 from your stop of s1a2b3c4: the driver takes it up again at its next look.'));
  });

  it('opens a session’s folder by its id, and says a gone folder in the catalogue’s words', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_OPEN_FOLDER') throw refusal('SESSION_FOLDER_GONE', { session: 's1a2b3c4' });
      return DRIVER_STATE;
    });
    const { result, notify } = acts();

    act(() => result.current.run('openFolder', { session: session() }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPEN_FOLDER', { payload: { id: 's1a2b3c4' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^The folder s1a2b3c4 worked in is not on this machine/), 'error'));
  });

  it('archives and unarchives one session, its own window and its id, as the row did', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_ARCHIVE'
      ? { archived: [{ session: 's1a2b3c4', at: '2026-10-02T10:00:00Z' }], kept: [] }
      : { opened: true, windows: [] }));
    const writeText = vi.fn(async () => {});
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });
    const { result, notify } = acts();

    act(() => result.current.run('archive', { session: session({ state: 'completed' }) }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Archived\./)));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', { payload: { ids: ['s1a2b3c4'], archived: true } });

    act(() => result.current.run('detach', { session: session() }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.WINDOWS', 'OPEN', { payload: { name: 'session:s1a2b3c4' } }));

    act(() => result.current.run('copy', { session: session() }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Copied s1a2b3c4.'));
    expect(writeText).toHaveBeenCalledWith('s1a2b3c4');
    vi.unstubAllGlobals();
  });

  /** What only the frame can do goes to the frame's door, and is offered only where the frame handed one. */
  it('hands the frame what is the frame’s, and can run it only where the frame handed its door', () => {
    const doors = { answer: vi.fn(), stop: vi.fn(), review: vi.fn(), terminal: vi.fn() };
    const { result } = acts(doors);

    for (const act of ['answer', 'stop', 'review'] as const) result.current.run(act, { session: session() });
    result.current.run('terminal', { session: session({ tree: null }), root: 'C:/somewhere/engine' });

    expect(doors.answer).toHaveBeenCalledWith('s1a2b3c4');
    expect(doors.stop).toHaveBeenCalledWith('s1a2b3c4');
    expect(doors.review).toHaveBeenCalledWith('s1a2b3c4');
    expect(doors.terminal).toHaveBeenCalledWith('C:/somewhere/engine');
    expect(result.current.can('stop')).toBe(true);

    const none = acts().result;
    expect(['answer', 'stop', 'review', 'terminal'].map((each) => none.current.can(each as 'stop'))).toEqual([false, false, false, false]);
    expect(none.current.can('retry')).toBe(true);
  });

  /** §3.3: a session waiting on you is stopped through the resolve, as its card's stop was; every other through the stop. */
  it('stops a session waiting on you through its resolve, and any other live one through the stop, saying what the driver did', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STOP_SESSION'
      ? { stopped: true }
      : { session: 'p4rk3d00', state: 'stopped', message: 'ok' }));
    const { result, notify } = acts();
    const done = vi.fn();

    act(() => result.current.stopNow(session({ id: 'p4rk3d00', state: 'awaiting-person' }), done));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'stopped' } }));
    await waitFor(() => expect(done).toHaveBeenCalledOnce());

    act(() => result.current.stopNow(session({ state: 'working' })));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 's1a2b3c4' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('session s1a2b3c4 is being stopped — the record will say the person ended it.'));
  });
});
