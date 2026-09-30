import { describe, expect, it } from 'vitest';
import {
  CENTRE_FLOOR, DOCK, type FramePrefs, frameLayout, LIST_BOUNDS, LIST_STRIP, type ListChoice, listToggled,
} from './layout';

// The frame's geometry (FRAME6, then D118 §3a): the reference console's field-tested numbers, as one pure
// function so every width a window can be is an ordinary assertion.

/** Sessions' list as a viewer who never touched it has it. */
const SESSIONS: ListChoice = { bounds: LIST_BOUNDS.sessions, width: null, closed: false, over: false };
const NONE: FramePrefs = { list: SESSIONS, dockShare: null, dockClosed: false, dockFull: false };
/** A window `viewport` wide, whose frame is that less the 48px activity bar. */
const at = (viewport: number, prefs: Partial<FramePrefs> = {}, list: Partial<ListChoice> = {}) =>
  frameLayout(viewport, viewport - 48, { ...NONE, ...prefs, list: prefs.list === null ? null : { ...SESSIONS, ...prefs.list, ...list } });

/** Every width worth a window, from a phone-narrow one to a wide monitor. */
const WIDTHS = [560, 640, 680, 720, 760, 768, 800, 900, 980, 1000, 1024, 1028, 1100, 1280, 1440, 1600, 1920, 2560];

describe('the bounds', () => {
  it('are FRAME6\'s for every list but Settings\', whose rows are single names', () => {
    for (const view of ['sessions', 'quests', 'projects', 'convergence', 'search', 'plugins'] as const) {
      expect(LIST_BOUNDS[view]).toEqual({ min: 264, max: 420, initial: 280 });
    }
    expect(LIST_BOUNDS.settings).toEqual({ min: 176, max: 320, initial: 176 });
  });
});

describe('the list', () => {
  it('opens at its default and keeps the width the person gave it, within its bounds', () => {
    expect(at(1600).list).toEqual({ mode: 'open', width: 280, beside: 280, auto: false });
    expect(at(1600, {}, { width: 350 }).list!.width).toBe(350);
    expect(at(1600, {}, { width: 100 }).list!.width).toBe(264);
    expect(at(1600, {}, { width: 900 }).list!.width).toBe(420);
    expect(at(1600, {}, { bounds: LIST_BOUNDS.settings }).list!.width).toBe(176);
    expect(at(1600, {}, { bounds: LIST_BOUNDS.settings, width: 900 }).list!.width).toBe(320);
  });

  it('is a strip when the person closes it, at every width, and nothing but the person opens it again', () => {
    for (const width of WIDTHS) {
      expect(at(width, {}, { closed: true }).list).toEqual({ mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false });
      // Asked to lay itself over, a list the person closed stays their strip: the open is theirs to press.
      expect(at(width, {}, { closed: true, over: true }).list!.mode).toBe('strip');
    }
  });

  /**
   * D118 §3a, amending FRAME6's fixed 1024 px: the list is a strip by itself when, at its width, the main
   * area would fall below its floor beside the side bar as it stands — its 32 px strip closed, its 300 px
   * floor open. So 900 px with the side bar closed leaves the rail open and 540 px for the conversation.
   */
  it('gives way by room, counting the side bar as it stands, never by a fixed width', () => {
    expect(at(900, { dockClosed: true }).list).toEqual({ mode: 'open', width: 280, beside: 280, auto: false });
    expect(852 - 280 - DOCK.strip).toBe(540);
    expect(at(900).list).toEqual({ mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: true });

    expect(at(1280).list!.mode).toBe('open');
    expect(at(1280, { dockClosed: true }).list!.mode).toBe('open');
    expect(at(680).list!.mode).toBe('strip');
    expect(at(680, { dockClosed: true }).list!.mode).toBe('strip');

    // The edges, exactly: a frame one pixel short of the list, the floor and the side bar has no room.
    const open = 48 + 280 + CENTRE_FLOOR + DOCK.floor;
    expect(at(open).list!.mode).toBe('open');
    expect(at(open - 1).list!.mode).toBe('strip');
    const closed = 48 + 280 + CENTRE_FLOOR + DOCK.strip;
    expect(at(closed, { dockClosed: true }).list!.mode).toBe('open');
    expect(at(closed - 1, { dockClosed: true }).list!.mode).toBe('strip');
  });

  it('measures the room at the width the person gave it, and at Settings\' narrower one', () => {
    // Side bar closed: 420 px beside the floor needs a frame of 852, which 900 px has and 880 px does not.
    expect(at(900, { dockClosed: true }, { width: 420 }).list!.mode).toBe('open');
    expect(at(880, { dockClosed: true }, { width: 420 }).list!.mode).toBe('strip');
    // Settings' 176 px fits where Sessions' 280 does not: 1000 px with the side bar open.
    expect(at(1000).list!.mode).toBe('strip');
    expect(at(1000, {}, { bounds: LIST_BOUNDS.settings }).list!.mode).toBe('open');
  });

  it('holds the main area at its floor wherever the list is open beside it, at every width', () => {
    for (const width of WIDTHS) {
      for (const dockClosed of [false, true]) {
        const layout = at(width, { dockClosed });
        const sideBar = dockClosed ? DOCK.strip : DOCK.floor;
        if (layout.list!.mode === 'open') expect(width - 48 - layout.list!.beside - sideBar).toBeGreaterThanOrEqual(CENTRE_FLOOR);
        else expect(width - 48 - 280 - sideBar).toBeLessThan(CENTRE_FLOOR);
      }
    }
  });

  /** "The list gives way first, because its strip keeps its doors": the side bar asks to be closed only past it. */
  it('gives way before the side bar does, at every width', () => {
    for (const width of WIDTHS) {
      const layout = at(width);
      if (layout.dock.mode === 'cramped') expect(layout.list!.mode).toBe('strip');
      if (layout.list!.mode === 'open') expect(layout.dock.mode).not.toBe('cramped');
    }
  });

  it('comes back when the window widens, where it was the window that took it', () => {
    expect(at(1000, {}, { width: 350 }).list).toEqual({ mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: true });
    expect(at(1200, {}, { width: 350 }).list).toEqual({ mode: 'open', width: 350, beside: 350, auto: false });
  });

  /**
   * D118 §3a, amending FRAME6: a strip the window drew opens the list OVER the main area, beside the strip,
   * rather than offering no open. The strip stays, so the main area keeps the room the window gave it.
   */
  it('lays itself over the main area when a strip the window drew is opened, and only then', () => {
    expect(at(900, {}, { over: true }).list).toEqual({ mode: 'over', width: 280, beside: LIST_STRIP, auto: true });
    expect(at(680, {}, { over: true, width: 350 }).list).toEqual({ mode: 'over', width: 350, beside: LIST_STRIP, auto: true });
    // With room, it is simply open beside: nothing is laid over what it can sit beside.
    expect(at(1600, {}, { over: true }).list!.mode).toBe('open');
    // Never wider than what lies beside the strip.
    expect(at(300, {}, { over: true }).list!.width).toBe(300 - 48 - LIST_STRIP);
  });
});

/** DOCK1a: a view with no list — Overview, Map — gives the list's room to the rest. */
describe('a view with no list', () => {
  it('has none, at any width, and the side bar and the main area share what the list would have taken', () => {
    expect(at(1600, { list: null }).list).toBeNull();
    expect(at(900, { list: null }).list).toBeNull();
    // A window where the list's strip cramps the side bar does not cramp it without one.
    expect(at(800).dock.mode).toBe('cramped');
    expect(at(800, { list: null }).dock.mode).toBe('docked');
  });
});

describe('a toggle of the list', () => {
  it('closes an open list, dismisses one laid over, and opens a strip — over the main area where there is no room', () => {
    expect(listToggled({ mode: 'open' })).toEqual({ closed: true, over: false });
    expect(listToggled({ mode: 'over' })).toEqual({ closed: false, over: false });
    // A strip opens whoever drew it: the person's closing is undone, and the window's is laid over.
    expect(listToggled({ mode: 'strip' })).toEqual({ closed: false, over: true });
  });

  it('lands where the person can see it, at every width', () => {
    for (const width of WIDTHS) {
      for (const closed of [false, true]) {
        const before = at(width, {}, { closed }).list!;
        const after = at(width, {}, listToggled(before)).list!;
        // Shown becomes not shown, and not shown becomes shown: open beside where there is room, else over.
        expect(after.mode === 'strip').toBe(before.mode !== 'strip');
      }
    }
  });
});

describe('the side bar', () => {
  it('opens at 45% of the window and keeps the share the person gave it', () => {
    expect(at(1600).dock).toEqual({ mode: 'docked', width: 720 });
    expect(at(1600, { dockShare: 500 / 1600 }).dock).toEqual({ mode: 'docked', width: 500 });
  });

  /**
   * LAYOUT1: a dragged dock was kept in pixels, so it stayed 389px while the
   * window grew from 1518 to 1923 (measured on the shell). A share grows and shrinks with the window.
   */
  it('keeps a dragged width as a share, so it follows the window as it grows and shrinks', () => {
    const share = 389 / 1518;
    expect(at(1518, { dockShare: share }).dock.width).toBe(389);
    expect(at(1923, { dockShare: share }).dock.width).toBe(Math.round(1923 * share));
    expect(at(2560, { dockShare: share }).dock.width).toBe(Math.round(2560 * share));
  });

  it('never takes more than 70% of the window', () => {
    expect(at(3000, { dockShare: 2500 / 3000 }).dock).toEqual({ mode: 'docked', width: 2100 });
  });

  it('gives way to keep the main area at its floor, down to its own', () => {
    // 1232 frame − 280 list − 400 main area leaves 552, under the 576 it would open at.
    const layout = at(1280);
    expect(layout.dock).toEqual({ mode: 'docked', width: 552 });
    expect(1232 - layout.list!.beside - layout.dock.width).toBe(CENTRE_FLOOR);
    // 900 px: the list gave way to its strip, and the side bar takes what that left.
    expect(at(900).dock).toEqual({ mode: 'docked', width: 852 - LIST_STRIP - CENTRE_FLOOR });
  });

  it('asks to be closed, rather than squeezing the main area further, once at its floor', () => {
    // 752 − 56 strip − 400 leaves 296: under the side bar's own floor, with the list already a strip.
    expect(at(800).dock).toEqual({ mode: 'cramped', width: DOCK.floor });
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
