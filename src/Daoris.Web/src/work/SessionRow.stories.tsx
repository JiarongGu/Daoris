import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session, SessionState } from '../api';
import { SessionRow } from './SessionRow';

// Every state of a rail row, on the shipped component (D42 §5) — including the ones real data
// rarely shows. A row is a molecule, so each of these is reached by passing props: no driver, no
// service and no arranged world (components plan §2).

/** Relative to now, so a story reads as a duration rather than as the age of this file. */
const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const SESSION: Session = {
  id: 's1a2b3c4',
  quest: '7a82cc',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  kind: 'driven',
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

const meta: Meta<typeof SessionRow> = {
  title: 'Work/SessionRow',
  component: SessionRow,
  args: { session: SESSION, quest: QUEST },
  // The rail's real width, and the list element a row expects to sit in.
  decorators: [(Story) => (
    <ul className="m-0 w-[18rem] list-none border border-line bg-raised p-0 py-1">
      <Story />
    </ul>
  )],
};
export default meta;

type Story = StoryObj<typeof SessionRow>;

/** The common case: driven work in flight, two hours deep, moved minutes ago. */
export const Working: Story = {};

/** Attended. The accent stripe is the visible half; `aria-current` is the other. */
export const Selected: Story = { args: { selected: true } };

const STATES: SessionState[] = [
  'queued', 'starting', 'working', 'awaiting-person',
  'completed', 'declined', 'stood-down', 'failed', 'stopped',
];

/**
 * All nine, together — the view that makes a half-toned tenth state obvious at a glance. Note that
 * `awaiting person` wears the attention mark: it is a state, so no amount of activity outranks it.
 */
export const EveryState: Story = {
  render: (args) => (
    <>
      {STATES.map((state) => (
        <SessionRow {...args} key={state} session={{ ...SESSION, id: state, state }} />
      ))}
    </>
  ),
};

/** A conversation, serving no quest: the identity is derived, never invented (design §3). */
export const Conversation: Story = {
  args: { session: { ...SESSION, kind: 'chat', quest: null, created: at(7), updated: at(1) }, quest: null },
};

/**
 * A driven session whose quest the caller does not hold — the list has not loaded, or the quest
 * closed and fell out of it. The row answers with the reference rather than a name it made up.
 */
export const QuestNotInHand: Story = { args: { quest: null } };

/** A sentence for a title, in a column that has no room for one — in both scripts. */
export const LongTitle: Story = {
  args: {
    quest: {
      ...QUEST,
      title: 'Expose a streaming budget on the chunk API so the scheduler can cap hydration per frame',
    },
  },
};

export const ChineseTitle: Story = {
  args: {
    quest: {
      ...QUEST,
      title: '让世界流式加载在每一帧内限制水合工作量，并把预算暴露在区块 API 上，供上层调度器读取',
    },
  },
};

/**
 * A record mirrored from another machine (D47 §6): the id carries the origin, so the row can say
 * where the work is actually happening. A local row says nothing here, deliberately.
 */
export const AnotherMachine: Story = {
  args: { session: { ...SESSION, id: 'person@machine-a/s1a2b3c4' } },
};

/**
 * A session in a working tree of its own (D51) — which is how a repository comes to hold two at
 * once, and therefore why a row has to say which tree it is in. Silence is the registered root.
 */
export const OwnTree: Story = {
  args: {
    root: 'C:/checkouts/engine',
    session: { ...SESSION, tree: 'C:/somewhere/.daoris/trees/default/engine/streaming-budget' },
  },
};

/**
 * The same row in the repository's registered checkout. It carries a tree path too — the ledger
 * resolves an unstated tree to the root before recording one — and says nothing about it, which
 * is the distinction the first real-window pass caught the rail getting wrong.
 */
export const InTheRegisteredRoot: Story = {
  args: { root: 'C:/checkouts/engine', session: { ...SESSION, tree: 'C:/checkouts/engine' } },
};

/**
 * A session whose landing tidied its tree away (LOOK2b, D113): the row says where the work landed, not the tree it
 * no longer has.
 */
export const Landed: Story = {
  args: {
    root: 'C:/checkouts/engine',
    session: { ...SESSION, state: 'completed', created: at(95), updated: at(50), tree: 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9' },
    where: { treeGone: true, landed: { repository: 'engine', branch: 'feature/7a82cc-streaming-budget', state: 'standing' } },
  },
};

/** The same, once its branch has gone too: still where the work landed, and that the branch is gone. */
export const LandedBranchGone: Story = {
  args: {
    root: 'C:/checkouts/engine',
    session: { ...SESSION, state: 'completed', created: at(95), updated: at(50), tree: 'C:/somewhere/.daoris/trees/default/engine/s-2394e5d9' },
    where: { treeGone: true, landed: { repository: 'engine', branch: 'feature/7a82cc-streaming-budget', state: 'gone' } },
  },
};

/** Three hours deep. The whole reason elapsed is on the row: "moved 4m ago" says none of this. */
export const RunningThreeHours: Story = {
  args: { session: { ...SESSION, created: at(181), updated: at(4) } },
};

/** Parked: only the person can clear it, and the row is where they find out (SURF5). */
export const AwaitingPerson: Story = {
  args: { session: { ...SESSION, state: 'awaiting-person', created: at(52), updated: at(11) } },
};

/** Finished. The span is its lifetime — measured to where it ended, not to now. */
export const Finished: Story = {
  args: { session: { ...SESSION, state: 'completed', created: at(95), updated: at(50) } },
};
