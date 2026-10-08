import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { MapNode, Topology } from './topology';
import { LayeredMap } from './LayeredMap';

// MAP4: a circle too big for a ring — cards in layers, a viewport that pans and zooms, and a search.

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

describe('the layered map', () => {
  it('draws every repository as a card a keyboard reaches, named for what it holds', () => {
    render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={vi.fn()} />);
    expect(screen.getByRole('button', { name: /^portal-ui, / })).toHaveAttribute('tabindex', '0');
    expect(screen.getAllByRole('button', { name: /^repo-\d\d/ })).toHaveLength(20);
    expect(screen.getByRole('button', { name: /from portal-ui to portals-db/ })).toBeInTheDocument();
    // What nothing connects is set apart, and says so.
    expect(screen.getByText('No line drawn to these')).toBeInTheDocument();
  });

  it('finds a repository as it is typed, and chooses the first on Enter', () => {
    const onSelect = vi.fn();
    render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={onSelect} />);
    const find = screen.getByRole('textbox', { name: 'find a repository' });

    fireEvent.change(find, { target: { value: 'portal' } });
    expect(screen.getByText('2 matches')).toBeInTheDocument();
    // The rest step back, so the matches stand out.
    expect(screen.getByRole('button', { name: /^repo-00/ }).getAttribute('class')).toContain('opacity-25');
    fireEvent.keyDown(find, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith({ kind: 'node', id: 'portal-ui' });
  });

  it('sizes from a menu behind the size itself, with no arrow standing for a size', async () => {
    render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={vi.fn()} />);
    const svg = screen.getByRole('group', { name: 'the workspace map' });
    const width = () => Number(svg.getAttribute('viewBox')!.split(' ')[2]);
    const start = width();
    const sizing = async (item: string, role: 'menuitem' | 'menuitemradio' = 'menuitem') => {
      screen.getByRole('button', { name: /^Sizing: \d+%$/ }).focus();
      await userEvent.keyboard('{Enter}');
      await userEvent.click(screen.getByRole(role, { name: new RegExp(`^${item}`) }));
    };

    await sizing('Zoom in');
    expect(width()).toBeLessThan(start);
    await sizing('Zoom out');
    await sizing('Zoom out');
    expect(width()).toBeGreaterThan(start);
    // A size by name, ticked in the menu once it is the size, and said on the button.
    await sizing('100%', 'menuitemradio');
    expect(screen.getByRole('button', { name: 'Sizing: 100%' })).toHaveTextContent('100%');
    screen.getByRole('button', { name: 'Sizing: 100%' }).focus();
    await userEvent.keyboard('{Enter}');
    expect(screen.getByRole('menuitemradio', { name: '100%' }).querySelector('svg')).not.toBeNull();
    // Each keyed way says its key.
    expect(screen.getByRole('menuitem', { name: /^Show the whole map/ })).toHaveTextContent('0');
    await userEvent.keyboard('{Escape}');
  });

  /**
   * UXFIX1b: the sizes by name are one choice among three, so the one the map is at is said as well as drawn: a radio
   * row each, in one group named for what it chooses, `aria-checked` on the size now and on none at a size between
   * them. The ways that change the size by a step, or fit it, are acts and stay plain items.
   */
  it('says which size by name the map is at, as one choice among the three', async () => {
    render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={vi.fn()} />);
    const open = async () => {
      screen.getByRole('button', { name: /^Sizing: \d+%$/ }).focus();
      await userEvent.keyboard('{Enter}');
    };
    const checked = () => within(screen.getByRole('group', { name: 'Size' })).getAllByRole('menuitemradio')
      .map((size) => `${size.textContent} ${size.getAttribute('aria-checked')}`);

    await open();
    expect(screen.getAllByRole('menuitem').map((act) => act.textContent))
      .toEqual(['Zoom in+', 'Zoom out-', 'Show the whole map0']);
    await userEvent.click(screen.getByRole('menuitemradio', { name: '200%' }));
    await open();
    expect(checked()).toEqual(['50% false', '100% false', '200% true']);

    // A step off a size by name leaves none of the three ticked.
    await userEvent.click(screen.getByRole('menuitem', { name: /^Zoom out/ }));
    await open();
    expect(checked()).toEqual(['50% false', '100% false', '200% false']);
    await userEvent.keyboard('{Escape}');
  });

  it('zooms from its keys, and shows the whole on 0', () => {
    render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={vi.fn()} />);
    const svg = screen.getByRole('group', { name: 'the workspace map' });
    const width = () => Number(svg.getAttribute('viewBox')!.split(' ')[2]);
    const start = width();
    const region = screen.getByRole('region', { name: 'the workspace map' });
    fireEvent.keyDown(region, { key: '+' });
    expect(width()).toBeLessThan(start);
    fireEvent.keyDown(region, { key: '0' });
    // Fit shows it all: the viewBox holds the whole frame.
    expect(width()).toBeGreaterThanOrEqual(start);
  });

  it('draws a pair asked both ways as two lines apart, each with its own count in view', () => {
    const both: Topology = {
      ...TOPOLOGY,
      quests: [...TOPOLOGY.quests, { from: 'portals-db', to: 'portal-ui', quests: [], open: 1, live: null }],
    };
    render(<LayeredMap topology={both} selected={null} onSelect={vi.fn()} />);
    const there = screen.getByRole('button', { name: /from portal-ui to portals-db/ });
    const back = screen.getByRole('button', { name: /from portals-db to portal-ui/ });
    expect(there.querySelector('path')!.getAttribute('d')).not.toBe(back.querySelector('path')!.getAttribute('d'));
    // The counts stand apart by more than a bubble's width, so neither hides the other.
    const y = (line: HTMLElement) => Number(line.querySelector('circle')!.getAttribute('cy'));
    expect(Math.abs(y(there) - y(back))).toBeGreaterThan(22);
  });

  it('follows the drawing as its data arrives, until the person moves the view', () => {
    // The shared findings arrive after the rest. One between two askers of one repository bows round
    // the left, and the view must take in the room it needs rather than keep the first frame.
    const asked: Topology = {
      ...TOPOLOGY,
      quests: [...TOPOLOGY.quests, { from: 'repo-00', to: 'portals-db', quests: [], open: 1, live: null }],
      knowledge: [],
    };
    const later: Topology = { ...asked, knowledge: [{ a: 'portal-ui', b: 'repo-00', groups: 1 }] };
    const x = () => Number(screen.getByRole('group', { name: 'the workspace map' }).getAttribute('viewBox')!.split(' ')[0]);
    const view = (topology: Topology) => <Tooltip.Provider><LayeredMap topology={topology} selected={null} onSelect={vi.fn()} /></Tooltip.Provider>;

    const { rerender } = rtlRender(view(asked));
    const before = x();
    rerender(view(later));
    expect(x()).toBeLessThan(before);

    // Once the person has moved it, new data leaves the view where they put it.
    rerender(view(asked));
    const region = screen.getByRole('region', { name: 'the workspace map' });
    fireEvent.keyDown(region, { key: 'ArrowRight' });
    const theirs = x();
    rerender(view(later));
    expect(x()).toBe(theirs);
  });

  it('chooses and releases as the ring does: a second press, or Escape', () => {
    const onSelect = vi.fn();
    const { rerender } = render(<LayeredMap topology={TOPOLOGY} selected={null} onSelect={onSelect} />);
    fireEvent.click(screen.getByRole('button', { name: /^portal-ui, / }));
    expect(onSelect).toHaveBeenLastCalledWith({ kind: 'node', id: 'portal-ui' });

    rerender(<Tooltip.Provider><LayeredMap topology={TOPOLOGY} selected={{ kind: 'node', id: 'portal-ui' }} onSelect={onSelect} /></Tooltip.Provider>);
    fireEvent.keyDown(screen.getByRole('region', { name: 'the workspace map' }), { key: 'Escape' });
    expect(onSelect).toHaveBeenLastCalledWith(null);
  });
});
