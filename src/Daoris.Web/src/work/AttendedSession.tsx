import { type ComponentProps, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../data/api';
import { answeredPark, Button, EmptyState, shownState } from '../design/ui';
import type { ChainStep } from '../map/chain';
import { ChainLine } from '../map/ChainLine';
import { ChainStrip } from '../map/ChainStrip';
import type { SweepBranch } from '../settings/Sweep';
import type { AccountNamer } from '../agents/tools';
import type { QuestClose, Resolution } from './waits/AwaitingPerson';
import { HowItCameToBe, type TraceDoor } from './head/HowItCameToBe';
import { type AskOrigin, startingOf } from './head/startedFrom';
import type { MilestoneUnit } from './head/milestone';
import { WorkMilestone } from './head/WorkMilestone';
import type { Answered } from './InlineConfirm';
import type { SessionEntry } from './entries';
import { PartsFold } from './head/PartsFold';
import type { PartFacts } from './head/PartsStrip';
import type { SessionProcess } from './head/process';
import type { DetailRow } from './head/DetailsFold';
import { FinishedRecord } from './head/FinishedRecord';
import type { SessionRecord } from './head/sessionRecord';
import type { HelperWaits } from './waits/HelperAsks';
import { SessionHead } from './head/SessionHead';
import type { HeadLink } from './head/headRows';
import { RelatedSessions, type RelatedOwn, type RelatedRow } from './head/RelatedSessions';
import { TogetherDoor } from './head/TogetherDoor';
import { SessionMemory } from './head/SessionMemory';
import { SessionTimeline } from './head/SessionTimeline';
import type { PieceSection } from './pieces/Pieces';

/**
 * The one session the person is attending: its record, and what is observed about it.
 *
 * @remarks
 * **The stream is not here.** It lives in an **output panel** the person can grow, shrink and
 * hide, which is the one thing a well inside a region can never be.
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
 * so the timeline leaves it out rather than saying it twice. ONE rule for the column and the dock:
 * the head shows a parked note only where it can be answered here, so the dock must not hide every
 * parked note, or another machine's parked note is shown nowhere on a wide window. A park the person answered shows its
 * answer in the head and not its note, so its note stays the timeline's.
 */
export const noteIsInTheHead = (session: Session, answerableHere: boolean) =>
  session.state === 'awaiting-person' && !answeredPark(session) && answerableHere;

export function AttendedSession({
  session, quest, ask, opening, brief, taking, lastTurn, onResolve, onAnswerAsk, onAnswerSession,
  chain = [], onSession, onQuest, onReview, related, relatedOwn, onRelated, together, memory, timeline = 'dock', branch, ownBranch, onDiscardBranch, discardingBranch,
  headed = false, trace, ownSignIn = false, nameOf,
  continues, helps, heavy, process, parts, helpers,
  gitAccess, supervision, milestone, record, onOpenBranch, pieces, waitingOn, terminalHold, supervisorCourse,
  placeHold, state,
}: {
  /** Passed straight through to the head: a parked line's hold on its checkout, with *Let others work here*. */
  placeHold?: Parameters<typeof SessionHead>[0]['placeHold'];
  /** Passed straight through to the head: the supervisor's decision on a point its rule referred, with *Overrule* while it is held. */
  supervisorCourse?: Parameters<typeof SessionHead>[0]['supervisorCourse'];
  /** Passed straight through to the head: what each running session holds of git access, with *Tighten…*. */
  gitAccess?: Parameters<typeof SessionHead>[0]['gitAccess'];
  /** Passed straight through to the head: the line's counts and the acts the person's rules made on it, *Details*' course and Supervisor line. */
  supervision?: Parameters<typeof SessionHead>[0]['supervision'];
  /** Passed straight through to the head: a terminal holding this conversation, with *Let go of the terminal*. */
  terminalHold?: Parameters<typeof SessionHead>[0]['terminalHold'];
  /** Its pieces, the frame's: handed to its head's Pieces section. Absent, none is drawn. */
  pieces?: PieceSection | null;
  /** What its line waits on: *At work*'s answer, handed to its head's *Waiting on*. Absent, none is drawn. */
  waitingOn?: Parameters<typeof SessionHead>[0]['waitingOn'];
  /** Passed straight through to the head: its state line, the driver's reader's (`stateLineOf`), said beside its title where no header says it. */
  state?: Parameters<typeof SessionHead>[0]['state'];
  /** Passed straight through to the head: the process its own tree runs under, *Details*' rows. */
  process?: SessionProcess | null;
  /** Passed straight through to the head: the helpers it asked for, with the person's yes. */
  helpers?: HelperWaits | null;
  /**
   * Its entry's parts: under the head, a strip of every record of the line of work it belongs to, the
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
   * Passed straight through to the head: the session it continues and the one a helper helps, where the frame holds them.
   * Its *Details* opens them with `onSession`.
   */
  continues?: HeadLink | null;
  helps?: HeadLink | null;
  /** Passed straight through to the head: *Details*' *Heavy gates*, where it holds or waits for its repository's slot. */
  heavy?: DetailRow | null;
  /** Its agent has accounts, so a record naming none ran on the tool's own sign-in, said in its head. */
  ownSignIn?: boolean;
  /**
   * What a person calls an account, from the roster the frame holds: its head's *Details*, its chain's
   * rows and its trace say each account by it. Absent, each record's id is said.
   */
  nameOf?: AccountNamer;
  /**
   * How it came to be, folded at the foot of its record, and the doors its chain opens by id: the page
   * holds the fold and the read. Absent where nothing can read the trace: a browser, which has no driver.
   */
  trace?: TraceDoor & { onSession?: (id: string) => void; onQuest?: (id: string) => void; onAsk?: (id: string) => void };
  /**
   * Where its work stands, at the foot of its record: the end reached, the result
   * kept, the clean-up and the recovery. Absent where the driver names no unit for it: a browser, a teammate's, or an answer that lacks it.
   */
  milestone?: MilestoneUnit | null;
  /** What is kept of the tree it ran in once the tree is let go. Absent while the tree stands or none is kept. */
  record?: SessionRecord | null;
  /**
   * How its work's landing branch landed: the milestone's door to that branch's page in Repositories, named by the
   * branch the unit's landing made. Absent where nothing opens a page there.
   */
  onOpenBranch?: (repository: string, branch: string) => void;
  /**
   * A page header above carries its state, its id and its acts, so the record head says neither
   * again. A window with no header (a detached session) keeps them in the head.
   */
  headed?: boolean;
  /** Open its review — the move beside work it left that no branch of the person's holds. */
  onReview?: () => void;
  /** The sessions beside it in its repository whose work meets its own, each a door to its head through `onSession`'s id. */
  related?: RelatedRow[];
  /** Set where the session's own tree is not read here: said once by the related line. */
  relatedOwn?: RelatedOwn | null;
  onRelated?: (id: string) => void;
  /** *Bring them together…* and the operation it is in, head or member. */
  together?: ComponentProps<typeof TogetherDoor>;
  /** Its memory changes and the person's decision on them. */
  memory?: ComponentProps<typeof SessionMemory>;
  /** Open a quest's record — a stop on the chain, or one it asked. Absent where there is nowhere to open it. */
  onQuest?: (quest: Quest) => void;
  /** The branch its own tree left, as the clean-up judges it — the head's. */
  branch?: SweepBranch | null;
  /** The branch a moved tree is on, passed straight to the head. */
  ownBranch?: string | null;
  /** Passed straight through to the head: that branch discarded once its tree is gone, told back to its ask. */
  onDiscardBranch?: (answered: Answered) => void;
  /** That discard is on its way. */
  discardingBranch?: boolean;
  /** The attended session, or null when the person has not chosen one. */
  session: Session | null;
  quest?: Quest | null;
  /** The ask it came from, with its words where the frame holds the record: *Started from* and the requirement it started with. */
  ask?: AskOrigin | null;
  /** What the person first said in it, where this machine holds its record — a conversation's name. */
  opening?: string | null;
  /** The brief its start is given, where its record keeps one: *Started with* shows it beside the quest's or ask's words. */
  brief?: string | null;
  /** Whether a turn is in flight, as the driver says — the head reads a chat between turns as idle. */
  taking?: boolean;
  /** When its last turn ended here, for the head's *moved*. */
  lastTurn?: string | null;
  /** Passed straight through to the head, which says where a parked session is answered where nothing can; its stop is the page header's. */
  onResolve?: (state: Resolution, note: string | null, close?: QuestClose, answered?: Answered) => void;
  /** Passed straight through too: where an intake's answer is, its ask. */
  onAnswerAsk?: (ask: string) => void;
  /** And the answer to a driven session that parked to ask the person. */
  onAnswerSession?: (answer: string) => Promise<string | null> | void;
  /**
   * The chain its quest belongs to, the same strip a quest's page shows. Rendered only
   * when there is one, like there: a lone quest has nothing before or after it to show.
   */
  chain?: ChainStep[];
  /** Attend another session of the chain — the frame's own selection, so every region follows. */
  onSession?: (session: Session) => void;
  /**
   * Where the timeline is: in the frame's dock, which is there at every width — or here,
   * in a window with no dock (a detached session).
   */
  timeline?: 'dock' | 'always';
}) {
  const { t } = useTranslation();
  // The chain as one line, or whole: folded on every session it opens on,
  // since a strip opened on one session would stand between the next one's head and its conversation.
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
    // 🔴 The centre's width, whatever it is: a 768px measure here would leave the record
    // at half a maximized window. The pill stays beside its title in the head instead.
    <article className="grid w-full content-start gap-2">
      <SessionHead
        session={session}
        quest={quest}
        ask={ask}
        opening={opening}
        brief={brief}
        taking={taking}
        lastTurn={lastTurn}
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
        heavy={heavy}
        process={process}
        onOpenSession={onSession}
        ownSignIn={ownSignIn}
        nameOf={nameOf}
        helpers={helpers}
        pieces={pieces}
        waitingOn={waitingOn}
        gitAccess={gitAccess}
        state={state}
        supervision={supervision}
        terminalHold={terminalHold}
        supervisorCourse={supervisorCourse}
        placeHold={placeHold}
      />
      {/* Its parts as a flowchart, the strip its rail. Folded to one line (the drawing alone is over the head's whole budget),
          keyed by the line of work, not the session: a press on a part shows another session of it, and the flow being read
          stays open. */}
      {parts && <PartsFold key={`parts:${parts.entry.entry}`} {...parts} />}
      {/* Every stop a door: a session attends, a quest opens.
          One line of stops by default, since the whole strip stands 350 to 450px between the head and
          the conversation, saying the head's title again; whole on a press. */}
      {chain.length > 1 && (chainWhole === session.id
        ? (
          <div className="grid justify-items-start gap-1">
            <ChainStrip chain={chain} level={3} attended={session.id} onQuest={onQuest} onSession={onSession} nameOf={nameOf} />
            <Button variant="ghost" className="px-1.5 py-0 text-small" onClick={() => showChain(false)}>{t('chain.hide')}</Button>
          </div>
        )
        : <ChainLine chain={chain} onQuest={onQuest} onExpand={() => showChain(true)} />)}
      {/* Who it worked with and its channel's messages are rows of its conversation, by their actors, so the one timeline reads them.
          🔴 Each fold keyed here names its kind beside the record's id: keyed by the id alone, two folds would take each other's
          fibers, and each draw of a running session would leave a copy of a line in the page. */}
      {((related && related.length > 0) || relatedOwn) && <RelatedSessions repository={session.repository} related={related ?? []} own={relatedOwn} onSession={onRelated} />}
      {together && <TogetherDoor {...together} />}
      {memory && <SessionMemory {...memory} />}
      {/* Where its work stands: the end, the result, the clean-up and the recovery, read from the driver, folded
          to its line, opened in place; keyed by the record so another one starts folded. */}
      {milestone && <WorkMilestone key={`milestone:${session.id}`} unit={milestone} />}
      {/* Its door to how the branch its landing made landed, a line beside the milestone and none of its own. */}
      {milestone && onOpenBranch && <LandedBranchDoor unit={milestone} onOpen={onOpenBranch} />}
      {/* What the service kept of its finished tree, shown once the tree is let go. */}
      {record && <FinishedRecord record={record} />}
      {/* Folded, a line above the conversation: nothing is read until the person opens it. */}
      {trace && <HowItCameToBe kind="session" id={session.id} {...trace} nameOf={nameOf} started={startingOf(quest, ask, session, opening, brief)} />}
      {/* The timeline lives in the right dock, which is there at every width, so a copy here
          would draw it twice. Only a window with no dock carries it. */}
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

/**
 * The milestone's door to how its landing branch landed: the branch the unit's landing made, as its result or its
 * entries name it, opened on its repository's page. A landing branch is the branch's whole story, so it is named by the branch and
 * never by the unit's session, which is any one of its executors. Nothing where the unit names no landing branch.
 */
function LandedBranchDoor({ unit, onOpen }: { unit: MilestoneUnit; onOpen: (repository: string, branch: string) => void }) {
  const { t } = useTranslation();
  const branch = [...unit.result, ...unit.entries].find((entry) => entry.kind === 'landing-branch')?.resource;
  if (!branch) return null;
  return (
    <div className="flex justify-start">
      <Button variant="ghost" className="min-h-0 px-1.5 py-0.5 text-small" onClick={() => onOpen(unit.repository, branch)}>
        {t('landing.door', { branch })}
      </Button>
    </div>
  );
}
