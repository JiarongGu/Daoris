import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  chooseRow, knowledgeList, mainArea, modeChoice, pageTitled, showKnowledge, showSearch, switchTo,
} from './test/knowledgeViews';

// The view over a stubbed service, like QuestsView's: the shapes the endpoint returns, no host. Since FRAME1f the view
// is a list pane and a page (D118 §2): the box, *local only* and the hits are the list, and the entry a hit names is
// read in the main area, where it was the reader drawer. Since UX6i (D150 §2.2) it is Knowledge's Search, held through
// the place: Knowledge's list is Search's while Search is chosen at its head.

const HITS = [{
  id: 'engine:docs/FIX-LOG.md#encoding', repository: 'engine', kind: 'Fix', path: 'docs/FIX-LOG.md',
  title: 'An encoding trap', excerpt: 'set the encoding first', score: 1,
}];
const ENTRY = {
  id: 'engine:docs/FIX-LOG.md#encoding', repository: 'engine', kind: 'Fix', provenance: 'Local', path: 'docs/FIX-LOG.md',
  title: 'An encoding trap', body: 'Set the **encoding** first, then write.\nA console that is not UTF-8 mangles the rest.',
};

const FILTERS = 'daoris.list.search.filters';
const CHOSEN = 'daoris.list.search.chosen';

/** The service: the hits for any search, the one entry by its id, and no findings, for Knowledge's other mode. */
function service(hits: unknown = HITS, tier?: string) {
  return vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/search')) return Response.json(hits, tier ? { headers: { 'x-daoris-tier': tier } } : undefined);
    if (url.startsWith('/api/convergence')) return Response.json([]);
    if (url.startsWith('/api/entry')) {
      return url.includes(encodeURIComponent(ENTRY.id))
        ? Response.json(ENTRY)
        : Response.json({ error: 'no entry with that id' }, { status: 404 });
    }
    throw new Error(`unstubbed request: ${url}`);
  });
}

const box = () => within(knowledgeList()).getByRole('searchbox', { name: 'search knowledge' });

describe('Search', () => {
  beforeEach(() => vi.stubGlobal('fetch', service()));
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  /**
   * The clearing control is the platform's own, and exists only while there is something to
   * clear. `type="search"` brings Chromium's native one, which WebView2 paints in Windows' accent
   * colour — a blue ✕ in a palette with no blue, seen on the deployed application; that one is
   * hidden by tokens.css, and this is what stands in its place.
   */
  it('offers to clear the query only once there is one, and clears it', async () => {
    showSearch();
    expect(screen.queryByRole('button', { name: 'Clear search' })).not.toBeInTheDocument();

    await userEvent.type(box(), 'encoding');
    expect(box()).toHaveValue('encoding');
    await userEvent.click(screen.getByRole('button', { name: 'Clear search' }));

    expect(box()).toHaveValue('');
    expect(screen.queryByRole('button', { name: 'Clear search' })).not.toBeInTheDocument();
  });

  /** D118 §3a: a list's search clears on Escape, as the session list's does. */
  it('clears what was typed on Escape', async () => {
    showSearch();
    await userEvent.type(box(), 'encoding{Escape}');
    expect(box()).toHaveValue('');
  });

  it('marks the matched term in each excerpt', async () => {
    showSearch();
    await userEvent.type(box(), 'encoding');

    expect(await within(knowledgeList()).findByText('An encoding trap')).toBeInTheDocument();
    expect(screen.getByText('encoding', { selector: 'mark' })).toBeInTheDocument();
    // Its tint adds no space beside the word (UX5 U61): *syllables .* read as a gap before the stop.
    expect(screen.getByText('encoding', { selector: 'mark' })).toHaveClass('-mx-0.5');
  });

  /**
   * D118 §3d (audit SR4, A2): a hit opens its entry in the main area, where the reader drawer lay over the side bar
   * and the panel; the entry is read as it is written, and the hit wears the list's choice.
   */
  it('reads the entry a hit names in the main area, as it is written, and in no drawer', async () => {
    showSearch();
    await userEvent.type(box(), 'encoding');
    const page = await chooseRow('An encoding trap');

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(within(page).getByText(/Set the \*\*encoding\*\* first/).tagName).toBe('PRE');
    expect(within(page).getByText('engine · docs/FIX-LOG.md')).toBeInTheDocument();
    const row = within(knowledgeList()).getByRole('listitem', { name: 'An encoding trap' });
    expect(within(row).getByRole('button')).toHaveAttribute('aria-current', 'true');
  });

  /** D118 §3e (audit SR8): ↓ goes from the box into the hits, as VS Code's filter boxes hand it on. */
  it('moves from the box into the hits on ↓', async () => {
    showSearch();
    await userEvent.type(box(), 'encoding');
    const row = await within(knowledgeList()).findByRole('listitem', { name: 'An encoding trap' });

    await userEvent.keyboard('{ArrowDown}');
    expect(within(row).getByRole('button')).toHaveFocus();
  });

  /** D118 §3f (audit SR9): *local only* and the entry chosen are the list's memory; what was typed is not. */
  it('remembers local only and the entry chosen, and reopens them, but not what was typed', async () => {
    const fetch = service();
    vi.stubGlobal('fetch', fetch);
    showSearch();
    await userEvent.click(within(knowledgeList()).getByRole('checkbox', { name: "Each repository's own only" }));
    await userEvent.type(box(), 'encoding');
    await chooseRow('An encoding trap');

    expect(JSON.parse(window.localStorage.getItem(FILTERS) ?? '{}')).toEqual({ localOnly: false });
    expect(window.localStorage.getItem(CHOSEN)).toBe(ENTRY.id);
    expect(fetch.mock.calls.map((call) => String(call[0])).some((url) => url.includes('localOnly=false'))).toBe(true);

    cleanup();
    showSearch();
    expect(within(knowledgeList()).getByRole('checkbox', { name: "Each repository's own only" })).not.toBeChecked();
    expect(box()).toHaveValue('');
    expect(await pageTitled('An encoding trap')).toBeInTheDocument();

    // Local only again is the default, so it is kept as nothing.
    await userEvent.click(within(knowledgeList()).getByRole('checkbox', { name: "Each repository's own only" }));
    expect(window.localStorage.getItem(FILTERS)).toBeNull();
  });

  /** D118 §3b: with nothing chosen the main area says how to choose; an entry the index let go says it has gone. */
  it('says how to choose with nothing chosen, and that an entry chosen now has gone', async () => {
    showSearch();
    expect(within(mainArea()).getByText('Choose a result')).toBeInTheDocument();

    cleanup();
    showSearch({ door: 'game:.claude/knowledge/retired.md' });
    await screen.findByText('This entry is no longer in the index');
    expect(within(mainArea()).getByText('This entry is no longer in the index')).toBeInTheDocument();
  });

  // UX6b (design §1 rule 6): a remembered entry the index let go opens nothing chosen, never on its gone state.
  it('opens with nothing chosen on a remembered entry that has gone, and forgets it', async () => {
    window.localStorage.setItem(CHOSEN, 'game:.claude/knowledge/retired.md');
    showSearch();
    await waitFor(() => expect(window.localStorage.getItem(CHOSEN)).toBeNull());
    expect(mainArea()).toHaveTextContent('Choose a result');
    expect(screen.queryByText('This entry is no longer in the index')).toBeNull();
  });

  /**
   * Audit SR11: a first answer is skeleton rows with its words on the count's line (platform language §4), where it
   * was a line of words alone; and a newer search holds the last hits at reduced opacity rather than blanking the
   * list as the person types.
   */
  it('holds a first search with skeleton rows, and a newer one with the last hits, dimmed', async () => {
    // Each search waits until the test answers it, by what was asked: under load the box's debounce may ask for a
    // part of the word first, and only the whole word's answer is the one the list shows.
    const pending = new Map<string, (value: Response) => void>();
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => new Promise<Response>((resolve) => {
      pending.set(new URL(String(input), 'http://host').searchParams.get('q') ?? '', resolve);
    })));
    const settle = async (q: string, response: Response) => {
      await vi.waitFor(() => expect(pending.has(q)).toBe(true));
      pending.get(q)!(response);
    };
    const { container } = showSearch();

    await userEvent.type(box(), 'encoding');
    expect(await within(knowledgeList()).findByText('searching…')).toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i').length).toBeGreaterThan(0);

    await settle('encoding', Response.json(HITS));
    expect(await within(knowledgeList()).findByText('1 result')).toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"] > i')).toHaveLength(0);

    await userEvent.type(box(), ' trap');
    expect(await within(knowledgeList()).findByText('searching…')).toBeInTheDocument();
    const held = within(knowledgeList()).getByRole('listitem', { name: 'An encoding trap' });
    expect(held.closest('ul')).toHaveClass('opacity-60');
    expect(container.querySelectorAll('[aria-hidden="true"] > i')).toHaveLength(0);

    await settle('encoding trap', Response.json([]));
    expect(await within(knowledgeList()).findByText('No matches')).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U41: no match was a bare paragraph, which named the convergence view and offered no way
   * there, and it said *Lexical search matches words* on a deployment whose recall is semantic too.
   * An empty answer is an empty state (§4, POLISH4 for Convergence), in the tier's own words, and
   * its action goes where the sentence points.
   */
  it("answers nothing as an empty state, in its tier's words, with the way to convergence", async () => {
    vi.stubGlobal('fetch', service([]));
    const { unmount } = showSearch();

    await userEvent.type(box(), 'zebra quartz');
    expect(await screen.findByText('No matches')).toBeInTheDocument();
    expect(screen.getByText(/matches words, so a repository that reached the same conclusion/)).toBeInTheDocument();
    // Since UX6i the way to Convergence is Knowledge's other mode: the place switches, its head says so.
    await userEvent.click(screen.getByRole('button', { name: 'Look in Convergence' }));
    expect(within(modeChoice()).getByRole('radio', { name: 'Convergence' })).toHaveAttribute('aria-checked', 'true');
    expect(within(knowledgeList()).getByRole('slider')).toBeInTheDocument();
    unmount();

    showSearch({ semantic: true });
    await userEvent.type(box(), 'zebra quartz');
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
    vi.stubGlobal('fetch', service([], 'lexical'));
    const { unmount } = showSearch({ semantic: true });
    await userEvent.type(box(), 'zebra quartz');
    expect(await screen.findByText(/matches words, so a repository that reached the same conclusion/)).toBeInTheDocument();
    expect(screen.queryByText(/meaning as well as words/)).toBeNull();
    unmount();

    // Found things, but only by words, on a deployment that matches meaning too: said beside them.
    vi.stubGlobal('fetch', service(HITS, 'lexical'));
    const second = showSearch({ semantic: true });
    await userEvent.type(box(), 'encoding');
    expect(await screen.findByText('An encoding trap')).toBeInTheDocument();
    expect(screen.getByText(/matched on words only: the search by meaning did not answer/i)).toBeInTheDocument();
    second.unmount();

    // Nothing answering is not nothing matching.
    vi.stubGlobal('fetch', service([], 'none'));
    showSearch({ semantic: true });
    await userEvent.type(box(), 'zebra quartz');
    expect(await screen.findByText('Nothing answered')).toBeInTheDocument();
    expect(screen.queryByText('No matches')).toBeNull();
  });
});

/**
 * UX6i (D150 §2.2): Search and Convergence are one place, Knowledge, whose list's head switches between them, remembered.
 * Each mode keeps its list, its main area and its memory, so what was typed, the entry chosen and *local only* are
 * Search's still when the person comes back from Convergence.
 */
describe("Search, as Knowledge's mode", () => {
  beforeEach(() => vi.stubGlobal('fetch', service()));
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it("is Knowledge's list while Search is chosen at its head, the pane named for the place", () => {
    showSearch();
    expect(within(modeChoice()).getByRole('radio', { name: 'Search' })).toHaveAttribute('aria-checked', 'true');
    expect(box()).toBeInTheDocument();
    expect(screen.queryByRole('complementary', { name: 'Search' })).toBeNull();
  });

  it('keeps what was typed, the entry chosen and local only while Convergence is in front, and gives them back', async () => {
    showSearch();
    await userEvent.click(within(knowledgeList()).getByRole('checkbox', { name: "Each repository's own only" }));
    await userEvent.type(box(), 'encoding');
    await chooseRow('An encoding trap');

    await switchTo('Convergence');
    expect(within(knowledgeList()).queryByRole('searchbox')).toBeNull();
    expect(within(mainArea()).getByText('Choose a finding')).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.knowledge.mode')).toBe('convergence');

    await switchTo('Search');
    expect(box()).toHaveValue('encoding');
    expect(within(knowledgeList()).getByRole('checkbox', { name: "Each repository's own only" })).not.toBeChecked();
    expect(await pageTitled('An encoding trap')).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.knowledge.mode')).toBeNull();
  });

  it('opens in the mode it was left in', async () => {
    showSearch();
    await switchTo('Convergence');
    cleanup();
    showKnowledge();
    expect(within(modeChoice()).getByRole('radio', { name: 'Convergence' })).toHaveAttribute('aria-checked', 'true');
  });

  it('closes as the place, and its strip stands for the choice', async () => {
    showSearch();
    await userEvent.click(screen.getByRole('button', { name: 'Hide the result list' }));
    expect(window.localStorage.getItem('daoris.list.knowledge.closed')).toBe('1');
    expect(window.localStorage.getItem('daoris.list.search.closed')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Convergence' }));
    expect(screen.getByRole('button', { name: 'Convergence' })).toHaveAttribute('aria-current', 'true');
    // Still closed: the closing is the place's, so the other mode's list does not open itself.
    expect(screen.getByRole('button', { name: 'Show the finding list' })).toBeInTheDocument();
  });
});
