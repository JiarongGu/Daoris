import { type PointerEvent, type ReactNode, type RefObject, useEffect, useRef, useState } from 'react';
import { isComposing } from '../../lib/composing';
import { edgeDrift } from './gesture';
import type { Point } from './viewMath';

// The connection line (CANVAS1b; D224, the canvas design §2.5), which takes over the chart's port handling with its drop rules: a press on
// a port begins the way, a drag from it draws a rubber line from the port's box to the pointer with the step under it outlined where
// it can take the way, and a release over that step commits one intent. The view pans at the viewport's edge, Escape and a lost capture
// cancel, and a release on open ground is the feature's.

/** What a feature hands the canvas to connect with. */
export type Connect = {
  /** The port a press landed on: its node and outcome; null where it is not one. */
  from: (target: Element) => { id: string; port: string } | null;
  /** The press on the port: the way begins, and the feature's pick mode with it. */
  onStart: (id: string, port: string) => void;
  /** Whether a step can take the way being drawn. */
  can: (id: string) => boolean;
  /** A release over a step that can take it. */
  onConnect: (to: string) => void;
  /** A release on open ground. */
  onGround: () => void;
  /** The drag given up: Escape, a pointer cancelled or its capture lost. */
  onCancel: () => void;
};

/** A drag's travel, in screen pixels, before a press on a port is a line. */
const THRESHOLD = 3;
/** How near the viewport's edge the view pans, and how fast at the edge itself, in screen px (per frame). */
const EDGE = 32;
const SPEED = 14;
/** The controls a release on is theirs, not the ground's. */
const CONTROLS = 'button, a, input, select, textarea';

type Line = { from: Point; to: Point; over: { x: number; y: number; w: number; h: number } | null };

/**
 * The connection line's gestures for a pane: the pointer handlers to attach, and the overlay (drawn in the pane's own screen space, so
 * it starts inside its port's box at every scale). `pan` moves the view by screen pixels.
 */
export function useConnection(
  connect: Connect | null | undefined, pane: RefObject<HTMLElement | null>, pan: (dx: number, dy: number) => void,
): {
  down: (event: PointerEvent) => void; move: (event: PointerEvent) => void; up: (event: PointerEvent) => void; lost: () => void;
  /** Whether a way is being drawn: the view is the person's meanwhile, and no focus the way begins moves it. */
  active: () => boolean;
  /** Whether the press just ended was dragged: a plain press on a port is followed by the view, a drawn line is not. */
  dragged: () => boolean;
  overlay: ReactNode;
} {
  const latest = useRef(connect);
  latest.current = connect;
  const press = useRef<{ pointerId: number; port: string; source: HTMLElement; from: Point; dragging: boolean } | null>(null);
  const pointer = useRef<Point>({ x: 0, y: 0 });
  const [line, setLine] = useState<Line | null>(null);
  const [drift, setDrift] = useState<Point | null>(null);

  const under = (x: number, y: number, fallback: Element | null): Element | null =>
    (typeof document.elementFromPoint === 'function' ? document.elementFromPoint(x, y) : null) ?? fallback;

  const aim = (x: number, y: number, source: HTMLElement): Line | null => {
    const region = pane.current?.getBoundingClientRect();
    if (!region) return null;
    const start = source.getBoundingClientRect();
    const holder = under(x, y, null)?.closest<HTMLElement>('[data-flow-node]');
    const target = holder && latest.current?.can(holder.dataset.flowNode!) ? holder.getBoundingClientRect() : null;
    return {
      // Inside the port's own box: its middle, a quarter up from its foot.
      from: { x: start.left + start.width / 2 - region.left, y: start.bottom - start.height / 4 - region.top },
      to: { x: x - region.left, y: y - region.top },
      over: target ? { x: target.left - region.left, y: target.top - region.top, w: target.width, h: target.height } : null,
    };
  };

  const dragged = useRef(false);
  const clear = () => { dragged.current = press.current?.dragging ?? false; press.current = null; setLine(null); setDrift(null); };

  const down = (event: PointerEvent) => {
    const now = latest.current;
    if (!now || event.button !== 0) return;
    const port = (event.target as Element).closest<HTMLElement>('[data-flow-port]');
    const at = port ? now.from(port) : null;
    if (!port || !at) return;
    press.current = { pointerId: event.pointerId, port: port.dataset.flowPort!, source: port, from: { x: event.clientX, y: event.clientY }, dragging: false };
    now.onStart(at.id, at.port);
  };

  const move = (event: PointerEvent) => {
    const going = press.current;
    if (!going || going.pointerId !== event.pointerId) return;
    pointer.current = { x: event.clientX, y: event.clientY };
    if (!going.dragging) {
      if (Math.hypot(event.clientX - going.from.x, event.clientY - going.from.y) < THRESHOLD) return;
      going.dragging = true;
      pane.current?.setPointerCapture?.(event.pointerId);
    }
    const next = aim(event.clientX, event.clientY, going.source);
    setLine(next);
    const region = pane.current?.getBoundingClientRect();
    if (next && region) {
      const vector = edgeDrift(next.to, { w: region.width, h: region.height }, EDGE, SPEED);
      setDrift(vector.x === 0 && vector.y === 0 ? null : vector);
    }
  };

  const up = (event: PointerEvent) => {
    const going = press.current;
    if (!going || going.pointerId !== event.pointerId) return;
    const now = latest.current;
    clear();
    if (!now) return;
    const target = under(event.clientX, event.clientY, event.target as Element) ?? (event.target as Element);
    if (target.closest('[data-flow-port]')?.getAttribute('data-flow-port') === going.port) return;
    const holder = target.closest<HTMLElement>('[data-flow-node]');
    if (holder) {
      const id = holder.dataset.flowNode!;
      if (now.can(id)) now.onConnect(id);
      return;
    }
    if (!target.closest(CONTROLS)) now.onGround();
  };

  // A pointer cancelled or its capture lost mid-drag is given up safely; a lost capture after a release finds nothing to give up.
  const lost = () => {
    const going = press.current;
    if (!going) return;
    clear();
    if (going.dragging) latest.current?.onCancel();
  };

  // Escape while the line is drawn, heard before the chart's own keys (which would hand the focus to the node and keep the way).
  const drawing = line !== null;
  useEffect(() => {
    if (!drawing) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (isComposing(event)) return;
      if (event.key !== 'Escape') return;
      event.preventDefault();
      event.stopPropagation();
      lost();
    };
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [drawing]);

  // The view drifts while the pointer rests at the edge: a frame loop only for as long as that lasts.
  useEffect(() => {
    const going = press.current;
    if (!drift || !going || typeof requestAnimationFrame !== 'function') return undefined;
    let frame = 0;
    const tick = () => {
      pan(drift.x, drift.y);
      const next = going.source.isConnected ? aim(pointer.current.x, pointer.current.y, going.source) : null;
      if (next) setLine(next);
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [drift]);

  const overlay = line && (
    <>
      <svg aria-hidden="true" data-connection-line="" className="pointer-events-none absolute left-0 top-0 size-full overflow-visible">
        <line x1={line.from.x} y1={line.from.y} x2={line.to.x} y2={line.to.y} className="stroke-accent" strokeWidth="2" strokeDasharray="4 3" />
        <circle cx={line.from.x} cy={line.from.y} r="3" className="fill-accent" />
        <circle cx={line.to.x} cy={line.to.y} r="3" className="fill-accent" />
      </svg>
      {line.over && (
        <div
          aria-hidden="true"
          data-connection-target=""
          className="pointer-events-none absolute rounded-card outline outline-2 outline-offset-2 outline-accent"
          style={{ left: line.over.x, top: line.over.y, width: line.over.w, height: line.over.h }}
        />
      )}
    </>
  );

  return { down, move, up, lost, active: () => press.current !== null, dragged: () => dragged.current, overlay };
}
