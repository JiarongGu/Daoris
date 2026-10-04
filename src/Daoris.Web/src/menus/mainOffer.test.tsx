import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ViewMain } from '../work/ViewMain';
import { QuestPage } from '../quests/QuestPage';
import { OPEN, TAKEN } from '../quests/fixtures';
import { MainOffer, offerStore, useMainOffer } from './mainOffer';
import type { ContextOffer } from './press';

// The record in the main area, as the menu bar reads it (UX7a, D152 §2): what its page offers a right-click is what the
// Run menu's record group runs, by the page's own owner, so a menu row is enabled exactly when the header offers it.

const offer = (ids: string[], label = 'A quest'): ContextOffer => ({ label, acts: ids.map((id) => ({ id, label: id, onSelect: vi.fn() })) });

describe('the main area\'s offer', () => {
  it('is what the page in front offers while it shows its record, and nothing once it goes', () => {
    const store = offerStore();
    const { rerender, unmount } = render(
      <MainOffer.Provider value={store}><ViewMain menu={offer(['take', 'decline'])}>a page</ViewMain></MainOffer.Provider>,
    );
    expect(store.get()?.acts.map((act) => act.id)).toEqual(['take', 'decline']);

    // A page with nothing chosen offers nothing, as its right-click does.
    rerender(<MainOffer.Provider value={store}><ViewMain state="none" menu={offer(['take'])}>a page</ViewMain></MainOffer.Provider>);
    expect(store.get()).toBeNull();

    rerender(<MainOffer.Provider value={store}><ViewMain menu={offer(['done'])}>a page</ViewMain></MainOffer.Provider>);
    unmount();
    expect(store.get()).toBeNull();
  });

  it('runs an offered act by the page\'s own press, or copies what it copies, and nothing that is off', () => {
    const copy = vi.fn();
    const store = offerStore(copy);
    const take = vi.fn();
    render(
      <MainOffer.Provider value={store}>
        <ViewMain menu={{
          acts: [
            { id: 'take', label: 'Take', onSelect: take },
            { id: 'done', label: 'Mark done', onSelect: vi.fn(), disabled: true },
            { id: 'copy', label: 'Copy quest ID', copy: 'abc123' },
          ],
        }}
        >a page</ViewMain>
      </MainOffer.Provider>,
    );
    expect(store.run('take')).toBe(true);
    expect(take).toHaveBeenCalledOnce();
    expect(store.run('done')).toBe(false);
    expect(store.run('copy')).toBe(true);
    expect(copy).toHaveBeenCalledWith('abc123');
    expect(store.run('decline')).toBe(false);
  });

  it('tells its reader when what is offered changes, and only then', () => {
    const store = offerStore();
    const seen = vi.fn();
    function Reader() {
      const now = useMainOffer(store);
      seen(now?.acts.map((act) => act.id).join(',') ?? 'none');
      return null;
    }
    const page = (ids: string[]) => (
      <MainOffer.Provider value={store}><Reader /><ViewMain menu={offer(ids)}>a page</ViewMain></MainOffer.Provider>
    );
    const { rerender } = render(page(['take']));
    const before = seen.mock.calls.length;
    // The same acts drawn again, as every render of a page draws its menu anew: no news.
    rerender(page(['take']));
    rerender(page(['take']));
    expect(seen.mock.calls.slice(before).map((call) => call[0]).every((value) => value === 'take')).toBe(true);
    act(() => { rerender(page(['done', 'decline'])); });
    expect(seen).toHaveBeenLastCalledWith('done,decline');
  });

  it('is nothing outside a window that reads it: a story or the monitor publishes to no one', () => {
    render(<ViewMain menu={offer(['take'])}>a page</ViewMain>);
    expect(screen.getByText('a page')).toBeInTheDocument();
  });
});

/**
 * D152 §2: a record's act in the menu is enabled exactly when the record's header offers it. The quest page's header is
 * its buttons; its offer is what the Run menu's *This quest* reads.
 */
describe('a quest\'s header and its offer', () => {
  const nothing = () => {};
  const page = (quest: typeof OPEN, store = offerStore()) => {
    render(
      <Tooltip.Provider>
        <MainOffer.Provider value={store}>
          <QuestPage quest={quest} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} />
        </MainOffer.Provider>
      </Tooltip.Provider>,
    );
    return store;
  };

  it.each([['open', OPEN], ['taken', TAKEN]])('offers a %s quest\'s header acts, and no other', (_, quest) => {
    const store = page(quest);
    // A header inside the main area is no landmark, so it is found by its place.
    const header = within(screen.getByRole('main').querySelector('header')!);
    const buttons = header.getAllByRole('button').map((button) => button.textContent);
    const offered = store.get()!.acts.filter((act) => act.id !== 'copy').map((act) => act.label);
    expect(offered).toEqual(buttons);
  });
});
