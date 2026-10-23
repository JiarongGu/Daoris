import { type CSSProperties, type ReactNode, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { WAIT_PANEL, waitPanelMax } from './waitPanelHeight';

/** What marks a part of the panel whose waits scroll within the panel's height: the strip, and a park's ask in its box. */
export const WAIT_PART = 'data-wait-part';

/** The parts drawn now: a strip standing aside is `hidden`, and takes none of the height. */
const PARTS = `[${WAIT_PART}]:not([hidden])`;

const heightOf = (element: Element) => element.getBoundingClientRect().height;

/** The parts' height as drawn. */
const partsOf = (panel: HTMLElement) => [...panel.querySelectorAll(PARTS)].reduce((total, part) => total + heightOf(part), 0);

/**
 * The room the conversation and the waits share: the main area beside the panel in its column and the parts as drawn. It holds
 * still as a part grows, the main area giving what the part takes. 0 where there is no main area beside it, or nothing is laid out.
 */
function roomOf(panel: HTMLElement): number {
  const main = [...(panel.parentElement?.children ?? [])].find((sibling) => sibling.getAttribute('data-region') === 'main');
  return main ? heightOf(main) + partsOf(panel) : 0;
}

/**
 * **The waiting panel** (WAITPANEL1): what waits on the person at the foot of a session's conversation, the strip and the box
 * under it, with its top edge a handle that sizes it and the conversation's own presses above it.
 *
 * @remarks
 * **The height is the most its waits take**: the strip's waits and a park's ask in its box each scroll within their share of it,
 * and the box's field is never in it, so words are always typed in sight. Fewer waits than the height take their own height. A
 * drag starts from what is drawn, so the edge moves with the pointer from the first pixel.
 *
 * **Bounded both ways**: the floor (`WAIT_PANEL.min`) keeps a line of the strip and a line under it; the ceiling leaves the
 * conversation's main area its own floor (`WAIT_PANEL.conversation`) of the room the two share in the column, read again as the
 * column changes, so a height kept in a tall window never takes a short window's conversation, nor does the output panel's.
 *
 * **The output panel's handle, turned to this panel** (D55): a separator that takes the focus, ↑ and ↓ a step, Home and End to
 * either end, and a double-click back to where it started, as a sash does. Drawn only while a part is: with nothing waiting there
 * is nothing to size.
 *
 * **Its presses above it** (`controls`): *To the top* and *Back to bottom* lie over the conversation's foot, just above the
 * handle, in a layer over the conversation's own sticky lines and over the panel, so neither is ever under it.
 *
 * A molecule: the height and its changes are its holder's (`useWaitPanelHeight`); it holds only which parts are drawn.
 */
export function WaitPanel({ height, onResize, onReset, controls, children }: {
  /** The most its waits take, as kept. */
  height: number;
  onResize: (height: number) => void;
  /** Forget the kept height: a double-click on the handle. */
  onReset?: () => void;
  /** The conversation's presses, drawn above the panel's top edge; none draws nothing there. */
  controls?: ReactNode;
  /** The strip and the box. */
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const panel = useRef<HTMLDivElement>(null);
  const [parts, setParts] = useState(0);
  const [room, setRoom] = useState(0);

  // Which parts are drawn, read again as the strip comes, goes or stands aside: a hook-free child says so by its mark alone.
  useLayoutEffect(() => {
    const node = panel.current;
    if (!node) return undefined;
    const count = () => setParts(node.querySelectorAll(PARTS).length);
    count();
    const observer = typeof MutationObserver === 'undefined' ? null : new MutationObserver(count);
    observer?.observe(node, { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden', WAIT_PART] });
    return () => observer?.disconnect();
  }, []);

  // The room the conversation and the waits share, read again as the column or the main area changes size (a window resized,
  // the output panel dragged, a part come or gone).
  useLayoutEffect(() => {
    const node = panel.current;
    const column = node?.parentElement;
    if (!node || !column) return undefined;
    const read = () => setRoom(roomOf(node));
    read();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(read);
    for (const watched of column.children) observer?.observe(watched);
    observer?.observe(column);
    return () => observer?.disconnect();
  }, [parts]);

  const max = waitPanelMax(typeof window === 'undefined' ? 0 : window.innerHeight, room);
  const clamp = (next: number) => Math.round(Math.min(max, Math.max(WAIT_PANEL.min, next)));
  const kept = clamp(height);
  // What the parts take now; a page that lays nothing out (a test's) says nothing, and the kept height stands for it.
  const drawn = () => {
    const sum = panel.current ? partsOf(panel.current) : 0;
    return sum > 0 ? Math.min(kept, sum) : kept;
  };

  const drag = (event: React.PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;
    event.preventDefault();
    const startY = event.clientY;
    const start = drawn();
    const move = (moved: PointerEvent) => onResize(clamp(start + (startY - moved.clientY)));
    const done = () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', done);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', done);
  };

  // Each part's share of the height, held under the ceiling whatever was kept.
  const style = { '--wait-part': `${Math.floor(kept / Math.max(1, parts))}px` } as CSSProperties;

  return (
    <div ref={panel} data-region="wait-panel" style={style} className="relative flex shrink-0 flex-col">
      {controls && (
        // Over the conversation's foot and above the handle: z-20 lies over the conversation's sticky lines (z-[9]) and the
        // handle (z-10), in the frame's own layer (`isolate`). The row lets the pointer through where it draws nothing.
        <div
          role="group"
          aria-label={t('work.wait.scroll')}
          data-wait-controls=""
          className="pointer-events-none absolute bottom-full right-4 z-20 mb-2 flex gap-1.5 [&>*]:pointer-events-auto"
        >
          {controls}
        </div>
      )}
      {parts > 0 && (
        <div
          role="separator"
          aria-orientation="horizontal"
          aria-label={t('work.wait.resize')}
          aria-valuenow={kept}
          aria-valuemin={WAIT_PANEL.min}
          aria-valuemax={max}
          tabIndex={0}
          onPointerDown={drag}
          onDoubleClick={onReset}
          onKeyDown={(event) => {
            if (event.key === 'ArrowUp') onResize(clamp(drawn() + WAIT_PANEL.step));
            else if (event.key === 'ArrowDown') onResize(clamp(drawn() - WAIT_PANEL.step));
            else if (event.key === 'Home') onResize(max);
            else if (event.key === 'End') onResize(WAIT_PANEL.min);
            else return;
            event.preventDefault();
          }}
          // Over the panel's top border, 6px tall and centred on it, so it takes no height of its own.
          className="absolute inset-x-0 -top-[3px] z-10 h-1.5 cursor-ns-resize bg-transparent transition-colors duration-(--speed) hover:bg-accent-soft focus-visible:bg-accent-soft focus-visible:outline-none"
        />
      )}
      {children}
    </div>
  );
}
