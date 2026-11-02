import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactElement } from 'react';
import type { CodeDependency, CodeModule } from '../data/api';
import { graphIdentity, memoryKey } from '../design/canvas/viewMemory';
import { buildTree, defaultOpen, layoutCode } from './codeClusters';
import { type CodeFixture, syntheticMap, WITH_A_CYCLE } from './codeFixtures';
import { CodeMapDetail, CodeMapWorld, cutId, FITS } from './CodeMap';

// MAP3a and CANVAS1f: a repository's own code map on the canvas — folders, modules as buttons, only the supplied dependencies as lines.
// jsdom lays nothing out, so unless a width is stood in for the canvas is below its floor and the world is drawn whole as a pane.

const measured = vi.hoisted(() => ({ width: undefined as number | undefined }));
vi.mock('../design/useWidth', async (original) => ({ ...(await original<typeof import('../design/useWidth')>()), useWidth: () => measured.width }));

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const MODULES: CodeModule[] = [
  { id: 'web', path: 'src/Web', summary: 'the platform' },
  { id: 'service', path: 'src/Service', summary: 'indexes the family' },
  { id: 'a-module-with-a-very-long-name', path: 'src/Long', summary: '' },
];
const DEPENDENCIES: CodeDependency[] = [
  { from: 'web', to: 'service', kind: 'http' },
  { from: 'a-module-with-a-very-long-name', to: 'service', kind: 'imports' },
];

function Host({ map = { modules: MODULES, dependencies: DEPENDENCIES }, first = null, onChange = () => undefined, version }: {
  map?: CodeFixture; first?: string | null; onChange?: (id: string | null) => void; version?: string;
}) {
  const [selected, setSelected] = useState<string | null>(first);
  return (
    <CodeMapWorld
      repository="engine" modules={map.modules} dependencies={map.dependencies} selected={selected} version={version}
      onSelect={(id) => { setSelected(id); onChange(id); }}
    />
  );
}

const lines = () => [...document.querySelectorAll<SVGGElement>('[data-flow-line]')];
beforeEach(() => { measured.width = undefined; });
afterEach(() => localStorage.clear());

describe('the code map on the canvas', () => {
  it('is a button per module, named with how many things it depends on, in a region named for the repository', async () => {
    const onChange = vi.fn();
    render(<Host onChange={onChange} />);
    const map = screen.getByRole('group', { name: 'the code map of engine' });
    await userEvent.click(within(map).getByRole('button', { name: 'web, depends on 1' }));
    expect(onChange).toHaveBeenCalledWith('web');
    expect(within(map).getByRole('button', { name: 'service, depends on 0' })).toBeInTheDocument();
  });

  it('cuts an id too long for its card, and keeps it whole in its name', () => {
    render(<Host />);
    const long = screen.getByRole('button', { name: 'a-module-with-a-very-long-name, depends on 1' });
    expect(long.textContent).toBe(cutId('a-module-with-a-very-long-name'));
    expect(long.textContent).toMatch(/…$/);
    expect(long.textContent!.length).toBe(FITS);
  });

  it('marks the chosen module as pressed, releases it on a second press and on Escape', async () => {
    const onChange = vi.fn();
    render(<Host first="service" onChange={onChange} />);
    const service = screen.getByRole('button', { name: 'service, depends on 0' });
    expect(service).toHaveAttribute('aria-pressed', 'true');
    await userEvent.click(service);
    expect(onChange).toHaveBeenLastCalledWith(null);
    await userEvent.click(service);
    expect(onChange).toHaveBeenLastCalledWith('service');
    service.focus();
    await userEvent.keyboard('{Escape}');
    expect(onChange).toHaveBeenLastCalledWith(null);
  });

  it('draws only the supplied dependencies, each named with its kind as written', () => {
    render(<Host />);
    expect(lines().map((line) => line.getAttribute('aria-label')).sort()).toEqual([
      'a-module-with-a-very-long-name depends on service (imports)', 'web depends on service (http)',
    ]);
    // Two modules of one folder with no dependency between them are joined by nothing.
    expect(lines()).toHaveLength(2);
  });

  it('stands the chosen module\'s neighbourhood forward and steps the rest back', () => {
    render(<Host first="web" />);
    const back = lines().filter((line) => line.getAttribute('class')!.includes('opacity-20'));
    expect(back.map((line) => line.getAttribute('aria-label'))).toEqual(['a-module-with-a-very-long-name depends on service (imports)']);
    expect(screen.getByRole('button', { name: 'a-module-with-a-very-long-name, depends on 1' }).parentElement!.innerHTML).toContain('opacity-40');
  });

  it('draws the arrows in ink, heads too', () => {
    render(<Host />);
    expect(document.querySelector('#code-arrow path')!.getAttribute('class')).toBe('fill-ink-faint');
    for (const line of lines()) expect(line.querySelector('path.shown')!.getAttribute('class')).toContain('stroke-ink-faint');
  });
});

describe('folders', () => {
  const big = syntheticMap(100);

  it('open closed past 60 modules, a line between two counted until one opens', async () => {
    render(<Host map={big} />);
    expect(screen.queryByRole('button', { name: /^module-0000/ })).toBeNull();
    const folders = screen.getAllByRole('button', { name: /a folder of \d+ modules?\. Press to open$/ });
    expect(folders.length).toBeGreaterThan(1);
    const counted = lines().filter((line) => /: \d+ dependenc/.test(line.getAttribute('aria-label')!));
    expect(counted.length).toBeGreaterThan(0);
    expect(counted[0]!.textContent).toMatch(/×\d+/);
    // A folder opens to its subfolders, closed, and a press on one of those to its modules; the rest stay cards.
    await userEvent.click(folders[0]!);
    expect(document.querySelector('[data-canvas-group]')).not.toBeNull();
    expect(screen.queryByRole('button', { name: /^module-\d{4}, depends on/ })).toBeNull();
    await userEvent.click(screen.getAllByRole('button', { name: /^area-00\/part-00, a folder/ })[0]!);
    expect(screen.getAllByRole('button', { name: /^module-\d{4}, depends on/ }).length).toBe(12);
    expect(screen.getAllByRole('button', { name: /a folder of/ }).length).toBe(folders.length + 1);
  });

  it('open the path to a chosen module, from outside the drawing too', () => {
    render(<Host map={big} first="module-0042" />);
    expect(screen.getByRole('button', { name: /^module-0042, depends on/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.queryByRole('button', { name: /^module-0000/ })).toBeNull();
  });

  it('are found: find opens the path to its match and chooses it', async () => {
    const onChange = vi.fn();
    render(<Host map={big} onChange={onChange} />);
    const field = screen.getByRole('searchbox', { name: 'find a module' });
    await userEvent.type(field, 'module-0057');
    await userEvent.keyboard('{Enter}');
    expect(onChange).toHaveBeenLastCalledWith('module-0057');
    expect(screen.getByRole('button', { name: /^module-0057, depends on/ })).toHaveAttribute('aria-pressed', 'true');
    expect(field).toHaveFocus();
  });

  it('are listed with every module under its folder, a pick choosing the module', async () => {
    const onChange = vi.fn();
    render(<Host map={big} onChange={onChange} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));
    const list = document.querySelector('[data-canvas-list]')!;
    expect(within(list as HTMLElement).getAllByRole('heading', { level: 3 }).length).toBeGreaterThan(1);
    expect(within(list as HTMLElement).getAllByRole('listitem')).toHaveLength(100);
    await userEvent.click(within(list as HTMLElement).getByRole('button', { name: 'module-0009' }));
    expect(onChange).toHaveBeenCalledWith('module-0009');
    await userEvent.click(screen.getByRole('button', { name: 'Show as graph' }));
    expect(screen.getByRole('group', { name: 'the code map of engine' })).toBeInTheDocument();
  });

  it('are all opened by Show every folder, and closed again by Fold the folders again', async () => {
    render(<Host map={big} />);
    await userEvent.click(screen.getByRole('button', { name: 'Show every folder' }));
    expect(screen.getAllByRole('button', { name: /^module-\d{4}, depends on/ })).toHaveLength(100);
    await userEvent.click(screen.getByRole('button', { name: 'Fold the folders again' }));
    expect(screen.queryByRole('button', { name: /^module-0000/ })).toBeNull();
  });
});

describe('a cycle', () => {
  it('is one card labelled a cycle, whole when opened', async () => {
    const small = { modules: WITH_A_CYCLE.modules, dependencies: WITH_A_CYCLE.dependencies };
    const tree = buildTree(small.modules, small.dependencies);
    expect(defaultOpen(tree).size).toBeGreaterThan(0);
    render(<Host map={small} />);
    // At five modules everything opens, the cycle too: its frame says so and its members are cards.
    expect(screen.getByText('cycle of 3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'engine, depends on 1' })).toBeInTheDocument();
  });
});

describe('the viewer\'s place', () => {
  beforeEach(() => { measured.width = 900; });
  const scaleOf = () => Number(document.querySelector('[data-canvas-scale]')!.getAttribute('data-canvas-scale'));

  it('is kept per version: another commit restores no coordinates', () => {
    const tree = buildTree(MODULES, DEPENDENCIES);
    const world = layoutCode(tree, defaultOpen(tree), { cycle: (count) => `cycle of ${count}` }).world;
    const identity = graphIdentity([...world.order, ...world.lines.map((line) => line.id)]);
    localStorage.setItem(memoryKey('code', 'engine@aaa'), JSON.stringify({ identity, view: { x: 0, y: 0, k: 1.7 }, anchor: null, selected: null }));
    const first = render(<Host version="aaa" />);
    expect(scaleOf()).toBeCloseTo(1.7, 1);
    first.unmount();
    render(<Host version="bbb" />);
    expect(scaleOf()).not.toBeCloseTo(1.7, 1);
    expect(scaleOf()).toBeGreaterThanOrEqual(0.8);
  });

  it('opens fitted, never below four fifths', () => {
    render(<Host />);
    expect(scaleOf()).toBeGreaterThanOrEqual(0.8);
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

  it('lists what a module depends on and what uses it, each a door to that module', async () => {
    const onSelect = vi.fn();
    render(<CodeMapDetail modules={MODULES} dependencies={DEPENDENCIES} selected="service" file="docs/code-map.json" onSelect={onSelect} />);

    expect(screen.getByText('src/Service')).toBeInTheDocument();
    expect(screen.getByText('indexes the family')).toBeInTheDocument();
    expect(screen.getByText('none')).toBeInTheDocument();
    expect(screen.getByText('http')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'web' }));
    expect(onSelect).toHaveBeenCalledWith('web');
  });

  it('keeps an id whole where the card cut it', () => {
    render(<CodeMapDetail modules={MODULES} dependencies={DEPENDENCIES} selected="a-module-with-a-very-long-name" file="docs/code-map.json" onSelect={() => {}} />);
    expect(screen.getByText('a-module-with-a-very-long-name')).toBeInTheDocument();
  });
});
