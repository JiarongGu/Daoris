import type { ReactNode } from 'react';
import type { TFunction } from 'i18next';
import type { Quest } from '../../data/api';
import { answeredPark } from '../../design/ui';
import { RunWaitsHead } from '../head/RunWaitsHead';
import { partsOf, RunParts } from '../../workflow/parts/RunParts';
import { sessionOrigin } from '../identity';
import type { Answered } from '../InlineConfirm';
import { AwaitingPerson } from '../waits/AwaitingPerson';
import { ConversationSignIn } from '../waits/ConversationSignIn';
import { type DockLine, dockName, dockOf, type DockWait } from '../waits/dock';
import { HelperAsks } from '../waits/HelperAsks';
import { HandOverAgentAsk } from '../waits/HandOverAgentAsk';
import { Discards, Lands, type WorkAsk } from '../review/Lands';
import { heldFor, PermissionAsks } from '../waits/PermissionAsks';
import { AnswerAlone, answerFor } from '../waits/AnswerAlone';
import { GitAsk, gitAsksFor } from '../waits/GitAsk';
import { ProposedRules, type RuleAsk } from '../waits/ProposedRules';
import { rulesAsked, WORK_WAITS } from '../waits/waits';
import type { RunWaitDoors, WaitProps } from '../waits/waitProps';
import type { WaitAnswer, WaitCardFacts } from './WaitCard';

/** The dock's waits the session itself asked the person: a step its agent holds says it waits on these. */
const ASKED: ReadonlySet<DockWait['kind']> = new Set(['question', 'intake', 'goAhead', 'permission', 'gitAsk', 'answerAlone', 'helper', 'handOver']);

/** Who asks, for every card's *From*: the session's agent, its maker and the model that ran. */
export type Asker = { role: string; agent: string; model: string | null };

/** What the dock draws: each wait as its card, the answer to a card's press, and what its run waits on that no press settles. */
export type DockCards = { cards: WaitCardFacts[]; onAnswer: (id: string, answer: WaitAnswer) => void; foot: ReactNode };

/**
 * **The dock's cards**: every wait the person settles, read by the dock's one reader (`dockOf`: `waitsOf`'s kinds and its run's
 * steps held for the person or left failed, oldest first, each keyed once) and drawn as the wait card (`WaitCard`). A go-ahead, a
 * question that offers its answers and an intake's ask are answered by the card's own presses; every other kind carries its own
 * presses in the card (a park's *Finish…* and *Decline…*, a held call's options, a git go-ahead, a third answer alone, a rule, a
 * helper, a hand-over, the work's line with its gates, a discard, a step's *Try again* or its approval). Below the cards stands
 * what its run waits on that no press of the person's settles
 * (`RunParts`). A teammate's session draws its waits as one line with no press, since nothing this window sends reaches it.
 */
export function dockCardsOf({ t, props, answerBelow, workAsk, ruleAsk, runWaits, quest, asker, refused }: {
  t: TFunction;
  props: WaitProps;
  answerBelow: boolean;
  workAsk?: WorkAsk;
  ruleAsk?: RuleAsk;
  runWaits: RunWaitDoors | null;
  quest: Quest | null;
  asker: Asker;
  /** Where a press's refusal is said: the page's notice. */
  refused: (sentence: string) => void;
}): DockCards {
  const { session, toEvent } = props;
  if (sessionOrigin(session) !== null) {
    const waits: DockWait[] = [
      ...(session.state === 'awaiting-person' && !answeredPark(session) ? [{ kind: 'question' as const, below: false }] : []),
      ...(runWaits?.waits ?? []).map((step) => ({ kind: 'step' as const, ...step })),
    ];
    const foot = waits.length > 0
      ? <p className="m-0 text-small text-ink-soft">{t('work.wait.teammate', { names: waits.map((wait) => dockName(t, wait)).join(' · ') })}</p>
      : null;
    return { cards: [], onAnswer: () => {}, foot };
  }
  const facts = { ...props, answerBelow };
  // Every go-ahead a park asked is its card's here: the composer below takes words alone.
  const lines = dockOf(facts, runWaits?.waits ?? []);
  // The work's line stands whenever its tree offers work, held or not: its holds, its set-up and its door are pressed here too.
  const workLine = props.lands || props.discards ? { key: 'work', wait: { kind: props.lands ? 'accept' : 'discard' }, since: session.updated } as DockLine : null;
  const drawn = workLine && !lines.some((line) => (WORK_WAITS as ReadonlySet<string>).has(line.wait.kind) || line.wait.kind === 'discard') ? [...lines, workLine] : lines;
  const blockedOn = lines.flatMap((line) => (ASKED.has(line.wait.kind) ? [{ key: line.key, name: dockName(t, line.wait) }] : []));
  const answered: Answered = { done: () => {}, refused };
  const answers = new Map<string, (answer: WaitAnswer) => void>();
  const card = (line: DockLine, fields: Partial<WaitCardFacts>): WaitCardFacts => ({
    id: line.key, title: dockName(t, line.wait), kind: line.wait.kind, since: line.since, what: '', look: [], known: [],
    from: { ...asker, at: line.since }, ...fields,
  });
  let worked = false;
  const cards = drawn.flatMap((line): WaitCardFacts[] => {
    const { wait } = line;
    const inConversation = (to: (() => void) | undefined) => (to ? [{ label: t('work.wait.inConversation'), onOpen: to }] : []);
    switch (wait.kind) {
      case 'goAhead': {
        const goAhead = (props.goAheads ?? []).find((each) => each.number === wait.number);
        if (!goAhead) return [];
        answers.set(line.key, (answer) => props.onGoAhead?.(wait.number, answer === 'yes', undefined, answered));
        return [card(line, {
          title: `${t('asks.goAhead.act', { kind: goAhead.kind, on: goAhead.on })} · ${goAhead.act}`,
          what: goAhead.asked.map((asked) => asked.why).filter(Boolean).join('\n'),
          yes: { label: t('asks.goAhead.approve'), effect: t('work.session.wait.goAheadYes', { act: goAhead.act, on: goAhead.on }) },
          no: { label: t('asks.goAhead.refuse'), effect: t('work.session.wait.goAheadNo') },
          look: inConversation(toEvent?.goAheads?.[wait.number]),
        })];
      }
      case 'intake':
        answers.set(line.key, () => props.onAnswerAsk?.(wait.ask));
        return [card(line, {
          what: t('work.wait.intake'),
          yes: { label: t('work.intake.answer', { ask: wait.ask }), effect: t('work.session.wait.intakeYes') },
          look: inConversation(toEvent?.question),
        })];
      case 'question': {
        const question = props.question;
        if (session.kind === 'chat' && question?.site) {
          return [card(line, {
            what: question.question, look: inConversation(toEvent?.question),
            presses: <ConversationSignIn key={session.id} question={question} site={question.site} pending={props.resolving} onAnswer={props.onAnswerSession} />,
          })];
        }
        if (question && question.choices.length > 0 && props.onAnswerChoice) {
          answers.set(line.key, (answer) => {
            const choice = typeof answer === 'object' ? question.choices[answer.option] : undefined;
            if (choice) void props.onAnswerChoice?.(choice.label, '');
          });
          return [card(line, {
            what: question.question, options: question.choices.map((choice) => ({ label: choice.label, effect: choice.does })),
            look: inConversation(toEvent?.question),
          })];
        }
        return [card(line, {
          what: question?.question ?? session.note ?? '', look: inConversation(toEvent?.question),
          presses: props.onResolve || props.onAnswerSession ? (
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
            />
          ) : <span className="text-small text-ink-soft">{t('work.wait.questionBelow')}</span>,
        })];
      }
      case 'permission':
        return props.permissions ? [card(line, {
          look: inConversation(toEvent?.permissions?.[wait.request]),
          presses: <PermissionAsks waits={heldFor(props.permissions.waits, session.id).filter((held) => held.request === wait.request)} onAnswer={props.permissions.onAnswer} busy={props.permissions.busy} />,
        })] : [];
      case 'gitAsk':
        return props.gitAsks ? [card(line, {
          presses: gitAsksFor(props.gitAsks.waits, session.id).filter((ask) => ask.id === wait.id).map((ask) => (
            <GitAsk key={ask.id} ask={ask} onAnswer={props.gitAsks!.onAnswer} busy={props.gitAsks!.busy} />
          )),
        })] : [];
      case 'answerAlone': {
        const alone = props.answers ? answerFor(props.answers.waits, session.id) : null;
        return alone && props.answers ? [card(line, {
          presses: <AnswerAlone wait={alone} nameOf={props.answers.nameOf} onAnswer={props.answers.onAnswer} onChangeRule={props.answers.onChangeRule} busy={props.answers.busy} />,
        })] : [];
      }
      case 'rule':
        return props.rules ? [card(line, {
          look: inConversation(toEvent?.rules?.[wait.id]),
          presses: (
            <ProposedRules
              proposals={rulesAsked(props.rules.proposals, session.id).filter((proposal) => proposal.id === wait.id)}
              busy={props.rules.busy} onAnswer={props.rules.onAnswer} heard={props.rules.heard} ask={ruleAsk}
            />
          ),
        })] : [];
      case 'helper':
        return props.helpers ? [card(line, {
          look: inConversation(toEvent?.helpers?.[wait.id]),
          presses: <HelperAsks {...props.helpers} asks={props.helpers.asks.filter((row) => row.id === wait.id)} />,
        })] : [];
      case 'handOver':
        return props.handOver ? [card(line, { what: t('work.handOverAsk.lead'), presses: <HandOverAgentAsk {...props.handOver} /> })] : [];
      case 'discard':
        return props.discards ? [card(line, {
          presses: <Discards discards={props.discards} onReview={props.onReview} onDiscard={props.onDiscardTree} ask={workAsk} busy={props.workBusy} />,
        })] : [];
      case 'step':
        return runWaits ? [card(line, {
          presses: (
            <RunWaitsHead
              waits={[wait]} steps={runWaits.steps} onWord={runWaits.onWord} onHeld={runWaits.onHeld} hold={runWaits.hold} release={runWaits.release}
              blocked={blockedOn} onTo={(key) => document.querySelector<HTMLElement>(`[data-wait="${key}"]`)?.focus()}
            />
          ),
        })] : [];
      default:
        // The work's line: its Accept, rebase, opinion and review are one `Lands`, drawn at the oldest of them.
        if (!WORK_WAITS.has(wait.kind) || worked || !props.lands) return [];
        worked = true;
        return [card({ ...line, key: 'work' }, {
          look: inConversation(toEvent?.work),
          presses: (
            <Lands
              session={session} lands={props.lands} landing={props.landing} onReview={props.onReview} onReviewRun={props.onReviewRun}
              onSayRebase={props.onSayRebase} review={props.review} behind={props.behind} opinion={props.opinion} toEvent={toEvent}
            />
          ),
        })];
    }
  });
  const parts = runWaits?.parts && partsOf(runWaits.parts.runs).length > 0 ? runWaits.parts : null;
  return {
    cards,
    onAnswer: (id, answer) => answers.get(id)?.(answer),
    foot: parts ? <RunParts runs={parts.runs} onQuest={parts.onQuest} /> : null,
  };
}
