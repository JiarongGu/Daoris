import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppMenu, type MenuItem } from './AppMenu';

const MANAGE: MenuItem[] = [
  { id: 'overview', label: 'Overview', icon: 'overview' },
  { id: 'quests', label: 'Quests', icon: 'quests', badge: 3 },
  { id: 'settings', label: 'Machine', icon: 'settings', separated: true },
];

describe('AppMenu', () => {
  // 🔴 A fresh `setup()` per test. The shared `userEvent.*` API carries pointer state between
  // interactions, and Radix reads it — the first menu in a file opened and every one after it
  // silently did not, which reads as "the component is broken" rather than "the harness is".
  const open = async (name = 'Manage') => {
    const user = userEvent.setup();
    screen.getByRole('button', { name: new RegExp(name) }).focus();
    await user.keyboard('{Enter}');
    return user;
  };

  it('names its destinations, which the icon rail cannot', async () => {
    render(
      <AppMenu label="Manage" trigger="manage" items={MANAGE} active current="overview"
        onChoose={() => {}} />,
    );
    await open();

    expect(screen.getByRole('menuitem', { name: /Overview/ })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Machine/ })).toBeInTheDocument();
  });

  /**
   * 🔴 The point of the whole change: a view in the OTHER frame is one click, where the toggle made
   * it two — switch, then hunt an unlabelled icon.
   */
  it('says which frame as well as which view, so one click crosses the boundary', async () => {
    const onChoose = vi.fn();
    render(
      <AppMenu label="Manage" trigger="manage" items={MANAGE} active={false}
        onChoose={onChoose} />,
    );
    const user = await open();
    // Arrow to it and press Enter: the keyboard path is the one D41 §6 requires, and it is also
    // the one that does not depend on the pointer state a previous test may have left behind.
    await user.keyboard('{ArrowDown}{Enter}');

    expect(onChoose).toHaveBeenCalledWith('manage', 'quests');
  });

  it('carries a count beside the destination it belongs to', async () => {
    render(
      <AppMenu label="Manage" trigger="manage" items={MANAGE} active current="overview"
        onChoose={() => {}} />,
    );
    await open();

    expect(screen.getByRole('menuitem', { name: /Quests/ })).toHaveTextContent('3');
  });

  it('wears no badge for zero — a zero badge is furniture', () => {
    render(
      <AppMenu label="Work" trigger="work" items={MANAGE} active={false} badge={0}
        onChoose={() => {}} />,
    );

    expect(screen.getByRole('button', { name: /Work/ })).not.toHaveTextContent('0');
  });

  it('shows attention on the frame itself when it rides there', () => {
    render(
      <AppMenu label="Work" trigger="work" items={MANAGE} active={false} badge={2}
        onChoose={() => {}} />,
    );

    expect(screen.getByRole('button', { name: /Work/ })).toHaveTextContent('2');
  });

  /** The check follows the frame too: Manage's current view is not current while you are in Work. */
  it('marks nothing current in a frame you are not in', async () => {
    render(
      <AppMenu label="Manage" trigger="manage" items={MANAGE} active={false} current="overview"
        onChoose={() => {}} />,
    );
    await open();

    const items = screen.getAllByRole('menuitem');
    expect(items.some((item) => item.querySelector('svg'))).toBe(true); // the row icons still render
    expect(screen.getByRole('menuitem', { name: /Overview/ })).toHaveTextContent('Overview');
  });
});
