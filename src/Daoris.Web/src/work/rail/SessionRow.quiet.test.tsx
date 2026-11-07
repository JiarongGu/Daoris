import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import { SessionRow } from './SessionRow';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4',
  repository: 'engine',
  door: 'claude-code',
  state: 'working',
  kind: 'driven',
  created: '2026-09-21T09:46:00Z',
  updated: '2026-09-21T11:56:00Z',
  ...over,
});

const placed = (over: Partial<SessionGrouping> & Pick<SessionGrouping, 'group' | 'shown'>): SessionGrouping => ({
  session: 's1a2b3c4', archived: false, teammate: false, ...over,
});

/**
 * A quiet session Daoris's one word is kept for says it is told beside the quiet
 * it speaks of, and one it is not kept for says only that it is quiet. In both languages.
 */
describe('a quiet session’s row', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-21T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('says it was told, only where the watcher kept its word, in en and zh', async () => {
    const quiet = '2026-09-21T11:46:00Z';
    const calling = { title: '', since: quiet, quiet };
    const { rerender } = render(<SessionRow session={session()} place="engine" grouping={placed({ group: 'working', shown: 'working', calling: { ...calling, quietTold: true } })} />);
    expect(screen.getByText(/quiet · 14m · Daoris told it · /)).toBeInTheDocument();

    rerender(<SessionRow session={session()} place="engine" grouping={placed({ group: 'working', shown: 'working', calling })} />);
    expect(screen.getByText(/quiet · 14m · /)).toBeInTheDocument();
    expect(screen.queryByText(/Daoris told it/)).toBeNull();

    await i18n.changeLanguage('zh');
    try {
      rerender(<SessionRow session={session()} place="engine" grouping={placed({ group: 'working', shown: 'working', calling: { ...calling, quietTold: true } })} />);
      expect(screen.getByText(/无动静 · 14 分钟 · Daoris 已提醒它/)).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
