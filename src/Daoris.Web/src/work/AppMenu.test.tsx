import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { AppMenu, AppMenuBar, type BarMenu, focusMenuBar, type MenuItem } from './AppMenu';

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

  /** UX7a (D152 §3.2): each row prints its key, a record's group is named, and an act that does not apply keeps its place. */
  it('prints a row\'s key, names a group, and keeps an act that does not apply in its place, disabled', async () => {
    const items: MenuItem[] = [
      { id: 'start', label: 'Start a session…', shortcut: 'Ctrl+Shift+N' },
      { id: 'stop', label: 'Stop…', disabled: true, separated: true, heading: { label: 'This session' } },
    ];
    render(<Tooltip.Provider><AppMenu label="Run" trigger="run" items={items} onChoose={() => {}} /></Tooltip.Provider>);
    await open('Run');

    expect(screen.getByRole('menuitem', { name: /Start a session/ })).toHaveTextContent('Ctrl+Shift+N');
    expect(screen.getByText('This session')).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Stop/ })).toHaveAttribute('aria-disabled', 'true');
  });

  /** View's *Theme ▸* (D152 §3.3): a row that opens to its side, a radio group ticked as it stands. */
  it('opens a submenu to its side, its choices a radio group with the one in force ticked', async () => {
    const onChoose = vi.fn();
    const items: MenuItem[] = [{
      id: 'view.theme', label: 'Theme', sub: [
        { id: 'view.theme:light', label: 'Light' }, { id: 'view.theme:dark', label: 'Dark', checked: true },
      ],
    }];
    render(<AppMenu label="View" trigger="view" items={items} onChoose={onChoose} />);
    const user = await open('View');
    await user.keyboard('{ArrowDown}{ArrowRight}');

    expect(await screen.findByRole('menuitemradio', { name: 'Dark' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('menuitemradio', { name: 'Light' })).toHaveAttribute('aria-checked', 'false');
    await user.click(screen.getByRole('menuitemradio', { name: 'Light' }));
    expect(onChoose).toHaveBeenCalledWith('view', 'view.theme:light');
  });
});

describe('AppMenuBar (D152 §3.3)', () => {
  const MENUS: BarMenu[] = [
    { id: 'workspace', label: 'Workspace', letter: 'W', items: [{ id: 'a', label: 'New ask…' }] },
    { id: 'edit', label: 'Edit', letter: 'E', items: [{ id: 'b', label: 'Find' }] },
    { id: 'view', label: 'View', letter: 'V', items: [{ id: 'c', label: 'Commands' }] },
  ];

  function Bar({ mnemonics = false, menus = MENUS, onChoose = () => {} }: {
    mnemonics?: boolean; menus?: BarMenu[]; onChoose?: (menu: string, item: string) => void;
  }) {
    const [open, setOpen] = useState<string | null>(null);
    return (
      <>
        <input aria-label="before" />
        <AppMenuBar menus={menus} open={open} onOpen={setOpen} onChoose={onChoose} mnemonics={mnemonics} label="Menu bar" />
      </>
    );
  }

  it('opens the menu under the pointer once one is open, as every Windows menu bar does', async () => {
    render(<Bar />);
    const user = userEvent.setup();
    // Opened from the keyboard, AppMenu's harness for AppMenu's reason; then the pointer moves along the bar.
    screen.getByRole('button', { name: 'Workspace' }).focus();
    await user.keyboard('{Enter}');
    expect(await screen.findByRole('menuitem', { name: 'New ask…' })).toBeInTheDocument();

    await user.hover(screen.getByRole('button', { name: 'Edit' }));
    expect(await screen.findByRole('menuitem', { name: 'Find' })).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('menuitem', { name: 'New ask…' })).toBeNull());
  });

  it('walks the bar with ← and → from inside a menu, wrapping at its ends', async () => {
    render(<Bar />);
    const user = userEvent.setup();
    screen.getByRole('button', { name: 'Workspace' }).focus();
    await user.keyboard('{Enter}');
    await screen.findByRole('menuitem', { name: 'New ask…' });

    await user.keyboard('{ArrowRight}');
    expect(await screen.findByRole('menuitem', { name: 'Find' })).toBeInTheDocument();
    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(await screen.findByRole('menuitem', { name: 'Commands' })).toBeInTheDocument();
  });

  it('moves between the names with ← and → while no menu is open', async () => {
    render(<Bar />);
    const user = userEvent.setup();
    screen.getByRole('button', { name: 'Workspace' }).focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: 'Edit' })).toHaveFocus();
    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(screen.getByRole('button', { name: 'View' })).toHaveFocus();
  });

  it('hands the focus back to where it was on Escape from a name, and after a row is chosen', async () => {
    const onChoose = vi.fn();
    render(<Bar onChoose={onChoose} />);
    const user = userEvent.setup();
    const before = screen.getByRole('textbox', { name: 'before' });
    before.focus();
    act(() => { focusMenuBar(document); });
    expect(screen.getByRole('button', { name: 'Workspace' })).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(before).toHaveFocus();

    act(() => { focusMenuBar(document); });
    await user.keyboard('{Enter}');
    await user.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith('workspace', 'a');
    await waitFor(() => expect(before).toHaveFocus());
  });

  it('shows each menu\'s letter while Alt is held: underlined in a Latin name, after a Chinese one', () => {
    const { rerender, container } = render(<Bar mnemonics />);
    expect(container.querySelector('u')?.textContent).toBe('W');
    expect(screen.getByRole('button', { name: 'Workspace' })).toHaveAttribute('aria-keyshortcuts', 'Alt+W');

    rerender(<Bar mnemonics menus={[{ id: 'workspace', label: '工作区', letter: 'W', items: [] }]} />);
    expect(screen.getByRole('button', { name: '工作区(W)' })).toBeInTheDocument();
  });
});
