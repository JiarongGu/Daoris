import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { FULL, NOTHING_SET, WITH_A_FAILURE_PATH } from './fixtures';
import { answerOf, GO_AHEAD_REFUSAL, IN_REVIEW, LANDED } from './runFixtures';
import { WorkflowChart } from './WorkflowChart';
import { WorkflowRunView } from './WorkflowRunView';

// A workflow and its run drawn as flowcharts (FLOWCHART1d; the flowchart design §2.4, §2.5, §4.4, §5.1), at the frame's widths:
// 300 px, the side bar's floor, where a step's 19rem does not fit and the rail is drawn; 400 and 544 top to bottom, a step a
// rank; 1200 the same, never left to right, since the steps are read under *Before it lands* and *After it lands*. A failure path
// is a rank of two, so it draws only at 1200. The measured-box check draws these stories in a rendered browser and measures every
// box. The tab's and the side bar view's own stories are `WorkflowTab.stories.tsx` and `WorkflowRunView.stories.tsx`.

const nothing = () => {};

const meta: Meta<typeof WorkflowChart> = {
  title: 'Workflow/WorkflowFlow',
  component: WorkflowChart,
  args: { page: 'repository', name: 'engine', workflow: FULL, doors: { setup: nothing, workspaceSetup: nothing } },
};
export default meta;

type Story = StoryObj<typeof WorkflowChart>;

/** At every width, in light. The first decorator is the innermost: the language, then the frames, then the theme's page. */
const light = [atWidths(), inLight];

/**
 * **Current, everything at once**: the plugin that may hold a start under *Before it lands*, the work, the workspace's second
 * opinion, the repository's look, an automatic landing on a branch, the divider, and the pull request the person merges.
 */
export const Current: Story = { decorators: light };
export const CurrentDark: Story = { decorators: [atWidths(), inDark] };
export const CurrentChinese: Story = { decorators: [chinese, atWidths(), inLight] };
export const CurrentChineseDark: Story = { decorators: [chinese, atWidths(), inDark] };

/** **Nothing set anywhere**: the work, then a merge the person accepts, and the end; one section. */
export const NothingSet: Story = { args: { workflow: NOTHING_SET }, decorators: light };

/** **A failure path**: a newer driver's check after the pull request, sent back once, then you, on a dashed edge beside the end. */
export const AFailurePath: Story = { args: { workflow: WITH_A_FAILURE_PATH }, decorators: light };
export const AFailurePathChinese: Story = { args: { workflow: WITH_A_FAILURE_PATH }, decorators: [chinese, atWidths(), inLight] };

/** **A run in review**: the work done, the look current in open's hue, the landing, the pull request and the end not reached. */
export const RunInReview: Story = { render: () => <WorkflowRunView answer={answerOf(IN_REVIEW)} here="work" />, decorators: light };
export const RunInReviewDark: Story = { ...RunInReview, decorators: [atWidths(), inDark] };
export const RunInReviewChinese: Story = { ...RunInReview, decorators: [chinese, atWidths(), inLight] };

/**
 * **Steps passed over, and a go-ahead's refusal**: a named version's run, its second opinion passed over where nothing asked for
 * one and its look skipped by the person, each in its own words; the landing waits for the accept; the go-ahead's refusal waits
 * for you, on its failure path. No path is drawn: a run keeps where each step stands, not the way it went.
 */
export const RunPassedOver: Story = { render: () => <WorkflowRunView answer={answerOf(GO_AHEAD_REFUSAL)} />, decorators: light };
export const RunPassedOverChinese: Story = { ...RunPassedOver, decorators: [chinese, atWidths(), inLight] };

/** **Landed**: the run finished, the end of the line reached and done. */
export const RunLanded: Story = { render: () => <WorkflowRunView answer={answerOf(LANDED)} />, decorators: light };
