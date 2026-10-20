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

import type { Session } from '../data/api';
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
   * SESSONE1c (D167 point 5): a session that is a part of an entry of several shows its parts under its head, and a press shows
   * another part; a record alone shows no strip.
   */
  it('shows its entry\'s parts under the head, and shows another part on a press', async () => {
    const { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } = await import('./entries.fixtures');
    const userEvent = (await import('@testing-library/user-event')).default;
    vi.useRealTimers();
    const onShow = vi.fn();
    const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((row) => [row.id, { session: row }]));
    const { rerender } = render(<AttendedSession session={session()} />);
    expect(screen.queryByRole('navigation', { name: 'Parts' })).toBeNull();

    rerender(<AttendedSession session={session({ id: 'ap000003' })} parts={{ entry: EVIDENCE_ENTRIES[0]!, attended: 'ap000003', rows, onShow }} />);
    const strip = screen.getByRole('navigation', { name: 'Parts' });
    await userEvent.click(strip.querySelectorAll('button')[0]!);
    expect(onShow).toHaveBeenCalledWith('ap000001');
  });

  /**
   * FLOWCHART1b (the flowchart design §2.3): Accept's plan, read once its ask is open, is where the parts would land: a dashed node in
   * the parts' flow beside the press, drawn from the same plan and not before it is open.
   */
  it('draws the landing plan Accept read as a prospective node in the parts\' flow, once its ask is open', async () => {
    const { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } = await import('./entries.fixtures');
    vi.useRealTimers();
    vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      const width = this.hasAttribute('data-flow-pane') ? 1200 : 0;
      return { width, height: 0, top: 0, left: 0, right: width, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
    });
    vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
    try {
      const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((row) => [row.id, { session: row }]));
      const plan = { entry: EVIDENCE_ENTRIES[0]!.entry, repository: 'app', form: 'branch', target: 'daoris/t-one', parts: [{ session: 'ap000001' }, { session: 'ap000002' }] };
      const accept = { scope: 'entry' as const, offered: 2, open: false, onOpen: vi.fn(), plans: [plan], onLand: vi.fn() };
      const parts = { entry: EVIDENCE_ENTRIES[0]!, attended: 'ap000003', rows, onShow: vi.fn() };
      const { rerender } = render(<AttendedSession session={session({ id: 'ap000003' })} parts={parts} entryAccept={accept} />);
      expect(screen.queryByText('Accept would land these on daoris/t-one')).toBeNull();

      rerender(<AttendedSession session={session({ id: 'ap000003' })} parts={parts} entryAccept={{ ...accept, open: true }} />);
      expect(screen.getByRole('listitem', { name: /^Accept would land these on daoris\/t-one/ })).toBeInTheDocument();
    } finally {
      vi.restoreAllMocks();
      vi.unstubAllGlobals();
    }
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
