import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRef, useRef } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { Menu } from '../ui';
import { zoomBy, zoomLevel, zoomWheel } from '../zoom';
import { Canvas, type CanvasHandle, type CanvasMemory, type CanvasMinimap, type CanvasOptions, type CanvasSelection } from './Canvas';
import { memoryKey } from './viewMemory';
import type { Connect } from './useConnection';
import type { WorldRead } from './worldBoxes';

// CANVAS1a (D224; the canvas design §2.4, §5, traps 2-5 and 13): the viewport a placed world stands in. jsdom lays nothing out, so the
// world is handed to the canvas as its ink would measure it, and each element's rect is stood in for as the transform would place it.

const OPTIONS: CanvasOptions = { mode: 'display', zoom: { min: 0.5, max: 2 }, fit: 'Show the whole workflow', moves: 'a workflow: drag to move' };

/** A world of three nodes, 2000 by 2000 world units, the second far to the right. */
const WORLD: WorldRead = {
  k: 1, width: 2000, height: 2000, ways: new Map(), ports: new Map(), sections: [],
  nodes: new Map([['a', { x: 900, y: 40, w: 200, h: 80 }], ['b', { x: 1700, y: 600, w: 200, h: 80 }], ['c', { x: 100, y: 1800, w: 200, h: 80 }]]),
};

function Drawn({ handle, active = true, options = OPTIONS, selected = null, selection = null, connect = null, remember = null, minimap = null }: {
  handle?: React.Ref<CanvasHandle>; active?: boolean; options?: CanvasOptions; selected?: string | null;
  selection?: CanvasSelection | null; connect?: Connect | null; remember?: CanvasMemory | null; minimap?: CanvasMinimap | null;
}) {
  const pane = useRef<HTMLDivElement>(null);
  return (
    <Tooltip.Provider>
      <Canvas ref={handle} active={active} options={options} label="How the steps go on" width={800} rem={16} pane={pane}
        paneProps={{ 'data-flow-pane': '' }} tools={<button type="button">Show as list</button>} showTools entry="a" selected={selected}
        selection={selection} connect={connect} remember={remember} minimap={minimap}>
        <div role="list">
          {[...WORLD.nodes.keys()].map((id) => (
            <div key={id} role="listitem" data-flow-node={id} tabIndex={id === 'a' ? 0 : -1}>
              {id}
              <input aria-label={`${id}'s name`} />
              <span data-flow-port={`${id}:yes`}>yes</span>
            </div>
          ))}
        </div>
      </Canvas>
    </Tooltip.Provider>
  );
}

const region = () => screen.getByRole('region', { name: 'How the steps go on' });
const world = () => region().querySelector<HTMLElement>('[data-canvas-world]')!;
const scale = () => Number(world().getAttribute('data-canvas-scale'));
/** The view the world's transform says: the world point at the top left, and the scale. */
const view = () => {
  const [, tx, ty] = /translate\((-?[\d.e-]+)px, (-?[\d.e-]+)px\)/.exec(world().style.transform) ?? [];
  const k = scale();
  return { x: -Number(tx) / k + 0, y: -Number(ty) / k + 0, k };
};

/** Every element's rect as the world's transform places it: a node's from its world box, the region at the page's top left. */
function placeByView() {
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    const id = this.getAttribute('data-flow-node');
    const box = id ? WORLD.nodes.get(id) : this.hasAttribute('data-canvas-world') ? { x: 0, y: 0, w: 2000, h: 2000 } : null;
    if (!box) return { left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
    const { x, y, k } = view();
    return { left: (box.x - x) * k, top: (box.y - y) * k, width: box.w * k, height: box.h * k, right: 0, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
}

const wheel = (init: WheelEventInit) => {
  const event = new WheelEvent('wheel', { bubbles: true, cancelable: true, clientX: 400, clientY: 100, ...init });
  act(() => { region().dispatchEvent(event); });
  return event;
};

let user: ReturnType<typeof userEvent.setup>;

beforeEach(() => { user = userEvent.setup(); });

afterEach(async () => {
  vi.restoreAllMocks();
  zoomBy('reset');
  await i18n.changeLanguage('en');
});

describe('a canvas not offered', () => {
  it('is the pane as it was, with the tools above it and no region', () => {
    render(<Drawn active={false} />);
    expect(screen.queryByRole('region')).toBeNull();
    expect(document.querySelector('[data-flow-pane]')).not.toBeNull();
    expect(document.querySelector('[data-canvas-world]')).toBeNull();
    expect(screen.getByRole('button', { name: 'Show as list' })).toBeInTheDocument();
  });
});

describe('the viewport', () => {
  it('is a named region with how to move it, its world at 100%, and the size in the toolbar beside the tools', () => {
    render(<Drawn />);
    expect(region()).toHaveAttribute('aria-roledescription', 'a workflow: drag to move');
    expect(region()).toHaveAttribute('data-flow-pane');
    expect(scale()).toBe(1);
    expect(screen.getByRole('button', { name: 'Sizing: 100%' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show as list' })).toBeInTheDocument();
  });

  it('opens at 100% on its entry, and follows the drawing until the person moves it', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    // The entry's middle at the viewport's middle, the top of the world at the top.
    expect(view().k).toBe(1);
    expect(view().x).toBe(600);
  });
});

describe('the wheel', () => {
  it('with Ctrl zooms about the pointer continuously, its default prevented, and leaves the window\'s zoom where it was (trap 4)', () => {
    const steps = vi.fn((direction: 1 | -1) => zoomBy(direction > 0 ? 'in' : 'out'));
    const hear = zoomWheel(steps);
    window.addEventListener('wheel', hear);
    try {
      const handle = createRef<CanvasHandle>();
      render(<Drawn handle={handle} />);
      act(() => handle.current!.measured(WORLD));
      const event = wheel({ ctrlKey: true, deltaY: -100 });
      expect(event.defaultPrevented).toBe(true);
      expect(scale()).toBeCloseTo(1.25, 6);
      expect(screen.getByRole('button', { name: 'Sizing: 125%' })).toBeInTheDocument();
      expect(steps).not.toHaveBeenCalled();
      expect(zoomLevel()).toBe(100);
      // At the bound the wheel is still the canvas's, so the window never zooms behind it.
      for (let turn = 0; turn < 10; turn += 1) wheel({ ctrlKey: true, deltaY: -100 });
      expect(scale()).toBe(2);
      expect(zoomLevel()).toBe(100);
    } finally {
      window.removeEventListener('wheel', hear);
    }
  });

  it('plain, pans while the world overflows, and is the page\'s at either edge (trap 3)', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const top = view().y;
    expect(wheel({ deltaY: -60 }).defaultPrevented).toBe(false);
    expect(view().y).toBe(top);
    const down = wheel({ deltaY: 60 });
    expect(down.defaultPrevented).toBe(true);
    expect(view().y).toBe(top + 60);
    for (let turn = 0; turn < 60; turn += 1) wheel({ deltaY: 60 });
    expect(wheel({ deltaY: 60 }).defaultPrevented).toBe(false);
  });

  it('over a node\'s own scrolling content is that content\'s first', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const inner = screen.getByRole('textbox', { name: "a's name" });
    inner.style.overflowY = 'auto';
    Object.defineProperty(inner, 'scrollHeight', { value: 400 });
    Object.defineProperty(inner, 'clientHeight', { value: 100 });
    const event = new WheelEvent('wheel', { bubbles: true, cancelable: true, deltaY: 60 });
    const before = view();
    act(() => { inner.dispatchEvent(event); });
    expect(event.defaultPrevented).toBe(false);
    expect(view()).toEqual(before);
  });
});

describe('the keys', () => {
  it('Shift+1 fits, Shift+0 returns to 100%, Shift+2 zooms to the selection, from any focus in the canvas', async () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} selected="b" />);
    act(() => handle.current!.measured(WORLD));
    act(() => { (document.querySelector('[data-flow-node="a"]') as HTMLElement).focus(); });
    await user.keyboard('{Shift>}!{/Shift}');
    expect(scale()).toBe(0.5);
    await user.keyboard('{Shift>})){/Shift}');
    expect(scale()).toBe(1);
    await user.keyboard('{Shift>}@{/Shift}');
    expect(scale()).toBe(1);
    // b centred, held to the world's right edge and its margin: 2000 - 800 + 16.
    expect(view().x).toBe(1216);
  });

  it('type their characters in a node\'s field (trap 13), and leave + to the page', async () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const field = screen.getByRole('textbox', { name: "a's name" });
    await user.click(field);
    await user.keyboard('{Shift>}!@){/Shift}+');
    expect(field).toHaveValue('!@)+');
    expect(scale()).toBe(1);
  });
});

describe('following the focus', () => {
  it('reveals a node walked to by the smallest pan, and every scroll stays 0 (trap 2)', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    placeByView();
    region().scrollLeft = 50;
    act(() => { (document.querySelector('[data-flow-node="b"]') as HTMLElement).focus({ preventScroll: true }); });
    // b's right edge, 1900, brought inside the 2rem margin of an 800 px viewport: the view's left at 1900 + 32 - 800.
    expect(view().x).toBe(1132);
    expect(view().k).toBe(1);
    expect(region().scrollLeft).toBe(0);
    expect(region().scrollTop).toBe(0);
  });
});

describe('a drag', () => {
  it('on the ground pans, never from a node or a port, and Escape puts it back', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const start = view();
    fireEvent.pointerDown(document.querySelector('[data-flow-port="a:yes"]')!, { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 200, clientY: 100, pointerId: 1 });
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(view()).toEqual(start);
    fireEvent.pointerDown(document.querySelector('[data-flow-node="a"]')!, { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 200, clientY: 100, pointerId: 1 });
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(view()).toEqual(start);

    fireEvent.pointerDown(region(), { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 200, clientY: 40, pointerId: 1 });
    expect(view()).toEqual({ x: start.x + 100, y: start.y + 60, k: 1 });
    expect(world().style.willChange).toBe('transform');
    fireEvent.keyDown(region(), { key: 'Escape' });
    expect(view()).toEqual(start);
    expect(world().style.willChange).toBe('');
  });

  it('with the middle button pans from anywhere, a node too', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const start = view();
    fireEvent.pointerDown(document.querySelector('[data-flow-node="a"]')!, { button: 1, clientX: 300, clientY: 100, pointerId: 2 });
    fireEvent.pointerMove(region(), { clientX: 250, clientY: 100, pointerId: 2 });
    fireEvent.pointerUp(region(), { pointerId: 2 });
    expect(view()).toEqual({ ...start, x: start.x + 50 });
  });
});

describe('a relayout', () => {
  const moved = (read: WorldRead, id: string, dx: number, dy: number): WorldRead => ({
    ...read, nodes: new Map([...read.nodes].map(([key, box]) => [key, key === id ? { ...box, x: box.x + dx, y: box.y + dy } : box])),
  });
  const onScreen = (id: string, read: WorldRead) => {
    const box = read.nodes.get(id)!;
    const { x, y, k } = view();
    return { x: (box.x - x) * k, y: (box.y - y) * k };
  };

  it('keeps the focused node where it stood on screen once the person has moved the view (trap 7)', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    wheel({ deltaY: 120 });
    act(() => { (document.querySelector('[data-flow-node="b"]') as HTMLElement).focus({ preventScroll: true }); });
    const before = onScreen('b', WORLD);
    const after = moved(WORLD, 'b', -150, 200);
    act(() => handle.current!.measured(after));
    expect(onScreen('b', after)).toEqual(before);
  });

  it('leaves the view as it was when the same world is measured again, moved or not: a refresh refits nothing (trap 17)', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    const opened = view();
    act(() => handle.current!.measured(WORLD));
    expect(view()).toEqual(opened);
    wheel({ deltaY: 120 });
    const panned = view();
    act(() => handle.current!.measured({ ...WORLD, nodes: new Map(WORLD.nodes) }));
    expect(view()).toEqual(panned);
  });
});

describe('what stands in the world', () => {
  it('opens a node\'s menu outside the transform, in the page\'s portal (trap 10)', async () => {
    render(
      <Tooltip.Provider>
        <Canvas active options={OPTIONS} label="How the steps go on" width={800} rem={16} pane={{ current: null }} paneProps={{}} tools={null}
          showTools={false} entry={null}>
          <div data-flow-node="a">
            <Menu.Root>
              <Menu.Trigger asChild><button type="button">Its acts</button></Menu.Trigger>
              <Menu.Content><Menu.Item onSelect={() => {}}>Copy</Menu.Item></Menu.Content>
            </Menu.Root>
          </div>
        </Canvas>
      </Tooltip.Provider>,
    );
    screen.getByRole('button', { name: 'Its acts' }).focus();
    await user.keyboard('{Enter}');
    const item = await screen.findByRole('menuitem', { name: 'Copy' });
    expect(item.closest('[data-canvas-world]')).toBeNull();
  });

  it('is never the spike: no @xyflow import under src/ and no graph dependency in the web package (trap 21)', () => {
    const sources = import.meta.glob('/src/**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true }) as Record<string, string>;
    const own = Object.entries(sources).filter(([path]) => !path.endsWith('Canvas.test.tsx'));
    expect(own.filter(([, text]) => /from ['"]@xyflow|import\(['"]@xyflow/.test(text)).map(([path]) => path)).toEqual([]);
    const manifest = JSON.parse(readFileSync(join(process.cwd(), 'package.json'), 'utf8')) as Record<string, Record<string, string> | undefined>;
    const named = [...Object.keys(manifest.dependencies ?? {}), ...Object.keys(manifest.devDependencies ?? {})];
    expect(named.filter((name) => /xyflow|reactflow|cytoscape|sigma|graphology|d3-zoom|elkjs|dagre/.test(name))).toEqual([]);
  });

  it('leaves no listener or observer behind across twenty opens and closes (trap 22)', () => {
    const added = vi.spyOn(HTMLElement.prototype, 'addEventListener');
    const removed = vi.spyOn(HTMLElement.prototype, 'removeEventListener');
    for (let open = 0; open < 20; open += 1) render(<Drawn />).unmount();
    // The region's own: React's root keeps its wheel listeners on the container a test renders into.
    const wheels = (spy: typeof added) => spy.mock.calls
      .filter(([type], index) => type === 'wheel' && (spy.mock.contexts[index] as HTMLElement).hasAttribute('data-flow-pane')).length;
    expect(wheels(added)).toBeGreaterThan(0);
    expect(wheels(removed)).toBe(wheels(added));
  });
});

describe('the sizing menu', () => {
  it('offers zoom in and out, the feature\'s fit, the selection and 100% with their keys, and the sizes within the bounds', async () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} options={{ ...OPTIONS, mode: 'edit' }} />);
    act(() => handle.current!.measured(WORLD));
    screen.getByRole('button', { name: 'Sizing: 100%' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('menuitem', { name: /Show the whole workflow/ })).toHaveTextContent('Shift+1');
    expect(screen.getByRole('menuitem', { name: /Zoom to the selection/ })).toHaveAttribute('data-disabled');
    expect(screen.getAllByRole('menuitemradio').map((each) => each.textContent)).toEqual(['100%', '200%']);
    await user.click(screen.getByRole('menuitem', { name: /Show the whole workflow/ }));
    // Edit is floored at 80%, so the ＋ and the ports stay usable.
    expect(scale()).toBe(0.8);
  });
});

// CANVAS1b (D224; the canvas design §2.4, §2.5): the ground, the box, and the connection line.

const selectionOf = (over: Partial<CanvasSelection> = {}): CanvasSelection => ({ many: true, onGround: vi.fn(), onBox: vi.fn(), ...over });

describe('the ground and the box', () => {
  it('a press on the ground that does not travel is the feature\'s ground press, and a press on a node or a drag is not', () => {
    const selection = selectionOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} selection={selection} />);
    act(() => handle.current!.measured(WORLD));
    fireEvent.pointerDown(document.querySelector('[data-flow-node="a"]')!, { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(selection.onGround).not.toHaveBeenCalled();
    fireEvent.pointerDown(region(), { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 250, clientY: 100, pointerId: 1 });
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(selection.onGround).not.toHaveBeenCalled();
    fireEvent.pointerDown(region(), { button: 0, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(selection.onGround).toHaveBeenCalledOnce();
  });

  it('Shift+drag on the ground, where many may be chosen, draws a box and chooses what it touches, in reading order, and pans nothing', () => {
    const selection = selectionOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} selection={selection} />);
    act(() => handle.current!.measured(WORLD));
    const start = view();
    // Screen points that cover the world from (850, 0) to (1950, 700): the first node and the second.
    const at = (x: number, y: number) => ({ clientX: (x - start.x) * start.k, clientY: (y - start.y) * start.k });
    fireEvent.pointerDown(region(), { button: 0, shiftKey: true, pointerId: 1, ...at(850, 0) });
    fireEvent.pointerMove(region(), { shiftKey: true, pointerId: 1, ...at(1950, 700) });
    expect(region().querySelector('[data-canvas-box]')).not.toBeNull();
    fireEvent.pointerUp(region(), { pointerId: 1, ...at(1950, 700) });
    expect(selection.onBox).toHaveBeenCalledWith(['a', 'b']);
    expect(selection.onGround).not.toHaveBeenCalled();
    expect(view()).toEqual(start);
    expect(region().querySelector('[data-canvas-box]')).toBeNull();
  });

  it('is a pan, not a box, where only one may be chosen; and Escape gives a box up', () => {
    const one = selectionOf({ many: false });
    const handle = createRef<CanvasHandle>();
    const { unmount } = render(<Drawn handle={handle} selection={one} />);
    act(() => handle.current!.measured(WORLD));
    const start = view();
    fireEvent.pointerDown(region(), { button: 0, shiftKey: true, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 200, clientY: 100, pointerId: 1 });
    expect(view().x).toBe(start.x + 100);
    expect(one.onBox).not.toHaveBeenCalled();
    unmount();

    const many = selectionOf();
    const second = createRef<CanvasHandle>();
    render(<Drawn handle={second} selection={many} />);
    act(() => second.current!.measured(WORLD));
    fireEvent.pointerDown(region(), { button: 0, shiftKey: true, clientX: 300, clientY: 100, pointerId: 1 });
    fireEvent.pointerMove(region(), { clientX: 200, clientY: 160, pointerId: 1 });
    expect(region().querySelector('[data-canvas-box]')).not.toBeNull();
    fireEvent.keyDown(region(), { key: 'Escape' });
    expect(region().querySelector('[data-canvas-box]')).toBeNull();
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(many.onBox).not.toHaveBeenCalled();
  });
});

describe('the connection line', () => {
  const connectOf = () => ({
    from: (target: Element) => {
      const at = target.closest('[data-flow-port]')?.getAttribute('data-flow-port');
      return at ? { id: at.split(':')[0]!, port: at.split(':')[1]! } : null;
    },
    onStart: vi.fn(), can: (id: string) => id === 'b', onConnect: vi.fn(), onGround: vi.fn(), onCancel: vi.fn(),
  }) satisfies Connect;
  const port = () => document.querySelector<HTMLElement>('[data-flow-port="a:yes"]')!;
  const node = (id: string) => document.querySelector<HTMLElement>(`[data-flow-node="${id}"]`)!;
  const line = () => document.querySelector('[data-connection-line]');
  /** What is under the pointer, which jsdom cannot say. */
  const under = (element: Element | null) => { (document as unknown as { elementFromPoint: unknown }).elementFromPoint = () => element; };
  afterEach(() => { delete (document as unknown as { elementFromPoint?: unknown }).elementFromPoint; });

  const dragFrom = (to: Element | null, x = 400, y = 300) => {
    fireEvent.pointerDown(port(), { button: 0, clientX: 120, clientY: 215, pointerId: 1 });
    under(to);
    fireEvent.pointerMove(region(), { clientX: x, clientY: y, pointerId: 1 });
  };

  it('begins the way at the press on a port, draws a rubber line to the pointer from inside the port\'s box, and pans nothing', () => {
    const connect = connectOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} connect={connect} />);
    act(() => handle.current!.measured(WORLD));
    const start = view();
    vi.spyOn(port(), 'getBoundingClientRect').mockReturnValue({ left: 100, top: 200, width: 40, height: 20, right: 140, bottom: 220, x: 100, y: 200, toJSON: () => ({}) } as DOMRect);
    fireEvent.pointerDown(port(), { button: 0, clientX: 120, clientY: 215, pointerId: 1 });
    expect(connect.onStart).toHaveBeenCalledWith('a', 'yes');
    expect(line()).toBeNull();
    under(null);
    fireEvent.pointerMove(region(), { clientX: 400, clientY: 300, pointerId: 1 });
    const drawn = line()!.querySelector('line')!;
    expect(Number(drawn.getAttribute('x1'))).toBeGreaterThanOrEqual(100);
    expect(Number(drawn.getAttribute('x1'))).toBeLessThanOrEqual(140);
    expect(Number(drawn.getAttribute('y1'))).toBeGreaterThanOrEqual(200);
    expect(Number(drawn.getAttribute('y1'))).toBeLessThanOrEqual(220);
    expect([drawn.getAttribute('x2'), drawn.getAttribute('y2')]).toEqual(['400', '300']);
    expect(view()).toEqual(start);
  });

  it('outlines the step under the pointer only where it can take the way, and commits one intent on a valid release', () => {
    const connect = connectOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} connect={connect} />);
    act(() => handle.current!.measured(WORLD));
    dragFrom(node('c'));
    expect(document.querySelector('[data-connection-target]')).toBeNull();
    under(node('b'));
    fireEvent.pointerMove(region(), { clientX: 410, clientY: 310, pointerId: 1 });
    expect(document.querySelector('[data-connection-target]')).not.toBeNull();
    fireEvent.pointerUp(region(), { clientX: 410, clientY: 310, pointerId: 1 });
    expect(connect.onConnect).toHaveBeenCalledOnce();
    expect(connect.onConnect).toHaveBeenCalledWith('b');
    expect(line()).toBeNull();
    expect(connect.onGround).not.toHaveBeenCalled();
  });

  it('does nothing on a release over a step that cannot take it, or over its own port, and gives open ground to the feature', () => {
    const connect = connectOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} connect={connect} />);
    act(() => handle.current!.measured(WORLD));
    dragFrom(node('c'));
    fireEvent.pointerUp(region(), { clientX: 400, clientY: 300, pointerId: 1 });
    dragFrom(port());
    fireEvent.pointerUp(region(), { clientX: 400, clientY: 300, pointerId: 1 });
    expect(connect.onConnect).not.toHaveBeenCalled();
    expect(connect.onGround).not.toHaveBeenCalled();
    dragFrom(region());
    fireEvent.pointerUp(region(), { clientX: 400, clientY: 300, pointerId: 1 });
    expect(connect.onGround).toHaveBeenCalledOnce();
    expect(connect.onCancel).not.toHaveBeenCalled();
  });

  it('is given up by Escape and a cancelled pointer, each once, and a lost capture after a release gives nothing up', () => {
    const connect = connectOf();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} connect={connect} />);
    act(() => handle.current!.measured(WORLD));
    dragFrom(null);
    expect(line()).not.toBeNull();
    fireEvent.keyDown(port(), { key: 'Escape' });
    expect(line()).toBeNull();
    expect(connect.onCancel).toHaveBeenCalledTimes(1);
    fireEvent.pointerUp(region(), { pointerId: 1 });
    expect(connect.onGround).not.toHaveBeenCalled();

    dragFrom(null);
    fireEvent.pointerCancel(region(), { pointerId: 1 });
    expect(line()).toBeNull();
    expect(connect.onCancel).toHaveBeenCalledTimes(2);

    dragFrom(node('b'));
    fireEvent.pointerUp(region(), { clientX: 400, clientY: 300, pointerId: 1 });
    fireEvent.lostPointerCapture(region(), { pointerId: 1 });
    expect(connect.onCancel).toHaveBeenCalledTimes(2);
    expect(connect.onConnect).toHaveBeenCalledTimes(1);
  });

  it('pans the view at the viewport\'s edge while the pointer rests there, and stops when it is released', () => {
    vi.useFakeTimers({ toFake: ['requestAnimationFrame', 'cancelAnimationFrame'] });
    try {
      const connect = connectOf();
      const handle = createRef<CanvasHandle>();
      render(<Drawn handle={handle} connect={connect} />);
      act(() => handle.current!.measured(WORLD));
      vi.spyOn(region(), 'getBoundingClientRect').mockReturnValue({ left: 0, top: 0, width: 800, height: 400, right: 800, bottom: 400, x: 0, y: 0, toJSON: () => ({}) } as DOMRect);
      const start = view();
      dragFrom(null, 799, 200);
      act(() => { vi.advanceTimersByTime(100); });
      expect(view().x).toBeGreaterThan(start.x);
      fireEvent.pointerUp(region(), { clientX: 799, clientY: 200, pointerId: 1 });
      const stopped = view();
      act(() => { vi.advanceTimersByTime(100); });
      expect(view()).toEqual(stopped);
    } finally {
      vi.useRealTimers();
    }
  });

  it('works where the viewport is not offered: the press and the drop, with no line drawn', () => {
    const connect = connectOf();
    render(<Drawn active={false} connect={connect} />);
    fireEvent.pointerDown(port(), { button: 0, clientX: 120, clientY: 215, pointerId: 1 });
    expect(connect.onStart).toHaveBeenCalledWith('a', 'yes');
    fireEvent.pointerUp(node('b'), { pointerId: 1 });
    expect(connect.onConnect).toHaveBeenCalledWith('b');
    expect(line()).toBeNull();
  });
});

// CANVAS1d (D224; the canvas design §4, trap 18): the minimap, switched in the sizing menu and remembered, and the viewer's place.

describe('the minimap', () => {
  const boxes = [...WORLD.nodes.values()];
  afterEach(() => localStorage.clear());

  it('stands where the world is past twice the viewport, its rectangle the view, and is switched in the sizing menu and remembered', async () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} minimap={{ boxes }} />);
    act(() => handle.current!.measured(WORLD));
    const map = document.querySelector<HTMLElement>('[data-canvas-minimap]');
    expect(map).not.toBeNull();
    expect(map).toHaveAttribute('aria-hidden', 'true');
    const rect = document.querySelector<HTMLElement>('[data-canvas-minimap-view]')!;
    // 160 px for the world's 2000: the viewport's 800 at 100% is 64 px wide.
    expect(Number.parseFloat(rect.style.width)).toBeCloseTo(64, 1);
    expect(Number.parseFloat(rect.style.left)).toBeCloseTo(view().x * 0.08, 1);
    screen.getByRole('button', { name: 'Sizing: 100%' }).focus();
    await user.keyboard('{Enter}');
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'Minimap' }));
    expect(document.querySelector('[data-canvas-minimap]')).toBeNull();
    expect(localStorage.getItem('daoris.canvas.minimap')).toBe('0');
  });

  it('is not offered to a drawing that gave none', () => {
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} />);
    act(() => handle.current!.measured(WORLD));
    expect(document.querySelector('[data-canvas-minimap]')).toBeNull();
  });
});

describe('the viewer\'s place', () => {
  const memory = (over: Partial<CanvasMemory> = {}): CanvasMemory => ({ feature: 'code', key: 'repo', identity: 'same', ...over });
  const kept = (identity: string) => localStorage.setItem(memoryKey('code', 'repo'), JSON.stringify({
    identity, view: { x: 300, y: 500, k: 1.5 }, anchor: { id: 'a', group: null, screen: { x: 100, y: 60 } }, selected: 'c',
  }));
  afterEach(() => localStorage.clear());

  it('opens an unchanged graph exactly where it was left', () => {
    kept('same');
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} remember={memory()} />);
    act(() => handle.current!.measured(WORLD));
    expect(view()).toEqual({ x: 300, y: 500, k: 1.5 });
  });

  it('keeps the surviving anchor where it stood on screen under a changed graph, and says the selection is gone', () => {
    kept('older');
    const onGone = vi.fn();
    const handle = createRef<CanvasHandle>();
    render(<Drawn handle={handle} remember={memory({ onGone })} />);
    const without = { ...WORLD, nodes: new Map([...WORLD.nodes].filter(([id]) => id !== 'c')) };
    act(() => handle.current!.measured(without));
    // a's box is at x 900, y 40: its top left at (100, 60) on screen at 1.5.
    expect(view().k).toBe(1.5);
    expect((900 - view().x) * 1.5).toBeCloseTo(100, 3);
    expect((40 - view().y) * 1.5).toBeCloseTo(60, 3);
    expect(onGone).toHaveBeenCalledTimes(1);
  });

  it('claims nothing when nothing of the old graph survives, and keeps the place once the view has held still', () => {
    kept('older');
    const handle = createRef<CanvasHandle>();
    vi.useFakeTimers();
    try {
      render(<Drawn handle={handle} remember={memory()} />);
      act(() => handle.current!.measured({ ...WORLD, nodes: new Map([['z', { x: 0, y: 0, w: 200, h: 80 }]]) }));
      expect(view().k).toBe(1);
      act(() => { handle.current!.setView({ x: 120, y: 80, k: 1 }); });
      act(() => { vi.advanceTimersByTime(500); });
      expect(JSON.parse(localStorage.getItem(memoryKey('code', 'repo'))!)).toMatchObject({ identity: 'same', view: { x: 120, y: 80, k: 1 } });
    } finally {
      vi.useRealTimers();
    }
  });
});
