import {
  type ButtonHTMLAttributes, type ComponentProps, createContext, Fragment, type ReactNode, useCallback, useContext, useEffect,
  useRef, useState,
} from 'react';
import * as Dialog from '@radix-ui/react-dialog';
import * as DropdownMenu from '@radix-ui/react-dropdown-menu';
import { sentence } from './format';
import * as Toast from '@radix-ui/react-toast';
import * as RadixSelect from '@radix-ui/react-select';
import * as Tooltip from '@radix-ui/react-tooltip';
import * as Checkbox from '@radix-ui/react-checkbox';
import {
  ArrowDown, ArrowDownToLine, ArrowLeftRight, ArrowUp, Brain, Check, ChevronDown, ChevronRight, ChevronUp, CircleHelp, Cloud,
  CloudOff, Compass, Copy, Ellipsis, FileDiff, FilePen, FileText, FolderOpen, Gauge, GitMerge, Globe, Inbox, Info, KeyRound, Languages,
  LayoutDashboard, LayoutGrid, Layers, Link, ListTodo, LogIn, Maximize2, Minimize2, Monitor, Network,
  PanelBottom, PanelLeft, PanelLeftClose, PanelLeftOpen, PanelRight, PanelRightClose, Paperclip, Plug, Plus,
  RotateCw, Search, Settings, Shield, Square, SquareArrowOutUpRight, SquareTerminal, Terminal, Trash2, TriangleAlert,
  Wrench, X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Quest, SessionState } from './api';
import { cn } from './lib/cn';
import { TOAST_LIMIT, withNotice } from './signals';

// The platform's component language (D41), rebuilt on headless primitives (D42): Radix supplies the
// behaviour — focus traps, dismissal, ARIA, typeahead — and every pixel stays ours, which is how the
// paper character survives a component library.

/** The icon roster. Lucide, tree-shaken; always decorative beside a real label. */
const ICONS = {
  overview: LayoutGrid,
  quests: ArrowLeftRight,
  projects: Layers,
  convergence: GitMerge,
  // The workspace map (MAP2): repositories as nodes, and what moved between them.
  map: Network,
  search: Search,
  // A GEAR (D66): Settings is the application's settings page now, and a gear is what every
  // application draws for one. The sliders it wore as "Machine" read as a mixer, not a place.
  settings: Settings,
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
  // The way through a long run (SESS1): the previous match, and the jump to where it first failed.
  chevronUp: ChevronUp,
  failure: TriangleAlert,
  // Ask Daoris (HELP1): its button on the strip, and its panel's heading.
  help: CircleHelp,
  // The second screen (SURF8): the monitor window, and popping one session out into its own.
  monitor: Monitor,
  external: SquareArrowOutUpRight,
  // Daoris's own browser (D78). Not the globe: that is a fetch, in the conversation's tool cards.
  browser: Compass,
  // 🔴 The two FRAMES, and they need glyphs of their own. Manage first wore `overview` — the same
  // grid as the Overview domain three rows below it — so the rail showed one icon twice meaning two
  // different things, which is worse than an unlabelled icon: it is a wrong label.
  frameManage: LayoutDashboard,
  frameWork: SquareTerminal,
  // The status bar's own two (2026-09-22): whether this circle has a deployment wired. A bar item
  // that is a word alone reads as a caption, and the glyph is what makes it scan as an item.
  cloud: Cloud,
  cloudOff: CloudOff,
  // Where a circle stands with its remote (SYNC6b), the way every IDE's sync item says it: what is
  // waiting to go up, what could not come level, and what needs a person.
  ahead: ArrowUp,
  behind: ArrowDown,
  conflict: TriangleAlert,
  // The accounts surface (2026-09-22): an account, signing into one, and removing one. The bin is on
  // Remove because Remove now deletes (D66 §3); it was kept off Forget while forgetting removed nothing.
  account: KeyRound,
  login: LogIn,
  remove: Trash2,
  // A setting's WHY (2026-09-23): the paragraph that used to sit above every control, one hover away.
  info: Info,
  // Signing in (2026-09-23): the link the tool printed, copied into a browser of the person's own.
  copy: Copy,
  // What a quest carries (D65 §2): an address, and a file.
  link: Link,
  attach: Paperclip,
  // The menus by domain (D75). A check beside a menu's check column read as "this one is ticked",
  // so what agents may do wears a shield, plugins a plug and usage a gauge.
  shield: Shield,
  plug: Plug,
  gauge: Gauge,
  // The conversation (D76): a tool call wears its ACP kind — reading, editing, running a command
  // (the terminal the Work frame already wears), searching, the web, thinking, anything else — and
  // the plan, and the way back to the tail.
  read: FileText,
  edit: FilePen,
  execute: SquareTerminal,
  fetch: Globe,
  think: Brain,
  tool: Wrench,
  plan: ListTodo,
  toBottom: ArrowDownToLine,
  // The frame's own controls (FRAME6): the rail and the dock closed and opened, and the dock over the frame.
  railClose: PanelLeftClose,
  railOpen: PanelLeftOpen,
  dockClose: PanelRightClose,
  // The layout toggles on the strip (DOCK1c, SURF11) — VS Code's pictures for the same three regions.
  layoutRail: PanelLeft,
  layoutPanel: PanelBottom,
  layoutRight: PanelRight,
  full: Maximize2,
  unfull: Minimize2,
  // A row's own menu (RAIL1).
  more: Ellipsis,
  // Stopping one piece of background work from its tab (CONSOLE3a): the square every player and
  // terminal draws for stop, which is not the bin (removing) or the cross (closing).
  stop: Square,
  // The person's own shell (CONSOLE4b): a prompt, where the console's framed square is a session's output.
  terminal: Terminal,
  // A folder of Daoris's own, opened in the file manager (LOG1c): the machine log's.
  folder: FolderOpen,
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

/**
 * 🔴 **A button, unless it says it submits.** HTML makes an untyped button inside a form its submit,
 * so a click runs its own handler AND the form's: the composer's send sent every message twice, and
 * a key form's cancel saved the key the person was cancelling (2026-09-25). A form's submit says
 * `type="submit"`.
 */
export function Button({
  variant = 'default', type = 'button', className, ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: keyof typeof BUTTON }) {
  return (
    <button
      {...props}
      type={type}
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
 * `awaiting-person` wears the waiting tone deliberately: it is the one state nothing but a person
 * can clear, so it should not sit quietly among the running ones, and it is not an outcome, so it
 * does not wear declined's red (UX5 U1). It shares its hue with `queued`; the word tells them apart.
 */
export const SESSION_TONE: Record<ShownState, keyof typeof PILL_TONE> = {
  'queued': 'open',
  'starting': 'taken',
  'working': 'taken',
  'awaiting-person': 'open',
  'completed': 'done',
  'declined': 'declined',
  'stood-down': 'neutral',
  'failed': 'declined',
  'stopped': 'neutral',
  'idle': 'neutral',
};

/**
 * What the page shows a session as: the nine states the record carries, and **idle** — a live chat
 * whose turn has ended, waiting for the person's next message (UX5 U17). The record says `working`
 * for a chat's whole life, since its process is; whether a turn is in flight is the driver's to say.
 */
export type ShownState = SessionState | 'idle';

/**
 * A session as the page shows it (UX5 U17, decided by the reference console, which draws a running
 * turn as live and a session between turns with no live mark at all). A live chat is idle when the
 * driver says no turn is in flight, and working while one is; a turn the driver has not answered for
 * is the record's own word, never a guess. Driven work is one long turn, so it is never idle.
 */
export function shownState(
  session: { kind?: 'driven' | 'chat'; state: SessionState }, taking: boolean | undefined,
): ShownState {
  return session.kind === 'chat' && session.state === 'working' && taking === false ? 'idle' : session.state;
}

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
 * is what the mark reads — with one exception the driver answers for: a live chat between turns is
 * `idle`, quiet rather than live (`shownState`, UX5 U17).
 */
export const SESSION_DOT: Record<ShownState, keyof typeof DOT_TONE> = {
  'queued': 'idle',
  'starting': 'live',
  'working': 'live',
  'awaiting-person': 'parked',
  'completed': 'ended',
  'declined': 'ended',
  'stood-down': 'ended',
  'failed': 'ended',
  'stopped': 'ended',
  'idle': 'idle',
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
  // Waiting on a person is the status palette's waiting hue, the one `WaitingCard` and the map wear.
  // It wore declined's red, and a session waiting on its person read as one that had failed (UX5 U1).
  parked: { mark: 'bg-st-open', word: 'text-st-open' },
  // A failure is an outcome, and red is its hue: a tool call that failed, never a session's liveness.
  failed: { mark: 'bg-st-declined', word: 'text-st-declined' },
  // Ended is completed, failed, declined and stopped at once, so it wears no outcome's hue: done's
  // green put a success mark beside a failed session in the rail. The pill beside it names the outcome.
  ended: { mark: 'bg-ink-faint', word: 'text-ink-soft' },
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
      <DotMark tone={tone} />
      {label}
    </span>
  );
}

/**
 * The mark alone, for a surface too narrow for the word — the rail's strip (FRAME6). The word goes
 * where the mark's control names itself, so it is never hue alone (D41 §6); anywhere with room for the
 * word, `Dot` is the component.
 */
export function DotMark({ tone = 'idle', className }: { tone?: keyof typeof DOT_TONE; className?: string }) {
  return <span aria-hidden className={cn('inline-block size-1.5 shrink-0 rounded-full', DOT_TONE[tone].mark, className)} />;
}

/**
 * One item on a list closed to its strip (FRAME6's session strip row, made general by D118 §5): the first
 * character of its name and its mark, one press away. What it is and how it stands are its accessible
 * name and its tip, since the strip has no room for the words and a mark is never hue alone (D41 §6).
 *
 * @remarks
 * A row of its list (`data-list-row`), so the list's arrows move along the strip too (`work/listKeys`).
 * The first character is taken whole, by code point, so a 中文 name shows its first character rather than
 * half of one.
 */
export function StripMark({ label, initialOf, tone, dimmed = false, current = false, onPress }: {
  /** Its accessible name and its tip: what it is and how it stands. */
  label: string;
  /** The name whose first character the strip shows. */
  initialOf: string;
  /** Its mark; absent, it wears none, as a plugin that is simply on does (D119). */
  tone?: keyof typeof DOT_TONE;
  /** Its initial drawn faint: a plugin switched off (D119). */
  dimmed?: boolean;
  /** The one chosen in the list, marked as the activity bar marks its current place. */
  current?: boolean;
  onPress?: () => void;
}) {
  return (
    <li data-list-row="">
      <Tip content={label} side="right">
        <button
          type="button"
          aria-label={label}
          aria-current={current || undefined}
          onClick={onPress}
          className={cn(
            'relative flex h-8 w-10 items-center justify-center rounded-control transition-colors duration-(--speed)',
            current ? 'bg-accent-soft text-ink' : 'text-ink-soft hover:bg-accent-soft/50',
          )}
        >
          {/* The same 2px accent rail the activity bar gives its current place. */}
          {current && <span aria-hidden className="absolute inset-y-1 left-0 w-0.5 rounded-full bg-accent" />}
          <span aria-hidden className={cn('text-small font-semibold uppercase', dimmed && 'text-ink-faint')}>
            {Array.from(initialOf)[0] ?? '?'}
          </span>
          {tone && <DotMark tone={tone} className="absolute right-1 top-1" />}
        </button>
      </Tip>
    </li>
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
          <dd className={cn('m-0 min-w-0 text-ink-soft', item.mono && 'font-mono text-small')}>
            {item.mono && typeof item.value === 'string' ? <PathText path={item.value} /> : item.value}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/**
 * A path or a URL that may have to wrap: it breaks after a separator, and inside a name only when
 * that one name is wider than the line. `break-all` broke anywhere, so a session's tree read
 * `family\g` over `ame` (UX5 U8). The text is unchanged: a `<wbr>` adds a place to break and no
 * character, so a copy is still the path.
 */
export function PathText({ path, className }: { path: string; className?: string }) {
  const parts = path.split(/(?<=[\\/])/);
  return (
    <span className={cn('font-mono wrap-anywhere', className)}>
      {parts.map((part, index) => (
        <Fragment key={index}>{part}{index < parts.length - 1 && <wbr />}</Fragment>
      ))}
    </span>
  );
}

/* ---------------------------------------------------------------- cards, tiles, headers */

export function Card({ id, warn, accent, className, children }: {
  /** What a link or a menu item brings into view (UX5 U72). */
  id?: string;
  warn?: boolean; accent?: boolean; className?: string; children: ReactNode;
}) {
  return (
    <article id={id} className={cn(
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

/**
 * Something waiting on the person — a parked session, a parked intake, a folder to trust. The one
 * frame each such card wears: the waiting tone on its left edge and on its heading. Four cards
 * wrote it out (REV3 CLEAN1), so a change of that tone is now one change.
 */
export function WaitingCard({ title, children }: { title: ReactNode; children?: ReactNode }) {
  return (
    <section className="rounded-card border border-line border-l-[3px] border-l-st-open bg-raised px-[1.15rem] py-3.5">
      <h3 className="m-0 text-small font-semibold text-st-open">{title}</h3>
      {children}
    </section>
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
 * **The column follows the window and prose does not.** Cards, tiles and tables take the column's
 * whole width (UX5 U59: it was capped at 72rem, and a maximized window left every view a third
 * empty) — but a paragraph inheriting it ran to about **190 characters a line** even under
 * the old cap, roughly triple the 45–75 the eye tracks without losing its place. Measured in the
 * real window before this existed: every sentence on the Machine view was one of those lines.
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
 * A sentence whose backticked spans are code: the one markup the catalogues and the service share.
 *
 * @remarks
 * **Nothing rendered it before this, and the installed window said so.** Fourteen catalogue strings
 * and a good many of the service's and the driver's sentences mark a command or a name the way a
 * commit message does, and every one reached the screen with its backticks, in both languages.
 *
 * **The words are unchanged**, so a service's sentence is still verbatim: only the pairs are read,
 * a lone backtick stays the character it is, and nothing else is parsed. A setting's hint, a tip and
 * a toast take this on their own, because each is always a sentence. Anywhere else a caller asks for
 * it, since content is shown as it is.
 */
export function Inline({ text }: { text: string }) {
  const parts = text.split(/`([^`\n]+)`/);
  return (
    <>
      {parts.map((part, i) => (i % 2 === 1
        // Mono at the sentence's own size and colour, so a command reads as one without shouting.
        ? <code key={i} className="font-mono">{part}</code>
        : part))}
    </>
  );
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
  title: string; meta?: ReactNode; onClose: () => void; footer?: ReactNode;
  children: ReactNode;
}) {
  const { t } = useTranslation();
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
          // 🔴 `top-9`, like the scrim beside it — the strip is the window's title bar (D56), and
          // the caption buttons the window paints there are painted OVER the page. A panel that
          // started at the top put its own header, close button included, under them: on the
          // deployed application the drawer's × sat exactly beneath the window's ✕, neither
          // dimmed nor reachable. Held by `tokens.test.ts` for every panel, as the scrim is.
          className={cn(
            'fixed bottom-6 right-0 top-9 z-10 flex flex-col border-l border-line bg-overlay focus:outline-none motion-safe:animate-[drawer-in_var(--speed)_ease-out]',
            // Never over the activity bar (3rem): the frame is three bars, and an overlay sits between
            // them (POLISH3). A wide one read a source file until FRAME1f gave an entry the main area (U43).
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
 * A box at the palette's place (SURF9, DOCK1d): the command palette, and Quick Ask, which is Ask Daoris's conversation
 * in the same place. Near the top, not centred, since what it holds grows downward and a centred box jumps as it does;
 * modal on Radix Dialog, as the drawer is, with the title bar and the status bar left live around it (`tokens.test.ts`'s
 * scrim bounds), and Escape or a press outside closing it.
 *
 * @remarks
 * Two boxes wrote this out on their own beside the drawer, so the atoms did not own the dialog primitive (MENU1). The
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

/* ---------------------------------------------------------------- form controls */

export function SelectField({ value, onChange, options, placeholder, ariaLabel, required, disabled, bar }: {
  value: string;
  onChange: (value: string) => void;
  options: { value: string; label: string }[];
  placeholder?: string;
  ariaLabel?: string;
  required?: boolean;
  /** Nothing can be chosen while it is set: a change already on its way (AGT6b). */
  disabled?: boolean;
  /**
   * 🔴 The status bar's shape rather than a form's. It was already there and looked wrong: a 30px bordered control
   * parked in a 24px bar, which reads as a form that fell out of a dialog. A bar item is
   * borderless, full-height, and lights on hover like every other item beside it — the chooser is
   * the same chooser, and only its trigger belongs to the bar.
   */
  bar?: boolean;
}) {
  return (
    <RadixSelect.Root value={value || undefined} onValueChange={onChange} required={required} disabled={disabled}>
      <RadixSelect.Trigger
        aria-label={ariaLabel}
        className={bar
          ? 'flex h-full items-center gap-1.5 px-2 text-meta text-ink transition-colors duration-[var(--speed)] hover:bg-accent-soft focus-visible:bg-accent-soft focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-accent'
          : 'inline-flex min-h-[1.9rem] min-w-0 max-w-full items-center justify-between gap-2 rounded-control border border-line-strong bg-raised px-2.5 py-1.5 text-body text-ink data-[placeholder]:text-ink-faint data-[disabled]:opacity-60'}
      >
        {bar && <Layers size={12} aria-hidden className="shrink-0 text-ink-soft" />}
        <span className="min-w-0 truncate"><RadixSelect.Value placeholder={placeholder} /></span>
        <ChevronDown size={bar ? 12 : 14} aria-hidden className="text-ink-faint" />
      </RadixSelect.Trigger>
      <RadixSelect.Portal>
        {/* SELECT1: a long list is capped at the room the popper measured on its side, and scrolls inside it.
            The viewport hides its own scrollbar, so the arrows at each end are what says there is more. */}
        <RadixSelect.Content
          position="popper" sideOffset={4} collisionPadding={8}
          className="z-30 max-h-[var(--radix-select-content-available-height)] min-w-[var(--radix-select-trigger-width)] overflow-hidden rounded-card border border-line bg-overlay shadow-[0_6px_24px_rgb(15_12_8/0.12)]"
        >
          <RadixSelect.ScrollUpButton className="flex h-5 shrink-0 cursor-default items-center justify-center text-ink-soft">
            <ChevronUp size={14} aria-hidden />
          </RadixSelect.ScrollUpButton>
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
          <RadixSelect.ScrollDownButton className="flex h-5 shrink-0 cursor-default items-center justify-center text-ink-soft">
            <ChevronDown size={14} aria-hidden />
          </RadixSelect.ScrollDownButton>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  );
}

/**
 * A checkbox with its sentence. `hideLabel` keeps the sentence for the accessibility tree and takes
 * it off the screen — for a setting row, where the label already stands at the left and a second
 * copy beside the box would say everything twice.
 */
export function CheckField({ checked, onChange, label, hideLabel, disabled, className }: {
  checked: boolean; onChange: (checked: boolean) => void; label: string; hideLabel?: boolean; disabled?: boolean;
  /** The label's size and ink where the box sits in a quieter row — a review file's *viewed*. */
  className?: string;
}) {
  return (
    <label className={cn('flex cursor-pointer items-center gap-2 whitespace-nowrap text-body text-ink-soft', className)}>
      <Checkbox.Root
        checked={checked}
        disabled={disabled}
        onCheckedChange={(state) => onChange(state === true)}
        className="grid size-4 shrink-0 place-items-center rounded-[4px] border border-line-strong bg-raised data-[state=checked]:border-accent data-[state=checked]:bg-accent data-[state=checked]:text-accent-ink"
      >
        <Checkbox.Indicator><Check size={12} strokeWidth={2.2} aria-hidden /></Checkbox.Indicator>
      </Checkbox.Root>
      <span className={cn(hideLabel && 'sr-only')}>{label}</span>
    </label>
  );
}

/**
 * A choice of a few, all in view (D66: the theme, the language) — a radiogroup of buttons, the arrow
 * keys moving the choice the way every radiogroup's do. For two to four options that fit a line: a
 * select would hide what a person is choosing between.
 */
export function Segmented<T extends string>({ label, value, options, onChange }: {
  /** The group's accessible name — the row's label, since the options alone say nothing of what. */
  label: string;
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
}) {
  const move = (by: number) => {
    const at = options.findIndex((option) => option.value === value);
    onChange(options[(at + by + options.length) % options.length]!.value);
  };

  return (
    <div
      role="radiogroup"
      aria-label={label}
      onKeyDown={(event) => {
        if (event.key === 'ArrowRight' || event.key === 'ArrowDown') move(1);
        else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') move(-1);
        else return;
        event.preventDefault();
      }}
      className="inline-flex rounded-control border border-line-strong bg-raised p-0.5"
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          role="radio"
          aria-checked={value === option.value}
          // Roving focus: the group is one tab stop, and the chosen option is where it lands.
          tabIndex={value === option.value ? 0 : -1}
          onClick={() => onChange(option.value)}
          className={cn(
            'inline-flex items-center rounded-[4px] px-2.5 py-1 text-small transition-colors duration-(--speed)',
            value === option.value ? 'bg-accent text-accent-ink' : 'text-ink-soft hover:text-ink',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

/* ---------------------------------------------------------------- menus */

/**
 * What a menu's rows light with under the pointer and the keys (MENU1). Two looks were already in the window when the
 * menus became one atom, and the atom kept both rather than change what a person sees: the title bar's menus and most
 * others light a row with the raised surface and lift its ink from soft, as a menu bar's do; a list's ⋯ and `＋` and a
 * session row's menu, which open from among a list's rows, light it with the accent's soft field, as those rows do.
 */
type MenuHighlight = 'raised' | 'accent';

const MenuHighlightContext = createContext<MenuHighlight>('raised');

/** A row's shape: one density for every menu, the highlight by the menu's look, a disabled row a fact rather than an act. */
const MENU_ROW = 'flex items-center gap-2 rounded-control px-2 py-1.5 text-small outline-none data-[disabled]:cursor-default data-[disabled]:text-ink-faint';
const MENU_LOOK: Record<MenuHighlight, string> = {
  raised: 'cursor-pointer text-ink-soft data-[highlighted]:bg-raised data-[highlighted]:text-ink',
  accent: 'cursor-default text-ink data-[highlighted]:bg-accent-soft',
};

/** A row's classes, by its menu's look: what a ticked row adds is the full ink, which an accent row already wears. */
function useMenuRow(className: string | undefined, ...extra: (string | false | undefined)[]): string {
  return cn(MENU_ROW, MENU_LOOK[useContext(MenuHighlightContext)], ...extra, className);
}

/**
 * The tick's column, always the same width, so the labels line up whether or not anything is ticked: a list that shifts
 * by 16px when the tick moves reads as two lists.
 */
function MenuTickColumn({ children }: { children?: ReactNode }) {
  return <span className="flex w-3.5 shrink-0 justify-center">{children}</span>;
}

/**
 * 🔴 `modal={false}`, for every menu. A modal menu makes the rest of the page inert (Radix puts `pointer-events: none`
 * on the body while it is open), and a menu in the title bar has no business doing that: VS Code's do not, and the
 * window must stay draggable beneath it. It also locks the scroll, which shifts the layout by the scrollbar's width on
 * every open. Found by two tests that could not click anything after an earlier test left a menu open (AppMenu's rule,
 * which every menu had written out on its own).
 */
function MenuRoot(props: ComponentProps<typeof DropdownMenu.Root>) {
  return <DropdownMenu.Root modal={false} {...props} />;
}

/**
 * A menu's content, in its portal: the overlay surface, the control's radius and the menus' shadow, 4px off its
 * trigger and 8px off the window's edges. Its width is the caller's (`className`), since a menu bar's and a status
 * item's menus are sized to what they hold.
 *
 * @remarks
 * **Capped at the room the popper measured on its side, and scrolled inside it** (MENU1, as SELECT1 for the select):
 * a long menu (a receiver filter, a long workspace list) ran off the window with nothing to scroll it by. The bar is
 * the theme's (`tokens.css`), and the keys still reach every row, since focusing one scrolls it into view.
 */
function MenuContent({ highlight = 'raised', className, children, ...props }: ComponentProps<typeof DropdownMenu.Content> & {
  /** What its rows light with: the raised surface by default, the accent's field for a list's own menus. */
  highlight?: MenuHighlight;
}) {
  return (
    <DropdownMenu.Portal>
      <DropdownMenu.Content
        sideOffset={4}
        collisionPadding={8}
        {...props}
        className={cn(
          'z-30 max-h-[var(--radix-dropdown-menu-content-available-height)] overflow-y-auto overflow-x-hidden',
          'rounded-control border border-line bg-overlay p-1 text-small shadow-lg',
          className,
        )}
      >
        <MenuHighlightContext.Provider value={highlight}>{children}</MenuHighlightContext.Provider>
      </DropdownMenu.Content>
    </DropdownMenu.Portal>
  );
}

/**
 * An act. `tick` says the tick column: absent, the row has none; false, it is reserved and empty; true, the row is the
 * current one, ticked and in the full ink, as the size a map is at or the workspace a window is scoped to. It stays a
 * `menuitem`: choosing it acts, where a checkbox item toggles.
 */
function MenuRow({ tick, className, children, ...props }: ComponentProps<typeof DropdownMenu.Item> & { tick?: boolean }) {
  const row = useMenuRow(className, tick && 'text-ink');
  return (
    <DropdownMenu.Item {...props} className={row}>
      {tick !== undefined && <MenuTickColumn>{tick && <Icon name="check" size={12} />}</MenuTickColumn>}
      {children}
    </DropdownMenu.Item>
  );
}

/** A toggle, ticked while it is on (a filter's *Include closed*, a kind of line drawn). */
function MenuCheckboxRow({ className, children, ...props }: ComponentProps<typeof DropdownMenu.CheckboxItem>) {
  const row = useMenuRow(className, 'data-[state=checked]:text-ink');
  return (
    <DropdownMenu.CheckboxItem {...props} className={row}>
      <MenuTickColumn><DropdownMenu.ItemIndicator><Icon name="check" size={12} /></DropdownMenu.ItemIndicator></MenuTickColumn>
      {children}
    </DropdownMenu.CheckboxItem>
  );
}

/** One value among several, in a `Menu.RadioGroup` named for what it chooses, the chosen one ticked. */
function MenuRadioRow({ className, children, ...props }: ComponentProps<typeof DropdownMenu.RadioItem>) {
  const row = useMenuRow(className, 'data-[state=checked]:text-ink');
  return (
    <DropdownMenu.RadioItem {...props} className={row}>
      <MenuTickColumn><DropdownMenu.ItemIndicator><Icon name="check" size={12} /></DropdownMenu.ItemIndicator></MenuTickColumn>
      {children}
    </DropdownMenu.RadioItem>
  );
}

/** A group's name, small and faint; its padding above is the caller's where it opens the menu or follows a rule. */
function MenuLabel({ className, ...props }: ComponentProps<typeof DropdownMenu.Label>) {
  return <DropdownMenu.Label {...props} className={cn('px-2 pb-1 pt-1 text-meta text-ink-faint', className)} />;
}

/** The rule between a menu's groups. */
function MenuSeparator({ className, ...props }: ComponentProps<typeof DropdownMenu.Separator>) {
  return <DropdownMenu.Separator {...props} className={cn('my-1 h-px bg-line', className)} />;
}

/**
 * **The menu** (MENU1): every dropdown in the window, on Radix's dropdown menu, as `SelectField` is every select. Its
 * parts are Radix's names, so a menu reads as one did before it had an atom, and every pixel is here: the surface,
 * the cap, the row's density and look, the tick's column, a label and a rule.
 *
 * @remarks
 * Eight files had each styled the primitive on their own, and none capped its height, so a long menu ran off the
 * window. `primitives.test.ts` now holds that no file but the atoms imports a primitive. No sub-menu is here, since no
 * menu has one; one is added here the day a menu needs it.
 */
export const Menu = {
  Root: MenuRoot,
  Trigger: DropdownMenu.Trigger,
  Content: MenuContent,
  Item: MenuRow,
  CheckboxItem: MenuCheckboxRow,
  RadioGroup: DropdownMenu.RadioGroup,
  RadioItem: MenuRadioRow,
  Label: MenuLabel,
  Separator: MenuSeparator,
};

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
export function CountBadge({ count, tone = 'accent' }: {
  count: number;
  /** A status hue for a count that IS a status (sessions waiting on a person); the accent otherwise. */
  tone?: 'accent' | 'open';
}) {
  if (count <= 0) return null;
  return (
    <span
      aria-hidden
      className={cn(
        // `px-0.5`, not `px-1`: measured on the window, a single digit plus 4px a side plus the border
        // came to 16.45px — wider than the 16px height by the padding alone. At 2px a side one digit
        // sits inside the circle, and two grow it into a pill as they should.
        'absolute right-0 top-0 flex h-4 min-w-4 items-center justify-center rounded-full border bg-page px-0.5',
        'font-mono text-meta leading-none tabular-nums',
        tone === 'open' ? 'border-st-open text-st-open' : 'border-accent text-accent',
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
 * right 60% of every card empty because the prose measure is 65ch and the column was 72rem. A setting
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

export function SettingRow({ label, hint, why, control, children }: {
  label: ReactNode; hint?: ReactNode; why?: string; control?: ReactNode; children?: ReactNode;
}) {
  return (
    // The row is its own container (WSR4): below 26rem the control goes under the label at the row's
    // full width. Beside a 155px label floor a 309px row left the control 130px, and a branch pattern
    // showed eleven characters.
    <div className="@container border-t border-line py-2.5 first:border-t-0 first:pt-0 last:pb-0">
      {/* 🔴 The label's column has a floor, 16rem or half the row, and the control's gives way to it.
          With `minmax(0,1fr)` a path as the control took its whole width first, and at 888 the label
          beside it was 24px wide, one character a line (UX5 U58). A path breaks at its separators. */}
      <div className="grid grid-cols-[minmax(min(16rem,50%),1fr)_auto] items-center gap-x-6 gap-y-1.5 @max-[26rem]:grid-cols-1">
        <div className="min-w-0">
          <div className="flex items-center gap-1.5 text-body font-medium text-ink">
            <span>{label}</span>
            {why && <WhyGlyph why={why} />}
          </div>
          {hint && (
            <div className="mt-0.5 max-w-prose text-small text-ink-faint">
              {typeof hint === 'string' ? <Inline text={hint} /> : hint}
            </div>
          )}
        </div>
        {control && <div className="flex min-w-0 shrink-0 items-center justify-end gap-2 @max-[26rem]:justify-start">{control}</div>}
        {children && <div className="col-span-2 min-w-0 @max-[26rem]:col-span-1">{children}</div>}
      </div>
    </div>
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
    // came to swallow clicks on its own options list once it moved into the app strip (D56).
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
          // workspace scope moved into the app strip (D56) and its tooltip landed on top of its own
          // options list. The rule belongs here rather than at that one call site.
          className="pointer-events-none z-30 max-w-[22rem] rounded-card border border-line bg-overlay px-3 py-2 text-small text-ink-soft shadow-[0_6px_24px_rgb(15_12_8/0.12)]"
        >
          <Inline text={content} />
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
 * A window's notices: the list its corner shows, and the two ways to change it. Capped by
 * `withNotice`, so a burst cannot climb the window — the newest are what a corner can promise to
 * show. Both windows hold one; each had written it out (REV3 CLEAN1).
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
          {/* 🔴 Clamped, with the whole of it one hover away. Seen on the deployed application
              (D62): the driver's hold on an untrusted repository is six lines naming a path, a
              consequence and the command that fixes it — every word of it worth keeping, and as a
              corner toast it covered the card it was about. The first two lines carry the verdict
              and the subject, which is what a glance is for; the rest is there when the glance was
              not enough. Never truncated in the DOM, so a screen reader still hears all of it. */}
          <Tip content={toast.text}>
            <Toast.Description className="line-clamp-2 flex-1"><Inline text={toast.text} /></Toast.Description>
          </Tip>
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
