import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { SearchView } from './SearchView';

// The view over a stubbed service, like QuestsView's: the shapes the endpoint returns, no host.

const HITS = [{
  id: 'e1', repository: 'engine', kind: 'Fix', path: 'docs/FIX-LOG.md',
  title: 'An encoding trap', excerpt: 'set the encoding first', score: 1,
}];

function view() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SearchView onOpen={() => {}} notify={() => {}} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('SearchView', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/search')) return Response.json(HITS);
      throw new Error(`unstubbed request: ${url}`);
    }));
  });
  afterEach(() => vi.unstubAllGlobals());

  /**
   * The clearing control is the platform's own, and exists only while there is something to
   * clear. `type="search"` brings Chromium's native one, which WebView2 paints in Windows' accent
   * colour — a blue ✕ in a palette with no blue, seen on the deployed application; that one is
   * hidden by tokens.css, and this is what stands in its place.
   */
  it('offers to clear the query only once there is one, and clears it', async () => {
    view();
    const box = screen.getByRole('searchbox');
    expect(screen.queryByRole('button', { name: 'clear the query' })).not.toBeInTheDocument();

    await userEvent.type(box, 'encoding');
    expect(box).toHaveValue('encoding');
    await userEvent.click(screen.getByRole('button', { name: 'clear the query' }));

    expect(box).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'clear the query' })).not.toBeInTheDocument();
  });

  it('marks the matched term in each excerpt', async () => {
    view();
    await userEvent.type(screen.getByRole('searchbox'), 'encoding');

    expect(await screen.findByText('An encoding trap')).toBeInTheDocument();
    expect(screen.getByText('encoding', { selector: 'mark' })).toBeInTheDocument();
  });
});
