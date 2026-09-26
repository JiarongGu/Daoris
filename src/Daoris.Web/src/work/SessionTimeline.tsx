import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { ago } from '../format';
import { SectionTitle } from '../ui';
import { type TimelineEvent, sessionTimeline } from './timeline';

/** The mark's hue per kind — an arrangement of existing tokens (D41), never a new colour. */
const MARK: Record<TimelineEvent['kind'], string> = {
  opened: 'border-line bg-raised',
  state: 'border-accent bg-accent-soft',
  quest: 'border-st-taken bg-st-taken/20',
  evidence: 'border-st-done bg-st-done/20',
};

/**
 * One observed event, beside the stream and never inside it (design §3).
 *
 * @remarks
 * A molecule: it is handed an event and renders it, so every kind — including the ones a real
 * session produces once a week — is reachable in a story.
 *
 * **What the driver said renders verbatim.** A note is an observation and an evidence bundle is a
 * sentence the driving machine wrote; the chrome around them speaks the active language and they
 * do not, which is where the platform's i18n boundary sits.
 */
export function TimelineEntry({ event }: { event: TimelineEvent }) {
  const { t } = useTranslation();

  const label = event.kind === 'opened' ? t('work.timeline.opened')
    : event.kind === 'state' ? t('work.timeline.state', { state: t(`sessionState.${event.state}`) })
      : event.kind === 'quest' ? t('work.timeline.quest', { status: t(`status.${event.status}`) })
        : t('work.timeline.evidence');

  const note = event.kind === 'state' || event.kind === 'quest' ? event.note : null;

  return (
    <li className="relative pb-3 pl-5 last:pb-0">
      {/* The rail behind the marks, stopping at the last one rather than trailing into nothing. */}
      <span aria-hidden className="absolute bottom-0 left-[3px] top-3 w-px bg-line last:hidden" />
      <span aria-hidden className={`absolute left-0 top-1.5 size-[7px] rounded-full border ${MARK[event.kind]}`} />

      <p className="m-0 flex flex-wrap items-baseline gap-x-2 text-small">
        <span className="text-ink">{label}</span>
        <span className="font-mono text-meta text-ink-faint">{ago(event.at)}</span>
      </p>

      {note && (
        <p className="m-0 mt-0.5 whitespace-pre-wrap text-small italic text-ink-soft">{note}</p>
      )}

      {event.kind === 'evidence' && (
        <>
          {event.text && (
            <p className="m-0 mt-0.5 whitespace-pre-wrap text-small text-ink-soft">{event.text}</p>
          )}
          {event.commits.length > 0 && (
            <ul className="m-0 mt-1 list-none p-0">
              {event.commits.map((commit) => (
                <li key={commit.sha} className="flex gap-2 text-small">
                  <code className="shrink-0 font-mono text-meta text-accent">{commit.sha}</code>
                  <span className="min-w-0 break-words text-ink-soft">{commit.subject}</span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </li>
  );
}

/**
 * The observed audit layer for one session.
 *
 * @remarks
 * **A molecule, not the organism the plan filed it as.** The components plan put this under
 * organisms before the derivation was known; `sessionTimeline` turned out to need only the record
 * and its quest, both of which the attended session already holds. The dependency rule decides the
 * layer, not the table — so this takes props, stays inside the presentational boundary, and every
 * shape of timeline is reachable in a story. The plan is corrected rather than quietly satisfied.
 */
export function SessionTimeline({ session, quest, hideCurrentNote = false, titled = true }: {
  session: Session;
  quest?: Quest | null;
  /**
   * Leave the note off the entry for the state the session is in NOW, because something above is
   * already showing it — a parked session's analysis, which design §4 puts at the top of the head.
   * The event keeps its note; only this rendering drops it, so the derivation stays complete.
   */
  hideCurrentNote?: boolean;
  /**
   * Whether it carries its own heading. The dock's tab names it, and the pane said *Timeline* again
   * under the *Timeline* tab (UX5 U65); in the column, with no tab above it, the heading stays.
   */
  titled?: boolean;
}) {
  const { t } = useTranslation();
  const events = sessionTimeline(session, quest).map((event) =>
    (hideCurrentNote && event.kind === 'state' ? { ...event, note: null } : event));

  return (
    <section>
      {/* Level 3: the attended session's head is the region's h2, and this sits under it. */}
      {titled && (
        <SectionTitle level={3}>
          <span title={t('work.timeline.hint')}>{t('work.timeline.title')}</span>
        </SectionTitle>
      )}
      <ol className="m-0 list-none p-0">
        {events.map((event, index) => (
          <TimelineEntry key={`${event.kind}-${index}`} event={event} />
        ))}
      </ol>
    </section>
  );
}
