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

  /**
   * HEADFIT1: who it worked with is one line, opened in place, and another record starts folded (the relations are the session's
   * own, unlike the parts', which belong to the line of work), as the milestone's fold does.
   */
  it('folds who it worked with to a line, and another record starts folded again', async () => {
    const userEvent = (await import('@testing-library/user-event')).default;
    vi.useRealTimers();
    const relations = { askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [] };
    const { rerender } = render(<AttendedSession session={session()} relations={relations} />);
    expect(screen.getByRole('region', { name: 'Who it worked with' })).toHaveTextContent('asked by z9y8x7w6');
    expect(screen.queryByText(/whose record this machine does not hold/)).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Show who it worked with' }));
    expect(screen.getByText(/session z9y8x7w6, whose record this machine does not hold/)).toBeInTheDocument();

    rerender(<AttendedSession session={session({ id: 's9f8e7d6' })} relations={relations} />);
    expect(screen.queryByText(/whose record this machine does not hold/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Show who it worked with' })).toBeInTheDocument();
  });

  /**
   * RELSPAM1: who it worked with and the milestone were both keyed by the session's id under the record's one parent, and React
   * matched the new fold to the milestone's old one by that key: each draw of a running driven session mounted a fresh fold and left
   * the last one in the page, never unmounted. A working session is drawn again at every change of its record, so its relations line
   * stood eleven times over on the install's window, and moving to another session left the old ones behind too.
   */
  it('keeps one line of who it worked with, however often a running session with a milestone is drawn again', async () => {
    vi.useRealTimers();
    const { SHARED_UNITS } = await import('../projects/git/landingFixtures');
    const relations = { askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [] };
    const running = (n: number, id = 's1a2b3c4') => (
      <AttendedSession
        session={session({ id, kind: 'driven', quest: 'q1', updated: `2026-09-21T11:${String(n).padStart(2, '0')}:00Z` })}
        relations={relations}
        milestone={SHARED_UNITS[1]!}
      />
    );
    const { rerender } = render(running(0));
    for (let n = 1; n <= 10; n += 1) rerender(running(n));
    expect(screen.getAllByRole('region', { name: 'Who it worked with' })).toHaveLength(1);
    expect(screen.getAllByRole('region', { name: 'Where this work stands' })).toHaveLength(1);

    rerender(running(11, 's9f8e7d6'));
    rerender(running(12, 's9f8e7d6'));
    expect(screen.getAllByRole('region', { name: 'Who it worked with' })).toHaveLength(1);
    expect(screen.getAllByRole('region', { name: 'Where this work stands' })).toHaveLength(1);
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
   * FLOWCHART1c (the flowchart design §2.7): the work's milestone opens how its landing branch landed, on that branch's page, by
   * the branch the unit's landing made and never by its session; a unit no landing branch names offers nothing.
   */
  it("opens how its work's landing branch landed from the milestone, and nothing where no landing branch is named", async () => {
    vi.useRealTimers();
    const { SHARED_UNITS, WAITING_UNITS } = await import('../projects/git/landingFixtures');
    const onOpenBranch = vi.fn();
    const { rerender } = render(<AttendedSession session={session()} milestone={SHARED_UNITS[1]!} onOpenBranch={onOpenBranch} />);
    screen.getByRole('button', { name: 'How review/q6-asset-cache landed' }).click();
    expect(onOpenBranch).toHaveBeenCalledWith('engine', 'review/q6-asset-cache');

    rerender(<AttendedSession session={session()} milestone={{ ...WAITING_UNITS[0]!, result: [] }} onOpenBranch={onOpenBranch} />);
    expect(screen.queryByRole('button', { name: /landed$/ })).toBeNull();
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
    // HEADFIT1: folded to its one line, which names how many and how the newest began; its parts are a press away.
    expect(screen.getByRole('navigation', { name: 'Parts' })).toHaveTextContent('Part 3 of 3 · newest: asked by db');
    expect(screen.getByRole('navigation', { name: 'Parts' }).querySelectorAll('button')).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
    const strip = screen.getByRole('navigation', { name: 'Parts' });
    await userEvent.click(strip.querySelectorAll('button')[0]!);
    expect(onShow).toHaveBeenCalledWith('ap000001');
  });

  /**
   * HEADFIT1: the parts open where the person left them while they move among the parts of one line of work (a press on a part
   * shows another session, and the flow they are reading must not fold under it), and another line of work starts folded.
   */
  it('keeps the parts open across the parts of one line of work, and folds them for another line', async () => {
    const { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } = await import('./entries.fixtures');
    const userEvent = (await import('@testing-library/user-event')).default;
    vi.useRealTimers();
    const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((row) => [row.id, { session: row }]));
    const parts = (entry: number, attended: string) => ({ entry: EVIDENCE_ENTRIES[entry]!, attended, rows, onShow: vi.fn() });
    const { rerender } = render(<AttendedSession session={session({ id: 'ap000003' })} parts={parts(0, 'ap000003')} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
    expect(screen.getByRole('button', { name: 'Hide the parts' })).toBeInTheDocument();

    rerender(<AttendedSession session={session({ id: 'ap000002' })} parts={parts(0, 'ap000002')} />);
    expect(screen.getByRole('button', { name: 'Hide the parts' })).toBeInTheDocument();
    expect(screen.getByText('Part 2 of 3')).toBeInTheDocument();

    rerender(<AttendedSession session={session({ id: 'db000003' })} parts={parts(1, 'db000003')} />);
    expect(screen.getByRole('button', { name: 'Show the parts' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hide the parts' })).toBeNull();
  });

  /**
   * FLOWCHART1b (the flowchart design §2.3): Accept's plan, read once its ask is open, is where the parts would land: a dashed node in
   * the parts' flow beside the press, drawn from the same plan and not before it is open. HEADFIT1: in the flow the person opened;
   * Accept's ask does not open it (a block opening under a press elsewhere would move the control just pressed).
   */
  it('draws the landing plan Accept read as a prospective node in the parts\' flow, once its ask is open', async () => {
    const userEvent = (await import('@testing-library/user-event')).default;
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
      expect(screen.queryByRole('listitem', { name: /^Accept would land these on daoris\/t-one/ })).toBeNull();
      await userEvent.click(screen.getByRole('button', { name: 'Show the parts' }));
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
