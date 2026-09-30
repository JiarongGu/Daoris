import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { FilePreview } from './FilePreview';
import type { TreeFile } from './preview';

// A file's preview (PREVIEW1, D111). A molecule: every state — read, marked, binary, bounded, empty,
// refused, reading — is reached by passing props, with no bridge, no tree and no git.

const FILE: TreeFile = {
  session: 'c0ffee11',
  path: 'src/world/chunk.ts',
  size: 61,
  binary: false,
  text: 'export const a = 1;\nexport const b = 2;\nexport const c = 3;\n',
  truncated: false,
};

const wrap = (node: ReactNode) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((cell) => cell.textContent);

describe('a file\'s preview', () => {
  it('names the file relative to its tree, and numbers each of its lines', () => {
    wrap(<FilePreview path={FILE.path} file={FILE} />);

    expect(screen.getByText('src/world/chunk.ts')).toBeInTheDocument();
    const rows = screen.getAllByRole('row');
    expect(rows).toHaveLength(3);
    expect(cells(rows[0]!)).toEqual(['1', 'export const a = 1;']);
    expect(cells(rows[2]!)).toEqual(['3', 'export const c = 3;']);
    // Its size and length, in the reader's words.
    expect(screen.getByText('61 B · 3 lines')).toBeInTheDocument();
  });

  /** Where the card knows them: the lines are marked, said in words, and nothing else is. */
  it('marks the lines a tool call named, and says which', () => {
    wrap(<FilePreview path={FILE.path} file={FILE} lines={{ from: 2, to: 3 }} />);

    const rows = screen.getAllByRole('row');
    expect(rows.map((row) => row.dataset.marked === 'true')).toEqual([false, true, true]);
    expect(screen.getByText('lines 2–3, as the call named them')).toBeInTheDocument();
  });

  it('says one named line as one', () => {
    wrap(<FilePreview path={FILE.path} file={FILE} lines={{ from: 2, to: 2 }} />);

    expect(screen.getByText('line 2, as the call named it')).toBeInTheDocument();
  });

  it('says a binary file in a sentence with its size, and draws no lines', () => {
    wrap(<FilePreview path="assets/logo.png" file={{ ...FILE, path: 'assets/logo.png', binary: true, text: null, size: 2048 }} />);

    expect(screen.getByText('logo.png is a binary file (2 KB), so there is no text here to show.')).toBeInTheDocument();
    expect(screen.queryAllByRole('row')).toHaveLength(0);
  });

  /** The bound is stated, never hidden (design §5): how much the file holds, and where the rest is. */
  it('says how much of a long file it shows, and that the file on disk has the rest', () => {
    wrap(<FilePreview path={FILE.path} file={{ ...FILE, size: 3 * 1024 * 1024, truncated: true }} />);

    expect(screen.getByText('This file is 3 MB; the preview shows its first 3 lines. The file on disk has the rest.'))
      .toBeInTheDocument();
  });

  /**
   * REVIEW2 (D113): once a landing tidied the tree away, the file is the landed branch's copy, and the preview says
   * so in a sentence — and that the branch, not the disk, has the rest of a long one.
   */
  it('says a file read from the landed branch is as that branch holds it', () => {
    wrap(<FilePreview path={FILE.path} file={{ ...FILE, branch: 'feature/0fda18-fix', size: 3 * 1024 * 1024, truncated: true }} />);

    const said = screen.getByText(/as the branch this session's work landed on holds it/);
    expect(said.textContent).toBe("The session's tree is gone, so this is the file as the branch this session's work landed on holds it: feature/0fda18-fix.");
    expect(screen.getByText('feature/0fda18-fix', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('This file is 3 MB; the preview shows its first 3 lines. The branch has the rest.')).toBeInTheDocument();
  });

  it('says nothing of a branch for a file on disk', () => {
    wrap(<FilePreview path={FILE.path} file={FILE} />);

    expect(screen.queryByText(/landed on holds it/)).toBeNull();
  });

  it('says an empty file is empty, rather than drawing nothing', () => {
    wrap(<FilePreview path="src/empty.ts" file={{ ...FILE, path: 'src/empty.ts', text: '', size: 0 }} />);

    expect(screen.getByText('This file is empty.')).toBeInTheDocument();
  });

  /** The host's refusal is the contract: its sentence, with its path as code. */
  it('shows the host\'s refusal as it said it', () => {
    wrap(<FilePreview path="../game/a.ts" refusal="`../game/a.ts` is outside this session's tree, so the preview does not read it." />);

    expect(screen.getByText('../game/a.ts', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/is outside this session's tree/)).toBeInTheDocument();
    expect(screen.queryAllByRole('row')).toHaveLength(0);
  });

  it('names the file while it is still being read', () => {
    wrap(<FilePreview path={FILE.path} pending />);

    expect(screen.getByText('src/world/chunk.ts')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('reading…');
  });

  /** The review's own patch, where it has one: never a second diff (D111). */
  it('offers the file\'s changes where the review holds a patch for it, and draws that patch', async () => {
    wrap(<FilePreview path={FILE.path} file={FILE} patch={'@@ -1 +1 @@\n-export const a = 0;\n+export const a = 1;'} />);

    const show = screen.getByRole('radiogroup', { name: 'Show' });
    expect(within(show).getByRole('radio', { name: 'File' })).toHaveAttribute('aria-checked', 'true');

    await userEvent.click(within(show).getByRole('radio', { name: 'Changes' }));
    expect(screen.getByText('@@ -1 +1 @@')).toBeInTheDocument();
    // The patch's own rows: numbered each side, signed, the old line beside the new.
    expect(screen.getAllByRole('row').map(cells)).toEqual([
      ['1', '', '−', 'export const a = 0;'],
      ['', '1', '+', 'export const a = 1;'],
    ]);
  });

  it('offers no changes where the review holds none', () => {
    wrap(<FilePreview path={FILE.path} file={FILE} />);

    expect(screen.queryByRole('radiogroup', { name: 'Show' })).toBeNull();
  });

  it('reads the file again when asked', async () => {
    const onReload = vi.fn();
    wrap(<FilePreview path={FILE.path} file={FILE} onReload={onReload} />);

    await userEvent.click(screen.getByRole('button', { name: 'Read again' }));
    expect(onReload).toHaveBeenCalledTimes(1);
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      wrap(<FilePreview path="assets/logo.png" file={{ ...FILE, path: 'assets/logo.png', binary: true, text: null, size: 2048 }} />);
      expect(screen.getByText('logo.png 是二进制文件（2 KB），这里没有可显示的文本。')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
