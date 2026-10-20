import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { HeldAttempt, MilestoneEntry, MilestoneUnit } from './milestone';
import { WorkMilestone } from './WorkMilestone';

// Where a piece of work stands (MILESTONEUX1, the UI/UX design §3.7): four independent facts, each marked by `Dot`'s tone and
// never a sequence, folded to their line and opened in place. A story per row of the design's state table, each folded and
// opened, then the narrow main area, 中文 and dark. The folded stories open on a press; the opened ones start open.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const entry = (over: Partial<MilestoneEntry>): MilestoneEntry => ({
  unit: 'u1', kind: 'session-branch', resource: 'daoris/s-1', role: 'temporary', disposition: 'kept', detail: 'standing', ...over,
});

const REACHED: MilestoneUnit['end'] = {
  state: 'ended', by: 'merged', on: 'main', revision: '3f9a12c', at: '2026-11-03T14:02:00Z', said: 'every commit of it is on `main`',
};
const TREE_HELD = entry({ kind: 'tree', resource: 's-1', disposition: 'pending', detail: "it waits for its session's last turn" });
const TREE_BLOCKED = entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' });
const FOLDER = entry({
  kind: 'moved-aside', resource: 's-1-20261103', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-11-10T14:02:00Z',
});

const unit = (over: Partial<MilestoneUnit> = {}): MilestoneUnit => ({
  unit: 'u1', repository: 'engine', branch: 'daoris/s-1', session: 's-1',
  end: REACHED,
  result: [entry({ kind: 'line', resource: 'main', role: 'result', detail: "the branch's commits are on the line" })],
  cleanup: { state: 'pending', waits: [TREE_HELD], gone: [] },
  recovery: [FOLDER],
  entries: [],
  ...over,
});

const HELD: HeldAttempt = {
  unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown',
  at: '2026-11-03T14:05:00Z', said: 'its plugin was asked and cannot say what became of it',
};

const meta = {
  title: 'Work/WorkMilestone',
  component: WorkMilestone,
  args: { unit: unit() },
  decorators: [storyWidths.half],
} satisfies Meta<typeof WorkMilestone>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Folded, as the record draws it: one line, the end reached, the clean-up pending, the recovery until a date. */
export const Folded: Story = {};

/** Opened in place: the design's picture, each fact marked by its own tone. */
export const Opened: Story = { args: { defaultOpen: true } };

/** The end not reached: the driver's why, and the other facts as they stand. */
export const EndNotReached: Story = {
  args: {
    defaultOpen: true,
    unit: unit({
      end: { state: 'not-ended', said: 'nothing was handed on and the line does not hold it' }, result: [], recovery: [],
    }),
  },
};

/** The end handed off: not reached, in its own word. */
export const EndHanded: Story = {
  args: { defaultOpen: true, unit: unit({ end: { state: 'handed', said: 'a later session carries it on' }, result: [] }) },
};

/** The owner unknown: never read as reached. */
export const EndUnknown: Story = {
  args: { defaultOpen: true, unit: unit({ end: { state: 'unknown', said: 'no record names its unit, so its end is not said' }, result: [], recovery: [] }) },
};

/** Over without its result: the person removed the branch outside Daoris. */
export const EndDropped: Story = {
  args: {
    defaultOpen: true,
    unit: unit({
      end: { state: 'dropped', said: '`feature/x` was removed outside Daoris, and git said it is missing' }, result: [],
      cleanup: { state: 'completed', waits: [], gone: [entry({ kind: 'landing-branch', resource: 'feature/x', disposition: 'removed' })] }, recovery: [],
    }),
  },
};

/** The clean-up pending, with what waits and why, each by its own value. */
export const CleanupPending: Story = {
  args: {
    defaultOpen: true,
    unit: unit({ cleanup: { state: 'pending', waits: [TREE_HELD, entry({ kind: 'end', resource: 'the end', disposition: 'pending', detail: 'the cycle waits' })], gone: [] } }),
  },
};

/** A blocked entry: in the danger ink, and the folded line says *blocked*. */
export const CleanupBlocked: Story = {
  args: { unit: unit({ cleanup: { state: 'pending', waits: [TREE_BLOCKED], gone: [] } }) },
};

/** The same, opened. */
export const CleanupBlockedOpened: Story = { args: { ...CleanupBlocked.args, defaultOpen: true } };

/** Completed with nothing kept for recovery, which is ordinary: the recovery says so and wears no mark. */
export const CompletedNothingKept: Story = {
  args: {
    defaultOpen: true,
    unit: unit({ cleanup: { state: 'completed', waits: [], gone: [entry({ disposition: 'removed', detail: 'went 2026-11-03 14:00 UTC' })] }, recovery: [] }),
  },
};

/** A plugin step's attempt held for the person: its row under the facts, and the folded line says *held for you*. */
export const AttemptHeld: Story = { args: { unit: unit({ held: [HELD] }) } };

/** The same, opened. */
export const AttemptHeldOpened: Story = { args: { ...AttemptHeld.args, defaultOpen: true } };

/** Everything that waits on the person, folded: the line carries both words, so none hides behind the fold. */
export const BlockedAndHeld: Story = {
  args: { unit: unit({ cleanup: { state: 'pending', waits: [TREE_BLOCKED], gone: [] }, held: [HELD] }) },
};

/** The narrow main area: the line wraps, and the label column stacks over its words under 24rem of its own width. */
export const Narrow: Story = {
  args: { defaultOpen: true, unit: unit({ cleanup: { state: 'pending', waits: [TREE_BLOCKED], gone: [] }, held: [HELD] }) },
  decorators: [storyWidths.narrow],
};

/** Folded and narrow: the line alone. */
export const NarrowFolded: Story = { args: { ...BlockedAndHeld.args }, decorators: [storyWidths.narrow] };

/** In 中文. */
export const Chinese: Story = { args: { ...Narrow.args }, decorators: [chinese] };

/** In 中文, narrow. */
export const ChineseNarrow: Story = { args: { ...Narrow.args }, decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { args: { ...Narrow.args }, decorators: [dark] };

/** In 中文 and dark, folded. */
export const ChineseDarkFolded: Story = { args: { ...BlockedAndHeld.args }, decorators: [chinese, dark] };
