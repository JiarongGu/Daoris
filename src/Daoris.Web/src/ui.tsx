import { type ButtonHTMLAttributes, type ReactNode, useEffect, useRef } from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import { sentence } from './format';
import * as Toast from '@radix-ui/react-toast';
import * as RadixSelect from '@radix-ui/react-select';
import * as Tooltip from '@radix-ui/react-tooltip';
import * as Checkbox from '@radix-ui/react-checkbox';
import {
  ArrowLeftRight, Check, ChevronDown, ChevronRight, Cloud, CloudOff, FileDiff, GitMerge, Inbox,
  KeyRound, Languages, LayoutDashboard, LayoutGrid, Layers, LogIn, Monitor, Plus, RotateCw, Search,
  SlidersHorizontal, SquareArrowOutUpRight, SquareTerminal, Trash2, X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Quest, SessionState } from './api';
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
  settings: SlidersHorizontal,
  refresh: RotateCw,
  plus: Plus,
  x: X,
  check: Check,
  inbox: Inbox,
  languages: Languages,
  // The review's own three (SURF6): a disclosure arrow both ways, and the dock's tab.
  chevronDown: ChevronDown,
  chevronRight: ChevronRight,
  diff: FileDiff,
  // The second screen (SURF8): the monitor window, and popping one session out into its own.
  monitor: Monitor,
  external: SquareArrowOutUpRight,
  // 🔴 The two FRAMES, and they need glyphs of their own. Manage first wore `overview` — the same
  // grid as the Overview domain three rows below it — so the rail showed one icon twice meaning two
  // different things, which is worse than an unlabelled icon: it is a wrong label.
  frameManage: LayoutDashboard,
  frameWork: SquareTerminal,
  // The status bar's own two (2026-09-22): whether this circle has a deployment wired. A bar item
  // that is a word alone reads as a caption, and the glyph is what makes it scan as an item.
  cloud: Cloud,
  cloudOff: CloudOff,
  // The accounts surface (2026-09-22): an account, signing into one, and letting one go. `Trash2`
  // 🔴 is deliberately NOT on Forget — forgetting removes nothing, and a bin says it does.
  account: KeyRound,
  login: LogIn,
  remove: Trash2,
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
        'inline-flex min-h-[1.9rem] items-center gap-1.5 rounded-control px-3 py-1.5 text-body',
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

/**
 * The same mapping for a session's nine states — exhaustive at compile time for the same reason,
 * and shared for a second one: the rail, the head and the quest card must not disagree about what
 * `stood-down` looks like, and three copies of a nine-row map disagree eventually.
 *
 * `awaiting-person` wears the attention tone deliberately: it is the one state nothing but a person
 * can clear, so it should not sit quietly among the running ones.
 */
export const SESSION_TONE: Record<SessionState, keyof typeof PILL_TONE> = {
  'queued': 'open',
  'starting': 'taken',
  'working': 'taken',
  'awaiting-person': 'declined',
  'completed': 'done',
  'declined': 'declined',
  'stood-down': 'neutral',
  'failed': 'declined',
  'stopped': 'neutral',
};

/**
 * Which states still hold their repository — the wire half of `Session.Active`, exhaustive here for
 * the reason the tone map is: the rail, the quest card and the attention band all ask it, and a set
 * spelled out three times acquires a tenth state in two of them.
 */
export const SESSION_ACTIVE: ReadonlySet<SessionState> =
  new Set<SessionState>(['queued', 'starting', 'working', 'awaiting-person']);

/**
 * The same nine states as a LIVENESS mark — `SESSION_TONE`'s sibling, because a pill and a dot
 * answer different questions: the pill says which state, the dot says whether anything is happening.
 *
 * @remarks
 * **Attention outranks activity, structurally rather than by precedence.** The reference console
 * spends a rule on it — a session needing its person wears the attention mark even while its process
 * is busy — and Daoris needs no rule, because `awaiting-person` IS a state and nothing is layered
 * over it. There is deliberately no "is a process alive" input here: the driver's running list is
 * this machine's, and a mirrored session working on another machine is working (D47 §6). The record
 * is what the mark reads.
 */
export const SESSION_DOT: Record<SessionState, keyof typeof DOT_TONE> = {
  'queued': 'idle',
  'starting': 'live',
  'working': 'live',
  'awaiting-person': 'parked',
  'completed': 'ended',
  'declined': 'ended',
  'stood-down': 'ended',
  'failed': 'ended',
  'stopped': 'ended',
};

/** Quest state on its soft field. The label is always present — status never rides on hue alone. */
export function Pill({ tone = 'neutral', title, children }: {
  tone?: keyof typeof PILL_TONE; title?: string; children: ReactNode;
}) {
  return (
    <span
      title={title}
      className={cn(
        'whitespace-nowrap rounded-full border px-2 py-0.5 font-mono text-meta',
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
      'inline-block rounded-full border px-2.5 py-px text-small leading-[1.45]',
      accent ? 'border-accent bg-accent-soft text-accent' : 'border-line bg-raised text-ink-soft',
    )}
    >
      {children}
    </span>
  );
}

/* ---------------------------------------------------------------- dots, wells, meta lines */

const DOT_TONE: Record<string, { mark: string; word: string }> = {
  live: { mark: 'bg-accent motion-safe:animate-pulse', word: 'text-accent' },
  parked: { mark: 'bg-st-declined', word: 'text-st-declined' },
  ended: { mark: 'bg-st-done', word: 'text-ink-soft' },
  idle: { mark: 'bg-line', word: 'text-ink-faint' },
};

/**
 * A liveness mark and the word for it — one component, because the label is not optional (D41 §6).
 *
 * @remarks
 * The mark is `aria-hidden`: the label already IS the accessible name, and a screen reader that
 * announces the state twice is worse served than one that hears it once. Hue distinguishes the four
 * at a glance for the people hue works for; the word is what everyone else reads.
 */
export function Dot({ tone = 'idle', label, className }: {
  tone?: keyof typeof DOT_TONE; label: string; className?: string;
}) {
  const shade = DOT_TONE[tone];
  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 whitespace-nowrap text-meta', shade.word, className,
    )}
    >
      <span aria-hidden className={cn('inline-block size-1.5 shrink-0 rounded-full', shade.mark)} />
      {label}
    </span>
  );
}

/**
 * A verbatim monospace region with a "what fell out" footer — a session's console, a build log, a
 * diff hunk.
 *
 * @remarks
 * **Never translated**: what a tool said is data, and that is where the platform's i18n boundary
 * sits. The chrome around it — the label, the live word, the footer — speaks the active language.
 *
 * The window is bounded and the footer states what the bound cost, because a well that silently
 * skipped the middle of a build log would be a worse lie than one that showed nothing.
 *
 * The tail is followed only **while live**; scrolling a finished log out from under a reader is
 * rude, and a person who scrolled up did so on purpose.
 */
export function MonoWell({ text, label, live = false, dropped = 0, tall = false, fill = false }: {
  text: string; label?: ReactNode; live?: boolean; dropped?: number; tall?: boolean;
  /**
   * Fill the height the caller gives instead of capping at one. What a growable output panel needs
   * (D55): a well with a maximum of its own can be put in a taller box and simply not use it.
   */
  fill?: boolean;
}) {
  const { t } = useTranslation();
  const well = useRef<HTMLPreElement>(null);

  useEffect(() => {
    if (live && well.current) well.current.scrollTop = well.current.scrollHeight;
  }, [text, live]);

  return (
    <div className={cn(fill && 'flex min-h-0 flex-1 flex-col')}>
      {(label || live) && (
        <p className="mb-1 flex items-baseline gap-2 text-meta text-ink-faint">
          {label && <span>{label}</span>}
          {live && <Dot tone="live" label={t('console.live')} />}
        </p>
      )}
      <pre
        ref={well}
        className={cn(
          'm-0 overflow-auto whitespace-pre-wrap break-words rounded-control border border-line',
          'bg-raised px-3 py-2.5 font-mono text-small leading-relaxed text-ink-soft',
          fill ? 'min-h-0 flex-1' : tall ? 'max-h-[26rem] min-h-40' : 'max-h-72',
        )}
      >
        {text}
      </pre>
      {dropped > 0 && (
        <p className="mt-1 text-meta text-ink-faint">{t('console.dropped', { count: dropped })}</p>
      )}
    </div>
  );
}

/**
 * The `label · value` pairs of a record's head.
 *
 * @remarks
 * **A pair with no value is absent, never blank.** On a session record every absence means something
 * real — no profile means the harness's own configuration home, no version means the record predates
 * the toolchain, no tree means a browser is reading over a remote — and a dash in the value slot
 * reads as a bug in all three. The caller says what it knows; this renders what it was given.
 */
export function MetaLine({ items, className }: {
  items: { label: string; value: ReactNode; mono?: boolean }[]; className?: string;
}) {
  const shown = items.filter((item) => item.value !== null && item.value !== undefined && item.value !== '');
  if (!shown.length) return null;
  return (
    <dl className={cn('m-0 flex flex-wrap items-baseline gap-x-4 gap-y-1 text-small', className)}>
      {shown.map((item) => (
        <div key={item.label} className="flex min-w-0 items-baseline gap-1.5">
          <dt className="shrink-0 text-ink-faint">{item.label}</dt>
          <dd className={cn(
            'm-0 min-w-0 text-ink-soft', item.mono && 'break-all font-mono text-small',
          )}
          >
            {item.value}
          </dd>
        </div>
      ))}
    </dl>
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
      <span className="text-body font-semibold">{title}</span>
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
      <span className="text-small text-ink-soft">{label}</span>
      <span className="text-value font-semibold leading-[1.15] tracking-[-0.01em]">{value}</span>
      <span className={cn('text-small', warn ? 'text-st-open' : 'text-ink-faint')}>{note}</span>
    </div>
  );
}

/** Every view opens with one of these: the title, one line of purpose, one primary action. */
export function PageHeader({ title, description, action }: {
  title: string; description: string; action?: ReactNode;
}) {
  return (
    <header className="mb-5 flex items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="text-view font-[650] tracking-[-0.01em]">{title}</h1>
        <Prose className="mt-1">{description}</Prose>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </header>
  );
}

/**
 * Explanatory text, at a measure a person can actually read.
 *
 * @remarks
 * **The column is 72rem and prose is not.** D41 §2 caps the CONTENT column, which is right for cards,
 * tiles and tables — but a paragraph inheriting it runs to about **190 characters a line** at the
 * amended 13px scale, roughly triple the 45–75 the eye tracks without losing its place. Measured in
 * the real window before this existed: every sentence on the Machine view was one of those lines.
 *
 * `max-w-prose` is 65ch and font-relative, so it stays right if the scale moves again — which is
 * exactly the property the hardcoded type sizes did not have (D56).
 *
 * This is for the console EXPLAINING itself. It is not for data: a quest's body, a knowledge entry
 * and a session's note are content, and content is shown as it is.
 */
export function Prose({ className, children }: { className?: string; children: ReactNode }) {
  return <p className={cn('m-0 max-w-prose text-body text-ink-soft', className)}>{children}</p>;
}

/**
 * A section's label. One style, and the heading LEVEL is a prop — because inside the attended
 * session the head is already an `h2`, and a second `h2` under it would flatten the region's
 * outline for the readers who navigate by headings. Everywhere else the default is unchanged.
 */
export function SectionTitle({ level = 2, children }: { level?: 2 | 3; children: ReactNode }) {
  const Heading = level === 3 ? 'h3' : 'h2';
  // `mt-6` separates a section from the one above it — and is dead space when the title IS the first
  // thing in its card, which it usually is. Every card in the console was carrying 24px of it above
  // its own heading, on top of the card's own padding.
  return (
    <Heading className="mb-2 mt-6 text-small font-semibold text-ink-faint first:mt-0">
      {children}
    </Heading>
  );
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
        {/* 🔴 Below the app strip, never over it. The strip reserves slots the WINDOW paints its
            caption buttons into (SURF7), and a page scrim cannot dim what the page does not draw —
            so a full-bleed one dimmed the title bar and left three bright buttons punched through
            it. Held by `tokens.test.ts`, because it is a rule about every overlay. */}
        <Dialog.Overlay className="fixed inset-y-0 bottom-0 left-12 right-0 top-9 z-10 bg-scrim" />
        <Dialog.Content
          aria-describedby={undefined}
          // D41 §6 says a drawer is `role="dialog"` WITH `aria-modal`, and Radix sets the role and
          // traps focus but never writes that attribute — so the sentence was true about the design
          // and false about the page for as long as it had existed. Stated here, and asserted by
          // `ui.test.tsx`, because a claim nothing checks is one nobody notices going wrong.
          aria-modal="true"
          className="fixed inset-y-0 right-0 z-10 flex w-[min(32rem,100%)] flex-col border-l border-line bg-overlay focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]"
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

/* ---------------------------------------------------------------- form controls */

export function SelectField({ value, onChange, options, placeholder, ariaLabel, required, bar }: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
  ariaLabel?: string;
  required?: boolean;
  /**
   * 🔴 The status bar's shape rather than a form's (owner, 2026-09-22: *"workspace can also have
   * its switch on the bottom bar"*). It was already there and looked wrong: a 30px bordered control
   * parked in a 24px bar, which reads as a form that fell out of a dialog. A bar item is
   * borderless, full-height, and lights on hover like every other item beside it — the chooser is
   * the same chooser, and only its trigger belongs to the bar.
   */
  bar?: boolean;
}) {
  return (
    <RadixSelect.Root value={value || undefined} onValueChange={onChange} required={required}>
      <RadixSelect.Trigger
        aria-label={ariaLabel}
        className={bar
          ? 'flex h-full items-center gap-1.5 px-2 text-meta text-ink transition-colors duration-[var(--speed)] hover:bg-accent-soft focus-visible:bg-accent-soft focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-accent'
          : 'inline-flex min-h-[1.9rem] items-center justify-between gap-2 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink data-[placeholder]:text-ink-faint'}
      >
        {bar && <Layers size={12} aria-hidden className="shrink-0 text-ink-soft" />}
        <RadixSelect.Value placeholder={placeholder} />
        <ChevronDown size={bar ? 12 : 14} aria-hidden className="text-ink-faint" />
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
                className="flex cursor-pointer items-center justify-between gap-3 rounded-[4px] px-2 py-1.5 text-body outline-none data-[highlighted]:bg-accent-soft data-[highlighted]:text-ink"
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
    <label className="flex cursor-pointer items-center gap-2 whitespace-nowrap text-body text-ink-soft">
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
    // `disableHoverableContent` is what makes the tooltip untouchable: with hoverable content on,
    // Radix gives the popper WRAPPER pointer events so you can move into the tooltip, and the
    // wrapper then sits over whatever the trigger opened. That is how the workspace scope's tooltip
    // came to swallow clicks on its own options list once it moved into the app strip (D56).
    // Nothing here is meant to be hovered INTO — every Tip carries one sentence.
    <Tooltip.Root disableHoverableContent>
      <Tooltip.Trigger asChild>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content
          sideOffset={6}
          // A tooltip explains; it is never a pointer target. Without this it can sit over the very
          // control it describes and swallow the click — which is exactly what happened when the
          // workspace scope moved into the app strip (D56) and its tooltip landed on top of its own
          // options list. The rule belongs here rather than at that one call site.
          className="pointer-events-none z-30 max-w-[22rem] rounded-card border border-line bg-overlay px-3 py-2 text-small text-ink-soft shadow-[0_6px_24px_rgb(15_12_8/0.12)]"
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
            'flex items-start gap-2.5 rounded-card border border-line bg-overlay py-2.5 pl-3.5 pr-2.5 text-body',
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
      <p className="mt-1 text-body font-semibold text-ink">{headline}</p>
      <p className="max-w-[28rem] text-body text-ink-soft">{body}</p>
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

/* ---------------------------------------------------------------- language */

export function LanguageSwitcher({ compact }: { compact?: boolean } = {}) {
  const { i18n, t } = useTranslation();
  const current = i18n.language.startsWith('zh') ? 'zh' : 'en';
  const next = current === 'zh' ? 'en' : 'zh';
  const label = t(`language.${next}`);
  return (
    // The visible label IS the accessible name — the language it switches to, in that language.
    // In the activity bar there is no room for it, so it becomes the accessible name instead: the
    // glyph alone would be a control with no name at all (D41 §6 — icons are decorative BESIDE a
    // real label, so an icon-only control has to carry one).
    <Tip content={compact ? label : ''}>
      <Button
        variant="ghost"
        aria-label={compact ? label : undefined}
        className={compact ? 'h-9 w-9 justify-center px-0' : 'w-full justify-center'}
        onClick={() => void i18n.changeLanguage(next)}
      >
        <Icon name="languages" size={14} />
        {!compact && label}
      </Button>
    </Tip>
  );
}
