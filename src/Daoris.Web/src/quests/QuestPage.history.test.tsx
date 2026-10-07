import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { HistoryDoor, HistoryPlan } from '../work/history';
import type { Answered } from '../work/InlineConfirm';
import { FAILED_NONE, FAILED_PLAN, QUEST_ASKED, QUEST_PLAN, QUEST_TREE_HERE } from '../work/historyFixtures';
import { DECLINED, DONE, TAKEN } from './fixtures';
import { QuestPage } from './QuestPage';

// A quest's two clears (HIST1e, D153; the history-clearing design §6.1), as the page draws them from its props: *Clear from
// this machine…* and *Clear failed sessions…* in its header's ⋯, each offered where its plan says something may go and
// absent where it does not, each listed under the header first and then pressed.

const nothing = () => {};

/** A plan of the fixture quest's own, by its id. */
const of = (plan: HistoryPlan, id: string): HistoryPlan => ({
  ...plan, id, units: plan.units.map((unit) => ({ ...unit, id, quests: unit.kind === 'quest' ? [id] : unit.quests })),
});

const door = (over: Partial<HistoryDoor> = {}): HistoryDoor => ({ plan: null, failed: null, busy: false, onClear: vi.fn(), ...over });

/** How a clear's ask hears its end (UXFIX2, ACCTEDIT1's contract). */
const ANSWERED = expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) });

const page = (props: Partial<Parameters<typeof QuestPage>[0]> = {}) => render(
  <QuestPage quest={DONE} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} {...props} />,
);

/** The header's ⋯, opened from the keyboard, as every menu here is in jsdom; its items' names. */
async function more() {
  const user = userEvent.setup();
  screen.getByRole('button', { name: 'More actions' }).focus();
  await user.keyboard('{Enter}');
  return (await screen.findAllByRole('menuitem')).map((item) => item.textContent);
}

describe('a closed quest’s clears (design §6.1)', () => {
  it('offers both in its ⋯ where each plan says something may go, and lists then sends exactly what it listed', async () => {
    const history = door({ plan: of(QUEST_PLAN, DONE.id), failed: of(FAILED_PLAN, DONE.id) });
    page({ history });

    expect(await more()).toEqual(['Clear from this machine…', 'Clear failed sessions…', 'Copy quest ID']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));

    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent(`Clears #${DONE.id}, its 3 sessions and what this machine kept of them`);
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear quest' }));
    expect(history.onClear).toHaveBeenCalledWith({ scope: 'quest', id: DONE.id }, [{ kind: 'quest', id: DONE.id }], ANSWERED);
  });

  it('clears its failed sessions alone, naming the teammate’s it keeps', async () => {
    const history = door({ failed: of(FAILED_PLAN, DECLINED.id) });
    page({ history, quest: DECLINED });

    await more();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Clear failed sessions…' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    expect(ask).toHaveTextContent('The quest and its other sessions stay.');
    expect(ask).toHaveTextContent('laptop/f9e8d7c6 ran on laptop, and its record is theirs: it stays here.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear 2' }));
    expect(history.onClear).toHaveBeenCalledWith({ scope: 'failed', id: DECLINED.id }, [{ kind: 'failed', id: DECLINED.id }], ANSWERED);
  });

  it('offers neither where its plan lists nothing that may go: asked by an ask, a tree still here, no failed session', async () => {
    page({ history: door({ plan: of(QUEST_ASKED, DONE.id), failed: of(FAILED_NONE, DONE.id) }) });
    expect(await more()).toEqual(['Copy quest ID']);
  });

  it('offers neither while the plans are on their way, or on a tree still here', async () => {
    const { unmount } = page({ history: door() });
    expect(await more()).toEqual(['Copy quest ID']);
    unmount();
    page({ history: door({ plan: of(QUEST_TREE_HERE, DONE.id) }) });
    expect(await more()).toEqual(['Copy quest ID']);
  });

  it('offers neither on a quest still in progress, whatever a plan says, nor in a browser', async () => {
    const { unmount } = page({ quest: TAKEN, history: door({ plan: of(QUEST_PLAN, TAKEN.id), failed: of(FAILED_PLAN, TAKEN.id) }) });
    expect(await more()).not.toContain('Clear from this machine…');
    unmount();
    page({ quest: DONE });
    expect(await more()).toEqual(['Copy quest ID']);
  });

  it('puts the list down with never mind, and waits while a clear is on its way', async () => {
    const history = door({ plan: of(QUEST_PLAN, DONE.id) });
    const { rerender } = page({ history });
    await more();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'clear from this machine' })).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'clear from this machine' })).toBeNull();

    await more();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));
    rerender(<QuestPage quest={DONE} onRespond={nothing} onDismiss={nothing} onOpenQuest={nothing} history={{ ...history, busy: true }} />);
    expect(screen.getByRole('button', { name: 'Clear quest' })).toBeDisabled();
  });

  /**
   * UXFIX2 (the second-opinion review, `ClearAsk.tsx:101`): opened from the ⋯, what the clear takes takes the focus and
   * describes the move, so a keyboard reaches *Clear quest* having heard what goes; *Never mind* gives the focus back to the
   * ⋯; the press keeps it open until the clear answers, a refusal said inside it, and it closes once the clear lands.
   */
  it('takes the focus to what goes, gives it back to the ⋯, and stays open until the clear answers', async () => {
    let answered: Answered | undefined;
    const history = door({
      plan: of(QUEST_PLAN, DONE.id),
      onClear: (_target, _units, told) => { answered = told; },
    });
    page({ history });
    const user = userEvent.setup();

    await more();
    await user.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));
    const ask = screen.getByRole('group', { name: 'clear from this machine' });
    const says = ask.querySelector<HTMLElement>('[tabindex="-1"]')!;
    expect(says).toHaveTextContent(`Clears #${DONE.id}, its 3 sessions`);
    await waitFor(() => expect(says).toHaveFocus());
    expect(within(ask).getByRole('button', { name: 'Clear quest' })).toHaveAccessibleDescription(says.textContent!);
    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.getByRole('button', { name: 'More actions' })).toHaveFocus();

    await more();
    await user.click(screen.getByRole('menuitem', { name: 'Clear from this machine…' }));
    await user.click(screen.getByRole('button', { name: 'Clear quest' }));
    expect(screen.getByRole('group', { name: 'clear from this machine' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Never mind' })).toBeDisabled();
    act(() => answered!.refused('s1a2b3c4 is still running, so it was not cleared. Stop it first.'));
    expect(within(screen.getByRole('group', { name: 'clear from this machine' })).getByRole('alert'))
      .toHaveTextContent('s1a2b3c4 is still running, so it was not cleared. Stop it first.');

    await user.click(screen.getByRole('button', { name: 'Clear quest' }));
    act(() => answered!.done());
    expect(screen.queryByRole('group', { name: 'clear from this machine' })).toBeNull();
  });
});
