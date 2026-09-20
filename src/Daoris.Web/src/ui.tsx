import { type ButtonHTMLAttributes, type ReactNode, useEffect } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import * as Toast from '@radix-ui/react-toast';
import * as RadixSelect from '@radix-ui/react-select';
import * as Tooltip from '@radix-ui/react-tooltip';
import * as Checkbox from '@radix-ui/react-checkbox';
import {
  ArrowLeftRight, Check, ChevronDown, GitMerge, Inbox, Languages, LayoutGrid, Layers,
  Plus, RotateCw, Search, X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Quest } from './api';
import { cn } from './lib/cn';

// The platform's component language (D41), rebuilt on headless primitives (D42): Radix supplies the
// behaviour — focus traps, dismissal, ARIA, typeahead — and every pixel stays ours, which is how the
// paper character survives a component library.

/** The icon roster. Lucide, tree-shaken; always decorative beside a real label. */
const ICONS = {
  overview: LayoutGrid,
  quests: ArrowLeftRight,
  projects: Layers,
  convergence: GitMerge,
  search: Search,
  refresh: RotateCw,
  plus: Plus,
  x: X,
  check: Check,
  inbox: Inbox,
  languages: Languages,
} as const;

export type IconName = keyof typeof ICONS;

export function Icon({ name, size = 16, className }: { name: IconName; size?: number; className?: string }) {
  const Glyph = ICONS[name];
  return <Glyph size={size} strokeWidth={1.7} aria-hidden className={cn('shrink-0', className)} />;
}

/* ---------------------------------------------------------------- buttons */

const BUTTON: Record<string, string> = {
  default:
    'border border-line bg-raised text-ink hover:enabled:border-accent',
  primary:
    'border border-accent bg-accent text-accent-ink hover:enabled:brightness-108',
  ghost:
    'border border-transparent bg-transparent text-ink-soft px-2 py-1 hover:enabled:text-ink hover:enabled:border-line',
  danger:
    'border border-st-declined bg-transparent text-st-declined hover:enabled:bg-st-declined/10',
};

export function Button({
  variant = 'default', className, ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof BUTTON }) {
  return (
    <button
      {...props}
      className={cn(
        'inline-flex min-h-[1.9rem] items-center gap-1.5 rounded-control px-3 py-1.5 text-[0.85rem]',
        'transition-colors duration-(--speed)',
        BUTTON[variant],
        className,
      )}
    />
  );
}

/* ---------------------------------------------------------------- pills & chips */

const PILL_TONE: Record<string, string> = {
  neutral: 'border-line text-ink-soft bg-raised',
  open: 'border-st-open text-st-open bg-st-open/10',
  taken: 'border-st-taken text-st-taken bg-st-taken/10',
  done: 'border-st-done text-st-done bg-st-done/10',
  declined: 'border-st-declined text-st-declined bg-st-declined/10',
};

/**
 * The one mapping from a quest's wire status to its pill tone — exhaustive at compile time, so a
 * fifth status cannot ship half-toned. Views read this rather than lower-casing the wire value.
 */
export const QUEST_TONE: Record<Quest['status'], keyof typeof PILL_TONE> = {
  Open: 'open',
  Taken: 'taken',
  Done: 'done',
  Declined: 'declined',
};

/** Quest state on its soft field. The label is always present — status never rides on hue alone. */
export function Pill({ tone = 'neutral', title, children }: {
  tone?: keyof typeof PILL_TONE; title?: string; children: ReactNode;
}) {
  return (
    <span
      title={title}
      className={cn(
        'whitespace-nowrap rounded-full border px-2 py-0.5 font-mono text-[0.72rem]',
        PILL_TONE[tone],
      )}
    >
      {children}
    </span>
  );
}

export function Chip({ accent, children }: { accent?: boolean; children: ReactNode }) {
  return (
    <span className={cn(
      'inline-block rounded-full border px-2.5 py-px text-[0.75rem] leading-[1.45]',
      accent ? 'border-accent bg-accent-soft text-accent' : 'border-line bg-raised text-ink-soft',
    )}
    >
      {children}
    </span>
  );
}

/* ---------------------------------------------------------------- cards, tiles, headers */

export function Card({ warn, accent, className, children }: {
  warn?: boolean; accent?: boolean; className?: string; children: ReactNode;
}) {
  return (
    <article className={cn(
      'rounded-card border border-line border-l-[3px] bg-raised px-[1.15rem] py-4',
      accent && 'border-l-accent',
      warn && 'border-l-st-open',
      className,
    )}
    >
      {children}
    </article>
  );
}

export function CardHeader({ title, aside }: { title: ReactNode; aside?: ReactNode }) {
  return (
    <header className="flex items-baseline justify-between gap-4">
      <span className="text-[0.95rem] font-semibold">{title}</span>
      {aside}
    </header>
  );
}

/** The stat-tile contract: label · value · context. Values wear proportional figures, deliberately. */
export function Tile({ label, value, note, warn }: {
  label: string; value: ReactNode; note: string; warn?: boolean;
}) {
  return (
    <div className={cn(
      'grid content-start gap-0.5 rounded-card border border-line bg-raised px-4 pb-3.5 pt-3',
      warn && 'border-l-[3px] border-l-st-open',
    )}
    >
      <span className="text-[0.78rem] text-ink-soft">{label}</span>
      <span className="text-[2rem] font-semibold leading-[1.15] tracking-[-0.01em]">{value}</span>
      <span className={cn('text-[0.75rem]', warn ? 'text-st-open' : 'text-ink-faint')}>{note}</span>
    </div>
  );
}

/** Every view opens with one of these: the title, one line of purpose, one primary action. */
export function PageHeader({ title, description, action }: {
  title: string; description: string; action?: ReactNode;
}) {
  return (
    <header className="mb-6 flex items-start justify-between gap-4">
      <div>
        <h1 className="text-[1.25rem] font-[650] tracking-[-0.01em]">{title}</h1>
        <p className="mt-1 text-[0.9rem] text-ink-soft">{description}</p>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </header>
  );
}

export function SectionTitle({ children }: { children: ReactNode }) {
  return <h2 className="mb-2.5 mt-6 text-[0.8rem] font-semibold text-ink-faint">{children}</h2>;
}

/* ---------------------------------------------------------------- drawer */

/**
 * The single detail-and-form surface (D41), on Radix Dialog: focus is trapped, ESC and the scrim
 * dismiss, and the list behind it survives. Every pixel is ours; the behaviour is not hand-rolled.
 */
export function Drawer({ title, meta, onClose, footer, children }: {
  title: string; meta?: ReactNode; onClose: () => void; footer?: ReactNode; children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <Dialog.Root open onOpenChange={(open) => { if (!open) onClose(); }}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-10 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed inset-y-0 right-0 z-10 flex w-[min(32rem,100%)] flex-col border-l border-line bg-overlay focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]"
        >
          <header className="flex items-start justify-between gap-4 border-b border-line px-5 pb-3.5 pt-4">
            <div>
              <Dialog.Title className="text-[1.05rem] font-[650] leading-[1.35]">{title}</Dialog.Title>
              {meta && (
                <div className="mt-1.5 flex flex-wrap items-baseline gap-2 text-[0.8rem] text-ink-soft">
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

/* ---------------------------------------------------------------- form controls */

export function SelectField({ value, onChange, options, placeholder, ariaLabel, required }: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
  ariaLabel?: string;
  required?: boolean;
}) {
  return (
    <RadixSelect.Root value={value || undefined} onValueChange={onChange} required={required}>
      <RadixSelect.Trigger
        aria-label={ariaLabel}
        className="inline-flex min-h-[1.9rem] items-center justify-between gap-2 rounded-control border border-line bg-raised px-2.5 py-1.5 text-[0.9rem] text-ink data-[placeholder]:text-ink-faint"
      >
        <RadixSelect.Value placeholder={placeholder} />
        <ChevronDown size={14} aria-hidden className="text-ink-faint" />
      </RadixSelect.Trigger>
      <RadixSelect.Portal>
        <RadixSelect.Content
          position="popper" sideOffset={4}
          className="z-30 min-w-[var(--radix-select-trigger-width)] overflow-hidden rounded-card border border-line bg-overlay shadow-[0_6px_24px_rgb(15_12_8/0.12)]"
        >
          <RadixSelect.Viewport className="p-1">
            {options.map((option) => (
              <RadixSelect.Item
                key={option.value}
                value={option.value}
                className="flex cursor-pointer items-center justify-between gap-3 rounded-[4px] px-2 py-1.5 text-[0.9rem] outline-none data-[highlighted]:bg-accent-soft data-[highlighted]:text-ink"
              >
                <RadixSelect.ItemText>{option.label}</RadixSelect.ItemText>
                <RadixSelect.ItemIndicator><Check size={14} aria-hidden /></RadixSelect.ItemIndicator>
              </RadixSelect.Item>
            ))}
          </RadixSelect.Viewport>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  );
}

export function CheckField({ checked, onChange, label }: {
  checked: boolean; onChange: (checked: boolean) => void; label: string;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 whitespace-nowrap text-[0.85rem] text-ink-soft">
      <Checkbox.Root
        checked={checked}
        onCheckedChange={(state) => onChange(state === true)}
        className="grid size-4 shrink-0 place-items-center rounded-[4px] border border-line bg-raised data-[state=checked]:border-accent data-[state=checked]:bg-accent data-[state=checked]:text-accent-ink"
      >
        <Checkbox.Indicator><Check size={12} strokeWidth={2.2} aria-hidden /></Checkbox.Indicator>
      </Checkbox.Root>
      {label}
    </label>
  );
}

/** A tooltip that carries a sentence — the tier pill's note, the adopted dot's meaning. */
export function Tip({ content, children }: { content: string; children: ReactNode }) {
  if (!content) return <>{children}</>;
  return (
    <Tooltip.Root>
      <Tooltip.Trigger asChild>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          sideOffset={6}
          className="z-30 max-w-[22rem] rounded-card border border-line bg-overlay px-3 py-2 text-[0.8rem] text-ink-soft shadow-[0_6px_24px_rgb(15_12_8/0.12)]"
        >
          {content}
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  );
}

/* ---------------------------------------------------------------- toasts */

export type ToastItem = { id: number; text: string; kind: 'ok' | 'error' };

/** The one shape a view's outcome channel has — declared once, not restated at every prop. */
export type Notify = (text: string, kind?: 'ok' | 'error') => void;

/**
 * Outcomes and errors, spoken from one corner — the service's sentence verbatim, because the refusal
 * text is the contract. Radix provides the timers, swipe-dismissal and the a11y announcements.
 */
export function Toasts({ items, onClose }: { items: ToastItem[]; onClose: (id: number) => void }) {
  const { t } = useTranslation();
  return (
    <Toast.Provider swipeDirection="right" duration={7000}>
      {items.map((toast) => (
        <Toast.Root
          key={toast.id}
          onOpenChange={(open) => { if (!open) onClose(toast.id); }}
          className={cn(
            'flex items-start gap-2.5 rounded-card border border-line bg-overlay py-2.5 pl-3.5 pr-2.5 text-[0.85rem]',
            'border-l-[3px] shadow-[0_6px_24px_rgb(15_12_8/0.12)] motion-safe:animate-[drawer-in_var(--speed)_ease-out]',
            toast.kind === 'error' ? 'border-l-st-declined' : 'border-l-accent',
          )}
        >
          <Toast.Description className="flex-1">{toast.text}</Toast.Description>
          <Toast.Close asChild>
            <Button variant="ghost" aria-label={t('common.dismiss')}><Icon name="x" size={14} /></Button>
          </Toast.Close>
        </Toast.Root>
      ))}
      <Toast.Viewport className="fixed bottom-5 right-5 z-20 flex w-[26rem] max-w-[90vw] flex-col gap-2 outline-none" />
    </Toast.Provider>
  );
}

/* ---------------------------------------------------------------- states */

/** A designed nothing: the glyph, the fact, and the action that would change the fact. */
export function EmptyState({ icon, headline, body, action }: {
  icon: IconName; headline: string; body: string; action?: ReactNode;
}) {
  return (
    <div className="grid justify-items-center gap-1.5 px-4 py-7 text-center text-ink-faint">
      <Icon name={icon} size={26} />
      <p className="mt-1 text-[0.95rem] font-semibold text-ink">{headline}</p>
      <p className="max-w-[28rem] text-[0.85rem] text-ink-soft">{body}</p>
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
    if (error) notify((error as Error).message, 'error');
  }, [error, notify]);
}

/* ---------------------------------------------------------------- language */

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation();
  const current = i18n.language.startsWith('zh') ? 'zh' : 'en';
  const next = current === 'zh' ? 'en' : 'zh';
  return (
    // The visible label IS the accessible name — the language it switches to, in that language.
    <Button
      variant="ghost"
      className="w-full justify-center"
      onClick={() => void i18n.changeLanguage(next)}
    >
      <Icon name="languages" size={14} />
      {t(`language.${next}`)}
    </Button>
  );
}
