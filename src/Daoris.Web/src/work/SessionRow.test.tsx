import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Quest, Session, SessionState } from '../api';
import { SessionRow } from './SessionRow';

// A molecule, so every state below is reached by PASSING it (components plan §2). There is no
// bridge here, no query client and no provider — if this file ever needed one, the rule that makes
// the working surface reviewable would already have been broken.

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
});

const STATES: SessionState[] = [
  'queued', 'starting', 'working', 'awaiting-person',
  'completed', 'declined', 'stood-down', 'failed', 'stopped',
];

describe('a session row', () => {
  // The clock is pinned: every duration below is a fact about the fixture, not about when the
  // suite ran. userEvent drives the same fake clock rather than waiting on a real one.
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it('leads with what the session is FOR, and says how long it has been going', () => {
    render(<SessionRow session={session({ quest: '7a82cc' })} quest={quest()} />);

    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    expect(screen.getByText('working')).toBeInTheDocument();
    // Started 09:46, now 12:00 — the fact "moved 4m ago" cannot carry (D55).
    expect(screen.getByText('2h 14m')).toBeInTheDocument();
    expect(screen.getByText(/moved 4m ago/)).toBeInTheDocument();
  });

  /**
   * RAIL2: a live chat's last move is its last turn. The record moves on state changes only, so
   * seconds after an answer a chat read "moved 4m ago"; the driver's last turn, when later, is what
   * the row says — written with an offset where the record writes Z, so compared as times.
   */
  it('says a live chat moved when its last turn ended, when that is later than its record', () => {
    const chat = session({ kind: 'chat' });
    const { rerender } = render(<SessionRow session={chat} lastTurn="2026-09-21T11:59:30+00:00" />);
    expect(screen.getByText(/moved just now/)).toBeInTheDocument();

    // An earlier turn says nothing the record does not already say better.
    rerender(<SessionRow session={chat} lastTurn="2026-09-21T11:00:00+00:00" />);
    expect(screen.getByText(/moved 4m ago/)).toBeInTheDocument();
  });

  it('wears every one of the nine states, mark and word together', () => {
    for (const state of STATES) {
      const { container, unmount } = render(<SessionRow session={session({ state })} />);
      expect(screen.getByText(i18n.t(`sessionState.${state}`))).toBeInTheDocument();
      expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull();
      unmount();
    }
  });

  /**
   * UX5 U17, decided by the reference console: a live chat whose turn has ended is idle, a quiet
   * mark and the word, and working only while a turn is in flight. It read *working* between turns.
   * Nothing known is the record's own word, never a guess.
   */
  it('says a live chat between turns is idle, and working only while a turn runs', () => {
    const chat = session({ kind: 'chat' });

    const { rerender } = render(<SessionRow session={chat} taking={false} />);
    expect(screen.getByText('idle')).toBeInTheDocument();
    expect(screen.queryByText('working')).toBeNull();

    rerender(<SessionRow session={chat} taking />);
    expect(screen.getByText('working')).toBeInTheDocument();

    rerender(<SessionRow session={chat} />);
    expect(screen.getByText('working')).toBeInTheDocument();

    // Driven work is one long turn: it is working whatever a queue says.
    rerender(<SessionRow session={session()} taking={false} />);
    expect(screen.getByText('working')).toBeInTheDocument();
  });

  it('says which way in a session was entered — driven work and a conversation read alike otherwise', () => {
    const { unmount } = render(<SessionRow session={session({ kind: 'driven' })} />);
    expect(screen.getByText(/driven/)).toBeInTheDocument();
    unmount();

    render(<SessionRow session={session({ kind: 'chat' })} />);
    expect(screen.getByText(/chat/)).toBeInTheDocument();
  });

  /** INT4g: an intake is a chat only by the way it was opened; the row says what it is. */
  it('says an intake is one, serving its ask — not a chat', () => {
    render(<SessionRow session={session({ kind: 'chat', repository: 'ask #0fda18', ask: '0fda18' })} />);

    expect(screen.getByText('intake for ask #0fda18')).toBeInTheDocument();
    expect(screen.getByText(/^intake · moved/)).toBeInTheDocument();
    expect(screen.queryByText(/chat/)).toBeNull();
  });

  it('names a session with no quest by its derived identity rather than by nothing', () => {
    render(<SessionRow session={session({ kind: 'chat', quest: null })} />);
    expect(screen.getByText('conversation')).toBeInTheDocument();
  });

  /**
   * The rail is ~18rem wide and a quest title is a sentence. The row truncates and keeps the whole
   * title reachable, rather than wrapping to four lines or cutting the text out of the DOM.
   */
  it('truncates a long title in the rail while keeping all of it available', () => {
    const long = '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取';
    render(<SessionRow session={session({ quest: '7a82cc' })} quest={quest({ title: long })} />);

    const title = screen.getByText(long);
    expect(title.className).toContain('truncate');
    expect(title).toHaveAttribute('title', long);
  });

  it('marks the attended session for a reader who cannot see the accent', () => {
    const { rerender } = render(<SessionRow session={session()} selected />);
    expect(screen.getByRole('button')).toHaveAttribute('aria-current', 'true');

    rerender(<SessionRow session={session()} />);
    expect(screen.getByRole('button')).not.toHaveAttribute('aria-current');
  });

  /**
   * Daoris is multi-machine by construction (D47) and the feed keys a mirrored record by
   * `origin/id`. Silence means here — a row that stamped every session with this machine's name
   * would spend the rail's scarcest space on the unsurprising half.
   */
  it('names the machine when the session runs on another one, and stays quiet when it does not', () => {
    const { unmount } = render(<SessionRow session={session({ id: 'person@machine-a/s1a2b3c4' })} />);
    expect(screen.getByText(/on person@machine-a/)).toBeInTheDocument();
    unmount();

    render(<SessionRow session={session()} />);
    expect(screen.queryByText(/ on /)).not.toBeInTheDocument();
  });

  /**
   * D51 made the tree the unit of exclusion, so a repository can hold two sessions at once and the
   * group header alone can no longer say which is where. The root case is the one the first
   * real-window pass caught: an ordinary session carries the ROOT's path in `tree`, so "has a
   * tree" would have labelled every row.
   */
  it('names a working tree the session opened for itself, and nothing for the registered root', () => {
    const root = 'C:/checkouts/engine';
    const { unmount } = render(<SessionRow
      root={root}
      session={session({ tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget' })}
    />);
    expect(screen.getByText(/in streaming-budget/)).toBeInTheDocument();
    unmount();

    const inRoot = render(<SessionRow root={root} session={session({ tree: root })} />);
    expect(screen.queryByText(/ in /)).not.toBeInTheDocument();
    inRoot.unmount();

    render(<SessionRow session={session({ tree: null })} />);
    expect(screen.queryByText(/ in /)).not.toBeInTheDocument();
  });

  it('measures a finished session to where it ended, not to now', () => {
    render(<SessionRow session={session({
      state: 'completed', created: '2026-09-21T09:00:00Z', updated: '2026-09-21T09:45:00Z',
    })}
    />);
    expect(screen.getByText('45m')).toBeInTheDocument();
  });

  it('renders in the active language, chrome and derived name alike', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionRow session={session({ kind: 'chat', quest: null, state: 'awaiting-person' })} />);

    expect(screen.getByText('等待人工')).toBeInTheDocument();
    expect(screen.getAllByText('对话').length).toBeGreaterThan(0);
    await i18n.changeLanguage('en');
  });
});

/**
 * RAIL1: a conversation is named by its first line, and a row carries a small menu of what has no other
 * home — its own window, its review, its id. The session's verbs (finish, stop) stay where they are
 * owned (D56), so they are never here.
 */
describe('a conversation\'s row', () => {
  it('is named by the first thing the person said in it', () => {
    render(<SessionRow session={session({ kind: 'chat' })} opening="Cap the hydration per frame" />);
    expect(screen.getByText('Cap the hydration per frame')).toBeInTheDocument();
  });

  it('offers its own window, its review and its id — and each does what it says', async () => {
    const detach = vi.fn();
    const review = vi.fn();
    const copy = vi.fn();
    render(
      <SessionRow session={session({ kind: 'chat' })} opening="Cap the hydration" onDetach={detach} onReview={review} onCopy={copy} />,
    );

    const user = userEvent.setup();
    screen.getByRole('button', { name: 'more for Cap the hydration' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent))
      .toEqual(['Open in its own window', 'Review its work', 'Copy its id']);
    expect(screen.queryByRole('menuitem', { name: /stop|finish/i })).toBeNull();

    await user.keyboard('{Enter}');
    expect(detach).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('offers no menu where the rail gave it nothing to do', () => {
    render(<SessionRow session={session()} />);
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });
});

/** No pinned clock here: userEvent's own waits are real, and nothing below reads a duration. */
describe('choosing a session', () => {
  it('hands the frame the id it was clicked with — selection is the frame\'s to hold', async () => {
    const select = vi.fn();
    render(<SessionRow session={session()} onSelect={select} />);

    await userEvent.click(screen.getByRole('button'));
    expect(select).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('is inert with nowhere to report to — a rail rendered read-only still renders', async () => {
    render(<SessionRow session={session()} />);
    await userEvent.click(screen.getByRole('button'));
    expect(screen.getByRole('button')).toBeInTheDocument();
  });
});
