import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { findingId } from './knowledge/records';
import { code } from './test/code';
import { chooseRow, listOf, mainArea, pageTitled, showConvergence } from './test/knowledgeViews';

// The view over a stubbed service, like SearchView's: the shapes the endpoint returns, no host. Since FRAME1f the view
// is a list pane and a page (D118 §2): the similarity and the findings are the list, and the finding chosen is read in
// the main area, the service's sentence and then each entry whole, where each entry was the reader drawer.

const GROUP = {
  method: 'Restatement', similarity: 0.91, repositories: ['engine', 'game'],
  suggestion: 'A copy that has drifted. Read both, and promote one with `daoris upstream <file>`.',
  entries: [
    { id: 'engine:.claude/rules/a.md', repository: 'engine', kind: 'Rule', path: '.claude/rules/a.md', title: 'a', excerpt: '' },
    { id: 'game:.claude/rules/a.md', repository: 'game', kind: 'Rule', path: '.claude/rules/a.md', title: 'a', excerpt: '' },
  ],
};
const BODIES: Record<string, string> = {
  'engine:.claude/rules/a.md': '# A rule\n\nThe engine says **this**.',
  'game:.claude/rules/a.md': '# A rule\n\nThe game says **this**, and a little more.',
};

const FILTERS = 'daoris.list.convergence.filters';
const CHOSEN = 'daoris.list.convergence.chosen';

/** The service: its findings at any similarity, and each entry by its id. */
function service(groups: unknown[] = [GROUP]) {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/convergence')) return Response.json(groups);
    if (url.startsWith('/api/entry')) {
      const id = decodeURIComponent(url.slice(url.indexOf('id=') + 3));
      const entry = GROUP.entries.find((candidate) => candidate.id === id);
      return entry && BODIES[id]
        ? Response.json({ ...entry, provenance: 'Local', body: BODIES[id] })
        : Response.json({ error: `no entry with id '${id}'` }, { status: 404 });
    }
    throw new Error(`unstubbed request: ${url}`);
  });
}

const slider = () => within(listOf('Convergence')).getByRole('slider');

describe('Convergence', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  /**
   * On the first real index the comparison took seconds, behind one bare "comparing…" line on an
   * otherwise empty page (POLISH3). A first load shows static skeleton rows (D41 §4), with the
   * words in the line the count will take, so nothing moves when the answer lands.
   */
  it('holds the list with skeleton rows while it compares, and the count takes the line after', async () => {
    let answer: (value: Response) => void = () => {};
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>((resolve) => { answer = resolve; })));
    const { container } = showConvergence();

    expect(await within(listOf('Convergence')).findByText('comparing…')).toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i').length).toBeGreaterThan(0);

    answer(Response.json([GROUP]));
    expect(await screen.findByText('1 finding')).toBeInTheDocument();
    expect(screen.queryByText('comparing…')).not.toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i')).toHaveLength(0);
  });

  /**
   * D118 §3d (audit CO4, A2): a finding opens in the main area, where its entries were each a drawer over the side
   * bar and the panel: the service's sentence first, verbatim, then each entry read whole, as it is written.
   */
  it("reads a finding in the main area: the service's sentence, then each entry whole", async () => {
    vi.stubGlobal('fetch', service());
    showConvergence();
    const page = await chooseRow('Convergence', 'a');

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(within(page).getByText(/A copy that has drifted/)).toBeInTheDocument();
    expect(within(page).getByText(code('daoris upstream <file>'))).toBeInTheDocument();
    const engine = within(page).getAllByRole('region', { name: 'a' })[0]!;
    expect(await within(engine).findByText(/The engine says \*\*this\*\*/)).toHaveProperty('tagName', 'PRE');
    expect(await within(page).findByText(/The game says \*\*this\*\*, and a little more/)).toBeInTheDocument();
    expect(within(page).getByText('engine ↔ game')).toBeInTheDocument();
    expect(within(page).getByText('Substantially the same words')).toBeInTheDocument();
  });

  /** An entry the index let go since the finding was made says so in its place, and the others still read. */
  it("says an entry the index no longer holds in its place, and reads the others", async () => {
    delete BODIES['game:.claude/rules/a.md'];
    try {
      vi.stubGlobal('fetch', service());
      showConvergence();
      const page = await chooseRow('Convergence', 'a');

      expect(await within(page).findByText(/No longer in the index/)).toBeInTheDocument();
      expect(await within(page).findByText(/The engine says \*\*this\*\*/)).toBeInTheDocument();
    } finally {
      BODIES['game:.claude/rules/a.md'] = '# A rule\n\nThe game says **this**, and a little more.';
    }
  });

  /** D118 §3f (audit CO9): the similarity and the finding chosen are the list's memory, and a relaunch reopens both. */
  it('remembers the similarity and the finding chosen, and reopens them', async () => {
    const fetch = service();
    vi.stubGlobal('fetch', fetch);
    showConvergence();
    fireEvent.change(slider(), { target: { value: '0.66' } });
    await chooseRow('Convergence', 'a');

    await vi.waitFor(() => expect(JSON.parse(window.localStorage.getItem(FILTERS) ?? '{}')).toEqual({ threshold: 0.66 }));
    expect(window.localStorage.getItem(CHOSEN)).toBe(findingId(GROUP));
    expect(fetch.mock.calls.map((call) => String(call[0])).some((url) => url.includes('minimumSimilarity=0.66'))).toBe(true);

    cleanup();
    showConvergence();
    expect(slider()).toHaveValue('0.66');
    expect(await pageTitled('a')).toBeInTheDocument();
  });

  /** D118 §3b: a finding chosen that the answer no longer holds has gone, at the similarity it was asked at. */
  it('says a finding chosen now has gone once the findings no longer hold it', async () => {
    vi.stubGlobal('fetch', service([]));
    showConvergence({ door: findingId(GROUP) });

    await screen.findByText('This finding is no longer listed');
    expect(within(mainArea()).getByText('This finding is no longer listed')).toBeInTheDocument();
    expect(within(mainArea()).getByText(/no longer converge at 0.75 or above/)).toBeInTheDocument();
  });

  // UX6b (design §1 rule 6): Convergence opened on *this finding is no longer in the list* on the owner's install. A
  // remembered finding the index no longer holds opens nothing chosen; one still listed reopens.
  it('opens with nothing chosen on a remembered finding that has gone, never on its gone state', async () => {
    vi.stubGlobal('fetch', service([]));
    window.localStorage.setItem(CHOSEN, findingId(GROUP));
    showConvergence();

    await waitFor(() => expect(mainArea()).toHaveTextContent('Choose a finding'));
    expect(screen.queryByText('This finding is no longer listed')).toBeNull();
    expect(window.localStorage.getItem(CHOSEN)).toBeNull();
  });

  it('says how to choose with nothing chosen', async () => {
    vi.stubGlobal('fetch', service());
    showConvergence();
    expect(within(mainArea()).getByText('Choose a finding')).toBeInTheDocument();
  });
});

describe('Convergence, looked at on the window (POLISH4)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  /** The tier's note says what this deployment can find, under the similarity it is about, in the list. */
  it("says the tier's note under the similarity", async () => {
    vi.stubGlobal('fetch', service());
    showConvergence();

    expect(within(listOf('Convergence')).getByText(/Without an embedding endpoint/)).toBeInTheDocument();
  });

  /** An empty answer was a bare line; an empty state names the fact and offers what changes it. */
  it('says nothing converges as an empty state, and lowers the similarity on a press', async () => {
    vi.stubGlobal('fetch', service([]));
    showConvergence();

    expect(await screen.findByText('Nothing converges at 0.75 or above')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Lower to 0.65' }));
    expect(await screen.findByText('Nothing converges at 0.65 or above')).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U42: at the floor the empty state still said *a lower similarity finds weaker likenesses*
   * and offered nothing to lower, because nothing lower is offered. At the floor it says so.
   */
  it('says the floor is the floor, rather than inviting a lower similarity it cannot give', async () => {
    vi.stubGlobal('fetch', service([]));
    showConvergence();

    for (const step of ['0.65', '0.55', '0.50']) {
      await userEvent.click(await screen.findByRole('button', { name: `Lower to ${step}` }));
    }
    expect(await screen.findByText('Nothing converges at 0.50 or above')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /lower it/ })).toBeNull();
    expect(screen.queryByText(/A lower similarity finds/)).toBeNull();
    expect(screen.getByText(/lowest it goes/)).toBeInTheDocument();
  });
});
