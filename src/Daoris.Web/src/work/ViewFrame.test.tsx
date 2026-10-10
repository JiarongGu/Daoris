import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import type { OpenGroup } from '../opener';
import type { ListMode } from './layout';
import { groupHeading, useBringGroup } from './ListPane';
import { type ChoiceStanding, useListPanes } from './listPanes';
import { type ListSpec, ViewFrame } from './ViewFrame';
import { ViewMain } from './ViewMain';

// A browser's frame (D118 §4, amending DOCK1a): the view's list and its main area, which hold nothing
// machine-local, and never the side bar or the panel, which hold this machine's sessions (D47 §4).

const ROWS = ['Expose a streaming budget', 'Read the budget from the level file'];

/**
 * The application's half, as `App` holds it in a browser: the lists' memory and a list laid over. `standings` is what
 * the view reads of each item (UX6b); an item it does not name still waits.
 *
 * A go's group (ENTRY1g) arrives with the view it opens, as `App.apply` sets both at once, and is brought into view as Quests
 * brings it, from above the frame, once its list has `answered`. `view` is Quests, or a view of another list to go from.
 */
function Browser({ withList = true, onMode, standings = {}, view = 'quests', go = null, answered = true, onBrought }: {
  withList?: boolean;
  onMode?: (mode: ListMode | null) => void;
  standings?: Record<string, ChoiceStanding>;
  view?: 'quests' | 'projects';
  go?: OpenGroup | null;
  answered?: boolean;
  onBrought?: () => void;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const [group, setGroup] = useState(go);
  const [went, setWent] = useState(go);
  if (go !== went) {
    setWent(go);
    setGroup(go);
  }
  const brought = () => {
    onBrought?.();
    setGroup(null);
  };
  useBringGroup(view === 'quests' && group ? groupHeading('quests', group) : null, answered, brought);
  const chosen = lists.pane(view).chosen;
  const list: ListSpec = {
    view,
    name: view === 'quests' ? 'Quests' : 'Repositories',
    labels: view === 'quests'
      ? { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' }
      : { open: 'Show the repository list', close: 'Hide the repository list', resize: 'repository list width' },
    chosen,
    standing: chosen ? standings[chosen] ?? 'live' : undefined,
    body: (
      <>
        {view === 'quests' && <h3 id={groupHeading('quests', 'held')} tabIndex={-1}>Waiting on you</h3>}
        <ul>
          {ROWS.map((title) => (
            <li key={title} data-list-row=""><button type="button" onClick={() => lists.choose(view, title)}>{title}</button></li>
          ))}
        </ul>
      </>
    ),
  };
  return (
    <ViewFrame
      layout={{ list: withList ? list : undefined, main: <ViewMain><h1>{chosen ?? 'Nothing chosen'}</h1></ViewMain> }}
      lists={lists}
      over={over}
      onOver={setOver}
      onListMode={onMode}
      group={group}
      onGroupBrought={brought}
    />
  );
}

const widen = (width: number) => act(() => {
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
  window.dispatchEvent(new Event('resize'));
});

const show = (props: Parameters<typeof Browser>[0] = {}) => render(<Tooltip.Provider><Browser {...props} /></Tooltip.Provider>);

afterEach(() => {
  window.localStorage.clear();
  widen(1024);
});

describe("a browser's frame", () => {
  it('draws the view\'s list beside its main area, and no side bar and no panel', () => {
    show();
    expect(screen.getByRole('separator', { name: 'quest list width' })).toBeInTheDocument();
    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull();
  });

  it('opens the chosen item in the main area, and remembers it for the view', async () => {
    show();
    await userEvent.click(screen.getByRole('button', { name: 'Read the budget from the level file' }));
    expect(screen.getByRole('heading', { level: 1, name: 'Read the budget from the level file' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('Read the budget from the level file');
  });

  it('counts no side bar for room: open at 740 px, where a shell beside its closed side bar has none', () => {
    widen(740);
    show();
    expect(screen.getByRole('separator', { name: 'quest list width' })).toBeInTheDocument();
  });

  it('closes as the view\'s own, and lays the list over the main area where the window has no room', async () => {
    const modes: (ListMode | null)[] = [];
    show({ onMode: (mode) => modes.push(mode) });
    await userEvent.click(screen.getByRole('button', { name: 'Hide the quest list' }));
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBe('1');
    await userEvent.click(screen.getByRole('button', { name: 'Show the quest list' }));
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBeNull();

    widen(600);
    await waitFor(() => expect(screen.queryByRole('separator', { name: 'quest list width' })).toBeNull());
    await userEvent.click(screen.getByRole('button', { name: 'Show the quest list' }));
    const over = screen.getByRole('region', { name: 'Quests' });
    await userEvent.click(within(over).getByRole('button', { name: 'Expose a streaming budget' }));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Quests' })).toBeNull());
    // The application was told each mode, so its doors say whether the list is shown.
    expect(modes).toEqual(expect.arrayContaining(['open', 'strip', 'over']));
  });

  it('draws the main area alone for a view with no list', () => {
    const modes: (ListMode | null)[] = [];
    show({ withList: false, onMode: (mode) => modes.push(mode) });
    expect(screen.getByRole('main')).toBeInTheDocument();
    expect(document.querySelector('[data-region="list"]')).toBeNull();
    expect(modes).toEqual([null]);
  });
});

/**
 * ENTRY1g (D161's ENTRY1b note): a go to a group of the list lays it over the main area where it is a strip, whoever drew the
 * strip, so the group is brought into view there; and it stays laid over, as one the person opened does, until they let it
 * go. Nothing is remembered: the person's closing stands once it goes.
 */
describe('a go to a group of the list', () => {
  const mode = () => document.querySelector('[data-region="list"]')?.getAttribute('data-list-mode');

  it('lays a strip the window drew over the main area, focuses the group there, and keeps it laid over', async () => {
    widen(600);
    const brought = vi.fn();
    show({ go: 'held', onBrought: brought });

    const over = screen.getByRole('region', { name: 'Quests' });
    expect(within(over).getByRole('heading', { name: 'Waiting on you' })).toHaveFocus();
    expect(brought).toHaveBeenCalledTimes(1);
    // The go is let go and the list stays, until the person lets it go too.
    await waitFor(() => expect(mode()).toBe('over'));
    await userEvent.keyboard('{Escape}');
    expect(mode()).toBe('strip');
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBeNull();
  });

  it('lays the strip the person closed over, where there is room too, and their closing stands once it goes', () => {
    window.localStorage.setItem('daoris.list.quests.closed', '1');
    show({ go: 'held' });

    expect(mode()).toBe('over');
    expect(screen.getByRole('heading', { name: 'Waiting on you' })).toHaveFocus();
    fireEvent.pointerDown(screen.getByRole('main'));
    expect(mode()).toBe('strip');
    expect(window.localStorage.getItem('daoris.list.quests.closed')).toBe('1');
  });

  it('lets the go go with the list, where the person lets it go before its group is brought', async () => {
    widen(600);
    const brought = vi.fn();
    show({ go: 'held', answered: false, onBrought: brought });

    expect(screen.getByRole('region', { name: 'Quests' })).toHaveFocus();
    await userEvent.keyboard('{Escape}');
    expect(mode()).toBe('strip');
    expect(brought).toHaveBeenCalledTimes(1);
  });

  it('keeps the list laid over as the go opens its view, whatever the view before had chosen', () => {
    widen(600);
    window.localStorage.setItem('daoris.list.projects.chosen', ROWS[1]!);
    window.localStorage.setItem('daoris.list.quests.chosen', ROWS[0]!);
    const { rerender } = show({ view: 'projects' });

    rerender(<Tooltip.Provider><Browser view="quests" go="held" answered={false} /></Tooltip.Provider>);
    expect(mode()).toBe('over');
    rerender(<Tooltip.Provider><Browser view="quests" go="held" /></Tooltip.Provider>);
    expect(screen.getByRole('heading', { name: 'Waiting on you' })).toHaveFocus();
  });

  /** UX6b: a remembered item that closed is let go as the view opens, which is no choice, so the list stays laid over. */
  it('keeps the list laid over as the view lets go of a remembered item that closed', () => {
    widen(600);
    window.localStorage.setItem('daoris.list.quests.chosen', ROWS[1]!);
    const { rerender } = show({ go: 'held', answered: false, standings: { [ROWS[1]!]: 'closed' } });

    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
    expect(mode()).toBe('over');
    rerender(<Tooltip.Provider><Browser go="held" standings={{ [ROWS[1]!]: 'closed' }} /></Tooltip.Provider>);
    expect(screen.getByRole('heading', { name: 'Waiting on you' })).toHaveFocus();
  });
});

// UX6b (design §1 rule 6): the list is told what its chosen item is now, and a remembered one that closed or went is let
// go before the main area shows it. Quests reopened yesterday's done quest on the owner's install.
describe("a view's remembered choice", () => {
  const DONE = ROWS[1]!;

  it('opens with nothing chosen on a remembered item that closed, and forgets it', () => {
    window.localStorage.setItem('daoris.list.quests.chosen', DONE);
    show({ standings: { [DONE]: 'closed' } });
    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it('opens with nothing chosen on a remembered item that went, never on its gone state', () => {
    window.localStorage.setItem('daoris.list.quests.chosen', DONE);
    show({ standings: { [DONE]: 'gone' } });
    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
  });

  it('reopens a remembered item that still waits', () => {
    window.localStorage.setItem('daoris.list.quests.chosen', DONE);
    show();
    expect(screen.getByRole('heading', { level: 1, name: DONE })).toBeInTheDocument();
  });

  it('keeps a remembered item its view has not read yet, and lets it go once read closed', () => {
    window.localStorage.setItem('daoris.list.quests.chosen', DONE);
    const { rerender } = show({ standings: { [DONE]: 'unread' } });
    expect(screen.getByRole('heading', { level: 1, name: DONE })).toBeInTheDocument();
    rerender(<Tooltip.Provider><Browser standings={{ [DONE]: 'closed' }} /></Tooltip.Provider>);
    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
  });

  it('keeps an item the person chooses now, closed or not', async () => {
    show({ standings: { [DONE]: 'closed' } });
    await userEvent.click(screen.getByRole('button', { name: DONE }));
    expect(screen.getByRole('heading', { level: 1, name: DONE })).toBeInTheDocument();
  });
});

/**
 * UX6i (D150 §2.2): Knowledge's pane is one, and the list on it is its mode's. The mode's list keeps its chosen item, so a
 * remembered one that went is let go there; the pane's closing and width are the place's, whichever mode is drawn.
 */
function Moded({ standing }: { standing: ChoiceStanding }) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const chosen = lists.pane('convergence').chosen;
  const list: ListSpec = {
    view: 'knowledge',
    chosenIn: 'convergence',
    name: 'Knowledge',
    labels: { open: 'Show the finding list', close: 'Hide the finding list', resize: 'finding list width' },
    head: <p>the two-way choice</p>,
    chosen,
    standing: chosen ? standing : undefined,
    body: <ul><li data-list-row=""><button type="button">a finding</button></li></ul>,
  };
  return (
    <ViewFrame
      layout={{ list, main: <ViewMain><h1>{chosen ?? 'Nothing chosen'}</h1></ViewMain> }}
      lists={lists}
      over={over}
      onOver={setOver}
    />
  );
}

describe("a pane drawing another list's memory", () => {
  it("lets go of that list's remembered item, and closes as the pane's own", async () => {
    window.localStorage.setItem('daoris.list.convergence.chosen', 'engine:a.md\ngame:a.md');
    render(<Tooltip.Provider><Moded standing="gone" /></Tooltip.Provider>);
    expect(screen.getByRole('main')).toHaveTextContent('Nothing chosen');
    expect(window.localStorage.getItem('daoris.list.convergence.chosen')).toBeNull();
    expect(within(screen.getByRole('complementary', { name: 'Knowledge' })).getByText('the two-way choice')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Hide the finding list' }));
    expect(window.localStorage.getItem('daoris.list.knowledge.closed')).toBe('1');
    expect(window.localStorage.getItem('daoris.list.convergence.closed')).toBeNull();
  });
});
