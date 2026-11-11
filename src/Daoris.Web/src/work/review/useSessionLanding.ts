import { type ComponentProps, useEffect, useState } from 'react';
import type { TFunction } from 'i18next';
import {
  drawnRevisions, useAcceptUnderWay, type useChatTurns, useDiscardSessionTree, useDriver, useEntryLanding, useLandEntry, useLanding, useOpinionGate,
  type useRunNodeWord, useSessionDiff, type useSessionGroups, useUnderWay, useUnderWaySubject, type withCall,
} from '../../bridge/shell';
import type { NodeWordAnswer } from '../../workflow/run';
import type { Quest, Session } from '../../data/api';
import type { useRegistry } from '../../data/queries';
import { type Notify, SESSION_ACTIVE } from '../../design/ui';
import { sentence } from '../../locales/format';
import { opinionAsked } from '../gates/opinion';
import { useOpinionActs } from '../gates/opinionActs';
import { reviewHolds, reviewState } from '../gates/review';
import { useReviewActs } from '../gates/reviewActs';
import type { HeadOpinion, HeadReview, WorkAsk } from '../head/SessionHead';
import type { entryOfSession } from '../entries';
import type { Answered } from '../InlineConfirm';
import { ownTree } from '../identity';
import { conversationOffer } from '../waits/waits';
import type { EntryAccept } from './EntryAccept';
import type { FileOpen } from './preview';

/**
 * Landing the attended session's work: what its tree offers and where it would put it, the
 * review's and the second opinion's gates before it, its discard where the line holds it by content, its line of work's one
 * Accept, and where the work's ask is open, in the head or at the foot, never both.
 */
export function useSessionLanding({
  t, notify, attended, here, grouping, talking, registry, chatTurns, quests, onOpenQuest, runNodeWord, attendedEntry, channelAsk,
  attend, openPreview, groups,
}: {
  t: TFunction;
  notify: Notify;
  attended: Session | null;
  here: boolean;
  grouping: ReturnType<typeof withCall> | null;
  talking: boolean;
  registry: ReturnType<typeof useRegistry>['data'];
  chatTurns: ReturnType<typeof useChatTurns>;
  quests: Quest[] | undefined;
  onOpenQuest?: (quest: string) => void;
  runNodeWord: ReturnType<typeof useRunNodeWord>;
  attendedEntry: ReturnType<typeof entryOfSession>;
  channelAsk: string | null;
  attend: (id: string) => void;
  openPreview: (file: FileOpen) => void;
  groups: ReturnType<typeof useSessionGroups>['data'];
}) {
  // What its own tree offers to land, whatever its ending, as the driver's reader says it, and where accepting would
  // put it: the review's own plan, asked only where there is something to land, under the review's key.
  const driverOffers = attended && here && grouping?.lands ? grouping.lands : null;
  // An open conversation's record stays `working` between turns, so the driver's look offers its tree
  // nothing; once its turn has ended, by the driver's word on its turns, the review's own answer says what is beyond the line.
  const talksInTree = !driverOffers && talking && attended
    ? ownTree(attended, (registry ?? []).find((row) => row.repository === attended.repository)?.root)
    : null;
  const turnHeld = talksInTree ? chatTurns[attended!.id] : undefined;
  const turnEnded = Boolean(turnHeld && !turnHeld.taking && turnHeld.queued.length === 0);
  // When this frame saw that turn end, or first saw the conversation at rest: its record need not move with a turn, so the review is
  // read again from then, and an answer read before it, another turn's, offers nothing meanwhile.
  const [restsSince, setRestsSince] = useState<{ session: string; at: number } | null>(null);
  useEffect(() => {
    setRestsSince(turnEnded && attended ? { session: attended.id, at: Date.now() } : null);
  }, [attended?.id, turnEnded]);
  const endedAt = restsSince && restsSince.session === attended?.id ? restsSince.at : null;
  // How the tree stands against its line, from the review's own answer: the same query the review reads, so the
  // head offers no *Accept…* where the review says nothing is beyond the line. Asked only where there is something offered, or a
  // conversation's turn has ended in a tree of its own, whose read is as fresh as that end.
  const reviewMoved = attended && endedAt !== null && endedAt > Date.parse(attended.updated) ? new Date(endedAt).toISOString() : attended?.updated;
  const reviewRead = useSessionDiff(driverOffers || turnEnded ? attended!.id : null, attended ? { updated: reviewMoved!, live: SESSION_ACTIVE.has(attended.state) } : null);
  const behind = reviewRead.data?.behind ?? null;
  const lands = driverOffers ?? conversationOffer({
    tree: talksInTree, turn: turnHeld, review: reviewRead.data, endedAt, readAt: reviewRead.dataUpdatedAt,
  });
  // The branch a moved tree is on: the head names it where the clean-up lists no `daoris/<folder>` row for it.
  const ownBranch = attended && here ? grouping?.work?.branch ?? null : null;
  // The branch name typed in the head's land ask, so its plan is asked again with it and a refusal shows first.
  const [landName, setLandName] = useState('');
  const landing = useLanding(lands ? attended!.id : null, landName);
  // Another session's page is another ask: a name typed for one is not carried to the next.
  useEffect(() => setLandName(''), [attended?.id]);
  // A chat run's press refused for what the page can word says it in the reader's language; any
  // other refusal is the driver's sentence.
  const pressRefusal = (result: NodeWordAnswer) =>
    result.press && ['busy', 'elsewhere', 'moved', 'asked'].includes(result.press) ? t(`work.review.chatPress.${result.press}`) : result.message ?? '';
  // An Accept's answer told back to the press that made it, *Accept…*'s and *Accept without the look…*'s alike.
  const landed = (answered: Answered) => ({
    onSuccess: (result: NodeWordAnswer) => {
      if (!result.done) {
        answered.refused(pressRefusal(result));
        return;
      }
      notify(result.message ?? '');
      answered.done();
    },
    onError: (error: unknown) => answered.refused(sentence(error)),
  });
  // The Accept is the review record's word on the record the plan drew, carrying the second opinion's token; none where the run holds no review.
  const drawn = landing.data?.accept ?? null;
  const acceptsNow = useAcceptUnderWay(drawn?.run ?? null);
  const accept = drawn ? (answered: Answered, answers?: string | null) => runNodeWord.mutate({
    run: drawn.run, node: drawn.node, act: 'accept', words: null, attempt: drawn.attempt, revision: drawn.revision, answers,
  }, landed(answered)) : undefined;
  // The review's gate where it holds that work: the set-up step's record, whether Daoris still serves
  // its tab here, and the verdict's presses, each by the one owner; *Set it up* follows the session's own quest.
  const reviewActs = useReviewActs({ notify });
  const inReview = useDriver().data?.inReview;
  const gateHolds = reviewHolds(landing.data);
  const gateStep = gateHolds?.quest ? (quests ?? []).find((each) => each.id === gateHolds.quest) ?? null : null;
  const headReview: HeadReview | null = gateHolds && attended ? {
    step: gateStep,
    served: inReview && gateHolds.quest ? inReview.find((row) => row.quest === gateHolds.quest)?.served ?? false : null,
    busy: reviewActs.busy || runNodeWord.isPending,
    acts: {
      ...reviewActs.actsFor({
        state: reviewState(gateHolds.state),
        step: gateHolds.quest ?? null,
        record: gateStep,
        work: attended.quest ?? null,
        open: gateHolds.quest && onOpenQuest ? () => onOpenQuest(gateHolds.quest!) : undefined,
      }),
      // A chat run's look is a node of its graph: the person's skip of it, then the one Accept.
      ...(gateHolds.chatRun && gateHolds.lookNode && here ? {
        withoutLook: (words: string | null, answered: Answered) => runNodeWord.mutate(
          { run: gateHolds.chatRun!, node: gateHolds.lookNode!, act: 'skip', words },
          {
            onSuccess: (skipped) => {
              if (!skipped.done) { answered.refused(skipped.message ?? skipped.refused ?? ''); return; }
              if (accept) accept(answered);
              else answered.done();
            },
            onError: (error: unknown) => answered.refused(sentence(error)),
          }),
      } : {}),
    },
  } : null;
  // The second opinion's gate before it: its findings beside their answers, read only
  // where a level asks one, and its presses by the one owner; a finding's place opens the side bar's preview.
  const opinionActs = useOpinionActs({ notify });
  const secondOpinion = opinionAsked(landing.data);
  const opinionRead = useOpinionGate(secondOpinion && attended ? attended.id : null);
  const headOpinion: HeadOpinion | null = secondOpinion && attended ? {
    detail: opinionRead.data?.detail ?? null,
    busy: opinionActs.busy,
    acts: opinionActs.actsFor({ session: attended.id, gate: secondOpinion, openSession: attend }),
    onOpenFile: openPreview,
  } : null;
  // Where the line holds its commits by content, no landing but its discard, the review's own, unforced.
  const discards = attended && here && !lands && grouping?.discards ? grouping.discards : null;
  const discardTree = useDiscardSessionTree();
  // Its line of work's one Accept, or its ask's, lands it too: the head's own press waits while either is under way.
  const lineLandsNow = useUnderWaySubject('LAND_ENTRY', attendedEntry?.entry ?? null);
  const askLandsNow = useUnderWaySubject('LAND_ASK', channelAsk);
  const landsNow = acceptsNow || lineLandsNow || askLandsNow;
  const discardsNow = useUnderWay('DISCARD_SESSION_TREE', attended?.id ?? null);
  // Its one press, for the head and, in Accept's place, the review: never forced from here, a refusal the
  // driver's sentence inside the ask, and the review's Discard the door that asks twice.
  const discardHeld = discards && attended ? (answered: Answered) => discardTree.mutate({ id: attended.id }, {
    onSuccess: (result) => {
      if (!result.done) {
        answered.refused(result.message);
        return;
      }
      notify(result.message);
      answered.done();
    },
    onError: (error) => answered.refused(sentence(error)),
  }) : undefined;

  // Where the work's ask (*Accept…*, *Discard branch…*) is open: in the head or at the foot, never both. The session's
  // own press is that ask in the place of a part's.
  const [workAsk, setWorkAsk] = useState<{ session: string; at: 'head' | 'foot' | 'review' } | null>(null);
  const workAskAt = (at: 'head' | 'foot'): WorkAsk | undefined => (attended ? {
    open: workAsk?.session === attended.id && workAsk.at === at,
    onOpen: (open) => setWorkAsk((was) => (open
      ? { session: attended.id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
  // Its line of work's one Accept, where its parts offer work to land and
  // more than one does or the attended part has none of its own; the plan reads git for every part, so it is asked only once the ask
  // is open.
  const entryOffered = attendedEntry && here
    ? attendedEntry.parts.filter((part) => (groups ?? []).some((row) => row.session === part.session && row.lands)).length
    : 0;
  const entryAccepts = attendedEntry && (entryOffered > 1 || (entryOffered === 1 && !lands)) ? attendedEntry.entry : null;
  const entryPlan = useEntryLanding(entryAccepts, entryAccepts !== null && Boolean(attended && workAsk?.session === attended.id));
  const landEntry = useLandEntry();
  // Under way in any window: this one's press from its send, another's from the driver's telling.
  const entryLandsNow = useUnderWaySubject('LAND_ENTRY', entryAccepts);
  const entryBase: ComponentProps<typeof EntryAccept> | undefined = entryAccepts && attendedEntry ? {
    scope: 'entry',
    offered: entryOffered,
    open: false,
    onOpen: () => {},
    plans: entryPlan.data ? [entryPlan.data] : null,
    reading: entryPlan.isFetching && !entryPlan.data,
    busy: entryLandsNow,
    numberOf: (session) => attendedEntry.parts.findIndex((part) => part.session === session) + 1 || null,
    // Told back to its ask: every part landed, or the driver's sentence of what landed and what is refused, said inside it.
    onLand: (leaveOut, answered) => landEntry.mutate({ entry: entryAccepts, parts: entryOffered, leaveOut, revisions: drawnRevisions(entryPlan.data) }, {
      onSuccess: (result) => {
        if (!result.done) {
          answered.refused(result.message);
          return;
        }
        notify(result.message);
        answered.done();
      },
      onError: (error) => answered.refused(sentence(error)),
    }),
  } : undefined;
  // The press where it is drawn: its ask open there, and held where the ask is open in another place.
  const entryAt = (at: 'head' | 'foot' | 'review'): ComponentProps<typeof EntryAccept> | undefined => {
    if (!entryBase || !attended) return undefined;
    const mine = workAsk?.session === attended.id ? workAsk.at : null;
    return {
      ...entryBase,
      open: mine === at,
      held: mine !== null && mine !== at,
      onOpen: (open) => setWorkAsk((was) => (open
        ? { session: attended.id, at }
        : was?.session === attended.id && was.at === at ? null : was)),
    };
  };
  const entryAccept = entryAt('head');

  return {
    lands, behind, ownBranch, setLandName, landing, accept, gateHolds, headReview, headOpinion, discards, discardHeld, landsNow,
    discardsNow, workAskAt, entryOffered, landEntry, entryAt, entryAccept,
  };
}
