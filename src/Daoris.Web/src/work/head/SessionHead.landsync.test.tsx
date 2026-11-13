import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Where replaying the work onto the line conflicts, the review door gives way to the rebase words its session is asked to say, in both languages.

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
  id: 's1a2b3c4', quest: 'q1', repository: 'reports', door: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z', tree: 'C:/somewhere/reports', ...over,
});
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 3, uncommitted: 0 };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/delta' };
const CONFLICT = { says: 'CONFLICT (content): Merge conflict in CHANGELOG.md.', words: 'Rebase your work onto `main`.' };

const sayRebase = vi.fn(async () => true);

function head(landing: unknown) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <SessionHead session={session()} lands={LANDS} onReviewRun={vi.fn()} onSayRebase={sayRebase} landing={landing as never} />
    </QueryClientProvider>,
  );
}

describe("the head's rebase words where the line conflicts", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('offers the rebase words in the review door’s place where replaying conflicts', () => {
    head({ ...PLAN, conflict: CONFLICT });

    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
    expect(screen.getByText(/replaying this work onto the line conflicts/)).toHaveTextContent('Merge conflict in CHANGELOG.md');
    expect(screen.getByRole('button', { name: 'Ask it to rebase' })).toBeInTheDocument();
  });

  it('offers the door and no rebase words where nothing conflicts', () => {
    head(PLAN);

    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ask it to rebase' })).toBeNull();
  });

  it('says the rebase words in Chinese', async () => {
    await i18n.changeLanguage('zh');
    head({ ...PLAN, conflict: CONFLICT });

    expect(screen.getByRole('button', { name: '请它变基' })).toBeInTheDocument();
  });
});
