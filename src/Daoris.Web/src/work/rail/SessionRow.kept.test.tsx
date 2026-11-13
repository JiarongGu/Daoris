import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { Session } from '../../data/api';
import { SessionRow } from './SessionRow';

// What the person kept of a conversation, as its row wears it: the name they gave it is its title above its ask's and its first
// words, and a pin is marked. A molecule, so each is reached by passing it.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', repository: 'engine', door: 'claude-code', state: 'working', kind: 'chat',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', ...over,
});

describe('a session row of a conversation the person kept something of', () => {
  it('wears the name its person gave the conversation and marks it pinned', () => {
    render(<SessionRow
      session={session()} opening="how do I drive engine?"
      grouping={{ session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false, name: 'Workspaces', pinned: true }}
    />);

    expect(screen.getByText('Workspaces')).toBeInTheDocument();
    expect(screen.queryByText('how do I drive engine?')).toBeNull();
    expect(screen.getByText('pinned')).toBeInTheDocument();
  });

  it('wears its first words and no mark where nothing was kept', () => {
    render(<SessionRow
      session={session()} opening="how do I drive engine?"
      grouping={{ session: 's1a2b3c4', group: 'working', shown: 'working', archived: false, teammate: false }}
    />);

    expect(screen.getByText('how do I drive engine?')).toBeInTheDocument();
    expect(screen.queryByText('pinned')).toBeNull();
  });
});
