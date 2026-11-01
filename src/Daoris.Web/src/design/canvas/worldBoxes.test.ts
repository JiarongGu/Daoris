import { afterEach, describe, expect, it, vi } from 'vitest';
import { observeWorld, readWorld, scaleOf } from './worldBoxes';

// CANVAS1a (D224; the canvas design §2.1, trap 1): every box the ink, the reveal and the anchor read is in world units, the rect less the
// drawing's origin and divided by the scale the world is drawn at, to half a pixel; the same at every scale. jsdom lays nothing out, so
// each element's rect is stood in for as a world drawn at `k` and moved by a translation would give it.

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
});

/** A drawing whose world boxes are given, drawn at `k` and translated by (tx, ty), its elements' rects what the browser would give. */
function drawn(k: number, tx: number, ty: number) {
  document.body.innerHTML = `
    <div data-canvas-world="" data-canvas-scale="${k}">
      <div id="frame" data-box="0,0,600,400">
        <div data-flow-node="a" data-box="10.2,20,200,80"><span data-flow-port="a:yes" data-box="30,90,40,10"></span></div>
        <div data-flow-node="b" data-box="300,20,200,80.4"></div>
        <span data-flow-way="w1" data-box="250,140,16,80"></span>
        <div data-flow-section="" data-box="10,200,500,24"></div>
      </div>
    </div>`;
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    const [x = 0, y = 0, w = 0, h = 0] = (this.getAttribute('data-box') ?? '0,0,0,0').split(',').map(Number);
    return { left: tx + x * k, top: ty + y * k, width: w * k, height: h * k, right: 0, bottom: 0, x: 0, y: 0, toJSON: () => ({}) } as DOMRect;
  });
  return document.getElementById('frame')!;
}

describe('the scale a world is drawn at', () => {
  it('is its world\'s, and 1 outside a canvas', () => {
    expect(scaleOf(drawn(1.5, 0, 0))).toBe(1.5);
    document.body.innerHTML = '<div id="loose"></div>';
    expect(scaleOf(document.getElementById('loose')!)).toBe(1);
  });
});

describe('a world read', () => {
  it('gives every node, port, waypoint and divider in world units, to half a pixel, the same at five scales (trap 1)', () => {
    const at = (k: number) => readWorld(drawn(k, 37, -112))!;
    const one = at(1);
    expect(one.nodes.get('a')).toEqual({ x: 10, y: 20, w: 200, h: 80 });
    expect(one.nodes.get('b')).toEqual({ x: 300, y: 20, w: 200, h: 80.5 });
    expect(one.ports.get('a:yes')).toEqual({ x: 30, y: 90, w: 40, h: 10 });
    expect(one.ways.get('w1')).toEqual({ x: 250, y: 140, w: 16, h: 80 });
    expect(one.sections).toEqual([{ x: 10, y: 200, w: 500, h: 24 }]);
    expect([one.width, one.height]).toEqual([600, 400]);
    for (const k of [0.5, 0.8, 1.5, 2]) {
      const other = at(k);
      expect(other.k).toBe(k);
      expect([...other.nodes]).toEqual([...one.nodes]);
      expect([...other.ports]).toEqual([...one.ports]);
      expect([...other.ways]).toEqual([...one.ways]);
      expect(other.sections).toEqual(one.sections);
      expect([other.width, other.height]).toEqual([one.width, one.height]);
    }
  });

  it('is nothing where nothing was laid out', () => {
    document.body.innerHTML = '<div id="frame"><div data-flow-node="a"></div></div>';
    expect(readWorld(document.getElementById('frame')!)).toBeNull();
  });
});

describe('the one observer', () => {
  it('draws once a frame however many boxes resized in it (trap 14), and lets go when told', () => {
    const callbacks: ResizeObserverCallback[] = [];
    const observed: Element[] = [];
    const disconnect = vi.fn();
    vi.stubGlobal('ResizeObserver', class {
      constructor(callback: ResizeObserverCallback) { callbacks.push(callback); }
      observe(element: Element) { observed.push(element); }
      disconnect() { disconnect(); }
    });
    const frames: FrameRequestCallback[] = [];
    vi.stubGlobal('requestAnimationFrame', (callback: FrameRequestCallback) => frames.push(callback));
    vi.stubGlobal('cancelAnimationFrame', (id: number) => { frames[id - 1] = () => {}; });
    const frame = drawn(1, 0, 0);
    const draw = vi.fn();
    const stop = observeWorld(frame, draw);
    expect(draw).toHaveBeenCalledOnce();
    expect(callbacks).toHaveLength(1);
    expect(observed.map((element) => element.getAttribute('data-flow-node') ?? element.getAttribute('data-flow-port') ?? element.id))
      .toEqual(['frame', 'a', 'a:yes', 'b']);
    callbacks[0]!([], {} as ResizeObserver);
    callbacks[0]!([], {} as ResizeObserver);
    frames.forEach((each) => each(0));
    expect(draw).toHaveBeenCalledTimes(2);
    stop();
    expect(disconnect).toHaveBeenCalledOnce();
  });
});
