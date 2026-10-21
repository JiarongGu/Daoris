import { useTranslation } from 'react-i18next';
import type { AttentionRead } from '../bridge/attention';
import type { Notice } from '../bridge/notices';
import { Icon, SectionTitle, SkeletonRows } from '../design/ui';
import { cn } from '../lib/cn';
import { elapsed, span } from '../locales/format';
import type { NotificationsGroup } from '../notifications/NotificationsView';
import type { Confirmation } from '../person/Confirmations';
import { NothingNeedsYou } from '../work/attention/AttentionList';
import { attentionGroup, type AttentionGroup, GROUP_ORDER, needsListed } from '../work/attention/attention';

/** A line of the summary: its group's name, its count, and its oldest wait or its press's time left, where one is known. */
type Line = { id: 'presses' | AttentionGroup | 'done'; label: string; count: number; detail: string | null; opens: NotificationsGroup };

/**
 * Overview's summary of what needs the person (NOTIFY2b, D196 point 5, the notification design §4): a line per group with its count
 * and its oldest wait (the presses waiting for confirmation, *Holding work*, *Waiting for your word*, *Ready for you*), each a door
 * that shows *Notifications* at its group. Overview lists no row: every one, with its words and acts, is in *Notifications*.
 *
 * @remarks
 * **Counted by group, never the bell's total** (§4): the presses by the confirmation channel, each once, and the rows by
 * `attentionGroup` as .NET judged them (a hand-over ask whose session stands still with what holds work, a stale go-ahead with what
 * waits for the person's word), a press's own row being its card. The presses' line says the time left of the one that
 * ends first (R2), the others their oldest wait. **R1**: the lines share one label track while the summary is 32rem wide or more,
 * each value beside its words, and stack below it. With nothing waiting, today's one line, *Nothing needs you* and the sessions
 * working; not judged yet, its skeleton (PUSHUX1); in a plain browser, that this is the app's (D47 §4).
 *
 * A molecule: what waits comes in, and the doors go out.
 */
export function NeedsSummary({ attention, presses, notices = [], onNotifications, now = Date.now() }: {
  attention: AttentionRead;
  presses: Confirmation[];
  /** *Done and failed*, the window's notices (NOTIFY2f): their line, after what waits, with how many are unread. */
  notices?: Notice[];
  /** *Notifications* shown at a group: the app's alone, since a browser has no side bar. Absent, the lines are words. */
  onNotifications?: (group: NotificationsGroup) => void;
  /** The clock the times are read by. */
  now?: number;
}) {
  const { t } = useTranslation();
  // Read again whole (PUSHUX1): the last lines held, dimmed, until it lands, as the band held its rows.
  const resyncing = attention.state === 'known' && attention.resyncing;
  const region = (body: React.ReactNode) => (
    <section
      aria-label={t('work.attention.title')}
      aria-busy={resyncing || undefined}
      className={cn('mb-5 grid gap-2', resyncing && 'opacity-60 transition-opacity duration-(--speed)')}
    >
      <SectionTitle>{t('work.attention.title')}</SectionTitle>
      {body}
    </section>
  );

  if (attention.state === 'app') return region(<p className="m-0 text-body text-ink-soft">{t('work.attention.needsApp')}</p>);
  if (attention.state === 'reading') return region(<SkeletonRows />);

  const rows = needsListed(attention.rows);
  const lines: Line[] = [];
  if (presses.length > 0) {
    const first = Math.min(...presses.map((each) => new Date(each.expires).getTime()));
    lines.push({
      id: 'presses', label: t('person.label'), count: presses.length, opens: 'presses',
      detail: t('person.left', { time: span(Math.max(0, first - now)) }),
    });
  }
  for (const group of GROUP_ORDER) {
    const held = rows.filter((row) => attentionGroup(row) === group);
    if (held.length === 0) continue;
    const since = held.map((row) => row.since).filter((each): each is string => Boolean(each)).sort()[0];
    lines.push({
      id: group, label: t(`work.attention.group.${group}`), count: held.length, opens: 'needs',
      detail: since ? t('overview.needs.oldest', { age: elapsed(since, new Date(now).toISOString()) }) : null,
    });
  }

  // What ended waits on nobody: its line follows the waits, counting every notice, its detail how many are unread.
  const unread = notices.filter((notice) => !notice.read).length;
  const done: Line | null = notices.length > 0
    ? {
      id: 'done', label: t('notifications.done.title'), count: notices.length, opens: 'done',
      detail: unread > 0 ? t('notifications.done.unreadCount', { count: unread }) : null,
    }
    : null;
  if (lines.length === 0 && !done) return region(<NothingNeedsYou working={attention.working} />);
  if (done) lines.push(done);

  return region(
    // R1 (the UI/UX design §2): one label track at 32rem and wider, each line's count and wait beside its words; stacked below.
    // Lines, not a list of rows: every row is *Notifications*' (§4), so nothing here reads as one.
    <div className="@container grid grid-cols-[fit-content(min(22rem,50%))_auto_minmax(0,1fr)_auto] gap-x-4 @max-[32rem]:grid-cols-[minmax(0,1fr)_auto]">
      {lines.map((line) => {
        const cells = (
          <>
            <span className="min-w-0 text-body text-ink">{line.label}</span>
            <span className={cn('font-mono text-meta tabular-nums', line.id === 'presses' ? 'text-warn' : 'text-ink-open')}>{line.count}</span>
            <span className={cn('min-w-0 text-small text-ink-faint @max-[32rem]:col-span-2', line.id === 'presses' && 'text-warn')}>
              {line.detail}
            </span>
          </>
        );
        const grid = 'col-span-full grid grid-cols-subgrid items-baseline gap-y-0.5 border-t border-line py-1.5 first:border-t-0 @max-[32rem]:col-span-2';
        return (
          <div key={line.id} data-summary-line={line.id} className={grid}>
            {onNotifications
              ? (
                <button
                  type="button"
                  title={t('overview.needs.door')}
                  aria-description={t('overview.needs.door')}
                  onClick={() => onNotifications(line.opens)}
                  className="col-span-full grid cursor-pointer grid-cols-subgrid items-baseline border-0 bg-transparent p-0 text-left hover:bg-accent-soft @max-[32rem]:col-span-2"
                >
                  {cells}
                  <Icon name="chevronRight" size={13} className="text-ink-faint @max-[32rem]:hidden" />
                </button>
              )
              : cells}
          </div>
        );
      })}
    </div>,
  );
}
