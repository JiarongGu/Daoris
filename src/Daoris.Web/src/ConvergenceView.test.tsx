import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { ConvergenceView } from './ConvergenceView';

// The view over a stubbed service, like SearchView's: the shapes the endpoint returns, no host.

const GROUP = {
  method: 'Restatement', similarity: 0.91, repositories: ['engine', 'game'],
  suggestion: 'A copy that has drifted. Read both, and promote one with `daoris upstream <file>`.',
  entries: [
    { id: 'e1', repository: 'engine', kind: 'Rule', path: '.claude/rules/a.md', title: 'a', excerpt: '' },
    { id: 'e2', repository: 'game', kind: 'Rule', path: '.claude/rules/a.md', title: 'a', excerpt: '' },
  ],
};

function view() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <ConvergenceView semantic={false} onOpen={() => {}} notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('ConvergenceView', () => {
  afterEach(() => vi.unstubAllGlobals());

  /**
   * On the first real index the comparison took seconds, behind one bare "comparing…" line on an
   * otherwise empty page (POLISH3). A first load shows static skeleton rows (D41 §4), with the
   * words in the line the count will take, so nothing moves when the answer lands.
   */
  it('holds the page with skeleton rows while it compares, and the count takes the line after', async () => {
    let answer: (value: Response) => void = () => {};
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { container } = view();

    expect(await screen.findByText('comparing…')).toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i').length).toBeGreaterThan(0);

    answer(Response.json([GROUP]));
    expect(await screen.findByText('1 group')).toBeInTheDocument();
    expect(screen.queryByText('comparing…')).not.toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i')).toHaveLength(0);
  });
});

describe('ConvergenceView, looked at on the window (POLISH4)', () => {
  afterEach(() => vi.unstubAllGlobals());

  /** The tier's note ran the whole column, about 180 characters a line; prose has a measure (§4). */
  it('sets the tier note at the prose measure', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json([GROUP])));
    view();

    const note = await screen.findByText(/Without an embedding endpoint/);
    expect(note).toHaveClass('max-w-prose');
  });

  /** An empty answer was a bare line; an empty state names the fact and offers what changes it. */
  it('says nothing converges as an empty state, and lowers the similarity on a press', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
    view();

    expect(await screen.findByText('Nothing converges at 0.75 or above')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Lower to 0.65' }));
    expect(await screen.findByText('Nothing converges at 0.65 or above')).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U42: at the floor the empty state still said *a lower similarity finds weaker likenesses*
   * and offered nothing to lower, because nothing lower is offered. At the floor it says so.
   */
  it('says the floor is the floor, rather than inviting a lower similarity it cannot give', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
    view();

    for (const step of ['0.65', '0.55', '0.50']) {
      await userEvent.click(await screen.findByRole('button', { name: `Lower to ${step}` }));
    }
    expect(await screen.findByText('Nothing converges at 0.50 or above')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /lower it/ })).toBeNull();
    expect(screen.queryByText(/A lower similarity finds/)).toBeNull();
    expect(screen.getByText(/lowest it goes/)).toBeInTheDocument();
  });
});
