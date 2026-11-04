import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { Session } from '../../data/api';
import { SessionRow } from './SessionRow';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'stub',
  state: 'working',
  kind: 'chat',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

/**
 * A helper's and a continuing part's row says what it spent, as the Parts strip says a part's: its tokens over its turns, or that its
 * door said none; never zero, and nothing on any other row or where nothing answered.
 */
describe('a rail row’s cost', () => {
  it('MIXAGENT3a-iii says what a helper and a continuing part spent, never zero, and nothing on another row', () => {
    const helper = session({ serves: 's9f8e7d6' });
    const { rerender } = render(<SessionRow session={helper} helps="Expose" spent={{ turns: 2, reported: 2, input: 1200, output: 300 }} />);
    expect(screen.getByText(/helps Expose · 1,500 tokens over 2 turns · /i)).toBeInTheDocument();

    const continued = session({ from: 'a1b2c3d4' });
    rerender(<SessionRow session={continued} opening="Go on" continues="Why" spent={{ turns: 1, reported: 0 }} />);
    expect(screen.getByText(/1 turn, its tokens not said/)).toBeInTheDocument();

    rerender(<SessionRow session={continued} opening="Go on" continues="Why" spent={null} />);
    expect(screen.queryByText(/tokens/)).toBeNull();
    rerender(<SessionRow session={session()} opening="Go on" spent={{ turns: 3, reported: 3, input: 10 }} />);
    expect(screen.queryByText(/tokens/)).toBeNull();
  });
});
