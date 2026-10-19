import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// A session's life on this machine, as hooks: the list's groups by state and the archive marks (SESSUX1a, D126).

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => undefined,
}));

import { sentence } from '../locales/format';
import i18n from '../locales/i18n';
import {
  PERMISSION_WAITS_KEY, permissionsAgainIn, useAnswerPermission, useArchiveSessions, useDeleteSession, useGoOnNew, useOpenSessionFolder,
  usePermissionWaits, useSessionGroups, type SessionGrouping,
} from './sessions';

const wrapper = (client: QueryClient) => ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={client}>{children}</QueryClientProvider>
);

const parked: SessionGrouping = {
  session: 's3', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3, awaits: null, awaitsOf: null, work: null,
};

describe('the sessions domain', () => {
  afterEach(() => {
    invoke.mockReset();
  });

  /** The list reads each session's group from the host's one reader, every session or the ones it names. */
  it('asks the groups of DAORIS.DRIVER, for every session or the ones named', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ sessions: [parked] }));

    const every = renderHook(() => useSessionGroups(), { wrapper: wrapper(client) });
    await waitFor(() => expect(every.result.current.data).toEqual([parked]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', {});

    const some = renderHook(() => useSessionGroups(['s3', 's1', 's3']), { wrapper: wrapper(client) });
    await waitFor(() => expect(some.result.current.data).toEqual([parked]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GROUPS', { payload: { ids: ['s3', 's1'] } });
  });

  /** An answer with no rows in it is none, never a crash: a host older than the route says nothing of them. */
  it('reads an answer without rows as no rows', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({}));

    const { result } = renderHook(() => useSessionGroups(), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.data).toEqual([]));
  });

  /**
   * An archive names the sessions and which way. The sessions, their groups among them, are asked again by the driver's
   * `STATE_CHANGED`, in every window (STATEHUB1d, D169 point 6), and by the hook never (STATEHUB1f).
   */
  it('archives on DAORIS.DRIVER and leaves asking the sessions again to the state hub', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ archived: [{ session: 's1', at: '2026-10-02T09:00:00Z' }], kept: [] }));
    const { result } = renderHook(() => useArchiveSessions(), { wrapper: wrapper(client) });

    const answer = await result.current.mutateAsync({ ids: ['s1'], archived: true });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', { payload: { ids: ['s1'], archived: true } });
    expect(answer.archived.map((mark) => mark.session)).toEqual(['s1']);
    expect(invalidate).not.toHaveBeenCalled();
  });

  /**
   * A refusal reaches the person in their own language, naming the session; one that needs the person names which of
   * its two groups kept it, through the catalogue's context.
   */
  it('says an archive refused in the catalogue, naming what kept it', async () => {
    const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });

    try {
      await i18n.changeLanguage('en');
      expect(sentence(refusal('SESSION_LIVE', { session: 's2' }))).toMatch(/^s2 is still running/);
      expect(sentence(refusal('SESSION_UNKNOWN', { session: 's9' }))).toMatch(/no session s9/);
      expect(sentence(refusal('SESSION_NEEDS_YOU', { session: 's3', group: 'you', context: 'you' }))).toMatch(/s3 is waiting on you/);
      expect(sentence(refusal('SESSION_NEEDS_YOU', { session: 's4', group: 'review', context: 'review' }))).toMatch(/s4 has work to review/);

      await i18n.changeLanguage('zh');
      expect(sentence(refusal('SESSION_NEEDS_YOU', { session: 's3', group: 'you', context: 'you' }))).toMatch(/s3 正在等你处理/);
      expect(sentence(refusal('SESSION_NEEDS_YOU', { session: 's4', group: 'review', context: 'review' }))).toMatch(/s4 有待审阅的工作/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * SESSUX1d (D126 §3.5): *Open folder* names the session and nothing else, since the module names the folder from its
   * record; a folder this machine no longer holds is said in the catalogue, as is an id no record has.
   */
  it('opens a session’s folder on DAORIS.DRIVER by its id alone, and says a gone folder in the catalogue', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({ opened: true }));
    const { result } = renderHook(() => useOpenSessionFolder(), { wrapper: wrapper(client) });

    expect(await result.current.mutateAsync('s1')).toEqual({ opened: true });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_OPEN_FOLDER', { payload: { id: 's1' } });

    const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });
    try {
      await i18n.changeLanguage('en');
      expect(sentence(refusal('SESSION_FOLDER_GONE', { session: 's1' }))).toMatch(/^The folder s1 worked in is not on this machine/);
      expect(sentence(refusal('SESSION_UNKNOWN', { session: 's9', context: 'folder' }))).toMatch(/no folder to open/);
      await i18n.changeLanguage('zh');
      expect(sentence(refusal('SESSION_FOLDER_GONE', { session: 's1' }))).toMatch(/^s1 工作过的文件夹不在本机/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * SESSUX1f (D126 §5.4): a delete names the session alone, and each refusal reaches the person in the catalogue, naming
   * what kept it; which named it, and the archive's codes asked of a delete, by context. The sessions are asked again by the
   * state hub (STATEHUB1d).
   */
  it('deletes on DAORIS.DRIVER by its id, leaves the sessions to the state hub, and says each refusal in the catalogue', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ deleted: 's1', removed: ['record'] }));
    const { result } = renderHook(() => useDeleteSession(), { wrapper: wrapper(client) });

    expect(await result.current.mutateAsync('s1')).toEqual({ deleted: 's1', removed: ['record'] });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: 's1' } });
    expect(invalidate).not.toHaveBeenCalled();

    const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });
    try {
      await i18n.changeLanguage('en');
      expect(sentence(refusal('SESSION_LIVE', { session: 's2', context: 'delete' }))).toBe('s2 is still running, so it was not deleted. Stop it first.');
      expect(sentence(refusal('SESSION_UNKNOWN', { session: 's9', context: 'delete' }))).toMatch(/nothing to delete/);
      expect(sentence(refusal('SESSION_NOT_OURS', { session: 's3', machine: 'laptop' }))).toMatch(/^s3 ran on laptop/);
      expect(sentence(refusal('SESSION_SERVED_QUEST', { session: 's4', quest: 'q1' }))).toMatch(/^s4 worked on #q1/);
      expect(sentence(refusal('SESSION_NAMED', { session: 's5', context: 'ask', ask: 'a1', quest: '' }))).toMatch(/^Ask #a1 names s5 as its intake/);
      expect(sentence(refusal('SESSION_NAMED', { session: 's5', context: 'quest', ask: '', quest: 'q2' }))).toMatch(/^#q2 was published by s5/);
      expect(sentence(refusal('SESSION_NAMED', { session: 's5', context: 'landing', ask: '', quest: '' }))).toMatch(/^A landing names s5/);
      expect(sentence(refusal('SESSION_TREE_HERE', { session: 's6' }))).toMatch(/^s6’s tree is still on this machine/);
      expect(sentence(refusal('SESSION_ON_REMOTE', { session: 's7', workspace: 'aurora' }))).toMatch(/^The remote for aurora holds s7/);
      await i18n.changeLanguage('zh');
      expect(sentence(refusal('SESSION_NAMED', { session: 's5', context: 'quest', ask: '', quest: 'q2' }))).toMatch(/^委托 #q2 是由 s5 发布的/);
      expect(sentence(refusal('SESSION_ON_REMOTE', { session: 's7', workspace: 'aurora' }))).toMatch(/^aurora 的远端保存着 s7/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * CHATTAKE1c (D126's CHATTAKE1 note): a chat that took a quest is refused as having served one, and no quest records
   * which session took it, so the refusal names none. The shell sends the quest empty, and the sentence says the session
   * took a quest rather than reading `#`; one that names its quest still names it.
   */
  it('says a delete refused for a quest a chat took without naming one, in both languages', async () => {
    const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });
    const took = refusal('SESSION_SERVED_QUEST', { session: 's8', quest: '' });
    const unsent = refusal('SESSION_SERVED_QUEST', { session: 's8' });

    try {
      await i18n.changeLanguage('en');
      expect(sentence(took)).toBe('s8 took a quest, and its record is that work’s, so it was not deleted. Archive it instead.');
      expect(sentence(unsent)).toBe(sentence(took));
      expect(sentence(refusal('SESSION_SERVED_QUEST', { session: 's4', quest: 'q1' }))).toMatch(/^s4 worked on #q1,/);

      await i18n.changeLanguage('zh');
      expect(sentence(took)).toBe('s8 接下过一个委托，它的记录属于那项工作，所以没有删除。请改为归档。');
      expect(sentence(unsent)).toBe(sentence(took));
      expect(sentence(refusal('SESSION_SERVED_QUEST', { session: 's4', quest: 'q1' }))).toMatch(/^s4 为委托 #q1 工作过/);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * MSG1g2 (D137 §2.2): *Go on in a new session* names the session alone, and its answer is read field by field, so a
   * refusal keeps its code and a shell older than the route answers no choice kept; the sessions and the driver are asked
   * again by the state hub (STATEHUB1d), since the loop takes the choice up at its next look.
   */
  it('asks a new session of DAORIS.DRIVER by the session’s id, and reads its answer defensively', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ sent: false, why: 'closed', message: '#q1 has closed' }));
    const { result } = renderHook(() => useGoOnNew(), { wrapper: wrapper(client) });

    expect(await result.current.mutateAsync('s1')).toEqual({ sent: false, why: 'closed', message: '#q1 has closed' });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_ON_NEW', { payload: { id: 's1' } });
    expect(invalidate).not.toHaveBeenCalled();

    invoke.mockImplementation(async () => ({ sent: true, why: null, message: 'it goes on in a new session' }));
    expect(await result.current.mutateAsync('s1')).toEqual({ sent: true, why: null, message: 'it goes on in a new session' });
    invoke.mockImplementation(async () => null);
    expect(await result.current.mutateAsync('s1')).toEqual({ sent: false, why: null, message: '' });
  });
});

/**
 * AUTONOMY1b3c (D184's AUTONOMY1b3a note): what this machine's driven sessions hold for the person under asking, and the person's
 * answer. Neither route publishes, so the waits are asked every few seconds while a session works or one is held, and an answer
 * asks them again itself.
 */
describe('the permissions held for the person', () => {
  afterEach(() => {
    invoke.mockReset();
  });

  const held = {
    session: 'd1', request: '900', call: 'tc-1', title: 'npm test', kind: 'execute', since: '2026-10-20T09:00:00Z',
    choices: [{ id: 'yes', name: 'Allow', kind: 'allow_once' }, { id: 'no', name: 'Reject', kind: 'reject_once' }],
  };

  it('asks PERMISSION_WAITS of DAORIS.DRIVER, and reads its rows defensively', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({
      waits: [
        held,
        // A row naming no session or no request has nothing to answer, and a choice without its id nothing to send.
        { ...held, session: '' }, { ...held, request: null }, null,
        { session: 'd2', request: '7', since: '2026-10-20T09:01:00Z', choices: [{ id: 'x', name: 'Allow', kind: 'allow_once' }, { name: 'gone' }] },
      ],
    }));

    const { result } = renderHook(() => usePermissionWaits(false), { wrapper: wrapper(client) });

    await waitFor(() => expect(result.current.data).toEqual([
      held,
      { session: 'd2', request: '7', call: null, title: null, kind: null, since: '2026-10-20T09:01:00Z', choices: [{ id: 'x', name: 'Allow', kind: 'allow_once' }] },
    ]));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PERMISSION_WAITS', {});
  });

  it('reads an answer without rows as none held', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    invoke.mockImplementation(async () => ({}));
    const { result } = renderHook(() => usePermissionWaits(false), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.data).toEqual([]));
  });

  it('is asked again every few seconds while a session works or one is held, and never otherwise', () => {
    expect(permissionsAgainIn(true, 0)).toBeGreaterThan(0);
    expect(permissionsAgainIn(false, 1)).toBeGreaterThan(0);
    expect(permissionsAgainIn(true, 0)).toBeLessThanOrEqual(5_000);
    expect(permissionsAgainIn(false, 0)).toBe(false);
  });

  it('answers on SESSION_PERMISSION_ANSWER with the chosen option, and asks the waits again itself', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ answered: true, why: null, message: 'Answered `Allow`.' }));
    const { result } = renderHook(() => useAnswerPermission(), { wrapper: wrapper(client) });

    expect(await result.current.mutateAsync({ session: 'd1', request: '900', option: 'yes' }))
      .toEqual({ answered: true, why: null, message: 'Answered `Allow`.' });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_PERMISSION_ANSWER', { payload: { session: 'd1', request: '900', option: 'yes' } });
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: PERMISSION_WAITS_KEY }));

    // A refusal is the answer, by its code; a shell older than the route answers nothing answered.
    invoke.mockImplementation(async () => ({ answered: false, why: 'unheld', message: 'Nothing was answered.' }));
    expect(await result.current.mutateAsync({ session: 'd1', request: '900', option: 'yes' }))
      .toEqual({ answered: false, why: 'unheld', message: 'Nothing was answered.' });
    invoke.mockImplementation(async () => null);
    expect(await result.current.mutateAsync({ session: 'd1', request: '900', option: 'no' })).toEqual({ answered: false, why: null, message: '' });
  });
});
