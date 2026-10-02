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

import { sentence } from '../format';
import i18n from '../i18n';
import { keys } from '../queries';
import { useArchiveSessions, useOpenSessionFolder, useSessionGroups, type SessionGrouping } from './sessions';

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

  /** An archive names the sessions and which way, and afterwards the sessions, their groups among them, are asked again. */
  it('archives on DAORIS.DRIVER and asks the sessions again', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ archived: [{ session: 's1', at: '2026-10-02T09:00:00Z' }], kept: [] }));
    const { result } = renderHook(() => useArchiveSessions(), { wrapper: wrapper(client) });

    const answer = await result.current.mutateAsync({ ids: ['s1'], archived: true });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', { payload: { ids: ['s1'], archived: true } });
    expect(answer.archived.map((mark) => mark.session)).toEqual(['s1']);
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions }));
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
});
