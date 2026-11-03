import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// WORKFLOWCLEAN1g-i (the cleanup design §4.3-§4.6): the conversion's review, a molecule: what changes by shape, the two choices, what lowers your
// part, what stays, what it never touches, the terminal line and one Apply; then the done state with its outcomes and Undo.

import i18n from '../../locales/i18n';
import { APPLIED, MOVED, NOTHING, planOf, UNDO_OFFERED, UNDONE } from './convertFixtures';
import { ConvertReview, type ConvertReviewProps } from './ConvertReview';
import { DEFAULT_CHOICES } from './convertPlan';

const show = (props: Partial<ConvertReviewProps> = {}) => {
  const on = { onChoose: vi.fn(), onApply: vi.fn(), onUndo: vi.fn(), onClose: vi.fn() };
  render(
    <ConvertReview
      plan={planOf(DEFAULT_CHOICES)}
      choices={DEFAULT_CHOICES}
      reading={false}
      stale={false}
      problem={null}
      applying={false}
      undoing={false}
      result={null}
      undo={null}
      undone={null}
      {...on}
      {...props}
    />,
  );
  return on;
};

const headings = () => screen.getAllByRole('heading', { level: 3 }).map((each) => each.textContent);

describe('Convert…, the review', () => {
  beforeEach(() => { i18n.changeLanguage('en'); });
  afterEach(cleanup);

  it('reads in its order: what changes, the two choices, what stays, what it never touches, the terminal, then one Apply', () => {
    show();
    expect(headings()).toEqual(['What converting changes', 'Your two choices', 'What stays as it is', 'What it never touches']);
    const apply = screen.getByRole('button', { name: 'Apply' });
    expect(screen.getByText('In a terminal').compareDocumentPosition(apply) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(screen.getAllByRole('button', { name: 'Apply' })).toHaveLength(1);
  });

  it('says each operation once, by shape, with the places it is made at', () => {
    show();
    const changes = screen.getByRole('list', { name: 'What converting changes' });
    expect(within(changes).getAllByText(/gets version 2/)).toHaveLength(1);
    expect(changes.textContent).toContain('merge-after-accept gets version 2: its handed landing becomes land merge with your review (engine, site)');
    expect(within(changes).getByText('Steps')).toBeTruthy();
    expect(within(changes).getByText('Handed')).toBeTruthy();
    expect(changes.textContent).toContain('merge-after-accept becomes the home\'s default workflow');
  });

  it('offers the row\'s two choices and sends the one pressed', async () => {
    const on = show();
    await userEvent.click(screen.getByRole('radio', { name: 'Keep a review' }));
    expect(on.onChoose).toHaveBeenCalledWith({ composed: 'review' });
    await userEvent.click(screen.getByRole('radio', { name: 'With no review, as it was saved' }));
    expect(on.onChoose).toHaveBeenLastCalledWith({ landAutomatic: 'none' });
  });

  it('says what lowers your part only when the choices lower it', () => {
    show();
    expect(headings()).not.toContain('What lowers your part');
    cleanup();
    const lowering = { composed: 'none', landAutomatic: 'none' } as const;
    show({ plan: planOf(lowering), choices: lowering });
    expect(headings()).toContain('What lowers your part');
    expect(screen.getByText(/lands with no review, as it was saved/)).toBeTruthy();
    expect(screen.getByRole('region', { name: 'What lowers your part' })).toBeTruthy();
  });

  it('counts the bound runs as ending on the old engine, and says the leftovers, work cycles and kept', () => {
    show();
    const stays = screen.getByRole('region', { name: 'What stays as it is' });
    expect(within(stays).getByText(/1 bound run ends on the old engine/)).toBeTruthy();
    expect(within(stays).getByText(/a Current unit is still open there/)).toBeTruthy();
    expect(within(stays).getByText(/stays on Current: its quests close/)).toBeTruthy();
  });

  it('shows the terminal line for the plan, with its digest', () => {
    show();
    expect(screen.getByText(/workflow convert --plan/)).toBeTruthy();
    expect(screen.getByText(new RegExp(`workflow convert --apply ${planOf(DEFAULT_CHOICES).digest}`))).toBeTruthy();
    expect(screen.getByText(/workflow convert --undo/)).toBeTruthy();
  });

  it('presses Apply once, and waits while the plan is read or the apply is made', async () => {
    const on = show();
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    expect(on.onApply).toHaveBeenCalledTimes(1);
    cleanup();
    show({ reading: true });
    expect((screen.getByRole('button', { name: 'Apply' }) as HTMLButtonElement).disabled).toBe(true);
    cleanup();
    show({ applying: true });
    expect((screen.getByRole('button', { name: 'Applying…' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('has nothing to apply when the plan writes nothing', () => {
    show({ plan: NOTHING });
    expect((screen.getByRole('button', { name: 'Apply' }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText('Nothing is left to convert.')).toBeTruthy();
  });

  it('says the done state: each operation with its outcome, and Undo while it is offered', async () => {
    const on = show({ plan: NOTHING, result: APPLIED, undo: UNDO_OFFERED });
    const done = screen.getByRole('status');
    expect(done.textContent).toContain('a new version of release: its steps become a workflow of nodes — done');
    expect(done.textContent).toContain('a new workflow site-workflow — failed: the save was refused: a node has no door.');
    await userEvent.click(screen.getByRole('button', { name: 'Undo the conversion' }));
    expect(on.onUndo).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/the landing rule of/)).toBeTruthy();
  });

  it('offers no Undo where the driver offers none', () => {
    show({ plan: NOTHING, result: APPLIED, undo: null });
    expect(screen.queryByRole('button', { name: 'Undo the conversion' })).toBeNull();
  });

  it('says an undone conversion and that its saved versions stay', () => {
    show({ undone: UNDONE });
    expect(screen.getByText(/the conversion is undone; the versions it saved stay saved/)).toBeTruthy();
  });

  it('says a moved plan in the driver\'s words, with nothing written', () => {
    show({ result: MOVED });
    expect(screen.getByText(/the plan you read no longer holds/)).toBeTruthy();
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('tabs through the choices to Apply, which is focusable and disabled while busy', async () => {
    show();
    const user = userEvent.setup();
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Close' }));
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'As it landed, with no review' }));
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole('radio', { name: 'With your review' }));
    await user.tab();
    await user.tab();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Apply' }));
    cleanup();
    show({ applying: true });
    expect((document.querySelector('[data-review-apply]') as HTMLButtonElement).disabled).toBe(true);
  });
});
