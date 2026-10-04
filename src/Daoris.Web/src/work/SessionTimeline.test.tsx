import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
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

/** The entries are the ordered list's own children — a commit list nests inside one of them. */
const entriesOf = () => [...screen.getAllByRole('list')[0].children];

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

    expect(screen.queryByText(/step/i)).not.toBeInTheDocument();
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
