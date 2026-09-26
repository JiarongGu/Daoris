import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
    expect(screen.getByText('reached awaiting person')).toBeInTheDocument();
  });

  it('carries the driver\'s observation verbatim beside it', () => {
    render(<TimelineEntry
      event={{ kind: 'state', at: '2026-09-21T11:00:00Z', state: 'failed', note: 'the process exited 1' }}
    />);
    expect(screen.getByText('the process exited 1')).toBeInTheDocument();
  });

  it('names a quest transition by its status word', () => {
    render(<TimelineEntry event={{ kind: 'quest', at: '2026-09-21T10:30:00Z', status: 'Done' }} />);
    expect(screen.getByText('quest → Done')).toBeInTheDocument();
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
    expect(entries[1]).toHaveTextContent('quest → Done');
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
