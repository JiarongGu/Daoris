import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { MapNode, Topology } from './topology';
import { MapWorld } from './MapWorld';

// MAP2, MAP4 and CANVAS1e: the workspace map as one canvas — the ring fitted and the layers that pan and zoom, a find with its list, and
// every card and line a stop the arrows walk. jsdom lays nothing out, so a width is stood in for where the viewport is the point.

const measured = vi.hoisted(() => ({ width: undefined as number | undefined }));
vi.mock('../design/useWidth', async (original) => ({ ...(await original<typeof import('../design/useWidth')>()), useWidth: () => measured.width }));

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const node = (id: string, extra: Partial<MapNode> = {}): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0, ...extra });

const IDS = Array.from({ length: 20 }, (_, index) => `repo-${String(index).padStart(2, '0')}`);
const TOPOLOGY: Topology = {
  nodes: [...IDS.map((id) => node(id)), node('portal-ui', { open: 2, working: true }), node('portals-db')],
  quests: [{ from: 'portal-ui', to: 'portals-db', quests: [], open: 1, live: null }],
  asks: [],
  chains: [],
  depends: [],
  knowledge: [{ a: 'portals-db', b: 'repo-03', groups: 2 }],
  outside: 0,
  circles: 1,
};

const map = (topology: Topology, selected = null as Parameters<typeof MapWorld>[0]['selected'], onSelect = vi.fn()) => (
  <MapWorld topology={topology} selected={selected} onSelect={onSelect} storeKey="test" />
);
const scaleOf = () => Number(document.querySelector('[data-canvas-scale]')!.getAttribute('data-canvas-scale'));

beforeEach(() => { measured.width = undefined; });
afterEach(() => localStorage.clear());

describe('the map as layers', () => {
  it('draws every repository as a card a keyboard reaches, named for what it holds, with one Tab stop among cards and lines', () => {
    render(map(TOPOLOGY));
    const stops = [...document.querySelectorAll('[data-flow-node], [data-flow-line]')];
    expect(stops.filter((each) => each.getAttribute('tabindex') === '0')).toHaveLength(1);
    expect(screen.getByRole('button', { name: /^portal-ui, / })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^repo-\d\d/ })).toHaveLength(20);
    expect(screen.getByRole('button', { name: /from portal-ui to portals-db/ })).toBeInTheDocument();
    // What nothing connects is set apart, and says so.
    expect(screen.getByText('No line drawn to these')).toBeInTheDocument();
  });

  it('walks from a card to its line and the card beyond, and back (the roving stop, trap 15)', async () => {
    render(map(TOPOLOGY));
    const user = userEvent.setup();
    screen.getByRole('button', { name: /^portal-ui, / }).focus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: /from portal-ui to portals-db/ })).toHaveFocus();
    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('button', { name: /^portals-db, / })).toHaveFocus();
    // A shared finding is reached from either card: ← from its far end, by the first line in.
    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(screen.getByRole('button', { name: /^portal-ui, / })).toHaveFocus();
  });

  it('finds a repository as it is typed, lists the matches, dims the rest and chooses the one shown on Enter', async () => {
    const onSelect = vi.fn();
    render(map(TOPOLOGY, null, onSelect));
    const user = userEvent.setup();
    const find = screen.getByRole('searchbox', { name: 'find a repository' });
    await user.type(find, 'portal');
    expect(screen.getByRole('list', { name: 'Matches' }).querySelectorAll('li')).toHaveLength(2);
    // The rest step back, so the matches stand out.
    expect(document.querySelector('[data-flow-node="repo-00"]')!.className).toContain('opacity-25');
    await user.keyboard('{Enter}');
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'node', id: 'portal-ui' });
    expect(find).toHaveFocus();
  });

  it('chooses and releases as the ring does: a second press, or Escape', async () => {
    const onSelect = vi.fn();
    const { rerender } = render(map(TOPOLOGY, null, onSelect));
    fireEvent.click(screen.getByRole('button', { name: /^portal-ui, / }));
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'node', id: 'portal-ui' });

    rerender(<Tooltip.Provider>{map(TOPOLOGY, { kind: 'node', id: 'portal-ui' }, onSelect)}</Tooltip.Provider>);
    expect(screen.getByRole('button', { name: /^portal-ui, / })).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: /^portal-ui, / }));
    expect(onSelect).toHaveBeenLastCalledWith(null);
    fireEvent.keyDown(screen.getByRole('button', { name: /^portal-ui, / }), { key: 'Escape' });
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });

  it('draws a pair asked both ways as two lines apart, each with its own count in view, and each chosen alone', async () => {
    const both: Topology = {
      ...TOPOLOGY,
      quests: [...TOPOLOGY.quests, { from: 'portals-db', to: 'portal-ui', quests: [], open: 1, live: null }],
    };
    const onSelect = vi.fn();
    render(map(both, null, onSelect));
    const there = screen.getByRole('button', { name: /from portal-ui to portals-db/ });
    const back = screen.getByRole('button', { name: /from portals-db to portal-ui/ });
    expect(there.querySelector('path')!.getAttribute('d')).not.toBe(back.querySelector('path')!.getAttribute('d'));
    // The counts stand apart by more than a bubble's width, so neither hides the other.
    const y = (line: HTMLElement) => Number(line.querySelector('circle')!.getAttribute('cy'));
    expect(Math.abs(y(there) - y(back))).toBeGreaterThan(22);
    await userEvent.click(back);
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'quests', from: 'portals-db', to: 'portal-ui' });
    await userEvent.click(there);
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'quests', from: 'portal-ui', to: 'portals-db' });
  });
});

describe('the map in its viewport', () => {
  beforeEach(() => { measured.width = 900; });

  it('is a named region and a Tab stop, with the size in the toolbar and no sizing menu in the map\'s own words', async () => {
    render(map(TOPOLOGY));
    const region = screen.getByRole('region', { name: 'the workspace map' });
    expect(region).toHaveAttribute('tabindex', '0');
    expect(region).toHaveAttribute('aria-roledescription', 'a map: drag or the arrows to move, Ctrl+wheel to zoom');
    // Opens as large as fits, never below four fifths of the type scale.
    expect(scaleOf()).toBeGreaterThanOrEqual(0.8);
    expect(scaleOf()).toBeLessThanOrEqual(1);
    screen.getByRole('button', { name: /^Sizing: \d+%$/ }).focus();
    await userEvent.keyboard('{Enter}');
    expect(screen.getAllByRole('menuitem').map((act) => act.textContent)).toEqual(['Zoom in', 'Zoom out', 'Show the whole mapShift+1', 'Zoom to the selectionShift+2', 'Actual sizeShift+0']);
    await userEvent.keyboard('{Escape}');
  });

  it('zooms from its keys on the region, and shows the whole on 0', async () => {
    render(map(TOPOLOGY));
    const start = scaleOf();
    screen.getByRole('region', { name: 'the workspace map' }).focus();
    await userEvent.keyboard('+');
    expect(scaleOf()).toBeGreaterThan(start);
    await userEvent.keyboard('0');
    expect(scaleOf()).toBeLessThanOrEqual(start);
  });

  it('zooms continuously on a pinch: forty small deltas are under 2x (trap 5)', () => {
    render(map(TOPOLOGY));
    const region = screen.getByRole('region', { name: 'the workspace map' });
    const start = scaleOf();
    for (let step = 0; step < 40; step += 1) act(() => { region.dispatchEvent(new WheelEvent('wheel', { bubbles: true, cancelable: true, ctrlKey: true, deltaY: -5 })); });
    expect(scaleOf() / start).toBeGreaterThan(1.3);
    expect(scaleOf() / start).toBeLessThan(2);
  });

  it('follows the drawing as its data arrives, until the person moves the view', () => {
    // The shared findings arrive after the rest. One between two askers of one repository bows round the left, and the world takes in
    // the room it needs rather than keep the first frame.
    const asked: Topology = {
      ...TOPOLOGY,
      quests: [...TOPOLOGY.quests, { from: 'repo-00', to: 'portals-db', quests: [], open: 1, live: null }],
      knowledge: [],
    };
    const later: Topology = { ...asked, knowledge: [{ a: 'portal-ui', b: 'repo-00', groups: 1 }] };
    const left = () => Number(document.querySelector('[role="group"][aria-label="the workspace map"] > svg')!.getAttribute('viewBox')!.split(' ')[0]);
    const { rerender } = render(map(asked));
    const before = left();
    rerender(<Tooltip.Provider>{map(later)}</Tooltip.Provider>);
    expect(left()).toBeLessThan(before);
  });

  it('keeps the choice and the focus when a thirteenth repository turns the ring into layers', () => {
    const twelve: Topology = { ...TOPOLOGY, nodes: TOPOLOGY.nodes.slice(0, 11).concat(node('portal-ui')), quests: [], knowledge: [] };
    const thirteen: Topology = { ...twelve, nodes: [...twelve.nodes, node('one-more')] };
    const { rerender } = render(map(twelve, { kind: 'node', id: 'portal-ui' }));
    const card = screen.getByRole('button', { name: /^portal-ui, / });
    card.focus();
    expect(document.querySelector('[data-flow-node="portal-ui"]')).toBe(card);
    rerender(<Tooltip.Provider>{map(thirteen, { kind: 'node', id: 'portal-ui' })}</Tooltip.Provider>);
    expect(screen.getByRole('button', { name: /^portal-ui, / })).toBe(card);
    expect(card).toHaveFocus();
    expect(card).toHaveAttribute('aria-pressed', 'true');
  });
});

describe('the map as a ring', () => {
  const RING: Topology = {
    ...TOPOLOGY,
    nodes: [node('engine', { open: 1, working: true }), node('game', { open: 1 })],
    quests: [{ from: 'game', to: 'engine', quests: [], open: 1, live: null }],
    knowledge: [],
  };

  it('is fitted, with no sizing, and its repositories and lines are the same stops', () => {
    measured.width = 900;
    render(map(RING));
    expect(screen.queryByRole('button', { name: /^Sizing/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'engine, 1 open, a session working now' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /from game to engine/ })).toBeInTheDocument();
    expect(scaleOf()).toBeLessThanOrEqual(1);
  });
});

describe('the map as a list', () => {
  it('reads every repository, then each kind of line, named as the drawing names them, a pick choosing and a second releasing', async () => {
    const onSelect = vi.fn();
    const { rerender } = render(map(TOPOLOGY, null, onSelect));
    await userEvent.click(screen.getByRole('button', { name: 'Show as list' }));
    const list = document.querySelector<HTMLElement>('[data-canvas-list]')!;
    expect(within(list).getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent)).toEqual([
      expect.stringContaining('Repositories'), expect.stringContaining('Quests between repositories'), expect.stringContaining('The same thing learned twice'),
    ]);
    expect(within(list).getAllByRole('listitem')).toHaveLength(TOPOLOGY.nodes.length + 2);
    await userEvent.click(within(list).getByRole('button', { name: /from portal-ui to portals-db/ }));
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'quests', from: 'portal-ui', to: 'portals-db' });
    rerender(<Tooltip.Provider>{map(TOPOLOGY, { kind: 'quests', from: 'portal-ui', to: 'portals-db' }, onSelect)}</Tooltip.Provider>);
    await userEvent.click(within(document.querySelector<HTMLElement>('[data-canvas-list]')!).getByRole('button', { name: /from portal-ui to portals-db/ }));
    expect(onSelect).toHaveBeenLastCalledWith(null);
    await userEvent.click(screen.getByRole('button', { name: 'Show as graph' }));
    expect(screen.getByRole('group', { name: 'the workspace map' })).toBeInTheDocument();
  });
});

describe('a choice standing forward (MAPUX1b)', () => {
  const card = (id: string) => document.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!.firstElementChild!;

  it('keeps the chosen repository and those it shares a line with at full ink and steps the others back', () => {
    render(map(TOPOLOGY, { kind: 'node', id: 'portal-ui' }));
    expect(card('portal-ui').className).not.toContain('opacity-40');
    expect(card('portals-db').className).not.toContain('opacity-40');
    expect(card('repo-05').className).toContain('opacity-40');
  });

  it('steps nothing back with nothing chosen, and stands a chosen line\'s two ends forward', () => {
    const { rerender } = render(map(TOPOLOGY));
    expect(card('repo-05').className).not.toContain('opacity-40');
    rerender(<Tooltip.Provider>{map(TOPOLOGY, { kind: 'quests', from: 'portal-ui', to: 'portals-db' })}</Tooltip.Provider>);
    expect(card('portal-ui').className).not.toContain('opacity-40');
    expect(card('repo-05').className).toContain('opacity-40');
  });
});
