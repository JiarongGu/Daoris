import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
import { StreamTile } from './StreamTile';

// A molecule, so every state is reached by PASSING it (components plan §2) — including the one
// nothing local can produce: a session running on somebody else's machine.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-22T09:46:00Z',
  updated: '2026-09-22T11:56:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  filed: '2026-09-14T09:00:00Z',
  updated: '2026-09-22T09:00:00Z',
  ...over,
});

describe('a stream tile', () => {
  beforeEach(async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-09-22T12:00:00Z'));
    await i18n.changeLanguage('en');
  });

  it('names what the session is for, and how long it has been going', () => {
    render(
      <StreamTile session={session()} quest={quest()}>
        <pre>building…</pre>
      </StreamTile>,
    );

    expect(screen.getByText('Expose a streaming budget on the chunk API')).toBeInTheDocument();
    expect(screen.getByText('engine')).toBeInTheDocument();
    expect(screen.getByText('building…')).toBeInTheDocument();
    // Live, so the span runs to now rather than to the last update.
    expect(screen.getByText(/2h 14m/)).toBeInTheDocument();
  });

  /**
   * The reference console's one priority rule (components plan §3a): a session that needs the
   * person wears the attention mark even while its process is busy.
   */
  it('wears the attention mark when a session is waiting on a person', () => {
    render(<StreamTile session={session({ state: 'awaiting-person' })} />);

    expect(screen.getByText('awaiting person')).toBeInTheDocument();
  });

  /**
   * A record fed from another machine is keyed `origin/id` (D47 §6) and its console never left that
   * machine. Saying so beats an empty well, which would read as "this session is silent".
   */
  it('says where a session runs when its stream is not this machine to give', () => {
    render(
      <StreamTile session={session({ id: 'laptop/s1a2b3c4' })}>
        <pre>a stream that should not be rendered</pre>
      </StreamTile>,
    );

    expect(screen.getByText(/runs on laptop/i)).toBeInTheDocument();
    expect(screen.queryByText('a stream that should not be rendered')).not.toBeInTheDocument();
  });

  it('offers a session its own window, where the shell can open one', async () => {
    const detach = vi.fn();
    render(<StreamTile session={session()} onDetach={detach} />);

    await userEvent.click(screen.getByRole('button', { name: /own window/i }));

    expect(detach).toHaveBeenCalledWith('s1a2b3c4');
  });

  it('offers no window where nothing can open one', () => {
    render(<StreamTile session={session()} />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('speaks the reader’s language', async () => {
    await i18n.changeLanguage('zh');
    render(<StreamTile session={session({ kind: 'chat', quest: undefined })} />);

    expect(screen.getByText('对话')).toBeInTheDocument();
  });
});
