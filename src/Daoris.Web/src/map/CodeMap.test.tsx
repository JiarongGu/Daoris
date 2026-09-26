import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { CodeDependency, CodeModule } from '../api';
import { CodeMapCanvas, CodeMapDetail, codeEdge, codeWidth } from './CodeMap';

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

  /**
   * UX5 U44: drawn at its own size, one unit a pixel, so a module's name is the type scale's. It was
   * stretched to its card: 640 units drawn 775px wide at 1400, a name 13.3px beside a 13px body.
   */
  it('is drawn at its own size, shrinking only where its card is narrower', () => {
    const { container } = render(
      <CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => {}} />);
    const svg = container.querySelector('svg')!;
    const [, , width] = svg.getAttribute('viewBox')!.split(' ').map(Number);

    expect(svg.getAttribute('width')).toBe(String(width));
    expect(svg.getAttribute('class')).toContain('max-w-full');
  });

  it('meets a narrow card with its width, not with smaller names', () => {
    expect(codeWidth(1, undefined)).toBe(640);
    expect(codeWidth(1, 2000)).toBe(640);
    expect(codeWidth(1, 400)).toBe(400);
    // A row too wide for the card keeps its boxes whole, and the drawing shrinks as the last resort.
    expect(codeWidth(6, 400)).toBe(6 * (136 + 20) + 48);
  });

  /** UX5 U47, as on the workspace map: a second press releases the choice, and so does Escape. */
  it('releases the chosen module on a second press, and on Escape', () => {
    const onSelect = vi.fn();
    render(<CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected="service" onSelect={onSelect} />);
    const service = screen.getByRole('button', { name: 'service, depends on 0' });

    fireEvent.click(service);
    expect(onSelect).toHaveBeenLastCalledWith(null);
    fireEvent.keyDown(service, { key: 'Escape' });
    expect(onSelect).toHaveBeenCalledTimes(2);
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });

  /** U47: a module the keyboard is on lights its arrows, as the pointer over it does. */
  it('lights the arrows of a focused module', () => {
    const { container } = render(
      <CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => {}} />);
    const back = () => [...container.querySelectorAll('svg > path')]
      .filter((path) => path.getAttribute('class')!.includes('opacity-20'));
    expect(back()).toHaveLength(0);

    fireEvent.focus(screen.getByRole('button', { name: 'web, depends on 1' }));

    // web → service stands forward; the long module's arrow steps back.
    expect(back()).toHaveLength(1);
  });
});

describe('an arrow', () => {
  /**
   * UX5 U48: the arrows are the code map's content, so they wear an ink, not a container's line.
   * `--line-strong` is 1.6:1 on the card in both themes, and a stepped-back arrow was a fifth of
   * that: in dark the unlit ones vanished.
   */
  it('is drawn in ink, its head too', () => {
    const { container } = render(
      <CodeMapCanvas repository="engine" modules={MODULES} dependencies={DEPENDENCIES} selected={null} onSelect={() => {}} />);

    for (const path of container.querySelectorAll('svg > path')) {
      expect(path.getAttribute('class')).toContain('stroke-ink-faint');
      expect(path.getAttribute('class')).not.toContain('line-strong');
    }
    expect(container.querySelector('#code-arrow path')!.getAttribute('class')).toBe('fill-ink-faint');
  });

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

  /**
   * MAP3e: a teammate's map came down with the sync, so it says where from — the commit, its line and
   * whose key fed it — rather than implying this machine read a checkout it does not have.
   */
  it('says where a fed map came from: the commit, its line, and who fed it', () => {
    render(
      <CodeMapDetail
        modules={MODULES} dependencies={DEPENDENCIES} selected={null} file="docs/code-map.json" onSelect={() => {}}
        fed={{
          commit: 'c0ffee1234567890', shortCommit: 'c0ffee12', committedAt: new Date().toISOString(),
          branch: 'main', origin: 'person@machine-b',
        }}
      />,
    );

    const line = screen.getByText(/c0ffee12/);
    expect(line.textContent).toContain('docs/code-map.json');
    expect(line.textContent).toContain('main');
    expect(line.textContent).toContain('person@machine-b');
    expect(line.textContent).toContain('brought here by the sync');
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
