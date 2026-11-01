import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { MERGE_GRAPH, RELEASE_GRAPH, TWO_OUTCOMES_GRAPH } from '../graphFixtures';
import { APPEND, HOLDS, LIST, PALETTE, REFUSED, REFUSED_GRAPH, ROLES } from './editFixtures';
import { WorkflowEditor } from './WorkflowEditor';

// The workflow editor (WORKFLOWEDIT1b, FLOWCHART2b; D223 §3), drawn from props: the release default on its chart, a ＋ on each way and in
// each ◎, judged sound; the merge workflow it is composed from; a decision whose two outcomes reach one step; the same release refused at
// a way back that goes forward, each refusal with a door to its step; a version appended to a workflow two places choose; nothing composed
// yet, with the presets and saved workflows to begin from; and its reads on their way and refused. In English and 中文, both themes, at the
// main area's wide, half and narrow widths (80, 39 and 25rem): the inspector stands beside the chart at 80rem and is a drawer below it.
// The measured-box check draws the canvas stories in a browser: no ＋ over a node, a label or another ＋.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor',
  component: WorkflowEditor,
  args: {
    list: LIST, palette: PALETTE, error: null, roles: ROLES, shown: null, onShow: nothing, judged: HOLDS, judging: false, onJudge: nothing,
    saving: false, onSave: async () => HOLDS, using: false, onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: RELEASE_GRAPH, name: 'Release workflow' },
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

/** **The release default, on its chart**: a ＋ on each way a step can stand on and in the ◎ of *none*, judged sound. */
export const Release: Story = {};
export const ReleaseHalf: Story = { decorators: [storyWidths.half] };
export const ReleaseNarrow: Story = { decorators: [storyWidths.narrow] };
export const ReleaseChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const ReleaseChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ReleaseChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const ReleaseDark: Story = { decorators: [storyWidths.wide, dark] };
export const ReleaseDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ReleaseChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** **The merge workflow**, which the release workflow is composed from: one chain, a ＋ on each of its ways. */
export const Merge: Story = { args: { startFrom: { graph: MERGE_GRAPH, name: 'Merge workflow' } } };
export const MergeHalf: Story = { ...Merge, decorators: [storyWidths.half] };
export const MergeChineseHalf: Story = { ...Merge, decorators: [storyWidths.half, chinese] };
export const MergeChineseDark: Story = { ...Merge, decorators: [storyWidths.wide, chinese, dark] };

/** **Two outcomes reaching one step**, each from its own port with its own ＋, the third ending at a ◎ with its ＋. */
export const TwoOutcomes: Story = { args: { startFrom: { graph: TWO_OUTCOMES_GRAPH } } };
export const TwoOutcomesHalf: Story = { ...TwoOutcomes, decorators: [storyWidths.half] };

/** **Refused where it is wrong**: *n to put right* in the head, each refusal a door to the step it names. */
export const Refused: Story = { args: { judged: REFUSED, startFrom: { graph: REFUSED_GRAPH } } };
export const RefusedNarrow: Story = { ...Refused, decorators: [storyWidths.narrow] };
export const RefusedChineseHalf: Story = { ...Refused, decorators: [storyWidths.half, chinese] };
export const RefusedDark: Story = { ...Refused, decorators: [storyWidths.wide, dark] };

/** **An append**: a saved workflow at v2, chosen in two places, each named before the save, which the person authorizes. */
export const Append: Story = {
  args: { judged: APPEND, startFrom: { graph: RELEASE_GRAPH, id: 'docs-release', name: 'Docs release', base: 2 } },
};
export const AppendChineseNarrow: Story = { ...Append, decorators: [storyWidths.narrow, chinese] };

/** **Nothing composed yet**: the presets and the saved workflows to begin from, and the picker a press away. */
export const Empty: Story = { args: { startFrom: undefined, judged: null } };
export const EmptyChinese: Story = { ...Empty, decorators: [storyWidths.half, chinese] };
export const EmptyDarkNarrow: Story = { ...Empty, decorators: [storyWidths.narrow, dark] };

/** Its reads on their way, and refused in the driver's words. */
export const Reading: Story = { args: { palette: null, list: null } };
export const Unread: Story = { args: { palette: null, list: null, error: new Error('the driver is still coming up — try again in a moment.') } };
export const UnreadChineseDark: Story = { ...Unread, decorators: [storyWidths.narrow, chinese, dark] };
