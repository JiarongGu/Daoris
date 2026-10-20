import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { Quest } from '../data/api';
import { QuestConflicts, SettledConflicts } from './Conflicts';
import {
  BROUGHT_FORWARD, BROUGHT_FORWARD_UNCONFIRMED, CONFLICTED, DROPPED_FIRST, HELD_ELSEWHERE, LOST_AGAIN, LOST_CLOSED, LOST_TO_REOPEN,
  MINE_LOST, SETTLED_DROPPED, THEIRS_LOST,
} from './fixtures';

// A quest's conflicts (SYNCMERGE1c2, D178 points 2, 7 and 8) as its page draws them (`QuestPage.tsx`: the open ones above the
// body, how they ended under it), on their own (STORYGAP1, the UI/UX design §1.2 item 6): this machine's lost take with its two
// choices, another machine's with its dismissal, why a bring-forward is not offered, a second loss, an unconfirmed bring-forward,
// and the settled ones; at the main area's three widths, in 中文 and dark.

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The open conflicts, then how the settled ones ended, as the page places them. */
function Conflicts({ quest, busy = false }: { quest: Quest; busy?: boolean }) {
  return (
    <>
      <QuestConflicts className="mb-4" quest={quest} busy={busy} onDismiss={nothing} onForward={nothing} />
      <SettledConflicts quest={quest} />
    </>
  );
}

const meta = {
  title: 'Quests/Conflicts',
  component: Conflicts,
  args: { quest: MINE_LOST },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof Conflicts>;

export default meta;
type Story = StoryObj<typeof meta>;

/** This machine's take lost: *Bring mine forward* and *Drop mine*, with what each does. */
export const MineLost: Story = {};

/** Another machine's take lost: its dismissal only. */
export const TheirsLost: Story = { args: { quest: THEIRS_LOST } };

/** A host that names no machine: a dismissal, as before. */
export const NoMachineNamed: Story = { args: { quest: CONFLICTED } };

/** Lost to the person's reopen. */
export const LostToTheReopen: Story = { args: { quest: LOST_TO_REOPEN } };

/** The bring-forward not offered, and why: another machine holds the step. */
export const HeldElsewhere: Story = { args: { quest: HELD_ELSEWHERE } };

/** The bring-forward not offered, and why: the step closed. */
export const StepClosed: Story = { args: { quest: LOST_CLOSED } };

/** A second loss: the bring-forward lost again, said on the conflict it was to settle. */
export const LostAgain: Story = { args: { quest: LOST_AGAIN } };

/** A bring-forward the remote has not answered: unconfirmed, never settled. */
export const Unconfirmed: Story = { args: { quest: BROUGHT_FORWARD_UNCONFIRMED } };

/** Settled: dropped, and nothing to press. */
export const SettledDropped: Story = { args: { quest: SETTLED_DROPPED } };

/** Settled: brought forward and confirmed. */
export const SettledForward: Story = { args: { quest: BROUGHT_FORWARD } };

/** A fresh take's conflict dropped first. */
export const DroppedFirst: Story = { args: { quest: DROPPED_FIRST } };

/** A press on its way: both choices wait. */
export const Busy: Story = { args: { busy: true } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { args: { quest: LOST_AGAIN }, decorators: [storyWidths.half] };

/** The main area's 400 px floor: the choices wrap under the conflict's line. */
export const Narrow: Story = { args: { quest: LOST_AGAIN }, decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { args: { quest: LOST_AGAIN }, decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { decorators: [dark] };

/** In 中文 and dark, half, settled. */
export const ChineseDarkHalf: Story = { args: { quest: BROUGHT_FORWARD }, decorators: [chinese, dark, storyWidths.half] };
