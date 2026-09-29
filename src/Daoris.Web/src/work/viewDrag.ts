import { type DragEvent, useRef, useState } from 'react';
import type { Place, ViewId } from './placements';

// DOCK1e: a view's tab dragged from one region and dropped on the other, as VS Code's tabs are. The
// move is the same `onMove` the tab list's *Move to* calls (DOCK1b), so a drop and a menu press can
// never disagree about what a move does; dragging is the second door, the menu stays the first.

/** What a dragged tab carries: the view, under a type nothing but a Daoris tab writes. */
const VIEW_TYPE = 'application/x-daoris-view';
/**
 * Where it was dragged from, as a type of its own: a page may read a drag's types while it is over a
 * region but not its data, and a region must know whether the view is already its own before the drop,
 * or it would light up for a move that does nothing.
 */
const fromType = (place: Place) => `application/x-daoris-view-from-${place}`;

const VIEWS: readonly string[] = ['timeline', 'review', 'ask', 'console'];

/** What makes a view's tab draggable, and tells the frame while it is being dragged. */
export function dragProps(view: ViewId, from: Place, onDrag?: (view: ViewId | null) => void) {
  return {
    draggable: true,
    onDragStart: (event: DragEvent) => {
      event.dataTransfer.setData(VIEW_TYPE, view);
      event.dataTransfer.setData(fromType(from), view);
      event.dataTransfer.effectAllowed = 'move';
      onDrag?.(view);
    },
    onDragEnd: () => onDrag?.(null),
  };
}

/**
 * A region a view can be dropped on: lit while a view from the OTHER region is over it, and moving the
 * view there on the drop. Nothing at all where there is no `onMove`, as the tab list offers no move.
 */
export function useViewDrop(place: Place, onMove?: (view: ViewId, to: Place) => void) {
  const [over, setOver] = useState(false);
  // Enter and leave fire for every child the pointer crosses, so the region counts them rather than
  // flickering off at the first child's edge.
  const depth = useRef(0);
  const movable = (event: DragEvent) => {
    const types = Array.from(event.dataTransfer?.types ?? []);
    return types.includes(VIEW_TYPE) && !types.includes(fromType(place));
  };

  if (!onMove) return { over: false, props: {} };
  return {
    over,
    props: {
      onDragEnter: (event: DragEvent) => {
        if (!movable(event)) return;
        event.preventDefault();
        depth.current += 1;
        setOver(true);
      },
      onDragOver: (event: DragEvent) => {
        if (!movable(event)) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
      },
      onDragLeave: (event: DragEvent) => {
        if (!movable(event)) return;
        depth.current = Math.max(0, depth.current - 1);
        if (depth.current === 0) setOver(false);
      },
      onDrop: (event: DragEvent) => {
        if (!movable(event)) return;
        event.preventDefault();
        depth.current = 0;
        setOver(false);
        const view = event.dataTransfer.getData(VIEW_TYPE);
        if (VIEWS.includes(view)) onMove(view as ViewId, place);
      },
    },
  };
}
