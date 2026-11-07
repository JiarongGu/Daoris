import { type ReactNode, useContext, useEffect, useRef, useState } from 'react';
import { cn } from '../../lib/cn';
import * as Tooltip from '@radix-ui/react-tooltip';
import { Icon, FRAME_HELD, FrameHeld } from './Icon';
import { Inline } from './Card';

/**
 * A count on an icon — a CIRCLE for one digit, a pill beyond.
 *
 * @remarks
 * 🔴 Measured before it was fixed: 14px wide and 17.2px tall, because the width came from `min-w-3.5`
 * and the height from the line-height plus the border — two numbers nothing held equal, so a single
 * digit sat in an upright oval. Here the box is one fixed height, the minimum width is the same
 * number, and the text is centred by flex with no line-height of its own to push it taller.
 * `ui.test.tsx` holds the two equal.
 */
export function CountBadge({ count, tone = 'open' }: {
  count: number;
  /**
   * A badge counts only what waits on the person (BADGEUX1, the UI/UX design §3.10), so it wears open's hue, the person's; there is
   * no quantity in the accent. The warning hue is a press that waits unshown with a clock on it (NOTIFY2b, the notification design
   * §3.3: the bell's count of the presses).
   */
  tone?: 'open' | 'warn';
}) {
  if (count <= 0) return null;
  return (
    <span
      aria-hidden
      data-tone={tone}
      className={cn(
        // `px-0.5`, not `px-1`: measured on the window, a single digit plus 4px a side plus the border
        // came to 16.45px — wider than the 16px height by the padding alone. At 2px a side one digit
        // sits inside the circle, and two grow it into a pill as they should.
        'absolute right-0 top-0 flex h-4 min-w-4 items-center justify-center rounded-full border bg-page px-0.5',
        'font-mono text-meta leading-none tabular-nums',
        tone === 'warn' ? 'border-warn text-warn' : 'border-st-open text-ink-open',
      )}
    >
      {count > 99 ? '99+' : count}
    </span>
  );
}

/**
 * One setting, as a row: what it is, one line on what it does, the control at the right — and the
 * reason it exists one hover away, on the glyph, instead of in a paragraph above it.
 *
 * @remarks
 * 🔴 The machine's settings were a long list, and the screenshot said what "long" meant: five
 * cards, each opening with a four-line paragraph, the first control 580px below the title at the
 * D56 scale and the second a full screen down — a settings page laid out as an essay, with the
 * right 60% of every card empty because the prose measure was 65ch and the column was 72rem. A setting
 * is a row, the way every settings surface a person already knows lays one out: the label leads, the
 * hint is one line, the control sits where the eye expects it. The paragraph that motivated the
 * setting is still there, on the info glyph, for the person who asks why.
 *
 * `children` is the row's own extra — a warning the value earned, a notice about it — spanning both
 * columns beneath.
 */
/**
 * The info glyph a reason sits on: the paragraph that motivated a thing, one press or hover away
 * rather than read before it every visit (§4, *a setting is a row*). A note, so a screen reader
 * reads the reason as the glyph's name. Shared by `SettingRow` and any heading that has a why.
 */
export function WhyGlyph({ why }: { why: string }) {
  return (
    <Tip content={why}>
      <span
        tabIndex={0}
        role="note"
        aria-label={why}
        className="inline-flex cursor-help text-ink-faint hover:text-ink"
      >
        <Icon name="info" size={13} />
      </span>
    </Tip>
  );
}


/**
 * A tooltip that carries a sentence — the tier pill's note, the adopted dot's meaning.
 *
 * @remarks
 * **Where it sits and when it goes are the editor's rules**, VS Code's. It opens below
 * the control, aligned to its leading edge, and flips only when there is no room; a rail's tips
 * open beside the rail (`side`). And it goes on **any** scroll, key, click, or loss of the window's
 * focus, and the moment the pointer is off its trigger — Radix alone closes on the trigger's own
 * pointerleave and on Escape, so a tip stayed up when the list under it scrolled, when the window
 * lost focus to the browser a login opened, and when the button it described disabled itself on
 * the click, because a disabled control fires no pointerleave at all.
 */
export function Tip({ content, children, side = 'bottom' }: {
  content: string;
  children: ReactNode;
  /** Which side it opens on — below by default; a vertical rail's tips open beside it. */
  side?: 'top' | 'bottom' | 'left' | 'right';
}) {
  const [open, setOpen] = useState(false);
  // Typed as the button Radix's trigger renders; with `asChild` the ref lands on whatever the child
  // is, and only `contains` is asked of it.
  const trigger = useRef<HTMLButtonElement>(null);
  // A tip on the frame keeps the frame's size while the content zooms.
  const held = useContext(FrameHeld);

  useEffect(() => {
    if (!open) return undefined;
    const close = () => setOpen(false);
    // Off the trigger for any reason at all — a disabled control, a re-rendered row — is closed.
    const offTrigger = (event: PointerEvent) => {
      const at = trigger.current;
      if (at && !at.contains(event.target as Node)) close();
    };
    window.addEventListener('scroll', close, true);
    window.addEventListener('blur', close);
    document.addEventListener('keydown', close, true);
    document.addEventListener('pointerdown', close, true);
    document.addEventListener('pointermove', offTrigger, true);
    return () => {
      window.removeEventListener('scroll', close, true);
      window.removeEventListener('blur', close);
      document.removeEventListener('keydown', close, true);
      document.removeEventListener('pointerdown', close, true);
      document.removeEventListener('pointermove', offTrigger, true);
    };
  }, [open]);

  if (!content) return <>{children}</>;
  return (
    // `disableHoverableContent` is what makes the tooltip untouchable: with hoverable content on,
    // Radix gives the popper WRAPPER pointer events so you can move into the tooltip, and the
    // wrapper then sits over whatever the trigger opened. That is how the workspace scope's tooltip
    // came to swallow clicks on its own options list once it moved into the app strip.
    // Nothing here is meant to be hovered INTO — every Tip carries one sentence.
    <Tooltip.Root open={open} onOpenChange={setOpen} disableHoverableContent>
      <Tooltip.Trigger asChild ref={trigger}>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          side={side}
          align={side === 'top' || side === 'bottom' ? 'start' : 'center'}
          sideOffset={6}
          collisionPadding={8}
          // A tooltip explains; it is never a pointer target. Without this it can sit over the very
          // control it describes and swallow the click — which is exactly what happened when the
          // workspace scope moved into the app strip and its tooltip landed on top of its own
          // options list. The rule belongs here rather than at that one call site.
          className={cn(
            'pointer-events-none z-30 max-w-[22rem] rounded-card border border-line bg-overlay px-3 py-2 text-small text-ink-soft shadow-[0_6px_24px_rgb(15_12_8/0.12)]',
            held && FRAME_HELD,
          )}
        >
          <Inline text={content} />
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  );
}
