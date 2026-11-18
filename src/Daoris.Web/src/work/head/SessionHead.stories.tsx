import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../../data/api';
import { aQuest } from '../../quests/aQuest';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { sessionWidths } from '../../design/storyWidths';
import { SessionHead } from './SessionHead';

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

// The record in every shape it really arrives in: driven, a conversation, parked with its analysis,
// ended, and read over a remote where the machine-local half is deliberately absent. Drawn in a session's page at the
// frame's widths, with the session's gutters.

const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  door: 'claude-code',
  state: 'working',
  kind: 'driven',
  doorVersion: '2.1.4',
  profile: 'owner',
  actor: 'build', choice: 'before',
  asked: { agent: 'claude-code', model: { source: 'account-default', carried: false }, effort: { source: 'account-default', carried: false } },
  ran: { models: ['claude-sonnet-5-5'], more: 0, entries: [], entriesMore: 0 },
  tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget',
  created: at(134),
  updated: at(4),
};

const QUEST: Quest = {
  ...aQuest(),
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
  decorators: [sessionWidths.wide],
};
export default meta;

type Story = StoryObj<typeof SessionHead>;

/** Driven work in its own tree, running as a named account. */
export const Driven: Story = {};

/** The same at half the window. */
export const DrivenHalf: Story = { decorators: [sessionWidths.half] };

/** The same at the main area's 400 px floor. */
export const DrivenNarrow: Story = { decorators: [sessionWidths.narrow] };

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
 * reason, none of which survive rewording. The three moves attach here.
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

/** Presses the parked card's *Finish…* and chooses to mark its quest done, as a person would. */
const finishMarkingDone: Story['play'] = async ({ canvasElement }) => {
  const press = (name: string) => [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
    .find((button) => button.textContent === name)?.click();
  press('Finish…');
  await new Promise((resolve) => setTimeout(resolve, 0));
  canvasElement.querySelectorAll<HTMLButtonElement>('[role="radio"]')[1]?.click();
};

/**
 * Finishing a park whose quest is still taken: *Finish…* asks, in the same act, what becomes of
 * the quest, *Leave it as it is* by default or *Mark it done as yours* with the person's note. Here it is chosen.
 */
export const FinishingMarksItsQuestDone: Story = {
  args: { ...Parked.args, onResolve: () => {} },
  play: finishMarkingDone,
};

/**
 * Answered: the park above, once the person answered, for up to one look of the driver's. The record is
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
      said: [{ id: 'w1', text: 'The second: cap it on the chunk API. Apply it to dev first.', at: at(1), files: [] }],
    },
    onResolve: () => {},
    onAnswerSession: () => {},
  },
};

/**
 * A parked INTAKE: it serves an ask and runs in Daoris's own room, so the record says *ask*
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
      door: 'claude-code-acp',
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

/** A RUNNING intake: no box to type in, its ask's door in the head; its stop is the page header's. */
export const IntakeRunning: Story = {
  args: {
    session: {
      ...SESSION,
      id: 'r7n8t7k6',
      quest: null,
      kind: 'chat',
      repository: 'ask #0fda18',
      ask: '0fda18',
      door: 'claude-code-acp',
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
      noteParts: [{ words: 'the process exited 1 with the quest unexplained.', by: 'program' }],
    },
  },
};

/**
 * Failed, its tree gone and its branch standing with commits no branch of the person's holds: *Discard branch…*
 * beside what it left, asking once under the line on its press.
 */
export const FailedItsBranchLeft: Story = {
  args: {
    session: {
      ...SESSION, state: 'failed', created: at(40), updated: at(31), note: 'the process exited 1 with the quest unexplained.',
      noteParts: [{ words: 'the process exited 1 with the quest unexplained.', by: 'program' }],
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
 * The same with a long branch name, at the main area's 400 px floor, in the session's gutters. The name is whole,
 * wrapping after its separators on the line's own row, never cut to one line with nothing to read the rest by.
 */
export const FailedItsLongBranchLeftAtTheFloor: Story = {
  args: {
    ...FailedItsBranchLeft.args,
    branch: { ...FailedItsBranchLeft.args!.branch!, branch: 'daoris/s-5a6b7c8d-a-chain-step-that-carries-a-long-slug-past-the-head' },
  },
  decorators: [sessionWidths.narrow],
};

/**
 * Failed, its note by code: Daoris's lines worded in the window's language, the agent's words beneath their
 * lead-in as written. The cooling line is shown in the shape an older note keeps and in the current shape,
 * naming whose accounts and the kind of limit, both worded.
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

/** Parked, its lead-in by code and its question as the agent wrote it, with the moves a shell has. */
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
 * A record mirrored from another machine: the machine is named, and the tree and the
 * account are absent because they never travelled. Three absences, none of them an error.
 */
export const FromAnotherMachine: Story = {
  args: {
    session: { ...SESSION, id: 'person@machine-a/s1a2b3c4', tree: null, profile: null, actor: null, choice: null, asked: null, ran: null },
  },
};

/** A record older than the toolchain: no version, no account, and still a complete record. */
export const BeforeTheToolchain: Story = {
  args: { session: { ...SESSION, doorVersion: null, profile: null, tree: null, actor: null, choice: null, asked: null, ran: null } },
};

/** 中文 content beside English chrome — the i18n boundary, seen rather than described. */
export const ChineseQuest: Story = {
  args: {
    quest: { ...QUEST, title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上' },
  },
};

// ——— Whom it continues and whom a helper helps: *Details* opened, the other
// session's title with its door where the frame holds it, its id in mono where it does not; in English, 中文 and dark.

/** Opens the folded *Details*, in either language, so the story shows its rows. */
const openDetails: Story['play'] = async ({ canvasElement }) => {
  [...canvasElement.querySelectorAll<HTMLButtonElement>('button')]
    .find((button) => /^(Details|详情)/.test(button.textContent ?? ''))?.click();
};

const EARLIER: Session = { ...SESSION, id: 'a1b2c3d4', quest: null, kind: 'chat', door: 'codex', state: 'completed' };
const HANDED: Session = {
  ...SESSION, quest: null, kind: 'chat', tree: null, from: 'a1b2c3d4', fromWhy: 'near', fromBy: 'daoris', created: at(9), updated: at(1),
};
const A_HELPER: Session = { ...SESSION, id: 'h3lp3r00', quest: null, kind: 'chat', tree: null, serves: 's1a2b3c4', created: at(3), updated: at(1) };

/** A conversation handed over: *Continues* names the session it came from, with its door. */
export const ContinuesAnother: Story = {
  args: {
    session: HANDED, quest: null, opening: 'Go on with the hydrator fix',
    continues: { session: EARLIER, title: 'Why the hydrator drops frames' }, onOpenSession: () => {},
  },
  play: openDetails,
};
export const ContinuesAnotherChineseDark: Story = {
  ...ContinuesAnother,
  args: { ...ContinuesAnother.args, continues: { session: EARLIER, title: '为什么水合会丢帧' } },
  decorators: [chinese, dark],
};

/** The session it continues is not listed here (deleted): its id in mono, no door. */
export const ContinuesOneNotHeld: Story = {
  args: { session: HANDED, quest: null, onOpenSession: () => {} },
  play: openDetails,
};

/** A helper: titled as one, *Helping* names the session it answers, with its door. */
export const Helping: Story = {
  args: {
    session: A_HELPER, quest: null, headed: false,
    helps: { session: SESSION, title: 'Expose a streaming budget on the chunk API' }, onOpenSession: () => {},
  },
  play: openDetails,
};
export const HelpingChinese: Story = { ...Helping, decorators: [chinese] };
export const HelpingDark: Story = { ...Helping, decorators: [dark] };

/** Asked, acknowledged and reported stay separate, in the agent's own names. */
export const ActorReported: Story = {
  args: { session: { ...SESSION, ran: { models: ['claude-sonnet-5-5', 'claude-haiku-4-5-20251001'], more: 1,
    entries: [{ at: at(2), acknowledged: 'sonnet', effort: 'high' }], entriesMore: 0 } } },
  play: openDetails,
};
export const ActorReportedHalf: Story = { ...ActorReported, decorators: [sessionWidths.half] };
export const ActorReportedNarrow: Story = { ...ActorReported, decorators: [sessionWidths.narrow] };
export const ActorReportedChinese: Story = { ...ActorReported, decorators: [chinese, sessionWidths.wide] };
export const ActorReportedChineseHalf: Story = { ...ActorReported, decorators: [chinese, sessionWidths.half] };
export const ActorReportedChineseNarrow: Story = { ...ActorReported, decorators: [chinese, sessionWidths.narrow] };
/** *Details* says how the choice is reached; a record with no `chosen` says the policy is not recorded. */
export const ActorChosenAfterALimit: Story = {
  args: { session: { ...SESSION, choice: '2', chosen: { rule: 'order', by: 'moved', condition: 'limit', from: '1' } } },
  play: openDetails,
};
export const ActorChosenByWeightNarrow: Story = {
  args: { session: { ...SESSION, choice: '2', chosen: { rule: 'weight', by: 'weight' } } },
  decorators: [sessionWidths.narrow],
  play: openDetails,
};
export const ActorChosenChineseDark: Story = { ...ActorChosenAfterALimit, decorators: [chinese, dark] };
export const ActorChosenNotRecorded: Story = { args: { session: { ...SESSION, choice: '2' } }, play: openDetails };
export const ActorNotReported: Story = { args: { session: { ...SESSION, ran: null } }, play: openDetails };
