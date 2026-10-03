import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { BODIES, CONVERGENT, RESTATEMENT } from './fixtures';
import { FindingMainNotice, FindingPage } from './FindingPage';
import type { EntryReading } from './records';

// A finding's page as a molecule (FRAME1f, D118 §2): the finding and each entry's reading arrive, and nothing is held.

const show = (readings: Record<string, EntryReading>, finding = CONVERGENT) =>
  render(<Tooltip.Provider><FindingPage finding={finding} readings={readings} /></Tooltip.Provider>);

const read = (id: string): EntryReading => ({ state: 'read', entry: BODIES[id]! });

describe("a finding's page", () => {
  /**
   * The service's sentence is the contract (D21, D31): verbatim, its command as code, never a button that applies it;
   * and it already says what kind of finding this is, so no gloss of the UI's sits beside it.
   */
  it("says the service's sentence first, verbatim, and adds no act", () => {
    show({ [CONVERGENT.entries[0]!.id]: read(CONVERGENT.entries[0]!.id), [CONVERGENT.entries[1]!.id]: read(CONVERGENT.entries[1]!.id) });

    expect(screen.getByText(/Two repositories learned this separately/)).toBeInTheDocument();
    expect(screen.getByText('daoris upstream <file>').tagName).toBe('CODE');
    expect(screen.queryByRole('button')).toBeNull();
  });

  /** Each entry is read whole, as it is written, under its own title, kind, provenance and place. */
  it('reads each entry whole under its title, kind, provenance and place', () => {
    show({ [CONVERGENT.entries[0]!.id]: read(CONVERGENT.entries[0]!.id), [CONVERGENT.entries[1]!.id]: read(CONVERGENT.entries[1]!.id) });

    const second = within(screen.getByRole('region', { name: 'world-streaming' }));
    expect(second.getByText(/\*\*chunk hydration\*\* runs neighbours-first/).tagName).toBe('PRE');
    expect(second.getByText('knowledge')).toBeInTheDocument();
    expect(second.getByText('local')).toBeInTheDocument();
    expect(second.getByText('game · .claude/knowledge/world-streaming.md')).toBeInTheDocument();
  });

  it("heads the page with its entries' titles, how alike, who reached it and what kind of likeness", () => {
    show({}, RESTATEMENT);

    const head = screen.getByRole('heading', { level: 1, name: 'engine-mechanics' }).closest('header')!;
    expect(within(head).getByText('0.912')).toBeInTheDocument();
    expect(within(head).getByText('engine ↔ game')).toBeInTheDocument();
    expect(within(head).getByText('Substantially the same words')).toBeInTheDocument();
  });

  /** One entry's trouble never blanks the others: on its way, gone, or failed, each is said in its own place. */
  it("says each entry's own trouble in its place, and reads the rest", () => {
    const [first, second] = CONVERGENT.entries;
    show({ [first!.id]: { state: 'gone' }, [second!.id]: read(second!.id) });
    expect(within(screen.getByRole('region', { name: first!.title })).getByText(/No longer in the index/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'world-streaming' })).getByText(/chunk hydration/, { selector: 'pre' })).toBeInTheDocument();
  });
});

describe("Convergence's main area with no finding", () => {
  it('says how to choose, and that a finding has gone at the similarity asked', () => {
    const { unmount } = render(<FindingMainNotice state="none" />);
    expect(screen.getByText('Choose a finding')).toBeInTheDocument();
    unmount();

    render(<FindingMainNotice state="gone" at={0.82} />);
    expect(screen.getByText('This finding is no longer listed')).toBeInTheDocument();
    expect(screen.getByText(/no longer converge at 0.82 or above/)).toBeInTheDocument();
  });
});

/** CTX1 (D138, design §4): each entry on a finding's page offers its own path on a right-click. */
describe("a finding's entries on a right-click", () => {
  it('offers each entry’s path, in its own section', async () => {
    const copy = vi.fn();
    const [first, second] = CONVERGENT.entries;
    show({ [first!.id]: read(first!.id), [second!.id]: read(second!.id) });
    render(<ContextMenus doors={{ copy }} />);

    rightClick(screen.getByRole('heading', { level: 2, name: second!.title }));
    expect(await menuActs(`Actions for ${second!.title}`)).toEqual(['Copy path']);
    await userEvent.click(screen.getByRole('menuitem', { name: 'Copy path' }));
    expect(copy).toHaveBeenCalledWith(second!.path);
  });
});
