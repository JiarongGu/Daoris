import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { GoAhead, Session } from '../data/api';
import { type GoAheadAnswer, GoAheadList } from '../asks/GoAheadList';
import { Button } from '../design/ui';
import type { QuestClose, Resolution } from './AwaitingPerson';
import type { LineBehind } from './diff';
import type { DiscardOffer, LandOffer } from './groups';
import type { Answered } from './InlineConfirm';
import type { SayRebase } from './LandingSync';
import { Discards, type HeadOpinion, type HeadReview, type LandingPlan, Lands, type WorkAsk } from './Lands';
import { type Wait, WORK_WAITS, waitsOf } from './waits';

/**
 * What the frame hands both the session's head and the strip at its foot (WAITFOOT1b, D168 point 3): every wait's press and its
 * pending, one object, so a press in either place is the same press and its answer settles both.
 */
export type WaitProps = {
  session: Session;
  resolving?: boolean;
  /** A parked session's moves, the head's card's (D56): the strip reads it only to know the park is the person's here. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /** And its answer's door where no box below takes it: the card's too. */
  onAnswerSession?: (answer: string | null) => void;
  onAnswerAsk?: (ask: string) => void;
  goAheads?: GoAhead[];
  onGoAhead?: GoAheadAnswer;
  lands?: LandOffer | null;
  landing?: LandingPlan | null;
  planning?: boolean;
  onLand?: (answered: Answered, answers?: string | null, branch?: string | null) => void;
  onSayRebase?: SayRebase;
  onName?: (name: string) => void;
  review?: HeadReview | null;
  behind?: LineBehind | null;
  opinion?: HeadOpinion | null;
  onReview?: () => void;
  discards?: DiscardOffer | null;
  onDiscardTree?: (answered: Answered) => void;
  /** A landing or a discard of its work is on its way, pressed in either place. */
  workBusy?: boolean;
};

/** A wait's name on the folded line. */
function nameOf(t: TFunction, wait: Wait): string {
  switch (wait.kind) {
    case 'question': return t('work.wait.name.question');
    case 'intake': return t('work.wait.name.intake', { ask: wait.ask });
    case 'goAhead': return t('work.wait.name.goAhead', { number: wait.number });
    default: return t(`work.wait.name.${wait.kind}`);
  }
}

/**
 * **What waits on the person, at the foot** (WAITFOOT1b, D168): above the session's box and outside the conversation's scroll,
 * where the person reads its last message, each open wait with the same press its head has. The owner, 2026-10-15: *"I'm
 * usually looking at the last message … currently missing the end access, so I have to scroll back to the top"*.
 *
 * @remarks
 * **The head's molecules, not copies** (D168 point 2): a go-ahead still open is `GoAheadList`'s, the work's line `Lands` with its
 * *Accept…*, the rebase words and the gates, a discard the line holds `Discards`, each handed what the head is handed. A park's
 * question is named and not answered here: the box below is its answer's one door (FIX-LOG 2026-09-29), and its *Finish…* and
 * *Decline…* stay its card's (D56). A hand-over offer is the pinned page head's, already on screen.
 *
 * **One owner of each answer** (D168 point 3): the frame holds the presses and their pending; the work's ask is open in one
 * place at a time (`workAsk`); a refusal is said where the press was made.
 *
 * Several fold to one line naming them; *To the top* takes the person to the head's waits. Nothing waits, nothing is drawn.
 * A molecule: no hook beyond its own fold.
 */
export function WaitStrip({ answerBelow = false, workAsk, onTop, ...props }: WaitProps & {
  /** The foot's box takes the park's answer: a driven park's answer box, or a parked chat's composer. */
  answerBelow?: boolean;
  /** Where the work's ask is open, the frame's, so it is open here or in the head (D168 point 3). */
  workAsk?: WorkAsk;
  /** Scroll the session's head, where every wait stands with its reading, into view. */
  onTop?: () => void;
}) {
  const { t } = useTranslation();
  const [folded, setFolded] = useState(false);
  const { session } = props;
  const waits = waitsOf({ ...props, answerBelow });
  if (waits.length === 0) return null;
  const several = waits.length > 1;
  const shut = several && folded;
  const question = waits.find((wait) => wait.kind === 'question');
  const intake = waits.find((wait): wait is Extract<Wait, { kind: 'intake' }> => wait.kind === 'intake');
  const open = new Set(waits.filter((wait) => wait.kind === 'goAhead').map((wait) => (wait as { number: number }).number));
  const work = waits.some((wait) => WORK_WAITS.has(wait.kind));
  const discard = waits.some((wait) => wait.kind === 'discard');
  // Never pluralised by `count`: in Chinese one is `other` too, and one wait is said without its number.
  const title = several ? t('work.wait.several', { n: waits.length }) : t('work.wait.title');

  return (
    <section aria-label={title} className="shrink-0 border-t border-line bg-page">
      {/* Bounded, so a long gate never pushes the conversation off the window: the strip scrolls in itself. */}
      <div className="grid max-h-[40vh] grid-cols-[minmax(0,1fr)] gap-2 overflow-y-auto px-4 py-2">
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <h2 className="m-0 text-small font-semibold text-ink-open">{title}</h2>
          {shut && <span className="min-w-0 text-small text-ink-soft">{waits.map((wait) => nameOf(t, wait)).join(' · ')}</span>}
          <span className="ml-auto flex shrink-0 gap-1">
            {several && (
              <Button variant="ghost" className="px-2 py-0.5 text-small" aria-expanded={!shut} onClick={() => setFolded(!shut)}>
                {shut ? t('work.wait.show') : t('work.wait.fold')}
              </Button>
            )}
            {onTop && <Button variant="ghost" className="px-2 py-0.5 text-small" onClick={onTop}>{t('work.wait.top')}</Button>}
          </span>
        </div>
        {!shut && (
          <>
            {question?.kind === 'question' && (
              <p className="m-0 text-small text-ink-soft">{t(question.below ? 'work.wait.questionBelow' : 'work.wait.questionTop')}</p>
            )}
            {intake && (
              <p className="m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft">
                <span>{t('work.wait.intake')}</span>
                <Button variant="primary" className="px-2 py-0.5 text-small" onClick={() => props.onAnswerAsk?.(intake.ask)}>
                  {t('work.intake.answer', { ask: intake.ask })}
                </Button>
              </p>
            )}
            {open.size > 0 && (
              <div className="grid gap-1">
                <GoAheadList
                  goAheads={(props.goAheads ?? []).filter((goAhead) => open.has(goAhead.number))}
                  busy={props.resolving}
                  onAnswer={props.onGoAhead}
                  lead={t('work.awaiting.goAheads')}
                />
              </div>
            )}
            {work && props.lands && (
              <Lands
                session={session}
                lands={props.lands}
                landing={props.landing}
                planning={props.planning}
                onReview={props.onReview}
                onLand={props.onLand}
                onSayRebase={props.onSayRebase}
                onName={props.onName}
                review={props.review}
                behind={props.behind}
                opinion={props.opinion}
                ask={workAsk}
                busy={props.workBusy}
              />
            )}
            {discard && props.discards && (
              <Discards discards={props.discards} onReview={props.onReview} onDiscard={props.onDiscardTree} ask={workAsk} busy={props.workBusy} />
            )}
          </>
        )}
      </div>
    </section>
  );
}
