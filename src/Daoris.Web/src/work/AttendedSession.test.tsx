import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// The bridge mocked as present, because the promoted stream is the half a browser structurally
// cannot reach (frontend architecture §4) and the point of this region is that it is one surface.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import type { Session } from '../api';
import { AttendedSession } from './AttendedSession';

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  quest: null,
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'chat',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

describe('the attended session', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    invoke.mockImplementation(async () => ({ lines: [], live: false, dropped: 0 }));
  });
  afterEach(() => {
    vi.useRealTimers();
    invoke.mockReset();
    eventHandlers.clear();
  });

  it('offers a designed nothing when the person has not chosen a session', () => {
    render(<AttendedSession session={null} />);
    expect(screen.getByText('Nothing attended')).toBeInTheDocument();
  });

  it('is the record and the observed layer, in that order', () => {
    render(<AttendedSession session={session()} />);

    const head = screen.getByRole('heading', { level: 2 });
    expect(head).toHaveTextContent('conversation');
    expect(screen.getByText('Timeline')).toBeInTheDocument();
    expect(head.compareDocumentPosition(screen.getByText('Timeline')))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  /**
   * The stream has ONE home (design §3, D55) and it is the frame's output panel, not this region.
   * Asserted here because a second console rendered beside the record is exactly the duplication
   * the rule exists to prevent — and it stays invisible until two of them disagree.
   */
  it('renders no console of its own — the stream lives in the panel', () => {
    render(<AttendedSession session={session()} />);

    expect(invoke).not.toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'TAIL_SESSION', expect.anything(),
    );
  });
});
