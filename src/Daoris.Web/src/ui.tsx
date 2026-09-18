import { useEffect, useRef, type ReactNode } from 'react';

// The platform's small component language (docs/2026-09-19-platform-ux.md, D41). Everything here is
// hand-rolled on purpose: a dozen stroke icons and three primitives do not earn a dependency, and the
// paper character survives better in 200 lines of our own than under a framework's defaults.

/** Inline stroke icons, 24-unit grid, drawn by hand. Decorative — always beside a real label. */
const PATHS: Record<string, ReactNode> = {
  overview: (
    <>
      <rect x="3.5" y="3.5" width="7" height="7" rx="1.5" />
      <rect x="13.5" y="3.5" width="7" height="7" rx="1.5" />
      <rect x="3.5" y="13.5" width="7" height="7" rx="1.5" />
      <rect x="13.5" y="13.5" width="7" height="7" rx="1.5" />
    </>
  ),
  quests: (
    <>
      <path d="M6.5 8h11" />
      <path d="m14.5 5 3 3-3 3" />
      <path d="M17.5 16h-11" />
      <path d="m9.5 13-3 3 3 3" />
    </>
  ),
  projects: (
    <>
      <path d="M12 3.5 4 7.5l8 4 8-4-8-4Z" />
      <path d="m4 12.5 8 4 8-4" />
      <path d="m4 17 8 4 8-4" />
    </>
  ),
  convergence: (
    <>
      <circle cx="6" cy="5.5" r="2" />
      <circle cx="6" cy="18.5" r="2" />
      <circle cx="18" cy="12" r="2" />
      <path d="M6 7.5v9" />
      <path d="M6.5 9c1 3.5 5 4.5 9 4.7" />
    </>
  ),
  search: (
    <>
      <circle cx="11" cy="11" r="6" />
      <path d="m15.5 15.5 4.5 4.5" />
    </>
  ),
  refresh: (
    <>
      <path d="M20.5 4.5v5h-5" />
      <path d="M3.5 12a8.5 8.5 0 0 1 16.5-2.8" />
      <path d="M3.5 19.5v-5h5" />
      <path d="M20.5 12A8.5 8.5 0 0 1 4 14.8" />
    </>
  ),
  plus: (
    <>
      <path d="M12 5v14" />
      <path d="M5 12h14" />
    </>
  ),
  x: (
    <>
      <path d="m6 6 12 12" />
      <path d="M18 6 6 18" />
    </>
  ),
  check: <path d="m5 12.5 4.5 4.5L19 7" />,
  clock: (
    <>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5V12l3 2.5" />
    </>
  ),
  inbox: (
    <>
      <path d="M4 13.5V18a1.5 1.5 0 0 0 1.5 1.5h13A1.5 1.5 0 0 0 20 18v-4.5" />
      <path d="M4 13.5h4.5l1.5 2.5h4l1.5-2.5H20" />
      <path d="M6 13.5 8 5h8l2 8.5" />
    </>
  ),
};

export function Icon({ name, size = 16 }: { name: keyof typeof PATHS | string; size?: number }) {
  return (
    <svg
      className="icon" width={size} height={size} viewBox="0 0 24 24" aria-hidden="true"
      fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"
    >
      {PATHS[name]}
    </svg>
  );
}

/** Every view opens with one of these: the title, one line of what it is for, one primary action. */
export function PageHeader({ title, description, action }: {
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <header className="page-head">
      <div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      {action && <div className="page-action">{action}</div>}
    </header>
  );
}

/**
 * The single detail-and-form surface (D41): a knowledge entry, a quest's detail, the compose form —
 * one pattern instead of three, and the list behind it survives. ESC, the scrim, and × all close it.
 */
export function Drawer({ title, meta, onClose, footer, children }: {
  title: string;
  meta?: ReactNode;
  onClose: () => void;
  footer?: ReactNode;
  children: ReactNode;
}) {
  const body = useRef<HTMLDivElement>(null);

  useEffect(() => {
    body.current?.focus();
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  return (
    <div className="scrim" onClick={onClose}>
      <aside
        className="drawer" role="dialog" aria-modal="true" aria-label={title}
        onClick={(event) => event.stopPropagation()}
      >
        <header className="drawer-head">
          <div className="drawer-title">
            <h2>{title}</h2>
            {meta && <div className="drawer-meta">{meta}</div>}
          </div>
          <button className="ghost" onClick={onClose} aria-label="close">
            <Icon name="x" />
          </button>
        </header>
        <div className="drawer-body" tabIndex={-1} ref={body}>{children}</div>
        {footer && <footer className="drawer-foot">{footer}</footer>}
      </aside>
    </div>
  );
}

/** A designed nothing: the glyph, the fact, and the action that would change the fact. */
export function EmptyState({ icon, headline, body, action }: {
  icon: string;
  headline: string;
  body: string;
  action?: ReactNode;
}) {
  return (
    <div className="empty-state">
      <Icon name={icon} size={26} />
      <p className="empty-headline">{headline}</p>
      <p className="empty-body">{body}</p>
      {action}
    </div>
  );
}

export type ToastItem = { id: number; text: string; kind: 'ok' | 'error' };

/**
 * Action outcomes and errors, spoken from one place — the service's sentence verbatim, because the
 * refusal text is the contract. Fixed to a corner so nothing shifts the layout to speak.
 */
export function Toasts({ items, onClose }: { items: ToastItem[]; onClose: (id: number) => void }) {
  if (items.length === 0) return null;
  return (
    <div className="toasts">
      {items.map((toast) => (
        <div key={toast.id} className={`toast ${toast.kind}`} role="status">
          <span>{toast.text}</span>
          <button className="ghost" onClick={() => onClose(toast.id)} aria-label="dismiss">
            <Icon name="x" size={14} />
          </button>
        </div>
      ))}
    </div>
  );
}

/** Static two-tone placeholders — no shimmer, calmer on paper, and safe under reduced motion. */
export function SkeletonRows({ rows = 3 }: { rows?: number }) {
  return (
    <div className="skeleton-rows" aria-hidden="true">
      {Array.from({ length: rows }, (_, index) => <i key={index} />)}
    </div>
  );
}
