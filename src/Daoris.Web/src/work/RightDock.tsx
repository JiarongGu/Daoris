import { useTranslation } from 'react-i18next';
import type { ReactNode } from 'react';
import { Icon, type IconName } from '../ui';
import { cn } from '../lib/cn';

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
 */
export function RightDock({ tab, onTab, children }: {
  tab: DockTab;
  onTab: (tab: DockTab) => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();

  const tabs: { id: DockTab; label: string; icon: IconName }[] = [
    { id: 'timeline', label: t('work.review.timelineTab'), icon: 'quests' },
    { id: 'review', label: t('work.review.tab'), icon: 'diff' },
  ];

  return (
    <aside className="flex w-[26rem] shrink-0 flex-col border-l border-line max-xl:w-[22rem] max-lg:hidden">
      <div role="tablist" aria-label={t('work.dock.label')} className="flex shrink-0 border-b border-line">
        {tabs.map(({ id, label, icon }) => (
          <button
            key={id}
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

      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">{children}</div>
    </aside>
  );
}
