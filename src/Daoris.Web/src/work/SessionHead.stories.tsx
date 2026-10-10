import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { GoAhead, Quest, Session } from '../api';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN } from './reviewFixtures';
import { SessionHead } from './SessionHead';

const dark: Decorator = (Story) => <InTheme theme="dark"><div className="max-w-3xl bg-page p-4"><Story /></div></InTheme>;

// The record in every shape it really arrives in: driven, a conversation, parked with its analysis,
// ended, and read over a remote where the machine-local half is deliberately absent.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
  harnessVersion: '2.1.4',
  profile: 'owner',
  tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget',
  created: at(134),
  updated: at(4),
};

const QUEST: Quest = {
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  filed: at(10_000),
  updated: at(240),
};

const meta: Meta<typeof SessionHead> = {
  title: 'Work/SessionHead',
  component: SessionHead,
  args: { session: SESSION, quest: QUEST },
  decorators: [(Story) => <div className="max-w-3xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof SessionHead>;

/** Driven work in its own tree, running as a named account. */
export const Driven: Story = {};

/** The kind's workflow asks less of the person and its paths were exceeded; the terminal's Keep is the available door. */
export const WorkflowPathsHeld: Story = { args: {
  session: { ...SESSION, state: 'completed' },
  lands: { branch: 'daoris/s-workflow', tree: 's-workflow', commits: 2, uncommitted: 0 },
  onLand: () => {},
  landing: { form: 'branch', target: 'feature/docs', workflow: { state: 'kind-paths', holds: true,
    says: 'Holds: this work changed paths outside Documentation. Keep its workflow with `daoris-driver workflow keep s1a2b3c4`.' } },
} };
export const WorkflowPathsHeldChineseDark: Story = { ...WorkflowPathsHeld, decorators: [chinese, dark] };
export const WorkflowBindingUnread: Story = { ...WorkflowPathsHeld, args: { ...WorkflowPathsHeld.args,
  landing: { workflow: { state: 'unread', holds: true, says: 'Its stored run binding could not be read. Restore the binding to continue.' } },
} };

/** A conversation: no quest, so the identity is the kind and nothing is invented. */
export const Conversation: Story = {
  args: {
    session: { ...SESSION, kind: 'chat', quest: null, tree: null, created: at(9), updated: at(1) },
    quest: null,
  },
};

/**
 * Parked. The analysis sits ABOVE the record because it is the reason the person is looking, and
 * it renders verbatim — `autonomous-development` asks it for options, a recommendation and a
 * reason, none of which survive rewording. The three moves are SURF5's and attach here.
 */
export const Parked: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(11),
      note: 'Two ways forward.\n\n1. Cap hydration in the scheduler — smaller change, but the budget then lives away from the API that spends it.\n2. Cap it on the chunk API itself — touches more call sites, and the budget ends up where the work is.\n\nI recommend the second: the quest asks for the budget to be exposed on the chunk API, and option 1 would leave that promise half kept.',
    },
  },
};

/** Presses the parked card's *Finish…* and chooses to mark its quest done (QUESTCLOSE1), as a person would. */
const finishMarkingDone: Story['play'] = async ({ canvasElement }) => {
  const press = (name: string) => [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
    .find((button) => button.textContent === name)?.click();
  press('Finish…');
  await new Promise((resolve) => setTimeout(resolve, 0));
  canvasElement.querySelectorAll<HTMLButtonElement>('[role="radio"]')[1]?.click();
};

/**
 * Finishing a park whose quest is still taken (QUESTCLOSE1, D126's note): *Finish…* asks, in the same act, what becomes of
 * the quest, *Leave it as it is* by default or *Mark it done as yours* with the person's note. Here it was chosen.
 */
export const FinishingMarksItsQuestDone: Story = {
  args: { ...Parked.args, onResolve: () => {} },
  play: finishMarkingDone,
};

/**
 * Answered (ANSWER1c, D131): the park above, once the person answered, for up to one look of the driver's. The record is
 * still parked with the answer set; the head shows the answer, says the same session goes on with it, and offers none
 * of the parked card's moves, though the frame could act. Its state reads *answered*, quiet, not the waiting hue.
 */
export const Answered: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(1),
      note: 'Two ways forward; I recommend the second.\n\nAnswered: The second: cap it on the chunk API. Apply it to dev first.',
      answer: 'The second: cap it on the chunk API. Apply it to dev first.',
    },
    onResolve: () => {},
    onAnswerSession: () => {},
  },
};

/** Two go-aheads a parked session asked on its quest's ask (KNOWUSE1a), both still waiting on the person. */
const GO_AHEADS: GoAhead[] = [
  {
    number: 1, kind: 'write', on: 'production', act: 'chunk budget configuration', state: 'asked',
    asked: [{ session: SESSION.id, quest: QUEST.id, at: at(14), why: 'The cap is read from the live configuration.' }],
  },
  {
    number: 2, kind: 'release', on: 'production', act: 'streaming budget build', state: 'asked',
    asked: [{ session: SESSION.id, quest: QUEST.id, at: at(12), why: 'The quest asks for it shipped.' }],
  },
];

/**
 * Parked asking for go-aheads (KNOWUSE1a2, D135 §2): what it asked stands beneath its card, each with *Approve* and
 * *Refuse*, and answering the last one open there sends the same session on, as on the ask's page (GOAHEAD2b).
 */
export const ParkedAskingGoAheads: Story = {
  args: {
    session: {
      ...SESSION,
      state: 'awaiting-person',
      created: at(52),
      updated: at(11),
      note: 'The cap is written; it needs go-aheads 1 and 2 to reach production. Both are listed on the ask.',
    },
    goAheads: GO_AHEADS,
    onResolve: () => {},
    onGoAhead: () => {},
  },
};

/**
 * The same park once the person approved the first (GOAHEAD2b): go-ahead 2 is still open, so it stays parked with no
 * answer, as at the ask's door, and goes on once the second is answered.
 */
export const OneGoAheadAnsweredOneOpen: Story = {
  args: {
    session: {
      ...SESSION, state: 'awaiting-person', created: at(52), updated: at(11),
      note: 'The cap is written; it needs go-aheads 1 and 2 to reach production. Both are listed on the ask.',
    },
    goAheads: [{ ...GO_AHEADS[0]!, state: 'approved', answer: { approved: true, words: 'dev first', at: at(1) } }, GO_AHEADS[1]!],
    onResolve: () => {},
    onGoAhead: () => {},
  },
};

/**
 * The same park answered on its card with a go-ahead still open (KNOWUSE1a2): its blank answer is kept, so it goes on at
 * the driver's next look with what it holds, and the second, still waiting, can be answered before it does.
 */
export const AnsweredWithAGoAheadWaiting: Story = {
  args: {
    session: {
      ...SESSION, state: 'awaiting-person', created: at(52), updated: at(1), answer: 'carry on.',
      note: 'The cap is written; it needs go-aheads 1 and 2 to reach production.\n\nAnswered: carry on.',
    },
    goAheads: [{ ...GO_AHEADS[0]!, state: 'approved', answer: { approved: true, words: 'dev first', at: at(1) } }, GO_AHEADS[1]!],
    onResolve: () => {},
    onGoAhead: () => {},
  },
};

/**
 * A parked INTAKE (INT4g): it serves an ask and runs in Daoris's own room, so the record says *ask*
 * and *room* rather than *repository* and *tree*, and its answer is a door to the ask.
 */
export const IntakeAsking: Story = {
  args: {
    session: {
      ...SESSION,
      id: 'i9n8t7k6',
      quest: null,
      kind: 'chat',
      repository: 'ask #0fda18',
      ask: '0fda18',
      adapter: 'claude-code-acp',
      state: 'awaiting-person',
      tree: 'C:/somewhere/data/intake/default',
      created: at(14),
      updated: at(12),
      note: 'published nothing: the declarations did not settle ask `#0fda18`, so it asks you rather than guess — its question ends its transcript.',
    },
    quest: null,
    onResolve: () => {},
    onAnswerAsk: () => {},
  },
};

/** A RUNNING intake (INT4h): no box to type in, its ask's door in the head; its stop is the page header's (SESSUX1d). */
export const IntakeRunning: Story = {
  args: {
    session: {
      ...SESSION,
      id: 'r7n8t7k6',
      quest: null,
      kind: 'chat',
      repository: 'ask #0fda18',
      ask: '0fda18',
      adapter: 'claude-code-acp',
      state: 'working',
      tree: 'C:/somewhere/data/intake/default',
      created: at(3),
      updated: at(1),
      note: undefined,
    },
    quest: null,
    onAnswerAsk: () => {},
  },
};

/** Ended, so the clock reads as a lifetime rather than an age. */
export const Finished: Story = {
  args: {
    session: {
      ...SESSION, state: 'completed', created: at(95), updated: at(50), note: 'gates green; quest closed by the session.',
    },
    quest: { ...QUEST, status: 'Done' },
  },
};

/** Failed, with what the driver observed — a note, not an analysis: nothing is waiting on anyone. */
export const Failed: Story = {
  args: {
    session: {
      ...SESSION, state: 'failed', created: at(40), updated: at(31), note: 'the process exited 1 with the quest unexplained.',
    },
  },
};

/**
 * Failed, its tree gone and its branch standing with commits no branch of the person's holds (LAND3b): *Discard branch…*
 * beside what it left, asking once under the line on its press.
 */
export const FailedItsBranchLeft: Story = {
  args: {
    session: {
      ...SESSION, state: 'failed', created: at(40), updated: at(31), note: 'the process exited 1 with the quest unexplained.',
    },
    branch: {
      repository: 'engine', workspace: 'default', branch: 'daoris/streaming-budget', hasTree: false,
      kind: 'unlanded', commits: 2, removable: false, discardable: true,
    },
    onReview: () => {},
    onDiscardBranch: () => {},
  },
};

/**
 * Failed, its tree still here holding a commit no branch of the person's holds (LAND4, the owner's case): what it left, its
 * branch and its tree, with *Accept…*, which asks once under the line, saying where the rule puts it.
 */
export const FailedItsWorkToLand: Story = {
  args: {
    session: {
      ...SESSION, state: 'failed', created: at(40), updated: at(31), note: 'the process exited 1 with the quest unexplained.',
    },
    lands: { branch: 'daoris/s-4e6837ed', tree: 's-4e6837ed', commits: 1, uncommitted: 0 },
    landing: { form: 'branch', target: 'feature/streaming-budget', plugin: 'azure-devops-pull-request' },
    onReview: () => {},
    onLand: () => {},
  },
};

/** Finished at a checkpoint by the person, its commits not landed yet (LAND4): the same offer, with no word that it did not finish. */
export const FinishedAtACheckpointToLand: Story = {
  args: {
    ...FailedItsWorkToLand.args,
    session: {
      ...SESSION, state: 'completed', created: at(40), updated: at(31), note: 'The person finished this at a checkpoint.',
    },
    lands: { branch: 'daoris/s-56cb4d29', tree: 's-56cb4d29', commits: 2, uncommitted: 0 },
  },
};

/** The review's presses on the desktop (REVIEWENV1g). */
const REVIEW_ACTS = { reviewed: () => {}, notYet: () => {}, skip: () => {}, setUp: () => {}, showAgain: () => {}, open: () => {} };

/**
 * Finished, its work waiting for the person's review (REVIEWENV1g): *Review in `local`* where *Accept…* would be, the set-up
 * step's showing, its tab still served, and *Reviewed*, *Not yet…*, *Show it again* and *Skip…*.
 */
export const WaitsForReviewShown: Story = {
  args: {
    ...FinishedAtACheckpointToLand.args,
    landing: { form: 'merge', target: 'main', review: { state: 'shown', environment: 'local', level: 'set-up-step', quest: 'q2' } },
    review: { step: STEP_SHOWN, served: true, acts: REVIEW_ACTS },
  },
};

/** The same in 中文. */
export const WaitsForReviewShownChinese: Story = { ...WaitsForReviewShown, decorators: [chinese] };

/** The same in dark. */
export const WaitsForReviewShownDark: Story = { ...WaitsForReviewShown, decorators: [dark] };

/** Nothing shows the work yet: *Set it up in `local`* and *Skip…* where *Accept…* would be. */
export const WaitsForReviewNotShown: Story = {
  args: {
    ...FinishedAtACheckpointToLand.args,
    landing: { form: 'merge', target: 'main', review: { state: 'not-shown', environment: 'local', level: 'repository' } },
    review: { acts: REVIEW_ACTS },
  },
};

/** The same in 中文. */
export const WaitsForReviewNotShownChinese: Story = { ...WaitsForReviewNotShown, decorators: [chinese] };

/** Its set-up step at work: being set up, the door to the step, and the skip. */
export const WaitsForReviewBeingSetUp: Story = {
  args: {
    ...FinishedAtACheckpointToLand.args,
    landing: { form: 'merge', target: 'main', review: { state: 'being-set-up', environment: 'local', quest: 'q2' } },
    review: { step: STEP_SETTING_UP, acts: REVIEW_ACTS },
  },
};

/** Reviewed, and work added since: what was reviewed does not hold it. */
export const WaitsForReviewNotHeld: Story = {
  args: {
    ...FinishedAtACheckpointToLand.args,
    landing: { form: 'merge', target: 'main', review: { state: 'not-held', environment: 'local', quest: 'q2' } },
    review: { step: STEP_REVIEWED, acts: REVIEW_ACTS },
  },
};

/**
 * Finished, its work squash-merged as a pull request and its tree still here (SQUASHTIDY1b, the owner's case): no *Accept…*,
 * the driver's sentence where it would be, and *Discard branch…*, which asks once naming the ref its commits stay at.
 */
export const SquashMergedItsDiscard: Story = {
  args: {
    session: {
      ...SESSION, state: 'completed', created: at(40), updated: at(31), note: 'The person finished this at a checkpoint.',
    },
    discards: {
      branch: 'daoris/s-56cb4d29',
      tree: 's-56cb4d29',
      says: 'Its work is on `main` by content (a squash merge).',
      keeps: 'refs/daoris/discarded/daoris/s-56cb4d29',
      keptAt: 'Its commits stay at `refs/daoris/discarded/daoris/s-56cb4d29` until you delete that ref; '
        + '`git branch daoris/s-56cb4d29 refs/daoris/discarded/daoris/s-56cb4d29` brings the branch back.',
    },
    onReview: () => {},
    onDiscardTree: () => {},
  },
};

/** The same, its tree holding a file no commit does: the driver says why it stays, and no unforced discard is offered. */
export const SquashMergedItsTreeStays: Story = {
  args: {
    ...SquashMergedItsDiscard.args,
    discards: {
      ...SquashMergedItsDiscard.args!.discards!,
      says: 'Its work is on `main` by content (a squash merge). Its tree stays: it holds 1 ignored path(s) your checkout does '
        + 'not have: local.db, which a discard would destroy.',
      keeps: null,
      keptAt: null,
    },
  },
};

/**
 * UXFIX4b: the same with a long branch name, at the main area's 400 px floor less a page's gutters. The name is whole,
 * wrapping after its separators on the line's own row, where it was cut to one line with nothing to read the rest by.
 */
export const FailedItsLongBranchLeftAtTheFloor: Story = {
  args: {
    ...FailedItsBranchLeft.args,
    branch: { ...FailedItsBranchLeft.args!.branch!, branch: 'daoris/s-5a6b7c8d-a-chain-step-that-carries-a-long-slug-past-the-head' },
  },
  decorators: [(Story) => <div className="w-[352px]"><Story /></div>],
};

/**
 * Failed, its note by code (LANG1b, D142): Daoris's lines worded in the window's language, the agent's words beneath their
 * lead-in as written. The two stories above are records from before parts, shown as kept and marked. The cooling line is
 * shown in the shape a note from before AGT3d keeps and in the shape written since, naming whose accounts and the kind of
 * limit, both worded.
 */
export const FailedByCode: Story = {
  args: {
    session: {
      ...SESSION, state: 'failed', created: at(40), updated: at(31),
      note: 'The agent’s turn failed with the quest still taken: the ACP agent refused the call. It exited with code 1.',
      noteParts: [
        { code: 'ended.turn-failed-taken', values: {}, text: 'The agent’s turn failed with the quest still taken:' },
        { words: 'the ACP agent refused the call: rate limited until 16:00.', by: 'agent' },
        { code: 'account.cooling', values: { until: '2026-10-03T16:00:00Z', why: 'stated' }, text: 'The account it ran on is cooling until 2026-10-03 16:00 UTC (the agent said so); nothing starts on it until then.' },
        {
          code: 'account.cooling-window',
          values: { until: '2026-10-03T16:00:00Z', why: 'stated', owner: 'claude-code', window: 'weekly' },
          text: 'The `claude-code` account it ran on hit its weekly limit and is cooling until Oct 3, 16:00 (UTC), as the agent said, and nothing starts on it until then.',
        },
      ],
    },
  },
};

/** Parked, its lead-in by code and its question as the agent wrote it (LANG1b), with the moves a shell has. */
export const ParkedByCode: Story = {
  args: {
    session: {
      ...SESSION, state: 'awaiting-person', created: at(52), updated: at(11),
      note: 'It stopped with its quest still taken, to ask you: …',
      noteParts: [
        { code: 'ended.parked-asked', values: {}, text: 'It stopped with its quest still taken, to ask you:' },
        { words: 'Two ways forward.\n\n1. Cap hydration in the scheduler.\n2. Cap it on the chunk API itself.\n\nI recommend the second.', by: 'agent' },
      ],
    },
    onResolve: () => {},
    onAnswerSession: () => {},
  },
};

/**
 * A record mirrored from another machine (D47 §6): the machine is named, and the tree and the
 * account are absent because they never travelled. Three absences, none of them an error.
 */
export const FromAnotherMachine: Story = {
  args: {
    session: { ...SESSION, id: 'person@machine-a/s1a2b3c4', tree: null, profile: null },
  },
};

/** A record older than the toolchain: no version, no account, and still a complete record. */
export const BeforeTheToolchain: Story = {
  args: { session: { ...SESSION, harnessVersion: null, profile: null, tree: null } },
};

/** 中文 content beside English chrome — the i18n boundary, seen rather than described. */
export const ChineseQuest: Story = {
  args: {
    quest: { ...QUEST, title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上' },
  },
};
