import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// LANDSYNC1 (D87's 2026-10-15 amendment points 4 and 5): the head's land ask says what Accept leaves out because the line holds it,
// in both languages; where replaying conflicts, *Accept…*'s place holds the words its session is asked to rebase with.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { SessionHead } from './SessionHead';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q1', repository: 'reports', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z', tree: 'C:/somewhere/reports', ...over,
});
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 3, uncommitted: 0 };
const LEFT = [{ commit: '1a2b3c4d', subject: 'the fix', why: 'patch', on: '9f8e7d6c' }];
const REPLAYS = [{ commit: '2b3c4d5e', subject: 'more of the work' }, { commit: '3c4d5e6f', subject: 'the notes' }];
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/delta', leftOut: LEFT, lands: REPLAYS };
const CONFLICT = { says: 'CONFLICT (content): Merge conflict in CHANGELOG.md.', words: 'Rebase your work onto `main`.' };

const sayRebase = vi.fn(async () => true);

function head(landing: unknown) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <SessionHead session={session()} lands={LANDS} onLand={vi.fn()} onName={vi.fn()} onSayRebase={sayRebase} landing={landing as never} />
    </QueryClientProvider>,
  );
}

describe("the head's land ask and what the line holds", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says what Accept leaves out and why, and what it replays', async () => {
    head(PLAN);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));

    const ask = screen.getByRole('group', { name: 'accept daoris/s-1a2b3c4' });
    expect(within(ask).getByText('Accepting leaves out 1 commit the line already holds:')).toBeInTheDocument();
    expect(within(ask).getByText(/on the line as/)).toHaveTextContent('on the line as 9f8e7d6c, the same change');
    expect(within(ask).getByText('It replays the 2 other commits onto the line.')).toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    head(PLAN);
    await userEvent.click(screen.getByRole('button', { name: '采纳…' }));

    expect(screen.getByText('采纳会略去主线已包含的 1 个提交：')).toBeInTheDocument();
    expect(screen.getByText(/在主线上，是同一改动/)).toHaveTextContent('以 9f8e7d6c 在主线上，是同一改动');
    expect(screen.getByText('其余 2 个提交会重放到主线上。')).toBeInTheDocument();
  });

  it('offers the rebase words in Accept’s place where replaying conflicts', () => {
    head({ ...PLAN, conflict: CONFLICT });

    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(screen.getByText(/replaying this work onto the line conflicts/)).toHaveTextContent('Merge conflict in CHANGELOG.md');
    expect(screen.getByRole('button', { name: 'Ask it to rebase' })).toBeInTheDocument();
  });
});
