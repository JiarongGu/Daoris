import { useEffect, useState } from 'react';
import {
  useAcceptUnderWay, type useChatTurns, useDiscardSessionTree, useDriver, useLandEntry, useLanding, useOpinionGate,
  useSessionDiff, type useSessionGroups, useUnderWay, useUnderWaySubject, type withCall,
} from '../../bridge/shell';
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
import type { FileOpen } from './preview';

/**
 * The attended session's work to land: what its tree offers, the review's and the second opinion's gates before it, the door to
 * its run's review where the Accept is pressed, its discard where the line holds it by content, and where the discard's ask is open,
 * in the head or at the foot, never both.
 */
export function useSessionLanding({
  notify, attended, here, grouping, talking, registry, chatTurns, quests, onOpenQuest, onReviewRun, attendedEntry, channelAsk,
  attend, openPreview, groups,
}: {
  notify: Notify;
  attended: Session | null;
  here: boolean;
  grouping: ReturnType<typeof withCall> | null;
  talking: boolean;
  registry: ReturnType<typeof useRegistry>['data'];
  chatTurns: ReturnType<typeof useChatTurns>;
  quests: Quest[] | undefined;
  onOpenQuest?: (quest: string) => void;
  /** Opens `review/<run>` over the session followed. */
  onReviewRun: (run: string) => void;
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
  // How the tree stands against its line, from the review's own query, asked only where something is offered or a conversation's turn has ended in its own tree.
  const reviewMoved = attended && endedAt !== null && endedAt > Date.parse(attended.updated) ? new Date(endedAt).toISOString() : attended?.updated;
  const reviewRead = useSessionDiff(driverOffers || turnEnded ? attended!.id : null, attended ? { updated: reviewMoved!, live: SESSION_ACTIVE.has(attended.state) } : null);
  const behind = reviewRead.data?.behind ?? null;
  const lands = driverOffers ?? conversationOffer({
    tree: talksInTree, turn: turnHeld, review: reviewRead.data, endedAt, readAt: reviewRead.dataUpdatedAt,
  });
  const ownBranch = attended && here ? grouping?.work?.branch ?? null : null;
  const landing = useLanding(lands ? attended!.id : null);
  const drawn = landing.data?.accept ?? null;
  const acceptsNow = useAcceptUnderWay(drawn?.run ?? null);
  const reviewRun = drawn && attended && here ? () => onReviewRun(drawn.run) : undefined;
  // The review's gate where it holds that work: the set-up step's record, whether Daoris still serves
  // its tab here, and the verdict's presses, each by the one owner; *Set it up* follows the session's own quest.
  const reviewActs = useReviewActs({ notify });
  const inReview = useDriver().data?.inReview;
  const gateHolds = reviewHolds(landing.data);
  const gateStep = gateHolds?.quest ? (quests ?? []).find((each) => each.id === gateHolds.quest) ?? null : null;
  const headReview: HeadReview | null = gateHolds && attended ? {
    step: gateStep,
    served: inReview && gateHolds.quest ? inReview.find((row) => row.quest === gateHolds.quest)?.served ?? false : null,
    busy: reviewActs.busy,
    acts: reviewActs.actsFor({
      state: reviewState(gateHolds.state),
      step: gateHolds.quest ?? null,
      record: gateStep,
      work: attended.quest ?? null,
      open: gateHolds.quest && onOpenQuest ? () => onOpenQuest(gateHolds.quest!) : undefined,
    }),
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
  const lineLandsNow = useUnderWaySubject('LAND_ENTRY', attendedEntry?.entry ?? null);
  const askLandsNow = useUnderWaySubject('LAND_ASK', channelAsk);
  const landsNow = acceptsNow || lineLandsNow || askLandsNow;
  const discardsNow = useUnderWay('DISCARD_SESSION_TREE', attended?.id ?? null);
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

  // Where the work's ask (*Discard branch…*) is open: in the head or at the foot, never both.
  const [workAsk, setWorkAsk] = useState<{ session: string; at: 'head' | 'foot' } | null>(null);
  const workAskAt = (at: 'head' | 'foot'): WorkAsk | undefined => (attended ? {
    open: workAsk?.session === attended.id && workAsk.at === at,
    onOpen: (open) => setWorkAsk((was) => (open
      ? { session: attended.id, at }
      : was?.session === attended.id && was.at === at ? null : was)),
  } : undefined);
  // How many of its line of work's parts offer work to land, which the together door's own Accept lands as one.
  const entryOffered = attendedEntry && here
    ? attendedEntry.parts.filter((part) => (groups ?? []).some((row) => row.session === part.session && row.lands)).length
    : 0;
  const landEntry = useLandEntry();

  return {
    lands, behind, ownBranch, landing, reviewRun, gateHolds, headReview, headOpinion, discards, discardHeld, landsNow,
    discardsNow, workAskAt, entryOffered, landEntry,
  };
}
