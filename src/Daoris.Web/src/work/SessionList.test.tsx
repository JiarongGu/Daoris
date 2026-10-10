import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import type { Session } from '../api';
import { offeredActs } from './acts';
import type { SessionGrouping } from './groups';
import { ArchiveEndedAsk, type SessionRowFacts, SessionList, SessionStrip } from './SessionList';

// Sessions' list (SESSUX1c, D126 §2.1, §4) as a molecule: by state, by repository, the strip and a long Ended group,
// each reached by passing it. No bridge and no query client: the rail above it holds them.

const NOW = new Date('2026-10-02T12:00:00Z');
const at = (minutesAgo: number) => new Date(NOW.getTime() - minutesAgo * 60_000).toISOString();

const session = (over: Partial<Session> & { id: string; state: Session['state'] }): Session => ({
  repository: 'engine', adapter: 'claude-code', kind: 'driven', created: at(90), updated: at(5), ...over,
});

const placed = (over: Partial<SessionGrouping> & Pick<SessionGrouping, 'session' | 'group'>): SessionGrouping => ({
  shown: 'working', archived: false, teammate: false, ...over,
});

const SESSIONS: Session[] = [
  session({ id: 'w0rk1ng0', state: 'working', repository: 'tools' }),
  session({ id: 'p4rk3d00', state: 'awaiting-person', kind: 'chat' }),
  session({ id: 'f41led00', state: 'failed', quest: 'q1' }),
  session({ id: 'st0pp3d0', state: 'stopped', tree: 'C:/somewhere/.daoris/trees/default/engine/st0pp3d0' }),
  session({ id: 'aw41t000', state: 'completed', quest: 'q2', repository: 'game' }),
  session({ id: 'd0ne0000', state: 'completed', updated: at(30) }),
  session({ id: 'd0ne0001', state: 'declined', updated: at(10) }),
];

/** The reader's answer, in its order (`SessionGroups.Read`). */
const GROUPINGS: SessionGrouping[] = [
  placed({ session: 'p4rk3d00', group: 'you', shown: 'awaiting-person' }),
  placed({ session: 'f41led00', group: 'you', shown: 'parked', strikes: 3 }),
  placed({ session: 'st0pp3d0', group: 'review', shown: 'stopped', work: { commits: 2, uncommitted: 0 } }),
  placed({ session: 'w0rk1ng0', group: 'working', shown: 'working' }),
  placed({ session: 'aw41t000', group: 'later', shown: 'awaiting-reply', awaits: 'q9q9q9', awaitsOf: 'engine' }),
  placed({ session: 'd0ne0001', group: 'ended', shown: 'declined' }),
  placed({ session: 'd0ne0000', group: 'ended', shown: 'completed' }),
];

const facts = (sessions: Session[]): SessionRowFacts[] => sessions.map((row) => ({ session: row }));

const headings = () => screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);

/** The session rows under a group's heading: its list's rows, never the press that shows the rest. */
const groupRows = (name: string) => {
  const section = screen.getByRole('heading', { level: 3, name }).closest('section')!;
  return [...section.querySelectorAll<HTMLElement>('[data-list-row]')];
};

function list(props: Partial<Parameters<typeof SessionList>[0]> = {}) {
  return render(
    <Tooltip.Provider>
      <SessionList arrangement="state" rows={facts(SESSIONS)} groupings={GROUPINGS} {...props} />
    </Tooltip.Provider>,
  );
}

describe("Sessions' list by state", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  /** D126 §2.1: five groups in the order the person acts on them, each heading counted, each in the reader's order. */
  it('lists the five groups in the order the person acts on them, each counted', () => {
    list();

    expect(headings()).toEqual(['Waiting on you (2)', 'To review (1)', 'Working (1)', 'Resumes later (1)', 'Ended (2)']);
    expect(groupRows('Waiting on you (2)').map((row) => within(row).getAllByText(/./)[0]!.textContent))
      .toEqual(['waiting on you', 'parked']);
    expect(within(groupRows('Ended (2)')[0]!).getByText('declined')).toBeInTheDocument();
  });

  /** §4.2: no group header names a repository, so each row's line does; the group headers carry no repository facts. */
  it("says each row's repository on its line, and no repository's facts on a group", () => {
    list();

    expect(within(groupRows('Working (1)')[0]!).getByText(/^tools · driven/)).toBeInTheDocument();
    expect(within(groupRows('Resumes later (1)')[0]!).getByText(/^game · waits on #q9q9q9, asked of engine/)).toBeInTheDocument();
    expect(within(groupRows('To review (1)')[0]!).getByText(/^engine · 2 commits to review/)).toBeInTheDocument();
    expect(screen.queryByText('drives here')).toBeNull();
  });

  it('leaves out a group with nothing in it', () => {
    list({ rows: facts([SESSIONS[0]!]), groupings: [GROUPINGS[3]!] });
    expect(headings()).toEqual(['Working (1)']);
  });

  /** Archived is out of the way unless shown (§4.1), and shown last, in the reader's order. */
  it('hides what is archived unless it is shown', () => {
    const marked = [...GROUPINGS.slice(0, 6), placed({ session: 'd0ne0000', group: 'archived', shown: 'completed', archived: true })];

    const { unmount } = list({ groupings: marked });
    expect(headings()).toEqual(['Waiting on you (2)', 'To review (1)', 'Working (1)', 'Resumes later (1)', 'Ended (1)']);
    unmount();

    list({ groupings: marked, archived: true });
    expect(headings().at(-1)).toBe('Archived (1)');
  });

  /** §4.4: Ended shows twelve, then a press that shows the rest; the attended session is never cut out of it. */
  it('shows twelve of a long Ended group, then the rest on a press', async () => {
    const ended = Array.from({ length: 15 }, (_, index) => session({ id: `e${String(index).padStart(7, '0')}`, state: 'completed', updated: at(index + 1) }));
    list({ rows: facts(ended), groupings: ended.map((row) => placed({ session: row.id, group: 'ended', shown: 'completed' })) });

    expect(groupRows('Ended (15)')).toHaveLength(12);
    await userEvent.click(screen.getByRole('button', { name: 'Show 3 more' }));
    expect(groupRows('Ended (15)')).toHaveLength(15);
    expect(screen.queryByRole('button', { name: /^Show \d+ more$/ })).toBeNull();
  });

  it('keeps the attended session in a long Ended group past its twelve', () => {
    const ended = Array.from({ length: 15 }, (_, index) => session({ id: `e${String(index).padStart(7, '0')}`, state: 'completed', updated: at(index + 1) }));
    list({ rows: facts(ended), groupings: ended.map((row) => placed({ session: row.id, group: 'ended', shown: 'completed' })), selected: 'e0000014' });

    expect(groupRows('Ended (15)')).toHaveLength(13);
    expect(screen.getByRole('button', { name: 'Show 2 more' })).toBeInTheDocument();
    expect(screen.getAllByRole('button').find((button) => button.getAttribute('aria-current') === 'true')).toBeDefined();
  });

  it('chooses a row', async () => {
    const choose = vi.fn();
    list({ onSelect: choose });

    await userEvent.click(within(groupRows('Working (1)')[0]!).getAllByRole('button')[0]!);
    expect(choose).toHaveBeenCalledWith('w0rk1ng0');
  });

  it('says nothing is here, and how sessions come to be, when the list holds none', () => {
    list({ rows: [], groupings: [] });
    expect(screen.getByText('Nothing is running')).toBeInTheDocument();
  });

  it('names its groups in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      list();
      expect(headings()).toEqual(['等你处理（2）', '待审阅（1）', '工作中（1）', '稍后继续（1）', '已结束（2）']);
      expect(screen.getByText('已挂起')).toBeInTheDocument();
      expect(screen.getByText('等回复')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

/**
 * SESSUX1e, D126 §4.1, §5.2: *Show archived* draws Archived last, its rows Unarchive-able, and says when nothing is
 * archived; an archived row under no Archived heading says it is archived on its line (by repository).
 */
describe("Sessions' list, archived", () => {
  const marked = [...GROUPINGS.slice(0, 6), placed({ session: 'd0ne0000', group: 'archived', shown: 'completed', archived: true })];

  it('says nothing is archived under Archived when it is shown and empty', () => {
    list({ archived: true });

    expect(headings().at(-1)).toBe('Archived (0)');
    const section = screen.getByRole('heading', { level: 3, name: 'Archived (0)' }).closest('section')!;
    expect(within(section).getByText('Nothing archived')).toBeInTheDocument();
    // Nothing a row's arrows would stop on.
    expect(section.querySelectorAll('[data-list-row]')).toHaveLength(0);
  });

  it('draws no empty Archived group while archived is hidden', () => {
    list();
    expect(screen.queryByText('Nothing archived')).toBeNull();
  });

  /** Under the Archived heading the heading says it; a row's line need not say it again. */
  /** Each row's acts as the one rule offers them for where the reader placed it (SESSUX1d, D126 §3.1), as the rail hands them. */
  const actsBy = (groupings: SessionGrouping[]) => (row: Session) =>
    offeredActs({ session: row, grouping: groupings.find((each) => each.session === row.id) }, 'row');

  it('lists the archived under Archived, their lines quiet about it, each offering Unarchive', async () => {
    const act = vi.fn();
    list({ archived: true, groupings: marked, actsFor: actsBy(marked), onAct: act });

    const [row] = groupRows('Archived (1)');
    expect(within(row!).queryByText(/· archived ·/)).toBeNull();
    const user = userEvent.setup();
    within(row!).getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Unarchive' }));
    expect(act).toHaveBeenCalledWith('unarchive', 'd0ne0000');
  });

  it('offers Archive on what ended, and on nothing that waits on you', async () => {
    const act = vi.fn();
    list({ actsFor: actsBy(GROUPINGS), onAct: act });
    const user = userEvent.setup();

    within(groupRows('Waiting on you (2)')[1]!).getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByRole('menuitem', { name: 'Archive' })).toBeNull();
    // A parked quest's last session offers Try again where its row is.
    expect(screen.getByRole('menuitem', { name: 'Try again' })).toBeInTheDocument();
    await user.keyboard('{Escape}');

    within(groupRows('Ended (2)')[0]!).getByRole('button', { name: /^more for / }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitem', { name: 'Archive' }));
    expect(act).toHaveBeenCalledWith('archive', 'd0ne0001');
  });

  /** By repository an archived row sits among the ended with no heading of its own, so its line says it. */
  it('says an archived row is archived on its line by repository', () => {
    list({ arrangement: 'repository', archived: true, groupings: marked });

    const ended = screen.getByRole('region', { name: 'Ended' });
    expect(within(ended).getByText(/^archived · driven · /)).toBeInTheDocument();
  });

  it('names Archived and its empty state in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      list({ archived: true });
      expect(headings().at(-1)).toBe('已归档（0）');
      expect(screen.getByText('没有已归档的会话')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

/**
 * SESSUX1e, D126 §5.3: *Archive what ended…* lists first, under the list's header, what it would take and what stays,
 * and archives on a second press exactly what the first listed, since a list is a fact about a moment.
 */
describe("Archive what ended's first press", () => {
  const ask = (props: Partial<Parameters<typeof ArchiveEndedAsk>[0]> = {}) => render(
    <ArchiveEndedAsk going={['d0ne0001', 'd0ne0000']} kept={{ you: 2, review: 1 }} onArchive={() => {}} onCancel={() => {}} {...props} />,
  );

  it('says what it would archive and what it keeps, and archives what it said on the second press', async () => {
    const archive = vi.fn();
    ask({ onArchive: archive });

    const group = screen.getByRole('group', { name: 'Archive what ended…' });
    expect(group).toHaveTextContent('Archives 2 sessions that ended. Kept in the list: 2 waiting on you, 1 to review.');
    await userEvent.click(within(group).getByRole('button', { name: 'Archive 2' }));
    expect(archive).toHaveBeenCalledWith(['d0ne0001', 'd0ne0000'], expect.objectContaining({ done: expect.any(Function) }));
  });

  /** UXFIX2b2b: nothing is destroyed, so the move wears the primary's hue; it waits, says a refusal inside, closes once it landed. */
  it('archives in the primary hue, says a refusal inside the ask, and closes it once the archive landed', async () => {
    const archive = vi.fn();
    const cancel = vi.fn();
    ask({ onArchive: archive, onCancel: cancel });

    const group = screen.getByRole('group', { name: 'Archive what ended…' });
    const move = within(group).getByRole('button', { name: 'Archive 2' });
    expect(move.className).toContain('bg-accent');
    await userEvent.click(move);
    expect(move).toBeDisabled();
    const answered = archive.mock.calls[0]![1] as { done: () => void; refused: (sentence: string) => void };
    act(() => answered.refused('The driver is not running.'));
    expect(within(group).getByRole('alert')).toHaveTextContent('The driver is not running.');
    expect(cancel).not.toHaveBeenCalled();

    act(() => answered.done());
    expect(cancel).toHaveBeenCalledOnce();
  });

  /** The reader answers on every tick; the second press sends what the first listed, never a later answer's list. */
  it('sends only what its first press listed, whatever the list says since', async () => {
    const archive = vi.fn();
    const { rerender } = ask({ onArchive: archive });

    rerender(<ArchiveEndedAsk going={['d0ne0001', 'd0ne0000', 'n3wly000']} kept={{ you: 0, review: 0 }} onArchive={archive} onCancel={() => {}} />);
    expect(screen.getByRole('group', { name: 'Archive what ended…' })).toHaveTextContent('Archives 2 sessions that ended.');
    await userEvent.click(screen.getByRole('button', { name: 'Archive 2' }));
    expect(archive).toHaveBeenCalledWith(['d0ne0001', 'd0ne0000'], expect.objectContaining({ done: expect.any(Function) }));
  });

  it('names only what it keeps, one session in the singular', () => {
    const { unmount } = ask({ going: ['d0ne0001'], kept: { you: 0, review: 3 } });
    expect(screen.getByText('Archives 1 session that ended. Kept in the list: 3 to review.')).toBeInTheDocument();
    unmount();

    ask({ kept: { you: 0, review: 0 } });
    expect(screen.getByText('Archives 2 sessions that ended.')).toBeInTheDocument();
  });

  /** Nothing to archive: it says so, and offers no press that would archive nothing. */
  it('says when nothing has ended to archive, and offers only to close', async () => {
    const cancel = vi.fn();
    ask({ going: [], kept: { you: 1, review: 0 }, onCancel: cancel });

    const group = screen.getByRole('group', { name: 'Archive what ended…' });
    expect(group).toHaveTextContent('Nothing that ended is left to archive. Kept in the list: 1 waiting on you.');
    expect(within(group).getAllByRole('button').map((button) => button.textContent)).toEqual(['Close']);
    await userEvent.click(within(group).getByRole('button', { name: 'Close' }));
    expect(cancel).toHaveBeenCalled();
  });

  it('never minds', async () => {
    const cancel = vi.fn();
    ask({ onCancel: cancel });
    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(cancel).toHaveBeenCalled();
  });

  it('holds its presses while the archive is on its way', () => {
    ask({ busy: true });
    expect(screen.getByRole('button', { name: 'Archive 2' })).toBeDisabled();
  });

  it('asks in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      ask();
      const group = screen.getByRole('group', { name: '归档已结束的会话…' });
      expect(group).toHaveTextContent('将归档 2 个已结束的会话。仍留在列表中：2 个等你处理，1 个待审阅。');
      expect(within(group).getByRole('button', { name: '归档 2 个' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

describe("Sessions' list by repository", () => {
  /** §4.3: today's arrangement, with the reader's words: a parked quest's last session sorts with waiting on you. */
  it("groups by repository with its facts, waiting on you and parked first, and the ended beneath", () => {
    list({
      arrangement: 'repository',
      repositoryFacts: (repository) => ({ drivable: repository === 'engine' }),
    });

    const ended = screen.getByRole('region', { name: 'Ended' });
    expect(headings().filter((heading) => !ended.contains(screen.getByText(heading!)))).toEqual(['engine', 'tools']);
    expect(screen.getByText('drives here')).toBeInTheDocument();
    const engine = within(screen.getByRole('heading', { level: 3, name: 'engine' }).closest('section')!).getAllByRole('listitem');
    expect(engine.map((row) => within(row).getAllByText(/./)[0]!.textContent)).toEqual(['waiting on you', 'parked']);
    // The ended beneath, newest first, with their words: awaiting a reply, to review, and the record's own.
    expect(within(ended).getAllByRole('listitem').map((row) => within(row).getAllByText(/./)[0]!.textContent))
      .toEqual(['stopped', 'awaiting reply', 'declined', 'completed']);
  });

  /** The monitor's list is the present tense only (UX5 U70): nothing ended, and no parked row with no tile to go to. */
  it('lists only what is running where it is live', () => {
    list({ arrangement: 'repository', groupings: undefined, live: true });
    expect(screen.queryByRole('region', { name: 'Ended' })).toBeNull();
    expect(screen.queryByText('parked')).toBeNull();
  });
});

describe("Sessions' strip", () => {
  /** §2.5: what waits on the person first, then what runs, each named in words, since a mark is never hue alone. */
  it('holds waiting on you, then working, each named with its word', () => {
    render(
      <Tooltip.Provider>
        <SessionStrip arrangement="state" rows={facts(SESSIONS)} groupings={GROUPINGS} label="Sessions" />
      </Tooltip.Provider>,
    );

    const strip = screen.getByRole('navigation', { name: 'Sessions' });
    expect(within(strip).getAllByRole('button').map((button) => button.getAttribute('aria-label'))).toEqual([
      'Chat · engine · waiting on you',
      '#q1 · engine · parked',
      'Session · tools · working',
    ]);
  });
});
