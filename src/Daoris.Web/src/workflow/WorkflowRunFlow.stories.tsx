import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { Button } from '../design/ui';
import { answerOf, GRAPH_RUN_DECIDED, GRAPH_RUN_RELEASED, GRAPH_RUN_REVIEW } from './runFixtures';
import type { NodeWordAsk } from './run';
import { WorkflowRunView } from './WorkflowRunView';

// A run's view of where its work stands (SETUPUX1u; D221, the Setup design's §2.3), at the side bar's floor (300 px), 25rem (400 px) and 45%
// of the install's window (696 px), in each language and theme: its head naming the bound version and how it was chosen, one sentence of
// where the work stands, the step the run stands at open with its owner's control, and a decision's returned outcome with the route
// not taken told apart. The measured-box check draws these in a rendered browser (`measured/flow.spec.ts`, the `runview` book); the view's
// other states are `WorkflowRunView.stories.tsx`.

const meta: Meta<typeof WorkflowRunView> = { title: 'Workflow/WorkflowRunFlow', component: WorkflowRunView, args: { here: null } };
export default meta;

type Story = StoryObj<typeof WorkflowRunView>;

const frames = atWidths([300, 400, 696]);
const light = [frames, inLight];
const dark = [frames, inDark];

/** What a press of the person's word answers in a story: kept, so the ask closes. */
const kept = async (ask: NodeWordAsk) => ({ run: ask.run, node: ask.node, act: ask.act, done: true });

/** **A release run, outcome `release`**: the find came to release, which was recorded and copied; *Finished* follows the copy, from the record. */
export const ReleaseRun: Story = { args: { answer: answerOf(GRAPH_RUN_RELEASED) }, decorators: light };
export const ReleaseRunDark: Story = { ...ReleaseRun, decorators: dark };
export const ReleaseRunChinese: Story = { ...ReleaseRun, decorators: [chinese, frames, inLight] };
export const ReleaseRunChineseDark: Story = { ...ReleaseRun, decorators: [chinese, frames, inDark] };

/** **A release run, outcome `none`**: no route follows the find's *none*; the release and the copy are not taken, and *Finished* follows *No further step*. */
export const NoneRun: Story = { args: { answer: answerOf(GRAPH_RUN_DECIDED) }, decorators: light };
export const NoneRunDark: Story = { ...NoneRun, decorators: dark };
export const NoneRunChinese: Story = { ...NoneRun, decorators: [chinese, frames, inLight] };
export const NoneRunChineseDark: Story = { ...NoneRun, decorators: [chinese, frames, inDark] };

/** **A review waiting on you**: the step the run stands at open, with *Accept…* and *Ask for changes…* under it; the others a title and one fact. */
const accept = <Button>Accept…</Button>;
export const ReviewRun: Story = { args: { answer: answerOf(GRAPH_RUN_REVIEW), onNodeWord: kept, controls: { review: accept } }, decorators: light };
export const ReviewRunDark: Story = { ...ReviewRun, decorators: dark };
export const ReviewRunChinese: Story = { ...ReviewRun, decorators: [chinese, frames, inLight] };
export const ReviewRunChineseDark: Story = { ...ReviewRun, decorators: [chinese, frames, inDark] };
