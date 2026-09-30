import { useTranslation } from 'react-i18next';
import { type ReactNode, useState } from 'react';
import { Button, Icon, Tip } from '../ui';
import { cn } from '../lib/cn';
import { Splitter } from './frame';
import type { DockMode } from './layout';
import type { Place, ViewId } from './placements';
import { DropMark, viewEntries, ViewsMenu } from './ViewsMenu';
import { dragProps, useViewDrop } from './viewDrag';

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

  if (mode === 'closed') {
    return (
      <aside
        aria-label={t('work.dock.label')}
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

      {/* 🔴 The tabs give way before the dock's own buttons do (found looking at DOCK1c): with a third
          tab, Ask Daoris, the close was clipped off a dock at its floor, and then a scrollbar ran under the
          names. So they shrink as a browser's tabs and VS Code's do: the selected tab keeps its whole name, as a browser's active tab keeps its
          width, and the others give way, each cut with an ellipsis down to its icon; only then do they
          scroll. Every full name is its tab's own and its tip's, and the list at the row's end (DOCK1b)
          names them all, as Chrome's does, so a tab cut to its icon is never the only way to one. */}
      <div className="flex shrink-0 items-center border-b border-line pr-1">
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
                  'flex max-w-44 items-center gap-1.5 whitespace-nowrap border-b-2 px-3 py-1.5 text-small transition-colors duration-(--speed)',
                  shown === id
                    ? 'shrink-0 border-b-accent text-ink'
                    : 'min-w-9 shrink border-b-transparent text-ink-faint hover:text-ink',
                )}
              >
                <Icon name={icon} size={14} className="shrink-0" />
                <span className="min-w-0 truncate">{label}</span>
              </button>
            </Tip>
          ))}
          {/* A file's preview (PREVIEW1): after the views, shrinking as they do, with its own × — a
              sibling of the tab, since a button inside a button is neither. */}
          {preview && (
            <div
              className={cn(
                'flex max-w-52 items-center border-b-2 transition-colors duration-(--speed)',
                shown === 'preview'
                  ? 'shrink-0 border-b-accent text-ink'
                  : 'min-w-9 shrink border-b-transparent text-ink-faint hover:text-ink',
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
                  <span className="min-w-0 truncate">{preview.name}</span>
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
