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
import { AttendedSession, noteIsInTheHead } from './AttendedSession';

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

  /**
   * UX7c (D152 §7, amending SESS2 H7's remembered choice): the chain opens folded to its one line on every session, whatever
   * was opened on the last one, so the conversation is not pushed down by a strip the person once opened elsewhere.
   */
  it('opens the chain folded on every session, whatever the last one showed', async () => {
    vi.useRealTimers();
    window.localStorage.setItem('daoris.chainWhole', '1');
    const { buildChain } = await import('../map/chain');
    const quests = [
      { id: 'q1', from: 'ask #a1', to: 'engine', title: 'Develop it', body: '', status: 'Done' as const, filed: NOW.toISOString(), updated: NOW.toISOString() },
      { id: 'q2', from: 'ask #a1', to: 'engine', title: 'Verify it', body: '', status: 'Open' as const, filed: NOW.toISOString(), updated: NOW.toISOString(), parent: 'q1' },
    ];
    const driven = session({ kind: 'driven', quest: 'q2' });
    const { rerender } = render(<AttendedSession session={driven} chain={buildChain('q2', quests, [driven])} />);
    expect(screen.queryByRole('region', { name: 'How this work ran' })).toBeNull();

    screen.getByRole('button', { name: /Show how it ran/ }).click();
    expect(await screen.findByRole('region', { name: 'How this work ran' })).toBeInTheDocument();

    const next = session({ id: 's9f8e7d6', kind: 'driven', quest: 'q2' });
    rerender(<AttendedSession session={next} chain={buildChain('q2', quests, [next])} />);
    expect(screen.queryByRole('region', { name: 'How this work ran' })).toBeNull();
    window.localStorage.removeItem('daoris.chainWhole');
  });

  it('offers a designed nothing when the person has not chosen a session', () => {
    render(<AttendedSession session={null} />);
    expect(screen.getByText('Nothing attended')).toBeInTheDocument();
  });

  it('is the record and the observed layer, in that order', () => {
    render(<AttendedSession session={session()} timeline="always" />);

    const head = screen.getByRole('heading', { level: 2 });
    expect(head).toHaveTextContent('Chat');
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

  /**
   * REV3 web-work F8: a detached window has no dock, so it draws the timeline itself, at every width.
   */
  it('keeps its timeline where there is no dock to hold it', () => {
    render(<AttendedSession session={session()} timeline="always" />);
    expect(screen.getByRole('heading', { name: 'Timeline' })).toBeInTheDocument();
  });

  /**
   * FRAME6: the frame's dock is there at every width now — docked, cramped, full or closed to a strip
   * of its tabs — so the column's narrow-window copy, which drew the timeline twice, is gone.
   */
  it('leaves its timeline to the dock by default, at every width', () => {
    render(<AttendedSession session={session()} />);
    expect(screen.queryByRole('heading', { name: 'Timeline' })).toBeNull();
  });

  /**
   * One rule for where a parked session's note is shown, read by the column AND the dock (REV3). The
   * dock hid the note of every parked session, while the head shows it only where it can be answered
   * here — so another machine's parked note, on a wide window, was shown nowhere.
   */
  it('says the note is in the head only where the session can be answered here', () => {
    const parked = session({ state: 'awaiting-person', note: 'Two ways forward.' });
    expect(noteIsInTheHead(parked, true)).toBe(true);
    expect(noteIsInTheHead(parked, false)).toBe(false);
    expect(noteIsInTheHead(session(), true)).toBe(false);
  });
});
