import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { LandingLifeFlow } from './LandingFlow';
import {
  OLDER_BRANCH, SHARED_BRANCH, SHARED_DUE, SHARED_UNITS, WAITING_BRANCH, WAITING_DUE, WAITING_UNITS,
} from './landingFixtures';

// How a landed branch landed, drawn as a flowchart (FLOWCHART1c; the flowchart design §2.7, §3.6): the due entry and its tries,
// three holds in a row stacked; the landing, with the session it was made for and each advance naming its session, three by one
// session stacked; the push as a fact with no time and the pull request's answer as a snapshot, each hung from the landing; the
// end, the clean-up and the recovery of the units it carries. At the frame's widths, 300 px the side bar's floor, 400 and 544 the
// main area's, 1200 a wide pane. The first decorator is the innermost.

const meta: Meta<typeof LandingLifeFlow> = {
  title: 'Repositories/LandingFlow',
  component: LandingLifeFlow,
};
export default meta;

type Story = StoryObj<typeof LandingLifeFlow>;

const light = [atWidths(), inLight];

const SHARED = { name: SHARED_BRANCH.branch, branch: SHARED_BRANCH, due: SHARED_DUE, units: SHARED_UNITS };
const WAITING = { name: WAITING_BRANCH.branch, branch: WAITING_BRANCH, due: WAITING_DUE, units: WAITING_UNITS };

/** Three sessions' work on one branch: held three times then landed, moved on once by `s3` and three times by `s4`, its pull request merged. */
export const Shared: Story = { args: { landing: SHARED }, decorators: light };
/** The same in dark. */
export const SharedDark: Story = { args: { landing: SHARED }, decorators: [atWidths(), inDark] };
/** The same in 中文. */
export const SharedChinese: Story = { args: { landing: SHARED }, decorators: [chinese, atWidths(), inLight] };
/** Accepted by the person after two holds, which stay apart, and a wait for their review: its pull request open, its clean-up waiting. */
export const Waiting: Story = { args: { landing: WAITING }, decorators: light };
/** The same in 中文. */
export const WaitingChinese: Story = { args: { landing: WAITING }, decorators: [chinese, atWidths(), inLight] };
/** From a driver before the advances were kept: the landing says they were not read; handed to its plugin, which did not push it. */
export const Older: Story = { args: { landing: { name: OLDER_BRANCH.branch, branch: OLDER_BRANCH } }, decorators: light };
/** A landing into the line, from the trace's gap: merged and accepted, no branch kept, nothing pushed. */
export const MergedIntoTheLine: Story = {
  args: { landing: { name: 'main', branch: null, gap: 'merge-accepted', due: WAITING_DUE, units: WAITING_UNITS } },
  decorators: light,
};
