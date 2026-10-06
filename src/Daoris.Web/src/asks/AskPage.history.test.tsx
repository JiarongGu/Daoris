import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Ask } from '../api';
import type { HistoryDoor, HistoryPlan } from '../work/history';
import { ASK_OPEN, ASK_PLAN } from '../work/historyFixtures';
import { AskPage } from './AskPage';
import { CLOSED, DONE, PUBLISHED } from './fixtures';

// An ask's clear (HIST1e, D153; the history-clearing design §6.1), as the page draws it from its props: *Clear from this
// machine…* in its header's ⋯ once every quest of its work is closed and its plan says it may go, listed under the header
// first and then pressed, whole or not at all.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** A plan of the fixture ask's own, by its id. */
const of = (plan: HistoryPlan, id: string): HistoryPlan => ({
  ...plan, id, units: plan.units.map((unit) => ({ ...unit, id, asks: unit.clearable ? [id] : unit.asks })),
});

const door = (over: Partial<HistoryDoor> = {}): HistoryDoor => ({ plan: null, busy: false, onClear: vi.fn(), ...over });

const page = (ask: Ask, history?: HistoryDoor) => render(
  <AskPage
    ask={ask} receivers={['engine']} questTitles={{}} onPublish={vi.fn()} onClose={vi.fn()} onOpenQuest={vi.fn()} history={history}
  />,
);

/** The header's ⋯, opened from the keyboard, as every menu here is in jsdom; its items' names. */
async function more() {
  const user = userEvent.setup();
  screen.getByRole('button', { name: 'More actions' }).focus();
  await user.keyboard('{Enter}');
  return (await screen.findAllByRole('menuitem')).map((item) => item.textContent);
}

describe('an ask’s clear (design §6.1)', () => {
  it('offers it in its ⋯ once its work closed, lists its work, and sends exactly the ask it listed', async () => {
    const history = door({ plan: of(ASK_PLAN, DONE.id) });
    page(DONE, history);

    expect(await more()).toEqual(['Clear from this machine…']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent(`Clears ask #${DONE.id}, the 2 quests it became, their 5 sessions`);
    expect(ask).toHaveTextContent('Nothing brings it back.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear ask' }));
    expect(history.onClear).toHaveBeenCalledWith({ scope: 'ask', id: DONE.id }, [{ kind: 'ask', id: DONE.id }], expect.any(Function));
  });

  it('offers it on an ask closed by the person, as its plan says', async () => {
    page(CLOSED, door({ plan: of(ASK_PLAN, CLOSED.id) }));
    expect(await more()).toEqual(['Clear from this machine…']);
  });

  it('offers nothing, and draws no ⋯, where the plan keeps it, while it is asked, on a live ask, or in a browser', () => {
    const { unmount } = page(DONE, door({ plan: of(ASK_OPEN, DONE.id) }));
    expect(screen.queryByRole('button', { name: 'More actions' })).toBeNull();
    unmount();
    const asked = page(DONE, door());
    expect(screen.queryByRole('button', { name: 'More actions' })).toBeNull();
    asked.unmount();
    const live = page(PUBLISHED, door({ plan: of(ASK_PLAN, PUBLISHED.id) }));
    expect(screen.queryByRole('button', { name: 'More actions' })).toBeNull();
    live.unmount();
    page(DONE);
    expect(screen.queryByRole('button', { name: 'More actions' })).toBeNull();
  });
});
