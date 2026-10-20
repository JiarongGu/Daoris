import { createRef } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { cappedBlocks } from '../../test/measure';
import { Prose } from '../../design/ui';
import { PageHead, PageTabs, ViewMain } from './ViewMain';

// The main area (D118 §3b): props only, since a molecule imports no hook. jsdom lays nothing out, so what
// it holds is the property each state rests on — the container a view's split is measured by, the region
// the keys focus, and what each state draws in place of the page.

const NONE = { headline: 'Nothing chosen', body: 'Choose one in the list and it opens here.', action: <button type="button">New quest</button> };
const GONE = { headline: 'No record of that quest', body: 'It is not in this workspace any more.' };

describe('the main area', () => {
  it('is the container its page lays out by, and the region the keys focus', () => {
    render(<ViewMain><p>the page</p></ViewMain>);
    const main = screen.getByRole('main');
    // Named, so a split inside a card's own container still measures the main area (`@4xl/main:`).
    expect(main).toHaveClass('@container/main');
    expect(main).toHaveAttribute('data-region', 'main');
    // It scrolls by itself, and is the block what is positioned inside it is placed in (PERM1).
    expect(main).toHaveClass('overflow-y-auto', 'relative', 'min-w-0');
    expect(screen.getByText('the page')).toBeInTheDocument();
  });

  it('draws the chosen item under its page header', () => {
    render(<ViewMain header={<h1>Expose a streaming budget</h1>} none={NONE}><p>the record</p></ViewMain>);
    expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(screen.getByText('the record')).toBeInTheDocument();
    expect(screen.queryByText('Nothing chosen')).toBeNull();
  });

  it('says how to choose where nothing is, and offers the list\'s ＋', () => {
    render(<ViewMain state="none" none={NONE}><p>the record</p></ViewMain>);
    expect(screen.getByText('Nothing chosen')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'New quest' })).toBeInTheDocument();
    expect(screen.queryByText('the record')).toBeNull();
  });

  it('draws skeleton rows while the chosen item loads, never the empty state', () => {
    render(<ViewMain state="loading" none={NONE}><p>the record</p></ViewMain>);
    const main = screen.getByRole('main');
    expect(main).toHaveAttribute('aria-busy', 'true');
    expect(main).toHaveAttribute('data-main-state', 'loading');
    expect(screen.queryByText('Nothing chosen')).toBeNull();
    expect(screen.queryByText('the record')).toBeNull();
    expect(main.querySelector('[aria-hidden] i')).not.toBeNull();
  });

  it('says an item has gone, rather than drawing nothing or the empty state', () => {
    render(<ViewMain state="gone" none={NONE} gone={GONE}><p>the record</p></ViewMain>);
    expect(screen.getByText('No record of that quest')).toBeInTheDocument();
    expect(screen.queryByText('Nothing chosen')).toBeNull();
    expect(screen.getByRole('main')).not.toHaveAttribute('aria-busy');
  });

  it('keeps the tighter gutters Sessions uses, as a column its conversation follows', () => {
    const ref = createRef<HTMLElement>();
    render(<ViewMain gutters="session" ref={ref}><p>the conversation</p></ViewMain>);
    expect(ref.current).toBe(screen.getByRole('main'));
    expect(ref.current).toHaveClass('flex', 'flex-col', 'px-4');
  });

  it('caps no content: the page follows the main area (UX5 U59)', () => {
    render(<ViewMain header={<h1>Overview</h1>}><p>the page</p></ViewMain>);
    expect(screen.getByRole('main').className).not.toMatch(/\bmax-w-/);
  });

  /**
   * D141 (LAYOUT11): the main area is the page's one column and the one place a line's length could be set, and it
   * sets none. On the install a quest's title ran the pane while its body stopped at 65ch, with 750 px empty beside it,
   * and a plugin's detail wrapped at two edges: so neither the column nor a block it holds keeps a measure, the header's
   * line and the console's own explanation (`Prose`) included.
   */
  it('is the page\'s one column, and neither it nor a block it holds keeps a measure', () => {
    render(
      <ViewMain
        header={<PageHead title="Expose a streaming budget" id="#19c0de" line="Taken: its receiver is working on it." />}
        gone={GONE}
      >
        <Prose>Nothing on this machine can carry it out yet; the terminal can.</Prose>
        <p className="m-0 whitespace-pre-wrap text-body">The ask itself, as the asker wrote it.</p>
      </ViewMain>,
    );
    const main = screen.getByRole('main');
    expect(cappedBlocks(main)).toEqual([]);
    expect(main.className).not.toMatch(/\bmax-w-/);
  });

  it('caps no block in Sessions\' column either', () => {
    render(<ViewMain gutters="session"><Prose>From before conversations were kept.</Prose></ViewMain>);
    expect(cappedBlocks(screen.getByRole('main'))).toEqual([]);
  });
});

describe("a page's header", () => {
  /**
   * NAME2: a plugin's description, a repository's summary and a quest's state line are content, shown as they
   * are (platform language §4). Cut to one line, the install showed a plugin's description ending in an
   * ellipsis; it wraps whole instead, and at the column's edge as every block of the page does (D141).
   */
  it('shows its line whole, wrapping at the column\'s edge', () => {
    const description = 'Holds every quest that arrives after hours until the morning, then lets the driver start them in the order they came.';
    render(<PageHead title="Acme gate" id="acme.gate" line={description} />);
    const line = screen.getByText(description);
    expect(line).not.toHaveClass('truncate');
    expect(line).toHaveClass('wrap-anywhere');
    expect(line.className).not.toMatch(/\bmax-w-/);
    expect(line).not.toHaveAttribute('title');
  });

  /** HEAD2f (D162 §2): a head with its own door to the whole text offers one way, so the title keeps its tip and drops Show all. */
  it('drops Show all where the head has its own door to the whole text, and keeps the tip', () => {
    const tall = vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(120);
    const fits = vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(60);
    render(<PageHead title="A long title" clamp={2} wholeElsewhere />);
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1 })).toHaveAttribute('title', 'A long title');
    tall.mockRestore();
    fits.mockRestore();
  });

  /**
   * UX7c (D152 §7, the UX7 design §5.1, §5.3): a record whose state moves leads with its state, then its title, at most the
   * lines it is given, whole in its tip; and under them its facts, once, faint. No id line where it is handed none.
   */
  it('leads with its state, clamps its title to the lines given, and says its facts on one line', () => {
    const title = '(Re-filed from ask #39c495, whose quest was taken outside the driver with no session.)';
    render(<PageHead title={title} lead={<span>open</span>} clamp={2} facts={['to engine', 'from ask #f6d947', 'filed 4h ago']} />);

    const heading = screen.getByRole('heading', { level: 1, name: title });
    expect(heading).toHaveClass('line-clamp-2');
    expect(heading).toHaveAttribute('title', title);
    expect(screen.getByText('open').compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getByText('to engine · from ask #f6d947 · filed 4h ago')).toBeInTheDocument();
    expect(screen.getByRole('banner').querySelector('.font-mono')).toBeNull();
  });

  /**
   * RAILLINE1: a session's head keeps its facts to one line, cut with an ellipsis before the door at its end, which is never cut;
   * the line says what it cut in its tip. jsdom lays nothing out, so the classes are what is read.
   */
  it('keeps its facts to one line where asked, whole in its tip, and never cuts the door after them', () => {
    render(
      <PageHead
        title="A session" facts={['engine', '2m', 'running npm test…']} factsOneLine factsTip="engine · 2m · running npm test --all"
        fact={<button type="button">Full access</button>}
      />,
    );
    const said = screen.getByText('engine · 2m · running npm test…');
    expect(said).toHaveClass('truncate', 'min-w-0');
    const line = said.parentElement!;
    expect(line).toHaveClass('flex');
    expect(line).not.toHaveClass('wrap-anywhere');
    expect(line).toHaveAttribute('title', 'engine · 2m · running npm test --all');
    expect(screen.getByRole('button', { name: 'Full access' }).parentElement).toHaveClass('shrink-0');
  });

  /** A title cut at its lines is whole on a press (the UX7 design §5.3), and only where it was cut. */
  it('offers the whole title on a press where it was cut, and nothing where it fits', async () => {
    const tall = vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(120);
    const fits = vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(60);
    const { unmount } = render(<PageHead title="A long title" clamp={2} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Show all' }));
    expect(screen.getByRole('heading', { level: 1 })).not.toHaveClass('line-clamp-2');
    expect(screen.getByRole('button', { name: 'Show less' })).toBeInTheDocument();
    unmount();

    tall.mockReturnValue(60);
    render(<PageHead title="A short one" clamp={2} />);
    expect(screen.queryByRole('button', { name: 'Show all' })).toBeNull();
    tall.mockRestore();
    fits.mockRestore();
  });

  /**
   * HEAD2a (D162 §3, the entry-and-heads design §2): the acts hold still. Above 640 px of the main area the head is a grid of
   * the title's flexible column and the acts' own, the acts at the title's first line; at 640 px or less they take a row
   * under the facts. jsdom lays nothing out, so the container-query classes and the order are what is read.
   */
  it('puts its acts after the title in order, in a column of their own at the wide main area and a row under it at the narrow', () => {
    render(
      <PageHead
        title="A title long enough to wrap under itself and never move the buttons beside it"
        facts={['to engine']}
        acts={<><button type="button">Take</button><button type="button">Hand back</button></>}
        more={<button type="button">More</button>}
      />,
    );
    const header = screen.getByRole('banner');
    expect(header).toHaveClass('grid', '@[40rem]/main:grid-cols-[minmax(0,1fr)_auto]', '@[40rem]/main:items-start');
    const heading = screen.getByRole('heading', { level: 1 });
    const buttons = screen.getAllByRole('button');
    expect(buttons.map((b) => b.textContent)).toEqual(['Take', 'Hand back', 'More']);
    for (const button of buttons) expect(heading.compareDocumentPosition(button) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    const acts = buttons[0]!.parentElement!;
    expect(acts).toContainElement(buttons[2]!);
    expect(acts).toHaveClass('justify-end', '@[40rem]/main:self-start');
    expect(acts).not.toHaveClass('flex-wrap');
  });

  it('keeps an act that folds only above the narrow line, for its caller to say again in the ⋯', () => {
    render(
      <PageHead
        title="A quest"
        acts={<button type="button">Take</button>}
        fold={<button type="button">Decline…</button>}
        more={<button type="button">More</button>}
      />,
    );
    const folded = screen.getByRole('button', { name: 'Decline…' }).parentElement!;
    expect(folded).toHaveClass('hidden', '@[40rem]/main:contents');
    expect(screen.getAllByRole('button').map((b) => b.textContent)).toEqual(['Take', 'Decline…', 'More']);
  });
});

describe("a page's tabs", () => {
  const TABS = [{ id: 'details', label: 'Details' }, { id: 'setup', label: 'Setup' }] as const;

  // UX6f (D150 §4.2): a repository's page is several pages under one header, each a tab, the chosen one its holder's to
  // remember. One tab panel at a time, named by its tab, and the tabs one stop the arrow keys move along.
  it('shows the chosen tab\'s panel, named by its tab, and says which tab is chosen', () => {
    render(<PageTabs label="engine" tabs={TABS} chosen="setup" onChoose={vi.fn()}>the setup</PageTabs>);
    const list = screen.getByRole('tablist', { name: 'engine' });
    expect(within(list).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
    expect(within(list).getByRole('tab', { name: 'Details' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tabpanel', { name: 'Setup' })).toHaveTextContent('the setup');
    // One stop: the chosen tab is where the Tab key lands.
    expect(within(list).getByRole('tab', { name: 'Setup' })).toHaveAttribute('tabindex', '0');
    expect(within(list).getByRole('tab', { name: 'Details' })).toHaveAttribute('tabindex', '-1');
  });

  it('chooses a tab on a press, and with the arrow keys, Home and End', async () => {
    const onChoose = vi.fn();
    render(<PageTabs label="engine" tabs={TABS} chosen="details" onChoose={onChoose}>the details</PageTabs>);
    await userEvent.click(screen.getByRole('tab', { name: 'Setup' }));
    expect(onChoose).toHaveBeenLastCalledWith('setup');
    screen.getByRole('tab', { name: 'Details' }).focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(onChoose).toHaveBeenLastCalledWith('setup');
    await userEvent.keyboard('{ArrowLeft}');
    expect(onChoose).toHaveBeenLastCalledWith('setup');
    await userEvent.keyboard('{End}');
    expect(onChoose).toHaveBeenLastCalledWith('setup');
    await userEvent.keyboard('{Home}');
    expect(onChoose).toHaveBeenLastCalledWith('details');
  });

  // TABS1: a tab is its whole name, never a name cut; a row too narrow for them scrolls rather than cutting one.
  it('keeps each tab\'s whole name on one line', () => {
    render(<PageTabs label="engine" tabs={TABS} chosen="details" onChoose={vi.fn()}>the details</PageTabs>);
    for (const tab of screen.getAllByRole('tab')) {
      expect(tab).toHaveClass('whitespace-nowrap', 'shrink-0');
      expect(tab).not.toHaveClass('truncate');
    }
  });
});
