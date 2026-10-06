import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { Quest, Session } from '../api';
import { ChainStrip } from './ChainStrip';
import { buildChain } from './chain';

const quest = (id: string, extra: Partial<Quest> = {}): Quest => ({
  id, from: 'game', to: 'engine', title: `quest ${id}`, body: '', status: 'Open',
  filed: '2026-09-23T00:00:00Z', updated: '2026-09-23T00:00:00Z', ...extra,
});

const run = (id: string, questId: string, extra: Partial<Session> = {}): Session => ({
  id, quest: questId, repository: 'engine', adapter: 'claude-code', state: 'completed',
  created: '2026-09-23T01:00:00Z', updated: '2026-09-23T01:00:00Z', ...extra,
});

const QUESTS = [
  quest('a', { from: 'ask #abc123', status: 'Done', title: 'Develop it' }),
  quest('b', { parent: 'a', to: 'game', title: 'Verify it', then: [{ to: 'engine', title: 'Report back', body: '' }] }),
];

describe('the chain strip', () => {
  it('reads the chain in order, every state in words', () => {
    render(<ChainStrip chain={buildChain('b', QUESTS, [])} />);

    const steps = within(screen.getByRole('region', { name: 'How this work ran' })).getAllByRole('listitem');
    expect(steps.map((step) => step.textContent)).toEqual([
      expect.stringContaining('ask #abc123'),
      expect.stringMatching(/Develop it.*done/),
      expect.stringMatching(/Verify it.*open.*this quest/),
      expect.stringMatching(/→ engine.*Report back.*not published yet/),
    ]);
  });

  /** D49 §4: which agent, version and account each attempt ran on — read from its record. */
  it('says on what each quest ran, attempt by attempt', () => {
    render(<ChainStrip chain={buildChain('a', QUESTS, [
      run('s1', 'a', { state: 'failed', harnessVersion: '2.1.270', profile: 'work' }),
      run('s2', 'a', { created: '2026-09-23T02:00:00Z', adapter: 'claude-code-acp' }),
    ])} />);

    expect(screen.getByText('claude-code · 2.1.270 · as work')).toBeInTheDocument();
    expect(screen.getByText('claude-code-acp')).toBeInTheDocument();
    expect(screen.getByText('failed')).toBeInTheDocument();
    // The quest after it has run nothing yet, and says so.
    expect(screen.getByText('no session yet')).toBeInTheDocument();
  });

  /** Seen on the window: "no session yet" under a closed quest promised a session that never comes. */
  it('says a closed quest nobody drove was closed without one', () => {
    render(<ChainStrip chain={buildChain('b', QUESTS, [])} />);

    expect(screen.getByText('closed without a session')).toBeInTheDocument();
    expect(screen.getByText('no session yet')).toBeInTheDocument();
  });

  it('opens another quest or a session through the surface that holds it, never the one being read', () => {
    const onQuest = vi.fn();
    const onSession = vi.fn();
    render(<ChainStrip
      chain={buildChain('b', QUESTS, [run('s1', 'a')])}
      onQuest={onQuest}
      onSession={onSession}
    />);

    fireEvent.click(screen.getByRole('button', { name: 'Develop it' }));
    expect(onQuest).toHaveBeenCalledWith(expect.objectContaining({ id: 'a' }));
    // The quest being read is not a door to itself.
    expect(screen.queryByRole('button', { name: 'Verify it' })).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'claude-code' }));
    expect(onSession).toHaveBeenCalledWith(expect.objectContaining({ id: 's1' }));
  });

  /**
   * SESS1 S7, from the first real workspace: under a quest carried on three times the strip drew three
   * identical rows, and nothing said which was the one being read.
   */
  it('marks the session being read, and tells a quest\'s sessions apart by when and how long', () => {
    const onSession = vi.fn();
    render(<ChainStrip
      attended="s2"
      chain={buildChain('a', QUESTS, [
        run('s1', 'a', { state: 'failed', created: '2026-09-23T01:00:00Z', updated: '2026-09-23T01:30:00Z' }),
        run('s2', 'a', { created: '2026-09-23T02:00:00Z', updated: '2026-09-23T02:12:00Z' }),
      ])}
      onSession={onSession}
    />);

    const rows = within(screen.getByRole('region', { name: 'How this work ran' })).getAllByRole('listitem')
      .filter((item) => /attempt/.test(item.textContent ?? '') && item.tagName === 'LI' && !item.querySelector('li'));
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringMatching(/failed.*attempt 1.*ran 30m/),
      expect.stringMatching(/completed.*attempt 2.*ran 12m.*this session/),
    ]);
    // The one being read is not a door to itself; the other is.
    expect(screen.getAllByRole('button', { name: /^attempt/ })).toHaveLength(1);
    fireEvent.click(screen.getByRole('button', { name: 'attempt 1' }));
    expect(onSession).toHaveBeenCalledWith(expect.objectContaining({ id: 's1' }));
  });

  /**
   * UX7c (D152 §7): a quest whose sessions all ran on one agent says it once, above its rows; each row says its attempt,
   * its account and how long, so *three failed before this one* reads at a glance.
   */
  it('says the agent once where every attempt ran on it, and each row its attempt and account', () => {
    render(<ChainStrip chain={buildChain('a', QUESTS, [
      run('s1', 'a', { state: 'failed', harnessVersion: '0.84.0', profile: 'account-1', adapter: 'claude-code-acp' }),
      run('s2', 'a', { state: 'failed', harnessVersion: '0.84.0', profile: 'account-1', adapter: 'claude-code-acp', created: '2026-09-23T02:00:00Z' }),
      run('s3', 'a', { state: 'working', harnessVersion: '0.84.0', profile: 'account-2', adapter: 'claude-code-acp', created: '2026-09-23T03:00:00Z' }),
    ])} />);

    expect(screen.getAllByText('claude-code-acp · 0.84.0')).toHaveLength(1);
    expect(screen.getByText('attempt 3')).toBeInTheDocument();
    expect(screen.getAllByText('account-1')).toHaveLength(2);
    expect(screen.getByText('account-2')).toBeInTheDocument();
  });

  /** ACCTNAME1 (D152 §4.2): each attempt's account by the person's name, where the surface hands the roster's namer. */
  it('names each attempt’s account by the person’s name where a namer is handed', () => {
    const nameOf = (_owner: string, profile?: string | null) => (profile === 'acct-3f9c1a2b' ? 'work' : profile ?? '');
    const { unmount } = render(<ChainStrip nameOf={nameOf} chain={buildChain('a', QUESTS, [
      run('s1', 'a', { state: 'failed', profile: 'acct-3f9c1a2b' }),
      run('s2', 'a', { state: 'working', profile: 'account-2', created: '2026-09-23T02:00:00Z' }),
    ])} />);

    expect(screen.getByText('work')).toBeInTheDocument();
    expect(screen.getByText('account-2')).toBeInTheDocument();
    expect(screen.queryByText('acct-3f9c1a2b')).toBeNull();
    unmount();

    // A quest whose attempts ran on different agents says each one's line, its account by name too.
    render(<ChainStrip nameOf={nameOf} chain={buildChain('a', QUESTS, [run('s1', 'a', { profile: 'acct-3f9c1a2b', harnessVersion: '2.1.270' })])} />);
    expect(screen.getByText('claude-code · 2.1.270 · as work')).toBeInTheDocument();
  });

  it('offers no door where the surface gave none', () => {
    render(<ChainStrip chain={buildChain('b', QUESTS, [run('s1', 'a')])} />);

    expect(screen.queryAllByRole('button')).toEqual([]);
  });
});
