import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Quest } from '../api';
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
    expect(line.textContent).toMatch(/ask #abc123.*Develop it.*Done.*this quest.*Taken.*next: engine/);
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
});
