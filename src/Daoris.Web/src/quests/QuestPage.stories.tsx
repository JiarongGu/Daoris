import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { buildChain } from '../map/chain';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { HistoryDoor } from '../work/history/history';
import { FAILED_PLAN, QUEST_ASKED, QUEST_PLAN } from '../work/history/historyFixtures';
import type { WorkDoor } from '../work/pausing/pausing';
import { ABANDONED_ENTRY, PAUSABLE_QUEST, PAUSED_QUEST } from '../work/pausing/pausingFixtures';
import { STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN } from '../work/gates/reviewFixtures';
import { answer, QUEST_CHAIN } from '../work/head/traceFixtures';
import { IN_REVIEW } from '../workflow/runFixtures';
import {
  ACCEPTED, BROUGHT_FORWARD, BROUGHT_FORWARD_UNCONFIRMED, CHAINED, CJK, CLOSED_EARLY, CLOSED_EARLY_ENDS, CONFLICTED, DECLINED, DELETABLE,
  DONE, DROPPED_FIRST, EXHAUSTED, FAILED, FINISHED_HERE, HELD, HELD_BY_PERSON, HELD_CJK, HELD_ELSEWHERE, LANED, LOST_AGAIN, LOST_CLOSED,
  LOST_TO_REOPEN, MET, MINE_LOST, OPEN, PART_CLOSED, PART_CLOSED_ENDS, PAUSED_ITSELF, PAUSED_WITH_ASK, PAUSED_WITH_QUEST, QUESTION, REFILED,
  REFILED_NAMED, REFILED_UNNAMED, REOPENED_HERE, REQUIRING, SETTLED_DROPPED, STARTING, STOPPED, TAKEN, TAKEN_ELSEWHERE, THEIRS_LOST, TRUST,
  WAITING, WAITS_FOR_ACCOUNT, WORKING,
} from './fixtures';
import { QuestPage, QuestsMainNotice } from './QuestPage';

// A quest's page (FRAME1d, D118 §3d) in Quests' main area, where it was a drawer: open, taken, sitting for the
// person's hold, held for the agent's trust with its question open, parked by its strikes, waiting on a question, a
// conflict, carrying links and files, a chain, a driven session's record, declined, done, a delete asking, a 中文
// title, its closure graph's end; and the main area with no record: nothing chosen, gone, loading.

const nothing = () => {};

/**
 * Dark, as a person chooses it (D66): a flex item of the main area's height frame, with no padding of its own, so the page's
 * scroll box stays bounded.
 */
const dark: Decorator = (Story) => <InTheme theme="dark" className="flex min-h-0 min-w-0 flex-1 p-0"><Story /></InTheme>;

const meta: Meta<typeof QuestPage> = {
  title: 'Quests/QuestPage',
  component: QuestPage,
  args: {
    quest: OPEN,
    onRespond: nothing, onDelete: nothing, onDismiss: nothing, onRetry: nothing, onTrusting: nothing, onGrant: nothing,
    onOpenQuest: nothing,
  },
  // The main area's height, inside the main area at its wide width (the UI/UX design R6, STORYGAP1); a story's own width
  // is drawn instead of it.
  decorators: [(Story) => <div className="flex h-[44rem] border border-line bg-page"><Story /></div>, storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof QuestPage>;

/** Open: *Take* is the loud act in its header, then *Decline…*, and *Mark done…* in its ⋯ (UX7c); it carries a link and two files. */
export const Open: Story = {};

/** Taken: *Mark done…* is the loud act now (UX5 U31); it asks once under the header, with the person's note (QUESTCLOSE1). */
export const Taken: Story = { args: { quest: TAKEN } };

/**
 * Its work in its workflow (WORKFLOW1c, the workflow design §7): one line among the head's facts, *Workflow: waits for your look*,
 * in open's hue since it waits on the person, whose door opens the run in the session's side bar.
 */
export const InItsWorkflow: Story = { args: { quest: TAKEN, workflow: { runs: [IN_REVIEW], onOpen: nothing } } };

/** The same in 中文. */
export const InItsWorkflowChinese: Story = { ...InItsWorkflow, decorators: [chinese] };

/**
 * The install's case (QUESTCLOSE1, D126's note): its session here finished at a checkpoint and the quest still taken. *Sitting*
 * says nothing here carries it on, with *Mark done…* where it is read and loud in its head.
 */
export const FinishedAtACheckpoint: Story = { args: { quest: TAKEN, session: FINISHED_HERE, onAttend: nothing } };

/** The same in 中文. */
export const FinishedAtACheckpointChinese: Story = { ...FinishedAtACheckpoint, decorators: [chinese] };

/** The same in dark. */
export const FinishedAtACheckpointDark: Story = { ...FinishedAtACheckpoint, decorators: [dark] };

/** Carrying the person's requirements: *Mark done…*'s ask says they are not answered one by one. */
export const FinishedWithRequirements: Story = {
  args: { quest: REQUIRING, session: { ...FINISHED_HERE, quest: REQUIRING.id }, onAttend: nothing },
};

/** Sitting because the person holds its repository: the driver's sentence, in the page's language. */
export const Sitting: Story = { args: { sitting: HELD_BY_PERSON } };

/** Held for the agent's trust where it would run (D73), its question opened on the press. */
export const HeldForTrust: Story = { args: { hold: TRUST, trusting: true } };

/** Parked by its strikes (RETRY1): *Try again* where its sentence is read. */
export const ParkedByStrikes: Story = { args: { sitting: EXHAUSTED } };

/** Waits for an account (TOOL4g, D125 §4): its pill and the hold's sentence, and no *Try again*, since nothing needs you. */
export const WaitsForAnAccount: Story = { args: { sitting: WAITS_FOR_ACCOUNT } };

/** Held by the person's stop (SESSUX1b, D126 §3.4): its sentence, and *Try again*, which releases it. */
export const HeldByAStop: Story = { args: { sitting: STOPPED } };

/** Taken on another machine after this machine's session was cut off (CARRY2b): whose take, and no *Try again*. */
export const TakenElsewhere: Story = { args: { quest: TAKEN, sitting: TAKEN_ELSEWHERE } };

/** The same in 中文, said from the machine and session the tick names (CARRY2c), never the driver's English. */
export const TakenElsewhereChinese: Story = { ...TakenElsewhere, decorators: [chinese] };

/** Taken, and waiting on a question its taker asked another repository (D79). */
export const WaitingOnAQuestion: Story = { args: { quest: WAITING, question: { id: QUESTION.id, quest: QUESTION } } };

/** A move that reached the remote second (D68 §5), kept for a person with its dismissal. */
export const Conflict: Story = { args: { quest: CONFLICTED } };

/**
 * This machine's lost take on a step open again (SYNCMERGE1c2, D178 point 2): *Bring mine forward* and *Drop mine*, and what each does,
 * the dismissal's apart from the bring-forward's.
 */
export const ConflictMine: Story = { args: { quest: MINE_LOST, onForward: nothing } };

/** The same in 中文. */
export const ConflictMineChinese: Story = { ...ConflictMine, decorators: [chinese] };

/** The same in dark. */
export const ConflictMineDark: Story = { ...ConflictMine, decorators: [dark] };

/** Another machine's lost take on a step open here: its dismissal only, as before. */
export const ConflictTheirs: Story = { args: { quest: THEIRS_LOST, onForward: nothing } };

/** Lost to the person's reopen of the work it grew from, this machine's take and another's, each said as that. */
export const ConflictLostToTheReopen: Story = { args: { quest: LOST_TO_REOPEN, onForward: nothing } };

/** Another machine holds the step: no *Bring mine forward*, and the row says which machine holds it. */
export const ConflictHeldElsewhere: Story = { args: { quest: HELD_ELSEWHERE, onForward: nothing } };

/** The step closed since: *Drop mine* only, and why nothing can be brought forward. */
export const ConflictOnAClosedStep: Story = { args: { quest: LOST_CLOSED, onForward: nothing } };

/** Brought forward and lost again: one row, the second loss said on the conflict it was to settle, and *Drop mine* takes both. */
export const ConflictLostAgain: Story = { args: { quest: LOST_AGAIN, onForward: nothing } };

/** The same in 中文. */
export const ConflictLostAgainChinese: Story = { ...ConflictLostAgain, decorators: [chinese] };

/** Brought forward offline (D178 point 8): unconfirmed until a sync reaches the remote, and never said as settled. */
export const ConflictUnconfirmed: Story = { args: { quest: BROUGHT_FORWARD_UNCONFIRMED, onForward: nothing } };

/** Brought forward, and the remote confirmed it: settled, below what it carries, with nothing to press. */
export const ConflictBroughtForward: Story = { args: { quest: BROUGHT_FORWARD, onForward: nothing } };

/** The same in 中文. */
export const ConflictBroughtForwardChinese: Story = { ...ConflictBroughtForward, decorators: [chinese] };

/** Dropped on another machine before this machine's bring-forward reached the remote: its fresh take is a conflict with its own choices. */
export const ConflictDroppedFirst: Story = { args: { quest: DROPPED_FIRST, onForward: nothing } };

/** Dropped: settled, and nothing waits. */
export const ConflictDropped: Story = { args: { quest: SETTLED_DROPPED, onForward: nothing } };

/**
 * A step its source's reopen held after this machine's take landed (D178 point 2): its go-on and abandon named as a lost take's two
 * choices, *Bring mine forward* and *Drop mine…*, and a decline's consequence said apart from a dismissal's.
 */
export const HeldStepMine: Story = { args: { quest: REOPENED_HERE, goOn: { ready: true, onGoOn: nothing } } };

/** The same in 中文. */
export const HeldStepMineChinese: Story = { ...HeldStepMine, decorators: [chinese] };

/**
 * A closure graph's quest the person closed done before its graph's end (FABRIC-CLOSE1j2, §E5): *Close* among its facts says so, in
 * the work's foot's words, naming the nodes its done stopped.
 */
export const ClosedByYouBeforeItsEnd: Story = { args: { quest: CLOSED_EARLY, ends: CLOSED_EARLY_ENDS } };

/** The same in 中文. */
export const ClosedByYouBeforeItsEndChinese: Story = { ...ClosedByYouBeforeItsEnd, decorators: [chinese] };

/**
 * A chain closed only in part (FABRIC-CLOSE1j2, §E5): the quest the remote refused, lost with its take, and the close node that waits
 * again and would close it over the winner's take, its stop offered in the danger ink; What needs you holds the press.
 */
export const PartiallyClosed: Story = { args: { quest: PART_CLOSED, ends: PART_CLOSED_ENDS } };

/** The same at half the main area. */
export const PartiallyClosedHalf: Story = { ...PartiallyClosed, decorators: [storyWidths.half] };

/** The same at the main area's floor. */
export const PartiallyClosedAtTheFloor: Story = { ...PartiallyClosed, decorators: [storyWidths.narrow] };

/** The same in 中文, dark: the stop recorded, waiting for the next look, no longer in the danger ink. */
export const PartiallyClosedStopRecordedChineseDark: Story = {
  args: { quest: PART_CLOSED, ends: [{ ...PART_CLOSED_ENDS[0]!, resolution: 'stop', words: [] }] }, decorators: [chinese, dark],
};

/** Addressed to two of the engine's lanes, each named where its registration says (D115 §2.2). */
export const Lanes: Story = { args: { quest: LANED, lanes: 'assets (Assets), core (Core)' } };

/** A step of a chain (MAP1): the quest it follows, unseen, this one, and the step its close will publish. */
export const AChain: Story = { args: { quest: CHAINED, chain: buildChain(CHAINED.id, [CHAINED], []) } };

/**
 * A driven session works it: its record and the door into Sessions, where its stop is (D126 §3.6): a quest is decided
 * on its page, and its sessions are managed on theirs.
 */
export const WithItsSession: Story = {
  args: {
    session: WORKING, onAttend: nothing,
    chain: buildChain(OPEN.id, [OPEN, { ...OPEN, id: 'n3n3n3', parent: OPEN.id, title: 'Next' }], [FAILED, WORKING]),
  },
};

/** The same session read in a browser: the record, and no door. */
export const WithItsSessionInABrowser: Story = { args: { session: WORKING } };

/**
 * Its session's note by code (LANG1b, D142): Daoris's lines in the window's language, and the agent's last words beneath
 * them as written. The record above is one from before parts, shown as kept and marked.
 */
export const WithItsSessionByCode: Story = {
  args: {
    quest: TAKEN,
    onAttend: nothing,
    session: {
      ...FAILED,
      note: 'Exit 1 with the quest still taken.',
      noteParts: [
        { code: 'ended.carried-unfinished', values: { exit: 1 }, text: 'It carried the quest on, and ended with it still taken (exit 1).' },
        { code: 'started.other-account', values: {}, text: 'It runs on another account.' },
      ],
    },
  },
};

/** Declined, with the reason the asker can act on. */
export const Declined: Story = { args: { quest: DECLINED } };

/** Done: nothing left to do here. */
export const Done: Story = { args: { quest: DONE } };

/** A closed quest's two clears on this machine (HIST1e, D153 §6.1): *Clear from this machine…* and *Clear failed sessions…* in its ⋯. */
const clears: HistoryDoor = {
  plan: { ...QUEST_PLAN, id: DONE.id, units: [{ ...QUEST_PLAN.units[0]!, id: DONE.id, quests: [DONE.id] }] },
  failed: { ...FAILED_PLAN, id: DONE.id, units: [{ ...FAILED_PLAN.units[0]!, id: DONE.id }] },
  busy: false,
  onClear: nothing,
};

/** Done, with both clears in its ⋯, each listing under the header on its first press (`Work/Clear`). */
export const DoneWithItsClears: Story = { args: { quest: DONE, history: clears } };

/** Done, and asked by an ask: no clear is offered here, since the ask's page clears it. */
export const DoneAskedByAnAsk: Story = { args: { quest: DONE, history: { ...clears, plan: { ...QUEST_ASKED, id: DONE.id }, failed: null } } };

/** Nobody has started on it, so the service says it may go (D95): *Delete…* in its ⋯ (UX7c). */
export const Deletable: Story = { args: { quest: DELETABLE } };

/** A title in 中文, which the header wraps. */
export const ChineseTitle: Story = { args: { quest: CJK } };

// ——— Titles first (UX7c, D152 §7; the UX7 design §5.3): its state, its name on two lines at most, one facts line, and a
// body that does not open on the title again. The install's re-filed quest, with and without a short title.

/** Named by the service from its words (SESSUX1j): the head says the name, and the body opens on the whole title. */
export const LongTitleNamedFromItsWords: Story = { args: { quest: REFILED, session: STARTING } };

/** Named by its intake's short title, with a ticket key leading it. */
export const LongTitleWithAShortTitle: Story = { args: { quest: REFILED_NAMED, session: STARTING } };

/**
 * From a host before short titles (HEAD2b): the title, two lines at most, with *View full request* moving the focus to the
 * request below, whose first line the body drops; *Take* holds still beside the title, *Decline…* in the fold and the ⋯.
 */
export const LongTitleWithNoShortTitle: Story = { args: { quest: REFILED_UNNAMED, session: STARTING } };

/** The same at a 680 px window (the main area at its half width): the acts take a row under the facts, *Decline…* in the ⋯ only. */
export const LongTitleNarrow: Story = { args: { quest: REFILED_UNNAMED, session: STARTING }, decorators: [storyWidths.half] };

/** The same at the main area's 400 px floor. */
export const LongTitleAtTheFloor: Story = { args: { quest: REFILED_UNNAMED, session: STARTING }, decorators: [storyWidths.narrow] };

/** The same in 中文, in dark. */
export const LongTitleChineseDark: Story = { args: { quest: REFILED_UNNAMED, session: STARTING }, decorators: [chinese, dark] };

// ——— What the person required, and how its done answered (DRIFT1d2, D133 §3–§4). *Open* above names none, and draws no
// section.

/** Taken, with what the person required in their words and each check; its done answers each. */
export const ItsRequirements: Story = { args: { quest: REQUIRING } };

/** Done, each requirement met, with how its check was met in the done's words. */
export const RequirementsMet: Story = { args: { quest: MET } };

/**
 * A departure holds it for the person's yes: above the body, as a conflict sits, in open's hue, each departure with its
 * reason and the words it relied on, what the yes publishes, and *Accept the departure*; its header says it awaits them.
 */
export const HeldForYourYes: Story = { args: { quest: HELD, onAccept: nothing } };

/** The yes on its way: the press waits. */
export const Accepting: Story = { args: { quest: HELD, onAccept: nothing, accepting: true } };

/** Accepted: back after the body, the departure neutral, and when the person said yes. */
export const DepartureAccepted: Story = { args: { quest: ACCEPTED, onAccept: nothing } };

/** Held, in 中文: the person's words and the done's as written, the chrome in the window's language. */
export const HeldForYourYesChinese: Story = { args: { quest: HELD_CJK, onAccept: nothing } };

// ——— Its work on this machine (PAUSE1e, D132 §7.1): *Pause…* and *Abandon…* in its header, beside *Decline…*; *Resume*
// under *Sitting* for its own pause.

const PLAN = { ...PAUSABLE_QUEST, id: OPEN.id };
const work = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: PLAN, wired: false, busy: false, onPause: nothing, onResume: nothing, onAbandon: nothing, ...over,
});

/** In flight on this machine: *Pause…* and *Abandon…* in its ⋯ (UX7c), *Decline…* beside it. */
export const WorkInFlight: Story = { args: { work: work() } };
/** Paused on its own: the sentence under *Sitting*, and *Resume* where *Try again* stands for a stop. */
export const PausedOnItsOwn: Story = { args: { sitting: PAUSED_ITSELF, work: work({ plan: { ...PAUSED_QUEST, id: OPEN.id } }) } };
/** Paused with its ask: the sentence, and a door to the ask, where that pause is resumed. */
export const PausedWithItsAsk: Story = { args: { sitting: PAUSED_WITH_ASK, onOpenAsk: nothing, work: work() } };
/** A question its asker's pause holds: a door to the quest whose pause it is. */
export const PausedWithAnotherQuest: Story = { args: { sitting: PAUSED_WITH_QUEST, work: work() } };
/** Abandoned: declined with the reason, when, what went and what stayed. */
export const WorkAbandoned: Story = {
  args: {
    quest: { ...OPEN, status: 'Declined', note: ABANDONED_ENTRY.reason },
    work: work({ plan: { ...PLAN, abandon: { abandonable: false, pieces: [], closes: null, abandoned: { ...ABANDONED_ENTRY, closed: false, declined: [OPEN.id] } } } }),
  },
};
/** In a browser: none of the three, and the terminal's commands named. */
export const WorkInABrowser: Story = { args: {} };

/** Nothing chosen: how to choose, and the ＋'s two kinds, Ask first. */
export const NothingChosen: Story = {
  render: () => <QuestsMainNotice state="none" actions={[{ id: 'ask', label: 'Ask' }, { id: 'quest', label: 'New quest' }]} />,
};

/** A quest chosen that is no longer here. */
export const Gone: Story = { render: () => <QuestsMainNotice state="gone" gone="quest" /> };

/** An ask chosen that is no longer here. */
export const AskGone: Story = { render: () => <QuestsMainNotice state="gone" gone="ask" /> };

/** The chosen record on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <QuestsMainNotice state="loading" /> };

// ——— How it came to be (TRACE1b, D143): folded at the foot of its page, then open on its chain.

const TRACED = { ...DONE, id: 'q1', to: 'dashboards', title: 'Fix the dashboard figure' };

/** How it came to be, folded at the page's foot: nothing read until it opens. */
export const HowItCameToBeFolded: Story = { args: { quest: TRACED, trace: { open: false, onToggle: nothing } } };

/** How it came to be, open: the ask and the person's words, the quest, both its sessions and the carry-on. */
export const HowItCameToBeOpen: Story = {
  args: { quest: TRACED, onAttend: nothing, onOpenAsk: nothing, trace: { open: true, onToggle: nothing, answer: answer(QUEST_CHAIN) } },
};

/** Open, in 中文. */
export const HowItCameToBeChinese: Story = { ...HowItCameToBeOpen, decorators: [chinese] };

/** Open, in dark. */
export const HowItCameToBeDark: Story = { ...HowItCameToBeOpen, decorators: [dark] };

/** Open, in 中文 and dark. */
export const HowItCameToBeChineseDark: Story = { ...HowItCameToBeOpen, decorators: [chinese, dark] };

/** The presses a set-up step's review is handed on the desktop (REVIEWENV1g). */
const REVIEW_ACTS = { reviewed: nothing, notYet: nothing, skip: nothing, showAgain: nothing };

/**
 * A set-up step shown and waiting for the person's look (REVIEWENV1g): *awaits review* in its header, *Review in `local`*
 * above its body with what it showed, where, that its tab is still served, and how to show it again by hand, then *Reviewed*,
 * *Not yet…*, *Show it again* and *Skip…*.
 */
export const SetUpShown: Story = { args: { quest: STEP_SHOWN, review: { acts: REVIEW_ACTS, served: true } } };

/** The same in 中文. */
export const SetUpShownChinese: Story = { ...SetUpShown, decorators: [chinese] };

/** The same in dark. */
export const SetUpShownDark: Story = { ...SetUpShown, decorators: [dark] };

/** The same in 中文 and dark. */
export const SetUpShownChineseDark: Story = { ...SetUpShown, decorators: [chinese, dark] };

/** Shown again after the person's not yet: the newest waits, and the first is listed under it with their words. */
export const SetUpShownAgain: Story = { args: { quest: STEP_SHOWN_AGAIN, review: { acts: REVIEW_ACTS, served: false } } };

/** Its session still works: being set up, and only the skip. */
export const SetUpBeingSetUp: Story = { args: { quest: STEP_SETTING_UP, review: { acts: REVIEW_ACTS } } };

/** Reviewed: every set-up and its verdict, nothing left to press. */
export const SetUpReviewed: Story = { args: { quest: STEP_REVIEWED, review: { acts: REVIEW_ACTS } } };

/** In a browser: no shell to reach a session or a tab, so *Reviewed* and *Skip…* alone. */
export const SetUpInABrowser: Story = { args: { quest: STEP_SHOWN, review: { acts: { reviewed: nothing, skip: nothing } } } };
