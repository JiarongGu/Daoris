import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dot, Icon, type IconName, Tip } from '../ui';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS } from './caption';
import { SessionConsole } from '../SessionConsole';
import { cn } from '../lib/cn';
import { Mark } from '../Mark';

// The frame's own furniture (D55, extended by D56): the app strip, the activity bar, the mode
// switch, the status bar and the output panel. Small, presentational, and kept together because
// they are one arrangement rather than five features — what the workbench references all have and
// what this platform had nowhere to put.

export type Mode = 'manage' | 'work';

/**
 * The **app strip** (D56): the application's one global row, across the top of the window.
 *
 * @remarks
 * It holds what is true in **both** frames — the wordmark, the mode switch, the workspace scope —
 * which is precisely why none of them belongs in a sidebar owned by one frame. Measured before it
 * was built: 42% of the window's width was navigation and a form, and the session that is the
 * application's organising object (D55) got about 28%.
 *
 * **`captionRoom` reserves the right edge, and the WINDOW paints there** (SURF7; D56 as amended has
 * the reason). The three slots are empty by design — the window cuts their rectangles out of the
 * WebView2 and draws the buttons itself.
 *
 * **It is presentational, and the window wiring arrives as props** — `useWindowChrome` holds the
 * bridge, so every state of this strip is still reachable by passing props, including in a browser
 * where there is no window to command and none of them are passed at all.
 */
export function AppStrip({
  mode, modeAvailable, attention = 0, onMode, scope, center, menus, captionRoom,
  stripRef, onDragStart, onToggleMaximize, onResizeTop,
}: {
  mode: Mode;
  modeAvailable: boolean;
  attention?: number;
  onMode: (mode: Mode) => void;
  /** The workspace switcher, or nothing while the deployment holds one circle (WSP5). */
  scope?: ReactNode;
  /** The command center — where you are, and the way into everything (taken from VS Code's shape). */
  center?: ReactNode;
  /** The frame menus, when the caller has a roster of sub-views to offer. */
  menus?: ReactNode;
  captionRoom?: boolean;
  stripRef?: (element: HTMLElement | null) => void;
  /** Absent in a browser: there is no window to move, so the strip is simply a strip. */
  onDragStart?: () => void;
  onToggleMaximize?: () => void;
  onResizeTop?: () => void;
}) {
  return (
    <header
      ref={stripRef}
      // The title bar's own gesture. `onPointerDown` rather than a click: the OS move loop has to
      // start while the button is still down, which is also why the host dispatches it inline.
      onPointerDown={(event) => {
        // Only the strip itself drags. A press that began on the mode switch or the scope is that
        // control's, and handing it to the OS would make every button a drag handle.
        if (event.button !== 0 || event.target !== event.currentTarget) return;
        onDragStart?.();
      }}
      onDoubleClick={(event) => {
        if (event.target !== event.currentTarget) return;
        onToggleMaximize?.();
      }}
      className="relative flex h-9 shrink-0 items-center gap-3 border-b border-line bg-raised pl-3"
    >
      {/* The frameless technique hands the top edge to the client, so the top resize border is
          re-added by a sliver that asks the OS for a size loop. Above the strip's own contents in
          z-order, and 4px tall — enough to hit, small enough not to steal the wordmark's clicks. */}
      {onResizeTop && (
        <div
          aria-hidden
          onPointerDown={(event) => { if (event.button === 0) onResizeTop(); }}
          className="absolute inset-x-0 top-0 z-10 h-1 cursor-ns-resize"
        />
      )}

      {/* 🔴 The MARK alone (owner, 2026-09-22). The name was 20px of serif in every window forever,
          and a title bar is where an IDE puts what you can DO — the application's name belongs in its
          About, which the menu beside this now carries. D41 §1 gave the serif one appearance; this is
          it not being spent on saying the name of the thing you are already looking at. */}
      <div className="pointer-events-none flex items-center pl-0.5">
        <Mark size={18} className="text-accent" />
      </div>

      {/* The frames, as menus of what is inside them (VS Code's menu bar). `menus` replaces the
          two-button toggle; the toggle survives as the fallback for a caller that has no roster to
          offer, which is what a story or a narrow test renders. */}
      {menus ?? <ModeSwitch mode={mode} available={modeAvailable} attention={attention} onChange={onMode} />}

      {/* The middle, which used to be ~1,400px of nothing at any real window width. Absolutely
          positioned so it centres on the STRIP rather than on whatever is left over after the
          wordmark and the mode switch — a flex-centred child drifts as those change width, and the
          mode switch changes width with its attention badge. */}
      {center}

      <div className="ml-auto flex items-center gap-2">{scope}</div>

      {/* Reserved, never drawn: the window owns these pixels. Three slots of 44px — the width the
          strip has always held open, so nothing shifted when they became real. */}
      {captionRoom && (
        <div aria-hidden className="flex h-full shrink-0">
          {CAPTION_SLOTS.map((kind) => (
            <div key={kind} {...{ [CAPTION_ATTRIBUTE]: kind }} className="h-full w-11" />
          ))}
        </div>
      )}
    </header>
  );
}

/**
 * The **activity bar** (D56): the management domains as icons, down the window's left edge.
 *
 * @remarks
 * **Identical in both frames**, which is what makes Manage and Work peers rather than one nesting
 * inside the other. In Work `active` is `null` — nothing here is current, because the current thing
 * is the other frame — and selecting a domain is a door back into Manage on it.
 *
 * It replaces a 15rem labelled sidebar that, in Work, was six items belonging to the other frame
 * above ~440px of empty column. The cost is the labels, paid by the tooltip here, the view's own
 * page header, and eventually SURF9's palette.
 *
 * **Absent, never disabled** (the rule the Machine tab already followed): a browser is handed a
 * shorter `items`, because a greyed row implies the thing exists somewhere you could get to.
 */
export function ActivityBar<T extends string>({
  label, items, active, onSelect, footer, frames, onFrame,
}: {
  /** The bar's accessible name — passed in, so this stays a molecule with no i18n of its own. */
  label: string;
  items: { tab: T; label: string; icon: IconName; badge?: number }[];
  /** The current domain, or null in a frame where no domain is current. */
  active: T | null;
  onSelect: (tab: T) => void;
  /** Actions, not state: refresh and language. State went to the status bar. */
  footer?: ReactNode;
  /**
   * The FRAMES, at the top of the rail (owner, 2026-09-22: *"those mode/tab switches can be at left
   * menu"*).
   *
   * 🔴 They were a segmented toggle in the title bar, then briefly menus there — and both were the
   * wrong home. A title bar in an IDE holds the **application's** menus (settings, configuration,
   * help); switching what you are looking at belongs on the rail, which is exactly what an activity
   * bar is for. Separated from the domains below, because a frame and a view inside one are not
   * peers.
   */
  frames?: { id: string; label: string; icon: IconName; badge?: number; active: boolean }[];
  /**
   * Choosing a frame. Its own callback rather than `onSelect`, because a frame id is not a domain
   * id — routing both through one handler needed a cast, and a cast here would be the component
   * telling the compiler something untrue about its own contract.
   */
  onFrame?: (id: string) => void;
}) {
  return (
    <nav
      aria-label={label}
      className="flex w-12 shrink-0 flex-col items-center gap-0.5 border-r border-line py-1.5"
    >
      {frames && frames.length > 0 && (
        <>
          {frames.map((frame) => (
            <Tip key={frame.id} content={frame.label} side="right">
              <button
                type="button"
                aria-label={frame.label}
                aria-pressed={frame.active}
                onClick={() => onFrame?.(frame.id)}
                className={cn(
                  'relative flex h-9 w-9 items-center justify-center rounded-control transition-colors duration-(--speed)',
                  frame.active
                    ? 'bg-accent text-accent-ink'
                    : 'text-ink-faint hover:bg-raised hover:text-ink',
                )}
              >
                <Icon name={frame.icon} size={17} />
                {frame.badge !== undefined && frame.badge > 0 && (
                  <span
                    aria-hidden
                    className="absolute right-0.5 top-0.5 min-w-3.5 rounded-full border border-st-open bg-page px-0.5 text-center font-mono text-meta leading-[0.95rem] tabular-nums text-st-open"
                  >
                    {frame.badge}
                  </span>
                )}
              </button>
            </Tip>
          ))}
          {/* A frame and a view inside one are not peers, and a rail with no rule between them
              reads as one list of eight equal things. */}
          <div aria-hidden className="my-1.5 h-px w-5 shrink-0 bg-line" />
        </>
      )}

      {items.map(({ tab, label: name, icon, badge }) => (
        <Tip key={tab} content={name} side="right">
          <button
            type="button"
            aria-label={name}
            aria-current={active === tab ? 'page' : undefined}
            onClick={() => onSelect(tab)}
            className={cn(
              'relative flex h-9 w-9 items-center justify-center rounded-control transition-colors duration-(--speed)',
              active === tab
                ? 'bg-accent-soft text-accent'
                : 'text-ink-faint hover:bg-raised hover:text-ink',
            )}
          >
            {/* The 2px accent rail D41 §2 gives the active nav item, rotated onto a narrow bar. */}
            {active === tab && (
              <span aria-hidden className="absolute inset-y-1 left-0 w-0.5 rounded-full bg-accent" />
            )}
            <Icon name={icon} size={17} />
            {badge !== undefined && badge > 0 && (
              <span
                aria-hidden
                className="absolute right-0.5 top-0.5 min-w-3.5 rounded-full border border-accent bg-page px-0.5 text-center font-mono text-meta leading-[0.95rem] tabular-nums text-accent"
              >
                {badge}
              </span>
            )}
          </button>
        </Tip>
      ))}

      {footer && <div className="mt-auto flex flex-col items-center gap-0.5">{footer}</div>}
    </nav>
  );
}

/**
 * *Manage* ⇄ *Work*, as **peers** (D55) — not a sixth nav item.
 *
 * @remarks
 * **Absent where Work is** (D47 §4 / D55): over a keyed remote the Work frame is not rendered at
 * all, so a switch offering it would be a door onto nothing. `available` is the shell's answer,
 * and `false` renders nothing rather than a disabled control — the same rule the Machine tab
 * follows, and for the same reason: a disabled control implies the thing exists elsewhere.
 *
 * **It carries the second of the sidebar's two counts** (design §4): how many things need a
 * person. It is the **only** badge that wears a status hue, because it is the only one that is a
 * status — the session count beside it in the status bar is a quantity. Design §4 asked for both
 * counts in the sidebar; after D55 the quantity's home is the status bar and the status's home is
 * the door into the thing, which is here.
 */
export function ModeSwitch({ mode, available, attention = 0, onChange }: {
  mode: Mode;
  available: boolean;
  /** How many things are waiting on a person. Zero wears nothing: a zero badge is furniture. */
  attention?: number;
  onChange: (mode: Mode) => void;
}) {
  const { t } = useTranslation();
  if (!available) return null;

  return (
    <div
      role="group"
      aria-label={t('work.mode.label')}
      className="inline-flex rounded-control border border-line-strong bg-raised p-0.5"
    >
      {(['manage', 'work'] as const).map((target) => (
        <button
          key={target}
          type="button"
          aria-pressed={mode === target}
          onClick={() => onChange(target)}
          className={cn(
            'inline-flex items-center rounded-[4px] px-2.5 py-1 text-small transition-colors duration-(--speed)',
            mode === target ? 'bg-accent text-accent-ink' : 'text-ink-soft hover:text-ink',
          )}
        >
          {t(`work.mode.${target}`)}
          {target === 'work' && attention > 0 && (
            <span
              title={t('work.mode.needsYou', { count: attention })}
              className="ml-1.5 rounded-full border border-st-open bg-st-open/15 px-1.5 font-mono text-meta tabular-nums text-st-open"
            >
              {attention}
            </span>
          )}
        </button>
      ))}
    </div>
  );
}

/** What the driver is, as a surface can honestly know it. */
export type DriverPresence = 'running' | 'stopped' | 'absent';

/**
 * Ambient truth: the driver, how much is running, which circle, and whether this one syncs.
 *
 * @remarks
 * **It must be true without being looked at** — VS Code's status bar, and the reason D55 added
 * one: none of these four had anywhere to live, so each was either absent or buried in a view the
 * person was not on.
 *
 * `driver` is three states rather than a boolean because the third is real and different: `absent`
 * is a browser, which has no driver and never will, and `stopped` is a shell whose driver did not
 * answer. Telling a person "not answering" when the honest answer is "not here" sends them looking
 * for a fault.
 *
 * **It gained the recall tier and the index count when the sidebar was retired** (D56). Both are
 * ambient state and neither was ever anything else; the tier in particular must be *stated on every
 * screen* (D24), which a bar present on every screen by construction serves better than a sidebar
 * foot ever did. The tier's sentence stays the service's own, verbatim, in its tooltip.
 */
export function StatusBar({
  driver, sessions, workspace, remote, tier, indexed, scope,
  onDriver, onSessions, onRemote, onIndex,
}: {
  driver: DriverPresence;
  sessions: number;
  /** The chosen circle, or null for every circle this deployment holds (WSP5). */
  workspace: string | null;
  /** Whether this workspace has a deployment wired, or null where the question cannot be asked. */
  remote: boolean | null;
  /** What answered — D24's tier, with the service's own note. Absent until the service says. */
  tier?: { label: string; note: string; semantic: boolean };
  /** What the index holds, already worded by the caller. */
  indexed?: string;
  /** The scope as a control, where the deployment holds more than one circle (WSP5). */
  scope?: ReactNode;
  /** Where each fact leads. Absent means the fact is stated and not offered (see `StatusItem`). */
  onDriver?: () => void;
  onSessions?: () => void;
  onRemote?: () => void;
  onIndex?: () => void;
}) {
  const { t } = useTranslation();
  const tone = driver === 'running' ? 'live' : driver === 'stopped' ? 'parked' : 'idle';
  const driverWord = driver === 'running' ? 'Running' : driver === 'stopped' ? 'Stopped' : 'Absent';

  return (
    <footer
      aria-label={t('work.status.label')}
      /* 🔴 `items-stretch` and a fixed height, not `items-center` and padding — because a hover
         target that stops short of the bar's edges reads as a word that lit up rather than as a
         control. Every item fills the bar's full height; that single property is most of what makes
         the reference console's bar feel like a bar. */
      className="flex h-6 shrink-0 items-stretch overflow-hidden border-t border-line bg-raised text-meta text-ink-soft"
    >
      <StatusItem
        onPress={driver === 'absent' ? undefined : onDriver}
        tip={t('work.status.driverTip')}
        label={t('work.status.driver')}
      >
        {/* 🔴 The NOUN stays, dimmed, in front of the value. Photographing the bar without it
            showed `● ready` — ready is not a thing, and the subject was one hover away. The
            reference console keeps its nouns for the same reason (`Spaces: 2`, `UTF-8`), and the
            two weights are what stop a noun from competing with its own value.
            The mark and its word are one component and one tone — the label is not optional
            (D41 §6), so a second copy beside it would say the same thing twice in two colours. */}
        <span className="text-ink-faint">{t('work.status.driver')}</span>
        <Dot tone={tone} label={t(`work.status.driver${driverWord}`)} />
      </StatusItem>

      <StatusItem
        onPress={onSessions}
        tip={t('work.status.sessionsTip')}
        label={t('work.status.sessionsLabel')}
      >
        <Icon name="frameWork" size={12} />
        <span className="tabular-nums text-ink">{t('work.status.sessions', { count: sessions })}</span>
      </StatusItem>

      {/* 🔴 The scope, and it is a CONTROL here (owner: *"this workspace switch can also in a better
          location and design too"*). It had a dropdown in the app strip AND a read-only copy here,
          which is one fact in two places — and the strip is for what you can do while this bar is
          for what is true, which is exactly what a scope is. Clickable status items are also the
          reference console's own pattern: its remote indicator opens a menu from this bar.
          `scope` absent leaves it read-only, which is what a browser with one circle gets. */}
      {scope
        ? <span className="flex items-stretch">{scope}</span>
        : (
          <StatusItem tip={t('work.status.workspaceTip')} label={t('work.status.workspace')}>
            <Icon name="projects" size={12} />
            <span className="text-ink">{workspace ?? t('work.status.everyWorkspace')}</span>
          </StatusItem>
        )}

      {remote !== null && (
        <StatusItem
          onPress={onRemote}
          tip={t('work.status.remoteTip')}
          label={t('work.status.remote')}
          className="hidden sm:flex"
        >
          <Icon name={remote ? 'cloud' : 'cloudOff'} size={12} />
          <span className="text-ink">
            {remote ? t('work.status.remoteWired') : t('work.status.remoteLocal')}
          </span>
        </StatusItem>
      )}

      {/* Pushed right: what the INDEX is, rather than what the machine is doing. The reference
          console splits its bar the same way — the workspace on the left, what the editor is
          currently answering with on the right. */}
      <span className="ml-auto flex items-stretch">
        {indexed && (
          <StatusItem
            onPress={onIndex}
            tip={t('work.status.indexedTip')}
            label={t('work.status.indexedLabel')}
            className="hidden md:flex"
          >
            <Icon name="convergence" size={12} />
            <span className="tabular-nums">{indexed}</span>
          </StatusItem>
        )}
        {/* The tier's sentence stays the service's own, verbatim (D24) — so this one is a TIP and
            never a target: there is nothing to go to, only something to understand. */}
        {tier && (
          <StatusItem tip={tier.note} label={t('work.status.tierLabel')}>
            <span className={cn('font-mono', tier.semantic ? 'text-accent' : 'text-warn')}>
              {tier.label}
            </span>
          </StatusItem>
        )}
      </span>
    </footer>
  );
}

/**
 * One item in the status bar — a full-height box that lights on hover when it leads somewhere.
 *
 * @remarks
 * Written from the owner's *"the bottom styling still not really close to vscode which have better
 * design with display and action (on click or on hover)"* (2026-09-22), and the two halves need
 * different answers.
 *
 * 🔴 **A button when it leads somewhere, plain text when it does not.** A bar where everything looks
 * alike and half of it responds is worse than one where nothing does, because the half that does
 * nothing is the one a person presses first. So the element itself carries the difference —
 * `<button>` gets the hover wash, the pointer and the focus ring; a `<span>` gets none of them and
 * cannot be tabbed to.
 *
 * **Every item says what it is** through its `label`, whether or not it is pressable. The bar's
 * values are terse by design (`ready`, `3 sessions`, `wired`) and terse is only legible when the
 * noun is one hover away — which is also the only way the screen-reader name of a pressable item is
 * anything other than the value it happens to show right now.
 *
 * The wash is `accent-soft`, which is a real token in both themes rather than an opacity over the
 * bar: a white-alpha hover disappears on a light bar, and this bar is light half the time.
 */
function StatusItem({ children, label, tip, onPress, className }: {
  children: ReactNode;
  /** The noun, for the tooltip's first line and the accessible name. */
  label: string;
  /** What this fact means, or what pressing it does. */
  tip?: string;
  /** Absent leaves the item as text — see above; it is the whole distinction. */
  onPress?: () => void;
  className?: string;
}) {
  const inner = (
    <span className="flex h-full items-center gap-1.5 px-2">{children}</span>
  );

  const item = onPress
    ? (
      <button
        type="button"
        aria-label={label}
        onClick={onPress}
        className={cn(
          'flex items-stretch transition-colors duration-[var(--speed)]',
          'hover:bg-accent-soft hover:text-ink',
          'focus-visible:bg-accent-soft focus-visible:outline-none',
          'focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-accent',
          className,
        )}
      >
        {inner}
      </button>
    )
    : <span className={cn('flex items-stretch', className)}>{inner}</span>;

  return tip ? <Tip content={`${label} — ${tip}`}>{item}</Tip> : item;
}

/** What a person can drag the panel between. Below the floor it is not a panel, it is a sliver. */
export const PANEL_MIN = 96;
export const PANEL_MAX = 720;
const PANEL_STEP = 48;

/**
 * The stream, as a region a person can **grow, shrink and hide** (D55).
 *
 * @remarks
 * A well inside a card is the one arrangement that cannot be made bigger when you need it bigger,
 * which is why output lives in a panel in every workbench ever shipped. The console component is
 * unchanged — it fills the height this gives it (`MonoWell`'s `fill`) instead of capping at one.
 *
 * **The handle is keyboard-operable**, not only draggable: a resize that needs a mouse is a resize
 * some people do not have. Arrow keys move it a step, Home and End take it to the extremes.
 *
 * **Closing is deterministic**: nothing reopens this panel but the person. It is the reference
 * console's rule, and the reason is that a layout which springs back on a window resize teaches
 * people not to trust the control.
 */
export function OutputPanel({ sessionId, height, collapsed, onResize, onToggle }: {
  /** Whose stream — null when nothing is attended, which is a state rather than an absence. */
  sessionId: string | null;
  height: number;
  collapsed: boolean;
  onResize: (height: number) => void;
  onToggle: () => void;
}) {
  const { t } = useTranslation();
  const clamp = (value: number) => Math.min(PANEL_MAX, Math.max(PANEL_MIN, value));

  const drag = (event: React.PointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    const startY = event.clientY;
    const startHeight = height;
    const move = (moved: PointerEvent) => onResize(clamp(startHeight + (startY - moved.clientY)));
    const done = () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', done);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', done);
  };

  return (
    <section className="flex shrink-0 flex-col border-t border-line">
      {!collapsed && (
        <div
          role="separator"
          aria-orientation="horizontal"
          aria-label={t('work.panel.resize')}
          aria-valuenow={height}
          aria-valuemin={PANEL_MIN}
          aria-valuemax={PANEL_MAX}
          tabIndex={0}
          onPointerDown={drag}
          onKeyDown={(event) => {
            if (event.key === 'ArrowUp') onResize(clamp(height + PANEL_STEP));
            else if (event.key === 'ArrowDown') onResize(clamp(height - PANEL_STEP));
            else if (event.key === 'Home') onResize(PANEL_MAX);
            else if (event.key === 'End') onResize(PANEL_MIN);
            else return;
            event.preventDefault();
          }}
          className="h-1.5 cursor-ns-resize bg-transparent hover:bg-accent-soft"
        />
      )}

      <header className="flex items-center gap-2 px-4 py-1">
        <span className="text-meta text-ink-faint">{t('work.panel.title')}</span>
        <Button variant="ghost" className="ml-auto" onClick={onToggle}>
          {collapsed ? t('work.panel.show') : t('work.panel.hide')}
        </Button>
      </header>

      {!collapsed && (
        <div className="flex min-h-0 flex-col px-4 pb-3" style={{ height }}>
          {sessionId
            ? <SessionConsole id={sessionId} fill />
            : <p className="m-0 text-small text-ink-faint">{t('work.panel.none')}</p>}
        </div>
      )}
    </section>
  );
}
