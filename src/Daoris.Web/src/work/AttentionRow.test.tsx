import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
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

/** Every act a row can be handed, each a spy. */
const everyAct = (): Required<AttentionActs> => ({
  publish: vi.fn(), retry: vi.fn(), answer: vi.fn(), trust: vi.fn(),
  acceptDeparture: vi.fn(), acceptRule: vi.fn(), declineRule: vi.fn(),
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

    expect(acts.publish).toHaveBeenCalledWith({ ...PROPOSAL, publishTo: [] }, ['tools']);
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

    expect(acts.answer).toHaveBeenCalledWith(GO_AHEAD, true, 'dev first');
    expect(screen.queryByRole('group')).toBeNull();
  });

  it('refuses a go-ahead the same way, with no words', async () => {
    const acts = everyAct();
    render(<AttentionRow item={GO_AHEAD} acts={acts} />);

    await userEvent.click(screen.getByRole('button', { name: 'Refuse…' }));
    expect(screen.getByText(/is handed your no/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(acts.answer).toHaveBeenCalledWith(GO_AHEAD, false, undefined);
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
    expect(acts.trust).toHaveBeenCalledWith(TRUST);
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
    expect(acts.acceptRule).toHaveBeenCalledWith(RULE);
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
    for (const item of [PARKED, PROPOSAL, PARKED_QUEST, GO_AHEAD, TRUST, DEPARTURE, RULE, REVIEW]) {
      const { unmount } = render(<AttentionRow item={item} acts={everyAct()} onOpen={vi.fn()} />);
      expect(screen.getAllByRole('button').length, item.kind).toBeLessThanOrEqual(3);
      unmount();
    }
  });
});
