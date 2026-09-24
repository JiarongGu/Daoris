import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { SessionHead } from './SessionHead';

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

/** A RUNNING intake (INT4h): no box to type in, its stop and its ask's door in the head. */
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
    onStop: () => {},
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
