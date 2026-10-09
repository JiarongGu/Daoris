import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import type { Command } from '../commands';
import { CommandPalette } from './CommandPalette';

// The widget half of SURF9. What is IN the list is the registry's job and is tested there; this is
// about the keyboard, the selection, and the promise that pressing Enter runs the thing you can see.

const command = (id: string, title: string, run = vi.fn()): Command =>
  ({ id, title, group: 'go to', icon: 'overview', run });

const LIST = [
  command('go.overview', 'Overview'),
  command('go.quests', 'Quests'),
  command('go.search', 'Search'),
];

const show = (commands: Command[] = LIST) => {
  const onClose = vi.fn();
  render(<CommandPalette open commands={commands} onClose={onClose} />);
  return { onClose };
};

describe('the command palette', () => {
  it('shows everything before anything is typed, and focuses where you type', async () => {
    show();
    expect(screen.getAllByRole('option')).toHaveLength(3);
    expect(document.activeElement).toBe(screen.getByRole('combobox', { name: 'Commands' }));
  });

  it('narrows as you type, by subsequence', async () => {
    show();
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), 'qs');

    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0].textContent).toContain('Quests');
  });

  it('says so when nothing matches, rather than showing an empty box', async () => {
    show();
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), 'zzzq');

    expect(screen.queryAllByRole('option')).toHaveLength(0);
    expect(screen.getByText(/Nothing matches/)).toBeTruthy();
    expect(screen.getByText(/zzzq/)).toBeTruthy();
  });

  it('runs the selected command on Enter and closes first', async () => {
    const run = vi.fn();
    const { onClose } = show([command('go.overview', 'Overview', run)]);

    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), '{Enter}');

    expect(run).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
    // Closed BEFORE running, so a command that opens a drawer does not open it under the palette.
    expect(onClose.mock.invocationCallOrder[0]).toBeLessThan(run.mock.invocationCallOrder[0]);
  });

  // IME1: a question typed through an input method is composed, and accepting its candidate runs nothing, moves
  // nothing and closes nothing.
  it('leaves a composing press to the input method: no run, no move, no close', () => {
    const quests = command('go.quests', 'Quests');
    const { onClose } = show([command('go.overview', 'Overview'), quests]);
    const field = screen.getByRole('combobox', { name: 'Commands' });

    expect(fireEvent.keyDown(field, { key: 'ArrowDown', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Enter', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Enter', keyCode: 229 })).toBe(true);
    // The box's Escape is Radix's, heard on the document first, and prevented is the one way it leaves the box open.
    fireEvent.keyDown(field, { key: 'Escape', isComposing: true });
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByRole('option', { name: /Overview/ })).toHaveAttribute('aria-selected', 'true');

    fireEvent.keyDown(field, { key: 'ArrowDown' });
    fireEvent.keyDown(field, { key: 'Enter' });
    expect(quests.run).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('moves with the arrow keys and wraps, so the list has no dead ends', async () => {
    show();
    const input = screen.getByRole('combobox', { name: 'Commands' });

    expect(screen.getAllByRole('option')[0].getAttribute('aria-selected')).toBe('true');

    await userEvent.type(input, '{ArrowDown}');
    expect(screen.getAllByRole('option')[1].getAttribute('aria-selected')).toBe('true');

    // Up from the first wraps to the last.
    await userEvent.type(input, '{ArrowUp}{ArrowUp}');
    expect(screen.getAllByRole('option')[2].getAttribute('aria-selected')).toBe('true');
  });

  /**
   * The selection has to survive narrowing. Typing past the end of a shorter list and pressing Enter
   * would otherwise run whatever happened to be at a stale index — or nothing.
   */
  it('keeps the selection inside the list when typing narrows it', async () => {
    const run = vi.fn();
    show([LIST[0], LIST[1], command('go.search', 'Search', run)]);
    const input = screen.getByRole('combobox', { name: 'Commands' });

    await userEvent.type(input, '{ArrowDown}{ArrowDown}');
    await userEvent.type(input, 'sea');

    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0].getAttribute('aria-selected')).toBe('true');

    await userEvent.type(input, '{Enter}');
    expect(run).toHaveBeenCalledTimes(1);
  });

  it('runs what was clicked', async () => {
    const run = vi.fn();
    show([LIST[0], command('go.quests', 'Quests', run)]);

    await userEvent.click(screen.getByRole('option', { name: /Quests/ }));
    expect(run).toHaveBeenCalledTimes(1);
  });

  /** Keyboard and pointer must agree: what Enter runs is what the cursor is over. */
  it('follows the pointer, so Enter runs what the cursor is on', async () => {
    const run = vi.fn();
    show([LIST[0], command('go.quests', 'Quests', run)]);

    await userEvent.hover(screen.getByRole('option', { name: /Quests/ }));
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), '{Enter}');

    expect(run).toHaveBeenCalledTimes(1);
  });

  it('is a dialog a screen reader can follow (D41 §6)', () => {
    show();
    const dialog = screen.getByRole('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');

    const input = screen.getByRole('combobox', { name: 'Commands' });
    // The active option is announced as the arrows move it, which a plain list never does.
    expect(input.getAttribute('aria-activedescendant')).toBe('palette-go.overview');
  });

  // DOCK1d: the command center's one question — what is typed can be asked of Ask Daoris.
  it('offers to ask Ask Daoris what was typed, last and set apart, and asks on Enter', async () => {
    const onAsk = vi.fn();
    const onClose = vi.fn();
    render(<CommandPalette open commands={LIST} onClose={onClose} onAsk={onAsk} />);
    const input = screen.getByRole('combobox', { name: 'Commands' });
    // Nothing typed, nothing to ask.
    expect(screen.getAllByRole('option')).toHaveLength(3);

    await userEvent.type(input, 'why is engine held?');
    // Nothing matched, so it says so, and the question is still one Enter away.
    expect(screen.getByText(/Nothing matches/)).toBeTruthy();
    expect(screen.getByRole('option', { name: /Ask Daoris: “why is engine held\?”/ })).toHaveAttribute('aria-selected', 'true');

    await userEvent.type(input, '{Enter}');
    expect(onClose).toHaveBeenCalled();
    expect(onAsk).toHaveBeenCalledWith('why is engine held?');
  });

  it('keeps the commands first when they match, with the question after them', async () => {
    const onAsk = vi.fn();
    render(<CommandPalette open commands={LIST} onClose={() => {}} onAsk={onAsk} />);
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), 'quests');

    const options = screen.getAllByRole('option');
    expect(options.map((option) => option.textContent)).toEqual(['Questsgo to', 'Ask Daoris: “quests”Ask Daoris']);
    expect(options[0]).toHaveAttribute('aria-selected', 'true');
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), '{ArrowDown}{Enter}');
    expect(onAsk).toHaveBeenCalledWith('quests');
  });

  it('starts fresh every time it opens — a palette you must clear is one you avoid', async () => {
    const { rerender } = render(<CommandPalette open commands={LIST} onClose={() => {}} />);
    await userEvent.type(screen.getByRole('combobox', { name: 'Commands' }), 'quests');
    expect(screen.getAllByRole('option')).toHaveLength(1);

    rerender(<CommandPalette open={false} commands={LIST} onClose={() => {}} />);
    rerender(<CommandPalette open commands={LIST} onClose={() => {}} />);

    expect(screen.getAllByRole('option')).toHaveLength(3);
  });
});
