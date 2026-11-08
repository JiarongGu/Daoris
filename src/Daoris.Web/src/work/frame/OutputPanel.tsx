import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, DotMark, Icon, type IconName } from '../../design/ui';
import { cn } from '../../lib/cn';
import type { Place, ViewId } from './placements';
import { DropMark, viewEntries, ViewsMenu } from './ViewsMenu';
import { dragProps, useViewDrop } from './viewDrag';
import { useTabFit } from './tabFit';

/**
 * One tab of the output panel: the session's own console, or something it runs beside
 * itself — a subagent, or background work.
 */
export type PanelTab = {
  /** What the console under it tails. */
  key: string;
  kind: 'session' | 'subagent' | 'task';
  label: string;
  tone: 'live' | 'ended' | 'failed' | 'idle';
  /** How it stands, in words: the tab is named by it, so the mark is never hue alone. */
  status: string;
  /** Whether a person can stop it from here: a running task its harness says can be stopped. */
  stoppable?: boolean;
};

const TAB_ICON: Record<PanelTab['kind'], IconName> = { session: 'frameWork', subagent: 'think', task: 'execute' };

/** What a person can drag the panel between. Below the floor it is not a panel, it is a sliver. */
export const PANEL_MIN = 96;
export const PANEL_MAX = 720;
const PANEL_STEP = 48;

/**
 * The stream, as a region a person can **grow, shrink and hide**.
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
 * **A tab per thing that is running**: the session, then each subagent and background
 * task it started, so none of their output is missed. One tab is no tabs — a session running
 * nothing beside itself keeps the header it had.
 *
 * **A region, not the console's alone**: it holds whichever views stand in the panel, the
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
   * imports no hook, not even through an organism.
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
  /** Stop a stoppable tab's task. */
  onStop?: (key: string) => void;
  /** The views standing in the panel, the console alone until the person moves one. */
  views?: readonly ViewId[];
  /** The view shown; ignored when it is not one of `views`. */
  view?: ViewId;
  onView?: (view: ViewId) => void;
  onMove?: (view: ViewId, to: Place) => void;
  onReset?: () => void;
  /** Told while one of its views is dragged, and with null when the drag ends. */
  onDrag?: (view: ViewId | null) => void;
  /** The shown view when it is not the console. */
  children?: ReactNode;
}) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState(false);
  // A view dragged from the side bar lands here, hidden or shown.
  const drop = useViewDrop('panel', onMove);
  const carry = (id: ViewId) => (onMove ? dragProps(id, 'panel', onDrag) : {});
  const clamp = (value: number) => Math.min(PANEL_MAX, Math.max(PANEL_MIN, value));
  const entries = viewEntries(t, views);
  const shown = entries.some((entry) => entry.id === view) ? view : entries[0]?.id;
  const tabbed = shown === 'console' && tabs && tabs.length > 1;
  // Whether the unselected views are their icons. The header also holds the console's streams, a
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
      window.removeEventListener('pointercancel', done);
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', done);
    window.addEventListener('pointercancel', done);
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
                    // The side bar's tabs, a size down: the selected one whole, and the rest whole
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
          // name as VS Code's lone view is by its title.
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
          {/* Absent where it would list one view and offer nothing (absent, never a door that does
              nothing): a detached session's window, which has no side bar to move a view to. */}
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
 * The console's own tabs: the session, then each subagent and background task it runs,
 * each named by how it stands. Its own molecule, because the console can stand in the
 * right side bar too, and its streams go with it.
 *
 * @remarks
 * **The selected task carries its stop, after the tabs**, as VS Code's panel carries *Kill
 * Terminal* for the terminal in view: only while it runs and its harness says it can be stopped, and
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
