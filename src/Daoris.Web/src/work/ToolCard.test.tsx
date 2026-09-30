import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import type { Block } from './conversation';
import { FileOpener, type FileOpen } from './preview';
import { ToolCard } from './ToolCard';

// A tool card's file as a door into the side bar's preview (PREVIEW1, D111). A molecule: the opener is a
// context the Work frame provides, so each case is a card and an opener passed in.

const TREE = 'C:/somewhere/engine';
const at = '2026-09-30T10:00:00Z';

const READ: Block = {
  key: '1', kind: 'tool', at, title: `Read ${TREE}/src/chunk.ts`, toolKind: 'read', status: 'completed',
  locations: [`${TREE}/src/chunk.ts`],
  input: JSON.stringify({ file_path: `${TREE}/src/chunk.ts`, offset: 10, limit: 10 }),
  output: 'export const a = 1;',
};

const EDIT: Block = {
  key: '2', kind: 'tool', at, title: 'Edit a file', toolKind: 'edit', status: 'failed',
  content: [{ type: 'diff', path: `${TREE}/src/world.ts`, oldText: 'let cap = 0;', newText: 'let cap = 4;' }],
};

function show(call: Block, open?: (file: FileOpen) => void, tree: string | null = TREE) {
  const node: ReactNode = <ToolCard call={call} tree={tree} />;
  return render(open ? <FileOpener.Provider value={open}>{node}</FileOpener.Provider> : node);
}

describe('a tool card\'s file', () => {
  it('is text where nothing opens previews, and the card is one row that opens as before', async () => {
    show(READ);

    expect(screen.queryByRole('button', { name: /^preview / })).toBeNull();
    const row = screen.getByRole('button', { expanded: false });
    expect(row).toHaveTextContent('Read src/chunk.ts');
    await userEvent.click(row);
    expect(screen.getByText('export const a = 1;')).toBeInTheDocument();
  });

  /** A read's lines are its own input's offset and limit: the preview marks them. */
  it('opens the preview on the file a read named, with the lines it read, and leaves the card closed', async () => {
    const open = vi.fn();
    show(READ, open);

    // The title keeps its verb, and the path beside it is the door — named once, not twice.
    expect(screen.getByRole('button', { expanded: false })).toHaveAccessibleName(`Read src/chunk.ts`);
    await userEvent.click(screen.getByRole('button', { name: 'preview src/chunk.ts, lines 10–19' }));

    expect(open).toHaveBeenCalledWith({ path: 'src/chunk.ts', lines: { from: 10, to: 19 } });
    expect(screen.getByRole('button', { expanded: false })).toBeInTheDocument();
    expect(screen.getAllByText('src/chunk.ts')).toHaveLength(1);
  });

  it('opens an edit\'s file from where the card names it, with no lines, since an edit names none', async () => {
    const open = vi.fn();
    show(EDIT, open);

    // A failed call is open: the file its edit touched is in its body.
    await userEvent.click(screen.getByRole('button', { name: 'preview src/world.ts' }));
    expect(open).toHaveBeenCalledWith({ path: 'src/world.ts', lines: null });
  });

  /** A door that could only refuse is not offered (UX5 U66): the host would refuse this path. */
  it('stays text for a path outside the session\'s tree', () => {
    show({ ...READ, title: 'Read C:/somewhere/game/a.ts', locations: ['C:/somewhere/game/a.ts'] }, vi.fn());

    expect(screen.queryByRole('button', { name: /^preview / })).toBeNull();
    expect(screen.getByText('Read C:/somewhere/game/a.ts')).toBeInTheDocument();
  });

  /** A search's `offset` is not a line: only a read's input names lines. */
  it('names no lines for a call that is not a read, whatever its input carries', async () => {
    const open = vi.fn();
    show({ ...READ, title: 'Search', toolKind: 'search', input: JSON.stringify({ path: `${TREE}/src/chunk.ts`, offset: 10 }) }, open);

    await userEvent.click(screen.getByRole('button', { name: 'preview src/chunk.ts' }));
    expect(open).toHaveBeenCalledWith({ path: 'src/chunk.ts', lines: null });
  });
});
