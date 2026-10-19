import { describe, expect, it, vi } from 'vitest';
import { useEffect, useState } from 'react';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
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
      { id: 'default', label: 'default', checked: true, radio: true },
      { id: 'studio', label: 'studio', checked: false, radio: true },
    ];
    render(<AppMenu label="Workspace" trigger="workspace" items={items} onChoose={() => {}} />);
    await open('Workspace');

    expect(screen.getByRole('menuitemradio', { name: /default/ }).querySelector('svg')).not.toBeNull();
    expect(screen.getByRole('menuitemradio', { name: /studio/ }).querySelector('svg')).toBeNull();
  });

  /**
   * UXFIX1: a tick is said, not only drawn. A toggle (a region shown) is a checkbox item, a choice among its rows (the
   * place, the workspace) a radio item in one group with the rows it is among, and a door neither; each says whether it
   * is checked, and each is chosen as any row is.
   */
  it('says each tick: a toggle as a checkbox, a choice as a radio among its rows, a door as neither', async () => {
    const onChoose = vi.fn();
    const items: MenuItem[] = [
      { id: 'view.panel', label: 'Panel', checked: true, shortcut: 'Ctrl+J' },
      { id: 'view.side', label: 'Side bar', checked: false },
      { id: 'go.overview', label: 'Overview', checked: false, radio: true, separated: true },
      { id: 'go.quests', label: 'Quests', checked: true, radio: true, badge: 3 },
      { id: 'go.nextRegion', label: 'Next region', separated: true },
    ];
    render(<AppMenu label="View" trigger="view" items={items} onChoose={onChoose} />);
    let user = await open('View');

    const panel = screen.getByRole('menuitemcheckbox', { name: /^Panel/ });
    expect(panel).toHaveAttribute('aria-checked', 'true');
    expect(panel).toHaveTextContent('Ctrl+J');
    expect(panel.querySelector('svg')).not.toBeNull();
    expect(screen.getByRole('menuitemcheckbox', { name: 'Side bar' })).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('menuitemcheckbox', { name: 'Side bar' }).querySelector('svg')).toBeNull();

    const quests = screen.getByRole('menuitemradio', { name: /^Quests/ });
    expect(quests).toHaveAttribute('aria-checked', 'true');
    expect(quests).toHaveTextContent('3');
    expect(screen.getByRole('menuitemradio', { name: 'Overview' })).toHaveAttribute('aria-checked', 'false');
    // One choice: the two places share a group, and nothing else is in it.
    const group = quests.closest<HTMLElement>('[role="group"]')!;
    expect(within(group).getAllByRole('menuitemradio').map((row) => row.textContent)).toEqual(['Overview', 'Quests3']);
    expect(within(group).queryByRole('menuitemcheckbox')).toBeNull();
    expect(within(group).queryByRole('menuitem')).toBeNull();

    const door = screen.getByRole('menuitem', { name: 'Next region' });
    expect(door).not.toHaveAttribute('aria-checked');
    expect(screen.getAllByRole('menuitem')).toEqual([door]);

    // The keys reach each kind in order, and choosing one says which.
    await user.keyboard('{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenLastCalledWith('view', 'view.side');
    user = await open('View');
    await user.keyboard('{ArrowDown}{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenLastCalledWith('view', 'go.overview');
    user = await open('View');
    await user.keyboard('{End}{Enter}');
    expect(onChoose).toHaveBeenLastCalledWith('view', 'go.nextRegion');
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

  function Bar({ mnemonics = false, menus = MENUS, onChoose = () => {}, compact = false }: {
    mnemonics?: boolean; menus?: BarMenu[]; onChoose?: (menu: string, item: string) => void; compact?: boolean;
  }) {
    const [open, setOpen] = useState<string | null>(null);
    return (
      <Tooltip.Provider>
        <input aria-label="before" />
        <AppMenuBar
          menus={menus} open={open} onOpen={setOpen} onChoose={onChoose} mnemonics={mnemonics} label="Menu bar"
          compact={compact} foldLabel="Menu"
        />
      </Tooltip.Provider>
    );
  }

  /** UXFIX1: View's regions are toggles and Go's places one choice; the bar's keys treat those rows as any other. */
  const TICKED: BarMenu[] = [
    { id: 'view', label: 'View', letter: 'V', items: [{ id: 'view.panel', label: 'Panel', checked: true }] },
    { id: 'go', label: 'Go', letter: 'G', items: [{ id: 'go.quests', label: 'Quests', checked: true, radio: true }] },
  ];

  it('walks the bar with ← and → from a ticked row, and hands the focus back once one is chosen (UXFIX1)', async () => {
    const onChoose = vi.fn();
    render(<Bar menus={TICKED} onChoose={onChoose} />);
    const user = userEvent.setup();
    const before = screen.getByRole('textbox', { name: 'before' });
    before.focus();
    act(() => { focusMenuBar(document); });
    await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('menuitemcheckbox', { name: 'Panel' })).toHaveFocus());

    await user.keyboard('{ArrowRight}');
    expect(await screen.findByRole('menuitemradio', { name: 'Quests' })).toHaveAttribute('aria-checked', 'true');
    await waitFor(() => expect(screen.queryByRole('menuitemcheckbox', { name: 'Panel' })).toBeNull());
    await user.keyboard('{ArrowLeft}');
    expect(await screen.findByRole('menuitemcheckbox', { name: 'Panel' })).toHaveAttribute('aria-checked', 'true');

    await waitFor(() => expect(screen.getByRole('menuitemcheckbox', { name: 'Panel' })).toHaveFocus());
    await user.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith('view', 'view.panel');
    await waitFor(() => expect(before).toHaveFocus());
  });

  it('says the same ticks folded, where each menu\'s rows open to its side (UXFIX1)', async () => {
    render(<Bar menus={TICKED} compact />);
    const user = userEvent.setup();
    within(screen.getByRole('navigation', { name: 'Menu bar' })).getByRole('button', { name: 'Menu' }).focus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'View' })).toHaveFocus());
    await user.keyboard('{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitemcheckbox', { name: 'Panel' })).toHaveFocus());
    expect(screen.getByRole('menuitemcheckbox', { name: 'Panel' })).toHaveAttribute('aria-checked', 'true');
    await user.keyboard('{ArrowLeft}{ArrowDown}{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitemradio', { name: 'Quests' })).toHaveFocus());
    expect(screen.getByRole('menuitemradio', { name: 'Quests' })).toHaveAttribute('aria-checked', 'true');
  });

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

  /** UX7a2: a menu Alt opened from a field, not through a name, still hands the focus back to that field. */
  it('hands the focus back to the field a menu was opened from without its name', async () => {
    const onChoose = vi.fn();
    render(<AltBar onChoose={onChoose} />);
    const user = userEvent.setup();
    const before = screen.getByRole('textbox', { name: 'before' });
    before.focus();
    fireEvent.keyDown(before, { key: 'e', code: 'KeyE', altKey: true });
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Find' })).toHaveFocus());
    await user.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith('edit', 'b');
    await waitFor(() => expect(before).toHaveFocus());
  });
});

/**
 * A bar whose menus Alt opens by their letter, as the window's key handler does (`App`): the key lands where the focus
 * is, which is how Radix learns the menu was opened from the keyboard.
 */
function AltBar({ compact = false, mnemonics = false, onChoose = () => {} }: {
  compact?: boolean; mnemonics?: boolean; onChoose?: (menu: string, item: string) => void;
}) {
  const [open, setOpen] = useState<string | null>(null);
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const menu = FOLD_MENUS.find((each) => event.altKey && event.code === `Key${each.letter}`);
      if (menu) setOpen(menu.id);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);
  return (
    <Tooltip.Provider>
      <input aria-label="before" />
      <AppMenuBar
        menus={FOLD_MENUS} open={open} onOpen={setOpen} onChoose={onChoose} mnemonics={mnemonics} label="Menu bar"
        compact={compact} foldLabel="Menu"
      />
    </Tooltip.Provider>
  );
}

const FOLD_MENUS: BarMenu[] = [
  { id: 'workspace', label: 'Workspace', letter: 'W', items: [{ id: 'a', label: 'New ask…' }] },
  { id: 'edit', label: 'Edit', letter: 'E', items: [{ id: 'b', label: 'Find' }, { id: 'b2', label: 'Copy ID' }] },
  {
    id: 'view', label: 'View', letter: 'V', items: [
      { id: 'c', label: 'Commands' },
      { id: 'view.theme', label: 'Theme', sub: [{ id: 'view.theme:light', label: 'Light', checked: true }, { id: 'view.theme:dark', label: 'Dark' }] },
    ],
  },
];

/**
 * UX7a2 (D152's UX7a note, the design §3.3): below the width the seven names need, they fold into one menu whose rows are
 * the menus, each opening its own rows to its side, and the keys still reach every row.
 */
describe('AppMenuBar folded into one menu (UX7a2)', () => {
  const fold = () => within(screen.getByRole('navigation', { name: 'Menu bar' })).getByRole('button', { name: 'Menu' });
  const openFold = async () => {
    const user = userEvent.setup();
    fold().focus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Workspace' })).toHaveFocus());
    return user;
  };

  it('is one named button on the bar, whose menu\'s rows are the menus in order', async () => {
    render(<AltBar compact />);
    expect(within(screen.getByRole('navigation', { name: 'Menu bar' })).getAllByRole('button')).toHaveLength(1);
    await openFold();
    const rows = screen.getAllByRole('menuitem');
    expect(rows.map((row) => row.textContent)).toEqual(['Workspace', 'Edit', 'View']);
    for (const row of rows) expect(row).toHaveAttribute('aria-haspopup', 'menu');
  });

  it('walks the menus with ↓, opens one with → onto its first row, and goes back to its name with ←', async () => {
    render(<AltBar compact />);
    const user = await openFold();
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Edit' })).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Find' })).toHaveFocus());
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('menuitem', { name: 'Copy ID' })).toHaveFocus();
    await user.keyboard('{ArrowLeft}');
    await waitFor(() => expect(screen.queryByRole('menuitem', { name: 'Find' })).toBeNull());
    expect(screen.getByRole('menuitem', { name: 'Edit' })).toHaveFocus();
  });

  it('reaches a submenu inside a menu, View\'s Theme, and chooses in it', async () => {
    const onChoose = vi.fn();
    render(<AltBar compact onChoose={onChoose} />);
    const user = await openFold();
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Commands' })).toHaveFocus());
    await user.keyboard('{ArrowDown}{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitemradio', { name: 'Light' })).toHaveFocus());
    await user.keyboard('{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenCalledWith('view', 'view.theme:dark');
  });

  it('opens the menu Alt names inside the fold, and hands the focus back once a row is chosen', async () => {
    const onChoose = vi.fn();
    render(<AltBar compact onChoose={onChoose} />);
    const user = userEvent.setup();
    const before = screen.getByRole('textbox', { name: 'before' });
    before.focus();
    fireEvent.keyDown(before, { key: 'e', code: 'KeyE', altKey: true });
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Find' })).toHaveFocus());
    expect(screen.getByRole('menuitem', { name: 'Edit' })).toHaveAttribute('aria-expanded', 'true');

    await user.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith('edit', 'b');
    await waitFor(() => expect(before).toHaveFocus());
    expect(screen.queryByRole('menuitem')).toBeNull();
  });

  it('closes onto the fold on Escape, and Escape on the fold hands the focus back to where it was', async () => {
    render(<AltBar compact />);
    const user = userEvent.setup();
    const before = screen.getByRole('textbox', { name: 'before' });
    before.focus();
    act(() => { focusMenuBar(document); });
    expect(fold()).toHaveFocus();
    await user.keyboard('{Enter}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'Workspace' })).toHaveFocus());
    await user.keyboard('{ArrowRight}');
    await waitFor(() => expect(screen.getByRole('menuitem', { name: 'New ask…' })).toHaveFocus());

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menuitem')).toBeNull());
    expect(fold()).toHaveFocus();
    await user.keyboard('{Escape}');
    expect(before).toHaveFocus();
  });

  it('shows each menu\'s letter on its row while Alt is held, and names its key', async () => {
    render(<AltBar compact mnemonics />);
    await openFold();
    const workspace = screen.getByRole('menuitem', { name: 'Workspace' });
    expect(workspace.querySelector('u')?.textContent).toBe('W');
    expect(workspace).toHaveAttribute('aria-keyshortcuts', 'Alt+W');
  });
});

/**
 * UXFIX1: a menu's name and the fold's ☰ are each a target of 28 px at least, the platform language's floor (§6), and no
 * taller than the 36 px strip they sit in (`AppStrip`'s `h-9`). At `py-1` alone the ☰ was about 23 px tall.
 */
describe('the bar\'s triggers as targets', () => {
  /** A Tailwind spacing class's size in px, on the 4 px step. */
  const px = (className: string, prefix: string) => {
    const step = new RegExp(`(?:^|\\s)${prefix}-(\\d+(?:\\.\\d+)?)(?:\\s|$)`).exec(className)?.[1];
    return step === undefined ? undefined : Number(step) * 4;
  };
  const triggers = () => within(screen.getByRole('navigation', { name: 'Menu bar' })).getAllByRole('button');

  it('gives every name on the bar a box of 28 px at least, within the strip', () => {
    render(<AltBar />);
    expect(triggers()).toHaveLength(3);
    for (const name of triggers()) {
      expect(px(name.className, 'min-h'), name.textContent ?? '').toBeGreaterThanOrEqual(28);
      expect(px(name.className, 'min-h'), name.textContent ?? '').toBeLessThanOrEqual(36);
      expect(px(name.className, 'min-w'), name.textContent ?? '').toBeGreaterThanOrEqual(28);
    }
  });

  it('gives the fold\'s ☰ the same box', () => {
    render(<AltBar compact />);
    const [fold] = triggers();
    expect(fold).toHaveAccessibleName('Menu');
    expect(px(fold!.className, 'min-h')).toBeGreaterThanOrEqual(28);
    expect(px(fold!.className, 'min-h')).toBeLessThanOrEqual(36);
    expect(px(fold!.className, 'min-w')).toBeGreaterThanOrEqual(28);
  });
});
