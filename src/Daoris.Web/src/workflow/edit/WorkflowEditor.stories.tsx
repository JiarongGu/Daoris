import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { APPEND, HOLDS, LIST, PALETTE, REFUSED, REFUSED_GRAPH, RELEASE_GRAPH, ROLES } from './editFixtures';
import { WorkflowEditor } from './WorkflowEditor';

// The step editor (WORKFLOWEDIT1b), drawn from props: the release default composed and judged sound, the same steps refused at the way
// back that goes forward and at the work's role, with a rule this page has no words for said in the driver's; a version appended to a
// workflow two places choose; nothing composed yet; and its reads on their way and refused. In English and 中文, both themes, at the main
// area's wide, half and narrow widths (80, 39 and 25rem).

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor',
  component: WorkflowEditor,
  args: {
    list: LIST, palette: PALETTE, error: null, roles: ROLES, shown: null, onShow: nothing, judged: HOLDS, judging: false, onJudge: nothing,
    saving: false, onSave: async () => HOLDS, using: false, onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: RELEASE_GRAPH, name: 'Release after you accept' },
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

/** **The release default, composed**: its steps drawn on the canvas and listed, each with where it goes on, judged sound. */
export const Release: Story = {};
export const ReleaseHalf: Story = { decorators: [storyWidths.half] };
export const ReleaseNarrow: Story = { decorators: [storyWidths.narrow] };
export const ReleaseChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const ReleaseChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ReleaseChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const ReleaseDark: Story = { decorators: [storyWidths.half, dark] };
export const ReleaseChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/**
 * **Refused where it is wrong**: the way back from the review into the landing said at that way, the work's role at the work, each in the
 * page's words with the driver's beneath; a rule this page has no words for said above, in the driver's.
 */
export const Refused: Story = { args: { judged: REFUSED, startFrom: { graph: REFUSED_GRAPH } } };
export const RefusedNarrow: Story = { ...Refused, decorators: [storyWidths.narrow] };
export const RefusedChineseHalf: Story = { ...Refused, decorators: [storyWidths.half, chinese] };
export const RefusedDark: Story = { ...Refused, decorators: [storyWidths.wide, dark] };

/** **An append**: a saved workflow at v2, chosen in two places, each named before the save, which the person authorizes. */
export const Append: Story = {
  args: { judged: APPEND, startFrom: { graph: RELEASE_GRAPH, id: 'docs-release', name: 'Docs release', base: 2 } },
};
export const AppendChineseNarrow: Story = { ...Append, decorators: [storyWidths.narrow, chinese] };

/** **Nothing composed yet**: the source to start from and the palette's steps, and no judgement. */
export const Empty: Story = { args: { startFrom: undefined, judged: null } };
export const EmptyChinese: Story = { ...Empty, decorators: [storyWidths.half, chinese] };

/** Its reads on their way, and refused in the driver's words. */
export const Reading: Story = { args: { palette: null, list: null } };
export const Unread: Story = { args: { palette: null, list: null, error: new Error('the driver is still coming up — try again in a moment.') } };
export const UnreadChineseDark: Story = { ...Unread, decorators: [storyWidths.narrow, chinese, dark] };
