import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEWENV1g (D154 point 7; the review environment design §3.1): the session's review says the work waits for the person's
// review where *Accept* would be, and offers no *Accept* the landing door would refuse; where nothing waits, it is as it was.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../i18n';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'src/report/compare.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main', source: 'workspace' };

function answer(landing: unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return landing;
    return {};
  });
}

function pane() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe("the review's foot while a review's gate holds the work", () => {
  afterEach(() => { invoke.mockReset(); });

  it.each(['kind-paths', 'cannot-start', 'unread'])('shows the workflow %s hold instead of Accept', async (state) => {
    answer({ ...PLAN, workflow: { state, holds: true, says: 'Holds: choose a saved workflow in the terminal.' } });
    pane();
    expect(await screen.findByRole('region', { name: 'Workflow before it lands' })).toHaveTextContent('choose a saved workflow');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });

  it('says it waits for the review in its environment, in the gate\'s state, and offers no Accept', async () => {
    answer({ ...PLAN, review: { state: 'shown', environment: 'local', level: 'set-up-step', quest: 'q2' } });
    pane();

    expect(await screen.findByText(/Waits for your review in/)).toHaveTextContent('Waits for your review in local · shown');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toBeInTheDocument();
  });

  it('offers Accept as before where the gate lets the work go', async () => {
    answer(PLAN);
    pane();

    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByText(/Waits for your review/)).toBeNull();
  });
});
