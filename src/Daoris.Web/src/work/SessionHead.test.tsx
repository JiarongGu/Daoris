import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../i18n';
import type { GoAhead, Quest, Session } from '../api';
import type { SweepBranch } from '../settings/Sweep';
import { SessionHead } from './SessionHead';

// Props-only, like every molecule here: a parked session with its analysis, a record read over a
// remote with no tree and no account, and an ended one are all reached by passing them.

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  harnessVersion: '2.1.4',
  profile: 'owner',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

/** An intake parked asking the person (INT4b): a chat in the ask's name, in Daoris's own room. */
const INTAKE: Partial<Session> = {
  id: 'i9n8t7k6',
  quest: null,
  kind: 'chat',
  repository: 'ask #0fda18',
  ask: '0fda18',
  state: 'awaiting-person',
  tree: 'C:/somewhere/data/intake/default',
};

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Taken',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
});

/** Open the head's folded *Details* (UX7c, D152 §7), where its reference facts are. */
const details = () => fireEvent.click(screen.getByRole('button', { name: /^Details|^详情/ }));

describe('the attended session\'s head', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  /** REV3: a half-written decline reason carried into the next parked session, ready to decline it. */
  it('starts each parked session\'s decline empty — a reason written for one never carries to another', () => {
    const parked: Partial<Session> = { state: 'awaiting-person', note: 'needs a person' };
    const { rerender } = render(<SessionHead session={session(parked)} onResolve={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: 'Decline…' }));
    fireEvent.change(screen.getByLabelText(/the reason/), { target: { value: 'the chunk API is being replaced' } });

    rerender(<SessionHead session={session({ ...parked, id: 's9f8e7d6' })} onResolve={vi.fn()} />);
    expect(screen.queryByLabelText(/the reason/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Decline…' })).toBeInTheDocument();
  });

  /** SESS1 S10: what its own tree left — the branch, and whether its work is on a branch of the person's. */
  it('says what its own tree left: the branch, and whether its work landed', () => {
    const { rerender } = render(<SessionHead session={session({ state: 'completed' })} branch={{
      repository: 'engine', workspace: 'default', branch: 'daoris/s-a900f1ad', hasTree: true,
      kind: 'unlanded', commits: 3, removable: false,
    }} />);
    expect(screen.getByText('daoris/s-a900f1ad')).toBeInTheDocument();
    expect(screen.getByText('3 commits no branch of yours holds')).toBeInTheDocument();

    rerender(<SessionHead session={session({ state: 'completed' })} branch={{
      repository: 'engine', workspace: 'default', branch: 'daoris/s-a900f1ad', hasTree: true,
      kind: 'landed', commits: 3, where: 'feature/0fda18-fix', removable: true,
    }} />);
    expect(screen.getByText('landed on feature/0fda18-fix')).toBeInTheDocument();

    rerender(<SessionHead session={session({ state: 'completed' })} />);
    expect(screen.queryByText('Its work')).toBeNull();
  });

  /**
   * UX7c (D152 §7): its reference facts are a folded *Details*, its line naming the quest, the agent and the start; open,
   * each fact named, the account its own row, and the id with *Copy*.
   */
  it('is the record: identity, state, and what the session ran on and as, its reference folded', () => {
    render(<SessionHead session={session()} quest={quest()} />);

    expect(screen.getByRole('heading', { name: 'Expose a streaming budget on the chunk API' }))
      .toBeInTheDocument();
    expect(screen.getByText('working')).toBeInTheDocument();
    const fold = screen.getByRole('button', { name: /^Details/ });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(fold).toHaveTextContent('quest #7a82cc · claude-code · 2.1.4 · started 2h ago');
    expect(screen.queryByText('engine')).toBeNull();

    details();
    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('claude-code · 2.1.4')).toBeInTheDocument();
    expect(screen.getByText('owner')).toBeInTheDocument();
    expect(screen.getByText('#7a82cc')).toBeInTheDocument();
    expect(screen.getByText('s1a2b3c4')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copy' })).toBeInTheDocument();
  });

  /**
   * D125 §3.7 (TOOL4m's rest, built with UX6e): a session that ran on the tool's own sign-in names no account in its record,
   * so the head says *your own sign-in* where it shows an account, for an agent that has accounts at all.
   */
  it('says a session ran on your own sign-in where its record names no account', () => {
    const { rerender } = render(<SessionHead session={session({ profile: null })} ownSignIn />);
    details();
    expect(screen.getByText('on your own sign-in')).toBeInTheDocument();

    // An agent with no accounts to speak of says nothing of one.
    rerender(<SessionHead session={session({ profile: null })} />);
    expect(screen.queryByText('on your own sign-in')).toBeNull();
    expect(screen.queryByText('Account')).toBeNull();
  });

  /**
   * The state stays beside the title it names, however wide the head is (platform language §4:
   * status leads). The head follows the centre's width now (UX5 U16), and pushed to the far edge the
   * pill sat a thousand pixels from its title on a wide window, which is what U10 had capped around.
   */
  /**
   * UX7c (D152 §7): under a page header that says the title, the record does not say it again; where the header showed the
   * quest's short title (SESSUX1j), the record says the whole title once, at body size. The id is in *Details* alone.
   */
  it('under a header says the whole title once, only where the header showed a short title', () => {
    const { rerender } = render(<SessionHead session={session()} quest={quest()} headed />);
    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
    expect(screen.queryByText('Expose a streaming budget on the chunk API')).toBeNull();
    expect(screen.queryByText('working')).toBeNull();

    rerender(<SessionHead session={session()} quest={quest({ short: 'Streaming budget' })} headed />);
    const whole = screen.getByText('Expose a streaming budget on the chunk API');
    expect(whole.tagName).toBe('P');
    expect(whole.className).toContain('line-clamp-2');
    expect(screen.queryByText('Streaming budget')).toBeNull();
    expect(screen.queryByText('s1a2b3c4')).toBeNull();
  });

  /** UX5 U17: the head says a live chat between turns is idle, as the rail does. */
  it('says a live chat between turns is idle', () => {
    const { rerender } = render(<SessionHead session={session({ kind: 'chat' })} taking={false} />);
    expect(screen.getByText('idle')).toBeInTheDocument();

    rerender(<SessionHead session={session({ kind: 'chat' })} taking />);
    expect(screen.getByText('working')).toBeInTheDocument();
  });

  it('keeps the state beside the title rather than at the far edge', () => {
    render(<SessionHead session={session()} quest={quest()} />);

    const row = screen.getByRole('heading', { level: 2 }).parentElement!;
    expect(row).toContainElement(screen.getByText('working'));
    expect(row.className).not.toContain('justify-between');
  });

  it('measures a running session to now and a finished one to where it ended', () => {
    const { unmount } = render(<SessionHead session={session()} />);
    details();
    expect(screen.getByText('Running for')).toBeInTheDocument();
    expect(screen.getByText('2h 14m')).toBeInTheDocument();
    unmount();

    render(<SessionHead session={session({ state: 'completed' })} />);
    details();
    expect(screen.getByText('Ran for')).toBeInTheDocument();
  });

  /**
   * SESS2 H3: the tree's whole machine path was the head's longest line, where the branch already
   * names the tree. It stays the person's (D51 §9 guards the door, not this machine's own surface),
   * on hover of the repository, and is no longer a line of its own.
   */
  it('keeps the whole tree path on the repository\'s hover rather than as a line of its own', () => {
    const tree = 'C:/somewhere/.daoris/trees/default/engine/streaming-budget';
    render(<SessionHead session={session({ tree })} />);
    details();

    expect(screen.queryByText('tree')).not.toBeInTheDocument();
    expect(screen.queryByText(tree)).not.toBeInTheDocument();
    expect(screen.getByText('engine')).toHaveAttribute('title', tree);
  });

  /**
   * An absence means something real on this record and a dash would read as a bug in all of them:
   * no tree is the registered root, no profile is the harness's own configuration home, and a
   * browser over a keyed remote is told neither.
   */
  it('omits what it was not told rather than rendering a blank', () => {
    render(<SessionHead session={session({ tree: null, profile: null, quest: null })} />);
    details();

    expect(screen.queryByText('tree')).not.toBeInTheDocument();
    expect(screen.queryByText('Quest')).not.toBeInTheDocument();
    expect(screen.queryByText('Account')).not.toBeInTheDocument();
    expect(screen.getByText('claude-code · 2.1.4')).toBeInTheDocument();
  });

  it('names the machine when the record came from another one', () => {
    render(<SessionHead session={session({ id: 'person@machine-a/s1a2b3c4' })} />);
    details();

    expect(screen.getByText('Machine')).toBeInTheDocument();
    expect(screen.getByText('person@machine-a')).toBeInTheDocument();
  });

  /**
   * `AwaitingPerson` has meant "only the person can clear this" since D46 and has never been
   * rendered anywhere. Its analysis belongs at the TOP of the head (design §4) — above the record,
   * because it is the reason the person is looking.
   */
  it('puts a parked session\'s analysis above its record, verbatim', () => {
    const analysis = 'Two ways forward.\n1. Cap in the scheduler.\n2. Cap in the chunk API.\nI recommend the second: it keeps the budget where the API already is.';
    render(<SessionHead session={session({ state: 'awaiting-person', note: analysis })} />);

    const waiting = screen.getByText('This one is waiting on you');
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
    // Above the record: the analysis precedes its Details in document order.
    expect(waiting.compareDocumentPosition(screen.getByRole('button', { name: /^Details/ })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  /**
   * ANSWER1c (D131): answered, the park stays parked until the driver's next look, and the same session goes on then.
   * The head shows the answer and says so, offers none of the moves a park waiting on the person has, and its state
   * reads answered, wherever the frame could act.
   */
  it('shows a park the person answered as going on, with the answer and no moves', () => {
    render(
      <SessionHead
        session={session({ state: 'awaiting-person', note: 'Two ways forward.\n\nAnswered: The second.', answer: 'The second.' })}
        onResolve={vi.fn()}
        onAnswerSession={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    expect(screen.getByText('The second.')).toBeInTheDocument();
    expect(screen.getByText("The same session goes on with this answer at the driver's next look.")).toBeInTheDocument();
    expect(screen.queryByText('This one is waiting on you')).toBeNull();
    for (const name of ['Finish', 'Decline…', 'Answer and carry on…']) expect(screen.queryByRole('button', { name })).toBeNull();
    expect(screen.getByText('answered')).toBeInTheDocument();
  });

  /**
   * SESS2 H5, H6 (reversing the rule that it left the note to the timeline, which since FRAME6 is in
   * the side bar and starts closed): an ended session says how it stands in the record's own words —
   * why a failed one failed — and a long note shows three lines with the rest a press away. A running
   * session says nothing here: its conversation is the answer.
   */
  it('says why an ended session stands where it does, in the record\'s words, and nothing while it runs', async () => {
    const { unmount } = render(<SessionHead session={session({ state: 'failed', note: 'the process exited 1' })} />);
    expect(screen.queryByText('This one is waiting on you')).not.toBeInTheDocument();
    expect(screen.getByText('the process exited 1')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    unmount();

    const long = `the agent's turn failed with the quest still taken: ${'the ACP agent refused the call. '.repeat(12)}`;
    const second = render(<SessionHead session={session({ state: 'failed', note: long })} />);
    expect(screen.getByText(long.trim(), { collapseWhitespace: false, exact: false }).closest('.line-clamp-3')).not.toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Show all' }));
    expect(screen.getByRole('button', { name: 'Show less' })).toBeInTheDocument();
    second.unmount();

    render(<SessionHead session={session({ state: 'working', note: 'reached working' })} />);
    expect(screen.queryByText('reached working')).not.toBeInTheDocument();
  });

  /** SESS2 H4: what its tree left, with the move that acts on it when the work is the person's to take. */
  it('says what its tree left, and offers the review when no branch of the person\'s holds the work', async () => {
    const onReview = vi.fn();
    const { unmount } = render(
      <SessionHead
        session={session({ state: 'completed' })}
        branch={{ repository: 'engine', branch: 'daoris/s-43c14a70', kind: 'unlanded', commits: 2 } as SweepBranch}
        onReview={onReview}
      />,
    );
    expect(screen.getByText('daoris/s-43c14a70')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Review' }));
    expect(onReview).toHaveBeenCalledOnce();
    unmount();

    render(
      <SessionHead
        session={session({ state: 'completed' })}
        branch={{ repository: 'engine', branch: 'daoris/s-1', kind: 'landed', where: 'feature/x', commits: 0 } as SweepBranch}
        onReview={onReview}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Review' })).toBeNull();
  });

  /**
   * INT4g: an intake serves an ask and runs in Daoris's own room, which is not a repository's tree
   * (INT4b). Its record says so rather than reading `repository: ask #…`.
   */
  it('names an intake\'s ask and room rather than a repository and a tree', () => {
    render(<SessionHead session={session({ ...INTAKE, state: 'working' })} />);

    expect(screen.getByRole('heading', { name: 'Intake for ask #0fda18' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Details/ })).toHaveTextContent('ask #0fda18');
    details();
    expect(screen.getByText('Ask')).toBeInTheDocument();
    expect(screen.getByText('#0fda18')).toBeInTheDocument();
    expect(screen.queryByText('Repository')).toBeNull();
    expect(screen.queryByText('tree')).toBeNull();
  });

  /**
   * INT4g: a parked intake's answer is on its ask. Finish, decline and stop each ended the record
   * without answering the ask, which then fell back to a proposal — so the head leads to the ask.
   */
  it('gives a parked intake its ask as the answer, not a parked session\'s three moves', () => {
    const onAnswerAsk = vi.fn();
    render(
      <SessionHead
        session={session({ ...INTAKE, note: 'published nothing: it asks you rather than guess.' })}
        onResolve={() => {}}
        onAnswerAsk={onAnswerAsk}
      />,
    );

    expect(screen.getByText('published nothing: it asks you rather than guess.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Finish' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Decline…' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Answer ask #0fda18' }));
    expect(onAnswerAsk).toHaveBeenCalledWith('0fda18');
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} quest={quest()} />);
    details();

    expect(screen.getByText('仓库')).toBeInTheDocument();
    expect(screen.getByText('详情')).toBeInTheDocument();
    expect(screen.getByText('工作中')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});

/**
 * LANG1b (D142 points 1, 4, 5): the head words Daoris's lines in the record's note in the reader's language, wherever it
 * shows the note — how an ended session stands, a park's card, the card with no moves, a parked intake's — keeps the
 * agent's words as written beneath their lead-in, and shows a record from before parts as it was kept, marked.
 */
describe('the head’s note, in the reader’s language', () => {
  beforeEach(async () => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
    await i18n.changeLanguage('zh');
  });
  afterEach(async () => {
    vi.useRealTimers();
    await i18n.changeLanguage('en');
  });

  const PARKED: Partial<Session> = {
    state: 'awaiting-person',
    note: 'It stopped with its quest still taken, to ask you:\nWhich branch should the release land on?',
    noteParts: [
      { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
      { words: 'Which branch should the release land on?\n\n1. main\n2. release/1.0', by: 'agent' },
    ],
  };

  it('says how an ended session stands in the reader’s words, and an old record as it was kept', () => {
    const { unmount } = render(
      <SessionHead
        session={session({
          state: 'failed',
          note: 'exit 2 with the quest still taken.',
          noteParts: [{ code: 'ended.taken-exit', values: { exit: 2 }, text: 'exit 2 with the quest still taken.' }],
        })}
      />,
    );
    expect(screen.getByText('退出码 2，委托仍已接下。')).toBeInTheDocument();
    expect(screen.queryByText(/with the quest still taken/)).toBeNull();
    expect(screen.queryByText('按原文显示')).toBeNull();
    unmount();

    render(<SessionHead session={session({ state: 'failed', note: 'exit 2 with the quest still taken.' })} />);
    expect(screen.getByText('按原文显示')).toBeInTheDocument();
    expect(screen.getByText('exit 2 with the quest still taken.')).toBeInTheDocument();
  });

  it('words a park’s lead-in on its card and keeps the agent’s question beneath it as written', () => {
    render(<SessionHead session={session(PARKED)} onResolve={vi.fn()} onAnswerSession={vi.fn()} />);

    const card = screen.getByRole('heading', { name: '这个会话在等你' }).closest('section')!;
    expect(within(card).getByText('它停了下来，委托仍已接下，想问你：')).toBeInTheDocument();
    const question = within(card).getByText(/Which branch should the release land on\?/);
    expect(question.closest('blockquote')!.textContent).toBe('Which branch should the release land on?\n\n1. main\n2. release/1.0');
  });

  it('words the note on the card a browser is shown, with no moves', () => {
    render(<SessionHead session={session(PARKED)} />);
    expect(screen.getByText('它停了下来，委托仍已接下，想问你：')).toBeInTheDocument();
    expect(screen.getByText(/Which branch should the release land on\?/).closest('blockquote')).not.toBeNull();
  });

  it('words a parked intake’s line on its card', () => {
    render(
      <SessionHead
        session={session({
          ...INTAKE,
          note: 'published nothing: …',
          noteParts: [{ code: 'intake.asks', values: { ask: '0fda18' }, text: 'published nothing: …' }],
        })}
        onAnswerAsk={vi.fn()}
      />,
    );
    expect(screen.getByText(/它什么也没发布：声明无法确定需求 #0fda18/)).toBeInTheDocument();
  });
});

/** Two go-aheads the parked session asked, both still waiting on the person (KNOWUSE1a). */
const ASKED: GoAhead[] = [
  {
    number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T11:00:00Z', why: 'The tile reads its target from it.' }],
  },
  {
    number: 2, kind: 'release', on: 'production', act: 'comparison report', state: 'asked',
    asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: '2026-09-21T11:10:00Z', why: 'Ship it.' }],
  },
];

/**
 * KNOWUSE1a2 (D135 §2): a park that asked go-aheads shows them beneath its card, each with *Approve* and *Refuse*, and a
 * press says which go-ahead, yes or no, and the person's words, for the frame to answer it and the park together. With
 * nothing to act through they are shown with no door; a park that asked none, or a session not parked, shows nothing new.
 */
describe('a park\'s go-aheads', () => {
  const parked: Partial<Session> = { state: 'awaiting-person', note: 'I need the two go-aheads to finish.' };

  it('lists what the park asked beneath its card, each with Approve and Refuse, saying an answer here goes on', () => {
    render(<SessionHead session={session(parked)} onResolve={vi.fn()} goAheads={ASKED} onGoAhead={vi.fn()} />);

    const section = screen.getByRole('region', { name: 'Go-aheads it asked' });
    expect(section).toHaveTextContent('Answering one here answers this session too');
    const items = within(section).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('write on production');
    expect(items[0]).toHaveTextContent('“dashboard configuration”');
    expect(items[1]).toHaveTextContent('release on production');
    for (const item of items) {
      expect(within(item).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
      expect(within(item).getByRole('button', { name: 'Refuse' })).toBeInTheDocument();
    }
    // Beneath the card that asks, which stays: its finish and decline are still the person's.
    expect(screen.getByText('This one is waiting on you').compareDocumentPosition(section)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    expect(screen.getByRole('button', { name: 'Finish' })).toBeInTheDocument();
  });

  it('says which go-ahead, the yes or the no, and the person\'s words, where they gave any', () => {
    const onGoAhead = vi.fn();
    render(<SessionHead session={session(parked)} goAheads={ASKED} onGoAhead={onGoAhead} />);
    const [first, second] = within(screen.getByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem');

    fireEvent.change(within(second!).getByLabelText('your words, if any'), { target: { value: 'dev first, then production' } });
    fireEvent.click(within(second!).getByRole('button', { name: 'Approve' }));
    fireEvent.click(within(first!).getByRole('button', { name: 'Refuse' }));

    expect(onGoAhead.mock.calls).toEqual([[2, true, 'dev first, then production'], [1, false, undefined]]);
  });

  it('keeps them beneath an answered park, the one still waiting answerable until the session goes on', () => {
    const answered: GoAhead[] = [
      { ...ASKED[0]!, state: 'approved', answer: { approved: true, at: '2026-09-21T11:58:00Z' } },
      ASKED[1]!,
    ];
    render(
      <SessionHead session={session({ ...parked, answer: 'carry on.' })} goAheads={answered} onGoAhead={vi.fn()} />,
    );

    expect(screen.getByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    const [first, second] = within(screen.getByRole('region', { name: 'Go-aheads it asked' })).getAllByRole('listitem');
    expect(first).toHaveTextContent('approved');
    expect(within(first!).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(within(second!).getByRole('button', { name: 'Approve' })).toBeInTheDocument();
  });

  it('shows them with no door where nothing can answer them', () => {
    render(<SessionHead session={session(parked)} goAheads={ASKED} />);

    const section = screen.getByRole('region', { name: 'Go-aheads it asked' });
    expect(within(section).getAllByRole('listitem')).toHaveLength(2);
    expect(within(section).queryByRole('button')).toBeNull();
  });

  it('shows nothing new for a park that asked none, or a session no longer parked', () => {
    const { rerender } = render(<SessionHead session={session(parked)} goAheads={[]} onGoAhead={vi.fn()} />);
    expect(screen.queryByRole('region', { name: 'Go-aheads it asked' })).toBeNull();

    rerender(<SessionHead session={session({ state: 'working' })} goAheads={ASKED} onGoAhead={vi.fn()} />);
    expect(screen.queryByRole('region', { name: 'Go-aheads it asked' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Approve' })).toBeNull();
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session(parked)} goAheads={ASKED} onGoAhead={vi.fn()} />);

    expect(screen.getByRole('region', { name: '它请求的放行' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: '放行' })).toHaveLength(2);
    await i18n.changeLanguage('en');
  });
});
