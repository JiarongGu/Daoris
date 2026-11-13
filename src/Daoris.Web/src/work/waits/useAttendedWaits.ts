import { useEffect, useState } from 'react';
import type { TFunction } from 'i18next';
import {
  type HandOverWord, useAnswerAlone, useAnswerPermission, useAnswerWaits, useApproveHandOver, useAttention, useDeclineHandOver,
  useDriver, useGitAnswer, usePermissionWaits, useRules, useSessionHandOverAsk, useSessionHelpers, useSessionPartners, type useSay,
  useTightenSessionGitAccess,
} from '../../bridge/shell';
import type { SessionGitAccessHold } from '../head/GitAccessFact';
import type { Session } from '../../data/api';
import type { Notify } from '../../design/ui';
import { sentence } from '../../locales/format';
import type { AnchorState, TailState } from '../conversation/SessionConversation';
import { useAwayFromTop } from '../conversation/followTail';
import type { useBoxWords } from '../conversation/useBoxWords';
import type { ViewId } from '../frame/placements';
import type { Answered } from '../InlineConfirm';
import type { PieceSection } from '../pieces/Pieces';
import type { useSessionLanding } from '../review/useSessionLanding';
import type { boxOf } from '../say';
import type { useSessionActs } from '../sessionActs';
import type { HandOverRoster } from '../start/HandOverAsk';
import { answerAloneSaid, type AnswerWaits } from './AnswerAlone';
import { type AnchorWait, waitEvents } from './anchors';
import type { GitAskWaits } from './GitAsk';
import { handOverRefused, type HandOverAskWait } from './HandOverAgentAsk';
import { permissionSaid, type PermissionWaits } from './PermissionAsks';
import type { RuleAsk } from './ProposedRules';
import { useRuleAnswer } from './ruleAnswer';
import type { WaitProps } from './WaitStrip';
import { useWaitPanelHeight } from './waitPanelHeight';
import { waitsOf } from './waits';
import { useWaitsOnScreen } from './waitsOnScreen';
import { useTeam } from '../team/useTeam';
import { leadsPieces, takesPartner } from '../identity';

/**
 * What waits on the attended session's person: its helpers, pieces, hand-over, tool calls, answers
 * alone, git go-aheads and proposed rules, gathered into one object for the head and the strip at the foot alike; and where
 * each stands on the screen, the way to it, and the conversation's presses above the waiting panel.
 */
export function useAttendedWaits({
  t, notify, attended, here, intake, agentRoster, actions, chosen, attend, box, say, answering, question, goAheads, parkAsk, onAnswerAsk,
  boxWords, openView, work, talking, centre, onChangeRule, pieceAsking = null, setPieceAsking,
}: {
  /** The door to the place whose supervisor rule held an answer: *Change the rule…* on the head and the strip; absent, it is not drawn. */
  onChangeRule?: (place: string) => void;
  /** The session whose Pieces section has its ask open, the head's ⋯'s and the section's one state; absent, the section holds it. */
  pieceAsking?: string | null;
  setPieceAsking?: (session: string | null) => void;
  t: TFunction;
  notify: Notify;
  attended: Session | null;
  here: boolean;
  intake: boolean;
  agentRoster: HandOverRoster;
  actions: ReturnType<typeof useSessionActs>;
  chosen: () => void;
  attend: (id: string) => void;
  box: ReturnType<typeof boxOf>;
  say: ReturnType<typeof useSay>;
  answering: boolean;
  question: WaitProps['question'];
  goAheads: WaitProps['goAheads'];
  /** The ask the park's go-aheads are on, which keys each as *What needs you* does. */
  parkAsk: string | null;
  onAnswerAsk?: (ask: string) => void;
  boxWords: ReturnType<typeof useBoxWords>;
  openView: (view: ViewId) => void;
  work: ReturnType<typeof useSessionLanding>;
  talking: boolean;
  centre: Parameters<typeof useWaitsOnScreen>[0];
}) {
  const { resolve, parkGoAhead, personDone, onResolve, answerFromCard, choiceFromCard, parkPicked, onParkPick, onGoAhead } = boxWords;
  const {
    lands, headReview, behind, headOpinion, landing, reviewRun, discards, discardHeld, landsNow, discardsNow,
  } = work;
  // The rules agents proposed, the band's own read, and their answer's one owner: the attended session's own open
  // ones are a wait where the person talks to it. The machine's file, so a browser has none.
  const ruleState = useRules();
  const ruleAnswer = useRuleAnswer({ notify });
  // The helpers the attended session asked for: this machine's own files, so only for its own record. Its head
  // lists those still on their way, with the person's yes to an agent's ask that waits on it and a door to each helper.
  const helperAsks = useSessionHelpers(attended && here ? attended.id : null);
  // The pieces it asked other agents to write: this machine's own files, so only for its own record. A
  // session working in a tree of its own leads them, driven or a conversation; a partner, a helper, a reviewer or an intake leads none.
  const leads = Boolean(attended && here && leadsPieces(attended));
  const takes = Boolean(attended && here && takesPartner(attended));
  const partnerPieces = useSessionPartners(attended && here ? attended.id : null);
  // Its agent's own ask to hand it over: this machine's own files, so only for its own record.
  const handOverAsk = useSessionHandOverAsk(attended && here ? attended.id : null);
  const approveHandOver = useApproveHandOver();
  const declineHandOver = useDeclineHandOver();
  // The head's helpers: the asks read above, the yes and the no through the acts' one owner, and a door to each helper.
  // Its team: the lead, the pass, *Bring in another agent…*'s doors, and a member's thread, each on this machine's files.
  const team = useTeam({
    attended, here,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    attend: (id: string) => { chosen(); attend(id); },
    open: (door) => { if (attended) actions.run(door, { session: attended }); },
  });
  const helperWaits = attended && here && helperAsks.data ? {
    team,
    asks: helperAsks.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onApprove: (ask: string, answered: Answered) => actions.approveHelperNow(attended.id, ask, answered),
    onDecline: (ask: string, answered: Answered) => actions.declineHelperNow(attended.id, ask, answered),
    onOpen: (helper: string) => {
      chosen();
      attend(helper);
    },
    busy: actions.approvingHelper,
  } : null;
  // Its pieces: the pieces read above, the ask where it may lead one, a door to each
  // partner and a working piece's stop, through the acts' one owner.
  const pieceSection: PieceSection | null = attended && here && (leads || (partnerPieces.data?.length ?? 0) > 0) ? {
    pieces: partnerPieces.data ?? [],
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    ask: takes ? {
      from: attended.door,
      roster: agentRoster,
      busy: actions.askingPartner,
      onAsk: (asked, answered) => actions.askPartnerNow(attended, asked, answered),
      ...(setPieceAsking ? {
        open: pieceAsking === attended.id,
        onOpen: (open: boolean) => setPieceAsking(open ? attended.id : null),
      } : {}),
    } : undefined,
    onOpen: (partner: string) => {
      chosen();
      attend(partner);
    },
    onStop: (partner: string, answered: Answered) => actions.stopPartnerNow(partner, answered),
    stopping: actions.stopping,
    onAct: (piece: string, act: 'discard' | 'hand-over', answered: Answered) => actions.actPieceNow(attended, piece, act, answered),
    acting: actions.actingOnPiece,
  } : null;
  // Its agent's own ask to hand it over, with the person's yes and no: a wait at the foot while the look
  // holds it on their word. A refusal is said beside the presses, in the page's words for its code.
  const handOverWord = (answer: HandOverWord, answered: Answered) =>
    (answer.stands ? answered.done() : answered.refused(handOverRefused(t, answer.why, answer.message)));
  const handOverWait: HandOverAskWait | null = attended && here && handOverAsk.data ? {
    ask: handOverAsk.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onApprove: (ask, answered) => approveHandOver.mutate({ session: attended.id, ask }, {
      onSuccess: (answer) => handOverWord(answer, answered),
      onError: (error) => answered.refused(sentence(error)),
    }),
    onDecline: (ask, answered) => declineHandOver.mutate({ session: attended.id, ask }, {
      onSuccess: (answer) => handOverWord(answer, answered),
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: approveHandOver.isPending || declineHandOver.isPending,
  } : null;
  // The tool calls the attended session holds for the person's answer under asking: the driver's own
  // list, this machine's record alone, asked every few seconds while it runs (the routes publish nothing), and the answer's one
  // owner, so the head and the foot press the same. A refusal of the answer is said where it is pressed, in the catalogue's words.
  const permissionWaits = usePermissionWaits(Boolean(attended && here && (attended.state === 'working' || attended.state === 'starting')));
  const answerPermission = useAnswerPermission();
  const heldPermissions: PermissionWaits | null = attended && here && permissionWaits.data ? {
    waits: permissionWaits.data,
    onAnswer: (held, option, answered) => answerPermission.mutate({ session: held.session, request: held.request, option }, {
      onSuccess: (answer) => (answer.answered ? answered.done() : answered.refused(permissionSaid(t, answer.why, answer.message))),
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: answerPermission.isPending,
  } : null;
  // The answers the answers-alone bound holds for the person: read off this
  // machine's marks under the sessions' key, and the answer's one owner, so the head and the foot press the same. Its answer said where
  // the press is made; a refusal in the catalogue's words.
  const answerWaits = useAnswerWaits();
  const answerAlone = useAnswerAlone();
  const heldAnswers: AnswerWaits | null = attended && here && answerWaits.data ? {
    waits: answerWaits.data,
    nameOf: (agent: string) => agentRoster.names?.[agent] ?? agent,
    onAnswer: (wait, goOn, answered) => answerAlone.mutate({ session: wait.session, goOn }, {
      onSuccess: (answer) => {
        if (!answer.answered) {
          answered.refused(answerAloneSaid(t, answer.why, answer.message));
          return;
        }
        notify(t(goOn ? 'work.wait.answer.wentOn' : 'work.wait.answer.kept'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: answerAlone.isPending,
    onChangeRule,
  } : null;
  // The git go-aheads its git door holds: the rows What needs you is pushed, so the head, the foot and the
  // band read one judgement and press one door; its answer said where it is pressed, a refusal in the driver's words.
  const attention = useAttention();
  const gitAnswer = useGitAnswer();
  const heldGitAsks: GitAskWaits | null = attended && here && attention.state === 'known' ? {
    waits: attention.rows.flatMap((row) => (row.kind === 'git-ask' && row.gitAsk ? [{ id: row.id, since: row.since, gitAsk: row.gitAsk }] : [])),
    onAnswer: (ask, allow, answered) => gitAnswer.mutate({ ask: ask.id, answer: allow ? 'allow' : 'refuse' }, {
      onSuccess: (answer) => {
        notify(t(!allow ? 'work.wait.gitAsk.refused' : answer.waiting ? 'work.wait.gitAsk.allowed' : 'work.wait.gitAsk.kept'));
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: gitAnswer.isPending,
  } : null;
  const tighten = useTightenSessionGitAccess();
  const driver = useDriver();
  const heldGitAccess: SessionGitAccessHold | null = attended && here && driver.data?.sessionGitAccess ? {
    records: driver.data.sessionGitAccess,
    onTighten: (session, access, answered) => tighten.mutate({ id: session, access }, {
      onSuccess: () => answered.done(),
      onError: (error) => answered.refused(sentence(error)),
    }),
    busy: tighten.isPending,
  } : null;

  // What waits on the person, handed to the head and to the strip at the foot alike: one object, so
  // a press in either place is the same press with the same pending, and its answer, the record read again, settles both.
  const waits: WaitProps | null = attended ? {
    session: attended,
    resolving: resolve.isPending || parkGoAhead.isPending || personDone.isPending,
    onResolve: here ? onResolve : undefined,
    // Its card's answer: through the box's say door, the one door that answers a park.
    onAnswerSession: here ? answerFromCard : undefined,
    onAnswerChoice: here && answering && question ? choiceFromCard : undefined,
    // The head's card's rows are the frame's selection too, so a row chosen there is chosen below.
    answerPick: here && answering && question ? { selected: parkPicked, onSelect: onParkPick } : undefined,
    onAnswerAsk: here ? onAnswerAsk : undefined,
    // The go-aheads it asked, answerable only where this machine can answer the park.
    goAheads,
    goAheadsOn: parkAsk,
    onGoAhead: here ? onGoAhead : undefined,
    // Its question, and whether the box below is the park's answer box, which carries every press of what it asks: then
    // the strip draws none of the park's waits, and the head lists them with no press. A parked chat's composer draws none of it.
    question,
    parkBox: answering,
    // The rules it proposed: settled through the band's own door, then, on an *Allow*, the session told so as its
    // next turn, which is composed with the rule. One that takes no words now (its box's line, an intake) is not told, and says so.
    rules: {
      proposals: ruleState.data?.proposals,
      onAnswer: here
        ? (proposal, accept, answered) => ruleAnswer.answer(proposal, accept, answered, intake || box.kind === 'line'
          ? null
          : { session: attended.id, say: (text) => say.mutateAsync({ id: attended.id, text }) })
        : undefined,
      busy: ruleAnswer.busy,
      heard: !intake && box.kind !== 'line',
    },
    // The helpers it asked for, with the yes and the no: an agent's ask the look holds waiting on the person's word is
    // one of its waits, at the head and at the foot alike.
    helpers: helperWaits,
    // Its agent's own ask to hand it over: one of its waits while the look holds it on the person's word, the strip's.
    handOver: handOverWait,
    // The tool calls it holds for the person's answer: one of its waits, at the head and at the foot alike.
    permissions: heldPermissions,
    // A third answer alone the bound holds for the person: one of its waits, at the head and at the foot alike.
    answers: heldAnswers,
    // A git go-ahead its git door holds: one of its waits, at the head and at the foot alike.
    gitAsks: heldGitAsks,
    // What each running session holds of git access, with the press that tightens it.
    gitAccess: heldGitAccess,
    // Its review, wherever the review stands: the move beside unlanded work.
    onReview: () => openView('outputs'),
    // Commits its tree holds, offered to land beside what it left; the Accept is the review page's, which `onReviewRun` opens.
    lands,
    review: headReview,
    behind,
    opinion: headOpinion,
    landing: landing.data,
    onReviewRun: lands ? reviewRun : undefined,
    onSayRebase: (text) => say.mutateAsync({ id: attended.id, text }).then((answer) => answer.sent),
    // Its discard, never forced from here (`discardHeld`).
    discards,
    onDiscardTree: discardHeld,
    // Under way for this session from either place or another window, never another session's press.
    workBusy: landsNow || discardsNow,
  } : null;

  // The waits that stand where the strip draws them, and the events that raised them, which the
  // conversation finds and tells; the way to each is handed to the head and the strip alike, and a raising block's *To the wait*
  // leads to its line where it is on screen.
  const answerBelow = answering || (talking && box.kind === 'chat');
  const standing = attended && here && waits ? waitsOf({ ...waits, answerBelow }) : [];
  // Where the box answers the park, its question and its go-aheads stand in the box, so *To the wait* leads there.
  const inTheBox = (wait: AnchorWait) => answering && (wait === 'question' || wait.startsWith('goAhead:'));
  const [anchored, setAnchored] = useState<(AnchorState & { session: string }) | null>(null);
  const toEvent = attended && anchored?.session === attended.id ? waitEvents(anchored.anchored, anchored.show) : undefined;
  // One wait in one place on the screen: while the head's waits are wholly on screen the
  // strip stands aside, and a raising block's *To the wait* leads to the head's line instead of the strip's.
  const waitsOnScreen = useWaitsOnScreen(centre);
  const [toWait, setToWait] = useState<{ session: string; wait: AnchorWait; n: number; at: 'head' | 'foot' | 'box' } | null>(null);
  // Spent once the person attends another: the strip drawn again on coming back takes no focus from it.
  useEffect(() => { setToWait(null); }, [attended?.id]);
  // To the head's line while it is on screen, where the strip stands aside, else the strip's; a park's question and
  // go-aheads to the box that answers them, which is always drawn. The head's *next* leads the same way.
  const goToWait = (anchor: AnchorWait) => {
    if (!attended) return;
    const at = inTheBox(anchor) ? 'box' : waitsOnScreen === true ? 'head' : 'foot';
    setToWait((was) => ({ session: attended.id, wait: anchor, n: (was?.n ?? 0) + 1, at }));
  };
  const seekAt = (at: 'head' | 'foot' | 'box') => (attended && toWait?.session === attended.id && toWait.at === at ? toWait : null);
  // And a rule's *Allow…*, by the proposal it asks about: open in the head or at the foot, never both.
  const [ruleAsk, setRuleAsk] = useState<{ session: string; id: string; at: 'head' | 'foot' } | null>(null);
  const ruleAskAt = (at: 'head' | 'foot'): RuleAsk | undefined => (attended ? {
    open: ruleAsk?.session === attended.id && ruleAsk.at === at ? ruleAsk.id : null,
    onOpen: (id) => setRuleAsk((was) => (id !== null
      ? { session: attended.id, id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
  // The waiting panel's height, kept per viewer; and the conversation's presses drawn above it, so neither is ever
  // under it: *Back to bottom* told by the conversation (`onTail`), *To the top* once the reader is down from the head.
  const waitPanel = useWaitPanelHeight();
  const [tail, setTail] = useState<TailState | null>(null);
  const awayFromTop = useAwayFromTop(centre, attended?.id);
  // *To the top*, above the waiting panel: the record's head, where every wait stands with its
  // reading, the focus with it.
  const toTop = () => {
    const element = centre.current;
    if (!element) return;
    // A test's DOM has no scrollTo on elements; a property set is the same place without the glide.
    if (typeof element.scrollTo === 'function') element.scrollTo({ top: 0, behavior: 'smooth' });
    else element.scrollTop = 0;
    element.querySelector<HTMLElement>('[data-record-head]')?.focus({ preventScroll: true });
  };

  return {
    partnerPieces, helperWaits, pieceSection, waits, answerBelow, standing, setAnchored, toEvent, waitsOnScreen, goToWait, seekAt,
    ruleAskAt, waitPanel, tail, setTail, awayFromTop, toTop,
  };
}
