import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Quest, Session, SessionState } from '../api';
import { moment } from '../format';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import type { SessionGrouping } from './groups';
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

    expect(screen.getByText('Intake for ask #0fda18')).toBeInTheDocument();
    expect(screen.getByText(/^intake · moved/)).toBeInTheDocument();
    expect(screen.queryByText(/chat/)).toBeNull();
  });

  /** XAGENT1g (the second-agent design §9): a second opinion's reviewer is marked as one, not as a chat. */
  it('marks a second opinion’s reviewer as one — not a chat', () => {
    render(<SessionRow session={session({ kind: 'chat', repository: 'engine', opinion: 'o1a2b3c4' })} />);

    expect(screen.getByText('Second opinion')).toBeInTheDocument();
    expect(screen.getByText(/^second opinion · moved/)).toBeInTheDocument();
    expect(screen.queryByText(/chat/)).toBeNull();
  });

  it('names a session with no quest by its derived identity rather than by nothing', () => {
    render(<SessionRow session={session({ kind: 'chat', quest: null })} />);
    expect(screen.getByText('Chat')).toBeInTheDocument();
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

  /**
   * LOOK2b: the rail said *in s-2394e5d9* after the landing had tidied that tree away and its branch had gone. It says
   * where the work landed, as the review does (D113): while the landed branch stands, or once the tree is gone. A tree
   * still here after its branch went is the session's again, and a tree gone with no landing is not claimed at all.
   */
  it('says where the work landed once its landing took the tree, and never names a tree that is gone', () => {
    const tree = 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9';
    const landed = { repository: 'engine', branch: 'feature/7a82cc-streaming-budget' };
    const row = (where: Parameters<typeof SessionRow>[0]['where']) =>
      render(<SessionRow root="C:/checkouts/engine" session={session({ state: 'completed', tree })} where={where} />);

    let shown = row({ treeGone: true, landed: { ...landed, state: 'standing' } });
    expect(screen.getByText(/landed on feature\/7a82cc-streaming-budget/)).toBeInTheDocument();
    expect(screen.queryByText(/in s-2394e5d9/)).not.toBeInTheDocument();
    shown.unmount();

    shown = row({ treeGone: true, landed: { ...landed, state: 'gone' } });
    expect(screen.getByText(/landed on feature\/7a82cc-streaming-budget, gone since/)).toBeInTheDocument();
    shown.unmount();

    // Landed without a tidy: the branch stands and the tree stays, and the review reads as landed.
    shown = row({ treeGone: false, landed: { ...landed, state: 'standing' } });
    expect(screen.getByText(/landed on feature\/7a82cc-streaming-budget/)).toBeInTheDocument();
    shown.unmount();

    // Carried on in its tree after its branch went (WSR6): the tree is where its work is.
    shown = row({ treeGone: false, landed: { ...landed, state: 'gone' } });
    expect(screen.getByText(/in s-2394e5d9/)).toBeInTheDocument();
    expect(screen.queryByText(/landed on/)).not.toBeInTheDocument();
    shown.unmount();

    // Merged and tidied, which records no landing: nothing is claimed of a tree that is gone.
    shown = row({ treeGone: true, landed: null });
    expect(screen.queryByText(/ in /)).not.toBeInTheDocument();
    expect(screen.queryByText(/landed on/)).not.toBeInTheDocument();
    shown.unmount();

    // Not answered (a browser, or before the machine replied): the tree, as before.
    row(undefined);
    expect(screen.getByText(/in s-2394e5d9/)).toBeInTheDocument();
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

    expect(screen.getByText('等你处理')).toBeInTheDocument();
    expect(screen.getAllByText('聊天').length).toBeGreaterThan(0);
    await i18n.changeLanguage('en');
  });
});

/**
 * SESSUX1c, D126 §2.2: a row wears the word the list's reader derived for it, its mark and its hue, and its line says
 * what the group it is in is about: how many sessions failed before its quest parked, which question its quest waits
 * on, what its own tree holds to review. By state no group header names a repository, so the row's line does.
 */
describe("a row as the list's reader places it", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  const placed = (over: Partial<SessionGrouping> & Pick<SessionGrouping, 'group' | 'shown'>): SessionGrouping => ({
    session: 's1a2b3c4', archived: false, teammate: false, ...over,
  });

  /** A parked quest's last session waits on the person: open's hue and the waiting mark, never the failure's red. */
  it('says parked in the waiting hue, and how many sessions failed before its quest parked', () => {
    render(
      <SessionRow session={session({ state: 'failed', quest: '7a82cc' })} quest={quest()} grouping={placed({ group: 'you', shown: 'parked', strikes: 3 })} />,
    );

    const word = screen.getByText('parked');
    expect(word.className).toContain('text-ink-open');
    expect(screen.queryByText('failed')).toBeNull();
    expect(screen.getByText(/after 3 failed sessions/)).toBeInTheDocument();
  });

  it('says a parked quest parked after its failed sessions where the count is not known', () => {
    render(<SessionRow session={session({ state: 'failed' })} grouping={placed({ group: 'you', shown: 'parked', strikes: null })} />);
    expect(screen.getByText(/after its failed sessions/)).toBeInTheDocument();
  });

  it('says awaiting reply, quietly, and the question its quest waits on and who it was asked of', () => {
    const { rerender } = render(
      <SessionRow
        session={session({ state: 'completed', quest: '7a82cc' })}
        quest={quest()}
        grouping={placed({ group: 'later', shown: 'awaiting-reply', awaits: 'q9q9q9', awaitsOf: 'game' })}
      />,
    );

    expect(screen.getByText('awaiting reply').className).not.toMatch(/(?:st|ink)-open/);
    expect(screen.getByText(/waits on #q9q9q9, asked of game/)).toBeInTheDocument();

    rerender(<SessionRow session={session({ state: 'completed' })} grouping={placed({ group: 'later', shown: 'awaiting-reply', awaits: 'q9q9q9' })} />);
    expect(screen.getByText(/waits on #q9q9q9/)).toBeInTheDocument();
  });

  /** To review keeps its own state's word; its line says what its tree holds that no branch of the person's does. */
  it('says what a session to review left in its tree, under its own word', () => {
    const stopped = session({ state: 'stopped' });
    const { rerender } = render(<SessionRow session={stopped} grouping={placed({ group: 'review', shown: 'stopped', work: { commits: 3, uncommitted: 0 } })} />);
    expect(screen.getByText('stopped')).toBeInTheDocument();
    expect(screen.getByText(/3 commits to review/)).toBeInTheDocument();

    rerender(<SessionRow session={stopped} grouping={placed({ group: 'review', shown: 'stopped', work: { commits: 0, uncommitted: 2 } })} />);
    expect(screen.getByText(/uncommitted changes/)).toBeInTheDocument();

    // A count git could not give is still work: said as work, never as nothing.
    rerender(<SessionRow session={stopped} grouping={placed({ group: 'review', shown: 'stopped', work: { commits: null, uncommitted: null } })} />);
    expect(screen.getByText(/work to review/)).toBeInTheDocument();
  });

  /**
   * MSG1f2 (D137 §3.2): an ended record the person's words wait on reads *going on* where the reader says the same session
   * goes on with them, and, resting under Resumes later, its line names what holds them: the person's hold, the cap, a
   * cool-off's reset, or the planner's own sentence for anything else.
   */
  it('says going on, and what holds the words of one that resumes later', () => {
    const ended = session({ state: 'completed', quest: '7a82cc' });
    const { rerender } = render(<SessionRow session={ended} grouping={placed({ group: 'working', shown: 'going-on' })} />);
    expect(screen.getByText('going on')).toBeInTheDocument();

    const held = (holds: SessionGrouping['holds']) =>
      rerender(<SessionRow session={ended} grouping={placed({ group: 'later', shown: 'completed', holds })} />);
    held({ why: 'hold', reason: '`engine` is held by the person.', repository: 'engine' });
    expect(screen.getByText(/goes on with your words once engine is no longer held/)).toBeInTheDocument();
    held({ why: 'cap', reason: 'the concurrency cap (2) is spent — it frees as sessions finish.' });
    expect(screen.getByText(/goes on with your words when a running session here ends/)).toBeInTheDocument();
    held({ why: 'cooling', reason: 'its account is cooling.', until: '2026-10-04T12:30:00Z' });
    const reset = `goes on with your words once its account cools, at ${moment('2026-10-04T12:30:00Z')}`;
    expect(screen.getByText((said) => said.includes(reset))).toBeInTheDocument();
    held({ why: 'busy', reason: 'session `s9` is active in the tree `#7a82cc` goes back into — one session per tree.' });
    expect(screen.getByText(/your words wait: session `s9` is active in the tree/)).toBeInTheDocument();
    expect(screen.getByTitle(/You wrote to this session/)).toBeInTheDocument();
  });

  /** A pause's line names whose pause, as before; what holds the words adds nothing beside it. */
  it('lets a pause name itself where the words wait on it', () => {
    render(
      <SessionRow
        session={session({ state: 'stopped' })}
        grouping={placed({ group: 'later', shown: 'stopped', pausedBy: { scope: 'ask', id: 'a1b2c3' }, holds: { why: 'paused', reason: 'paused.' } })}
      />,
    );
    expect(screen.getByText(/paused with ask #a1b2c3/)).toBeInTheDocument();
    expect(screen.queryByText(/your words/)).toBeNull();
  });

  it('names its repository first on its line where no group header does', () => {
    render(<SessionRow session={session()} place="engine" />);
    expect(screen.getByText(/^engine · driven · /)).toBeInTheDocument();
  });

  it('says it in 中文 too', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<SessionRow session={session({ state: 'failed' })} place="engine" grouping={placed({ group: 'you', shown: 'parked', strikes: 3 })} />);
      expect(screen.getByText('已挂起')).toBeInTheDocument();
      expect(screen.getByText(/^engine · 3 个会话失败后挂起/)).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
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

  it('offers no menu where the rail gave it nothing to do', () => {
    render(<SessionRow session={session()} />);
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });
});

/**
 * SESSUX1d, D126 §3.1: a row's ⋯ holds the session's acts where its row is, as the one rule (`acts.ts`) offers them and
 * the one owner (`sessionActs.ts`) carries them out. The row draws what it is handed, in that order, and reports the act
 * with its session; which acts apply is the rule's, held by `acts.test.ts`. SESSUX1e's archive is two of them. An
 * archived row says so on its line where no heading above it does: in a search, and by repository (§4.5).
 */
describe("a row's acts", () => {
  const placed = (over: Partial<SessionGrouping> & Pick<SessionGrouping, 'group' | 'shown'>): SessionGrouping => ({
    session: 's1a2b3c4', archived: false, teammate: false, ...over,
  });

  const items = async (props: Partial<Parameters<typeof SessionRow>[0]>) => {
    const view = render(<SessionRow session={session({ state: 'completed' })} onAct={() => {}} {...props} />);
    const user = userEvent.setup();
    // The row's door, then its menu's trigger, named in whichever language is on.
    screen.getAllByRole('button').at(-1)!.focus();
    await user.keyboard('{Enter}');
    const names = screen.getAllByRole('menuitem').map((item) => item.textContent);
    view.unmount();
    return names;
  };

  it('offers each act it is handed, named once, in order — and reports the act with its session', async () => {
    const act = vi.fn();
    render(
      <SessionRow
        session={session({ kind: 'chat' })}
        opening="Cap the hydration"
        acts={['answer', 'stop', 'retry', 'review', 'openFolder', 'terminal', 'detach', 'archive', 'unarchive', 'copy']}
        onAct={act}
      />,
    );

    const user = userEvent.setup();
    screen.getByRole('button', { name: 'more for Cap the hydration' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      'Answer…', 'Stop…', 'Try again', 'Review', 'Open folder', 'Open a terminal here', 'Open in its own window', 'Archive',
      'Unarchive', 'Copy session ID',
    ]);
    // Finish and Decline… stay the session's card's: one owner each (D56).
    expect(screen.queryByRole('menuitem', { name: /finish|decline/i })).toBeNull();

    await user.click(screen.getByRole('menuitem', { name: 'Stop…' }));
    expect(act).toHaveBeenCalledWith('stop', 's1a2b3c4');
  });

  /** CTX1 (D138, design §4): a right-click on the row offers its ⋯'s acts, the one list, and reports the act the same way. */
  it('offers the same acts on a right-click, named for the session', async () => {
    const act = vi.fn();
    render(
      <>
        <ContextMenus doors={{ copy: () => {} }} />
        <ul>
          <SessionRow session={session({ kind: 'chat', state: 'completed' })} opening="Cap the hydration" acts={['review', 'archive', 'copy']} onAct={act} />
        </ul>
      </>,
    );
    rightClick(screen.getByText('Cap the hydration'));
    expect(await menuActs('Actions for Cap the hydration')).toEqual(['Review', 'Archive', 'Copy session ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Archive' }));
    expect(act).toHaveBeenCalledWith('archive', 's1a2b3c4');
  });

  it('a row with nothing to report offers nothing to a right-click', () => {
    render(<><ContextMenus doors={{ copy: () => {} }} /><ul><SessionRow session={session({ kind: 'chat' })} opening="Cap" acts={['copy']} /></ul></>);
    rightClick(screen.getByText('Cap'));
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('offers no menu where it is handed no act, or nowhere to report one', () => {
    const { rerender } = render(<SessionRow session={session()} acts={[]} onAct={() => {}} />);
    expect(screen.getAllByRole('button')).toHaveLength(1);
    rerender(<SessionRow session={session()} acts={['copy']} />);
    expect(screen.getAllByRole('button')).toHaveLength(1);
  });

  it('archives and unarchives through the act it reports', async () => {
    const act = vi.fn();
    render(
      <SessionRow
        session={session({ state: 'completed' })} grouping={placed({ group: 'ended', shown: 'completed' })}
        acts={['archive', 'copy']} onAct={act}
      />,
    );

    const user = userEvent.setup();
    screen.getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Archive', 'Copy session ID']);
    await user.click(screen.getByRole('menuitem', { name: 'Archive' }));
    expect(act).toHaveBeenCalledWith('archive', 's1a2b3c4');
  });

  /** SESSUX1f (D126 §5.4): a conversation's ⋯ offers *Delete…*, named once, and reports it for the frame to ask. */
  it('offers Delete… through the act it reports', async () => {
    const act = vi.fn();
    render(
      <SessionRow
        session={session({ quest: null, kind: 'chat', state: 'completed' })}
        grouping={placed({ group: 'ended', shown: 'completed', deletable: true })}
        acts={['archive', 'delete', 'copy']} onAct={act}
      />,
    );

    const user = userEvent.setup();
    screen.getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Archive', 'Delete…', 'Copy session ID']);
    await user.click(screen.getByRole('menuitem', { name: 'Delete…' }));
    expect(act).toHaveBeenCalledWith('delete', 's1a2b3c4');
  });

  /** SESSUX1b's hold, said where the row is (D126 §2.2): a stop that holds its quest says so, with how it moves again. */
  it('says a stop holds its quest here until you try again, and why in its tip', () => {
    render(<SessionRow session={session({ state: 'stopped' })} grouping={placed({ group: 'ended', shown: 'stopped', holdsQuest: true })} />);
    const line = screen.getByText(/held here until you try again/);
    expect(line.getAttribute('title')).toMatch(/Try again/);
  });

  /** PAUSE1e (D132 §6.1): a session whose quest a pause holds says whose pause, before a stop's hold, and why in its tip. */
  it('says whose pause holds its quest, before a stop’s hold, and offers Resume in its ⋯', async () => {
    const paused = placed({ group: 'review', shown: 'stopped', holdsQuest: true, pausedBy: { scope: 'ask', id: 'a1b2c3' } });
    const act = vi.fn();
    render(<SessionRow session={session({ state: 'stopped' })} grouping={paused} acts={['resumeAsk', 'copy']} onAct={act} />);
    const line = screen.getByText(/paused with ask #a1b2c3/);
    expect(line.getAttribute('title')).toMatch(/Resume carries it on/);
    expect(screen.queryByText(/held here until you try again/)).toBeNull();

    const user = userEvent.setup();
    screen.getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Resume ask' }));
    expect(act).toHaveBeenCalledWith('resumeAsk', 's1a2b3c4');
  });

  it('says it is archived on its line where it is told to, and why in its tip', () => {
    const { rerender } = render(<SessionRow session={session({ state: 'completed' })} place="engine" archived />);
    const line = screen.getByText(/^engine · archived · driven/);
    expect(line.getAttribute('title')).toMatch(/Show archived/);

    rerender(<SessionRow session={session({ state: 'completed' })} place="engine" />);
    expect(screen.queryByText(/archived/)).toBeNull();
  });

  it('names its acts in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      expect(await items({ acts: ['archive', 'copy'] })).toEqual(['归档', '复制会话 ID']);
      expect(await items({ acts: ['unarchive', 'copy'] })).toEqual(['取消归档', '复制会话 ID']);
      expect(await items({ acts: ['answer', 'stop', 'retry', 'openFolder', 'terminal'] }))
        .toEqual(['回答…', '停止…', '重试', '打开文件夹', '在此打开终端']);
      expect(await items({ acts: ['pauseQuest', 'pauseAsk', 'resumeQuest', 'resumeAsk'] }))
        .toEqual(['暂缓委托…', '暂缓需求…', '恢复委托', '恢复需求']);
    } finally {
      await i18n.changeLanguage('en');
    }
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
