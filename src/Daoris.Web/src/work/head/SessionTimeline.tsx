import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../../data/api';
import { ago } from '../../locales/format';
import { cn } from '../../lib/cn';
import { Button, SectionTitle, type ShownState } from '../../design/ui';
import { Note } from '../Note';
import { linesTaken, noteBlocks, noteLines } from '../noteLines';
import { SessionFlow } from './SessionFlow';
import { type TimelineEvent, sessionTimeline } from './timeline';

/**
 * How many lines a note shows before it folds, and about how many letters the side bar's line holds
 * at its usual width: the install's side bar showed an ended session's whole verify report past the window's foot.
 */
const NOTE_LINES = 4;
const MEASURE = 56;

/** The mark's hue per kind — an arrangement of existing tokens, never a new colour. */
const MARK: Record<TimelineEvent['kind'], string> = {
  opened: 'border-line bg-raised',
  state: 'border-accent bg-accent-soft',
  quest: 'border-st-taken bg-st-taken/20',
  evidence: 'border-st-done bg-st-done/20',
};

/**
 * One observed event, beside the stream and never inside it.
 *
 * @remarks
 * A molecule: it is handed an event and renders it, so every kind — including the ones a real
 * session produces once a week — is reachable in a story.
 *
 * **A session's note is `Note`'s**: Daoris's lines worded by their codes in the reader's language,
 * someone's words as written, a record from before as kept, marked. An evidence bundle is a sentence the
 * driving machine wrote and renders verbatim, and a quest's note is its closer's words.
 *
 * **A note longer than four lines folds to them**, with *Show all*: a long note is someone's words or
 * the driver's account of a run, and the conversation and the record hold it whole.
 */
export function TimelineEntry({ event }: { event: TimelineEvent }) {
  const { t, i18n } = useTranslation();
  const [whole, setWhole] = useState(false);

  const label = event.kind === 'opened' ? t('work.timeline.opened')
    : event.kind === 'state' ? t('work.timeline.state', { state: t(`sessionState.${event.state}`) })
      : event.kind === 'quest' ? t('work.timeline.quest', { status: t(`status.${event.status}`) })
        : t('work.timeline.evidence');

  // A quest's note is its closer's words, shown as written; a session's is its record's, Daoris's lines worded.
  // A quest's note with parts is drawn by `Note` as well, so the person's done reads in the reader's language.
  const coded = event.kind === 'state' || (event.kind === 'quest' && !!event.noteParts?.length);
  const note = event.kind === 'quest' && !coded ? event.note : null;
  // Long is judged on what is shown: the note's blocks as `Note` draws them, or the closer's words.
  const shown = coded && (event.kind === 'state' || event.kind === 'quest')
    ? noteBlocks(t, noteLines(t, { parts: event.noteParts }, i18n.language)).map((block) => block.text)
    : note ? [note] : [];
  const long = linesTaken(shown, MEASURE) > NOTE_LINES;
  const folded = long && !whole;

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
        <p className={cn('m-0 mt-0.5 whitespace-pre-wrap text-small italic text-ink-soft', folded && 'line-clamp-4')}>{note}</p>
      )}
      {coded && (event.kind === 'state' || event.kind === 'quest') && (
        <Note
          parts={event.noteParts}
          clamp={folded ? NOTE_LINES : undefined}
          className="mt-0.5 text-small italic text-ink-soft"
        />
      )}
      {long && (
        <Button
          variant="ghost"
          aria-expanded={whole}
          className="mt-0.5 px-0 py-0 text-small"
          onClick={() => setWhole((was) => !was)}
        >
          {t(whole ? 'work.timeline.noteLess' : 'work.timeline.noteMore')}
        </Button>
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
 * **A molecule.** `sessionTimeline` needs only the record
 * and its quest, both of which the attended session already holds. The dependency rule decides the
 * layer — so this takes props, stays inside the presentational boundary, and every
 * shape of timeline is reachable in a story.
 */
export function SessionTimeline({ session, quest, shown, hideCurrentNote = false, titled = true }: {
  session: Session;
  quest?: Quest | null;
  /**
   * The word the page shows the session as (*idle*, *parked*, *awaiting reply*, *answered*, *going on*), which the states'
   * flow lights its current state with. Absent, the record's own reading.
   */
  shown?: ShownState;
  /**
   * Leave the note off the entry for the state the session is in NOW, because something above is
   * already showing it — a parked session's analysis, which design §4 puts at the top of the head.
   * The event keeps its note; only this rendering drops it, so the derivation stays complete.
   */
  hideCurrentNote?: boolean;
  /**
   * Whether it carries its own heading. The dock's tab names it, so the pane does not say *Timeline* again
   * under the *Timeline* tab; in the column, with no tab above it, the heading stays.
   */
  titled?: boolean;
}) {
  const { t } = useTranslation();
  const events = sessionTimeline(session, quest).map((event) =>
    (hideCurrentNote && event.kind === 'state' ? { ...event, note: null, noteParts: null } : event));

  return (
    <section>
      {/* Level 3: the attended session's head is the region's h2, and this sits under it. */}
      {titled && (
        <SectionTitle level={3}>
          <span title={t('work.timeline.hint')}>{t('work.timeline.title')}</span>
        </SectionTitle>
      )}
      {/* The session's states at its head: what the record says it is, above what is observed. */}
      <SessionFlow session={session} shown={shown} className="mb-3" />
      <ol className="m-0 list-none p-0">
        {events.map((event, index) => (
          <TimelineEntry key={`${event.kind}-${index}`} event={event} />
        ))}
      </ol>
    </section>
  );
}
