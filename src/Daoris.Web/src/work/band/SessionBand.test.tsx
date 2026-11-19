import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '../../locales/i18n';
import type { SessionEvent } from '../conversation/conversationTypes';
import { SessionBand } from './SessionBand';

// The band's organism, the bridge mocked: which tasks it offers to stop is its streams' `canStop`, and a stop goes to the session
// that runs the task.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: () => {},
}));

const at = new Date(Date.now() - 60_000).toISOString();
const LIFETIMES: SessionEvent[] = [
  { seq: 1, at, kind: 'task', id: 'bs00', name: 'dev server', by: 'reported' },
  { seq: 2, at, kind: 'task', id: 'bs01', name: 'a probe', by: 'reported' },
];

afterEach(() => invoke.mockReset());

describe('the live band on the session page', () => {
  it('stops a task its harness marks stoppable through the session that runs it, and offers none for one it does not', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_STREAMS') {
        return {
          session: 's1',
          streams: [
            { key: 's1/task/bs00', kind: 'task', name: 'dev server', live: true, state: null, canStop: true },
            { key: 's1/task/bs01', kind: 'task', name: 'a probe', live: true, state: null, canStop: false },
          ],
        };
      }
      if (type === 'STOP_TASK') return { stopped: true };
      if (type === 'TAIL_SESSION') return { session: 's1', lines: [], sequence: 0, live: true, dropped: 0 };
      return {};
    });
    render(
      <QueryClientProvider client={new QueryClient()}>
        <SessionBand session="s1" told={{ session: 's1', lifetimes: LIFETIMES, act: null, open: () => {} }} notify={() => {}} />
      </QueryClientProvider>,
    );

    const probe = document.querySelector<HTMLElement>('[data-band-row="bs01"]')!;
    const server = document.querySelector<HTMLElement>('[data-band-row="bs00"]')!;
    await userEvent.click(await within(server).findByRole('button', { name: 'Stop dev server' }));
    await userEvent.click(within(server).getByRole('button', { name: 'Stop it' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_TASK', { payload: { id: 's1', key: 's1/task/bs00' } }));
    expect(within(probe).queryByRole('button', { name: /^Stop/ })).toBeNull();
    expect(screen.getByRole('region', { name: 'Running 2' })).toBeTruthy();
  });
});
