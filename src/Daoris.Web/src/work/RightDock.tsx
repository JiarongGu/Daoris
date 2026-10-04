import { useTranslation } from 'react-i18next';
import { type ReactNode, useState } from 'react';
import type { SessionState } from '../api';
import { Button, Icon, SESSION_ACTIVE, Tip } from '../ui';
import { cn } from '../lib/cn';
import { Splitter } from './frame';
import type { DockMode } from './layout';
import type { Place, ViewId } from './placements';
import { DropMark, viewEntries, ViewsMenu } from './ViewsMenu';
import { dragProps, useViewDrop } from './viewDrag';
import { useTabFit } from './tabFit';

// The frame's third column (components plan §3a). It was deliberately unbuilt until it had a SECOND
// occupant — a dock holding one thing is a pane with extra chrome — and SURF6's diff is that
// occupant, so the timeline moves here and the attended column gets its height back.

/**
 * What the dock can show: any view that stands in the right side bar (DOCK1b), and a file's preview
 * (PREVIEW1, D111), which is a tab of the side bar and never a view that moves.
 */
export type DockTab = ViewId | 'preview';

/** A file's preview as the dock's tab: its name, its path for the tip, and its own ×. */
export type DockPreview = { name: string; path: string; onClose: () => void };

/**
 * The session a session's views follow (UX6b, D150 §8, design §2.5): on Sessions the attended one, whatever its state;
 * off Sessions only while it runs or waits on the person. An ended session's record is read on Sessions, and off it the
 * side bar kept one from the day before, its whole done note running past the window's foot.
 */
export function followed<S extends { state: SessionState }>(attended: S | null, offSessions: boolean): S | null {
  return attended && (!offSessions || SESSION_ACTIVE.has(attended.state)) ? attended : null;
}

/**
 * The tab the side bar opens on before the person picks one for the session it follows: Ask Daoris off Sessions with no
 * session followed, since a session's views have nothing to say there (UX6b); the timeline otherwise (FRAME6).
 */
export const dockOpensOn = (offSessions: boolean, following: boolean): DockTab => (offSessions && !following ? 'ask' : 'timeline');

/**
 * What a session's view says off Sessions with no session followed: that none is attended, without naming a list the
 * view does not have (audit SE11), or that the one attended has ended and is read on Sessions (UX6b); and the door there.
 */
export function NothingFollowed({ ended, onOpenSessions }: {
  /** A session is attended on Sessions, and has ended. */
  ended: boolean;
  onOpenSessions?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="grid justify-items-start gap-2 p-3">
      <p className="m-0 text-small text-ink-faint">{t(ended ? 'work.frame.ended' : 'work.frame.none')}</p>
      {onOpenSessions && <Button onClick={onOpenSessions}>{t('work.frame.goSessions')}</Button>}
    </div>
  );
}

/**
 * The right dock: per-session surfaces, keyed to whatever the person is attending.
 *
 * @remarks
 * **One selection binds every region** (IDE study §3), so this is handed the tab and the content
 * rather than choosing either — which also keeps every state reachable by passing props.
 *
 * **Tabs rather than stacking**, because the attended session already has four parts and a laptop
 * has one screen (study §2, the run-list references). The timeline and the diff answer different
 * questions about the same session and are never both urgent.
 *
 * **Its geometry is the frame's** (FRAME6, `layout.ts`), handed in as a mode and a width:
 * - `docked` beside the session, resized by its left edge;
 * - `cramped` at its floor with the session already squeezed, where it asks to be closed rather than
 *   squeezing further;
 * - `full` over the whole frame — the SAME element drawn larger, so a surface switched in and out of it
 *   is never drawn anew and keeps what the person did in it;
 * - `closed`, a strip of its tabs, since nothing but the person opens it again.
 *
 * **What it holds is the person's** (DOCK1b): the views standing in the right side bar, in the order
 * `viewsIn` gives, any of which can move to the panel from its tab's menu. Emptied, it says so.
 *
 * **A file's preview is a tab after them** (PREVIEW1, D111), named for the file and closed by its own ×:
 * it exists only once a door opened it, so it is not a view, never moves, and is not in the tab list's
 * moves.
 */
export function RightDock({
  tab, onTab, views = ['timeline', 'review'], onMove, onReset, onDrag, preview,
  mode, width, range, autoFull = false, onResize, onResetWidth, onClose, onOpen, onFull, children,
}: {
  /** The view shown; ignored when it is not one of `views`, or `preview` with no preview. */
  tab?: DockTab;
  /** The file previewed here, where a door opened one. */
  preview?: DockPreview | null;
  /**
   * The views standing here: the session's timeline and review, Ask Daoris beside them — the one
   * right region, as VS Code's chat is a view of its secondary side bar — and whatever the person moved in.
   */
  views?: readonly ViewId[];
  /** Sends a view to the other region (DOCK1b). */
  onMove?: (view: ViewId, to: Place) => void;
  /** Puts every view back where it started; absent while none has moved. */
  onReset?: () => void;
  /** Told while one of its tabs is dragged, and with null when the drag ends (DOCK1e). */
  onDrag?: (view: ViewId | null) => void;
  onTab: (tab: DockTab) => void;
  mode: DockMode;
  width: number;
  /** How far its edge may be dragged now. */
  range?: { min: number; max: number };
  /** Full because the window is narrow, which only widening undoes — so no way back is offered. */
  autoFull?: boolean;
  onResize?: (width: number) => void;
  onResetWidth?: () => void;
  onClose: () => void;
  onOpen: (tab: DockTab) => void;
  onFull?: (full: boolean) => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  // The tab list's menu, opened by its button or by a right-click on a tab.
  const [menu, setMenu] = useState(false);

  const tabs = viewEntries(t, views);
  const shown: DockTab | undefined = tab === 'preview'
    ? (preview ? 'preview' : tabs[0]?.id)
    : tabs.some((entry) => entry.id === tab) ? tab : tabs[0]?.id ?? (preview ? 'preview' : undefined);
  // A view dragged from the panel lands here, closed or open (DOCK1e).
  const drop = useViewDrop('right', onMove);
  // Whether the unselected tabs are their icons (TABS1). What the names need changes with the names, the
  // preview's name, whether the preview is shown (only then is it capped), and the buttons beside them.
  const fit = useTabFit([
    ...tabs.map((entry) => entry.label), preview?.name ?? '', shown === 'preview', Boolean(onFull) && !autoFull,
  ].join('\n'));

  if (mode === 'closed') {
    return (
      <aside
        aria-label={t('work.dock.label')}
        data-region="side"
        className="relative flex shrink-0 flex-col items-center gap-0.5 border-l border-line py-1.5"
        style={{ width }}
        {...drop.props}
      >
        {drop.over && <DropMark />}
        {/* Emptied, it is drawn only while a view is dragged to it (DOCK1e), and says what it is by
            the side bar's own picture rather than as a blank column at the window's edge. */}
        {tabs.length === 0 && !preview && <Icon name="layoutRight" size={14} className="mt-1.5 text-ink-faint" aria-hidden />}
        {tabs.map(({ id, label, icon }) => (
          <Tip key={id} content={t('work.dock.open', { tab: label })} side="left">
            <Button
              variant="ghost"
              aria-label={t('work.dock.open', { tab: label })}
              onClick={() => onOpen(id)}
              className="h-7 w-7 justify-center px-0"
              {...(onMove ? dragProps(id, 'right', onDrag) : {})}
            >
              <Icon name={icon} size={14} />
            </Button>
          </Tip>
        ))}
        {preview && (
          <Tip content={t('work.dock.open', { tab: t('work.preview.tab', { name: preview.path }) })} side="left">
            <Button
              variant="ghost"
              aria-label={t('work.dock.open', { tab: t('work.preview.tab', { name: preview.name }) })}
              onClick={() => onOpen('preview')}
              className="h-7 w-7 justify-center px-0"
            >
              <Icon name="read" size={14} />
            </Button>
          </Tip>
        )}
      </aside>
    );
  }

  const full = mode === 'full';
  const tabId = (id: DockTab) => `dock-tab-${id}`;

  return (
    <aside
      aria-label={t('work.dock.label')}
      data-region="side"
      data-mode={mode}
      className={cn(
        'flex flex-col bg-page',
        full ? 'absolute inset-0 z-20' : 'relative shrink-0 border-l border-line',
      )}
      style={full ? undefined : { width }}
      {...drop.props}
    >
      {drop.over && <DropMark />}
      {!full && range && onResize && (
        <Splitter
          label={t('work.dock.resize')}
          value={width}
          min={range.min}
          max={range.max}
          edge="left"
          onChange={onResize}
          onReset={onResetWidth}
        />
      )}

      {/* 🔴 A tab shows its whole name or its icon, never a name cut (TABS1). With a third tab, Ask
          Daoris, the close was clipped off a dock at its floor and then a scrollbar ran under the names
          (DOCK1c), so the tabs give way before the dock's own buttons do. They gave way by shrinking, each
          cut with an ellipsis, until a 430px side bar showed 时间线 as 时…: cutting a short name buys
          nothing. So the selected tab keeps its whole name, as a browser's active tab keeps its width, and
          the others keep theirs while the row holds every one; when it does not, each is its icon alone
          (`useTabFit`, measured). Only when the icons do not fit does the row scroll. Every name stays its
          tab's label and its tip's, and the list at the row's end (DOCK1b) names them all, as Chrome's
          does, so an icon is never the only way to a tab. */}
      <div ref={fit.row} data-fit={fit.compact ? 'icons' : 'names'} className="flex shrink-0 items-center border-b border-line pr-1">
        <div role="tablist" aria-label={t('work.dock.label')} className="flex min-w-0 flex-1 overflow-x-auto [scrollbar-width:none]">
          {tabs.map(({ id, label, icon }) => (
            <Tip key={id} content={label}>
              <button
                id={tabId(id)}
                type="button"
                role="tab"
                aria-label={label}
                aria-selected={shown === id}
                onClick={() => onTab(id)}
                // Dragged to the panel, it moves there (DOCK1e); the menu below is the same move by keys.
                {...(onMove ? dragProps(id, 'right', onDrag) : {})}
                // VS Code's tab menu: a right-click selects the tab and offers where it can go.
                onContextMenu={(event) => {
                  event.preventDefault();
                  onTab(id);
                  setMenu(true);
                }}
                className={cn(
                  // One line: a tab's name that wraps reads as two tabs (Ask Daoris, found looking at it).
                  'flex shrink-0 items-center gap-1.5 whitespace-nowrap border-b-2 px-3 py-1.5 text-small transition-colors duration-(--speed)',
                  shown === id ? 'border-b-accent text-ink' : 'border-b-transparent text-ink-faint hover:text-ink',
                )}
              >
                <Icon name={icon} size={14} className="shrink-0" />
                {(shown === id || !fit.compact) && <span>{label}</span>}
              </button>
            </Tip>
          ))}
          {/* A file's preview (PREVIEW1): after the views and by the same rule, with its own × — a
              sibling of the tab, since a button inside a button is neither. Shown, it is capped and its
              name may be cut, because a file's name is long and the tip carries its path. */}
          {preview && (
            <div
              className={cn(
                'flex shrink-0 items-center border-b-2 transition-colors duration-(--speed)',
                shown === 'preview' ? 'max-w-52 border-b-accent text-ink' : 'border-b-transparent text-ink-faint hover:text-ink',
              )}
            >
              <Tip content={t('work.preview.tab', { name: preview.path })}>
                <button
                  id={tabId('preview')}
                  type="button"
                  role="tab"
                  aria-label={t('work.preview.tab', { name: preview.name })}
                  aria-selected={shown === 'preview'}
                  onClick={() => onTab('preview')}
                  className="flex min-w-0 items-center gap-1.5 whitespace-nowrap py-1.5 pl-3 pr-1 text-small"
                >
                  <Icon name="read" size={14} className="shrink-0" />
                  {shown === 'preview'
                    ? <span className="min-w-0 truncate">{preview.name}</span>
                    : !fit.compact && <span>{preview.name}</span>}
                </button>
              </Tip>
              <Tip content={t('work.preview.close')}>
                <button
                  type="button"
                  aria-label={t('work.preview.close')}
                  onClick={preview.onClose}
                  className="mr-1 flex h-5 w-5 shrink-0 items-center justify-center rounded-control text-ink-faint hover:bg-raised hover:text-ink"
                >
                  <Icon name="x" size={12} />
                </button>
              </Tip>
            </div>
          )}
        </div>

        <div className="ml-auto flex shrink-0 items-center gap-0.5">
          <ViewsMenu
            region="right"
            views={tabs}
            selected={shown === 'preview' ? undefined : shown}
            onSelect={onTab}
            onMove={onMove}
            onReset={onReset}
            open={menu}
            onOpenChange={setMenu}
          />
          {onFull && !autoFull && (
            <Tip content={t(full ? 'work.dock.unfull' : 'work.dock.full')}>
              <Button
                variant="ghost"
                aria-label={t(full ? 'work.dock.unfull' : 'work.dock.full')}
                onClick={() => onFull(!full)}
                className="h-6 w-6 justify-center px-0"
              >
                <Icon name={full ? 'unfull' : 'full'} size={13} />
              </Button>
            </Tip>
          )}
          <Tip content={t('work.dock.close')}>
            <Button
              variant="ghost"
              aria-label={t('work.dock.close')}
              onClick={onClose}
              className="h-6 w-6 justify-center px-0"
            >
              <Icon name="dockClose" size={13} />
            </Button>
          </Tip>
        </div>
      </div>

      {mode === 'cramped' && (
        <p role="status" className="m-0 border-b border-line px-3 py-1.5 text-small text-ink-soft">
          {t('work.dock.cramped')}
        </p>
      )}

      {shown
        ? (
          <div role="tabpanel" aria-labelledby={tabId(shown)} className="flex min-h-0 flex-1 flex-col overflow-y-auto">
            {children}
          </div>
        )
        // Every view moved out: VS Code's empty container, which says how to fill it.
        : <p className="m-0 p-3 text-small text-ink-faint">{t('work.views.empty')}</p>}
    </aside>
  );
}
