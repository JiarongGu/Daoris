import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { clockOf } from '../format';
import i18n from '../i18n';
import { code } from '../test/code';
import { type Attention, type AttentionActs, AttentionRow } from './AttentionRow';

const PARKED: Attention = {
  id: 's1a2b3c4',
  kind: 'parked',
  title: 'Expose a streaming budget on the chunk API',
  where: 'engine',
  since: '2026-09-21T09:00:00Z',
  detail: 'Two ways forward; I recommend the second.',
};

/** An ask the declarations proposed and nobody has settled (INT4d) — it waits in a circle. */
const PROPOSAL: Attention = {
  id: '7c1e9a04b2d5',
  kind: 'proposal',
  title: 'The chunk streamer stalls on a cold cache.',
  where: 'aurora',
  since: '2026-09-21T09:00:00Z',
  detail: 'The declarations propose engine, game. Nothing is published until you choose.',
  publishTo: ['engine', 'game'],
  choices: ['engine', 'game', 'tools'],
};

const PARKED_QUEST: Attention = {
  id: '7a82cc', kind: 'parked-quest', title: 'Read the media field names from config', where: 'engine',
  since: '2026-09-21T09:00:00Z', detail: '3 session(s) have failed on `#7a82cc`.',
};

const GO_AHEAD: Attention = {
  id: '7c1e9a04b2d5#2', kind: 'go-ahead', ask: '7c1e9a04b2d5', number: 2, title: 'push on prod: “the menu entries”',
  where: 'ask #7c1e9a04b2d5', since: '2026-09-21T09:00:00Z', detail: 'The menu ships with the release.',
};

const TRUST: Attention = {
  id: 'C:/somewhere/.claude.json\nC:/somewhere/engine', kind: 'trust', title: 'C:/somewhere/engine', where: 'engine',
  since: '2026-09-21T09:00:00Z', detail: 'The agent ignores this folder\'s own permissions.allow until you trust it there.',
  trust: { folder: 'C:/somewhere/engine', trustFile: 'C:/somewhere/.claude.json' }, quest: '7a82cc',
};

const DEPARTURE: Attention = {
  id: '7a82cd', kind: 'departure', title: 'Cap hydration per frame', where: 'engine', since: '2026-09-21T09:00:00Z',
  detail: 'Requirement 2 departed: the scheduler holds the budget instead',
};

const RULE: Attention = {
  id: 'p0000002', kind: 'rule', title: 'allow WebFetch for every session on this machine', where: 'session i9n8t7k6',
  since: '2026-09-21T09:00:00Z', detail: 'The docs it needs are on the web.',
};

const REVIEW: Attention = {
  id: 'r3v13w00', kind: 'review', title: 'Verify the drill-down against DEV', where: 'engine',
  since: '2026-09-21T09:00:00Z', detail: '3 commits to review',
};

/** The intake for two asks, held behind `home`'s cool-off, having passed two accounts read signed out (UX6d, design §6.3). */
const ACCOUNT_WAIT: Attention = {
  id: 'wait:claude-code/account-2', kind: 'account-wait', title: 'Intake for 2 asks', where: 'work', circle: true,
  since: '2026-09-21T11:35:00Z', detail: 'home cools until Oct 6, 04:42; account-1 and account-3 read signed out at 10:42.',
  account: {
    agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, outside: null,
    named: [
      { id: 'account-2', label: 'home', state: 'cooling', read: null, until: '2026-10-06T04:42:00Z' },
      { id: 'account-1', label: 'account-1', state: 'out', read: '2026-09-21T10:42:00Z', until: null },
      { id: 'account-3', label: 'account-3', state: 'out', read: '2026-09-21T10:42:00Z', until: null },
    ],
  },
};

/** The same wait with nothing signed out, and a ready account outside `work`'s list (D130 §3.3). */
const LET_IN: Attention = {
  ...ACCOUNT_WAIT,
  detail: 'home cools until Oct 6, 04:42.',
  account: {
    ...ACCOUNT_WAIT.account!,
    named: [ACCOUNT_WAIT.account!.named[0]!, { id: 'account-5', label: 'spare', state: 'unknown', read: null, until: null }],
    outside: { id: 'account-4', label: 'account-4', list: 'work', workspace: 'work' },
  },
};

/** The same wait, where the one account outside `work`'s list no read answered: it is read before it is let in (UXFIX3). */
const READ_FIRST: Attention = {
  ...LET_IN,
  account: { ...LET_IN.account!, named: [ACCOUNT_WAIT.account!.named[0]!], outside: null, readFirst: { id: 'account-4', label: 'spare' } },
};

/**
 * A signed-out account a list holds, which no waiting start names: when it was read is known, since when it holds work is
 * not (UXFIX3).
 */
const SIGNED_OUT: Attention = {
  id: 'signed-out:claude-code/account-1', kind: 'signed-out', title: 'account-1', where: 'Claude Code',
  since: null, read: '2026-09-21T10:42:00Z', detail: 'It runs work in aurora.',
  account: {
    agent: 'claude-code', product: 'Claude Code', harness: 'claude-code', signsIn: true, outside: null,
    named: [{ id: 'account-1', label: 'account-1', state: 'out', read: '2026-09-21T10:42:00Z', until: null }],
  },
};

/** Every act a row can be handed, each a spy. */
/** What an ask under the row hands its act to say how it ended (UXFIX2b3a). */
const answeredByAct = { done: expect.any(Function), refused: expect.any(Function) };

const everyAct = (): Required<AttentionActs> => ({
  publish: vi.fn(), retry: vi.fn(), answer: vi.fn(), trust: vi.fn(),
  acceptDeparture: vi.fn(), acceptRule: vi.fn(), declineRule: vi.fn(),
  signIn: vi.fn(), read: vi.fn(), letRun: vi.fn(), reviewed: vi.fn(), notYet: vi.fn(), showAgain: vi.fn(),
  opinionAgain: vi.fn(), opinionAnyway: vi.fn(),
});

describe('a row in what needs you', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-21T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('says what it is, where, which kind of waiting, and for how long', () => {
    render(<AttentionRow item={PARKED} />);

    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('parked at a checkpoint')).toBeInTheDocument();
    // 🔴 UX5 U40: how long, as a span. It said *waiting 3h ago*, a point pasted into a duration,
    // which 中文 made plainly wrong: *已等待 3 小时前*, "has waited three hours ago".
    expect(screen.getByText('waiting 3h 0m')).toBeInTheDocument();
    expect(screen.getByText(/I recommend the second/)).toBeInTheDocument();
  });

  it('says how long in 中文 as a span, never as a point', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<AttentionRow item={PARKED} />);
      expect(screen.getByText('已等待 3 小时 0 分')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** The row is named by its title, so a list of them is read, and found, by what each is. */
  it('is a list item named by its title', () => {
    render(<ul><AttentionRow item={PARKED} /></ul>);
    expect(screen.getByRole('listitem', { name: 'Expose a streaming budget on the chunk API' })).toBeInTheDocument();
  });

  it('names the other kind by what is actually wrong with it', () => {
    render(<AttentionRow
      item={{ ...PARKED, kind: 'unanswerable', detail: '`retired` is not on this register.' }}
    />);

    expect(screen.getByText('nobody here can take this')).toBeInTheDocument();
  });

  /** A bare workspace name among repository names reads as one more repository. */
  it('names an ask by what it waits for, and its place as a workspace', () => {
    render(<AttentionRow item={PROPOSAL} />);

    expect(screen.getByText('proposed, not yet published')).toBeInTheDocument();
    expect(screen.getByText('workspace aurora')).toBeInTheDocument();
    expect(screen.getByText(/propose engine, game/)).toBeInTheDocument();
  });

  it('names an ask whose intake parked asking by that', () => {
    render(<AttentionRow item={{ ...PROPOSAL, kind: 'intake', detail: 'published nothing.' }} />);
    expect(screen.getByText('its intake asked you')).toBeInTheDocument();
  });

  /** UX6c: the three kinds the band gained, each by what it waits on. */
  it('names a go-ahead, a departure and work to review by what each waits on', () => {
    const { unmount } = render(<AttentionRow item={GO_AHEAD} />);
    expect(screen.getByText('needs your go-ahead')).toBeInTheDocument();
    expect(screen.getByText('ask #7c1e9a04b2d5')).toBeInTheDocument();
    unmount();
    const second = render(<AttentionRow item={DEPARTURE} />);
    expect(screen.getByText('awaits your yes')).toBeInTheDocument();
    expect(screen.getByText(/Requirement 2 departed/)).toBeInTheDocument();
    second.unmount();
    render(<AttentionRow item={REVIEW} />);
    expect(screen.getByText('to review')).toBeInTheDocument();
    expect(screen.getByText('3 commits to review')).toBeInTheDocument();
  });

  it('renders without a detail — a park that said nothing is still worth a row', () => {
    render(<AttentionRow item={{ ...PARKED, detail: null }} onOpen={() => {}} />);
    expect(screen.getByRole('button', { name: 'Answer…' })).toBeInTheDocument();
  });

  /**
   * LANG1b (D142 points 1, 4, 5): a parked session's note in the reader's language, as one run the row clamps, the agent's
   * question inline; a record from before parts as it was kept, marked.
   */
  it('words a parked session’s note in the reader’s language, and shows an old one as recorded', async () => {
    await i18n.changeLanguage('zh');
    try {
      const { container, unmount } = render(<AttentionRow
        item={{
          ...PARKED,
          detail: undefined,
          note: {
            note: 'It stopped with its quest still taken, to ask you: Which branch?',
            parts: [
              { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
              { words: 'Which branch?', by: 'agent' },
            ],
          },
        }}
        onOpen={vi.fn()}
      />);
      const clamped = container.querySelector('.line-clamp-2')!;
      expect(clamped.textContent).toBe('它停了下来，委托仍已接下，想问你：Which branch?');
      expect([...clamped.classList].filter((name) => /^(block|inline|flex|grid|inline-block)$/.test(name))).toEqual([]);
      unmount();

      render(<AttentionRow item={{ ...PARKED, detail: undefined, note: { note: 'two ways forward.' } }} />);
      expect(screen.getByText('按原文显示')).toBeInTheDocument();
      expect(screen.getByText('two ways forward.')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

/** No pinned clock: nothing below reads a duration, and userEvent's own waits are real. */
describe('opening one', () => {
  it('is a door, and hands back the whole item so the caller knows where to go', async () => {
    const open = vi.fn();
    render(<AttentionRow item={PARKED} onOpen={open} />);

    await userEvent.click(screen.getByRole('button', { name: 'Answer…' }));
    expect(open).toHaveBeenCalledWith(PARKED);
  });

  /**
   * 🔴 On the first real parked session the row said the session waited and not where to reply. A door names where it
   * goes, and since UX6c it is a press of its own beside the row's acts (design §6.3): answering a park, an intake's
   * question or a review needs reading, so each of those is its door alone; every other row opens its record.
   */
  it('says where its door goes, by its kind', () => {
    const doorOf = (item: Attention) => {
      const { unmount } = render(<AttentionRow item={item} onOpen={vi.fn()} />);
      const names = screen.getAllByRole('button').map((button) => button.textContent);
      unmount();
      return names;
    };
    expect(doorOf(PARKED)).toEqual(['Answer…']);
    expect(doorOf({ ...PROPOSAL, kind: 'intake' })).toEqual(['Answer ask #7c1e9a04b2d5']);
    expect(doorOf(REVIEW)).toEqual(['Review']);
    expect(doorOf({ ...PARKED, kind: 'unanswerable' })).toEqual(['Open']);
  });

  /**
   * 🔴 The same look: a parked session's whole analysis, a screen of it, filled the band, because a
   * `block` beside `line-clamp-2` overrode the display the clamp needs. jsdom lays nothing out, so this
   * holds the class the clamp depends on, and the window holds the look.
   */
  it('keeps a long analysis to two lines, with nothing overriding the clamp', () => {
    const { container } = render(<AttentionRow item={{ ...PARKED, detail: 'a long analysis '.repeat(80) }} onOpen={vi.fn()} />);

    const clamped = container.querySelector('.line-clamp-2')!;
    expect(clamped).not.toBeNull();
    expect([...clamped.classList].filter((name) => /^(block|inline|flex|grid|inline-block)$/.test(name))).toEqual([]);
  });

  /**
   * A door opens something, or it is not a door (platform language §4): a browser's parked row was a
   * button that did nothing. Knowing is the half that travels, so the row is still there — as text.
   */
  it('is no door where there is nowhere to go, and still says what is waiting', () => {
    render(<AttentionRow item={PARKED} />);

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
  });
});

/**
 * UX6c (design §6.3): an act sits on the row only where one press is safe and the row says what it does. What widens
 * what Daoris may do, and a choice, asks once under the row first; the rest act on the press.
 */
describe('settling one where it stands', () => {
  it('publishes an ask to what its declarations proposed, in one press', async () => {
    const acts = everyAct();
    render(<AttentionRow item={PROPOSAL} acts={acts} onOpen={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Publish to engine, game' }));
    expect(acts.publish).toHaveBeenCalledWith(PROPOSAL, ['engine', 'game']);
  });

  it('chooses where an ask goes under the row, and publishes only on the second press', async () => {
    const acts = everyAct();
    render(<AttentionRow item={{ ...PROPOSAL, publishTo: [] }} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Choose where it goes…' }));
    const asking = screen.getByRole('group', { name: 'Choose where it goes…' });
    expect(within(asking).getByText('Publish it to a repository workspace aurora can ask.')).toBeInTheDocument();
    const publish = within(asking).getByRole('button', { name: 'Publish' });
    expect(publish).toBeDisabled();
    await userEvent.click(within(asking).getByRole('combobox', { name: 'Choose a repository' }));
    await userEvent.click(await screen.findByRole('option', { name: 'tools' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'Choose where it goes…' })).getByRole('button', { name: 'Publish' }));

    expect(acts.publish).toHaveBeenCalledWith({ ...PROPOSAL, publishTo: [] }, ['tools'], answeredByAct);
  });

  it('puts a question down with never mind, having done nothing', async () => {
    const acts = everyAct();
    render(<AttentionRow item={PROPOSAL} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Choose…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group')).toBeNull();
    expect(acts.publish).not.toHaveBeenCalled();
  });

  it('tries a parked quest again in one press, and says that starts a session', async () => {
    const acts = everyAct();
    render(<AttentionRow item={PARKED_QUEST} acts={acts} onOpen={vi.fn()} />);

    expect(screen.getByText('starts a session in engine')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(acts.retry).toHaveBeenCalledWith(PARKED_QUEST);
  });

  /** A go-ahead's answer reaches every session on its ask, so each press says so first, and the yes carries their words. */
  it('approves a go-ahead after saying what every session on the ask is then handed, with the person’s words', async () => {
    const acts = everyAct();
    render(<AttentionRow item={GO_AHEAD} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Approve…' }));
    const asking = screen.getByRole('group', { name: 'Approve…' });
    expect(within(asking).getByText(/Every session on ask #7c1e9a04b2d5 is handed your yes/)).toBeInTheDocument();
    await userEvent.type(within(asking).getByRole('textbox', { name: 'your words, if any' }), 'dev first');
    await userEvent.click(within(asking).getByRole('button', { name: 'Approve' }));

    expect(acts.answer).toHaveBeenCalledWith(GO_AHEAD, true, 'dev first', answeredByAct);
    // It stays open until its act says it landed.
    expect(screen.getByRole('group', { name: 'Approve…' })).toBeInTheDocument();
    act(() => (vi.mocked(acts.answer).mock.calls[0]![3]).done());
    expect(screen.queryByRole('group')).toBeNull();
  });

  it('refuses a go-ahead the same way, with no words', async () => {
    const acts = everyAct();
    render(<AttentionRow item={GO_AHEAD} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Refuse…' }));
    expect(screen.getByText(/is handed your no/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(acts.answer).toHaveBeenCalledWith(GO_AHEAD, false, undefined, answeredByAct);
  });

  /** The agent's own trust question, asked in the open under the row, and granted only on its press (D73). */
  it('asks the agent’s trust question under the row before it trusts the folder', async () => {
    const acts = everyAct();
    render(<AttentionRow item={TRUST} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder…' }));
    expect(screen.getByText('Trust this folder for the agent?')).toBeInTheDocument();
    expect(screen.getByText('The driver is holding quest #7a82cc for it.')).toBeInTheDocument();
    expect(acts.trust).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    expect(acts.trust).toHaveBeenCalledWith(TRUST, answeredByAct);
  });

  /** UXFIX2b3b: the question stays while the grant is written; it closes once it landed and says a refusal itself. */
  it('keeps the trust question open for a refusal, and closes it once the grant lands', async () => {
    const acts = everyAct();
    render(<AttentionRow item={TRUST} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    const answered = vi.mocked(acts.trust!).mock.calls[0]![1]!;
    act(() => answered.refused('The agent file is locked.'));
    expect(screen.getByRole('alert')).toHaveTextContent('The agent file is locked.');
    expect(screen.getByText('Trust this folder for the agent?')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Trust this folder' }));
    act(() => vi.mocked(acts.trust!).mock.calls[1]![1]!.done());
    expect(screen.queryByText('Trust this folder for the agent?')).toBeNull();
  });

  it('accepts a departure in one press', async () => {
    const acts = everyAct();
    render(<AttentionRow item={DEPARTURE} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Accept the departure' }));
    expect(acts.acceptDeparture).toHaveBeenCalledWith(DEPARTURE);
  });

  /** 🔴 A widening never applies without the person (D74): its accept asks once, naming the change; its decline does not. */
  it('accepts a widening only after saying what it widens, and declines one in a press', async () => {
    const acts = everyAct();
    render(<AttentionRow item={RULE} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Decline' }));
    expect(acts.declineRule).toHaveBeenCalledWith(RULE);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    expect(screen.getByText(/It widens what agents may do: allow WebFetch for every session on this machine/)).toBeInTheDocument();
    expect(acts.acceptRule).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Accept' }));
    expect(acts.acceptRule).toHaveBeenCalledWith(RULE, answeredByAct);
  });

  /** A press already on its way is not offered twice. */
  it('holds every act while one is on its way', () => {
    render(<AttentionRow item={PROPOSAL} acts={everyAct()} onOpen={vi.fn()} busy />);
    expect(screen.getByRole('button', { name: 'Publish to engine, game' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Choose…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Open' })).toBeEnabled();
  });

  /** A browser has no driver and no rules: an act it is not handed is not offered, and the door still is. */
  it('offers no act it was not handed', () => {
    render(<AttentionRow item={PARKED_QUEST} onOpen={vi.fn()} />);
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Open']);
    expect(screen.queryByText('starts a session in engine')).toBeNull();
  });

  /** Design §9.3's budget: a row holds three controls at most, folded, whatever its kind. */
  it('holds three controls at most, door included', () => {
    for (const item of [PARKED, PROPOSAL, PARKED_QUEST, GO_AHEAD, TRUST, DEPARTURE, RULE, REVIEW, ACCOUNT_WAIT, LET_IN, SIGNED_OUT]) {
      const { unmount } = render(<AttentionRow item={item} acts={everyAct()} onOpen={vi.fn()} />);
      expect(screen.getAllByRole('button').length, item.kind).toBeLessThanOrEqual(3);
      unmount();
    }
  });
});

/**
 * UX6d (design §6.2–§6.3): a start waiting for accounts says each account that would serve it as last known, signs in the
 * ones read signed out in a press each, and lets a ready account in only after saying what that lets Daoris spend. Its door
 * is the agent's page, named by the agent.
 */
describe('an account’s row', () => {
  it('says what waits for an account, in which workspace, and why, as the accounts were last read', () => {
    render(<AttentionRow item={ACCOUNT_WAIT} />);

    expect(screen.getByText('waits for an account')).toBeInTheDocument();
    expect(screen.getByText('Intake for 2 asks')).toBeInTheDocument();
    expect(screen.getByText('workspace work')).toBeInTheDocument();
    expect(screen.getByText(/account-1 and account-3 read signed out at 10:42/)).toBeInTheDocument();
  });

  it('signs in each account read signed out in a press, and its door opens the agent', async () => {
    const acts = everyAct();
    const open = vi.fn();
    render(<AttentionRow item={ACCOUNT_WAIT} acts={acts} onOpen={open} />);

    expect(screen.getAllByRole('button').map((button) => button.textContent))
      .toEqual(['Sign in to account-1', 'Sign in to account-3', 'Claude Code']);
    await userEvent.click(screen.getByRole('button', { name: 'Sign in to account-3' }));
    expect(acts.signIn).toHaveBeenCalledWith(ACCOUNT_WAIT, 'account-3');
    await userEvent.click(screen.getByRole('button', { name: 'Claude Code' }));
    expect(open).toHaveBeenCalledWith(ACCOUNT_WAIT);
  });

  /** D130 §3.3: letting an account run a workspace widens what Daoris may spend, so it asks once, naming its terminal twin. */
  it('lets a ready account run the workspace only after saying what that lets Daoris spend', async () => {
    const acts = everyAct();
    render(<AttentionRow item={LET_IN} acts={acts} onOpen={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Let account-4 run work…' }));
    const asking = screen.getByRole('group', { name: 'Let account-4 run work…' });
    expect(within(asking).getByText(/account-4 joins work's list, so Daoris may start work's work on it/)).toBeInTheDocument();
    expect(within(asking).getByText(code('daoris agent profile join claude-code account-4 work'))).toBeInTheDocument();
    expect(acts.letRun).not.toHaveBeenCalled();
    await userEvent.click(within(asking).getByRole('button', { name: "Add to work's list" }));
    expect(acts.letRun).toHaveBeenCalledWith(LET_IN, 'account-4', answeredByAct);
  });

  it('says a join to this machine’s list is one, for every workspace that runs on it', async () => {
    const item: Attention = { ...LET_IN, account: { ...LET_IN.account!, outside: { ...LET_IN.account!.outside!, list: null } } };
    render(<AttentionRow item={item} acts={everyAct()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Let account-4 run work…' }));
    const asking = screen.getByRole('group', { name: 'Let account-4 run work…' });
    expect(within(asking).getByText(/joins this machine's list, which work runs on with every workspace/)).toBeInTheDocument();
    expect(within(asking).getByText(code('daoris agent profile join claude-code account-4 --machine'))).toBeInTheDocument();
    expect(within(asking).getByRole('button', { name: "Add to this machine's list" })).toBeInTheDocument();
  });

  /** §6.3: an account no read answered gets *Read* for that one account, the reading the person asks for. */
  it('reads an account no read answered, on the press', async () => {
    const acts = everyAct();
    render(<AttentionRow item={LET_IN} acts={acts} />);
    await userEvent.click(screen.getByRole('button', { name: 'Read spare' }));
    expect(acts.read).toHaveBeenCalledWith(LET_IN, 'account-5');
  });

  /** UXFIX3: an account no read answered may be signed out, so letting it run waits for a reading that says it is ready. */
  it('reads an unread account outside the list before it offers to let it run', async () => {
    const acts = everyAct();
    render(<AttentionRow item={READ_FIRST} acts={acts} onOpen={vi.fn()} />);

    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Read spare', 'Claude Code']);
    expect(screen.queryByRole('button', { name: /^Let / })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Read spare' }));
    expect(acts.read).toHaveBeenCalledWith(READ_FIRST, 'account-4');
    expect(acts.letRun).not.toHaveBeenCalled();
  });

  /**
   * UXFIX3: a signed-out account's row says when it was read where other rows say how long they waited. Its reading is not
   * a wait, and as one, reading it again made an old blocker look new.
   */
  it('says when a signed-out account was read, never that it waits', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-21T12:00:00Z'));
    try {
      const { unmount } = render(<AttentionRow item={SIGNED_OUT} />);
      expect(screen.getByText(`read ${clockOf('2026-09-21T10:42:00Z', new Date())}`)).toBeInTheDocument();
      expect(screen.queryByText(/waiting/)).not.toBeInTheDocument();
      expect(screen.getByText('It runs work in aurora.')).toBeInTheDocument();
      unmount();

      await i18n.changeLanguage('zh');
      render(<AttentionRow item={SIGNED_OUT} />);
      expect(screen.getByText(`${clockOf('2026-09-21T10:42:00Z', new Date(), 'zh')} 读取`)).toBeInTheDocument();
      expect(screen.queryByText(/已等待/)).not.toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
      vi.useRealTimers();
    }
  });

  it('says no time at all where the reading is undated', () => {
    render(<AttentionRow item={{ ...SIGNED_OUT, read: null }} />);
    expect(screen.queryByText(/^read |waiting/)).not.toBeInTheDocument();
  });

  it('signs a signed-out account in from its own row, named by the account', async () => {
    const acts = everyAct();
    render(<AttentionRow item={SIGNED_OUT} acts={acts} onOpen={vi.fn()} />);

    expect(screen.getByText('signed out')).toBeInTheDocument();
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Sign in', 'Claude Code']);
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(acts.signIn).toHaveBeenCalledWith(SIGNED_OUT, 'account-1');
  });

  /** A sign-in's steps happen where it was started (platform language §4): the band hands its panel, under the row. */
  it('holds what its band hands it under the row', () => {
    render(<AttentionRow item={SIGNED_OUT} acts={everyAct()} below={<p>the sign-in’s steps</p>} />);
    expect(within(screen.getByRole('listitem')).getByText('the sign-in’s steps')).toBeInTheDocument();
  });
});

const SET_UP: Attention = {
  id: 'q2', kind: 'set-up', title: 'Show #q1 in `local` for review', where: 'reports', since: '2026-09-21T11:35:00Z',
  detail: 'Shown in `local`.', setUp: { machine: 'desk', sequence: 7 }, local: true, session: 's9',
};

const OPINION: Attention = {
  id: 's42', kind: 'opinion', title: 'Fix the page bound in the catalog', where: 'storefront', since: '2026-09-21T11:35:00Z',
  detail: 'A finding is disputed.',
  opinion: {
    session: 's42', quest: 'q7', repository: 'storefront', since: '2026-09-21T11:35:00Z', auto: true,
    opinion: { state: 'disputed', holds: true, opinion: 'o1', reviewer: 'codex-acp', product: 'Codex', maker: 'OpenAI', disputes: 1 },
  },
};

/**
 * UXFIX2b3a: each ask of the row is the shared inline confirmation: it takes the focus on opening, stays open and says it is
 * waiting while its act runs, says a refusal inside it, and closes on success with the focus back on the press that opened it.
 */
describe('an ask under the row answers inside itself', () => {
  type Case = {
    name: string; item: Attention; open: string; move: string; handler: keyof AttentionActs;
    prepare?: (ask: HTMLElement) => Promise<void>;
  };
  const cases: Case[] = [
    { name: 'approve', item: GO_AHEAD, open: 'Approve…', move: 'Approve', handler: 'answer' },
    { name: 'refuse', item: GO_AHEAD, open: 'Refuse…', move: 'Refuse', handler: 'answer' },
    {
      name: 'choose', item: { ...PROPOSAL, publishTo: [] }, open: 'Choose where it goes…', move: 'Publish', handler: 'publish',
      prepare: async (ask) => {
        await userEvent.click(within(ask).getByRole('combobox', { name: 'Choose a repository' }));
        await userEvent.click(await screen.findByRole('option', { name: 'tools' }));
      },
    },
    { name: 'accept-rule', item: RULE, open: 'Accept…', move: 'Accept', handler: 'acceptRule' },
    { name: 'let-run', item: LET_IN, open: 'Let account-4 run work…', move: "Add to work's list", handler: 'letRun' },
    {
      name: 'not-yet', item: SET_UP, open: 'Not yet…', move: 'Send not yet', handler: 'notYet',
      prepare: async (ask) => { await userEvent.type(within(ask).getByRole('textbox'), 'the chart is empty'); },
    },
    { name: 'opinion-anyway', item: OPINION, open: 'Go on anyway…', move: 'Go on anyway', handler: 'opinionAnyway' },
  ];

  for (const one of cases) {
    it(`${one.name}: takes the focus, waits for its act, says a refusal inside, and gives the focus back once it lands`, async () => {
      const acts = everyAct();
      const handler = acts[one.handler] as ReturnType<typeof vi.fn>;
      render(<AttentionRow item={one.item} acts={acts} />);

      await userEvent.click(screen.getByRole('button', { name: one.open }));
      const ask = screen.getByRole('group', { name: one.open });
      // The explanation takes the focus on opening.
      expect(ask.contains(document.activeElement)).toBe(true);
      await one.prepare?.(ask);
      await userEvent.click(within(screen.getByRole('group', { name: one.open })).getByRole('button', { name: one.move }));

      // It stays open and says it waits while the act runs; the act hears how to answer.
      expect(handler).toHaveBeenCalledTimes(1);
      const answered = handler.mock.calls[0]!.at(-1) as { done: () => void; refused: (sentence: string) => void };
      expect(typeof answered.done).toBe('function');
      expect(screen.getByRole('group', { name: one.open })).toBeInTheDocument();
      expect(screen.getByRole('status')).toHaveTextContent(/\S/);

      // A refusal is said in the ask, and the move may be pressed again.
      act(() => answered.refused('The host said no.'));
      expect(within(screen.getByRole('group', { name: one.open })).getByRole('alert')).toHaveTextContent('The host said no.');
      expect(within(screen.getByRole('group', { name: one.open })).getByRole('button', { name: one.move })).toBeEnabled();

      await userEvent.click(within(screen.getByRole('group', { name: one.open })).getByRole('button', { name: one.move }));
      expect(handler).toHaveBeenCalledTimes(2);
      const again = handler.mock.calls[1]!.at(-1) as { done: () => void };
      act(() => again.done());

      expect(screen.queryByRole('group')).toBeNull();
      expect(screen.getByRole('button', { name: one.open })).toHaveFocus();
    });
  }
});
