import { useEffect, useRef, useState } from 'react';
import type { TFunction } from 'i18next';
import { toolOf } from '../../agents/tools';
import type { AgentDoors } from '../../agents/agentOfDoor';
import type { useActors } from '../../bridge/driver';
import type { useSessionAtWork } from '../../bridge/sessions/atWork';
import { useAccountUse, type useSessionOpenings, type useSessionWhere, useStopWaiting, useWorkPlan, type withCall } from '../../bridge/shell';
import type { Quest, Session } from '../../data/api';
import type { useAsks, useRegistry } from '../../data/queries';
import { SESSION_ACTIVE } from '../../design/ui';
import { sentence } from '../../locales/format';
import type { namer } from '../../settings/accounts/namer';
import { accountNames } from '../accountNames';
import { type ActFacts, actMenu, headerActs, offeredActs, primaryAct, stopAsk } from '../acts';
import type { useAttendedBox } from '../conversation/useAttendedBox';
import { type entryOfSession, nameOfEntry } from '../entries';
import { shownOf } from '../groups';
import type { heavyPartOf } from '../heavySlot';
import { heavyLine } from '../HeavySlotLine';
import { askName, linkedSessions, sessionTitle } from '../identity';
import { pauseAsk, wiredFor } from '../pausing/pausing';
import { PauseAsk } from '../pausing/WorkAsks';
import { partnersOf } from '../pieces/pieceRows';
import type { SessionDoors, useSessionActs } from '../sessionActs';
import { AskHelper } from '../start/AskHelper';
import { HandOverAsk, type HandOverRoster } from '../start/HandOverAsk';
import { handOverOffer, nextChoices } from '../start/handOverOffer';
import { HandOverOfferLine } from '../start/HandOverOfferLine';
import { anchorOf } from '../waits/anchors';
import { type ChatLimitHeld, chatLimitWaitSaid, limitedLine, limitWaitOf, limitWaitSaid } from '../waits/limitWait';
import { LimitWaitLine } from '../waits/LimitWaitLine';
import type { useAttendedWaits } from '../waits/useAttendedWaits';
import { type LineSupervision, supervisionChip } from './lineSupervision';
import { leadingAgentOf, nextPress, type SessionFactsOf, sessionFacts, sessionFactsTip } from './headFacts';
import type { HeadLink } from './headRows';
import type { AskOrigin } from './startedFrom';
import { DeleteAsk, SessionPageHead, StopAsk } from './SessionPageHead';
import type { useAttendedRecord } from './useAttendedRecord';
import type { useHeadAsks } from './useHeadAsks';

type Waits = ReturnType<typeof useAttendedWaits>;

/**
 * The attended session's page header: its acts by the one rule, the question it asks now under it, the
 * limit's wait and Daoris's offer to hand over, its name, its facts line and its *next*; and the sessions it continues and helps.
 */
export function useAttendedHead({
  t, language, attended, here, live, quest, grouping, registry, roster, sheets, accounts, nameOf, actions, doors, asks, actors, wiring,
  entryAsks, channelAsk, attendedEntry, openings, sessions, quests, branch, partnerPieces, agentRoster, waiting, following, withIt,
  atWorkHeld, heavyPart, heavyTitleOf, integration, standing, goToWait, chosen, rootOf, where, taking, supervision,
}: {
  /** The attended line's counts and its rules' acts: the head's chip where a rule holds the line or is one count from it. */
  supervision: LineSupervision;
  t: TFunction;
  language: string;
  attended: Session | null;
  here: boolean;
  live: boolean;
  quest: Quest | null;
  grouping: ReturnType<typeof withCall> | null;
  registry: ReturnType<typeof useRegistry>['data'];
  roster: Parameters<typeof accountNames>[0];
  /** The roster's sheet half, which says which agent each door is; absent where the roster names none. */
  sheets?: AgentDoors;
  accounts: Parameters<typeof limitWaitOf>[0]['answer'];
  nameOf: ReturnType<typeof namer>;
  actions: ReturnType<typeof useSessionActs>;
  doors: SessionDoors;
  asks: ReturnType<typeof useHeadAsks>;
  actors: ReturnType<typeof useActors>['data'];
  wiring: Parameters<typeof wiredFor>[0];
  entryAsks: ReturnType<typeof useAsks>['data'];
  channelAsk: string | null;
  attendedEntry: ReturnType<typeof entryOfSession>;
  openings: ReturnType<typeof useSessionOpenings>;
  sessions: Session[] | undefined;
  quests: Quest[] | undefined;
  branch: ReturnType<typeof useAttendedRecord>['branch'];
  partnerPieces: Waits['partnerPieces'];
  agentRoster: HandOverRoster;
  waiting: ReturnType<typeof useAttendedBox>['waiting'];
  following: Session | null;
  withIt: number;
  atWorkHeld: ReturnType<typeof useSessionAtWork>['data'];
  heavyPart: ReturnType<typeof heavyPartOf>;
  heavyTitleOf: (id: string) => string;
  integration: ReturnType<typeof useAttendedRecord>['integration'];
  standing: Waits['standing'];
  goToWait: Waits['goToWait'];
  chosen: () => void;
  rootOf: (repository: string) => string | null | undefined;
  where: ReturnType<typeof useSessionWhere>;
  taking: Record<string, boolean>;
}) {
  const {
    stopAsking, setStopAsking, deleteAsking, setDeleteAsking, handOverAsking, setHandOverAsking, helperAsking, setHelperAsking,
    handOverChosen, setHandOverChosen, offerDismissed, setOfferDismissed, pauseAsking, setPauseAsking,
  } = asks;
  // What *Try now* on a waiting line came to where it is refused, by the session it is pressed on.
  const [limitRefusal, setLimitRefusal] = useState<{ session: string; sentence: string } | null>(null);
  const accountUse = useAccountUse();
  // A conversation's limit wait, as its events hold it, told by the conversation that reads them.
  const [chatHeld, setChatHeld] = useState<{ session: string; held: ChatLimitHeld } | null>(null);
  const stopWaiting = useStopWaiting();
  const attendedFacts: ActFacts | null = attended
    ? { session: attended, quest, grouping, root: rootOf(attended.repository), where: where[attended.id] }
    : null;
  const headActs = attendedFacts ? offeredActs(attendedFacts, 'header').filter(actions.can) : [];
  // A driven line an account's limit cut off, every account it may run on cooling, says under its header
  // when it resumes by itself and how, with the person's ways out beside it: *Go on now with…* (the hand-over's ask), *Pause quest…*
  // and the account's *Try now*. Read from the accounts the page holds, never a guess: no walk that waits, no line.
  const attendedWorkspace = attended
    ? (registry ?? []).find((row) => row.repository === attended.repository)?.workspace || 'default'
    : 'default';
  const limitWait = attended && here && limitedLine(attended, quest?.status, Boolean(grouping?.pausedBy))
    ? limitWaitOf({ session: attended, workspace: attendedWorkspace, doors: roster, answer: accounts, now: new Date() })
    : null;
  // A conversation says its wait from its events alone (`chat.limit-waits`), worded by the walk
  // where the page holds it, else by the driver's own line. Its ways out: *Go on now with…*, *Stop…* while it runs or *Stop waiting*
  // once it ended, and *Try now*.
  const conversationHeld = attended && here && attended.kind === 'chat' && chatHeld?.session === attended.id ? chatHeld.held : null;
  const conversationWait = conversationHeld && attended
    ? limitWaitOf({ session: attended, workspace: attendedWorkspace, doors: roster, answer: accounts, now: new Date() })
    : null;
  const conversationLines = conversationHeld && attended
    ? chatLimitWaitSaid(t, {
      held: conversationHeld,
      wait: conversationWait,
      label: (account) => nameOf(attended.door, account),
      language,
      names: accountNames(roster),
      now: new Date(),
    })
    : null;
  const waitLines = limitWait && attended
    ? limitWaitSaid(t, limitWait, (account) => nameOf(attended.door, account))
    : conversationLines;
  const waitAccount = limitWait?.account ?? conversationWait?.account ?? attended?.profile ?? null;
  // The line, the same at the head and the foot: one element, its presses the same presses.
  const limitLine = (className: string) => (waitLines && attended ? (
    <LimitWaitLine
      // Keyed by the session and the place: a refusal said in one is never another's.
      key={`limit:${attended.id}`}
      className={className}
      lines={waitLines}
      busy={actions.handingOver || accountUse.isPending || stopWaiting.isPending}
      refusal={limitRefusal?.session === attended.id ? limitRefusal.sentence : null}
      onGoOnNow={headActs.includes('handOver') ? () => doors.handOver?.(attended.id) : undefined}
      onPause={limitWait && attended.quest ? () => doors.pause?.(attended.id, { scope: 'quest', id: attended.quest! }) : undefined}
      onStop={!limitWait && live && headActs.includes('stop') ? () => doors.stop?.(attended.id) : undefined}
      onStopWaiting={!limitWait && !live ? () => {
        setLimitRefusal(null);
        stopWaiting.mutate(attended.id, {
          onSuccess: (answer) => {
            if (!answer.stopped) setLimitRefusal({ session: attended.id, sentence: answer.message });
          },
          onError: (error) => setLimitRefusal({ session: attended.id, sentence: sentence(error) }),
        });
      } : undefined}
      onTryNow={() => {
        setLimitRefusal(null);
        accountUse.mutate(
          { door: attended.door, action: 'ready', ...(waitAccount ? { profile: waitAccount } : {}) },
          { onError: (error) => setLimitRefusal({ session: attended.id, sentence: sentence(error) }) },
        );
      }}
    />
  ) : null);
  // Daoris's offer to hand over: on a conversation this page offers the press on, when its own
  // account is cut or near its limit by its scope's near (never the agent's pace warning alone) and another agent
  // has an account the walk would take. No driver read.
  // Its order is the session's actor's, Build's for a driven line and Chat's for a conversation, where that
  // actor has an order of its own; the roster's order stands otherwise.
  const attendedActor = attended ? (actors ?? []).find((row) => row.id === (attended.kind === 'chat' ? 'chat' : 'build')) : undefined;
  const offer = attended && headActs.includes('handOver') && !offerDismissed.has(attended.id) && !waitLines
    ? handOverOffer({
      session: attended,
      workspace: attendedWorkspace,
      doors: roster,
      answer: accounts,
      now: new Date(),
      actor: attendedActor,
    })
    : null;
  const offerTo = offer ? roster.find((row) => row.door === offer.agent) : null;
  const asked = attended ? stopAsk(attended, quest) : null;
  // A pause's ask says what it stops from the driver's plan of the work; one that stops nothing applies at once.
  const pausing = pauseAsking && attended?.id === pauseAsking.session ? pauseAsking : null;
  const pausePlan = useWorkPlan(pausing?.target ?? null);
  const pauseWired = wiredFor(wiring, quest?.workspace ?? attended?.workspace);
  const pauseLines = pausePlan.plan ? pauseAsk(pausePlan.plan, { wired: pauseWired }) : null;
  const pauseQuiet = Boolean(pausing && pausePlan.plan && pauseLines === null);
  const pausedQuietly = useRef<string | null>(null);
  useEffect(() => {
    if (!pausing || !pauseQuiet) return;
    const key = `${pausing.target.scope}:${pausing.target.id}`;
    if (pausedQuietly.current === key) return;
    pausedQuietly.current = key;
    actions.work.pause(pausing.target, undefined, () => setPauseAsking(null));
  }, [pausing, pauseQuiet, actions.work]);
  useEffect(() => { if (!pausing) pausedQuietly.current = null; }, [pausing]);
  // Named as its row is: the entry's name, its ask's, where an ask began it, read under the rail's own key; otherwise
  // the record's own.
  const entryAsk = channelAsk ? (entryAsks ?? []).find((ask) => ask.id === channelAsk) : undefined;
  const originAskId = attended?.ask ?? channelAsk;
  const originAsk: AskOrigin | null = originAskId
    ? { id: originAskId, words: (entryAsks ?? []).find((ask) => ask.id === originAskId)?.sentence ?? null }
    : null;
  const attendedTitle = attended
    ? nameOfEntry(attendedEntry ?? undefined, entryAsk ? { [entryAsk.id]: askName(entryAsk) } : {})
      ?? sessionTitle(attended, quest, openings[attended.id])
    : '';
  // Whom it continues and whom a helper helps: the record's own links, joined to the sessions the frame holds, for
  // the head's facts and its *Details*. One the frame does not hold is said by its id, and its agent is not guessed.
  const linked = attended
    ? linkedSessions(attended, new Map((sessions ?? []).map((row): [string, Session] => [row.id, row])))
    : null;
  const headLink = (other: Session | null | undefined): HeadLink | null => (other ? {
    session: other,
    title: sessionTitle(other, other.quest ? (quests ?? []).find((row) => row.id === other.quest) : null, openings[other.id]),
  } : null);
  const continues = headLink(linked?.continues);
  const helps = headLink(linked?.helps);
  const headPrimary = primaryAct(headActs, grouping);
  // A right-click on the session's page offers its header's acts, in the order the header draws them.
  const pageMenu = attended && attendedFacts
    ? {
      label: attendedTitle,
      acts: actMenu(headerActs(headActs, headPrimary).all, t, (act) => actions.run(act, attendedFacts), actions.busy, attended),
    }
    : null;
  const attendedShown = attended ? shownOf(attended, grouping, taking[attended.id]) : null;
  // Its facts line by its state: the row's facts and the record's, so the head and the row agree.
  const headFactsOf: SessionFactsOf | null = attended && attendedShown ? {
    session: attended,
    shown: attendedShown,
    grouping,
    ownSignIn: roster.some((row) => row.door === attended.door),
    branch,
    nameOf,
    // The agent it continues by the roster's name for it, as the hand-over's ask names agents.
    continuesAgent: continues
      ? roster.find((row) => row.door === continues.session.door)?.product || continues.session.door
      : null,
    leadingAgent: leadingAgentOf(attendedEntry, sessions ?? [], roster, sheets),
    helping: helps?.title,
    // Its partners, the agents its pieces went to by the roster's names, and how many work now.
    partners: partnersOf(partnerPieces.data ?? [], (agent) => agentRoster.names?.[agent] ?? agent),
    waitingSince: waiting?.session === attended.id ? waiting.since : undefined,
    calling: waiting?.session === attended.id ? waiting.calling : undefined,
    withIt: following?.id === attended.id ? withIt : 0,
    windowOpened: atWorkHeld && 'answer' in atWorkHeld ? atWorkHeld.answer.windowOpened : undefined,
    // *Trying again at* while a transient episode waits.
    retrying: waiting?.session === attended.id ? waiting.retrying : undefined,
    // Its heavy run said in place of the call, by its gate or whom it waits behind.
    heavy: heavyPart ? heavyLine(t, heavyPart, heavyTitleOf) : null,
    integration: integration.data ?? null,
  } : null;
  const nextWait = nextPress(standing);
  const nextAnchor = standing[0] ? anchorOf(standing[0]) : null;
  const pageHead = attended && attendedFacts && attendedShown && headFactsOf && (
    <SessionPageHead
      session={attended}
      title={attendedTitle}
      shown={attendedShown}
      facts={sessionFacts(t, headFactsOf)}
      // The line is one line, its running call said short; the whole line, the call's whole title in it, is its tip.
      factsTip={sessionFactsTip(t, headFactsOf)}
      next={nextWait && nextAnchor ? { press: nextWait, onPress: () => goToWait(nextAnchor) } : null}
      chip={supervisionChip(t, supervision)}
      acts={headActs}
      primary={headPrimary}
      busy={actions.busy}
      onAct={(act) => actions.run(act, attendedFacts)}
      asking={stopAsking === attended.id && live && asked ? (
        <StopAsk
          // Keyed by the session: an ask open on one session, with its wait or its refusal, is never another's.
          key={attended.id}
          sentence={t(asked.key, asked.values)}
          busy={actions.stopping}
          onStop={(answered) => actions.stopNow(attended, answered)}
          // A stop that lands after the person moved to another session closes only the ask it is pressed in.
          onClose={() => setStopAsking((was) => (was === attended.id ? null : was))}
        />
      ) : deleteAsking === attended.id && headActs.includes('delete') ? (
        <DeleteAsk
          key={attended.id}
          busy={actions.deleting}
          onDelete={(answered) => actions.deleteNow(attended, answered)}
          onClose={() => setDeleteAsking((was) => (was === attended.id ? null : was))}
        />
      ) : handOverAsking === attended.id && headActs.includes('handOver') ? (
        <HandOverAsk
          key={attended.id}
          from={attended.door}
          first={[...new Set(nextChoices(attended, roster, attendedActor).map((step) => step.door.door))]}
          working={SESSION_ACTIVE.has(attended.state)}
          roster={agentRoster}
          initial={handOverChosen?.choice}
          busy={actions.handingOver}
          onHandOver={(choice, told) => actions.handOverNow(attended, choice, told, handOverChosen?.why)}
          onClose={() => {
            setHandOverAsking((was) => (was === attended.id ? null : was));
            setHandOverChosen(null);
          }}
        />
      ) : helperAsking === attended.id && headActs.includes('askHelper') ? (
        <AskHelper
          key={attended.id}
          from={attended.door}
          roster={agentRoster}
          busy={actions.askingHelper}
          onAsk={(asked, answered) => actions.askHelperNow(attended, asked, answered)}
          onClose={() => setHelperAsking((was) => (was === attended.id ? null : was))}
        />
      ) : pausing && !pauseQuiet ? (
        <PauseAsk
          className="mt-2.5"
          target={pausing.target}
          lines={pauseLines}
          meanIt={t(pausing.target.scope === 'ask' ? 'asks.record.pauseMeanIt' : 'quests.detail.pauseMeanIt')}
          busy={actions.work.pausing}
          onPause={(answered) => actions.work.pause(pausing.target, answered)}
          onClose={() => setPauseAsking(null)}
        />
      ) : waitLines && attended ? (
        limitLine('mt-2.5')
      ) : offer && attended ? (
        <HandOverOfferLine
          // Keyed by the session: a refusal or a kept answer said in one is never another's.
          key={attended.id}
          offer={offer}
          actorName={attendedActor ? t(`work.role.name.${attendedActor.id}`) : undefined}
          agentName={offerTo?.product || offer.agent}
          accountName={offer.account ? nameOf(offerTo ? toolOf(offerTo) : offer.agent, offer.account) : null}
          busy={actions.handingOver}
          onGoOn={(told) => actions.handOverNow(
            attended,
            {
              agent: offer.agent, account: offer.account ?? '', name: offerTo?.product || offer.agent,
              model: offer.choice?.model, effort: offer.choice?.effort,
            },
            told,
            offer.why,
          )}
          onChoose={() => {
            chosen();
            setStopAsking(null);
            setDeleteAsking(null);
            setHelperAsking(null);
            setPauseAsking(null);
            setHandOverChosen({ why: offer.why, choice: { adapter: offer.agent, profile: offer.account ?? '' } });
            setHandOverAsking(attended.id);
          }}
          onDismiss={() => setOfferDismissed((was) => new Set(was).add(attended.id))}
        />
      ) : null}
    />
  );

  return { setChatHeld, limitLine, headLink, continues, helps, originAsk, pageMenu, pageHead };
}
