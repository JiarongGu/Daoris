import { useTranslation } from 'react-i18next';
import * as Menu from '@radix-ui/react-dropdown-menu';
import { Icon, type IconName, Tip } from '../ui';
import { cn } from '../lib/cn';
import type { Place, ViewId } from './placements';

/** A view as its region's tab names it. */
export type ViewEntry = { id: ViewId; label: string; icon: IconName };

const VIEW_NAME: Record<ViewId, string> = {
  timeline: 'work.review.timelineTab', review: 'work.review.tab', ask: 'help.title', console: 'work.views.console',
};
const VIEW_ICON: Record<ViewId, IconName> = { timeline: 'quests', review: 'diff', ask: 'help', console: 'frameWork' };

/** Where a dragged view would land, lit as VS Code lights a container a tab is over (DOCK1e). */
export function DropMark() {
  return <div aria-hidden className="pointer-events-none absolute inset-0 z-30 border-2 border-accent bg-accent-soft opacity-70" />;
}

/** The views, named and pictured as every region's tabs show them: one name wherever a view stands. */
export function viewEntries(t: (key: string) => string, views: readonly ViewId[]): ViewEntry[] {
  return views.map((id) => ({ id, label: t(VIEW_NAME[id]), icon: VIEW_ICON[id] }));
}

const ITEM = cn(
  'flex cursor-pointer items-center gap-2 rounded-[4px] px-2 py-1.5 text-small outline-none',
  'data-[highlighted]:bg-raised data-[highlighted]:text-ink',
);

/**
 * The button at the end of a region's tab row (DOCK1b): every view the region holds, and where the
 * selected one can go.
 *
 * @remarks
 * **Chrome's tab list, for tabs that do not fit** (the owner, 2026-09-29: *"for long tabs you can see
 * take design from vscode or browser tab design"*, and then *"display only icon does not fix the
 * limitation"*). The tabs shrink as a browser's do, and however narrow the region gets, this lists
 * every one by its whole name, so none is ever out of reach.
 *
 * **VS Code's *Move to*, on the same button**, and on a right-click of a tab, which selects the tab
 * first so the move names it. The views move between the right side bar and the panel; *Reset view
 * locations* puts them back, here once anything has moved and in the View menu always.
 *
 * Controlled when `open` is handed in, so a right-click on a tab can open it.
 */
export function ViewsMenu({
  region, views, selected, onSelect, onMove, onReset, open, onOpenChange,
}: {
  /** Which region's views it lists. */
  region: Place;
  views: ViewEntry[];
  selected?: ViewId;
  onSelect: (view: ViewId) => void;
  /** Sends a view to the other region. */
  onMove?: (view: ViewId, to: Place) => void;
  /** Puts every view back where it started; absent while none has moved. */
  onReset?: () => void;
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const label = t(region === 'right' ? 'work.views.right' : 'work.views.panel');
  const other: Place = region === 'right' ? 'panel' : 'right';
  const chosen = views.find((view) => view.id === selected);

  return (
    <Menu.Root modal={false} open={open} onOpenChange={onOpenChange}>
      <Tip content={label}>
        <Menu.Trigger asChild>
          <button
            type="button"
            aria-label={label}
            className={cn(
              'flex h-6 w-6 shrink-0 items-center justify-center rounded-control text-ink-faint',
              'transition-colors duration-(--speed) hover:bg-raised hover:text-ink data-[state=open]:bg-raised data-[state=open]:text-ink',
              'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent',
            )}
          >
            <Icon name="chevronDown" size={13} />
          </button>
        </Menu.Trigger>
      </Tip>

      <Menu.Portal>
        <Menu.Content
          align="end"
          sideOffset={4}
          collisionPadding={8}
          className="z-30 min-w-56 max-w-80 rounded-control border border-line bg-overlay p-1 shadow-lg"
        >
          {views.map((view) => (
            <Menu.Item
              key={view.id}
              onSelect={() => onSelect(view.id)}
              className={cn(ITEM, view.id === selected ? 'text-ink' : 'text-ink-soft')}
            >
              <span className="flex w-3.5 shrink-0 justify-center">
                {view.id === selected && <Icon name="check" size={12} aria-hidden />}
              </span>
              <Icon name={view.icon} size={13} className="shrink-0 opacity-70" aria-hidden />
              <span className="truncate">{view.label}</span>
            </Menu.Item>
          ))}

          {(chosen && onMove) || onReset ? <Menu.Separator className="my-1 h-px bg-line" /> : null}
          {chosen && onMove && (
            <Menu.Item onSelect={() => onMove(chosen.id, other)} className={cn(ITEM, 'text-ink-soft')}>
              <span className="w-3.5 shrink-0" />
              <Icon name={other === 'right' ? 'layoutRight' : 'layoutPanel'} size={13} className="shrink-0 opacity-70" aria-hidden />
              <span className="truncate">
                {t(other === 'right' ? 'work.views.toRight' : 'work.views.toPanel', { view: chosen.label })}
              </span>
            </Menu.Item>
          )}
          {onReset && (
            <Menu.Item onSelect={onReset} className={cn(ITEM, 'text-ink-soft')}>
              <span className="w-3.5 shrink-0" />
              <Icon name="refresh" size={13} className="shrink-0 opacity-70" aria-hidden />
              <span className="truncate">{t('work.views.reset')}</span>
            </Menu.Item>
          )}
        </Menu.Content>
      </Menu.Portal>
    </Menu.Root>
  );
}
