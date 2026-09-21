import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Dot, Icon, type IconName, Tip } from '../ui';
import { SessionConsole } from '../SessionConsole';
import { cn } from '../lib/cn';

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
 * **`captionRoom` reserves the right edge for SURF7.** The caption buttons are drawn by the page
 * only once the window is frameless, and reserving their space now means the strip's contents do
 * not shift sideways when they arrive. A layout that moves on the next landing is a layout nobody
 * trusts.
 *
 * Until SURF7 lands, this strip and the OS title bar are both on screen. That is a known interim
 * recorded in D56, not a regression.
 */
export function AppStrip({ mode, modeAvailable, attention = 0, onMode, scope, captionRoom }: {
  mode: Mode;
  modeAvailable: boolean;
  attention?: number;
  onMode: (mode: Mode) => void;
  /** The workspace switcher, or nothing while the deployment holds one circle (WSP5). */
  scope?: ReactNode;
  captionRoom?: boolean;
}) {
  return (
    <header className="flex h-9 shrink-0 items-center gap-3 border-b border-line bg-raised pl-3 pr-2">
      <div className="flex items-baseline gap-1.5">
        {/* The serif's one appearance, and the one brand gesture beside it (D41 §1). */}
        <strong className="font-serif text-wordmark font-semibold tracking-[-0.01em]">Daoris</strong>
        <span className="text-small text-ink-faint">道衍</span>
      </div>

      <ModeSwitch mode={mode} available={modeAvailable} attention={attention} onChange={onMode} />

      <div className="ml-auto flex items-center gap-2">{scope}</div>

      {/* SURF7 draws minimize/maximize/close here, and reports their rectangles so Windows 11
          offers Snap Layouts on the maximize button (D55 §a). */}
      {captionRoom && <div aria-hidden className="h-full w-[8.25rem] shrink-0" />}
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
export function ActivityBar<T extends string>({ label, items, active, onSelect, footer }: {
  /** The bar's accessible name — passed in, so this stays a molecule with no i18n of its own. */
  label: string;
  items: { tab: T; label: string; icon: IconName; badge?: number }[];
  /** The current domain, or null in a frame where no domain is current. */
  active: T | null;
  onSelect: (tab: T) => void;
  /** Actions, not state: refresh and language. State went to the status bar. */
  footer?: ReactNode;
}) {
  return (
    <nav
      aria-label={label}
      className="flex w-12 shrink-0 flex-col items-center gap-0.5 border-r border-line py-1.5"
    >
      {items.map(({ tab, label: name, icon, badge }) => (
        <Tip key={tab} content={name}>
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
      className="inline-flex rounded-control border border-line bg-raised p-0.5"
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
export function StatusBar({ driver, sessions, workspace, remote, tier, indexed }: {
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
}) {
  const { t } = useTranslation();
  const tone = driver === 'running' ? 'live' : driver === 'stopped' ? 'parked' : 'idle';

  return (
    <footer
      aria-label={t('work.status.label')}
      className="flex flex-wrap items-center gap-x-4 gap-y-0.5 border-t border-line bg-raised px-3 py-0.5 text-meta text-ink-faint"
    >
      <span className="flex items-center gap-1.5">
        {t('work.status.driver')}
        <Dot
          tone={tone}
          label={t(`work.status.driver${driver === 'running' ? 'Running' : driver === 'stopped' ? 'Stopped' : 'Absent'}`)}
        />
      </span>
      <span>{t('work.status.sessions', { count: sessions })}</span>
      <span>
        {t('work.status.workspace')}
        {' · '}
        {workspace ?? t('work.status.everyWorkspace')}
      </span>
      {remote !== null && (
        <span>
          {t('work.status.remote')}
          {' · '}
          {remote ? t('work.status.remoteWired') : t('work.status.remoteLocal')}
        </span>
      )}

      {/* Pushed right: what the index is, rather than what the machine is doing. */}
      {tier && (
        <Tip content={tier.note}>
          <span
            className={cn(
              'ml-auto font-mono',
              tier.semantic ? 'text-accent' : 'text-warn',
            )}
          >
            {tier.label}
          </span>
        </Tip>
      )}
      {indexed && <span className={cn('tabular-nums', !tier && 'ml-auto')}>{indexed}</span>}
    </footer>
  );
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
