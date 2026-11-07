import { useCallback, useRef, useState, useSyncExternalStore } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { TOAST_LIMIT, withNotice } from '../../data/signals';
import * as Toast from '@radix-ui/react-toast';
import { Icon } from './Icon';
import { Button } from './Button';
import { Inline } from './Card';
import { watchDrawers, drawerOpen } from './Drawer';
import { Tip } from './Tip';

/* ---------------------------------------------------------------- toasts */

/**
 * A toast: its sentence and its kind; and, for an arrival worth interrupting for (NOTIFY2b, the notification design §3.3, the
 * platform language §4's *Toast*), a door into where it is answered, and whether it stays past its seven seconds until its owner
 * takes it away (a terminal's press's, until the view opens or the press ends).
 */
export type ToastItem = {
  id: number; text: string; kind: 'ok' | 'error';
  door?: { label: string; onOpen: () => void };
  stays?: boolean;
};

/** The one shape a view's outcome channel has — declared once, not restated at every prop. */
export type Notify = (text: string, kind?: 'ok' | 'error') => void;

/**
 * A window's notices: the list its corner shows, and the two ways to change it. Capped by
 * `withNotice`, so a burst cannot climb the window — the newest are what a corner can promise to
 * show. Both windows hold one; each had written it out.
 */
export function useToasts(): { toasts: ToastItem[]; notify: Notify; dismiss: (id: number) => void } {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const next = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const notify = useCallback<Notify>((text, kind = 'ok') => {
    const id = next.current;
    next.current += 1;
    setToasts((current) => withNotice(
      current, { id, text, kind }, TOAST_LIMIT, (shown, added) => shown.text === added.text && shown.kind === added.kind));
  }, []);

  return { toasts, notify, dismiss };
}

/**
 * Outcomes and errors, spoken from one corner — the service's sentence verbatim, because the refusal
 * text is the contract. Radix provides the timers, swipe-dismissal and the a11y announcements.
 */
export function Toasts({ items, onClose }: { items: ToastItem[]; onClose: (id: number) => void }) {
  const { t } = useTranslation();
  const beside = useSyncExternalStore(watchDrawers, drawerOpen, drawerOpen);
  return (
    <Toast.Provider swipeDirection="right" duration={7000}>
      {items.map((toast) => (
        <Toast.Root
          key={toast.id}
          // One that stays is closed by its owner or the person, never by its seven seconds.
          duration={toast.stays ? Number.POSITIVE_INFINITY : undefined}
          onOpenChange={(open) => { if (!open) onClose(toast.id); }}
          className={cn(
            'flex items-start gap-2.5 rounded-card border border-line bg-overlay py-2.5 pl-3.5 pr-2.5 text-body',
            'border-l-[3px] shadow-[0_6px_24px_rgb(15_12_8/0.12)] motion-safe:animate-[drawer-in_var(--speed)_ease-out]',
            toast.kind === 'error' ? 'border-l-st-declined' : 'border-l-accent',
          )}
        >
          {/* 🔴 Clamped, with the whole of it one hover away. Seen on the deployed application
              (D62): the driver's hold on an untrusted repository is six lines naming a path, a
              consequence and the command that fixes it — every word of it worth keeping, and as a
              corner toast it covered the card it was about. The first two lines carry the verdict
              and the subject, which is what a glance is for; the rest is there when the glance was
              not enough. Never truncated in the DOM, so a screen reader still hears all of it. */}
          <Tip content={toast.text}>
            <Toast.Description className="line-clamp-2 flex-1"><Inline text={toast.text} /></Toast.Description>
          </Tip>
          {/* Its door (NOTIFY2b): where the arrival is answered, opened on the person's press alone; it confirms nothing. */}
          {toast.door && (
            <Toast.Action asChild altText={toast.door.label}>
              <Button onClick={toast.door.onOpen}>{toast.door.label}</Button>
            </Toast.Action>
          )}
          <Toast.Close asChild>
            <Button variant="ghost" aria-label={t('common.dismiss')}><Icon name="x" size={14} /></Button>
          </Toast.Close>
        </Toast.Root>
      ))}
      {/* 🔴 Beside an open drawer, never over it (UXTOAST1, D41): the drawer holds this corner whole, its foot's presses
          with it. `right` is the drawer's width (`Drawer`) and the corner's gap; under 56rem none fits beside it, and they
          keep the corner. A modal drawer turns the page's pointer off, so a press on a toast went through it, to the scrim
          (closing the drawer) or to the press it covered: the toasts take their own presses while they show. */}
      <Toast.Viewport
        data-beside={beside ? 'drawer' : undefined}
        className={cn(
          'fixed bottom-5 right-5 z-20 flex w-[26rem] max-w-[90vw] flex-col gap-2 outline-none',
          beside && 'min-[56rem]:right-[calc(min(32rem,calc(100%-3rem))+1.25rem)] min-[56rem]:max-w-[calc(100%-min(32rem,calc(100%-3rem))-5.5rem)]',
          items.length > 0 && 'pointer-events-auto',
        )}
      />
    </Toast.Provider>
  );
}
