import { type ReactNode, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead, Quest, Session } from '../../data/api';
import { cn } from '../../lib/cn';
import type { WaitEvents } from './anchors';
import { type GoAheadAnswer, GoAheadList } from '../../asks/GoAheadList';
import { answeredPark, Button } from '../../design/ui';
import type { StepWaitRow } from '../attention/attentionPushed';
import { RunWaitsHead } from '../head/RunWaitsHead';
import { partsOf, RunParts } from '../../workflow/parts/RunParts';
import type { WorkflowRun } from '../../workflow/run';
import { sessionOrigin } from '../identity';
import { type AnswerPick, AwaitingPerson, type QuestClose, type Resolution } from './AwaitingPerson';
import { ConversationSignIn } from './ConversationSignIn';
import { type DockLine, dockName, dockOf, type DockWait, type StepWait } from './dock';
import type { LineBehind } from '../review/diff';
import type { DiscardOffer, LandOffer } from '../groups';
import { HelperAsks, type HelperWaits } from './HelperAsks';
import { HandOverAgentAsk, type HandOverAskWait } from './HandOverAgentAsk';
import type { Answered } from '../InlineConfirm';
import type { SayRebase } from '../review/LandingSync';
import { Discards, type HeadOpinion, type HeadReview, type LandingPlan, Lands, type WorkAsk } from '../review/Lands';
import { heldFor, PermissionAsks, type PermissionWaits } from './PermissionAsks';
import { AnswerAlone, answerFor, type AnswerWaits } from './AnswerAlone';
import type { SessionGitAccessHold } from '../head/GitAccessFact';
import { GitAsk, gitAsksFor, type GitAskWaits } from './GitAsk';
import { ProposedRules, type RuleAsk, type RuleWaits } from './ProposedRules';
import { InConversation, leftFor, type Seek, SOUGHT, useSought } from './WaitAnchor';
import { rulesAsked, WORK_WAITS } from './waits';
import { waitObligations } from './waitObligations';
import type { ParkQuestion } from './parkQuestion';

/**
 * What the frame hands both the session's head and the strip at its foot: every wait's press and its
 * pending, one object, so a press in either place is the same press and its answer settles both.
 */
export type WaitProps = {
  session: Session;
  resolving?: boolean;
  /** A parked session's moves, the head's card's: the strip reads it only to know the park is the person's here. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /**
   * And its answer's door, the card's: the box's own say door where the box below answers the park too, else the park's answer door
   * Settles with a refusal's sentence, or nothing.
   */
  onAnswerSession?: (answer: string) => Promise<string | null> | void;
  onAnswerChoice?: (label: string, words: string) => Promise<string | null> | void;
  answerPick?: AnswerPick;
  onAnswerAsk?: (ask: string) => void;
  goAheads?: GoAhead[];
  /** The ask its go-aheads are on, by which the count keys each as *What needs you* does. */
  goAheadsOn?: string | null;
  onGoAhead?: GoAheadAnswer;
  /**
   * The park's question as its agent asked it: the head's card shows it, and the box below
   * draws it with its answers as presses. Null where it asked none through the connector.
   */
  question?: ParkQuestion | null;
  /**
   * The box below is a driven park's answer box, which draws what the park asks with every press: the strip draws none
   * of the park's waits, and the head lists its go-aheads with no press. A parked chat's composer draws none of it, so is no such box.
   */
  parkBox?: boolean;
  /**
   * The tool calls it holds for the person's answer under asking: every request this machine's sessions
   * hold as the shell read them, its own drawn, the answer's one door and its pending. Absent where nothing reads them (a browser).
   */
  permissions?: PermissionWaits | null;
  /**
   * The answers the answers-alone bound holds for the person: every one the
   * shell read, its own drawn, the agents' names, the answer's one door and its pending. Absent where nothing reads them (a browser).
   */
  answers?: AnswerWaits | null;
  /**
   * The git go-aheads its git door holds: every one the shell pushed, its own drawn, the answer's one door
   * and its pending. Absent where nothing reads them (a browser).
   */
  gitAsks?: GitAskWaits | null;
  gitAccess?: SessionGitAccessHold | null;
  /**
   * The rules it proposed that the person still answers: the machine's proposals as the shell read them,
   * the answer's door, its pending, and whether the session takes words now. Absent where nothing reads the rules (a browser).
   */
  rules?: RuleWaits | null;
  /**
   * The helpers it asked for: its asks as the driver's look left them, the agents' names, the person's yes and
   * no, and a door to each helper. One an agent asked that the look holds waiting on the person's word is a wait.
   * Absent where nothing reads them (a browser, a teammate's record).
   */
  helpers?: HelperWaits | null;
  /**
   * Its agent's own ask to hand it over: the ask as the driver's look left it, the agents' names, the
   * person's yes and no. One the look holds waiting on their word is a wait. Absent where nothing reads it (a browser, a teammate's record).
   */
  handOver?: HandOverAskWait | null;
  lands?: LandOffer | null;
  landing?: LandingPlan | null;
  onReviewRun?: () => void;
  onSayRebase?: SayRebase;
  review?: HeadReview | null;
  behind?: LineBehind | null;
  opinion?: HeadOpinion | null;
  onReview?: () => void;
  discards?: DiscardOffer | null;
  onDiscardTree?: (answered: Answered) => void;
  workBusy?: boolean;
  /**
   * The way from each wait to the event in the conversation that raised it, where the conversation finds one:
   * *In the conversation* on the head's sections and the strip's lines alike.
   */
  toEvent?: WaitEvents;
};

/** The run's steps held for the person or left failed, as Sessions placed the session, with *What needs you*'s rows and their doors. */
export type RunWaitDoors = {
  waits: readonly StepWait[];
  steps: readonly StepWaitRow[];
  onWord?: Parameters<typeof RunWaitsHead>[0]['onWord'];
  onHeld?: Parameters<typeof RunWaitsHead>[0]['onHeld'];
  hold?: Parameters<typeof RunWaitsHead>[0]['hold'];
  release?: Parameters<typeof RunWaitsHead>[0]['release'];
  /** The session's runs as their one reading says them, whose ask's parts the dock names (`RunParts`), with a quest's door; none where unread. */
  parts?: { runs: readonly WorkflowRun[]; onQuest?: (id: string) => void } | null;
};

/** The dock's waits the session itself asked the person: a step its agent holds says it waits on these. */
const ASKED: ReadonlySet<DockWait['kind']> = new Set(['question', 'intake', 'goAhead', 'permission', 'gitAsk', 'answerAlone', 'helper', 'handOver']);

/**
 * **The dock: every wait the person settles, in one place above the composer**: outside the conversation's scroll, where
 * the person reads its last message, each open wait with its own press, oldest first under a count, never folded. It is the only
 * place a wait is pressed on the session's page: the head names the oldest in its state line and draws none of them.
 *
 * @remarks
 * **Today's presses, moved**: a park's card `AwaitingPerson` with its answer, *Finish…* and *Decline…* (where the box below
 * answers the park, the card's answers are the box's), an intake's *Answer*, a go-ahead `GoAheadList`'s where no box below answers
 * the park (where one does, the box draws it), a tool call held for the person's answer `PermissionAsks`, a git go-ahead `GitAsk`,
 * a third answer alone `AnswerAlone`, a rule it proposed `ProposedRules`, a helper it asked for `HelperAsks`, its agent's ask to
 * hand over `HandOverAgentAsk`, the work's line `Lands` with its door to the review, the rebase words and the gates, a discard the
 * line holds `Discards`, and a step of its run held for the person or left failed `RunWaitsHead` with the presses its row in
 * *What needs you* offers (*Try again*, *Done outside Daoris*, *End this work*) and, on a release's approval, the card its run's view
 * draws. Each line keeps its way to where it arose (*In the conversation*) where the conversation found it.
 *
 * **One owner of each answer**: the frame holds the presses and their pending; a refusal is said where the press is made.
 *
 * **A teammate's session** draws its waits as one line with no press: nothing this window sends reaches its machine.
 *
 * Nothing waits, nothing is drawn. A molecule: no hook beyond its focus and sought line.
 */
export function WaitStrip({ answerBelow = false, workAsk, ruleAsk, seek, runWaits = null, quest = null, ...props }: WaitProps & {
  /** The foot's box takes the park's answer: a driven park's answer box, or a parked chat's composer. */
  answerBelow?: boolean;
  /** Where the work's ask is open, the frame's. */
  workAsk?: WorkAsk;
  /** Where a rule's *Allow…* is open, the frame's. */
  ruleAsk?: RuleAsk;
  /**
   * The wait a raising block's *To the wait* or the head's state line leads to, counted so the same one pressed again is shown
   * again: its line takes the focus, outlined.
   */
  seek?: Seek | null;
  /** The run's steps that wait on the person, with their presses. Absent where nothing reads them (a browser, a story). */
  runWaits?: RunWaitDoors | null;
  /** The quest it serves, which a park's *Finish…* may close as the person's. */
  quest?: Quest | null;
}) {
  const { t } = useTranslation();
  const section = useRef<HTMLElement>(null);
  const { sought, letGo } = useSought(section, seek);
  const { session, toEvent } = props;
  if (sessionOrigin(session) !== null) return <TeammateWaits session={session} steps={runWaits?.waits ?? []} />;
  // A park's go-aheads whose box is right below are not drawn here: the box draws what the park asks, each answer a press the
  // box's words go with, so a line here would be a second place to answer the same ask. Nor are they counted.
  const boxBelow = (wait: DockWait) => wait.kind === 'goAhead' && Boolean(props.parkBox);
  const facts = { ...props, answerBelow };
  const all = dockOf(facts, runWaits?.waits ?? []);
  const lines = all.filter((line) => !boxBelow(line.wait));
  // The work's line stands here whenever its tree offers work, held or not: its holds, its set-up and its door are pressed here too,
  // though only an open gate is counted as a wait.
  const workLine = props.lands || props.discards ? { key: 'work', wait: { kind: props.lands ? 'accept' : 'discard' }, since: '' } as DockLine : null;
  const drawnLines = workLine && !lines.some((line) => (WORK_WAITS as ReadonlySet<string>).has(line.wait.kind) || line.wait.kind === 'discard') ? [...lines, workLine] : lines;
  const parts = runWaits?.parts && partsOf(runWaits.parts.runs).length > 0 ? runWaits.parts : null;
  if (drawnLines.length === 0 && !parts) return null;
  const pageWaits = (of: readonly DockLine[]) => of.flatMap((line) => (line.wait.kind === 'step' ? [] : [line.wait]));
  // The count is of obligations, keyed as the bell and *What needs you* key them, so a park on the go-ahead it asked is one; and
  // each step its run holds for the person.
  const owed = waitObligations(facts, pageWaits(lines), pageWaits(all)).length + lines.filter((line) => line.wait.kind === 'step').length;
  // Never pluralised by `count`: in Chinese one is `other` too, and one wait is said without its number.
  const title = owed > 1 ? t('work.wait.several', { n: owed }) : t('work.wait.title');
  // A kind several lines share says its lead once, on its first line; the work's line is one whatever gates it holds.
  const led = new Set<string>();
  const first = (kind: string) => {
    if (led.has(kind)) return false;
    led.add(kind);
    return true;
  };
  const marked = (prefix: string) => (sought?.startsWith(`${prefix}:`) ? sought.slice(prefix.length + 1) : undefined);

  // The waits a step's agent may be blocked on: what the session itself asked the person, each led to by its line.
  const blockedOn = (of: readonly DockLine[]) => of.flatMap((line) => (ASKED.has(line.wait.kind) ? [{ key: line.key, name: dockName(t, line.wait) }] : []));
  const to = (key: string) => {
    const line = [...(section.current?.querySelectorAll<HTMLElement>('[data-dock-line]') ?? [])].find((each) => each.dataset.dockLine === key);
    line?.scrollIntoView?.({ block: 'nearest' });
    line?.querySelector<HTMLElement>('[data-wait], input, textarea, button')?.focus();
  };

  const lineOf = ({ wait }: DockLine): ReactNode => {
    switch (wait.kind) {
      case 'question':
        // A conversation's sign-in question: its presses and no park's moves (a conversation has none to finish or decline).
        if (session.kind === 'chat' && props.question?.site) {
          return (
            <div data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
              <ConversationSignIn
                key={session.id}
                question={props.question}
                site={props.question.site}
                pending={props.resolving}
                onAnswer={props.onAnswerSession}
                onConversation={toEvent?.question}
              />
            </div>
          );
        }

        return props.onResolve || props.onAnswerSession ? (
          <div data-wait="question" tabIndex={-1} className={sought === 'question' ? SOUGHT : 'outline-none'}>
            {/* Keyed: a half-written decline reason belongs to the session it is written for. */}
            <AwaitingPerson
              key={session.id}
              parts={session.noteParts}
              question={props.question}
              quest={session.kind !== 'chat' && quest && quest.id === session.quest ? quest : null}
              pending={props.resolving}
              onResolve={props.onResolve}
              onAnswer={session.kind !== 'chat' ? props.onAnswerSession : undefined}
              onChoice={session.kind !== 'chat' ? props.onAnswerChoice : undefined}
              pick={props.answerPick}
              below={Boolean(props.parkBox)}
              onConversation={toEvent?.question}
            />
          </div>
        ) : (
          <p data-wait="question" tabIndex={-1} className={cn('m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft', sought === 'question' ? SOUGHT : 'outline-none')}>
            <span>{t('work.wait.questionBelow')}</span>
            {toEvent?.question && <InConversation onPress={toEvent.question} />}
          </p>
        );
      case 'intake':
        return (
          <p data-wait="question" tabIndex={-1} className={cn('m-0 flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-small text-ink-soft', sought === 'question' ? SOUGHT : 'outline-none')}>
            <span>{t('work.wait.intake')}</span>
            <Button variant="primary" className="px-2 py-0.5 text-small" onClick={() => props.onAnswerAsk?.(wait.ask)}>
              {t('work.intake.answer', { ask: wait.ask })}
            </Button>
            {toEvent?.question && <InConversation onPress={toEvent.question} />}
          </p>
        );
      case 'goAhead':
        return (
          <GoAheadList
            goAheads={(props.goAheads ?? []).filter((goAhead) => goAhead.number === wait.number)}
            busy={props.resolving}
            onAnswer={props.onGoAhead}
            lead={first('goAhead') ? t('work.awaiting.goAheads') : undefined}
            inConversation={toEvent?.goAheads}
            marked={marked('goAhead') !== undefined ? Number(marked('goAhead')) : undefined}
          />
        );
      case 'permission':
        return props.permissions && (
          <PermissionAsks
            waits={heldFor(props.permissions.waits, session.id).filter((held) => held.request === wait.request)}
            onAnswer={props.permissions.onAnswer}
            busy={props.permissions.busy}
            lead={first('permission') ? t('work.wait.permission.lead') : undefined}
            inConversation={toEvent?.permissions}
            marked={marked('permission')}
          />
        );
      case 'gitAsk':
        return props.gitAsks && gitAsksFor(props.gitAsks.waits, session.id).filter((ask) => ask.id === wait.id).map((ask) => (
          <GitAsk key={ask.id} ask={ask} onAnswer={props.gitAsks!.onAnswer} busy={props.gitAsks!.busy} marked={sought === `gitAsk:${ask.id}`} />
        ));
      case 'answerAlone': {
        const alone = props.answers ? answerFor(props.answers.waits, session.id) : null;
        return alone && props.answers && (
          <AnswerAlone wait={alone} nameOf={props.answers.nameOf} onAnswer={props.answers.onAnswer} onChangeRule={props.answers.onChangeRule} busy={props.answers.busy} />
        );
      }
      case 'rule':
        return props.rules && (
          <ProposedRules
            proposals={rulesAsked(props.rules.proposals, session.id).filter((proposal) => proposal.id === wait.id)}
            busy={props.rules.busy}
            onAnswer={props.rules.onAnswer}
            heard={props.rules.heard}
            lead={first('rule') ? t('work.wait.rule.lead') : undefined}
            inConversation={toEvent?.rules}
            marked={marked('rule')}
            ask={ruleAsk}
          />
        );
      case 'helper':
        return props.helpers && (
          <>
            {first('helper') && <p className="m-0 text-small text-ink-soft">{t('work.wait.helper.lead')}</p>}
            <HelperAsks {...props.helpers} asks={props.helpers.asks.filter((row) => row.id === wait.id)} inConversation={toEvent?.helpers} marked={marked('helper')} />
          </>
        );
      case 'handOver':
        return props.handOver && (
          <>
            <p className="m-0 text-small text-ink-soft">{t('work.handOverAsk.lead')}</p>
            <HandOverAgentAsk {...props.handOver} />
          </>
        );
      case 'discard':
        return props.discards && (
          <Discards discards={props.discards} onReview={props.onReview} onDiscard={props.onDiscardTree} ask={workAsk} busy={props.workBusy} />
        );
      case 'step':
        return runWaits && (
          <RunWaitsHead
            waits={[wait]} steps={runWaits.steps} onWord={runWaits.onWord} onHeld={runWaits.onHeld} hold={runWaits.hold} release={runWaits.release}
            blocked={blockedOn(lines)} onTo={to}
          />
        );
      default:
        // The work's line: its Accept, rebase, opinion and review are one `Lands`, drawn at the oldest of them.
        return WORK_WAITS.has(wait.kind) && first('work') && props.lands && (
          <div data-wait="work" tabIndex={-1} className={cn('grid gap-2', sought === 'work' || sought === 'opinion' ? SOUGHT : 'outline-none')}>
            <Lands
              session={session} lands={props.lands} landing={props.landing} onReview={props.onReview} onReviewRun={props.onReviewRun}
              onSayRebase={props.onSayRebase} review={props.review} behind={props.behind} opinion={props.opinion} toEvent={toEvent}
            />
          </div>
        );
    }
  };

  return (
    <section
      ref={section}
      aria-label={drawnLines.length > 0 ? title : t('work.wait.parts')}
      data-wait-part=""
      data-dock=""
      className="shrink-0 border-t border-line bg-page"
      // The outline goes once the person moves on from the line it led to.
      onBlur={(event) => {
        if (leftFor(event)) letGo();
      }}
    >
      {/* Bounded, so a long gate never pushes the conversation off the window: the dock scrolls in itself, within its share of the
          waiting panel's height, or 40% of the window where no panel holds it. */}
      <div className="grid max-h-[var(--wait-part,40vh)] grid-cols-[minmax(0,1fr)] gap-2 overflow-y-auto px-4 py-2">
        {owed > 0 && <h2 className="m-0 text-small font-semibold text-ink-open">{title}</h2>}
        {drawnLines.map((line) => {
          const drawn = lineOf(line);
          return drawn ? <div key={line.key} data-dock-line={line.key} className="grid min-w-0 gap-1">{drawn}</div> : null;
        })}
        {parts && <div data-dock-line="parts" className="grid min-w-0 gap-1"><RunParts runs={parts.runs} onQuest={parts.onQuest} /></div>}
      </div>
    </section>
  );
}

/**
 * **A teammate's waits**: its park and its run's steps named in one line, with no press, since nothing this window sends reaches
 * its machine. Nothing where none waits.
 */
function TeammateWaits({ session, steps }: { session: Session; steps: readonly StepWait[] }) {
  const { t } = useTranslation();
  const waits: DockWait[] = [
    ...(session.state === 'awaiting-person' && !answeredPark(session) ? [{ kind: 'question' as const, below: false }] : []),
    ...steps.map((step) => ({ kind: 'step' as const, ...step })),
  ];
  if (waits.length === 0) return null;
  const said = t('work.wait.teammate', { names: waits.map((wait) => dockName(t, wait)).join(' · ') });
  return (
    <section aria-label={said} data-wait-part="" data-dock="" className="shrink-0 border-t border-line bg-page px-4 py-2">
      <p className="m-0 text-small text-ink-soft">{said}</p>
    </section>
  );
}
