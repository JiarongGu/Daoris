import {
  type CSSProperties, type FocusEvent, type KeyboardEvent, type PointerEvent, type ReactNode, type Ref, type RefObject, useEffect,
  useImperativeHandle, useRef, useState,
} from 'react';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { useTall } from '../useWidth';
import { CanvasToolbar, type SizingActs } from './CanvasToolbar';
import { boxSelect, worldRect } from './gesture';
import { Minimap } from './Minimap';
import { minimapWanted, rememberMinimap, wantsMinimap } from './minimapRule';
import { type Connect, useConnection } from './useConnection';
import { anchorOf, keepView, memoryKey, recallView, restoreView } from './viewMemory';
import {
  type Box, clampScale, clampView, fitView, keepAnchor, panBy, type Point, reveal, type Size, translation, type View, wheelPan, wheelTravel,
  zoomAbout, zoomByWheel, type ZoomBounds,
} from './viewMath';
import { SCALE, WORLD, worldBox, type WorldRead } from './worldBoxes';

/** What a feature opts into: its mode, its zoom's bounds and its own words for fit and for moving. */
export type CanvasOptions = {
  mode: 'display' | 'edit';
  /** Within 10–250%: the workflow's 80–200% editing and 50–200% reading. */
  zoom: ZoomBounds;
  /** The feature's words for showing the whole (*Show the whole workflow*). */
  fit: string;
  /** How to move it, said as the region's role. */
  moves: string;
  /** Zoom off: the world is drawn at the scale that fits its width, never above 100%, with no sizing offered (the ring). */
  fixed?: boolean;
  /** Opens fitted rather than at 100%: as large as fits, never below `least`, and follows the drawing until the person moves it. */
  opening?: { least: number };
  /** The region is a Tab stop, where bare + - 0 and the arrows act (the map's keys). */
  stop?: boolean;
};

/** How far an arrow pans the view from the region, in screen pixels. */
const ARROW = 60;

/**
 * What a feature does with a selection beyond a press on a node: a press on the ground (which
 * releases the selection in display, does nothing in edit and chooses open ground in pick mode), and, where many may be chosen, a
 * Shift+drag box on the ground.
 */
export type CanvasSelection = {
  many: boolean;
  onGround: () => void;
  /** The nodes a drawn box touched, in reading order. */
  onBox: (ids: string[]) => void;
};

/** What the drawing tells the canvas each time it measured its world (a positioned world adds its groups' boxes), and what it can ask. */
export type CanvasHandle = {
  measured: (read: WorldRead, groups?: ReadonlyMap<string, Box>) => void;
  /** Brings a box to the middle at a readable scale: at least 100% where the bounds allow, never smaller than the view is. */
  showBox: (box: Box) => void;
  view: () => View;
  setView: (view: View) => void;
};

/**
 * What the viewer keeps: the view, by feature and key, restored if the graph's identity is
 * the same, else by the surviving anchor. `onGone` says the selection kept is no longer in the graph.
 */
export type CanvasMemory = {
  feature: string;
  key: string;
  identity: string;
  selected?: string | null;
  groupOf?: (id: string) => string | null;
  onGone?: () => void;
};

/** A large world's minimap: the boxes it draws, offered by `minimap.ts`'s rule. */
export type CanvasMinimap = { boxes: readonly Box[] };

/** The region's inner width from which a feature's drawing stands in the viewport, in rem: the 39rem story's 576 px. */
export const CANVAS_FLOOR = 36;
/** What follows the viewport down to the window's foot, as the map reserves: the page's foot and the status bar. */
const BELOW = 150;
/** The least height, in rem: a small drawing is never a tall empty box, and none is shorter than this. */
const LEAST = 20;
/** The edit mode's floor (the map's four fifths), 100% on a coarse pointer, so `text-meta`, the ＋ and the ports stay usable. */
const EDIT_FLOOR = 0.8;
/** A drag's travel, in screen pixels, before it is a pan rather than a press. */
const THRESHOLD = 3;
/** How long a wheel's gesture holds `will-change` on after its last event. */
const SETTLE_MS = 160;
/** What a press starts no pan on: a node, a port, a ＋ and every control. */
const CONTROLS = '[data-flow-node], [data-flow-port], [data-flow-insert], [data-flow-edge-hit], button, a, input, select, textarea, label, [role="button"], [contenteditable]';
const TYPED = 'textarea, select, [contenteditable=""], [contenteditable="true"], [contenteditable="plaintext-only"]';
/** The inputs a key is no character in, so a canvas key pressed there is still the canvas's. */
const PRESSED = new Set(['checkbox', 'radio', 'button', 'submit', 'reset', 'range', 'color', 'file']);
const EPSILON = 1e-6;

/** Whether a key aimed here types: a field keeps Shift+1's `!` (trap 13). */
function isField(target: Element): boolean {
  if (target.closest(TYPED)) return true;
  const input = target.closest('input');
  return input !== null && !PRESSED.has(input.type);
}

const same = (a: View, b: View) => Math.abs(a.x - b.x) < EPSILON && Math.abs(a.y - b.y) < EPSILON && Math.abs(a.k - b.k) < EPSILON;
const coarse = () => typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches;

/** Whether an element inside the region can scroll its own content the way a wheel turns: its own scroll comes first. */
function scrollsItself(target: Element | null, stop: Element, dx: number, dy: number): boolean {
  for (let element = target; element && element !== stop; element = element.parentElement) {
    const style = getComputedStyle(element);
    if (dy !== 0 && /(auto|scroll)/.test(style.overflowY) && element.scrollHeight > element.clientHeight) {
      if (dy < 0 ? element.scrollTop > 0 : element.scrollTop + element.clientHeight < element.scrollHeight - 1) return true;
    }
    if (dx !== 0 && /(auto|scroll)/.test(style.overflowX) && element.scrollWidth > element.clientWidth) {
      if (dx < 0 ? element.scrollLeft > 0 : element.scrollLeft + element.clientWidth < element.scrollWidth - 1) return true;
    }
  }
  return false;
}

/**
 * **The canvas**: a placed world, HTML laid out by CSS, transformed in a viewport that pans and
 * zooms and never lays it out again; its scale is said on the world for the boxes the drawing reads in world units. Where it is not
 * offered (below the 36rem floor, the list chosen, nothing measured) it is the pane without a canvas. A molecule: the view is its own.
 */
export function Canvas({
  ref, active, options, label, width, rem, pane, paneProps, tools, showTools, entry, selected = null, selection = null, connect = null,
  remember = null, minimap = null, overlay, onView, children,
}: {
  ref?: Ref<CanvasHandle>;
  /** Whether the viewport is offered: from the floor, the drawing chosen and measured. */
  active: boolean;
  options: CanvasOptions | null;
  /** Names the region for a reader. */
  label: string;
  /** The region's inner width, measured, in px. */
  width: number | undefined;
  /** The root's size, in px: the window's zoom moves it. */
  rem: number;
  pane: RefObject<HTMLDivElement | null>;
  paneProps: Record<`data-${string}`, string>;
  tools: ReactNode;
  showTools: boolean;
  /** The node the view opens on. */
  entry: string | null;
  selected?: string | null;
  selection?: CanvasSelection | null;
  /** The connection line, where the feature's ports are dragged from; it works with or without the viewport. */
  connect?: Connect | null;
  remember?: CanvasMemory | null;
  minimap?: CanvasMinimap | null;
  /** A layer in the viewport's own pixels, over the world and under the controls (the dots); given the view and the viewport. */
  overlay?: (view: View, viewport: Size) => ReactNode;
  /** Told when the view or the viewport changed, for what the drawing culls by. */
  onView?: (view: View, viewport: Size) => void;
  children: ReactNode;
}) {
  const on = active && options !== null && width !== undefined;
  const [world, setWorld] = useState<Size | null>(null);
  // The bounds follow the world measured: a fixed one is the scale that fits its width, so a handle call holds the newest world's own.
  const boundsFor = (size: Size | null): ZoomBounds => {
    if (!options) return { min: 1, max: 1 };
    if (options.fixed) {
      const fixed = Math.min(1, size && width ? width / Math.max(1, size.w) : 1);
      return { min: fixed, max: fixed };
    }
    return { min: options.mode === 'edit' ? Math.max(options.zoom.min, coarse() ? 1 : EDIT_FLOOR) : options.zoom.min, max: options.zoom.max };
  };
  const bounds = boundsFor(world);
  const tall = useTall(pane, BELOW);
  const pad = rem;
  const least = LEAST * rem;
  const cap = Math.max(least, tall ?? (typeof window === 'undefined' ? least : window.innerHeight - BELOW));

  const [view, setView] = useState<View>({ x: 0, y: 0, k: clampScale(1, bounds) });
  const [moving, setMoving] = useState(false);
  const viewRef = useRef(view);
  const worldRef = useRef(world);
  const moved = useRef(false);
  const boxes = useRef(new Map<string, Box>());
  const focused = useRef<string | null>(null);
  const element = useRef<HTMLDivElement>(null);
  const settle = useRef(0);
  const hitScale = useRef(1);
  // The element a focus landed on while a way or any control is being pressed: revealed when the press ends, if it is no drag.
  const pressedFocus = useRef<HTMLElement | null>(null);
  const pressing = useRef(false);

  const heightAt = (k: number, size: Size | null = worldRef.current) => Math.min(cap, Math.max(least, size === null ? least : size.h * k + 2 * pad));
  const viewport = (k: number): Size => ({ w: width ?? 0, h: heightAt(k) });
  const held = (asked: View): View => {
    const next = options?.fixed ? { ...asked, k: boundsFor(worldRef.current).min } : asked;
    return worldRef.current ? clampView(next, worldRef.current, viewport(next.k), pad) : next;
  };
  const commit = (next: View, byPerson = true) => {
    const kept = held(next);
    if (byPerson && !same(kept, viewRef.current)) moved.current = true;
    if (same(kept, viewRef.current)) return;
    viewRef.current = kept;
    setView(kept);
  };

  /** The view the canvas opens at: 100% within the bounds, the entry's middle at the viewport's, the world's top at the top. */
  const opening = (nodes: ReadonlyMap<string, Box>): View => {
    const size = worldRef.current;
    const own = boundsFor(size);
    if (options?.opening && size && width) {
      // As large as fits (never above 100%, never below the floor); `held` centres what fits and keeps the top left of what does not.
      const fit = Math.min(width / Math.max(1, size.w), cap / Math.max(1, size.h));
      const k = clampScale(Math.min(1, Math.max(options.opening.least, fit)), own);
      return { x: 0, y: 0 - pad / k, k };
    }
    const k = clampScale(1, own);
    const at = entry === null ? undefined : nodes.get(entry);
    const middle = at ? at.x + at.w / 2 : (worldRef.current?.w ?? 0) / 2;
    return { x: middle - (width ?? 0) / (2 * k), y: 0 - pad / k, k };
  };

  /** The node a relayout keeps on screen: the focused, else the selected, else the one nearest the viewport's middle. */
  const anchor = (next: ReadonlyMap<string, Box>): string | null => {
    for (const id of [focused.current, selected]) if (id !== null && boxes.current.has(id) && next.has(id)) return id;
    const { x, y, k } = viewRef.current;
    const middle = { x: x + (width ?? 0) / (2 * k), y: y + heightAt(k) / (2 * k) };
    let best: string | null = null;
    let distance = Infinity;
    for (const [id, box] of boxes.current) {
      if (!next.has(id)) continue;
      const far = Math.hypot(box.x + box.w / 2 - middle.x, box.y + box.h / 2 - middle.y);
      if (far < distance) { distance = far; best = id; }
    }
    return best;
  };

  useImperativeHandle(ref, () => ({
    showBox: (box) => {
      const k = Math.max(viewRef.current.k, clampScale(1, bounds));
      const size = viewport(k);
      commit({ x: box.x + box.w / 2 - size.w / (2 * k), y: box.y + box.h / 2 - size.h / (2 * k), k });
    },
    view: () => viewRef.current,
    setView: (next) => commit(next),
    measured: (read, groups) => {
      const size = { w: read.width, h: read.height };
      if (!worldRef.current || worldRef.current.w !== size.w || worldRef.current.h !== size.h) {
        worldRef.current = size;
        setWorld(size);
      }
      let next = viewRef.current;
      const kept = remember && boxes.current.size === 0 ? recallView(memoryKey(remember.feature, remember.key)) : null;
      const restored = kept && remember ? restoreView(kept, remember.identity, read.nodes, groups ?? new Map()) : null;
      if (restored && remember) {
        next = restored.view;
        moved.current = true;
        if (restored.selectionGone) remember.onGone?.();
      } else if (!moved.current) next = opening(read.nodes);
      else {
        const id = anchor(read.nodes);
        if (id !== null) next = keepAnchor(next, boxes.current.get(id)!, read.nodes.get(id)!);
      }
      boxes.current = new Map(read.nodes);
      commit(next, false);
    },
  }));

  // A fixed world follows its width: a narrower region fits it again.
  useEffect(() => {
    if (options?.fixed && on && Math.abs(viewRef.current.k - bounds.min) > EPSILON) commit({ ...viewRef.current, k: bounds.min }, false);
  }, [options?.fixed, on, bounds.min]);

  // A gesture holds `will-change` only while it lasts: kept on, a zoomed text stays blurred at its old raster (trap 9).
  const gesture = (lasting: boolean) => {
    window.clearTimeout(settle.current);
    setMoving(true);
    if (!lasting) settle.current = window.setTimeout(() => setMoving(false), SETTLE_MS);
  };
  useEffect(() => () => window.clearTimeout(settle.current), []);

  // The drawing is told where the view stands (what it culls by); the viewer's place is kept once the view has held still.
  const viewport0 = viewport(view.k);
  useEffect(() => {
    if (on) onView?.(view, viewport0);
  }, [on, view.x, view.y, view.k, viewport0.w, viewport0.h]);
  const keeping = useRef<(() => void) | null>(null);
  useEffect(() => {
    if (!on || !remember || !moved.current || boxes.current.size === 0) return undefined;
    const keep = () => {
      keeping.current = null;
      keepView({
        identity: remember.identity, view: viewRef.current, selected: remember.selected ?? null,
        anchor: anchorOf(viewRef.current, viewport(viewRef.current.k), boxes.current, remember.groupOf ?? (() => null)),
      }, memoryKey(remember.feature, remember.key));
    };
    keeping.current = keep;
    const timer = window.setTimeout(keep, 400);
    return () => window.clearTimeout(timer);
  }, [on, view.x, view.y, view.k, remember?.identity, remember?.selected]);
  // A viewer who leaves before the view has held still keeps the place they left at, not the one before it.
  useEffect(() => () => keeping.current?.(), []);
  const [mapOn, setMapOn] = useState(minimapWanted);

  // The wheel, heard before the window's zoom: a listener of its own, since a passive React one cannot keep the page from scrolling.
  useEffect(() => {
    const region = pane.current;
    if (!on || !region) return undefined;
    const onWheel = (event: WheelEvent) => {
      const rect = region.getBoundingClientRect();
      const at: Point = { x: event.clientX - rect.left, y: event.clientY - rect.top };
      if (event.ctrlKey || event.metaKey) {
        // Taken at the bounds too, so the window never zooms behind the canvas.
        event.preventDefault();
        const travel = wheelTravel(event);
        if (travel !== 0) commit(zoomByWheel(viewRef.current, travel, at, bounds));
        gesture(false);
        return;
      }
      const lines = event.deltaMode === 1 ? 33 : event.deltaMode === 2 ? 800 : 1;
      const across = event.shiftKey && event.deltaX === 0;
      const dx = (across ? event.deltaY : event.deltaX) * lines;
      const dy = (across ? 0 : event.deltaY) * lines;
      if (scrollsItself(event.target as Element, region, dx, dy) || !worldRef.current) return;
      const panned = wheelPan(viewRef.current, dx, dy, worldRef.current, viewport(viewRef.current.k), pad);
      if (!panned.taken) return;
      event.preventDefault();
      commit(panned.view);
      gesture(false);
    };
    region.addEventListener('wheel', onWheel, { passive: false });
    return () => region.removeEventListener('wheel', onWheel);
  });

  // A pan by the pointer: the ground with the first button, anywhere with the middle one, one finger; two fingers pinch.
  // A Shift+drag on the ground, where many may be chosen, draws a box instead (`lasso`), and selects what it touches.
  const drag = useRef<{ id: number; from: Point; start: View; going: boolean; box: boolean; ground: boolean } | null>(null);
  const touches = useRef(new Map<number, Point>());
  const pinch = useRef<{ distance: number; start: View } | null>(null);
  const [lasso, setLasso] = useState<Box | null>(null);
  const connection = useConnection(connect, pane, (dx, dy) => commit(panBy(viewRef.current, dx, dy)));
  const end = (restore: boolean) => {
    if (restore && drag.current?.going && !drag.current.box) commit(drag.current.start);
    drag.current = null;
    pinch.current = null;
    setLasso(null);
    window.clearTimeout(settle.current);
    setMoving(false);
  };
  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    connection.down(event);
    if (!on) return;
    pressing.current = true;
    if (event.pointerType === 'touch') {
      touches.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
      if (touches.current.size === 2) {
        const [a, b] = [...touches.current.values()] as [Point, Point];
        pinch.current = { distance: Math.max(1, Math.hypot(a.x - b.x, a.y - b.y)), start: viewRef.current };
        drag.current = null;
        return;
      }
    }
    const ground = !(event.target as Element).closest(CONTROLS);
    if (event.button === 1 || (event.button === 0 && ground)) {
      if (event.button === 1) event.preventDefault();
      const box = event.button === 0 && event.shiftKey && selection !== null && selection.many;
      drag.current = {
        id: event.pointerId, from: { x: event.clientX, y: event.clientY }, start: viewRef.current, going: false, box, ground: event.button === 0 && ground,
      };
    }
  };
  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    connection.move(event);
    if (event.pointerType === 'touch' && touches.current.has(event.pointerId)) {
      touches.current.set(event.pointerId, { x: event.clientX, y: event.clientY });
      if (pinch.current && touches.current.size >= 2) {
        const [a, b] = [...touches.current.values()] as [Point, Point];
        const rect = event.currentTarget.getBoundingClientRect();
        const middle = { x: (a.x + b.x) / 2 - rect.left, y: (a.y + b.y) / 2 - rect.top };
        const ratio = Math.hypot(a.x - b.x, a.y - b.y) / pinch.current.distance;
        commit(zoomAbout(viewRef.current, pinch.current.start.k * ratio, middle, bounds));
        gesture(true);
        return;
      }
    }
    const going = drag.current;
    if (!going || going.id !== event.pointerId) return;
    const [dx, dy] = [event.clientX - going.from.x, event.clientY - going.from.y];
    if (!going.going) {
      if (Math.hypot(dx, dy) < THRESHOLD) return;
      going.going = true;
      event.currentTarget.setPointerCapture?.(event.pointerId);
      if (!going.box) gesture(true);
    }
    if (going.box) {
      const rect = event.currentTarget.getBoundingClientRect();
      const [ax, ay] = [going.from.x - rect.left, going.from.y - rect.top];
      setLasso({ x: Math.min(ax, ax + dx), y: Math.min(ay, ay + dy), w: Math.abs(dx), h: Math.abs(dy) });
      return;
    }
    commit(panBy(going.start, -dx, -dy));
  };
  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    connection.up(event);
    const waiting = pressedFocus.current;
    pressedFocus.current = null;
    pressing.current = false;
    const scene = element.current;
    if (waiting && !connection.dragged() && scene?.contains(waiting) && waiting.isConnected) {
      const box = worldBox(waiting, scene.getBoundingClientRect(), viewRef.current.k);
      if (box.w > 0 || box.h > 0) commit(reveal(viewRef.current, box, viewport(viewRef.current.k), 2 * rem));
    }
    touches.current.delete(event.pointerId);
    const was = drag.current;
    if (was?.id === event.pointerId && was.box && was.going && selection) {
      const rect = event.currentTarget.getBoundingClientRect();
      const [ax, ay] = [was.from.x - rect.left, was.from.y - rect.top];
      const ids = boxSelect(worldRect(viewRef.current, { x: ax, y: ay }, { x: event.clientX - rect.left, y: event.clientY - rect.top }), boxes.current);
      selection.onBox(ids);
    } else if (was?.id === event.pointerId && was.ground && !was.going && selection) selection.onGround();
    if (was?.id === event.pointerId || pinch.current) end(false);
  };
  const onPointerCancel = (event: PointerEvent<HTMLDivElement>) => {
    connection.lost();
    pressing.current = false;
    touches.current.delete(event.pointerId);
    end(false);
  };

  const fitAll = () => {
    const size = worldRef.current;
    if (!size) return;
    commit(fitView({ x: 0, y: 0, w: size.w, h: size.h }, { w: width ?? 0, h: cap }, bounds, pad));
  };
  const target = () => (selected !== null && boxes.current.has(selected) ? selected : focused.current !== null && boxes.current.has(focused.current) ? focused.current : null);
  const toTarget = () => {
    const id = target();
    if (id === null) return;
    commit(fitView(boxes.current.get(id)!, viewport(1), { min: bounds.min, max: Math.min(1, bounds.max) }, 2 * rem));
  };
  const offered = minimap !== null && world !== null && wantsMinimap(world, { w: width ?? 0, h: cap });
  const middle = (): Point => ({ x: (width ?? 0) / 2, y: heightAt(viewRef.current.k) / 2 });
  const acts: SizingActs = {
    zoomIn: () => commit(zoomAbout(viewRef.current, viewRef.current.k * 1.25, middle(), bounds)),
    zoomOut: () => commit(zoomAbout(viewRef.current, viewRef.current.k / 1.25, middle(), bounds)),
    fit: fitAll,
    toSelection: target() === null ? null : toTarget,
    actual: () => commit(zoomAbout(viewRef.current, 1, middle(), bounds)),
    zoomTo: (k) => commit(zoomAbout(viewRef.current, k, middle(), bounds)),
    minimap: offered ? { on: mapOn, toggle: () => { setMapOn(!mapOn); rememberMinimap(!mapOn); } } : null,
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (!on || isComposing(event)) return;
    // The region itself, where it is a stop: the bare keys of the map, which a node or a field keeps for itself.
    if (options.stop && event.target === pane.current && !event.altKey && !event.ctrlKey && !event.metaKey) {
      const moves: Record<string, (() => void) | undefined> = {
        '+': acts.zoomIn, '=': acts.zoomIn, '-': acts.zoomOut, '0': event.shiftKey ? undefined : fitAll,
        ArrowLeft: () => commit(panBy(viewRef.current, -ARROW, 0)), ArrowRight: () => commit(panBy(viewRef.current, ARROW, 0)),
        ArrowUp: () => commit(panBy(viewRef.current, 0, -ARROW)), ArrowDown: () => commit(panBy(viewRef.current, 0, ARROW)),
      };
      const run = moves[event.key];
      if (run) {
        event.preventDefault();
        run();
        return;
      }
    }
    if (event.key === 'Escape' && drag.current?.going) {
      event.preventDefault();
      event.stopPropagation();
      end(true);
      return;
    }
    // Escape on the region itself releases the choice, as a press on the ground does.
    if (event.key === 'Escape' && options.stop && event.target === pane.current && selection) {
      event.preventDefault();
      selection.onGround();
      return;
    }
    if (!event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) return;
    if (isField(event.target as Element)) return;
    const run = { Digit1: fitAll, Digit2: toTarget, Digit0: acts.actual }[event.code];
    if (!run) return;
    event.preventDefault();
    run();
  };

  // The focus followed by the smallest pan; the region is never scrolled, whatever focused it (trap 2).
  const onFocus = (event: FocusEvent<HTMLDivElement>) => {
    if (!on) return;
    const region = event.currentTarget;
    const holder = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-node]');
    if (holder) focused.current = holder.dataset.flowNode!;
    const shown = holder ?? (event.target as HTMLElement);
    const scene = element.current;
    if (scene && scene.contains(shown)) {
      // While a way or a control is pressed the view is the person's, and a card or line shifted from under the pointer loses its click;
      // a press that is no drag follows the focus when it ends.
      if (connection.active() || pressing.current) pressedFocus.current = shown;
      else {
        const box = worldBox(shown, scene.getBoundingClientRect(), viewRef.current.k);
        if (box.w > 0 || box.h > 0) commit(reveal(viewRef.current, box, viewport(viewRef.current.k), 2 * rem));
      }
    }
    region.scrollTop = 0;
    region.scrollLeft = 0;
  };
  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    const next = event.relatedTarget as Node | null;
    if (!next || !event.currentTarget.contains(next)) focused.current = null;
  };
  const onScroll = (event: { currentTarget: HTMLDivElement }) => {
    if (!on) return;
    event.currentTarget.scrollTop = 0;
    event.currentTarget.scrollLeft = 0;
  };

  // One wrapper and one pane in both modes, so the pane the width is followed on is never drawn anew.
  if (!on) {
    return (
      <div
        className="min-w-0"
        onPointerDown={connection.down}
        onPointerMove={connection.move}
        onPointerUp={connection.up}
        onPointerCancel={connection.lost}
        onLostPointerCapture={connection.lost}
      >
        {showTools && <div className="mb-2 flex flex-wrap justify-end gap-x-1">{tools}</div>}
        <div ref={pane} {...paneProps} className="min-w-0 overflow-x-auto">{children}</div>
      </div>
    );
  }

  const shift = translation(view);
  // What an edited world's presses grow by below 100% (a ＋'s, `GapInsert`): held while a gesture lasts, since a custom property is
  // inherited and every change of it styles the whole world again, so a zoom frame stays the compositor's.
  if (!moving) hitScale.current = Math.min(1, Math.round(view.k * 20) / 20);
  const worldStyle = {
    transform: `translate(${shift.x}px, ${shift.y}px) scale(${view.k})`,
    transformOrigin: '0 0',
    minWidth: width,
    willChange: moving ? 'transform' : undefined,
    ...(options.mode === 'edit' ? { '--canvas-k': hitScale.current } : {}),
  } as CSSProperties;
  return (
    <div className="min-w-0" onKeyDown={onKeyDown}>
      {showTools || on ? <CanvasToolbar tools={tools} k={view.k} bounds={bounds} fit={options.fit} acts={acts} sizing={!options.fixed} /> : null}
      <div
        ref={pane}
        {...paneProps}
        tabIndex={options.stop ? 0 : undefined}
        role="region"
        aria-label={label}
        aria-roledescription={options.moves}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerCancel}
        onLostPointerCapture={onPointerCancel}
        onFocus={onFocus}
        onBlur={onBlur}
        onScroll={onScroll}
        className={cn('relative min-w-0 overflow-hidden rounded-control bg-page outline -outline-offset-1 outline-line', drag.current?.going && 'cursor-grabbing')}
        style={{ height: heightAt(view.k, world), touchAction: 'none' }}
      >
        <div ref={element} {...{ [WORLD]: '', [SCALE]: String(view.k) }} className="absolute left-0 top-0 w-max" style={worldStyle}>
          {children}
        </div>
        {overlay?.(view, viewport(view.k))}
        {offered && mapOn && minimap && world && (
          <Minimap world={world} view={view} viewport={viewport(view.k)} boxes={minimap.boxes} onGo={(next) => commit(next)} />
        )}
        {lasso && (
          <div aria-hidden="true" data-canvas-box="" className="pointer-events-none absolute border border-dashed border-accent" style={{ left: lasso.x, top: lasso.y, width: lasso.w, height: lasso.h }} />
        )}
        {connection.overlay}
      </div>
    </div>
  );
}
