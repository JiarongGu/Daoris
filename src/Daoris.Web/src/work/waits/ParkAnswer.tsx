import { useId, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead } from '../../data/api';
import { Button } from '../../design/ui';
import { cn } from '../../lib/cn';
import { elapsed } from '../../locales/format';
import type { WaitEvents } from './anchors';
import type { ParkQuestion } from './parkQuestion';
import { QuestionCard } from './QuestionCard';
import { InConversation, leftFor, type Seek, SOUGHT, useSought } from './WaitAnchor';

/** The kinds of act a go-ahead names that the page words; another is shown as the ask keeps it (as `GoAheadList` does). */
const KINDS = new Set(['write', 'release', 'push', 'sign-in', 'run']);

/**
 * **What a parked session asks, in its box** (PARKVIEW1, the park's ask design §3-§4): above the box's one field, at the foot where
 * the person reads its last message, so it is on screen at every width whatever the head shows.
 *
 * @remarks
 * - **Its question**, the agent's words as written, and each answer it offered as a row to select (`QuestionCard`, QUESTIONUI1a),
 *   sent with the box's own words by the box's *Send*. Recorded by the agent (`question_ask`), never read out of its prose.
 * - **One line over it** (PARKWORDS1): where its question is (here, or above in its last message where it recorded none) and how
 *   long it has waited, then *answer below*.
 * - **Each go-ahead still open** with *Approve* and *Refuse*, the box's words going as its words (GOAHEAD2b). Where a question waits
 *   beside them they come first, said so: an answer chosen or written is refused until they are answered (the frame's guard), and each
 *   go-ahead keeps the park for the question's answer, which then sends it on with them all (`goesOn: false`, the design §4).
 * - **The agent's own *by*** under its question, where it named one.
 * - **Behind a disclosure, folded** (PARKWORDS1): that no deadline runs, **what it holds meanwhile** (its quest taken, its own tree
 *   kept, nothing running) and **what an answer does**, Daoris's facts in its words, there for the person who asks.
 *
 * Bounded by its share of the waiting panel's height (WAITPANEL1; 40% of the window where no panel holds it) and scrolled within,
 * as the strip is, so a long question never pushes the field off the window.
 * A molecule: props in, presses out; the frame holds the words, the selection, the presses' doors and their pending.
 */
export function ParkAnswer({
  quest, tree = null, since, question, goAheads, busy = false, selected = null, onSelect, onGoAhead, seek, toEvent,
}: {
  /**
   * The wait a raising block's *To the wait* leads to here (WAITFOOT1c): its question or one of its go-aheads, outlined and given
   * the focus, as the strip's lines are.
   */
  seek?: Seek | null;
  /** The way from its question and each go-ahead to the event that raised it (WAITFOOT1c): *In the conversation*. */
  toEvent?: WaitEvents;
  /** The quest it holds taken, which it keeps while it waits. */
  quest?: string | null;
  /** Its own tree's name, null in the person's checkout (`ownTree`). */
  tree?: string | null;
  /** When it parked, the record's last move: how long it has waited. */
  since: string;
  /** Its question as the driver kept it for this park, or null where it asked none through the connector. */
  question: ParkQuestion | null;
  /** The go-aheads it asked that are still open, in their order. */
  goAheads: readonly GoAhead[];
  /** A press of this park's is on its way: no go-ahead's is offered again until it is answered. */
  busy?: boolean;
  /** The answer selected to go with the box's words, by its label (QUESTIONUI1a), or null. */
  selected?: string | null;
  /** Select one of its answers, or none; absent, the answers are listed with no radio. */
  onSelect?: (label: string | null) => void;
  /** Approve or refuse a go-ahead still open; absent, they are listed with no press. */
  onGoAhead?: (number: number, approved: boolean) => void;
}) {
  const { t } = useTranslation();
  const heading = useId();
  const section = useRef<HTMLElement>(null);
  const { sought, letGo } = useSought(section, seek);
  // Its question waits for its go-aheads (the design §4): each one answered first keeps the park for this answer, and the frame refuses
  // an answer until they are (QUESTIONUI1a keeps that order for a chosen answer and free words alike).
  const held = question !== null && goAheads.length > 0;

  return (
    <section
      ref={section}
      aria-labelledby={heading}
      data-wait-part=""
      className="grid max-h-[var(--wait-part,40vh)] grid-cols-[minmax(0,1fr)] gap-2 overflow-y-auto"
      onBlur={(event) => { if (leftFor(event)) letGo(); }}
    >
      <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
        <h2 id={heading} className="m-0 text-small font-semibold text-ink-open">{t('work.park.title')}</h2>
        {toEvent?.question && <span className="ml-auto"><InConversation onPress={toEvent.question} /></span>}
      </div>

      <div data-wait="question" tabIndex={-1} className={cn('grid gap-1.5', sought === 'question' ? SOUGHT : 'outline-none')}>
        <p className="m-0 text-small text-ink-soft">
          {t(question ? 'work.park.waitingHere' : 'work.park.waitingAbove', { span: elapsed(since) })}
        </p>
        {question && <QuestionCard question={question} selected={selected} onSelect={onSelect} />}
      </div>

      {goAheads.length > 0 && (
        <div className="grid gap-1">
          <p className="m-0 text-small font-semibold text-ink-soft">{t('work.park.goAheads')}</p>
          <ul className="m-0 grid list-none gap-1.5 p-0">
            {goAheads.map((goAhead) => {
              const why = goAhead.asked.find((request) => request.why.trim())?.why;
              const toCall = toEvent?.goAheads?.[goAhead.number];
              return (
                <li
                  key={goAhead.number}
                  data-go-ahead={goAhead.number}
                  tabIndex={-1}
                  className={cn(
                    'grid gap-1 rounded-control border border-line bg-raised px-3 py-2',
                    sought === `goAhead:${goAhead.number}` ? 'outline-2 outline-offset-2 outline-accent/60' : 'outline-none',
                  )}
                >
                  <p className="m-0 flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
                    <span className="font-mono text-meta text-ink-faint">#{goAhead.number}</span>
                    <span className="text-body font-semibold">
                      {t('asks.goAhead.act', {
                        kind: KINDS.has(goAhead.kind) ? t(`asks.goAhead.kind.${goAhead.kind}`) : goAhead.kind, on: goAhead.on,
                      })}
                    </span>
                    <span className="min-w-0 break-words text-body">“{goAhead.act}”</span>
                    {toCall && <span className="ml-auto"><InConversation onPress={toCall} /></span>}
                  </p>
                  {why && <p className="m-0 break-words text-small text-ink-soft">{why}</p>}
                  {onGoAhead && (
                    <span className="flex flex-wrap gap-2">
                      <Button disabled={busy} onClick={() => onGoAhead(goAhead.number, true)}>{t('asks.goAhead.approve')}</Button>
                      <Button variant="danger" disabled={busy} onClick={() => onGoAhead(goAhead.number, false)}>
                        {t('asks.goAhead.refuse')}
                      </Button>
                    </span>
                  )}
                </li>
              );
            })}
          </ul>
        </div>
      )}

      {held && <p className="m-0 text-small text-ink-soft">{t('work.park.order')}</p>}

      <details className="text-small text-ink-faint">
        <summary className="cursor-pointer">{t('work.park.whileWaiting')}</summary>
        <div className="mt-1 grid gap-0.5">
          <p className="m-0">{t('work.park.noDeadline')}</p>
          {quest && <p className="m-0">{t(tree ? 'work.park.meanwhileTree' : 'work.park.meanwhile', { quest, tree })}</p>}
          <p className="m-0">{t('work.park.goesOn')}</p>
        </div>
      </details>
    </section>
  );
}
