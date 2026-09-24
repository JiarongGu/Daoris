import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
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
