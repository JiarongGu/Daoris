import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Quest, Session } from '../../data/api';
import { aQuest } from '../../quests/aQuest';
import type { SweepBranch } from '../../settings/Sweep';
import type { Answered } from '../InlineConfirm';
import type { SessionProcess } from './process';
import { SessionHead } from './SessionHead';

const Head = SessionHead;
const STATE = (word: string, rest = '') => ({
  line: { kind: 'working' as const, since: null, wait: null, cause: null, more: 0, disagrees: null }, said: { word, rest },
});
import { said } from '../../test/said';

// Props-only, like every molecule here: a parked session with its analysis, a record read over a
// remote with no tree and no account, and an ended one are all reached by passing them.

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  door: 'claude-code',
  state: 'working',
  kind: 'driven',
  doorVersion: '2.1.4',
  profile: 'owner',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

/** An intake parked asking the person: a chat in the ask's name, in Daoris's own room. */
const INTAKE: Partial<Session> = {
  id: 'i9n8t7k6',
  quest: null,
  kind: 'chat',
  repository: 'ask #0fda18',
  ask: '0fda18',
  state: 'awaiting-person',
  tree: 'C:/somewhere/data/intake/default',
};

const quest = (over: Partial<Quest> = {}): Quest => aQuest({
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

/** Open the head's folded *Details*, where its reference facts are. */
const details = () => fireEvent.click(screen.getByRole('button', { name: /^Details|^详情/ }));

/** Every element between one and an ancestor of it, the ancestor included. */
function ancestorsUpTo(element: Element, last: Element): Element[] {
  const found: Element[] = [];
  for (let at = element.parentElement; at; at = at.parentElement) {
    found.push(at);
    if (at === last) break;
  }
  return found;
}

describe('the attended session\'s head', () => {
  it('shows the actor and the reported model in Details without replacing an absent acknowledgement', () => {
    render(<SessionHead session={session({ actor: 'build', choice: 'before',
      asked: { agent: 'claude-code', model: { source: 'account-default', carried: false }, effort: { source: 'account-default', carried: false } },
      ran: { models: ['reported-model'], more: 0, entries: [], entriesMore: 0 },
    })} />);
    details();
    expect(screen.getByText('Build')).toBeInTheDocument();
    expect(screen.getByText('as before')).toBeInTheDocument();
    expect(screen.getByText('reported-model')).toBeInTheDocument();
    expect(screen.getAllByText('not reported')).toHaveLength(2);
    expect(screen.getByText('the selection policy was not recorded')).toBeInTheDocument();
  });
  it('says in Details how the choice was reached, beside the chip’s reason', () => {
    render(<SessionHead session={session({ actor: 'build', choice: '2', chosen: { rule: 'order', by: 'moved', condition: 'limit', from: '1' } })} />);
    expect(screen.getByText('choice 2 · after a limit')).toBeInTheDocument();
    details();
    expect(screen.getByText('Moved here from choice 1: every account of that choice reached its limit.')).toBeInTheDocument();
  });
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  /** What its own tree left — the branch, and whether its work is on a branch of the person's. */
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
   * A tree the agent moved onto a branch of its own is named by the branch the driver's
   * `work` carries, once the clean-up lists none (it lists `daoris/` branches, and the tidy removed that one); the clean-up's row,
   * where it has one, still says whether the work landed.
   */
  it("names a moved tree's branch where the clean-up has no row", async () => {
    const { rerender } = render(<SessionHead session={session({ state: 'completed' })} ownBranch="feature/x-fix" />);
    expect(screen.getByText('Its work')).toBeInTheDocument();
    expect(screen.getByText('feature/x-fix')).toBeInTheDocument();

    rerender(<SessionHead session={session({ state: 'completed' })} ownBranch="feature/x-fix" branch={{
      repository: 'engine', workspace: 'default', branch: 'feature/x-fix', hasTree: false,
      kind: 'landed', commits: 3, where: 'main', removable: true,
    }} />);
    expect(screen.getByText('landed on main')).toBeInTheDocument();
    expect(screen.getAllByText('feature/x-fix')).toHaveLength(1);

    await i18n.changeLanguage('zh');
    rerender(<SessionHead session={session({ state: 'completed' })} ownBranch="feature/x-fix" />);
    expect(screen.getByText('它的工作')).toBeInTheDocument();
    expect(screen.getByText('feature/x-fix')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });

  /**
   * The branch its tree left is not cut to one line with nothing to read the rest by. It is shown whole as a branch
   * row's name is, wrapping after its separators on the line's own row, which wraps already.
   */
  it('shows the branch its tree left whole, wrapping after its separators, never cut to a line', () => {
    const LONG = 'daoris/s-5a6b7c8d-a-chain-step-that-carries-a-long-slug-past-the-head';
    render(<SessionHead session={session({ state: 'failed' })} branch={{
      repository: 'engine', workspace: 'default', branch: LONG, hasTree: false, kind: 'unlanded', commits: 2, removable: false,
    }} />);

    const name = screen.getByText(LONG);
    expect(name).toHaveAttribute('data-copy', LONG);
    expect(name.querySelector('wbr')).not.toBeNull();
    const line = name.closest('p')!;
    const classes = [name, ...ancestorsUpTo(name, line)].flatMap((each) => [...each.classList]);
    expect(classes.filter((each) => /^(?:truncate|text-ellipsis|whitespace-nowrap|overflow-hidden|line-clamp-\d+)$/.test(each))).toEqual([]);
    expect(line).toHaveClass('flex-wrap');
  });

  /**
   * Its reference facts are a folded *Details*, its line naming the quest, the agent and the start; open,
   * each fact named, the account its own row, and the id with *Copy*.
   */
  it('is the record: identity, state, and what the session ran on and as, its reference folded', () => {
    render(<SessionHead session={session()} quest={quest()} state={STATE('Working', '4m')} />);

    expect(screen.getByRole('heading', { name: 'Expose a streaming budget on the chunk API' }))
      .toBeInTheDocument();
    expect(screen.getByText('Working')).toBeInTheDocument();
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

  /** *Details* names its account by the person's name where the frame hands the roster's namer. */
  it('names its account in Details by the person’s name, and by its id with no namer', () => {
    const nameOf = (owner: string, profile?: string | null) => (owner === 'claude-code' && profile === 'acct-3f9c1a2b' ? 'work' : profile ?? '');
    const { rerender } = render(<SessionHead session={session({ profile: 'acct-3f9c1a2b' })} nameOf={nameOf} />);
    details();
    expect(screen.getByText('work')).toBeInTheDocument();
    expect(screen.queryByText('acct-3f9c1a2b')).toBeNull();

    rerender(<SessionHead session={session({ profile: 'acct-3f9c1a2b' })} />);
    expect(screen.getByText('acct-3f9c1a2b')).toBeInTheDocument();
  });

  /**
   * A session that ran on the tool's own sign-in names no account in its record,
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
   * The state stays beside the title it names, however wide the head is (status leads).
   * The head follows the centre's width, so the pill never sits a thousand pixels from its title on a wide window.
   */
  /**
   * Under a page header that says the title, the record does not say it again; where the header shows the
   * quest's short title, the record says the whole title once, at body size. The id is in *Details* alone.
   */
  it('under a header says no title at all, not even the whole one of a short-titled quest (HEAD2c)', () => {
    const { rerender } = render(<SessionHead session={session()} quest={quest()} headed />);
    expect(screen.queryByRole('heading', { level: 2 })).toBeNull();
    expect(screen.getAllByText('Expose a streaming budget on the chunk API')).toHaveLength(1);
    expect(screen.queryByText('working')).toBeNull();

    rerender(<SessionHead session={session()} quest={quest({ short: 'Streaming budget' })} headed />);
    expect(screen.getAllByText('Expose a streaming budget on the chunk API')).toHaveLength(1);
    expect(screen.queryByText('Streaming budget')).toBeNull();
    expect(screen.queryByText('s1a2b3c4')).toBeNull();
  });

  it('holds the quest in Details as *Started from*, with its door', () => {
    const onOpenQuest = vi.fn();
    render(<SessionHead session={session()} quest={quest({ short: 'Streaming budget' })} headed onOpenQuest={onOpenQuest} />);
    details();
    const row = screen.getByText('Started from', { selector: 'dt' }).nextElementSibling as HTMLElement;
    fireEvent.click(within(row).getByRole('button', { name: 'Expose a streaming budget on the chunk API' }));
    expect(onOpenQuest).toHaveBeenCalledWith(expect.objectContaining({ id: '7a82cc' }));
  });

  it('names the quest with no door where nothing can open it, and no origin where a session has none to say', () => {
    const { rerender } = render(<SessionHead session={session()} quest={quest()} headed />);
    details();
    expect(screen.getAllByText('Expose a streaming budget on the chunk API').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Expose a streaming budget on the chunk API' })).toBeNull();

    rerender(<SessionHead session={session({ quest: null, kind: 'driven' })} headed />);
    expect(screen.queryByText('Started from')).toBeNull();
  });

  it('says where it started from in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<SessionHead session={session()} quest={quest()} headed onOpenQuest={vi.fn()} />);
      details();
      expect(screen.getByText('起点', { selector: 'dt' })).toBeInTheDocument();
      expect(screen.getAllByRole('button', { name: 'Expose a streaming budget on the chunk API' }).length).toBe(2);
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * *Details* says the process its own tree runs under, how its work is committed and how
   * it lands, each with where it is set, in Setup's words; a session told none (no tree of its own here) says neither.
   */
  describe('the process it runs under', () => {
    const PROCESS: SessionProcess = {
      commits: { repository: 'engine', workspace: 'aurora', rule: { mode: 'ask', convention: 'Say why.' }, source: 'workspace' },
    };

    it('says how its work is committed and where that was set, and no landing rule', () => {
      render(<SessionHead session={session()} process={PROCESS} />);
      details();

      const commits = screen.getByText('Commits').nextElementSibling!;
      expect(commits).toHaveTextContent('Each commit waits for your yes before it is made.');
      expect(commits).toHaveTextContent('“Say why.”');
      expect(commits).toHaveTextContent('From the workspace aurora.');
      expect(screen.queryByText('Landing')).toBeNull();
    });

    it('says the default where nothing is set', () => {
      render(
        <SessionHead
          session={session()}
          process={{
            commits: { repository: 'engine', workspace: 'default', rule: { mode: 'as-you-go' }, source: 'default' },
          }}
        />,
      );
      details();

      expect(screen.getByText('Commits').nextElementSibling).toHaveTextContent(
        'Each piece that passes its checks is committed as the work goes. Daoris\'s default: nothing is set.');
      expect(screen.queryByText('Landing')).toBeNull();
    });

    it('says neither where it was told no process', () => {
      render(<SessionHead session={session()} process={null} />);
      details();

      expect(screen.queryByText('Commits')).not.toBeInTheDocument();
      expect(screen.queryByText('Landing')).not.toBeInTheDocument();
    });

    it('says them in 中文', async () => {
      await i18n.changeLanguage('zh');
      try {
        render(<SessionHead session={session()} process={PROCESS} />);
        details();
        expect(screen.getByText('提交').nextElementSibling).toHaveTextContent('每次提交前都等你同意。');
        expect(screen.getByText('提交').nextElementSibling).toHaveTextContent('沿用工作区 aurora。');
        expect(screen.queryByText('落地')).toBeNull();
      } finally {
        await i18n.changeLanguage('en');
      }
    });
  });

  /**
   * *Details* names the session this one continues and the one a helper helps, by
   * title with a door to it; where the frame holds no such session, its id in mono with no door.
   */
  describe('the sessions it continues and helps', () => {
    const earlier = session({ id: 'a1b2c3d4', quest: null, kind: 'chat', door: 'codex' });
    const asker = session({ id: 'b2c3d4e5' });

    it('names the session it continues by its title, with a door to it', () => {
      const onOpenSession = vi.fn();
      render(
        <SessionHead
          session={session({ quest: null, kind: 'chat', from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris' })}
          continues={{ session: earlier, title: 'Why the hydrator drops frames' }}
          onOpenSession={onOpenSession}
          headed
        />,
      );
      details();
      const row = screen.getByText('Started from', { selector: 'dt' }).nextElementSibling as HTMLElement;
      expect(within(row).getByText('Earlier session')).toBeInTheDocument();
      fireEvent.click(within(row).getByRole('button', { name: 'Why the hydrator drops frames' }));
      expect(onOpenSession).toHaveBeenCalledWith(earlier);
      expect(screen.queryByText(/Works/)).toBeNull();
    });

    it('names the session a helper helps by its title, with a door to it', () => {
      const onOpenSession = vi.fn();
      render(
        <SessionHead
          session={session({ quest: null, kind: 'chat', serves: 'b2c3d4e5' })}
          helps={{ session: asker, title: 'Expose a streaming budget' }}
          onOpenSession={onOpenSession}
          headed
        />,
      );
      details();
      const row = screen.getByText(/Works/).nextElementSibling as HTMLElement;
      fireEvent.click(within(row).getByRole('button', { name: 'Open session' }));
      expect(onOpenSession).toHaveBeenCalledWith(asker);
    });

    it('says the id with no door where the session is not held, or nothing can open it', () => {
      const { rerender } = render(
        <SessionHead session={session({ quest: null, kind: 'chat', from: 'a1b2c3d4', fromWhy: 'press', fromBy: 'person' })} headed onOpenSession={vi.fn()} />,
      );
      details();
      const row = screen.getByText('Started from', { selector: 'dt' }).nextElementSibling as HTMLElement;
      expect(within(row).getByText('a1b2c3d4')).toBeInTheDocument();
      expect(within(row).queryByRole('button')).toBeNull();

      rerender(
        <SessionHead
          session={session({ quest: null, kind: 'chat', serves: 'b2c3d4e5' })}
          helps={{ session: asker, title: 'Expose a streaming budget' }}
          headed
        />,
      );
      expect(within(screen.getByText(/Works/).nextElementSibling as HTMLElement).getByText('Expose a streaming budget')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Open session' })).toBeNull();
    });

    it('draws neither row for a session that names no link', () => {
      render(<SessionHead session={session()} continues={{ session: earlier, title: 'x' }} headed onOpenSession={vi.fn()} />);
      details();
      expect(screen.queryByText('Earlier session')).toBeNull();
      expect(screen.queryByText(/Works/)).toBeNull();
    });

    it('says them in 中文', async () => {
      await i18n.changeLanguage('zh');
      try {
        render(
          <SessionHead
            session={session({ quest: null, kind: 'chat', from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris', serves: 'b2c3d4e5' })}
            continues={{ session: earlier, title: '为什么丢帧' }}
            onOpenSession={vi.fn()}
            headed
          />,
        );
        details();
        expect(screen.getAllByText('先前会话').length).toBeGreaterThan(0);
        expect(screen.getByText('为谁工作')).toBeInTheDocument();
        expect(screen.getAllByRole('button', { name: '为什么丢帧' }).length).toBeGreaterThan(0);
      } finally {
        await i18n.changeLanguage('en');
      }
    });
  });

  it('says the state line it is given beside the title, never the record\'s word', () => {
    const { rerender } = render(<SessionHead session={session({ kind: 'chat' })} state={STATE('Waiting on you', '38m · Accept')} />);
    expect(screen.getByText('Waiting on you')).toBeInTheDocument();
    expect(screen.getByText('38m · Accept')).toBeInTheDocument();

    rerender(<SessionHead session={session({ kind: 'chat' })} />);
    for (const word of ['Waiting on you', 'working', 'idle']) expect(screen.queryByText(word)).toBeNull();
  });

  it('keeps the state beside the title rather than at the far edge', () => {
    render(<SessionHead session={session()} quest={quest()} state={STATE('Working')} />);

    const row = screen.getByRole('heading', { level: 2 }).parentElement!;
    expect(row).toContainElement(screen.getByText('Working'));
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
   * The tree's whole machine path would be the head's longest line, where the branch already
   * names the tree. It stays the person's (the door guards the path, not this machine's own surface),
   * on hover of the repository, and is no line of its own.
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
   * `AwaitingPerson` means "only the person can clear this". Its analysis belongs at the
   * TOP of the head — above the record,
   * because it is the reason the person is looking.
   */
  it('puts a parked session\'s analysis above its record, verbatim', () => {
    const analysis = 'Two ways forward.\n1. Cap in the scheduler.\n2. Cap in the chunk API.\nI recommend the second: it keeps the budget where the API already is.';
    render(<SessionHead session={session({ state: 'awaiting-person', note: analysis, noteParts: [{ words: analysis, by: 'agent' }] })} />);

    const waiting = screen.getByText('This one is waiting on you');
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
    // Above the record: the analysis precedes its Details in document order.
    expect(waiting.compareDocumentPosition(screen.getByRole('button', { name: /^Details/ })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  /**
   * Answered, the park stays parked until the driver's next look, and the same session goes on then.
   * The head shows the answer and says so, offers none of the moves a park waiting on the person has, and its state
   * reads answered, wherever the frame could act.
   */
  it('shows a park the person answered as going on, with the answer and no moves', () => {
    render(
      <SessionHead
        session={session({ state: 'awaiting-person', note: 'Two ways forward.\n\nAnswered: The second.', said: said('The second.') })}
        onResolve={vi.fn()}
        onAnswerSession={vi.fn()}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Your answer' })).toBeInTheDocument();
    expect(screen.getByText('The second.')).toBeInTheDocument();
    expect(screen.getByText("The same session goes on with this answer at the driver's next look.")).toBeInTheDocument();
    expect(screen.queryByText('This one is waiting on you')).toBeNull();
    for (const name of ['Finish', 'Decline…', 'Answer and carry on…']) expect(screen.queryByRole('button', { name })).toBeNull();
  });

  /**
   * An ended session says how it stands in the record's own words —
   * why a failed one failed — and a long note shows three lines with the rest a press away. A running
   * session says nothing here: its conversation is the answer.
   */
  it('says why an ended session stands where it does, in the record\'s words, and nothing while it runs', async () => {
    const { unmount } = render(<SessionHead session={session({ state: 'failed', note: 'the process exited 1', noteParts: [{ words: 'the process exited 1', by: 'before' }] })} />);
    expect(screen.queryByText('This one is waiting on you')).not.toBeInTheDocument();
    expect(screen.getByText('the process exited 1')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    unmount();

    const long = `the agent's turn failed with the quest still taken: ${'the ACP agent refused the call. '.repeat(12)}`;
    const second = render(<SessionHead session={session({ state: 'failed', note: long, noteParts: [{ words: long, by: 'before' }] })} />);
    expect(screen.getByText(long.trim(), { collapseWhitespace: false, exact: false }).closest('.line-clamp-3')).not.toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Show all' }));
    expect(screen.getByRole('button', { name: 'Show less' })).toBeInTheDocument();
    second.unmount();

    render(<SessionHead session={session({ state: 'working', note: 'reached working' })} />);
    expect(screen.queryByText('reached working')).not.toBeInTheDocument();
  });

  /**
   * A record ended `choice-not-offered` is said by its code's sentence, quiet, never the failure's red:
   * here naming the account it is refused on, the record's own; on a teammate's mirror, which carries no account, naming none. In zh too.
   */
  it('says a record ended choice-not-offered was not started, quietly, naming the account only here', async () => {
    const noteParts = [{ code: 'ended.choice-not-offered', values: { agent: 'claude-code-acp', model: 'opus' } }];
    const note = 'Not started: `claude-code-acp` did not offer `opus` on its account, so nothing ran; the next look chooses again by its actor\'s order.';
    const here = session({ state: 'failed', note, noteParts, profile: 'account-1' });
    const { container, unmount } = render(<SessionHead session={here} />);
    expect(screen.getByText('Not started: claude-code-acp did not offer opus on account-1.')).toBeInTheDocument();
    expect(container.innerHTML).not.toMatch(/danger/);
    unmount();

    const mirror = render(<SessionHead session={session({ id: 'jo/s9f8e7d6', state: 'failed', note, noteParts, profile: null })} />);
    expect(screen.getByText('Not started: the agent did not offer the model.')).toBeInTheDocument();
    mirror.unmount();

    await i18n.changeLanguage('zh');
    try {
      render(<SessionHead session={here} />);
      expect(screen.getByText('未启动：claude-code-acp 在 account-1 上不提供 opus。')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** What its tree left, with the move that acts on it when the work is the person's to take. */
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
    fireEvent.click(screen.getByRole('button', { name: 'Open outputs' }));
    expect(onReview).toHaveBeenCalledOnce();
    unmount();

    render(
      <SessionHead
        session={session({ state: 'completed' })}
        branch={{ repository: 'engine', branch: 'daoris/s-1', kind: 'landed', where: 'feature/x', commits: 0 } as SweepBranch}
        onReview={onReview}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
  });

  it('offers no door to the review where no commits wait', () => {
    render(
      <Head
        session={session({ state: 'failed' })}
        branch={{ repository: 'engine', branch: 'daoris/s-43c14a70', kind: 'empty', where: 'main', commits: 0 } as SweepBranch}
      />,
    );
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
    expect(screen.getByText('nothing beyond main')).toBeInTheDocument();
  });

  /**
   * A failed session's tree is gone and its branch stands, holding commits no branch of the
   * person's holds, so no clean-up takes it and the review has no tree to discard. Its head offers the discard beside
   * what it left, asking once, naming the branch and its commits; while the tree is here, the review's Discard serves.
   */
  it('offers to discard the branch a failed session left once its tree is gone, asking once', () => {
    const gone: SweepBranch = {
      repository: 'engine', workspace: 'default', branch: 'daoris/s-43c14a70', hasTree: false,
      kind: 'unlanded', commits: 2, removable: false, discardable: true,
    };
    const onDiscardBranch = vi.fn();
    const { rerender } = render(
      <SessionHead session={session({ state: 'failed' })} branch={gone} onReview={vi.fn()} onDiscardBranch={onDiscardBranch} />,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Discard branch…' }));
    expect(onDiscardBranch).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'discard daoris/s-43c14a70' });
    expect(ask).toHaveTextContent('Discards daoris/s-43c14a70 in engine, and with it 2 commits no branch of yours holds. Nothing brings them back.');
    fireEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(onDiscardBranch).toHaveBeenCalledOnce();
    // Open, and waiting, until the discard answers.
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();
    act(() => (onDiscardBranch.mock.calls[0]![0] as Answered).done());
    expect(screen.queryByRole('group', { name: 'discard daoris/s-43c14a70' })).toBeNull();

    rerender(<SessionHead session={session({ state: 'failed' })} branch={{ ...gone, hasTree: true }} onDiscardBranch={onDiscardBranch} />);
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();
    // Commits git could not judge are not offered, nor is anything where nothing can press it.
    rerender(<SessionHead session={session({ state: 'failed' })} branch={{ ...gone, discardable: false }} onDiscardBranch={onDiscardBranch} />);
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();
    rerender(<SessionHead session={session({ state: 'failed' })} branch={gone} />);
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();
  });

  /**
   * The head's discard is the one inline confirmation. Its sentence
   * takes the focus and describes the move; a branch the driver kept is said inside it, in its words, never closed at
   * once and refused in a toast; *Never mind* gives the focus back to *Discard branch…*.
   */
  it('says a kept branch inside the head’s discard ask, and gives the focus back to Discard branch… when put down', async () => {
    const gone: SweepBranch = {
      repository: 'engine', workspace: 'default', branch: 'daoris/s-43c14a70', hasTree: false,
      kind: 'unlanded', commits: 2, removable: false, discardable: true,
    };
    let answered: Answered | undefined;
    vi.useRealTimers();
    const user = userEvent.setup();
    render(
      <SessionHead session={session({ state: 'failed' })} branch={gone} onDiscardBranch={(told) => { answered = told; }} />,
    );

    await user.click(screen.getByRole('button', { name: 'Discard branch…' }));
    const ask = screen.getByRole('group', { name: 'discard daoris/s-43c14a70' });
    const says = within(ask).getByText(/^Discards/);
    await waitFor(() => expect(says).toHaveFocus());
    expect(within(ask).getByRole('button', { name: 'Discard branch' })).toHaveAccessibleDescription(says.textContent!);

    await user.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    act(() => answered!.refused('daoris/s-43c14a70 holds commits the driver could not judge, so it was kept.'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('daoris/s-43c14a70 holds commits the driver could not judge, so it was kept.');

    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'discard daoris/s-43c14a70' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard branch…' })).toHaveFocus();
  });

  /** An ask belongs to the branch it is asked about, so it never carries to another session's. */
  it('never carries an ask to discard one session\'s branch to another\'s', () => {
    const gone = (name: string): SweepBranch => ({
      repository: 'engine', workspace: 'default', branch: name, hasTree: false,
      kind: 'unlanded', commits: 1, removable: false, discardable: true,
    });
    const { rerender } = render(
      <SessionHead session={session({ state: 'failed' })} branch={gone('daoris/s-1')} onDiscardBranch={vi.fn()} />,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Discard branch…' }));
    expect(screen.getByRole('group', { name: 'discard daoris/s-1' })).toBeInTheDocument();

    rerender(<SessionHead session={session({ id: 's9f8e7d6', state: 'failed' })} branch={gone('daoris/s-2')} onDiscardBranch={vi.fn()} />);
    expect(screen.queryByRole('group', { name: /^discard/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard branch…' })).toBeInTheDocument();
  });

  /**
   * An intake serves an ask and runs in Daoris's own room, which is not a repository's tree.
   * Its record says so rather than reading `repository: ask #…`.
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

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} quest={quest()} />);
    details();

    expect(screen.getByText('仓库')).toBeInTheDocument();
    expect(screen.getByText('详情')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});

/**
 * The head words Daoris's lines in the record's note in the reader's language, wherever it
 * shows the note — how an ended session stands, a park's card, the card with no moves, a parked intake's — keeps the
 * agent's words as written beneath their lead-in, and shows a record from before parts as kept, marked.
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

    render(<SessionHead session={session({
      state: 'failed',
      note: 'exit 2 with the quest still taken.',
      noteParts: [{ words: 'exit 2 with the quest still taken.', by: 'before' }],
    })} />);
    expect(screen.getByText('按原文显示')).toBeInTheDocument();
    expect(screen.getByText('exit 2 with the quest still taken.')).toBeInTheDocument();
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

