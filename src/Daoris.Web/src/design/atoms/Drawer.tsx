import { type ReactNode, useLayoutEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import * as Dialog from '@radix-ui/react-dialog';
import { type IconName, Icon } from './Icon';
import { Button } from './Button';

/* ---------------------------------------------------------------- drawer */

/**
 * A dialog's Escape, kept while an input method composes. Radix closes a dialog on any Escape, heard on the
 * document before the field it was pressed in, so dropping a composition in Quick Ask's box or the palette's field
 * closed the box with it. Prevented, Radix leaves the dialog open; it prevented the same press itself when it closed.
 */
const composingEscape = (event: KeyboardEvent) => {
  if (isComposing(event)) event.preventDefault();
};

/**
 * How many drawers this window has open, for the toasts. A drawer holds the toasts' corner whole, so while one
 * is open they stand beside it; the drawer and the corner are siblings under the page, never one inside the other, so
 * the count is the window's rather than a context's.
 */
let drawersOpen = 0;
const drawerWatchers = new Set<() => void>();
/**
 * Told at each drawer opened or closed; and whether one is open. A drawer's scrim lies over the whole window, so what is under it
 * is not shown (NOTIFY2b, the notification design §3.4: a press's card under a scrim does not start its two minutes).
 */
export const watchDrawers = (watch: () => void) => {
  drawerWatchers.add(watch);
  return () => { drawerWatchers.delete(watch); };
};
export const drawerOpen = () => drawersOpen > 0;
const countDrawer = (by: 1 | -1) => {
  drawersOpen += by;
  drawerWatchers.forEach((watch) => watch());
};

/**
 * The single detail-and-form surface, on Radix Dialog: focus is trapped, ESC and the scrim
 * dismiss, and the list behind it survives. Every pixel is ours; the behaviour is not hand-rolled.
 * Radix gives the focus back on close only to a `Dialog.Trigger`, which this has none of: a caller
 * that opens it by state gives the focus back itself, a beat after the close.
 */
export function Drawer({ title, meta, onClose, footer, children }: {
  title: string; meta?: ReactNode; onClose: () => void; footer?: ReactNode;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  // Counted before the drawer is painted, so a toast is never drawn over it for a frame.
  useLayoutEffect(() => {
    countDrawer(1);
    return () => countDrawer(-1);
  }, []);
  return (
    <Dialog.Root open onOpenChange={(open) => { if (!open) onClose(); }}>
      <Dialog.Portal>
        {/* 🔴 Below the app strip, never over it. The strip reserves slots the WINDOW paints its
            caption buttons into (SURF7), and a page scrim cannot dim what the page does not draw —
            so a full-bleed one dimmed the title bar and left three bright buttons punched through
            it. Held by `tokens.test.ts`, because it is a rule about every overlay. And above the
            status bar, for the same reason the activity bar is left alone: the frame is three bars,
            and an overlay belongs to the content between them (POLISH3). */}
        <Dialog.Overlay className="fixed bottom-6 left-12 right-0 top-9 z-10 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          // D41 §6 says a drawer is `role="dialog"` WITH `aria-modal`, and Radix sets the role and
          // traps focus but never writes that attribute — so the sentence was true about the design
          // and false about the page for as long as it had existed. Stated here, and asserted by
          // `ui.test.tsx`, because a claim nothing checks is one nobody notices going wrong.
          aria-modal="true"
          onEscapeKeyDown={composingEscape}
          // 🔴 `top-9`, like the scrim beside it — the strip is the window's title bar, and
          // the caption buttons the window paints there are painted OVER the page. A panel that
          // started at the top put its own header, close button included, under them: on the
          // deployed application the drawer's × sat exactly beneath the window's ✕, neither
          // dimmed nor reachable. Held by `tokens.test.ts` for every panel, as the scrim is.
          className={cn(
            'fixed bottom-6 right-0 top-9 z-10 flex flex-col border-l border-line bg-overlay focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]',
            // Never over the activity bar (3rem): the frame is three bars, and an overlay sits between
            // them. A wide one read a source file until FRAME1f gave an entry the main area (U43). The toasts
            // stand this width in from the right while it is open (`Toasts`, UXTOAST1), and `ui.test.tsx` holds the two
            // together.
            'w-[min(32rem,calc(100%-3rem))]',
          )}
        >
          <header className="flex items-start justify-between gap-4 border-b border-line px-5 pb-3.5 pt-4">
            <div>
              <Dialog.Title className="text-title font-[650] leading-[1.35]">{title}</Dialog.Title>
              {meta && (
                <div className="mt-1.5 flex flex-wrap items-baseline gap-2 text-small text-ink-soft">
                  {meta}
                </div>
              )}
            </div>
            <Dialog.Close asChild>
              <Button variant="ghost" aria-label={t('common.close')}><Icon name="x" /></Button>
            </Dialog.Close>
          </header>
          <div className="flex-1 overflow-y-auto px-5 py-4">{children}</div>
          {footer && <footer className="border-t border-line px-5 py-3.5">{footer}</footer>}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/* ---------------------------------------------------------------- the palette's box */

/**
 * A box at the palette's place: the command palette, and Quick Ask, which is Ask Daoris's conversation
 * in the same place. Near the top, not centred, since what it holds grows downward and a centred box jumps as it does;
 * modal on Radix Dialog, as the drawer is, with the title bar and the status bar left live around it (`tokens.test.ts`'s
 * scrim bounds), and Escape or a press outside closing it.
 *
 * @remarks
 * Two boxes wrote this out on their own beside the drawer, so the atoms did not own the dialog primitive. The
 * size is by what it holds: a list grows to its rows up to 60% of the window (the palette), a conversation keeps one
 * height it fills (`fill`), and a conversation is wider (`wide`).
 */
export function QuickPanel({ open, onClose, title, header, wide = false, fill = false, initialFocus, children }: {
  open: boolean;
  onClose: () => void;
  /** Its accessible name, and its header's title where it has a header. */
  title: string;
  /**
   * Its header: the glyph before the title, the acts after it, and its close. Absent, the title is for a reader alone
   * and the box is what it holds, as the palette is its field and its list.
   */
  header?: { icon: IconName; actions?: ReactNode; closeLabel: string };
  /** A conversation's width rather than a list's. */
  wide?: boolean;
  /** One height its content fills, a conversation's, rather than its rows' up to a cap. */
  fill?: boolean;
  /**
   * Where the focus lands on opening, a selector inside the box (Quick Ask's message box); absent, its first control,
   * or one that asks for it with `autoFocus`.
   */
  initialFocus?: string;
  children: ReactNode;
}) {
  return (
    <Dialog.Root open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <Dialog.Portal>
        {/* Between the frame's bars, as the drawer's scrim is (see `Drawer` and `tokens.test.ts`). It is also what
            VS Code does: its title bar stays live while quick-open is up. */}
        <Dialog.Overlay className="fixed bottom-6 left-12 right-0 top-9 z-20 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          // Radix writes the role and traps focus but not this attribute (see `Drawer`).
          aria-modal="true"
          onEscapeKeyDown={composingEscape}
          onOpenAutoFocus={(event) => {
            const at = initialFocus ? (event.currentTarget as HTMLElement | null)?.querySelector<HTMLElement>(initialFocus) : null;
            if (at) {
              event.preventDefault();
              at.focus();
            }
          }}
          className={cn(
            'fixed left-1/2 top-[12vh] z-20 flex -translate-x-1/2 flex-col overflow-hidden rounded-overlay border border-line bg-overlay',
            'shadow-[0_12px_48px_rgb(15_12_8/0.22)] focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]',
            fill ? 'h-[min(34rem,72vh)]' : 'max-h-[60vh]',
            wide ? 'w-[min(42rem,92vw)]' : 'w-[min(34rem,92vw)]',
          )}
        >
          {header
            ? (
              <header className="flex shrink-0 items-center gap-2 border-b border-line px-3.5 py-2">
                <Icon name={header.icon} size={15} className="text-ink-soft" />
                <Dialog.Title className="m-0 text-body font-semibold text-ink">{title}</Dialog.Title>
                <span className="ml-auto flex items-center gap-0.5">
                  {header.actions}
                  <Dialog.Close asChild>
                    <Button variant="ghost" aria-label={header.closeLabel} className="h-7 w-7 justify-center px-0">
                      <Icon name="x" size={14} />
                    </Button>
                  </Dialog.Close>
                </span>
              </header>
            )
            : <Dialog.Title className="sr-only">{title}</Dialog.Title>}
          {children}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
