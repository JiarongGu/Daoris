import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import type { Entry } from '../api';
import { EntryMainNotice, EntryPage } from './EntryPage';

// An entry's page (FRAME1f, D118 §2): what the reader drawer showed, in Search's main area. Moved from the reader's
// own suite with it.

const ENTRY = {
  id: 'e1', repository: 'game', kind: 'Knowledge', provenance: 'Local', title: 'world-streaming',
  path: '.claude/knowledge/world-streaming.md',
  body: '# World streaming — chunk hydration order\n\nThe example game\'s own knowledge: **chunk hydration** runs neighbours-first, so a player\nturning the camera never sees an unhydrated seam.',
} as unknown as Entry;

const show = (entry: Entry = ENTRY) => render(<Tooltip.Provider><EntryPage entry={entry} /></Tooltip.Provider>);

describe("an entry's page", () => {
  /**
   * Doctrine is read as it is written: the body is the source, unrendered, so the page gives no
   * second opinion about what the file says.
   */
  it('shows the entry as it is written', () => {
    show();

    const body = screen.getByText(/# World streaming/);
    expect(body.tagName).toBe('PRE');
    expect(body.textContent).toContain('**chunk hydration**');
  });

  /**
   * 🔴 UX5 U43: a source is wrapped near a hundred characters, and the 32rem drawer held about sixty of its monospace,
   * so every line broke once more and the entry read as a zigzag. A wide drawer gave it room; since FRAME1f it is the
   * main area's, beside the list and the side bar rather than under a scrim (D118 §3d), and nothing narrower holds it.
   */
  it("is read in the main area at the area's own width, never in a drawer or at a prose measure", () => {
    show();

    expect(screen.queryByRole('dialog')).toBeNull();
    const body = within(screen.getByRole('main')).getByText(/# World streaming/);
    expect(body.className).not.toMatch(/max-w-/);
    expect(body.parentElement).toBe(screen.getByRole('main'));
  });

  /** Its header says what it is and where it lives: its title, its kind and provenance, its repository and path. */
  it('heads the page with its title, its kind and provenance, and where it lives', () => {
    show();

    const head = screen.getByRole('heading', { level: 1, name: 'world-streaming' }).closest('header')!;
    expect(within(head).getByText('knowledge')).toBeInTheDocument();
    expect(within(head).getByText('local')).toBeInTheDocument();
    expect(within(head).getByText('game · .claude/knowledge/world-streaming.md')).toBeInTheDocument();
  });
});

describe("Search's main area with no entry", () => {
  it('says how to choose with nothing chosen, and that an entry has gone once the index lets it go', () => {
    const { unmount } = render(<EntryMainNotice state="none" />);
    expect(screen.getByText('Choose a result')).toBeInTheDocument();
    unmount();

    render(<EntryMainNotice state="gone" />);
    expect(screen.getByText('This entry is no longer in the index')).toBeInTheDocument();
  });

  /** D118 §3b (audit SE11): a chosen entry on its way is skeleton rows, never the empty state. */
  it('holds the place with skeleton rows while the entry is on its way', () => {
    render(<EntryMainNotice state="loading" />);
    expect(screen.getByRole('main')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByText('Choose a result')).toBeNull();
  });
});

/** CTX1 (D138, design §4): an entry's page offers its path on a right-click. */
describe("an entry's page on a right-click", () => {
  it('offers Copy path', async () => {
    const copy = vi.fn();
    show();
    render(<ContextMenus doors={{ copy }} />);
    rightClick(screen.getByRole('heading', { level: 1, name: 'world-streaming' }));
    expect(await menuActs('Actions for world-streaming')).toEqual(['Copy path']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Copy path' }));
    expect(copy).toHaveBeenCalledWith('.claude/knowledge/world-streaming.md');
  });
});
