import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead, Session } from '../../data/api';
import { cn } from '../../lib/cn';
import type { WaitEvents } from './anchors';
import { type GoAheadAnswer, GoAheadList } from '../../asks/GoAheadList';
import { Button } from '../../design/ui';
import type { QuestClose, Resolution } from './AwaitingPerson';
import type { LineBehind } from '../review/diff';
import type { DiscardOffer, LandOffer } from '../groups';
import { HelperAsks, type HelperWaits } from './HelperAsks';
import { HandOverAgentAsk, type HandOverAskWait } from './HandOverAgentAsk';
import type { Answered } from '../InlineConfirm';
import type { SayRebase } from '../review/LandingSync';
import { Discards, type HeadOpinion, type HeadReview, type LandingPlan, Lands, type WorkAsk } from '../review/Lands';
import { heldFor, PermissionAsks, type PermissionWaits } from './PermissionAsks';
import { ProposedRules, type RuleAsk, type RuleWaits } from './ProposedRules';
import { InConversation, leftFor, type Seek, SOUGHT, useSought, waitName } from './WaitAnchor';
import { rulesAsked, type Wait, WORK_WAITS, waitsOf } from './waits';

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
  /**
   * The tool calls it holds for the person's answer under asking (AUTONOMY1b3c, D184 point 3): every request this machine's sessions
   * hold as the shell read them, its own drawn, the answer's one door and its pending. Absent where nothing reads them (a browser).
   */
  permissions?: PermissionWaits | null;
  /**
   * The rules it proposed that the person still answers (PROPOSEHERE1b, D74): the machine's proposals as the shell read them,
   * the answer's door, its pending, and whether the session takes words now. Absent where nothing reads the rules (a browser).
   */
  rules?: RuleWaits | null;
  /**
   * The helpers it asked for (MIXAGENT2b, D163): its asks as the driver's look left them, the agents' names, the person's yes and
   * no, and a door to each helper. One an agent asked that the look holds waiting on the person's word is a wait (MIXAGENT2c).
   * Absent where nothing reads them (a browser, a teammate's record).
   */
  helpers?: HelperWaits | null;
  /**
   * Its agent's own ask to hand it over (MIXAGENT1c, D163 point 3): the ask as the driver's look left it, the agents' names, the
   * person's yes and no. One the look holds waiting on their word is a wait. Absent where nothing reads it (a browser, a teammate's record).
   */
  handOver?: HandOverAskWait | null;
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
  /**
   * The way from each wait to the event in the conversation that raised it, where the conversation found one (WAITFOOT1c, D168
   * point 4): *In the conversation* on the head's sections and the strip's lines alike.
   */
  toEvent?: WaitEvents;
};

/**
 * **What waits on the person, at the foot** (WAITFOOT1b, D168): above the session's box and outside the conversation's scroll,
 * where the person reads its last message, each open wait with the same press its head has, so none is a record's length
 * above the words that raised it.
 *
 * @remarks
 * **The head's molecules, not copies** (D168 point 2): a go-ahead still open is `GoAheadList`'s, a rule the session proposed
 * `ProposedRules`' (PROPOSEHERE1b), the work's line `Lands` with its *Accept…*, the rebase words and the gates, a discard the line
 * holds `Discards`, an agent's ask for a helper waiting on the person's word `HelperAsks` with its yes and no (MIXAGENT2c), a tool
 * call held for their answer under asking `PermissionAsks` with a press per choice its agent offered (AUTONOMY1b3c), each
 * handed what the head is handed. A park's
 * question is named and not answered here: the box below is its answer's one door (FIX-LOG 2026-09-29), and its *Finish…* and
 * *Decline…* stay its card's (D56). A hand-over offer is the pinned page head's, already on screen.
 *
 * **One owner of each answer** (D168 point 3): the frame holds the presses and their pending; the work's ask is open in one
 * place at a time (`workAsk`); a refusal is said where the press was made.
 *
 * **One place at a time** (WAITFOOT2a, D168's WAITFOOT2 note): while the head's waits are wholly on screen (`aside`, the
 * frame's to read) the strip stands aside, hidden and not unmade, so a fold and words begun in it are kept. Never from under the
 * person: it stays while an ask is open in it, while a press led to its line, and while the focus is in it.
 *
 * Several fold to one line naming them; *To the top* takes the person to the head's waits. Nothing waits, nothing is drawn.
 * A molecule: no hook beyond its own fold, focus and sought line.
 */
export function WaitStrip({ answerBelow = false, workAsk, ruleAsk, onTop, seek, aside = false, ...props }: WaitProps & {
  /** The foot's box takes the park's answer: a driven park's answer box, or a parked chat's composer. */
  answerBelow?: boolean;
  /** Where the work's ask is open, the frame's, so it is open here or in the head (D168 point 3). */
  workAsk?: WorkAsk;
  /** Where a rule's *Allow…* is open, the frame's, so it is open here or in the head (D168 point 3). */
  ruleAsk?: RuleAsk;
  /** Scroll the session's head, where every wait stands with its reading, into view. */
  onTop?: () => void;
  /**
   * The wait a raising block's *To the wait* leads to (WAITFOOT1c), counted so the same one pressed again is shown again: the
   * strip unfolds, and its line takes the focus, outlined.
   */
  seek?: Seek | null;
  /** The head's waits are wholly on screen, so the strip stands aside unless the person is in it (WAITFOOT2a). */
  aside?: boolean;
}) {
  const { t } = useTranslation();
  const [folded, setFolded] = useState(false);
  const [inside, setInside] = useState(false);
  const section = useRef<HTMLElement>(null);
  const { sought, letGo } = useSought(section, seek, () => setFolded(false));
  // A line unmade with the focus in it (an ask put down, a go-ahead answered) says no blur: the focus is read again once drawn.
  useEffect(() => {
    if (inside && !section.current?.contains(section.current.ownerDocument.activeElement)) setInside(false);
  });
  const { session, toEvent } = props;
  const waits = waitsOf({ ...props, answerBelow });
  // A park's question whose box is right below is counted but not drawn (WAITFOOT2c): the box is the door and says so in its
  // own placeholder, so a line over it was a second statement of the same ask. With nothing else to draw, no strip.
  const boxBelow = (wait: Wait) => wait.kind === 'question' && wait.below;
  if (waits.every(boxBelow)) return null;
  // Never from under the person (WAITFOOT2a): an ask open here, or the focus, a line a press led to's among it, holds it up.
  const held = inside || Boolean(workAsk?.open) || Boolean(ruleAsk?.open);
  const several = waits.length > 1;
  const shut = several && folded;
  const question = waits.find((wait) => wait.kind === 'question');
  const intake = waits.find((wait): wait is Extract<Wait, { kind: 'intake' }> => wait.kind === 'intake');
  const open = new Set(waits.filter((wait) => wait.kind === 'goAhead').map((wait) => (wait as { number: number }).number));
  const asking = new Set(waits.filter((wait) => wait.kind === 'permission').map((wait) => (wait as { request: string }).request));
  const ruled = new Set(waits.filter((wait) => wait.kind === 'rule').map((wait) => (wait as { id: string }).id));
  const helping = new Set(waits.filter((wait) => wait.kind === 'helper').map((wait) => (wait as { id: string }).id));
  // Its agent's ask to hand it over (MIXAGENT1c) is the strip's own: the head draws no line of it, so the strip never stands aside for it.
  const handing = waits.some((wait) => wait.kind === 'handOver');
  const work = waits.some((wait) => WORK_WAITS.has(wait.kind));
  const discard = waits.some((wait) => wait.kind === 'discard');
  // Never pluralised by `count`: in Chinese one is `other` too, and one wait is said without its number.
  const title = several ? t('work.wait.several', { n: waits.length }) : t('work.wait.title');

  return (
    <section
      ref={section}
      aria-label={title}
      hidden={aside && !held && !handing}
      className="shrink-0 border-t border-line bg-page"
      onFocus={() => setInside(true)}
      // The outline goes once the person moves on from the line it led to, and the strip with it where the head's are on screen.
      onBlur={(event) => {
        if (!leftFor(event)) return;
        letGo();
        setInside(false);
      }}
    >
      {/* Bounded, so a long gate never pushes the conversation off the window: the strip scrolls in itself. */}
      <div className="grid max-h-[40vh] grid-cols-[minmax(0,1fr)] gap-2 overflow-y-auto px-4 py-2">
        <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1">
          <h2 className="m-0 text-small font-semibold text-ink-open">{title}</h2>
          {shut && <span className="min-w-0 text-small text-ink-soft">{waits.map((wait) => waitName(t, wait)).join(' · ')}</span>}
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
            {question?.kind === 'question' && !question.below && (
              <p
                data-wait="question"
                tabIndex={-1}
                className={cn('m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft', sought === 'question' ? SOUGHT : 'outline-none')}
              >
                <span>{t(question.below ? 'work.wait.questionBelow' : 'work.wait.questionTop')}</span>
                {toEvent?.question && <InConversation onPress={toEvent.question} />}
              </p>
            )}
            {intake && (
              <p
                data-wait="question"
                tabIndex={-1}
                className={cn('m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft', sought === 'question' ? SOUGHT : 'outline-none')}
              >
                <span>{t('work.wait.intake')}</span>
                <Button variant="primary" className="px-2 py-0.5 text-small" onClick={() => props.onAnswerAsk?.(intake.ask)}>
                  {t('work.intake.answer', { ask: intake.ask })}
                </Button>
                {toEvent?.question && <InConversation onPress={toEvent.question} />}
              </p>
            )}
            {open.size > 0 && (
              <div className="grid gap-1">
                <GoAheadList
                  goAheads={(props.goAheads ?? []).filter((goAhead) => open.has(goAhead.number))}
                  busy={props.resolving}
                  onAnswer={props.onGoAhead}
                  lead={t('work.awaiting.goAheads')}
                  inConversation={toEvent?.goAheads}
                  marked={sought?.startsWith('goAhead:') ? Number(sought.slice('goAhead:'.length)) : undefined}
                />
              </div>
            )}
            {asking.size > 0 && props.permissions && (
              <div className="grid gap-1">
                <PermissionAsks
                  waits={heldFor(props.permissions.waits, session.id).filter((held) => asking.has(held.request))}
                  onAnswer={props.permissions.onAnswer}
                  busy={props.permissions.busy}
                  lead={t('work.wait.permission.lead')}
                  inConversation={toEvent?.permissions}
                  marked={sought?.startsWith('permission:') ? sought.slice('permission:'.length) : undefined}
                />
              </div>
            )}
            {ruled.size > 0 && props.rules && (
              <div className="grid gap-1">
                <ProposedRules
                  proposals={rulesAsked(props.rules.proposals, session.id).filter((proposal) => ruled.has(proposal.id))}
                  busy={props.rules.busy}
                  onAnswer={props.rules.onAnswer}
                  heard={props.rules.heard}
                  lead={t('work.wait.rule.lead')}
                  inConversation={toEvent?.rules}
                  marked={sought?.startsWith('rule:') ? sought.slice('rule:'.length) : undefined}
                  ask={ruleAsk}
                />
              </div>
            )}
            {helping.size > 0 && props.helpers && (
              <div className="grid gap-1">
                <p className="m-0 text-small text-ink-soft">{t('work.wait.helper.lead')}</p>
                <HelperAsks
                  {...props.helpers}
                  asks={props.helpers.asks.filter((row) => helping.has(row.id))}
                  inConversation={toEvent?.helpers}
                  marked={sought?.startsWith('helper:') ? sought.slice('helper:'.length) : undefined}
                />
              </div>
            )}
            {handing && props.handOver && (
              <div className="grid gap-1">
                <p className="m-0 text-small text-ink-soft">{t('work.handOverAsk.lead')}</p>
                <HandOverAgentAsk {...props.handOver} />
              </div>
            )}
            {work && props.lands && (
              <div
                data-wait="work"
                tabIndex={-1}
                className={cn('grid gap-2', sought === 'work' || sought === 'opinion' ? SOUGHT : 'outline-none')}
              >
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
                  toEvent={toEvent}
                />
              </div>
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
