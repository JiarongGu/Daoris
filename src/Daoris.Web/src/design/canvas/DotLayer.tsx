import { useEffect, useRef } from 'react';
import { type Grid, queryGrid } from './spatialGrid';
import { viewRect } from './tiers';
import type { Size, View } from './viewMath';

/**
 * **The dot tier's layer**: below 4 px of effective `text-meta` a node is a dot, drawn on a canvas2D
 * layer in the viewport's own pixels, `aria-hidden` (the list and the keys reach every node), redrawn once a frame at most while the
 * view moves and never at rest. A molecule: the grid and the view are given.
 */
export function DotLayer({ grid, view, viewport, skip }: { grid: Grid; view: View; viewport: Size; skip: ReadonlySet<string> }) {
  const layer = useRef<HTMLCanvasElement>(null);
  const [width, height] = [Math.max(1, Math.round(viewport.w)), Math.max(1, Math.round(viewport.h))];
  useEffect(() => {
    const element = layer.current;
    const context = element?.getContext('2d');
    if (!element || !context) return undefined;
    const frame = requestAnimationFrame(() => {
      const ratio = window.devicePixelRatio || 1;
      if (element.width !== Math.round(width * ratio) || element.height !== Math.round(height * ratio)) {
        element.width = Math.round(width * ratio);
        element.height = Math.round(height * ratio);
      }
      context.setTransform(ratio, 0, 0, ratio, 0, 0);
      context.clearRect(0, 0, width, height);
      context.fillStyle = getComputedStyle(element).color;
      // A dot is never smaller than 2 px, so a far world is still a field of marks and never a blank.
      const side = Math.max(2, Math.min(4, 160 * view.k * 0.5));
      for (const id of queryGrid(grid, viewRect(view, viewport))) {
        if (skip.has(id)) continue;
        const box = grid.boxes.get(id)!;
        context.fillRect((box.x + box.w / 2 - view.x) * view.k - side / 2, (box.y + box.h / 2 - view.y) * view.k - side / 2, side, side);
      }
    });
    return () => cancelAnimationFrame(frame);
  }, [grid, view.x, view.y, view.k, width, height, skip]);
  return <canvas ref={layer} aria-hidden="true" data-canvas-dots="" className="pointer-events-none absolute left-0 top-0 text-ink-faint" style={{ width, height }} />;
}
