import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CountBadge, Dot, Icon, type IconName, Tip } from '../ui';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS } from './caption';
import { SessionConsole } from '../SessionConsole';
import { cn } from '../lib/cn';
import { Mark } from '../Mark';

// The window's own furniture (D55, extended by D56, simplified by D66): the app strip, the activity
// bar, the status bar and the output panel. Small, presentational, and kept together because they
// are one arrangement rather than four features — what the workbench references all have and what
// this platform had nowhere to put.

/**
 * The **app strip** (D56): the application's one global row, across the top of the window.
 *
 * @remarks
 * It holds what is true everywhere — the mark, the application's menus, the command center — which
 * is precisely why none of them belongs in a sidebar owned by one view. Measured before it was
 * built: 42% of the window's width was navigation and a form, and the session that is the
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
  scope, center, menus, captionRoom,
  stripRef, onDragStart, onToggleMaximize, onResizeTop,
}: {
  /** The workspace switcher, or nothing while the deployment holds one circle (WSP5). */
  scope?: ReactNode;
  /** The command center — where you are, and the way into everything (taken from VS Code's shape). */
  center?: ReactNode;
  /** The application's menus — settings, help — which is what a title bar holds in an IDE. */
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
        // Only the strip itself drags. A press that began on a menu or the scope is that control's,
        // and handing it to the OS would make every button a drag handle.
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

      {/* The application's menus (VS Code's menu bar). There is no mode switch beside them any more
          (D66): the activity bar is the one navigation, so a second one up here was a concept and a
          click between every reading and every working. */}
      {menus}

      {/* The middle, which used to be ~1,400px of nothing at any real window width. Absolutely
          positioned so it centres on the STRIP rather than on whatever is left over after the mark
          and the menus — a flex-centred child drifts as those change width. */}
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

/** One place on the activity bar. */
export type ActivityItem<T extends string> = {
  tab: T;
  label: string;
  icon: IconName;
  /** A count on the icon. Zero wears nothing. */
  badge?: number;
  /**
   * `open` for a count that IS a status — sessions waiting on a person — and the accent for a
   * quantity. The only status hue on the bar, because it is the only status on it.
   */
  tone?: 'accent' | 'open';
};

/**
 * The **activity bar**: the application's one navigation, down the window's left edge (D56, and
 * since D66 the only one).
 *
 * @remarks
 * 🔴 **One list, not two frames** (owner, 2026-09-23: *"since its all tab based so there probably no
 * need for manage/work?"*). The bar carried two frame icons above a rule and the views below them —
 * a second navigation stacked on the first, and a click between every reading and every working.
 * Sessions is a view now, one of the list, and nothing is gated behind a mode.
 *
 * It replaced a 15rem labelled sidebar. The cost is the labels, paid by the tooltip here, each
 * view's own page header, and SURF9's palette.
 *
 * **Absent, never disabled**: a browser is handed a shorter `items` (no Sessions — a stream never
 * leaves the machine), because a greyed row implies the thing exists somewhere you could get to.
 */
export function ActivityBar<T extends string>({
  label, items, end = [], active, onSelect, footer,
}: {
  /** The bar's accessible name — passed in, so this stays a molecule with no i18n of its own. */
  label: string;
  items: ActivityItem<T>[];
  /**
   * The places at the FOOT, below the actions — Settings, where every workbench puts its gear.
   * Places rather than actions, so they wear the current-place marking the list above does.
   */
  end?: ActivityItem<T>[];
  active: T;
  onSelect: (tab: T) => void;
  /** Actions, not state: refresh and language. State went to the status bar. */
  footer?: ReactNode;
}) {
  const place = ({ tab, label: name, icon, badge, tone }: ActivityItem<T>) => (
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
        <CountBadge count={badge ?? 0} tone={tone} />
      </button>
    </Tip>
  );

  return (
    <nav
      aria-label={label}
      className="flex w-12 shrink-0 flex-col items-center gap-0.5 border-r border-line py-1.5"
    >
      {items.map(place)}

      {(footer || end.length > 0) && (
        <div className="mt-auto flex flex-col items-center gap-0.5">
          {footer}
          {end.map(place)}
        </div>
      )}
    </nav>
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
  driver, sessions, workspace, remote, sync, tier, indexed, scope,
  onDriver, onSessions, onRemote, onIndex, onTier,
}: {
  driver: DriverPresence;
  sessions: number;
  /**
   * The scope in words (D75 §3): the chosen workspace, the one there is, every one among several, or
   * none yet. Null only while the registry has not answered.
   */
  workspace: string | null;
  /** Whether this workspace has a deployment wired, or null where the question cannot be asked. */
  remote: boolean | null;
  /**
   * Where a wired circle stands, as a control (SYNC6b) — it takes the remote item's place, because
   * "wired" is exactly what it elaborates. Absent leaves the plain remote item.
   */
  sync?: ReactNode;
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
  /** Where the tier is explained and changed — Daoris's own AI, on Settings (AGT6). */
  onTier?: () => void;
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

      {sync ? <span className="hidden items-stretch sm:flex">{sync}</span> : remote !== null && (
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
        {/* The tier's sentence stays the service's own, verbatim (D24), in the tip. It was text
            alone while there was nothing to go to; since AGT6 it leads where the tier is explained
            and how to change it is said — Daoris's own AI, on Settings — by the bar's own rule that
            an item goes where its fact is set. */}
        {tier && (
          <StatusItem onPress={onTier} tip={tier.note} label={t('work.status.tierLabel')}>
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
 * What makes a status item PRESSABLE: the hover wash, the focus ring, the full-height box. Exported so
 * an item that opens a menu rather than going somewhere (the sync item, SYNC6b) is the same control
 * to look at and to tab to — two copies of this string would be two bars.
 */
export const STATUS_PRESSABLE = cn(
  'flex items-stretch transition-colors duration-[var(--speed)]',
  'hover:bg-accent-soft hover:text-ink',
  'focus-visible:bg-accent-soft focus-visible:outline-none',
  'focus-visible:ring-1 focus-visible:ring-inset focus-visible:ring-accent',
);

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
        className={cn(STATUS_PRESSABLE, className)}
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
          {/* The panel keeps its height; a session with nothing held here keeps it as a sentence,
              not as an empty bordered well, which read as a field on the installed window. */}
          {sessionId
            ? <SessionConsole id={sessionId} fill quiet={t('work.panel.silent')} />
            : <p className="m-0 text-small text-ink-faint">{t('work.panel.none')}</p>}
        </div>
      )}
    </section>
  );
}
