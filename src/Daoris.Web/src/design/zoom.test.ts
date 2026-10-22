import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  applyZoom, readZoom, setZoom, subscribeZoom, ZOOM_KEY, ZOOM_LEVELS, zoomBy, zoomLevel, zoomStep, zoomWheel,
} from './zoom';

// The window's zoom (ZOOM1): an editor's steps, kept for the machine as the theme is, and applied to the page's root size so
// everything drawn in rem grows with it, while the frame wears the inverse (`--frame-zoom`) and so keeps its own size.

const root = () => document.documentElement;

describe('the window zoom', () => {
  beforeEach(() => {
    window.localStorage.removeItem(ZOOM_KEY);
    setZoom(100);
  });

  it('steps as an editor does, from 50% to 200%, with 100% the page as drawn', () => {
    expect(ZOOM_LEVELS).toEqual([50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200]);
    expect(zoomStep(100, 1)).toBe(110);
    expect(zoomStep(100, -1)).toBe(90);
    expect(zoomStep(150, 1)).toBe(175);
    // Bounded: past either end it stays at that end.
    expect(zoomStep(200, 1)).toBe(200);
    expect(zoomStep(50, -1)).toBe(50);
    // A level between two steps goes to the next one its way.
    expect(zoomStep(105, 1)).toBe(110);
    expect(zoomStep(105, -1)).toBe(100);
  });

  it("takes View's three acts: a step in, a step out, back to 100%", () => {
    zoomBy('in');
    zoomBy('in');
    expect(zoomLevel()).toBe(125);
    zoomBy('out');
    expect(zoomLevel()).toBe(110);
    zoomBy('reset');
    expect(zoomLevel()).toBe(100);
  });

  it('is 100% by default, and 100% leaves the page exactly as it was drawn', () => {
    expect(readZoom()).toBe(100);
    applyZoom(100);
    expect(root().style.fontSize).toBe('');
    expect(root().style.getPropertyValue('--frame-zoom')).toBe('');
  });

  it('grows the root size and gives the frame the inverse, so the frame keeps its size', () => {
    setZoom(125);

    expect(root().style.fontSize).toBe('125%');
    expect(Number(root().style.getPropertyValue('--frame-zoom'))).toBeCloseTo(0.8);
    // The frame's size is the root's times its own zoom: the same at every level.
    for (const level of ZOOM_LEVELS) {
      setZoom(level);
      const frame = Number(root().style.getPropertyValue('--frame-zoom') || '1');
      expect((level / 100) * frame).toBeCloseTo(1, 10);
    }
  });

  it('is remembered for the machine, and 100% forgets the key rather than writing it', () => {
    setZoom(150);
    expect(window.localStorage.getItem(ZOOM_KEY)).toBe('150');
    expect(readZoom()).toBe(150);
    expect(zoomLevel()).toBe(150);

    setZoom(100);
    expect(window.localStorage.getItem(ZOOM_KEY)).toBeNull();
  });

  it('takes no level that is not a step, and reads a remembered one it does not know as 100%', () => {
    setZoom(133);
    expect(zoomLevel()).toBe(100);
    window.localStorage.setItem(ZOOM_KEY, '999');
    expect(readZoom()).toBe(100);
    window.localStorage.setItem(ZOOM_KEY, 'large');
    expect(readZoom()).toBe(100);
  });

  it('tells whoever listens when it changes, and not when it stays', () => {
    const heard = vi.fn();
    const stop = subscribeZoom(heard);
    setZoom(110);
    setZoom(110);
    stop();
    setZoom(125);
    expect(heard).toHaveBeenCalledTimes(1);
  });

  /** Another main window on the same machine (ONEINSTANCE1) follows, as the theme does (WINDOW1). */
  it('follows a level another window chose', () => {
    const heard = vi.fn();
    const stop = subscribeZoom(heard);
    window.localStorage.setItem(ZOOM_KEY, '175');
    window.dispatchEvent(new StorageEvent('storage', { key: ZOOM_KEY, newValue: '175' }));
    stop();

    expect(zoomLevel()).toBe(175);
    expect(root().style.fontSize).toBe('175%');
    expect(heard).toHaveBeenCalledTimes(1);
  });
});

describe('the wheel with Ctrl', () => {
  const wheel = (init: WheelEventInit) => new WheelEvent('wheel', { cancelable: true, ...init });
  afterEach(() => vi.restoreAllMocks());

  it('steps once a notch, in towards the reader as the wheel turns away', () => {
    const steps: number[] = [];
    const on = zoomWheel((direction) => steps.push(direction));
    const up = wheel({ ctrlKey: true, deltaY: -100 });
    on(up);
    on(wheel({ ctrlKey: true, deltaY: 100 }));
    expect(steps).toEqual([1, -1]);
    // The engine's own zoom never runs beside it.
    expect(up.defaultPrevented).toBe(true);
  });

  it("gathers a touchpad's small deltas into one step, and starts again when the direction turns", () => {
    const steps: number[] = [];
    const on = zoomWheel((direction) => steps.push(direction));
    for (let i = 0; i < 4; i += 1) on(wheel({ ctrlKey: true, deltaY: -10 }));
    expect(steps).toEqual([]);
    on(wheel({ ctrlKey: true, deltaY: 30 }));
    on(wheel({ ctrlKey: true, deltaY: 30 }));
    expect(steps).toEqual([-1]);
  });

  it('reads a wheel that counts lines as a notch', () => {
    const steps: number[] = [];
    zoomWheel((direction) => steps.push(direction))(wheel({ ctrlKey: true, deltaY: -3, deltaMode: 1 }));
    expect(steps).toEqual([1]);
  });

  it('leaves a plain wheel to the page, and a wheel a surface already took (the Map zooms itself)', () => {
    const steps: number[] = [];
    const on = zoomWheel((direction) => steps.push(direction));
    const plain = wheel({ deltaY: -100 });
    on(plain);
    const taken = wheel({ ctrlKey: true, deltaY: -100 });
    taken.preventDefault();
    on(taken);
    expect(steps).toEqual([]);
    expect(plain.defaultPrevented).toBe(false);
  });
});
