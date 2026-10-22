import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../locales/i18n';
import { setZoom, ZOOM_KEY, zoomLevel } from '../design/zoom';
import { LayeredMap } from '../map/LayeredMap';
import type { MapNode, Topology } from '../map/topology';
import { useZoomKeys, useZoomWheel } from './windowZoom';

// ZOOM1 (the UI/UX design §3.11): Ctrl+wheel and the keys step the window's zoom, the engine's own zoom never beside it, and a
// surface that zooms itself (the Map) keeps its own Ctrl+wheel.

function Window({ wheel = true, keys = false, children }: { wheel?: boolean; keys?: boolean; children?: React.ReactNode }) {
  useZoomWheel(wheel);
  useZoomKeys(keys);
  return <Tooltip.Provider><main><p>content</p>{children}</main></Tooltip.Provider>;
}

const node = (id: string): MapNode => ({ id, owns: [], accepts: [], open: 0, working: false, parked: false, sessions: 0 });
const TOPOLOGY: Topology = {
  nodes: [node('engine'), node('game')],
  quests: [{ from: 'game', to: 'engine', quests: [], open: 1, live: null }],
  asks: [], chains: [], depends: [], knowledge: [], outside: 0, circles: 1,
};

describe("the window's zoom, by the wheel and the keys", () => {
  beforeEach(() => {
    window.localStorage.removeItem(ZOOM_KEY);
    setZoom(100);
  });
  afterEach(() => cleanup());

  it('steps in and out with Ctrl and the wheel, and takes the wheel from the engine', () => {
    render(<Window />);
    const wheel = (deltaY: number, ctrlKey = true) => fireEvent.wheel(screen.getByText('content'), { deltaY, ctrlKey });
    expect(wheel(-100)).toBe(false);
    expect(zoomLevel()).toBe(110);
    wheel(100);
    wheel(100);
    expect(zoomLevel()).toBe(90);
    // A plain wheel scrolls, as it always did.
    expect(wheel(-100, false)).toBe(true);
    expect(zoomLevel()).toBe(90);
    expect(window.localStorage.getItem(ZOOM_KEY)).toBe('90');
  });

  it('hears no wheel where the window has no zoom of its own (a browser)', () => {
    render(<Window wheel={false} />);
    fireEvent.wheel(screen.getByText('content'), { deltaY: -100, ctrlKey: true });
    expect(zoomLevel()).toBe(100);
  });

  it('leaves the Map its own Ctrl+wheel: the map zooms, the window does not', () => {
    render(<Window><LayeredMap topology={TOPOLOGY} selected={null} onSelect={vi.fn()} /></Window>);
    const svg = screen.getByRole('group', { name: 'the workspace map' });
    const width = () => Number(svg.getAttribute('viewBox')!.split(' ')[2]);
    const before = width();

    fireEvent.wheel(svg, { deltaY: -100, ctrlKey: true });
    expect(width()).toBeLessThan(before);
    expect(zoomLevel()).toBe(100);

    // Off the map, the same wheel is the window's.
    fireEvent.wheel(screen.getByText('content'), { deltaY: -100, ctrlKey: true });
    expect(zoomLevel()).toBe(110);
  });

  /** A window with no menu bar of its own (a detached session, the monitor) hears the zoom's keys from the same table. */
  it("takes the zoom's keys where it is asked to: in, out, and back to 100%", () => {
    render(<Window keys />);
    const key = (init: KeyboardEventInit) => fireEvent.keyDown(window, { ctrlKey: true, ...init });
    key({ key: '=', code: 'Equal' });
    key({ key: '+', code: 'Equal', shiftKey: true });
    expect(zoomLevel()).toBe(125);
    key({ key: '-', code: 'Minus' });
    expect(zoomLevel()).toBe(110);
    key({ key: '0', code: 'Digit0' });
    expect(zoomLevel()).toBe(100);
    // Any other key is left where it was going.
    expect(key({ key: 'b', code: 'KeyB' })).toBe(true);
  });
});
