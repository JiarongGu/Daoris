import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@shenora/react', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@shenora/react')>()),
  getBridge: () => ({ invoke: vi.fn() }),
}));

import type { HelpProposal } from '../help/ProposalCard';
import { settleBound } from './help';

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
