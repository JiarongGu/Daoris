import { type PointerEvent, useEffect, useRef } from 'react';
import { minimapGeometry, viewAtMinimap } from './minimapRule';
import type { Box, Size, View } from './viewMath';

/**
 * **The minimap**: the whole world as small boxes on a canvas2D layer and the view as a rectangle, which
 * a press or a drag moves. A pointer aid, hidden from a reader (the toolbar, the keys and the list reach everything it does); the
 * canvas offers it by `minimap.ts`'s rule. A molecule: the view is the canvas's.
 */
export function Minimap({ world, view, viewport, boxes, onGo }: {
  world: Size;
  view: View;
  viewport: Size;
  boxes: readonly Box[];
  onGo: (view: View) => void;
}) {
  const layer = useRef<HTMLCanvasElement>(null);
  const geometry = minimapGeometry(world, view, viewport);
  const [width, height] = [Math.max(1, Math.round(geometry.width)), Math.max(1, Math.round(geometry.height))];

  useEffect(() => {
    const element = layer.current;
    const context = element?.getContext('2d');
    if (!element || !context) return;
    context.clearRect(0, 0, width, height);
    context.fillStyle = getComputedStyle(element).color;
    for (const box of boxes) context.fillRect(box.x * geometry.scale, box.y * geometry.scale, Math.max(1, box.w * geometry.scale), Math.max(1, box.h * geometry.scale));
  }, [boxes, width, height, geometry.scale]);

  const go = (event: PointerEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    onGo(viewAtMinimap({ x: event.clientX - rect.left, y: event.clientY - rect.top }, geometry, view, viewport));
  };
  const down = (event: PointerEvent<HTMLDivElement>) => {
    event.stopPropagation();
    event.preventDefault();
    event.currentTarget.setPointerCapture?.(event.pointerId);
    go(event);
  };
  const move = (event: PointerEvent<HTMLDivElement>) => {
    event.stopPropagation();
    if (event.buttons & 1) go(event);
  };
  const stop = (event: PointerEvent<HTMLDivElement>) => event.stopPropagation();

  return (
    <div
      aria-hidden="true"
      data-canvas-minimap=""
      className="absolute bottom-2 right-2 cursor-pointer rounded-control border border-line bg-raised/90 text-ink-faint"
      style={{ width, height, touchAction: 'none' }}
      onPointerDown={down}
      onPointerMove={move}
      onPointerUp={stop}
      onPointerCancel={stop}
    >
      <canvas ref={layer} width={width} height={height} className="absolute inset-0" />
      <div
        data-canvas-minimap-view=""
        className="pointer-events-none absolute border border-accent bg-accent/10"
        style={{ left: geometry.rect.x, top: geometry.rect.y, width: geometry.rect.w, height: geometry.rect.h }}
      />
    </div>
  );
}
