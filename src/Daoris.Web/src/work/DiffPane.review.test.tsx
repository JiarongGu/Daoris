import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

/**
 * UXFIX2b2b: the review's own discard asks twice. The unforced press's refusal names what would go and arms the ask, which
 * says it; the forced press stays open and waiting until the driver answers, says its own refusal inside itself, and closes
 * once the tree went, the focus back on the press drawn again.
 */
describe('the review’s forced discard', () => {
  afterEach(() => { invoke.mockReset(); });

  const NAMES = 'The tree holds 2 uncommitted path(s), which a discard would destroy.';
  const discards = () => invoke.mock.calls.filter(([, type]) => type === 'DISCARD_SESSION_TREE').map(([, , options]) => options.payload);

  function drive(forced: Array<{ done: boolean; message: string } | Error>) {
    const queue = [...forced];
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: { force?: boolean } }) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return PLAN;
      if (type === 'DISCARD_SESSION_TREE') {
        if (!options?.payload?.force) return { done: false, message: NAMES };
        const next = queue.shift()!;
        if (next instanceof Error) throw next;
        return next;
      }
      return {};
    });
  }

  const armed = async () => {
    pane();
    await userEvent.click(await screen.findByRole('button', { name: 'Discard tree' }));
    return await screen.findByRole('group', { name: 'discard this tree' });
  };

  it('arms an ask that says what would go, and the first press stays a ghost until then', async () => {
    drive([]);
    const ask = await armed();

    expect(ask).toHaveTextContent(NAMES);
    // Said once: the ask holds the sentence, and the foot does not repeat it.
    expect(screen.getAllByText(NAMES)).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
    expect(within(ask).getByRole('button', { name: 'Discard anyway' }).className).toContain('text-ink-danger');
    expect(discards()).toEqual([{ id: 's1a2b3c4' }]);
  });

  it('says a refused forced discard inside the ask, and lets it be pressed again', async () => {
    drive([{ done: false, message: 'The tree is busy.' }, new Error('The driver is not running.')]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    expect(await within(ask).findByRole('alert')).toHaveTextContent('The tree is busy.');
    expect(discards()[1]).toEqual({ id: 's1a2b3c4', force: true });

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    await waitFor(() => expect(within(ask).getByRole('alert')).toHaveTextContent('The driver is not running.'));
    expect(screen.getByRole('group', { name: 'discard this tree' })).toBeInTheDocument();
  });

  it('closes once the tree went, says so in the foot, and gives the focus back to the press drawn again', async () => {
    drive([{ done: true, message: 'Discarded the tree.' }]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    await waitFor(() => expect(screen.queryByRole('group', { name: 'discard this tree' })).toBeNull());
    expect(screen.getByText('Discarded the tree.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toHaveFocus();
  });

  it('puts the ask down on Never mind, forcing nothing, and returns the focus', async () => {
    drive([]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'discard this tree' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toHaveFocus();
    expect(discards()).toEqual([{ id: 's1a2b3c4' }]);
  });
});
