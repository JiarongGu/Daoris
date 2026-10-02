import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import type { Session } from '../api';
import type { SessionGrouping } from './groups';
import { type SessionRowFacts, SessionList, SessionStrip } from './SessionList';

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
