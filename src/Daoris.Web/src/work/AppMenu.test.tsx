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
    render(<AppMenu label="Manage" trigger="manage" items={MANAGE} onChoose={() => {}} />);
    await open();

    expect(screen.getByRole('menuitem', { name: /Overview/ })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Machine/ })).toBeInTheDocument();
  });

  it('says which menu as well as which item, so the chooser knows what was asked', async () => {
    const onChoose = vi.fn();
    render(<AppMenu label="Manage" trigger="manage" items={MANAGE} onChoose={onChoose} />);
    const user = await open();
    // Arrow to it and press Enter: the keyboard path is the one D41 §6 requires, and it is also
    // the one that does not depend on the pointer state a previous test may have left behind.
    await user.keyboard('{ArrowDown}{Enter}');

    expect(onChoose).toHaveBeenCalledWith('manage', 'quests');
  });

  it('carries a count beside the destination it belongs to', async () => {
    render(<AppMenu label="Manage" trigger="manage" items={MANAGE} onChoose={() => {}} />);
    await open();

    expect(screen.getByRole('menuitem', { name: /Quests/ })).toHaveTextContent('3');
  });

  /** The workspace the window is scoped to wears the check (D75); nothing else does. */
  it('ticks the checked item and no other', async () => {
    const items: MenuItem[] = [
      { id: 'default', label: 'default', checked: true },
      { id: 'studio', label: 'studio' },
    ];
    render(<AppMenu label="Workspace" trigger="workspace" items={items} onChoose={() => {}} />);
    await open('Workspace');

    expect(screen.getByRole('menuitem', { name: /default/ }).querySelector('svg')).not.toBeNull();
    expect(screen.getByRole('menuitem', { name: /studio/ }).querySelector('svg')).toBeNull();
  });
});
