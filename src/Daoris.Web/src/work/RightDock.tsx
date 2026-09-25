import { useTranslation } from 'react-i18next';
import type { ReactNode } from 'react';
import { Button, Icon, type IconName, Tip } from '../ui';
import { cn } from '../lib/cn';
import { Splitter } from './frame';
import type { DockMode } from './layout';

// The frame's third column (components plan §3a). It was deliberately unbuilt until it had a SECOND
// occupant — a dock holding one thing is a pane with extra chrome — and SURF6's diff is that
// occupant, so the timeline moves here and the attended column gets its height back.

export type DockTab = 'timeline' | 'review';

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
 */
export function RightDock({
  tab, onTab, mode, width, range, autoFull = false, onResize, onResetWidth, onClose, onOpen, onFull, children,
}: {
  tab: DockTab;
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

  const tabs: { id: DockTab; label: string; icon: IconName }[] = [
    { id: 'timeline', label: t('work.review.timelineTab'), icon: 'quests' },
    { id: 'review', label: t('work.review.tab'), icon: 'diff' },
  ];

  if (mode === 'closed') {
    return (
      <aside
        aria-label={t('work.dock.label')}
        className="flex shrink-0 flex-col items-center gap-0.5 border-l border-line py-1.5"
        style={{ width }}
      >
        {tabs.map(({ id, label, icon }) => (
          <Tip key={id} content={t('work.dock.open', { tab: label })} side="left">
            <Button
              variant="ghost"
              aria-label={t('work.dock.open', { tab: label })}
              onClick={() => onOpen(id)}
              className="h-7 w-7 justify-center px-0"
            >
              <Icon name={icon} size={14} />
            </Button>
          </Tip>
        ))}
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
    >
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

      <div className="flex shrink-0 items-center border-b border-line pr-1">
        <div role="tablist" aria-label={t('work.dock.label')} className="flex">
          {tabs.map(({ id, label, icon }) => (
            <button
              key={id}
              id={tabId(id)}
              type="button"
              role="tab"
              aria-selected={tab === id}
              onClick={() => onTab(id)}
              className={cn(
                'flex items-center gap-1.5 border-b-2 px-3 py-1.5 text-small transition-colors duration-(--speed)',
                tab === id
                  ? 'border-b-accent text-ink'
                  : 'border-b-transparent text-ink-faint hover:text-ink',
              )}
            >
              <Icon name={icon} size={14} />
              {label}
            </button>
          ))}
        </div>

        <div className="ml-auto flex items-center gap-0.5">
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

      <div role="tabpanel" aria-labelledby={tabId(tab)} className="flex min-h-0 flex-1 flex-col overflow-y-auto">
        {children}
      </div>
    </aside>
  );
}
