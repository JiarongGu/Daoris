import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Quest } from '../data/api';
import { ChainLine } from './ChainLine';
import { buildChain } from './chain';

// SESS2 H7: the chain above a session as one line of stops, the head's quest named *this quest*, and
// the whole strip a press away.

const quest = (id: string, extra: Partial<Quest> = {}): Quest => ({
  id, from: 'game', to: 'engine', title: `quest ${id}`, body: '', status: 'Open',
  filed: '2026-09-23T00:00:00Z', updated: '2026-09-23T00:00:00Z', ...extra,
});

const QUESTS = [
  quest('a', { from: 'ask #abc123', status: 'Done', title: 'Develop it' }),
  quest('b', { parent: 'a', to: 'game', title: 'Verify it', status: 'Taken', then: [{ to: 'engine', title: 'Report back', body: '' }] }),
];

describe('the chain line', () => {
  it('reads the stops in order, every state in words, and the attended quest as this quest', () => {
    render(<ChainLine chain={buildChain('b', QUESTS, [])} onExpand={() => {}} />);

    const line = screen.getByRole('navigation', { name: 'How this work ran' });
    expect(line.textContent).toMatch(/ask #abc123.*Develop it.*done.*this quest.*taken.*next: engine/);
    // The head already says this quest's title: the line does not say it again.
    expect(within(line).queryByText('Verify it')).toBeNull();
  });

  it('opens another stop\'s quest, and the whole strip, on a press', () => {
    const onQuest = vi.fn();
    const onExpand = vi.fn();
    render(<ChainLine chain={buildChain('b', QUESTS, [])} onQuest={onQuest} onExpand={onExpand} />);

    fireEvent.click(screen.getByRole('button', { name: 'Develop it' }));
    expect(onQuest).toHaveBeenCalledWith(QUESTS[0]);
    fireEvent.click(screen.getByRole('button', { name: 'Show how it ran' }));
    expect(onExpand).toHaveBeenCalled();
  });

  // FABRIC-CLOSE1d2: the words the quest's page reads, in the line's plain ink.
  it('says submitted for a closure graph\'s submitted quest, and source reopened for a step held so', () => {
    const quests = [
      quest('a', { status: 'Taken', closure: 'gph-1', submission: { machine: 'desk', sequence: 1, at: '2026-09-23T02:00:00Z' } }),
      quest('b', { parent: 'a', status: 'Taken', hold: 'source-reopened' }),
    ];
    render(<ChainLine chain={buildChain('b', quests, [])} onExpand={() => {}} />);

    const line = screen.getByRole('navigation', { name: 'How this work ran' });
    expect(line.textContent).toMatch(/submitted.*this quest.*taken.*source reopened/);
  });

  it('keeps a quest no closure holds reading taken', () => {
    render(<ChainLine chain={buildChain('a', [quest('a', { status: 'Taken' })], [])} onExpand={() => {}} />);

    const line = screen.getByRole('navigation', { name: 'How this work ran' });
    expect(line.textContent).toMatch(/this quest.*taken/);
    expect(line.textContent).not.toMatch(/submitted|source reopened/);
  });
});
