import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Quest, Session } from '../../data/api';
import { SessionTimeline, TimelineEntry } from './SessionTimeline';

const NOW = new Date('2026-09-21T12:00:00Z');

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T11:00:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs a per-frame cap.',
  status: 'Done',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T10:30:00Z',
  ...over,
});

describe('one timeline entry', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it('says what was observed and when', () => {
    render(<TimelineEntry event={{ kind: 'opened', at: '2026-09-21T09:00:00Z' }} />);

    expect(screen.getByText('session opened')).toBeInTheDocument();
    expect(screen.getByText('3h ago')).toBeInTheDocument();
  });

  it('names the state reached, in the catalog\'s words', () => {
    render(<TimelineEntry event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'awaiting-person' }} />);
    expect(screen.getByText('reached waiting on you')).toBeInTheDocument();
  });

  it('carries a record from before parts as it was kept, marked, beside it', () => {
    render(<TimelineEntry
      event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'failed', note: 'the process exited 1' }}
    />);
    expect(screen.getByText('the process exited 1')).toBeInTheDocument();
    expect(screen.getByText('shown as recorded')).toBeInTheDocument();
  });

  /** LANG1b (D142 points 1, 4): Daoris's lines in the record's note, worded in the reader's language. */
  it('words the record’s note in the reader’s language', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<TimelineEntry
        event={{
          kind: 'state', at: '2026-09-21T11:00:00Z', state: 'failed', note: 'Timed out after 30 minutes and was killed.',
          noteParts: [{ code: 'ended.timeout', values: { minutes: 30 }, text: 'Timed out after 30 minutes and was killed.' }],
        }}
      />);
      expect(screen.getByText('运行超过 30 分钟，已被终止。')).toBeInTheDocument();
      expect(screen.queryByText('按原文显示')).toBeNull();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('names a quest transition by its status word', () => {
    render(<TimelineEntry event={{ kind: 'quest', at: '2026-09-21T10:30:00Z', status: 'Done' }} />);
    expect(screen.getByText('quest → done')).toBeInTheDocument();
  });

  it('renders the evidence sentence verbatim and the commits as rows', () => {
    render(<TimelineEntry
      event={{
        kind: 'evidence',
        at: '2026-09-21T11:00:00Z',
        text: 'commits landed:',
        commits: [
          { sha: 'c0ffee1', subject: 'cap hydration per frame' },
          { sha: 'deadbee', subject: '让区块 API 暴露流式预算' },
        ],
      }}
    />);

    expect(screen.getByText('commits landed:')).toBeInTheDocument();
    expect(screen.getByText('c0ffee1')).toBeInTheDocument();
    expect(screen.getByText('让区块 API 暴露流式预算')).toBeInTheDocument();
  });

  /**
   * UX6b (design §2.5): the install's side bar showed an ended session's whole done note, a full verify report, running
   * past the window's foot. A note longer than four lines folds to them, with *Show all*; the record holds it whole.
   */
  it('folds a note longer than four lines to them, and shows it all on the press', () => {
    const report = ['verify passed:', 'typecheck clean', 'cli 1200 tests', 'web 3798 tests', 'doc-budgets reported', 'devkit gates green'].join('\n');
    render(<TimelineEntry event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'completed', note: report }} />);

    const more = screen.getByRole('button', { name: 'Show all' });
    expect(more).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText(/devkit gates green/).closest('.line-clamp-4')).not.toBeNull();

    fireEvent.click(more);
    expect(screen.getByRole('button', { name: 'Show less' })).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText(/devkit gates green/).closest('.line-clamp-4')).toBeNull();
  });

  it('leaves a note of four lines or fewer whole, with nothing to press', () => {
    render(<TimelineEntry event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'failed', note: 'the process exited 1' }} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('the process exited 1').closest('.line-clamp-4')).toBeNull();
  });

  it("folds a quest's long note, its closer's words, the same way", () => {
    const note = 'Done. '.repeat(60).trim();
    render(<TimelineEntry event={{ kind: 'quest', at: '2026-09-21T10:30:00Z', status: 'Done', note }} />);
    expect(screen.getByText(note)).toHaveClass('line-clamp-4');
    fireEvent.click(screen.getByRole('button', { name: 'Show all' }));
    expect(screen.getByText(note)).not.toHaveClass('line-clamp-4');
  });

  it('folds a long note in 中文 by how wide its words set, and says so in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      const note = '验证通过，每一道检查都已通过。'.repeat(12);
      render(<TimelineEntry event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'completed', note }} />);
      expect(screen.getByRole('button', { name: '展开全部' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('shows an evidence bundle that carried no commits as its sentence alone', () => {
    render(<TimelineEntry
      event={{ kind: 'evidence', at: '2026-09-21T11:00:00Z', text: 'no commits landed', commits: [] }}
    />);

    expect(screen.getByText('no commits landed')).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
});

/**
 * The entries are the ordered list's own children — a commit list nests inside one of them. The states' flow draws a list
 * above them (FLOWCHART1b), so the observed events are the ordered one.
 */
const eventsOf = () => document.querySelector('ol')!;
const entriesOf = () => [...eventsOf().children];

describe('the timeline', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  /**
   * UX5 U65: in the dock its tab names it, and the pane said *Timeline* again under the *Timeline*
   * tab. Untitled there; titled where it stands in the column with no tab above it.
   */
  it('carries its heading only where no tab names it', () => {
    const { rerender } = render(<SessionTimeline session={session()} />);
    expect(screen.getByRole('heading', { name: 'Timeline' })).toBeInTheDocument();

    rerender(<SessionTimeline session={session()} titled={false} />);
    expect(screen.queryByRole('heading', { name: 'Timeline' })).toBeNull();
  });

  it('is one entry for a session nothing has happened to yet, and says so honestly', () => {
    render(<SessionTimeline session={session({ state: 'queued', updated: '2026-09-21T09:00:00Z' })} />);

    expect(entriesOf()).toHaveLength(1);
    expect(screen.getByText('session opened')).toBeInTheDocument();
  });

  it('runs oldest first', () => {
    render(<SessionTimeline
      session={session({
        state: 'completed',
        evidence: 'commits landed:\nc0ffee1 cap hydration per frame',
      })}
      quest={quest()}
    />);

    const entries = entriesOf();
    expect(entries[0]).toHaveTextContent('session opened');
    expect(entries[1]).toHaveTextContent('quest → done');
    expect(entries[2]).toHaveTextContent('reached completed');
    expect(entries[3]).toHaveTextContent('what came out');
  });

  /**
   * Step-parsing the stream was rejected by name (D52) and re-proposing it means answering the
   * D23/D24 argument first. This is the assertion that would fail if someone started: a session
   * with a busy console and nothing observed has a one-entry timeline, not an invented one.
   */
  it('invents nothing a harness did not tell the record', () => {
    render(<SessionTimeline session={session({ state: 'queued', updated: '2026-09-21T09:00:00Z' })} />);

    expect(within(eventsOf()).queryByText(/step/i)).not.toBeInTheDocument();
    expect(entriesOf()).toHaveLength(1);
  });

  it('speaks the active catalog', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionTimeline session={session()} />);

    expect(screen.getByText('时间线')).toBeInTheDocument();
    expect(screen.getByText('会话开启')).toBeInTheDocument();
    await i18n.changeLanguage('en');
  });
});

describe('a quest event’s coded note (QUESTCLOSE1b2)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('words the person’s done in 中文, and shows a note without parts as kept', async () => {
    await i18n.changeLanguage('zh');
    const noteParts = [{ code: 'quest.person-done', values: {} }, { words: 'In the folder.', by: 'person' }];
    const { unmount } = render(
      <TimelineEntry event={{ kind: 'quest', at: '2026-09-21T10:30:00Z', status: 'Done', note: 'The person marked this done: In the folder.', noteParts }} />,
    );
    expect(screen.getByText('已由人标为完成。')).toBeInTheDocument();
    expect(screen.getByText('In the folder.')).toBeInTheDocument();
    unmount();

    render(<TimelineEntry event={{ kind: 'quest', at: '2026-09-21T10:30:00Z', status: 'Done', note: 'Plain English.' }} />);
    expect(screen.getByText('Plain English.')).toBeInTheDocument();
  });
});

describe('the states at the head of the timeline (FLOWCHART1b; the flowchart design §2.2)', () => {
  beforeEach(() => {
    localStorage.removeItem('daoris.timeline.states');
    localStorage.removeItem('daoris.flow.list');
  });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const states = () => screen.getByRole('list', { name: 'How session s1a2b3c4 moves' });
  const node = (name: RegExp) => within(states()).getByRole('listitem', { name });

  it('draws the states above the observed events, the one the record is in lit, and the events as they were', () => {
    render(<SessionTimeline session={session({ state: 'awaiting-person' })} quest={quest()} />);

    expect(states().compareDocumentPosition(eventsOf()) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(states()).getAllByRole('listitem').map((each) => each.getAttribute('data-flow-node')))
      .toEqual(['queued', 'starting', 'working', 'awaiting-person', 'ended']);
    expect(node(/^waiting on you/)).toHaveAttribute('aria-current', 'step');
    expect(node(/^waiting on you/)).toHaveAccessibleName('waiting on you here now');
    expect(node(/^working/)).not.toHaveAttribute('aria-current');
    // Opened, reached waiting on you, and the quest's move: the observed events as they were.
    expect(entriesOf()).toHaveLength(3);
  });

  it('lights the outcomes for an ended session with the outcome\'s word, and says which may go on and which never does', () => {
    render(<SessionTimeline session={session({ state: 'completed' })} />);

    const ended = node(/^ended/);
    expect(ended).toHaveAttribute('aria-current', 'step');
    expect(ended).toHaveAccessibleName('ended completed');
    // Its members are words, the reached one marked; the stop is said once, and a stand-down is said never to go on.
    expect(within(ended).getByText('completed', { selector: '[aria-current]' })).toBeInTheDocument();
    for (const word of ['declined', 'stood down', 'failed', 'stopped']) expect(within(ended).getByText(word)).toBeInTheDocument();
    expect(within(ended).getByText('stopped: by you, from any live state')).toBeInTheDocument();
    expect(within(ended).getByText('a stand-down never goes on')).toBeInTheDocument();
    expect(ended).toHaveAccessibleDescription(/when your words wait for it and its working tree is free; a stand-down never goes on/);
  });

  it('says the return as a foot line, and that the record keeps where it stands and not how it got there', () => {
    render(<SessionTimeline session={session({ state: 'working' })} />);

    expect(within(node(/^waiting on you/)).getByRole('button', { name: /back to working, when you answer it/ })).toBeInTheDocument();
    expect(screen.getByText(/not how it got there/)).toBeInTheDocument();
  });

  it('says the shown word the page reads over the record, and no time (trap 19)', () => {
    render(<SessionTimeline session={session({ state: 'failed' })} shown="parked" />);

    expect(node(/^ended/)).toHaveAccessibleName('ended parked');
    expect(within(states()).queryByText(/ago|since/)).toBeNull();
  });

  it('draws going on refused with its reason for a record that can never go on', () => {
    render(<SessionTimeline session={session({ state: 'stood-down' })} />);

    expect(node(/^ended/)).toHaveAccessibleDescription(/it cannot go on: a stand-down has nothing to go on with/);
  });

  it('folds to its line, remembered, and opens again', async () => {
    const { unmount } = render(<SessionTimeline session={session({ state: 'working' })} />);
    const fold = screen.getByRole('button', { name: /^States/ });
    expect(fold).toHaveAttribute('aria-expanded', 'true');
    expect(fold).toHaveTextContent('States · working');

    await userEvent.click(fold);
    expect(screen.queryByRole('list', { name: 'How session s1a2b3c4 moves' })).toBeNull();
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    unmount();

    // Remembered on this machine: the next Timeline opens folded, and opens on a press.
    render(<SessionTimeline session={session({ state: 'working' })} />);
    expect(screen.queryByRole('list', { name: 'How session s1a2b3c4 moves' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: /^States/ }));
    expect(states()).toBeInTheDocument();
  });

  it('says a state it does not know as recorded on its line and its node, never as a catalogue key (trap 10)', () => {
    render(<SessionTimeline session={session({ state: 'paused' as Session['state'] })} />);

    expect(screen.getByRole('button', { name: /^States/ })).toHaveTextContent('States · paused');
    expect(node(/^`paused`, shown as recorded/)).toHaveAttribute('aria-current', 'step');
    expect(within(states()).queryByText(/sessionState\./)).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionTimeline session={session({ state: 'working' })} />);

    expect(screen.getByRole('list', { name: '会话 s1a2b3c4 如何流转' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^状态/ })).toHaveTextContent('状态 · 工作中');
    expect(screen.getByRole('listitem', { name: '工作中 当前所在' })).toHaveAttribute('aria-current', 'step');
  });
});
