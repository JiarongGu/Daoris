import { useTranslation } from 'react-i18next';
import { Icon, type IconName, Menu, Tip } from '../ui';
import { cn } from '../lib/cn';
import type { Place, ViewId } from './placements';

/** A view as its region's tab names it. */
export type ViewEntry = { id: ViewId; label: string; icon: IconName };

const VIEW_NAME: Record<ViewId, string> = {
  timeline: 'work.review.timelineTab', review: 'work.review.tab', ask: 'help.title', console: 'work.views.console',
  terminal: 'work.views.terminal',
};
const VIEW_ICON: Record<ViewId, IconName> = {
  timeline: 'quests', review: 'diff', ask: 'help', console: 'frameWork', terminal: 'terminal',
};

/** Where a dragged view would land, lit as VS Code lights a container a tab is over (DOCK1e). */
export function DropMark() {
  return <div aria-hidden className="pointer-events-none absolute inset-0 z-30 border-2 border-accent bg-accent-soft opacity-70" />;
}

/** The views, named and pictured as every region's tabs show them: one name wherever a view stands. */
export function viewEntries(t: (key: string) => string, views: readonly ViewId[]): ViewEntry[] {
  return views.map((id) => ({ id, label: t(VIEW_NAME[id]), icon: VIEW_ICON[id] }));
}

/**
 * The button at the end of a region's tab row (DOCK1b): every view the region holds, and where the
 * selected one can go.
 *
 * @remarks
 * **Chrome's tab list, for tabs that do not fit**: a tab drawn as its icon alone (TABS1) is named only by
 * its tip, and once even the icons scroll some are out of sight. However narrow the region gets, this
 * lists every one by its whole name, so none is ever out of reach.
 *
 * **VS Code's *Move to*, on the same button**, and on a right-click of a tab, which selects the tab
 * first so the move names it. The views move between the right side bar and the panel; *Reset view
 * locations* puts them back, here once anything has moved and in the View menu always.
 *
 * Controlled when `open` is handed in, so a right-click on a tab can open it.
 *
 * **The shown view is said, not only ticked** (UXFIX1b): it is one choice among the region's, so the views are radio
 * rows, where a plain item's drawn tick told a screen reader nothing; the moves and the reset stay acts.
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
    <Menu.Root open={open} onOpenChange={onOpenChange}>
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
            {/* VS Code's *Views and More Actions*: a menu, not a direction, so not an arrow. */}
            <Icon name="more" size={14} />
          </button>
        </Menu.Trigger>
      </Tip>

      <Menu.Content align="end" className="min-w-56 max-w-80">
        {/* One group named for the region, the shown view `aria-checked`. A row chooses through `onSelect`, the shown
            one too, as it did as a plain item. */}
        <Menu.RadioGroup aria-label={label} value={selected ?? ''}>
          {views.map((view) => (
            <Menu.RadioItem key={view.id} value={view.id} onSelect={() => onSelect(view.id)}>
              <Icon name={view.icon} size={13} className="shrink-0 opacity-70" />
              <span className="truncate">{view.label}</span>
            </Menu.RadioItem>
          ))}
        </Menu.RadioGroup>

        {(chosen && onMove) || onReset ? <Menu.Separator /> : null}
        {chosen && onMove && (
          <Menu.Item tick={false} onSelect={() => onMove(chosen.id, other)}>
            <Icon name={other === 'right' ? 'layoutRight' : 'layoutPanel'} size={13} className="shrink-0 opacity-70" />
            <span className="truncate">
              {t(other === 'right' ? 'work.views.toRight' : 'work.views.toPanel', { view: chosen.label })}
            </span>
          </Menu.Item>
        )}
        {onReset && (
          <Menu.Item tick={false} onSelect={onReset}>
            <Icon name="refresh" size={13} className="shrink-0 opacity-70" />
            <span className="truncate">{t('work.views.reset')}</span>
          </Menu.Item>
        )}
      </Menu.Content>
    </Menu.Root>
  );
}
