import { useState } from 'react';
import { afterEach, describe, expect, it } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import type { ListMode } from './layout';
import { type ChoiceStanding, useListPanes } from './listPanes';
import { type ListSpec, ViewFrame } from './ViewFrame';
import { ViewMain } from './ViewMain';

// A browser's frame (D118 §4, amending DOCK1a): the view's list and its main area, which hold nothing
// machine-local, and never the side bar or the panel, which hold this machine's sessions (D47 §4).

const ROWS = ['Expose a streaming budget', 'Read the budget from the level file'];

/**
 * The application's half, as `App` holds it in a browser: the lists' memory and a list laid over. `standings` is what
 * the view reads of each item (UX6b); an item it does not name still waits.
 */
function Browser({ withList = true, onMode, standings = {} }: {
  withList?: boolean;
  onMode?: (mode: ListMode | null) => void;
  standings?: Record<string, ChoiceStanding>;
}) {
  const lists = useListPanes();
  const [over, setOver] = useState(false);
  const chosen = lists.pane('quests').chosen;
  const list: ListSpec = {
    view: 'quests',
    name: 'Quests',
    labels: { open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' },
    chosen,
    standing: chosen ? standings[chosen] ?? 'live' : undefined,
    body: (
      <ul>
        {ROWS.map((title) => (
          <li key={title} data-list-row=""><button type="button" onClick={() => lists.choose('quests', title)}>{title}</button></li>
        ))}
      </ul>
    ),
  };
  return (
    <ViewFrame
      layout={{ list: withList ? list : undefined, main: <ViewMain><h1>{chosen ?? 'Nothing chosen'}</h1></ViewMain> }}
      lists={lists}
      over={over}
      onOver={setOver}
      onListMode={onMode}
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
