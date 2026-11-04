import { type ReactNode, useEffect } from 'react';
import { sentence } from '../locales/format';
import { type IconName, Icon } from './atoms/Icon';
import { Inline } from './atoms/Card';
import { type Notify } from './atoms/Toasts';
export * from './atoms/Icon';
export * from './atoms/Button';
export * from './atoms/Pill';
export * from './atoms/Marks';
export * from './atoms/Card';
export * from './atoms/Drawer';
export * from './atoms/FormControls';
export * from './atoms/Menu';
export * from './atoms/Tip';
export * from './atoms/SettingForm';
export * from './atoms/Toasts';

// The platform's component language (D41), rebuilt on headless primitives (D42): Radix supplies the
// behaviour — focus traps, dismissal, ARIA, typeahead — and every pixel stays ours, which is how the
// paper character survives a component library.

/* ---------------------------------------------------------------- states */

/** A designed nothing: the glyph, the fact, and the action that would change the fact. */
export function EmptyState({ icon, headline, body, action }: {
  icon: IconName; headline: string; body: string; action?: ReactNode;
}) {
  return (
    <div data-empty-state="" className="grid justify-items-center gap-1.5 px-4 py-7 text-center text-ink-faint">
      <Icon name={icon} size={26} />
      <p className="mt-1 text-body font-semibold text-ink">{headline}</p>
      <p className="max-w-[28rem] text-pretty text-body text-ink-soft"><Inline text={body} /></p>
      {action && <div className="mt-2.5">{action}</div>}
    </div>
  );
}

/** Static two-tone placeholders — no shimmer: calmer on paper, and safe under reduced motion. */
export function SkeletonRows({ rows = 3 }: { rows?: number }) {
  return (
    <div className="grid gap-2 py-1.5" aria-hidden>
      {Array.from({ length: rows }, (_, index) => (
        <i
          key={index}
          className="block h-3.5 rounded-[4px] bg-accent-soft opacity-55"
          style={{ width: index === 1 ? '82%' : index === 2 ? '64%' : '100%' }}
        />
      ))}
    </div>
  );
}

/** Surface a query error as a toast exactly once per change. */
export function useErrorNotify(error: unknown, notify: Notify) {
  useEffect(() => {
    if (error) notify(sentence(error), 'error');
  }, [error, notify]);
}

/**
 * A failed action as the person reads it: its own sentence — the service's or the driver's — as an
 * error toast. Every mutation's `onError` is handed this, so none can forget `sentence` (frontend
 * architecture §4a); it was written out at some thirty doors (REV3 CLEAN1).
 */
export const failure = (notify: Notify) => (error: unknown): void => notify(sentence(error), 'error');
