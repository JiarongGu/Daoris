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

function view({ semantic = false, onConverge = () => {} }: { semantic?: boolean; onConverge?: () => void } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SearchView onOpen={() => {}} notify={() => {}} semantic={semantic} onConverge={onConverge} />
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
    // Its tint adds no space beside the word (UX5 U61): *syllables .* read as a gap before the stop.
    expect(screen.getByText('encoding', { selector: 'mark' })).toHaveClass('-mx-0.5');
  });

  /**
   * 🔴 UX5 U41: no match was a bare paragraph, which named the convergence view and offered no way
   * there, and it said *Lexical search matches words* on a deployment whose recall is semantic too.
   * An empty answer is an empty state (§4, POLISH4 for Convergence), in the tier's own words, and
   * its action goes where the sentence points.
   */
  it("answers nothing as an empty state, in its tier's words, with the way to convergence", async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json([])));
    const onConverge = vi.fn();
    const { unmount } = view({ onConverge });

    await userEvent.type(screen.getByRole('searchbox'), 'zebra quartz');
    expect(await screen.findByText('No matches')).toBeInTheDocument();
    expect(screen.getByText(/matches words, so a repository that reached the same conclusion/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'look for convergence' }));
    expect(onConverge).toHaveBeenCalledOnce();
    unmount();

    view({ semantic: true });
    await userEvent.type(screen.getByRole('searchbox'), 'zebra quartz');
    expect(await screen.findByText('No matches')).toBeInTheDocument();
    expect(screen.queryByText(/Lexical search matches words/)).toBeNull();
    expect(screen.getByText(/meaning as well as words/)).toBeInTheDocument();
  });

  /**
   * TIER1: the tier is the one that ANSWERED, per answer (D24), from the service's `x-daoris-tier`.
   * A deployment configured for meaning whose embedder was down said *matches meaning as well as
   * words* over an answer only words had made.
   */
  it('words an answer by the tier that answered it, not the one configured', async () => {
    const answered = (tier: string, body: unknown) =>
      vi.fn(async () => Response.json(body, { headers: { 'x-daoris-tier': tier } }));

    vi.stubGlobal('fetch', answered('lexical', []));
    const { unmount } = view({ semantic: true });
    await userEvent.type(screen.getByRole('searchbox'), 'zebra quartz');
    expect(await screen.findByText(/matches words, so a repository that reached the same conclusion/)).toBeInTheDocument();
    expect(screen.queryByText(/meaning as well as words/)).toBeNull();
    unmount();

    // Found things, but only by words, on a deployment that matches meaning too: said beside them.
    vi.stubGlobal('fetch', answered('lexical', HITS));
    const second = view({ semantic: true });
    await userEvent.type(screen.getByRole('searchbox'), 'encoding');
    expect(await screen.findByText('An encoding trap')).toBeInTheDocument();
    expect(screen.getByText(/matched on words only: the search by meaning did not answer/i)).toBeInTheDocument();
    second.unmount();

    // Nothing answering is not nothing matching.
    vi.stubGlobal('fetch', answered('none', []));
    view({ semantic: true });
    await userEvent.type(screen.getByRole('searchbox'), 'zebra quartz');
    expect(await screen.findByText('Nothing answered')).toBeInTheDocument();
    expect(screen.queryByText('No matches')).toBeNull();
  });
});
