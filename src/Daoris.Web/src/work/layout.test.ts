import { describe, expect, it } from 'vitest';
import { CENTRE_FLOOR, DOCK, type FramePrefs, frameLayout, RAIL } from './layout';

// The Work frame's geometry (FRAME6, components plan §3a): the reference console's field-tested
// numbers, as one pure function so every width a window can be is an ordinary assertion.

const NONE: FramePrefs = { rail: null, railClosed: false, dock: null, dockClosed: false, dockFull: false };
/** A window `viewport` wide, whose frame is that less the 48px activity bar. */
const at = (viewport: number, prefs: Partial<FramePrefs> = {}) =>
  frameLayout(viewport, viewport - 48, { ...NONE, ...prefs });

describe('the rail', () => {
  it('opens at its default and keeps the width the person gave it, within its bounds', () => {
    expect(at(1600).rail).toEqual({ width: RAIL.initial, strip: false, auto: false });
    expect(at(1600, { rail: 350 }).rail.width).toBe(350);
    expect(at(1600, { rail: 100 }).rail.width).toBe(RAIL.min);
    expect(at(1600, { rail: 900 }).rail.width).toBe(RAIL.max);
  });

  it('is a strip when the person closes it, and when the window is too narrow — which it then undoes', () => {
    expect(at(1600, { railClosed: true }).rail).toEqual({ width: RAIL.strip, strip: true, auto: false });
    expect(at(1000, { rail: 350 }).rail).toEqual({ width: RAIL.strip, strip: true, auto: true });
    // Widening puts back what the PERSON chose; only their own close outlasts it.
    expect(at(1200, { rail: 350 }).rail).toEqual({ width: 350, strip: false, auto: false });
  });
});

describe('the dock', () => {
  it('opens at 45% of the window and keeps the width the person gave it', () => {
    expect(at(1600).dock).toEqual({ mode: 'docked', width: 720 });
    expect(at(1600, { dock: 500 }).dock).toEqual({ mode: 'docked', width: 500 });
  });

  it('never takes more than 70% of the window', () => {
    expect(at(3000, { dock: 2500 }).dock).toEqual({ mode: 'docked', width: 2100 });
  });

  it('gives way to keep the session at its floor, down to its own', () => {
    // 1232 frame − 280 rail − 400 centre leaves 552, under the 576 it would open at.
    const layout = at(1280);
    expect(layout.dock).toEqual({ mode: 'docked', width: 552 });
    expect(1232 - layout.rail.width - layout.dock.width).toBe(CENTRE_FLOOR);
  });

  it('asks to be closed, rather than squeezing the session further, once at its floor', () => {
    // 976 − 280 − 400 leaves 296: under the dock's own floor.
    expect(at(1024).dock).toEqual({ mode: 'cramped', width: DOCK.floor });
  });

  it('covers the frame below 768px, and when the person asks it to', () => {
    expect(at(700).dock.mode).toBe('full');
    expect(at(1600, { dockFull: true }).dock).toEqual({ mode: 'full', width: 1552 });
  });

  it('stays closed at every width once the person closed it', () => {
    for (const width of [700, 1024, 1600, 3000]) {
      expect(at(width, { dockClosed: true, dockFull: true }).dock.mode).toBe('closed');
    }
  });
});
