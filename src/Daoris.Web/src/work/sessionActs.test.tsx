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
    const answered = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.stopNow(session({ id: 'p4rk3d00', state: 'awaiting-person' }), answered));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RESOLVE_SESSION', { payload: { id: 'p4rk3d00', state: 'stopped' } }));
    await waitFor(() => expect(answered.done).toHaveBeenCalledOnce());

    act(() => result.current.stopNow(session({ state: 'working' })));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', { payload: { id: 's1a2b3c4' } }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('session s1a2b3c4 is being stopped — the record will say the person ended it.'));
  });

  /** SESSUX1f (D126 §5.4): Delete… is the frame's to ask (it asks once under the header), and its second press is the owner's. */
  it('hands Delete… to the frame, and its second press deletes by the id, saying so, and forgets the draft', async () => {
    const doors = { delete: vi.fn() };
    const { result: withDoor } = acts(doors);
    withDoor.current.run('delete', { session: session({ quest: null, kind: 'chat', state: 'completed' }) });
    expect(doors.delete).toHaveBeenCalledWith('s1a2b3c4');
    expect(acts().result.current.can('delete')).toBe(false);

    window.localStorage.setItem('daoris.drafts', JSON.stringify([['c0ffee00', 'half a thought'], ['other000', 'kept']]));
    invoke.mockImplementation(async () => ({ deleted: 'c0ffee00', removed: ['record', 'conversation'] }));
    const { result, notify } = acts();
    const answered = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.deleteNow(session({ id: 'c0ffee00', quest: null, kind: 'chat', state: 'completed' }), answered));

    await waitFor(() => expect(answered.done).toHaveBeenCalledOnce());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: 'c0ffee00' } });
    expect(notify).toHaveBeenCalledWith('Deleted c0ffee00: its record, and its words, transcript and files on this machine, are gone.');
    expect(JSON.parse(window.localStorage.getItem('daoris.drafts') ?? '[]')).toEqual([['other000', 'kept']]);
  });

  /** PAUSE1e (D132 §7.1): a pause is the frame's to ask, naming its quest or its ask; Resume goes to the work's owner at once. */
  it('hands a pause to the frame with the work it names, and resumes the pause that holds its quest over the driver', async () => {
    const doors = { pause: vi.fn() };
    const { result: withDoor } = acts(doors);
    const asked = { id: 'abc123', from: 'ask #a1b2c3', to: 'engine', title: 'T', body: '', status: 'Taken' as const, filed: '', updated: '' };
    withDoor.current.run('pauseQuest', { session: session({ state: 'working' }), quest: asked });
    withDoor.current.run('pauseAsk', { session: session({ state: 'working' }), quest: asked });
    expect(doors.pause).toHaveBeenNthCalledWith(1, 's1a2b3c4', { scope: 'quest', id: 'abc123' });
    expect(doors.pause).toHaveBeenNthCalledWith(2, 's1a2b3c4', { scope: 'ask', id: 'a1b2c3' });
    expect(acts().result.current.can('pauseQuest')).toBe(false);
    expect(acts().result.current.can('resumeAsk')).toBe(true);

    invoke.mockImplementation(async () => ({ scope: 'ask', id: 'a1b2c3', did: 'resumed', released: [], holds: [] }));
    const { result, notify } = acts();
    const grouping = {
      session: 's1a2b3c4', group: 'review' as const, shown: 'stopped', archived: false, teammate: false,
      pausedBy: { scope: 'ask' as const, id: 'a1b2c3' },
    };
    act(() => result.current.run('resumeAsk', { session: session({ state: 'stopped' }), grouping }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_RESUME', expect.objectContaining({ payload: { ask: 'a1b2c3' } })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "Resumed ask #a1b2c3: the driver's next look plans its work as it would have.", 'ok'));
  });

  /** UXFIX2: a refusal is said in the ask that was pressed, in the catalogue's words, and the ask stays open. */
  it('says a delete the host refused in the catalogue’s words, inside the ask, which stays open', async () => {
    invoke.mockImplementation(async () => { throw refusal('SESSION_SERVED_QUEST', { session: 's1a2b3c4', quest: 'abc123' }); });
    const { result, notify } = acts();
    const answered = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.deleteNow(session({ state: 'completed' }), answered));

    await waitFor(() => expect(answered.refused).toHaveBeenCalledWith(
      's1a2b3c4 worked on #abc123, and its record is that work’s, so it was not deleted. Archive it instead.'));
    expect(answered.done).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();

    // With no ask to say it in, it is a toast.
    act(() => result.current.deleteNow(session({ state: 'completed' })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      's1a2b3c4 worked on #abc123, and its record is that work’s, so it was not deleted. Archive it instead.', 'error'));
  });
});
