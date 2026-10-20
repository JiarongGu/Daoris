import { type ComponentProps, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { GoAhead, Quest, Session } from '../data/api';
import { ChannelPanel } from '../asks/ChannelSection';
import type { GoAheadAnswer } from '../asks/GoAheadList';
import { answeredPark, Button, EmptyState, shownState } from '../design/ui';
import type { ChainStep } from '../map/chain';
import { ChainLine } from '../map/ChainLine';
import { ChainStrip } from '../map/ChainStrip';
import type { SweepBranch } from '../settings/Sweep';
import type { AccountNamer } from '../agents/tools';
import type { QuestClose, Resolution } from './waits/AwaitingPerson';
import { HowItCameToBe, type TraceDoor } from './head/HowItCameToBe';
import type { WaitEvents } from './waits/anchors';
import type { MilestoneUnit } from './head/milestone';
import { WorkMilestone } from './head/WorkMilestone';
import type { Answered } from './InlineConfirm';
import type { SayRebase } from './review/LandingSync';
import type { SessionEntry } from './entries';
import { EntryAccept } from './review/EntryAccept';
import type { DiscardOffer, LandOffer } from './groups';
import { PartsFlow } from './head/PartsFlow';
import type { PartFacts } from './head/PartsStrip';
import type { SessionProcess } from './head/process';
import type { Relations } from './head/relations';
import type { LineBehind } from './review/diff';
import type { HelperWaits } from './waits/HelperAsks';
import type { PermissionWaits } from './waits/PermissionAsks';
import type { RuleAsk, RuleWaits } from './waits/ProposedRules';
import type { Seek } from './waits/WaitAnchor';
import { type HeadLink, type HeadOpinion, type HeadReview, type LandingPlan, SessionHead, type WorkAsk } from './head/SessionHead';
import { SessionRelations } from './head/SessionRelations';
import { SessionTimeline } from './head/SessionTimeline';

/**
 * The one session the person is attending: its record, and what was observed about it.
 *
 * @remarks
 * **The stream is not here, and that is D55.** The console came out of the quest drawer with
 * SURF4c and reached its home with SURF4d: an **output panel** the person can grow, shrink and
 * hide, which is the one thing a well inside a region can never be. `SessionConsole` itself never
 * changed — it was extracted once (SURF4a) and has been carried since.
 *
 * **The timeline sits beside the stream, never inside it.** One is what a tool printed and the
 * other is what Daoris observed, and folding them together is how the second stops being
 * trustworthy.
 *
 * **It is handed its session rather than fetching one**: the frame holds the selection, which is
 * what lets one choice bind the rail, this region and the panel at once.
 *
 * The composer sits beneath this region rather than inside it, for the same reason the panel does:
 * the record scrolls and the things you act with do not.
 */
/**
 * Whether a session's current note is already shown in its head — the parked card, with its moves —
 * so the timeline leaves it out rather than saying it twice. ONE rule for the column and the dock
 * (REV3): the dock hid every parked note, while the head shows one only where it can be answered
 * here, so another machine's parked note was shown nowhere on a wide window. A park the person answered shows its
 * answer in the head and not its note (ANSWER1c), so its note stays the timeline's.
 */
export const noteIsInTheHead = (session: Session, answerableHere: boolean) =>
  session.state === 'awaiting-person' && !answeredPark(session) && answerableHere;

export function AttendedSession({
  session, quest, opening, taking, lastTurn, resolving, onResolve, onAnswerAsk, onAnswerSession,
  chain = [], onSession, onQuest, onReview, relations, timeline = 'dock', branch, ownBranch, onDiscardBranch, discardingBranch,
  headed = false, goAheads, onGoAhead, trace, ownSignIn = false, nameOf, lands, landing, planning, onLand, onSayRebase, onName, discards, onDiscardTree, review, behind,
  opinion, continues, helps, process, parts, entryAccept, channel, workAsk, workBusy, toEvent, rules, ruleAsk, seek, helpers, permissions,
  milestone,
}: {
  /** Passed straight through to the head: the process its own tree runs under, *Details*' rows (PROCESS1e). */
  process?: SessionProcess | null;
  /** Passed straight through to the head: the helpers it asked for, with the person's yes (MIXAGENT2b). */
  helpers?: HelperWaits | null;
  /** Passed straight through to the head: the tool calls it holds for the person's answer, with a press per choice (AUTONOMY1b3c). */
  permissions?: PermissionWaits | null;
  /** Passed straight through to the head: the wait a raising block's *To the wait* leads to there (WAITFOOT2b). */
  seek?: Seek | null;
  /** Passed straight through to the head: the rules it proposed that the person still answers (PROPOSEHERE1b). */
  rules?: RuleWaits | null;
  /** And where a rule's *Allow…* is open, the frame's, the head's or the foot's (D168 point 3). */
  ruleAsk?: RuleAsk;
  /** Passed straight through to the head: where its work's ask is open, the frame's, the head's or the foot's (D168 point 3). */
  workAsk?: WorkAsk;
  /** And a landing or a discard of its work on its way, pressed in either place. */
  workBusy?: boolean;
  /** Passed straight through to the head: the way from each wait to the event that raised it (WAITFOOT1c, D168 point 4). */
  toEvent?: WaitEvents;
  /**
   * Its entry's parts (SESSONE1c, D167 point 5): under the head, a strip of every record of the line of work it belongs to, the
   * one shown marked, so the page's acts stay the part shown's and a press shows another. Absent where it is a record alone,
   * and from a host before entries.
   */
  parts?: {
    entry: SessionEntry;
    attended: string;
    rows: Readonly<Record<string, PartFacts | undefined>>;
    onShow: (session: string) => void;
  };
  /**
   * Its line of work's one Accept (ACCEPTALL1, D167's 2026-10-15 amendment), where more than one of its parts offers work to land:
   * the frame holds the plan and the press. Absent where one part or none does, whose own Accept is the head's.
   */
  entryAccept?: ComponentProps<typeof EntryAccept>;
  /**
   * Passed straight through to the head: the session it continues and the one a helper helps, where the frame holds them
   * (MIXAGENT3a-i). Its *Details* opens them with `onSession`.
   */
  continues?: HeadLink | null;
  helps?: HeadLink | null;
  /** Passed straight through to the head: what its own tree offers to land, as the driver's reader said it (LAND4). */
  lands?: LandOffer | null;
  /** And the review's gate where it holds that work (REVIEWENV1g): the set-up step, its tab and the verdict's presses. */
  review?: HeadReview | null;
  /** And how the tree stands against its line, from the review's answer (REVIEWBASE5): none beyond it, no *Accept…*. */
  behind?: LineBehind | null;
  /** And the second opinion's gate before it (XAGENT1g): its findings beside their answers, and its presses. */
  opinion?: HeadOpinion | null;
  /** And what it offers where the line holds its commits by content (SQUASHTIDY1b): no landing, its discard. */
  discards?: DiscardOffer | null;
  /** And that discard's press, the review's Discard unforced, told back to its ask. Absent where nothing can press it. */
  onDiscardTree?: (answered: Answered) => void;
  /** And where accepting would put it, the review's own plan (D87). */
  landing?: LandingPlan | null;
  /** And its press, the review's Accept, told back to its ask, with the second opinion's token where it answers it (XAGENT1g). */
  onLand?: (answered: Answered, answers?: string | null, branch?: string | null) => void;
  /** The press that says the rebase words where replaying conflicts (LANDSYNC1w). */
  onSayRebase?: SayRebase;
  /** The plan shown is the previous name's while the entered one is read (LANDNAME1b). */
  planning?: boolean;
  /** The branch name typed in the land ask (LANDNAME1b). */
  onName?: (name: string) => void;
  /** Its agent has accounts, so a record naming none ran on the tool's own sign-in (D125 §3.7), said in its head. */
  ownSignIn?: boolean;
  /**
   * What a person calls an account (ACCTNAME1, D152 §4.2), from the roster the frame holds: its head's *Details*, its chain's
   * rows and its trace say each account by it. Absent, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * How it came to be (TRACE1b, D143, D50), folded at the foot of its record, and the doors its chain opens by id: the page
   * holds the fold and the read. Absent where nothing can read the trace: a browser, which has no driver.
   */
  trace?: TraceDoor & { onSession?: (id: string) => void; onQuest?: (id: string) => void; onAsk?: (id: string) => void };
  /**
   * Where its work stands (FABRIC-MILESTONE1b, the fabric events design §D0), at the foot of its record: the end reached, the result
   * kept, the clean-up and the recovery. Absent where the driver names no unit for it: a browser, a teammate's, or an older shell.
   */
  milestone?: MilestoneUnit | null;
  /** The go-aheads it asked on its quest's ask, shown in the head while it is parked (KNOWUSE1a2). */
  goAheads?: GoAhead[];
  /** Passed straight through to the head: one of them answered, the park with it once none is open (KNOWUSE1a2, GOAHEAD2b). */
  onGoAhead?: GoAheadAnswer;
  /**
   * A page header above carries its state, its id and its acts (SESSUX1d, D126 §3.2), so the record head says neither
   * again. A window with no header (a detached session) keeps them in the head.
   */
  headed?: boolean;
  /** Open its review — the move beside work it left that no branch of the person's holds (SESS2 H4). */
  onReview?: () => void;
  /** Who it worked with beyond its chain (SESS1): the session that asked, and what it asked of others. */
  relations?: Relations;
  /**
   * Its ask's channel kept to its line of work (SESSONE2c, D167 points 6 to 8): as the shell answers it, the person's post and the
   * doors a line and the whole channel open. Absent for a conversation, an intake, a line rooted in a quest alone and a browser.
   */
  channel?: ComponentProps<typeof ChannelPanel>;
  /** Open a quest's record — a stop on the chain, or one it asked. Absent where there is nowhere to open it. */
  onQuest?: (quest: Quest) => void;
  /** The branch its own tree left, as the clean-up judged it (SESS1 S10) — the head's. */
  branch?: SweepBranch | null;
  /** The branch a moved tree is on (TREEFOLLOWWEB2), passed straight to the head. */
  ownBranch?: string | null;
  /** Passed straight through to the head: that branch discarded once its tree is gone (LAND3b), told back to its ask. */
  onDiscardBranch?: (answered: Answered) => void;
  /** That discard is on its way. */
  discardingBranch?: boolean;
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name (RAIL1). */
  opening?: string | null;
  /** Whether a turn is in flight, as the driver says — the head reads a chat between turns as idle (UX5 U17). */
  taking?: boolean;
  /** When its last turn ended here (RAIL2), for the head's *moved*. */
  lastTurn?: string | null;
  resolving?: boolean;
  /** Passed straight through to the head, where a parked session's moves live (design §4); its stop is the page header's. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /** Passed straight through too: where an intake's answer is, its ask (INT4g). */
  onAnswerAsk?: (ask: string) => void;
  /** And the answer to a driven session that parked to ask the person (STANDDOWN2). */
  onAnswerSession?: (answer: string | null) => void;
  /**
   * The chain its quest belongs to (MAP1), the same strip a quest's page shows. Rendered only
   * when there is one, like there: a lone quest has nothing before or after it to show.
   */
  chain?: ChainStep[];
  /** Attend another session of the chain — the frame's own selection, so every region follows. */
  onSession?: (session: Session) => void;
  /**
   * Where the timeline is: in the frame's dock, which since FRAME6 is there at every width — or here,
   * in a window with no dock (a detached session, REV3).
   */
  timeline?: 'dock' | 'always';
}) {
  const { t } = useTranslation();
  // The chain as one line, or whole: folded on every session it opens on (UX7c, D152 §7, amending SESS2 H7's remembered
  // choice), since a strip opened on one session stood between the next one's head and its conversation.
  const [chainWhole, setChainWhole] = useState<string | null>(null);
  const showChain = (whole: boolean) => setChainWhole(whole && session ? session.id : null);

  if (!session) {
    return (
      <EmptyState
        icon="inbox"
        headline={t('work.attended.none.headline')}
        body={t('work.attended.none.body')}
      />
    );
  }

  return (
    // 🔴 The centre's width, whatever it is (UX5 U16, the owner): a 768px measure here left the record
    // at half a maximized window. The pill stays beside its title in the head instead.
    <article className="grid w-full content-start gap-4">
      <SessionHead
        session={session}
        quest={quest}
        opening={opening}
        taking={taking}
        lastTurn={lastTurn}
        resolving={resolving}
        onResolve={onResolve}
        onAnswerAsk={onAnswerAsk}
        onAnswerSession={onAnswerSession}
        branch={branch}
        ownBranch={ownBranch}
        onReview={onReview}
        onDiscardBranch={onDiscardBranch}
        discardingBranch={discardingBranch}
        headed={headed}
        onOpenQuest={onQuest}
        continues={continues}
        helps={helps}
        process={process}
        onOpenSession={onSession}
        goAheads={goAheads}
        onGoAhead={onGoAhead}
        ownSignIn={ownSignIn}
        nameOf={nameOf}
        lands={lands}
        landing={landing}
        onLand={onLand}
        onSayRebase={onSayRebase}
        planning={planning}
        onName={onName}
        review={review}
        behind={behind}
        opinion={opinion}
        discards={discards}
        onDiscardTree={onDiscardTree}
        workAsk={workAsk}
        workBusy={workBusy}
        toEvent={toEvent}
        rules={rules}
        ruleAsk={ruleAsk}
        seek={seek}
        helpers={helpers}
        permissions={permissions}
      />
      {/* Its parts as a flowchart, the strip its rail (FLOWCHART1b); Accept's plan, once read, is where they would land. */}
      {parts && <PartsFlow {...parts} plan={entryAccept?.open ? entryAccept.plans?.[0] ?? null : null} />}
      {/* One Accept for every part with work to land (ACCEPTALL1, D167's 2026-10-15 amendment), under the strip that lists them. */}
      {entryAccept && <div className="grid justify-items-start gap-1"><EntryAccept {...entryAccept} /></div>}
      {/* Every stop a door (the study's §5: "every stop pressable"): a session attends, a quest opens.
          One line of stops by default, since the whole strip stood 350 to 450px between the head and
          the conversation, saying the head's title again (SESS2 H7); whole on a press. */}
      {chain.length > 1 && (chainWhole === session.id
        ? (
          <div className="grid justify-items-start gap-1">
            <ChainStrip chain={chain} level={3} attended={session.id} onQuest={onQuest} onSession={onSession} nameOf={nameOf} />
            <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={() => showChain(false)}>{t('chain.hide')}</Button>
          </div>
        )
        : <ChainLine chain={chain} onQuest={onQuest} onExpand={() => showChain(true)} />)}
      {relations && <SessionRelations relations={relations} onQuest={onQuest} onSession={onSession} />}
      {/* Its ask's channel kept to this line of work, folded to a line (SESSONE2c, D167 points 6 to 8). */}
      {channel && <ChannelPanel {...channel} />}
      {/* Where its work stands (FABRIC-MILESTONE1b): the end, the result, the clean-up and the recovery, read from the driver, folded
          to its line, opened in place (MILESTONEUX1); keyed by the record so another one starts folded. */}
      {milestone && <WorkMilestone key={session.id} unit={milestone} />}
      {/* Folded, a line above the conversation: nothing is read until the person opens it (TRACE1b). */}
      {trace && <HowItCameToBe kind="session" id={session.id} {...trace} nameOf={nameOf} />}
      {/* The timeline lives in the right dock since SURF6 gave the dock its second occupant. It stayed
          here on a narrow window while the dock was hidden there; FRAME6 keeps the dock at every width,
          and the copy here drew it twice. Only a window with no dock carries it. */}
      {timeline === 'always' && (
        <SessionTimeline
          session={session}
          quest={quest}
          shown={shownState(session, taking)}
          hideCurrentNote={noteIsInTheHead(session, Boolean(onResolve))}
        />
      )}
    </article>
  );
}
