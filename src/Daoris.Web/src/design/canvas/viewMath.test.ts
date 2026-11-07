import { describe, expect, it } from 'vitest';
import {
  clampView, fitView, keepAnchor, panBy, reveal, toScreen, toWorld, translation, wheelPan, wheelTravel, zoomAbout, zoomByWheel,
} from './viewMath';

// The viewport's arithmetic, pure. The view is the world point at the viewport's top left and
// the scale (the map's convention), so `screen = origin + k × (world − view)` and the transform's translation is −k × the view's point.

const VIEWPORT = { w: 800, h: 400 };
const WORLD = { w: 2000, h: 1000 };
const near = (value: number, expected: number) => expect(value).toBeCloseTo(expected, 6);

describe('world and screen', () => {
  it('round-trip at every scale and offset', () => {
    for (const view of [{ x: 0, y: 0, k: 1 }, { x: 120, y: -40, k: 0.5 }, { x: -30, y: 300, k: 2 }, { x: 7.5, y: 3.25, k: 0.8 }]) {
      const point = { x: 431, y: 97 };
      const there = toScreen(view, toWorld(view, point));
      near(there.x, point.x);
      near(there.y, point.y);
    }
  });

  it('translate the world by minus k times the view point', () => {
    expect(translation({ x: 100, y: 50, k: 2 })).toEqual({ x: -200, y: -100 });
    expect(toScreen({ x: 100, y: 50, k: 2 }, { x: 100, y: 50 })).toEqual({ x: 0, y: 0 });
  });
});

describe('zoom about a point', () => {
  it('keeps the world point under the pointer where it was', () => {
    const view = { x: 200, y: 100, k: 1 };
    const at = { x: 300, y: 150 };
    const under = toWorld(view, at);
    const next = zoomAbout(view, 1.5, at, { min: 0.5, max: 2 });
    near(next.k, 1.5);
    const after = toWorld(next, at);
    near(after.x, under.x);
    near(after.y, under.y);
  });

  it('holds the scale within the bounds, and then moves nothing', () => {
    const view = { x: 10, y: 20, k: 2 };
    expect(zoomAbout(view, 3, { x: 5, y: 5 }, { min: 0.5, max: 2 })).toEqual(view);
    expect(zoomAbout({ ...view, k: 0.5 }, 0.1, { x: 5, y: 5 }, { min: 0.5, max: 2 }).k).toBe(0.5);
  });

  it('by the wheel, continuously: a notch is 1.25×, a touchpad\'s small travel a small change, and 40 small deltas under 2× (trap 5)', () => {
    const view = { x: 0, y: 0, k: 1 };
    near(zoomByWheel(view, -100, { x: 0, y: 0 }, { min: 0.1, max: 2.5 }).k, 1.25);
    near(zoomByWheel(view, 100, { x: 0, y: 0 }, { min: 0.1, max: 2.5 }).k, 0.8);
    let pinched = view;
    for (let step = 0; step < 40; step += 1) pinched = zoomByWheel(pinched, -2, { x: 0, y: 0 }, { min: 0.1, max: 2.5 });
    expect(pinched.k).toBeGreaterThan(1);
    expect(pinched.k).toBeLessThan(2);
  });

  it('counts a wheel\'s travel in pixels, whatever it counts in', () => {
    expect(wheelTravel({ deltaY: 3, deltaMode: 1 })).toBe(99);
    expect(wheelTravel({ deltaY: 1, deltaMode: 2 })).toBe(800);
    expect(wheelTravel({ deltaY: -4, deltaMode: 0 })).toBe(-4);
  });
});

describe('the bounds of a pan', () => {
  it('keep the world in view where it overflows, and centre it where it fits', () => {
    expect(clampView({ x: -500, y: -500, k: 1 }, WORLD, VIEWPORT)).toEqual({ x: 0, y: 0, k: 1 });
    expect(clampView({ x: 5000, y: 5000, k: 1 }, WORLD, VIEWPORT)).toEqual({ x: 1200, y: 600, k: 1 });
    const small = clampView({ x: 99, y: 99, k: 1 }, { w: 400, h: 100 }, VIEWPORT);
    expect(small).toEqual({ x: -200, y: -150, k: 1 });
  });

  it('let a margin past each edge where it overflows', () => {
    expect(clampView({ x: -500, y: -500, k: 2 }, WORLD, VIEWPORT, 16)).toEqual({ x: -8, y: -8, k: 2 });
  });

  it('pan by screen pixels at the scale', () => {
    expect(panBy({ x: 10, y: 10, k: 2 }, 40, -20)).toEqual({ x: 30, y: 0, k: 2 });
  });
});

describe('a plain wheel', () => {
  it('pans while the world overflows that way and is not at that edge', () => {
    const taken = wheelPan({ x: 0, y: 100, k: 1 }, 0, 60, WORLD, VIEWPORT);
    expect(taken).toEqual({ view: { x: 0, y: 160, k: 1 }, taken: true });
  });

  it('is left to the page at either edge, and where the world fits that way (trap 3)', () => {
    expect(wheelPan({ x: 0, y: 0, k: 1 }, 0, -60, WORLD, VIEWPORT).taken).toBe(false);
    expect(wheelPan({ x: 0, y: 600, k: 1 }, 0, 60, WORLD, VIEWPORT).taken).toBe(false);
    expect(wheelPan({ x: -200, y: -150, k: 1 }, 0, 60, { w: 400, h: 100 }, VIEWPORT).taken).toBe(false);
  });
});

describe('fit', () => {
  it('shows the whole world, never above 100%, and within the bounds', () => {
    const fitted = fitView({ x: 0, y: 0, w: 2000, h: 1000 }, VIEWPORT, { min: 0.1, max: 2.5 }, 0);
    near(fitted.k, 0.4);
    expect(fitView({ x: 0, y: 0, w: 100, h: 100 }, VIEWPORT, { min: 0.1, max: 2.5 }, 0).k).toBe(1);
    expect(fitView({ x: 0, y: 0, w: 2000, h: 1000 }, VIEWPORT, { min: 0.8, max: 2 }, 0).k).toBe(0.8);
  });

  it('centres what it fits', () => {
    const fitted = fitView({ x: 100, y: 100, w: 200, h: 100 }, VIEWPORT, { min: 0.1, max: 2.5 }, 0);
    const middle = toScreen(fitted, { x: 200, y: 150 });
    near(middle.x, 400);
    near(middle.y, 200);
  });
});

describe('reveal', () => {
  const view = { x: 0, y: 0, k: 1 };

  it('moves nothing for a box already inside the margin', () => {
    expect(reveal(view, { x: 100, y: 100, w: 50, h: 50 }, VIEWPORT, 32)).toBe(view);
  });

  it('pans the least that brings a box inside the margin, never zooming', () => {
    expect(reveal(view, { x: 900, y: 100, w: 100, h: 50 }, VIEWPORT, 32)).toEqual({ x: 232, y: 0, k: 1 });
    expect(reveal({ x: 500, y: 300, k: 2 }, { x: 400, y: 300, w: 50, h: 50 }, VIEWPORT, 32)).toEqual({ x: 384, y: 284, k: 2 });
  });

  it('aligns the start of a box larger than the room', () => {
    expect(reveal(view, { x: 1000, y: 0, w: 2000, h: 50 }, VIEWPORT, 32).x).toBe(968);
  });
});

describe('the anchor across a relayout', () => {
  it('keeps a node where it stood on screen when its box moves in the world (trap 7)', () => {
    const view = { x: 100, y: 200, k: 0.8 };
    const before = { x: 300, y: 500, w: 100, h: 60 };
    const after = { x: 340, y: 620, w: 100, h: 60 };
    const kept = keepAnchor(view, before, after);
    expect(toScreen(kept, after)).toEqual(toScreen(view, before));
  });
});
