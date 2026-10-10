import { createElement, type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke, isAvailable: true }),
}));

import type { HelpProposal } from '../help/ProposalCard';
import { settleBound, useSettleHelp, type HelpSettled } from './help';

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

/**
 * ENTRY1d1: a move to a workspace changes the registry, so an Apply reads it again at once, as the Manage drawer's own
 * *Move to workspace* does, rather than waiting for the driver's watch to say it moved.
 */
describe('what an Apply reads again', () => {
  it('reads the registry again, which a move to a workspace changed', async () => {
    invoke.mockReset();
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { message: 'Applied.', applied: true } : undefined));
    const client = new QueryClient();
    const invalidated = vi.spyOn(client, 'invalidateQueries');
    const wrapper = ({ children }: { children: ReactNode }) => createElement(QueryClientProvider, { client }, children);
    const { result } = renderHook(() => useSettleHelp(), { wrapper });

    await act(async () => { await result.current.mutateAsync({ id: 'p12', apply: true }); });

    const keys = invalidated.mock.calls.map(([filters]) => filters?.queryKey);
    expect(keys).toContainEqual(['registry']);
    expect(keys).toContainEqual(['repositories']);
  });
});
