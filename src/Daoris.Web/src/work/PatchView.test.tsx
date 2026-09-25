import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import '../i18n';
import { PatchView } from './PatchView';

// A file's patch drawn (REVIEW2): numbered, highlighted in its language, unified or side by side. A
// molecule, so every state is reached by passing a patch.

const PATCH = [
  'diff --git a/src/chunk.rs b/src/chunk.rs',
  '--- a/src/chunk.rs',
  '+++ b/src/chunk.rs',
  '@@ -10,4 +10,5 @@ pub fn hydrate(region: &Region) {',
  '     for tile in &region.tiles {',
  '-        load(tile);',
  '+        if budget.spent() {',
  '+            break;',
  '+        }',
  '     }',
  '\\ No newline at end of file',
].join('\n');

const rows = () => screen.getAllByRole('row');
const cells = (row: HTMLElement) => within(row).getAllByRole('cell').map((cell) => cell.textContent);

describe('a patch, unified', () => {
  it('numbers each line on the side it belongs to, and signs what changed', () => {
    render(<PatchView patch={PATCH} path="src/chunk.rs" layout="unified" />);

    expect(screen.getByText('@@ -10,4 +10,5 @@ pub fn hydrate(region: &Region) {')).toBeInTheDocument();
    const lines = rows();
    expect(cells(lines[0]!)).toEqual(['10', '10', ' ', '    for tile in &region.tiles {']);
    expect(cells(lines[1]!)).toEqual(['11', '', '−', '        load(tile);']);
    expect(cells(lines[2]!)).toEqual(['', '11', '+', '        if budget.spent() {']);
    expect(lines[2]).toHaveAttribute('data-kind', 'add');
    expect(screen.getByText('\\ No newline at end of file')).toBeInTheDocument();
  });

  it('highlights the code in the file\'s own language, and leaves an unknown one plain', () => {
    const { container, unmount } = render(<PatchView patch={PATCH} path="src/chunk.rs" layout="unified" />);
    expect(container.querySelector('.hljs-keyword')?.textContent).toBe('for');
    unmount();

    const plain = render(<PatchView patch={PATCH} path="notes.txt" layout="unified" />);
    expect(plain.container.querySelector('[class^="hljs-"]')).toBeNull();
  });
});

describe('a patch, side by side', () => {
  it('sets each removal across from what replaced it, each side numbered', () => {
    render(<PatchView patch={PATCH} path="src/chunk.rs" layout="split" />);

    const lines = rows();
    expect(cells(lines[0]!)).toEqual(['10', '    for tile in &region.tiles {', '10', '    for tile in &region.tiles {']);
    expect(cells(lines[1]!)).toEqual(['11', '        load(tile);', '11', '        if budget.spent() {']);
    expect(cells(lines[2]!)).toEqual(['', '', '12', '            break;']);
  });
});

/**
 * Seen on the window (REVIEW2): a long line on the old side pushed the table wider than the pane, and
 * the new side went out of view — side by side showed one side. The two halves are fixed at half each
 * and their lines wrap, so both stay in view; the unified layout keeps one line to a row and scrolls.
 */
describe('a long line, side by side', () => {
  it('wraps within its half rather than pushing the other side out of view', () => {
    const { container } = render(<PatchView patch={PATCH} path="src/chunk.rs" layout="split" />);
    expect(container.querySelector('table')).toHaveClass('table-fixed');
    expect(screen.getAllByRole('row')[1]!.querySelectorAll('td')[1]).toHaveClass('whitespace-pre-wrap');
  });

  it('keeps a unified line whole, to scroll', () => {
    const { container } = render(<PatchView patch={PATCH} path="src/chunk.rs" layout="unified" />);
    expect(container.querySelector('table')).not.toHaveClass('table-fixed');
    expect(screen.getAllByRole('row')[1]!.querySelectorAll('td')[3]).toHaveClass('whitespace-pre');
  });
});

describe('a patch with no hunks', () => {
  it('says what git said — a rename — rather than drawing nothing', () => {
    render(<PatchView patch={'diff --git a/a.md b/b.md\nsimilarity index 100%\nrename from a.md\nrename to b.md'} path="b.md" layout="unified" />);
    expect(screen.getByText('rename from a.md')).toBeInTheDocument();
  });
});
