import { readFileSync } from 'node:fs';
import { join } from 'node:path';
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

import type { Session } from '../data/api';
import { list } from '../locales/format';
import i18n from '../locales/i18n';
import { en, zh } from '../locales';
import { WorkspaceScopeProvider } from '../data/scope';
import { type SessionDoors, useSessionActs } from './sessionActs';

/**
 * The names `SESSION_DELETE`'s `stayed` carries (SESSDEL1c, `.claude/knowledge/twins.md`): one table with the driver, its tests'
 * `fixtures/kept-names.json`, which its `KeptNamesTwinTests` holds `SessionHomeFiles`' names to. Each its driver's name and its
 * word on the wire.
 */
const KEPT = (JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'kept-names.json'), 'utf8')) as {
  names: Record<string, string>;
}).names;

const DRIVER_STATE = { drivable: ['engine'], holds: [], trees: [], running: [], notify: true, strikes: 3, forgiven: {} };

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'failed', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z', tree: 'C:/somewhere/engine', ...over,
});

function acts(doors?: SessionDoors) {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>
      <WorkspaceScopeProvider initial="games">{children}</WorkspaceScopeProvider>
    </QueryClientProvider>
  );
  const { result } = renderHook(() => useSessionActs({ notify, doors }), { wrapper });
  return { result, notify };
}

const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });

describe('the acts on a session', () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

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
    // MAINWINDOWS3: a session naming no workspace is followed in the window that detached it.
    expect(JSON.parse(window.localStorage.getItem('daoris.workspace.opened')!)['session:s1a2b3c4']).toEqual({ follows: 'main' });

    // …and one naming its workspace is held there, whatever this window moves to.
    act(() => result.current.run('detach', { session: session({ workspace: 'studio' }) }));
    await waitFor(() => expect(JSON.parse(window.localStorage.getItem('daoris.workspace.opened')!)['session:s1a2b3c4'])
      .toEqual({ workspace: 'studio' }));
    window.localStorage.clear();

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

  /**
   * SESSDEL1b (D126's SESSDEL1 note): `SESSION_DELETE` answers `stayed`, the names of what the disk would not let go of, and
   * the toast still said everything went. It names what stayed in the reader's language, in the error's tone since not all
   * of it went, and the ask still closes, since the record did. A delete that kept nothing says what it said.
   */
  it('says which of what this machine kept stayed, by name, in both languages', async () => {
    invoke.mockImplementation(async () => ({ deleted: 'c0ffee00', removed: ['record', 'conversation', 'files'], stayed: ['transcript'] }));
    const { result, notify } = acts();
    const answered = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.deleteNow(session({ id: 'c0ffee00', quest: null, kind: 'chat', state: 'completed' }), answered));

    await waitFor(() => expect(answered.done).toHaveBeenCalledOnce());
    expect(notify).toHaveBeenCalledWith(
      'Deleted c0ffee00: its record went, but the disk would not let go of its transcript, now left over on this machine.', 'error');
    expect(notify).not.toHaveBeenCalledWith(expect.stringContaining('are gone'));

    await i18n.changeLanguage('zh');
    invoke.mockImplementation(async () => ({ deleted: 'c0ffee00', removed: ['record', 'conversation'], stayed: ['transcript', 'files'] }));
    act(() => result.current.deleteNow(session({ id: 'c0ffee00', quest: null, kind: 'chat', state: 'completed' })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '已删除 c0ffee00：它的记录已移除，但它的记录文本和它的文件没能从磁盘移除，现已遗留在本机上。', 'error'));
  });

  it('says a delete that kept nothing as before, an empty stayed included', async () => {
    invoke.mockImplementation(async () => ({ deleted: 'c0ffee00', removed: ['record', 'conversation'], stayed: [] }));
    const { result, notify } = acts();
    act(() => result.current.deleteNow(session({ id: 'c0ffee00', quest: null, kind: 'chat', state: 'completed' })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Deleted c0ffee00: its record, and its words, transcript and files on this machine, are gone.'));
  });

  /**
   * SESSDEL1c: the page words the driver's names, and kept its own copy of them with no test holding the two together. Both
   * catalogues word exactly the shared table's names, `other` aside (a name a later driver adds), and the toast says each in
   * the catalogue's words rather than as the name it is: a name on one side alone fails here or in the driver's twin.
   */
  it.each([['en', en], ['zh', zh]] as const)('words exactly the names the driver’s table holds, in %s', (_, catalogue) => {
    const prefix = 'work.delete.kept.';
    const worded = Object.keys(catalogue).filter((key) => key.startsWith(prefix)).map((key) => key.slice(prefix.length));
    expect(worded.filter((name) => name !== 'other').sort()).toEqual(Object.values(KEPT).sort());
  });

  it.each(['en', 'zh'])('says each of the table’s names in the catalogue’s words in %s, never as the name itself', async (language) => {
    await i18n.changeLanguage(language);
    const names = Object.values(KEPT);
    invoke.mockImplementation(async () => ({ deleted: 'c0ffee00', removed: ['record'], stayed: names }));
    const { result, notify } = acts();

    act(() => result.current.deleteNow(session({ id: 'c0ffee00', quest: null, kind: 'chat', state: 'completed' })));

    const what = list(names.map((name) => i18n.t(`work.delete.kept.${name}`)));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(i18n.t('work.delete.stayed', { id: 'c0ffee00', what }), 'error'));
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

  /**
   * MIXAGENT1b-i (D163): *Go on with another agent…*'s press asks `SESSION_HAND_OVER` with the reason `press`; a conversation
   * that is not talking goes on at once, so the ask closes and the page attends the new conversation.
   */
  it('hands an idle conversation over by its press, closes the ask and attends the one it opened', async () => {
    invoke.mockImplementation(async () => ({ sessionId: 'c2', kept: true, message: 'conversation `c2` goes on.' }));
    const attend = vi.fn();
    const { result } = acts({ attend });
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null, state: 'completed' }), { agent: 'codex', account: 'team', name: 'Codex' }, told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', { payload: { id: 's1a2b3c4', agent: 'codex', account: 'team', why: 'press' } });
    expect(attend).toHaveBeenCalledWith('c2');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', expect.anything());
    expect(told.refused).not.toHaveBeenCalled();
  });

  /**
   * MIXAGENT2b (D163 point 2): *Ask another agent…*'s press asks `SESSION_HELPER_ASK` with the question and the agent the person
   * named, and closes the ask saying where the answer comes back; a refusal is said inside it in the page's words by its code, or
   * the driver's sentence for a code the page does not know.
   */
  it('asks a helper by the person’s press, and says a refusal in the ask by its code', async () => {
    invoke.mockImplementationOnce(async () => ({ asked: 'f00d0001', why: null, message: 'Asked.' }));
    const { result, notify } = acts();
    const told = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.askHelperNow(session({ kind: 'chat', quest: null }), { question: 'Which test?', agent: 'codex-acp' }, told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_ASK', { payload: { id: 's1a2b3c4', question: 'Which test?', agent: 'codex-acp' } });
    expect(notify).toHaveBeenCalledWith('Asked another agent: its answer comes back to this session as its next turn, marked as the helper’s.');

    invoke.mockImplementationOnce(async () => ({ asked: null, why: 'helper', message: 'x' }));
    act(() => result.current.askHelperNow(session(), { question: 'Why?', agent: null }, told));
    await waitFor(() => expect(told.refused).toHaveBeenCalledWith('A helper cannot ask for a helper: ask from the session it helps.'));
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_ASK', { payload: { id: 's1a2b3c4', question: 'Why?' } });

    invoke.mockImplementationOnce(async () => ({ asked: null, why: 'tomorrow', message: 'The driver’s own sentence.' }));
    act(() => result.current.askHelperNow(session(), { question: 'Why?', agent: null }, told));
    await waitFor(() => expect(told.refused).toHaveBeenLastCalledWith('The driver’s own sentence.'));
  });

  /** MIXAGENT2b (D163 point 3): the person's yes to an agent's ask, by the session and the ask; a refusal by its code. */
  it('says yes to an agent’s ask by its session and its id, and a refusal by its code', async () => {
    invoke.mockImplementationOnce(async () => ({ approved: true, why: null, message: 'yes' }));
    const { result } = acts();
    const told = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.approveHelperNow('s1a2b3c4', 'a1', told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_APPROVE', { payload: { session: 's1a2b3c4', ask: 'a1' } });

    invoke.mockImplementationOnce(async () => ({ approved: false, why: 'past', message: 'x' }));
    act(() => result.current.approveHelperNow('s1a2b3c4', 'a1', told));
    await waitFor(() => expect(told.refused).toHaveBeenCalledWith('It has started already, or ended: nothing waits on your yes.'));
  });

  /**
   * HELPERNO1 (D163 point 3): the person's no to an agent's ask, by the session and the ask, said where it went; a refusal by its
   * code, `approved` and `unread` among the no's own, or the driver's sentence for one the page does not know.
   */
  it('says no to an agent’s ask by its session and its id, and a refusal by its code', async () => {
    invoke.mockImplementationOnce(async () => ({ declined: true, why: null, message: 'no' }));
    const { result, notify } = acts();
    const told = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.declineHelperNow('s1a2b3c4', 'a1', told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HELPER_DECLINE', { payload: { session: 's1a2b3c4', ask: 'a1' } });
    expect(notify).toHaveBeenCalledWith('Not asked: no helper is started for it, and its session’s timeline says you said no.');

    invoke.mockImplementationOnce(async () => ({ declined: false, why: 'approved', message: 'x' }));
    act(() => result.current.declineHelperNow('s1a2b3c4', 'a1', told));
    await waitFor(() => expect(told.refused).toHaveBeenCalledWith('It has your yes already: the driver starts it at its next look.'));

    invoke.mockImplementationOnce(async () => ({ declined: false, why: 'unread', message: 'x' }));
    act(() => result.current.declineHelperNow('s1a2b3c4', 'a1', told));
    await waitFor(() => expect(told.refused).toHaveBeenLastCalledWith('The driver has not read it yet: say no once it waits on your word.'));

    invoke.mockImplementationOnce(async () => ({ declined: false, why: 'tomorrow', message: 'The driver’s own sentence.' }));
    act(() => result.current.declineHelperNow('s1a2b3c4', 'a1', told));
    await waitFor(() => expect(told.refused).toHaveBeenLastCalledWith('The driver’s own sentence.'));
  });

  /** MIXAGENT1b-ii (D163): Daoris's own offer is asked with its reason and as Daoris's own; a press names neither. */
  it('asks Daoris’s offer with its reason and asked-by daoris, which a press never sends', async () => {
    invoke.mockImplementation(async () => ({ sessionId: 'c2', kept: true, message: 'goes on.' }));
    const { result } = acts({ attend: vi.fn() });
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(
      session({ kind: 'chat', quest: null, state: 'completed' }), { agent: 'codex', account: 'team', name: 'Codex' }, told, 'near'));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', {
      payload: { id: 's1a2b3c4', agent: 'codex', account: 'team', why: 'near', askedBy: 'daoris' },
    });
  });

  it('says a refusal in the ask, in the page’s own words by its code, and keeps the choice', async () => {
    invoke.mockImplementation(async () => ({ sessionId: null, kept: false, why: 'same-agent', message: 'x already runs on y.' }));
    const attend = vi.fn();
    const { result } = acts({ attend });
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null, state: 'completed' }), { agent: 'codex', account: '', name: 'Codex' }, told));

    await waitFor(() => expect(told.refused).toHaveBeenCalledWith('It already runs on that agent and account: choose another agent.'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_HAND_OVER', { payload: { id: 's1a2b3c4', agent: 'codex', why: 'press' } });
    expect(told.done).not.toHaveBeenCalled();
    expect(attend).not.toHaveBeenCalled();
  });

  it('says what never goes on as the box says it, and a code a newer driver adds in the driver’s sentence', async () => {
    invoke.mockImplementationOnce(async () => ({ sessionId: null, kept: false, why: 'teammate', message: 'x' }));
    const { result } = acts();
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };
    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null }), { agent: 'codex', account: '', name: 'Codex' }, told));
    await waitFor(() => expect(told.refused).toHaveBeenCalledWith(expect.stringMatching(/conversation is on its own machine|where its conversation is/)));

    invoke.mockImplementationOnce(async () => ({ sessionId: null, kept: false, why: 'tomorrow', message: 'The driver’s own sentence.' }));
    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null }), { agent: 'codex', account: '', name: 'Codex' }, told));
    await waitFor(() => expect(told.refused).toHaveBeenLastCalledWith('The driver’s own sentence.'));
  });

  /** Stop and hand over: the hand-over is asked first and kept, then the turn is stopped, and its end takes it. */
  it('asks a working conversation’s hand-over first, then stops its turn, and says it goes on when the turn ends', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'CANCEL_TURN'
      ? { cancelled: true, withdrawn: [] }
      : { sessionId: null, kept: true, message: 'it goes on.' }));
    const { result } = acts();
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null, state: 'working' }), { agent: 'codex', account: '', name: 'Codex' }, told));

    await waitFor(() => expect(told.kept).toHaveBeenCalledWith('It goes on with Codex when its turn ends, in a conversation of its own.'));
    expect(invoke.mock.calls.map((call) => call[1])).toEqual(['SESSION_HAND_OVER', 'CANCEL_TURN']);
    expect(invoke).toHaveBeenLastCalledWith('DAORIS.DRIVER', 'CANCEL_TURN', { payload: { id: 's1a2b3c4' } });
    expect(told.done).not.toHaveBeenCalled();
  });

  it('keeps a refused working hand-over from stopping its turn', async () => {
    invoke.mockImplementation(async () => ({ sessionId: null, kept: false, why: 'waiting', message: 'x' }));
    const { result } = acts();
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null, state: 'working' }), { agent: 'codex', account: '', name: 'Codex' }, told));

    await waitFor(() => expect(told.refused).toHaveBeenCalledWith('It is already waiting to be handed over.'));
    expect(invoke.mock.calls.map((call) => call[1])).toEqual(['SESSION_HAND_OVER']);
  });

  it('says an answer whose briefing did not reach the new conversation, which opened all the same', async () => {
    invoke.mockImplementation(async () => ({ sessionId: 'c2', kept: true, why: 'unbriefed', message: 'x' }));
    const attend = vi.fn();
    const { result, notify } = acts({ attend });
    const told = { done: vi.fn(), refused: vi.fn(), kept: vi.fn() };

    act(() => result.current.handOverNow(session({ kind: 'chat', quest: null, state: 'completed' }), { agent: 'codex', account: '', name: 'Codex' }, told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(attend).toHaveBeenCalledWith('c2');
    expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^The new conversation opened, but what Daoris kept of this one did not reach/));
    expect(told.refused).not.toHaveBeenCalled();
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
