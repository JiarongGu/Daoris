import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { code } from '../test/code';
import type { DiffFile } from './diff';
import { DiffFileRow } from './DiffFileRow';

// Props in, states out — every case below is reached without a bridge, a repository or a git
// process (components plan §2).

const FILE: DiffFile = {
  path: 'src/world/chunk.ts',
  status: 'modified',
  added: 4,
  removed: 1,
  patch: '@@ -1,2 +1,3 @@\n context\n-gone\n+arrived\n+also arrived',
};

const show = (file: DiffFile, props: { open?: boolean; viewed?: boolean } = {}) => {
  const handlers = { onToggle: vi.fn(), onViewed: vi.fn() };
  render(
    <ul>
      <DiffFileRow file={file} open={props.open ?? false} viewed={props.viewed ?? false} {...handlers} />
    </ul>,
  );
  return handlers;
};

describe('DiffFileRow', () => {
  it('names the file and its counts without opening anything', () => {
    show(FILE);
    expect(screen.getByText('src/world/chunk.ts')).toBeTruthy();
    expect(screen.getByText('+4')).toBeTruthy();
    expect(screen.getByText('−1')).toBeTruthy();
    expect(screen.queryByText(/arrived/)).toBeNull();
  });

  it('opens in place — a multibuffer row, not a door to somewhere else', async () => {
    const { onToggle } = show(FILE);
    const disclosure = screen.getByRole('button', { expanded: false });
    await userEvent.click(disclosure);
    expect(onToggle).toHaveBeenCalledTimes(1);

    // Told it is open, it renders the patch itself rather than navigating.
    show({ ...FILE, path: 'other.ts' }, { open: true });
    expect(screen.getByText('also arrived')).toBeTruthy();
  });

  /**
   * The status is a letter AND a label, never a colour alone (D41 §6) — the accessible name carries
   * it for everyone the hue does not.
   */
  it('says which kind of change it is, in words', () => {
    show({ ...FILE, status: 'deleted' });
    expect(screen.getByLabelText('deleted')).toBeTruthy();
    expect(screen.getByLabelText('deleted').textContent).toBe('D');
  });

  /** "Not counted" and "counted nothing" are different answers, and a binary file is the first. */
  it('reports a binary file as uncounted rather than as zero', () => {
    show({ ...FILE, added: null, removed: null, patch: null });
    expect(screen.getByText('binary')).toBeTruthy();
    expect(screen.queryByText('+0')).toBeNull();
    expect(screen.queryByText('−0')).toBeNull();
  });

  /**
   * The bound is the host's, and a file it dropped is still LISTED — "this changed and you cannot
   * read it here" is information; an absent row is a lie about what the session did.
   */
  it('lists a file whose patch the bound dropped, and says where the rest is', () => {
    show({ ...FILE, patch: null }, { open: true });
    expect(screen.getByText('src/world/chunk.ts')).toBeTruthy();
    expect(screen.getByText(code(/git diff/))).toBeTruthy();
  });

  it('carries the person’s own viewed mark, which is theirs to set', async () => {
    const { onViewed } = show(FILE);
    await userEvent.click(screen.getByRole('checkbox', { name: 'Viewed' }));
    expect(onViewed).toHaveBeenCalledWith(true);
    // The platform's own box, never the OS's (UX5 U5).
    expect(document.querySelector('input[type="checkbox"]:not([aria-hidden="true"])')).toBeNull();
  });

  /** An empty patch line must still occupy a line, or the diff silently loses its blank context. */
  it('keeps a blank context line as a line', () => {
    show({ ...FILE, patch: '@@ -1,3 +1,3 @@\n a\n \n b' }, { open: true });
    expect(screen.getAllByRole('row')).toHaveLength(3);
  });

  /**
   * PREVIEW1 (D111): the file itself, read in the side bar, one press from its row — beside the row's own
   * disclosure, which still opens the patch in place.
   */
  it('opens the file\'s preview from its own button, leaving the patch where it was', async () => {
    const onPreview = vi.fn();
    const onToggle = vi.fn();
    render(
      <Tooltip.Provider>
        <ul>
          <DiffFileRow file={FILE} open={false} viewed={false} onToggle={onToggle} onViewed={() => {}} onPreview={onPreview} />
        </ul>
      </Tooltip.Provider>,
    );

    await userEvent.click(screen.getByRole('button', { name: 'preview src/world/chunk.ts' }));
    expect(onPreview).toHaveBeenCalledTimes(1);
    expect(onToggle).not.toHaveBeenCalled();
  });

  /** A door that could only refuse is not offered (UX5 U66): a deleted file is not in the tree any more. */
  it('offers no preview of a file the session deleted', () => {
    render(
      <Tooltip.Provider>
        <ul>
          <DiffFileRow file={{ ...FILE, status: 'deleted' }} open={false} viewed={false} onToggle={() => {}} onViewed={() => {}} onPreview={vi.fn()} />
        </ul>
      </Tooltip.Provider>,
    );

    expect(screen.queryByRole('button', { name: /^preview / })).toBeNull();
  });

  /** REVIEW2: the layout the pane chose, side by side here — each removal across from what replaced it. */
  it('draws its patch in the layout it is given', () => {
    render(
      <ul>
        <DiffFileRow file={FILE} open viewed={false} layout="split" onToggle={() => {}} onViewed={() => {}} />
      </ul>,
    );
    const row = screen.getAllByRole('row')[1]!;
    expect(row.textContent).toContain('gone');
    expect(row.textContent).toContain('arrived');
  });
});
