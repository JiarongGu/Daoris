import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { CodeDependency, CodeModule } from '../api';
import { CodeMapCanvas, CodeMapDetail, codeEdge } from './CodeMap';

// MAP3a: a repository's own code map, drawn — modules as buttons, arrows to what each depends on.

const MODULES: CodeModule[] = [
  { id: 'web', path: 'src/Web', summary: 'the platform' },
  { id: 'service', path: 'src/Service', summary: 'indexes the family' },
  { id: 'a-module-with-a-very-long-name', path: 'src/Long', summary: '' },
];
const DEPENDENCIES: CodeDependency[] = [
  { from: 'web', to: 'service', kind: 'http' },
  { from: 'a-module-with-a-very-long-name', to: 'service', kind: 'imports' },
];

describe('the code map drawing', () => {
  it('is a button per module, named with how many things it depends on', () => {
    const onSelect = vi.fn();
    render(<CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={onSelect} />);

    const map = screen.getByRole('group', { name: 'the code map of engine' });
    fireEvent.click(within(map).getByRole('button', { name: 'web, depends on 1' }));
    expect(onSelect).toHaveBeenCalledWith('web');
    expect(within(map).getByRole('button', { name: 'service, depends on 0' })).toBeInTheDocument();
  });

  /** A long id is cut on the drawing and whole in the name a reader hears. */
  it('cuts an id too long for its box, and keeps it whole in its name', () => {
    render(<CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => {}} />);

    const long = screen.getByRole('button', { name: 'a-module-with-a-very-long-name, depends on 1' });
    expect(long.textContent).toMatch(/…$/);
    expect(long.textContent!.length).toBeLessThan('a-module-with-a-very-long-name'.length);
  });

  it('marks the chosen module as pressed', () => {
    render(<CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected="service" onSelect={() => {}} />);

    expect(screen.getByRole('button', { name: 'service, depends on 0' })).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('an arrow', () => {
  /** 🔴 Seen on the window: `runtime → chunks` ran straight down behind `renderer` and vanished. */
  it('that skips a layer bows out past the column instead of running behind the box between', () => {
    const numbers = (path: string) => path.match(/-?\d+(\.\d+)?/g)!.map(Number);
    const [, , c1x, , c2x] = numbers(codeEdge({ x: 300, y: 0 }, { x: 300, y: 184 }));

    // Both control points lie beyond the boxes' right edge (a box is 136 wide, centred on 300).
    expect(c1x).toBeGreaterThan(300 + 68);
    expect(c2x).toBeGreaterThan(300 + 68);
  });

  it('to the next layer down runs from the bottom of one box to the top of the next', () => {
    expect(codeEdge({ x: 300, y: 0 }, { x: 300, y: 92 })).toMatch(/^M 300 36 /);
  });
});

describe('the code map detail', () => {
  it('says which file it came from before anything is chosen', () => {
    render(<CodeMapDetail modules={MODULES} dependencies={DEPENDENCIES} selected={null} file="docs/code-map.json" onSelect={() => {}} />);

    expect(screen.getByText(/docs\/code-map\.json/)).toBeInTheDocument();
  });

  it('lists what a module depends on and what uses it, each a door to that module', () => {
    const onSelect = vi.fn();
    render(<CodeMapDetail modules={MODULES} dependencies={DEPENDENCIES} selected="service" file="docs/code-map.json" onSelect={onSelect} />);

    expect(screen.getByText('src/Service')).toBeInTheDocument();
    expect(screen.getByText('indexes the family')).toBeInTheDocument();
    expect(screen.getByText('none')).toBeInTheDocument();
    expect(screen.getByText('http')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'web' }));
    expect(onSelect).toHaveBeenCalledWith('web');
  });
});
