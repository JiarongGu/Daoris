import { useCallback, useEffect, useRef } from 'react';
import type { CaptionButtonRect } from '@shenora/react';
import { WindowCommands, useShenora, useWindowMaximized } from '@shenora/react';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS } from './work/caption';

export { CAPTION_ATTRIBUTE, CAPTION_SLOTS, type CaptionSlot } from './work/caption';

// The window's own chrome, page side (SURF7 / D56). The strip that D56 built as a region becomes the
// title bar: it drags the window, double-click maximizes it, a strip at its very top resizes, and the
// room reserved at its right is handed to the OS as real caption buttons.
//
// **The window paints those buttons; the page only reserves and reports their rectangles** (D56 as
// amended, which has the reason).
//
// None of this exists in a browser. Every call goes through the bridge and simply rejects there, which
// is why each one is fire-and-forget: a window command that fails is a window that was never ours.

/**
 * Where the page reserved each caption button, in CSS px relative to the viewport — which is exactly
 * what `getBoundingClientRect()` gives and exactly what the host converts using the WebView's own
 * `DeviceDpi`.
 *
 * @remarks
 * **A zero-sized slot is dropped rather than reported.** An unlaid-out or hidden element measures
 * 0×0, and a zero rectangle either gets skipped host-side or — worse, if it ever stopped being
 * skipped — claims a degenerate region. Dropping here means the host is never asked to.
 *
 * Exported separately from the hook because it is the part worth testing: given a strip, which
 * rectangles does the OS get told about.
 */
export function captionRects(container: HTMLElement | null): CaptionButtonRect[] {
  if (!container) return [];

  return CAPTION_SLOTS.flatMap((kind) => {
    const slot = container.querySelector(`[${CAPTION_ATTRIBUTE}="${kind}"]`);
    if (!slot) return [];

    const { x, y, width, height } = slot.getBoundingClientRect();
    if (width <= 0 || height <= 0) return [];

    return [{ kind, x, y, width, height }];
  });
}

/** True when the OS is in dark mode, as the page's own tokens read it. */
function osPrefersDark(): boolean {
  try {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  } catch {
    // A jsdom without matchMedia, or a locked-down embedder. Dark is the shell's own default fill.
    return true;
  }
}

/**
 * The app strip, wired to the window it sits in.
 *
 * @remarks
 * **The caption rectangles are a snapshot and must be re-sent on every layout change** — a stale one
 * moves the hit-test off the button a person can see, which presents as "close sometimes does
 * nothing". A `ResizeObserver` on the strip catches the layout moving under a workspace switcher
 * appearing or a language changing; `resize` catches the window itself.
 *
 * **Dragging a maximized window restores it first**, which is what a native caption drag does — and
 * the host refuses `START_DRAG` while maximized anyway, because a manual work-area maximize keeps
 * `WindowState.Normal` and the OS would drag the maximized-size window with stale restore bounds.
 *
 * **The theme is pushed, not polled.** Without it a runtime light↔dark switch leaves the DWM border
 * and the caption buttons in the old theme, because those are painted natively and never see the
 * page's CSS.
 */
export function useWindowChrome() {
  const commands = useRef<WindowCommands | undefined>(undefined);
  const strip = useRef<HTMLElement | null>(null);
  const maximized = useWindowMaximized();
  // The bridge, not the driver: the caption room has to be right on the FIRST paint, and the
  // driver's answer arrives a round-trip later. Reserving it late would shift the strip's contents
  // sideways once, which is the one thing reserving the room early was meant to avoid.
  const { isAvailable } = useShenora();

  const window_ = () => (commands.current ??= new WindowCommands());

  // Failures are the browser, where there is no window to command. Swallowed rather than surfaced:
  // there is nothing a person could do about it and nothing went wrong.
  const fire = (run: (commands: WindowCommands) => Promise<unknown>) => {
    try {
      void run(window_()).catch(() => {});
    } catch {
      // `new WindowCommands()` itself throws with no bridge at all.
    }
  };

  const report = useCallback(() => {
    const rects = captionRects(strip.current);
    if (rects.length > 0) fire((commands) => commands.setCaptionButtons(rects));
  }, []);

  /** Attach to the strip. Reporting starts when the element arrives, not on a timer. */
  const stripRef = useCallback((element: HTMLElement | null) => {
    strip.current = element;
    if (element) report();
  }, [report]);

  useEffect(() => {
    report();

    const onResize = () => report();
    window.addEventListener('resize', onResize);

    // The strip's own layout can move without the window changing size at all — the workspace
    // switcher appearing once the family holds two circles is the case that actually happens.
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(report);
    if (observer && strip.current) observer.observe(strip.current);

    return () => {
      window.removeEventListener('resize', onResize);
      observer?.disconnect();
    };
  }, [report]);

  useEffect(() => {
    const media = typeof window.matchMedia === 'function'
      ? window.matchMedia('(prefers-color-scheme: dark)')
      : null;

    const push = () => fire((commands) => commands.setTheme(osPrefersDark()));
    push();
    media?.addEventListener('change', push);
    return () => media?.removeEventListener('change', push);
  }, []);

  return {
    /** Whether a window is here to command. False is a browser, and the strip stays a strip. */
    present: isAvailable,
    stripRef,
    maximized,
    /**
     * The strip's own pointer-down: hand off to the OS move loop. Restores first when maximized,
     * because that is what a native caption drag does and what the host's refusal expects.
     */
    onDragStart: useCallback(() => {
      if (maximized) fire((commands) => commands.toggleMaximize().then(() => commands.startDrag()));
      else fire((commands) => commands.startDrag());
    }, [maximized]),
    onToggleMaximize: useCallback(() => fire((commands) => commands.toggleMaximize()), []),
    /** The sliver above the strip: the frameless technique gives the top edge to the client. */
    onResizeTop: useCallback(() => fire((commands) => commands.startResize('top')), []),
  };
}
