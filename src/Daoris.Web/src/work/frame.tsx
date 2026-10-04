import { type PointerEvent as ReactPointerEvent, type ReactNode, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, CountBadge, Dot, DotMark, Icon, type IconName, Tip } from '../ui';
import { CAPTION_ATTRIBUTE, CAPTION_SLOTS } from './caption';
import { cn } from '../lib/cn';
import { Mark } from '../Mark';
import type { Place, ViewId } from './placements';
import { DropMark, viewEntries, ViewsMenu } from './ViewsMenu';
import { dragProps, useViewDrop } from './viewDrag';
import { useTabFit } from './tabFit';
import { LIST_DOOR } from './listKeys';

// The window's own furniture (D55, extended by D56, simplified by D66): the app strip, the activity
// bar, the status bar and the output panel. Small, presentational, and kept together because they
// are one arrangement rather than four features — what the workbench references all have and what
// this platform had nowhere to put.

/** The attribute the app strip's layout groups carry: their empty space is the strip's own. */
export const STRIP_SPACE = 'data-strip-space';

/** A press on the strip itself, or on space one of its groups leaves empty — never on a control. */
function isStripSpace(target: EventTarget, strip: EventTarget) {
  return target === strip || (target instanceof HTMLElement && target.hasAttribute(STRIP_SPACE));
}

/**
 * How far, in CSS px either side of the press, the pointer may travel on the strip before the press is
 * a drag (FRAME2). Windows' own drag distance (`SM_CXDRAG`, `SM_CYDRAG`) is 4 px by default; a page
 * cannot read the system metric, so the default stands in for it.
 */
export const DRAG_DISTANCE = 4;

/**
 * The strip's press, as a caption's (FRAME2): it only notes where it went down, and the drag begins once
 * the pointer travels past {@link DRAG_DISTANCE} with the button still held. A still click is nothing,
 * and since no move loop has started, the browser's `dblclick` arrives for a double-click.
 *
 * @remarks
 * Handing the press straight to the OS move loop, as SURF7 did, restored a maximized window on a single
 * click and let the loop swallow a double-click's second press. The move and the release are heard on
 * the window rather than the strip, because a quick drag leaves the strip's 36px before its first move.
 */
function useCaptionPress(onDragStart: (() => void) | undefined) {
  // The latest handler, read when the drag begins: it restores first only while the window is
  // maximized, and a press can outlive the render it began in.
  const drag = useRef(onDragStart);
  drag.current = onDragStart;
  const release = useRef<(() => void) | null>(null);
  useEffect(() => () => release.current?.(), []);

  return (event: ReactPointerEvent<HTMLElement>) => {
    release.current?.();
    // Only the strip's own space drags. A press that began on a menu or the scope is that control's,
    // and handing it to the OS would make every button a drag handle.
    if (!drag.current || event.button !== 0 || !isStripSpace(event.target, event.currentTarget)) return;

    const { pointerId, clientX: x, clientY: y } = event;
    const move = (next: PointerEvent) => {
      if (next.pointerId !== pointerId) return;
      // The button came up where nothing told us: the press is over, and it never became a drag.
      if ((next.buttons & 1) === 0) return stop();
      if (Math.abs(next.clientX - x) <= DRAG_DISTANCE && Math.abs(next.clientY - y) <= DRAG_DISTANCE) return;
      stop();
      drag.current?.();
    };
    const stop = () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', stop);
      window.removeEventListener('pointercancel', stop);
      release.current = null;
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', stop);
    window.addEventListener('pointercancel', stop);
    release.current = stop;
  };
}

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
  scope, center, menus, trailing, captionRoom,
  stripRef, onDragStart, onToggleMaximize, onResizeTop, onSystemMenu,
}: {
  /** The workspace switcher, or nothing while the deployment holds one circle (WSP5). */
  scope?: ReactNode;
  /** The command center — where you are, and the way into everything (taken from VS Code's shape). */
  center?: ReactNode;
  /** The application's menus — settings, help — which is what a title bar holds in an IDE. */
  menus?: ReactNode;
  /**
   * What toggles a region, at the strip's right beside the window's controls, as VS Code's layout
   * toggles sit — Ask Daoris's door, since it opens the right region.
   */
  trailing?: ReactNode;
  captionRoom?: boolean;
  stripRef?: (element: HTMLElement | null) => void;
  /**
   * Called once a press on the strip's own space has travelled past {@link DRAG_DISTANCE}, never on the
   * press itself (FRAME2). Absent in a browser: there is no window to move, so the strip is simply a strip.
   */
  onDragStart?: () => void;
  onToggleMaximize?: () => void;
  onResizeTop?: () => void;
  /** A right-click on the strip's own space opens the window's system menu, as a caption's does (CTX1, D138 §4). */
  onSystemMenu?: () => void;
}) {
  const onPress = useCaptionPress(onDragStart);
  return (
    <header
      ref={stripRef}
      onContextMenu={(event) => {
        // Only the strip's own space is the caption; a control's right-click is the page's to answer.
        if (!onSystemMenu || !isStripSpace(event.target, event.currentTarget)) return;
        event.preventDefault();
        onSystemMenu();
      }}
      // The title bar's own gesture, as a caption's (FRAME2): a press notes where it went down, and a
      // move past the drag distance starts the OS move loop while the button is still held, which is
      // also why the host dispatches it inline.
      onPointerDown={onPress}
      // A double-click reaches here because a still press starts no move loop to swallow it. The
      // engine decides what counts as one, so the page keeps no clock of its own.
      onDoubleClick={(event) => {
        if (!isStripSpace(event.target, event.currentTarget)) return;
        onToggleMaximize?.();
      }}
      // 🔴 One line, by rule: a Chinese label may break between any two characters, so without this
      // 中文's menus wrapped onto two lines each (道 / 衍) once the sides shared the strip (UX5 U15).
      className="relative flex h-9 shrink-0 items-center gap-3 whitespace-nowrap border-b border-line bg-raised"
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

      {/* 🔴 Three groups, and the middle one is the command center (UX5 U15). The two sides grow
          alike from nothing, so the middle is centred on the STRIP while both fit beside it, rather
          than on whatever the mark and the menus leave over. When they do not fit, the middle gives
          way instead of covering them: it was laid over the strip at a fixed 28rem, and at 888px it
          ran over the View menu. VS Code's title bar behaves the same way. 🔴 The mark's inset is
          the mark's own: the sides share the free space by their CONTENT boxes, so padding on the
          strip or on a group puts the middle that far off centre (12px, measured on the window). */}
      <div {...{ [STRIP_SPACE]: 'start' }} className="flex h-full flex-1 basis-0 items-center gap-3">
        {/* 🔴 The MARK alone. The name was 20px of serif in every window
            forever, and a title bar is where an IDE puts what you can DO — the application's name
            belongs in its About, which the menu beside this now carries. D41 §1 gave the serif one
            appearance; this is it not being spent on saying the name of the thing you are already
            looking at. */}
        <div className="pointer-events-none flex items-center pl-3.5">
          <Mark size={18} className="text-accent" />
        </div>

        {/* The application's menus (VS Code's menu bar). There is no mode switch beside them any
            more (D66): the activity bar is the one navigation, so a second one up here was a concept
            and a click between every reading and every working. */}
        {menus}
      </div>

      {/* The middle, which used to be ~1,400px of nothing at any real window width. */}
      <div {...{ [STRIP_SPACE]: 'center' }} className="flex h-full min-w-0 basis-md items-center justify-center">
        {center}
      </div>

      <div {...{ [STRIP_SPACE]: 'end' }} className="flex h-full flex-1 basis-0 items-center justify-end gap-3">
        <div className="flex items-center gap-2">{scope}</div>
        {trailing && <div className="flex items-center gap-1">{trailing}</div>}

        {/* Reserved, never drawn: the window owns these pixels. Three slots of 44px — the width the
            strip has always held open, so nothing shifted when they became real. */}
        {captionRoom && (
          <div aria-hidden className="flex h-full shrink-0">
            {CAPTION_SLOTS.map((kind) => (
              <div key={kind} {...{ [CAPTION_ATTRIBUTE]: kind }} className="h-full w-11" />
            ))}
          </div>
        )}
      </div>
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
  /** Its key, as the Go menu prints it (UX7a, D152 §3.4), said in its tip: a place's position is its key. */
  keys?: string;
};

/**
 * The **activity bar**: the application's one navigation, down the window's left edge (D56, and
 * since D66 the only one).
 *
 * @remarks
 * 🔴 **One list, not two frames**: every view is a tab, so there is nothing for two to divide. The
 * bar carried two frame icons above a rule and the views below them —
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
  label, items, end = [], active, onSelect, onToggleCurrent, footer,
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
  /**
   * A press on the place you are on (D118 §3a, audit F1): its view's list toggles, as VS Code's activity
   * bar toggles its side bar. Absent where the view has no list, and the press then selects it again.
   */
  onToggleCurrent?: () => void;
  /** Actions, not state: refresh and language. State went to the status bar. */
  footer?: ReactNode;
}) {
  const place = ({ tab, label: name, icon, badge, tone, keys }: ActivityItem<T>) => (
    <Tip key={tab} content={keys ? `${name} (${keys})` : name} side="right">
      <button
        type="button"
        aria-label={name}
        aria-keyshortcuts={keys}
        aria-current={active === tab ? 'page' : undefined}
        // The current place is a door to its list where it toggles it (D118 §3a).
        {...(active === tab && onToggleCurrent ? { [LIST_DOOR]: '' } : {})}
        onClick={() => (active === tab && onToggleCurrent ? onToggleCurrent() : onSelect(tab))}
        className={cn(
          'relative flex h-9 w-9 shrink-0 items-center justify-center rounded-control transition-colors duration-(--speed)',
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
      data-region="activity"
      // 🔴 A place keeps its size, and a bar too short to hold them scrolls (UX5 U22). At the
      // window's least height the places shrank until they touched, the counts sat over their
      // neighbours and Settings went under the status bar. No scrollbar is drawn in a 48px bar;
      // the wheel and the keyboard still reach every place.
      className="flex min-h-0 w-12 shrink-0 flex-col items-center gap-0.5 overflow-x-hidden overflow-y-auto border-r border-line py-1.5 [scrollbar-width:none]"
    >
      {items.map(place)}

      {(footer || end.length > 0) && (
        <div className="mt-auto flex shrink-0 flex-col items-center gap-0.5">
          {footer}
          {end.map(place)}
        </div>
      )}
    </nav>
  );
}

/**
 * What the driver is, as a surface can honestly know it. `starting` is a shell whose driver answers from its file while
 * its service is not up yet, so every route that reads the service still refuses (LOOK2a).
 */
export type DriverPresence = 'running' | 'starting' | 'stopped' | 'absent';

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
  driver, sessions, workspace, remote, sync, tier, indexed, scope, setup,
  onDriver, onSessions, onRemote, onIndex, onTier, onSetup,
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
  /**
   * How many of the setup's required steps are done (SETUP1b, D97), stated until they all are — or
   * absent where that is not known, a browser or a machine still being read.
   */
  setup?: { done: number; of: number };
  /** Where the setup is done: Get started, on Settings. */
  onSetup?: () => void;
}) {
  const { t } = useTranslation();
  const tone = driver === 'running' ? 'live' : driver === 'stopped' ? 'parked' : 'idle';
  const driverWord = driver === 'running' ? 'Running' : driver === 'starting' ? 'Starting' : driver === 'stopped' ? 'Stopped' : 'Absent';

  return (
    <footer
      aria-label={t('work.status.label')}
      data-region="status"
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

      {/* 🔴 The scope, and it is a CONTROL here. It had a dropdown in the app strip AND a read-only copy here,
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

      {/* Until the setup's required steps are done, and then gone: an always-there all-clear is not
          read. Waiting on the person, so the mark wears open's hue (platform-ux §3). */}
      {setup && setup.done < setup.of && (
        <StatusItem onPress={onSetup} tip={t('work.status.setupTip')} label={t('work.status.setupLabel')}>
          <Icon name="plan" size={12} className="text-st-open" />
          <span className="tabular-nums text-ink">{t('work.status.setup', setup)}</span>
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
 * Brought to VS Code's status bar — what it displays, and what it does on click or hover — and the
 * two halves need different answers.
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

/** How far one arrow press moves an edge — a column's width in a few presses, not dozens. */
const SPLITTER_STEP = 24;

/**
 * The edge a column is resized by (FRAME6): the rail's right edge, the dock's left.
 *
 * @remarks
 * **Moving away from its column grows the column**, whichever side of it the edge is on, by pointer
 * and by arrow alike — the output panel's rule turned on its side. Home and End take it to either end,
 * and a double-click puts the column back where it started, as a sash does in every workbench.
 *
 * It lies OVER the border between two columns, 6px wide and centred on it, so it takes no width of its
 * own: its parent is positioned, and `edge` says which side of it this is.
 */
export function Splitter({ label, value, min, max, edge, onChange, onReset }: {
  label: string;
  value: number;
  min: number;
  max: number;
  /** Which edge of its column this is. */
  edge: 'left' | 'right';
  onChange: (value: number) => void;
  onReset?: () => void;
}) {
  const clamp = (next: number) => Math.min(max, Math.max(min, next));
  const outward = edge === 'right' ? 1 : -1;

  const drag = (event: React.PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;
    event.preventDefault();
    const startX = event.clientX;
    const move = (moved: PointerEvent) => onChange(clamp(value + (moved.clientX - startX) * outward));
    const done = () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', done);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', done);
  };

  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label={label}
      aria-valuenow={value}
      aria-valuemin={min}
      aria-valuemax={max}
      tabIndex={0}
      onPointerDown={drag}
      onDoubleClick={onReset}
      onKeyDown={(event) => {
        if (event.key === 'ArrowRight') onChange(clamp(value + SPLITTER_STEP * outward));
        else if (event.key === 'ArrowLeft') onChange(clamp(value - SPLITTER_STEP * outward));
        else if (event.key === 'Home') onChange(max);
        else if (event.key === 'End') onChange(min);
        else return;
        event.preventDefault();
      }}
      className={cn(
        'absolute inset-y-0 z-10 w-1.5 cursor-ew-resize bg-transparent transition-colors duration-(--speed)',
        'hover:bg-accent-soft focus-visible:bg-accent-soft focus-visible:outline-none',
        edge === 'right' ? '-right-[3px]' : '-left-[3px]',
      )}
    />
  );
}

/**
 * One tab of the output panel (CONSOLE2): the session's own console, or something it runs beside
 * itself — a subagent, or background work.
 */
export type PanelTab = {
  /** What the console under it tails. */
  key: string;
  kind: 'session' | 'subagent' | 'task';
  label: string;
  tone: 'live' | 'ended' | 'failed' | 'idle';
  /** How it stands, in words: the tab is named by it, so the mark is never hue alone (D41 §6). */
  status: string;
  /** Whether a person can stop it from here (CONSOLE3a): a running task its harness said can be stopped. */
  stoppable?: boolean;
};

const TAB_ICON: Record<PanelTab['kind'], IconName> = { session: 'frameWork', subagent: 'think', task: 'execute' };

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
 *
 * **A tab per thing that is running** (CONSOLE2): the session, then each subagent and background
 * task it started, so none of their output is missed. One tab is no tabs — a session running
 * nothing beside itself keeps the header it had.
 *
 * **A region, not the console's alone** (DOCK1b): it holds whichever views stand in the panel, the
 * console by default, as VS Code's panel does; with more than one, each is a tab, and the list at
 * the header's end names them all and moves the shown one to the right side bar.
 */
export function OutputPanel({
  console: stream, height, collapsed, onResize, onToggle, tabs, selected, onSelect, onStop,
  views = ['console'], view, onView, onMove, onReset, onDrag, children,
}: {
  /**
   * The attended session's console, or null when nothing is attended — a state rather than an
   * absence. Handed in by the organism above, because the console reaches the bridge and a molecule
   * imports no hook, not even through an organism (REV3).
   */
  console: ReactNode | null;
  height: number;
  collapsed: boolean;
  onResize: (height: number) => void;
  onToggle: () => void;
  /** The session's tab first, then its streams, oldest first. */
  tabs?: PanelTab[];
  /** The tab whose console is shown. */
  selected?: string;
  onSelect?: (key: string) => void;
  /** Stop a stoppable tab's task (CONSOLE3a). */
  onStop?: (key: string) => void;
  /** The views standing in the panel (DOCK1b), the console alone until the person moves one. */
  views?: readonly ViewId[];
  /** The view shown; ignored when it is not one of `views`. */
  view?: ViewId;
  onView?: (view: ViewId) => void;
  onMove?: (view: ViewId, to: Place) => void;
  onReset?: () => void;
  /** Told while one of its views is dragged, and with null when the drag ends (DOCK1e). */
  onDrag?: (view: ViewId | null) => void;
  /** The shown view when it is not the console. */
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState(false);
  // A view dragged from the side bar lands here, hidden or shown (DOCK1e).
  const drop = useViewDrop('panel', onMove);
  const carry = (id: ViewId) => (onMove ? dragProps(id, 'panel', onDrag) : {});
  const clamp = (value: number) => Math.min(PANEL_MAX, Math.max(PANEL_MIN, value));
  const entries = viewEntries(t, views);
  const shown = entries.some((entry) => entry.id === view) ? view : entries[0]?.id;
  const tabbed = shown === 'console' && tabs && tabs.length > 1;
  // Whether the unselected views are their icons (TABS1). The header also holds the console's streams, a
  // stop and the show or hide, so what they say changes what the views' names need too.
  const fit = useTabFit([
    ...entries.map((entry) => entry.label), shown ?? '', collapsed,
    ...(tabbed ? tabs.map((tab) => tab.label) : []), tabbed ? selected ?? '' : '',
  ].join('\n'));

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
    <section aria-label={t('layout.panel')} data-region="panel" className="relative flex shrink-0 flex-col border-t border-line" {...drop.props}>
      {drop.over && <DropMark />}
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

      <header ref={fit.row} data-fit={fit.compact ? 'icons' : 'names'} className="flex min-w-0 items-center gap-2 px-4 py-1">
        {entries.length > 1
          ? (
            <div role="tablist" aria-label={t('work.views.panel')} className="flex min-w-0 shrink items-center overflow-x-auto [scrollbar-width:none]">
              {entries.map(({ id, label, icon }) => (
                <button
                  key={id}
                  type="button"
                  role="tab"
                  aria-selected={shown === id}
                  aria-label={label}
                  title={label}
                  onClick={() => onView?.(id)}
                  {...carry(id)}
                  onContextMenu={(event) => {
                    event.preventDefault();
                    onView?.(id);
                    setMenu(true);
                  }}
                  className={cn(
                    // The side bar's tabs, a size down (TABS1): the selected one whole, and the rest whole
                    // while the header holds every name, their icons alone when it does not; never cut.
                    'flex shrink-0 items-center gap-1.5 whitespace-nowrap border-b-2 px-2 py-0.5 text-meta transition-colors duration-(--speed)',
                    shown === id ? 'border-b-accent text-ink' : 'border-b-transparent text-ink-faint hover:text-ink',
                  )}
                >
                  <Icon name={icon} size={12} className="shrink-0" />
                  {(shown === id || !fit.compact) && <span>{label}</span>}
                </button>
              ))}
            </div>
          )
          // One view is no tabs: the header it always had, named for what it holds, and dragged by its
          // name as VS Code's lone view is by its title (DOCK1e).
          : entries[0] && (
            <span
              className={cn('shrink-0 text-meta text-ink-faint', onMove && 'cursor-grab')}
              {...carry(entries[0].id)}
            >
              {shown === 'console' ? t('work.panel.title') : entries[0].label}
            </span>
          )}
        {tabbed && <StreamTabs tabs={tabs} selected={selected} onSelect={onSelect} onStop={onStop} />}
        <div className="ml-auto flex shrink-0 items-center gap-1">
          {/* Absent where it would list one view and offer nothing (D118 §3a: absent, never a door that does
              nothing): a detached session's window, which has no side bar to move a view to (FRAME1h). */}
          {(entries.length > 1 || onMove || onReset) && (
            <ViewsMenu
              region="panel"
              views={entries}
              selected={shown}
              onSelect={(id) => onView?.(id)}
              onMove={onMove}
              onReset={onReset}
              open={menu}
              onOpenChange={setMenu}
            />
          )}
          <Button variant="ghost" className="shrink-0" onClick={onToggle}>
            {collapsed ? t('work.panel.show') : t('work.panel.hide')}
          </Button>
        </div>
      </header>

      {!collapsed && (shown === 'console' || !shown
        ? (
          <div className="flex min-h-0 flex-col px-4 pb-3" style={{ height }}>
            {/* The panel keeps its height; a session with nothing held here keeps it as a sentence,
                not as an empty bordered well, which read as a field on the installed window. */}
            {shown
              ? stream ?? <p className="m-0 text-small text-ink-faint">{t('work.panel.none')}</p>
              // Every view moved out: VS Code's empty container, which says how to fill it.
              : <p className="m-0 text-small text-ink-faint">{t('work.views.empty')}</p>}
          </div>
        )
        : (
          <div className="flex min-h-0 flex-col overflow-y-auto" style={{ height }}>
            {children}
          </div>
        ))}
    </section>
  );
}

/**
 * The console's own tabs (CONSOLE2): the session, then each subagent and background task it runs,
 * each named by how it stands. Its own molecule since DOCK1b, because the console can stand in the
 * right side bar too, and its streams go with it.
 *
 * @remarks
 * **The selected task carries its stop, after the tabs** (CONSOLE3a), as VS Code's panel carries *Kill
 * Terminal* for the terminal in view: only while it runs and its harness said it can be stopped, and
 * named for the task it stops. Not inside the tab list, which owns tabs and nothing else, and not
 * inside a tab, whose content is one label to a screen reader.
 */
export function StreamTabs({ tabs, selected, onSelect, onStop }: {
  tabs: PanelTab[];
  selected?: string;
  onSelect?: (key: string) => void;
  onStop?: (key: string) => void;
}) {
  const { t } = useTranslation();
  const stoppable = tabs.find((tab) => tab.key === selected && tab.stoppable);
  return (
    <div className="flex min-w-0 items-center gap-1">
      <div role="tablist" aria-label={t('work.panel.tabs')} className="flex min-w-0 items-center gap-0.5 overflow-x-auto">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            type="button"
            role="tab"
            aria-selected={selected === tab.key}
            aria-label={`${tab.label} — ${tab.status}`}
            title={tab.status}
            onClick={() => onSelect?.(tab.key)}
            className={cn(
              'flex max-w-48 shrink-0 cursor-pointer items-center gap-1.5 rounded-control border-0 px-2 py-0.5 text-meta transition-colors duration-(--speed)',
              selected === tab.key ? 'bg-raised text-ink' : 'bg-transparent text-ink-faint hover:text-ink',
            )}
          >
            <Icon name={TAB_ICON[tab.kind]} size={12} className="shrink-0" />
            <span className="min-w-0 truncate">{tab.label}</span>
            <DotMark tone={tab.tone} />
          </button>
        ))}
      </div>
      {stoppable && onStop && (
        <button
          type="button"
          aria-label={t('work.panel.stream.stop', { name: stoppable.label })}
          title={t('work.panel.stream.stop', { name: stoppable.label })}
          onClick={() => onStop(stoppable.key)}
          className="flex shrink-0 cursor-pointer items-center gap-1 rounded-control border-0 bg-transparent px-1.5 py-0.5 text-meta text-ink-faint transition-colors duration-(--speed) hover:bg-raised hover:text-warn"
        >
          <Icon name="stop" size={10} />
          <span>{t('work.panel.stream.stopShort')}</span>
        </button>
      )}
    </div>
  );
}
