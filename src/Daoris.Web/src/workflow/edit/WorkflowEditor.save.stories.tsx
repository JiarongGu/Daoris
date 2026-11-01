import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { MERGE_GRAPH } from '../graphFixtures';
import type { WorkflowSaveAnswer, WorkflowUseAnswer } from './answers';
import { APPEND, HOLDS, judgedFor, LIST, PALETTE, ROLES } from './editFixtures';
import { WorkflowEditor } from './WorkflowEditor';

// The editor's save and choice (SETUPUX1s; the Setup design's §2.3): *Review and save* once the draft's judgement holds, the review that
// says what is saved and which places an append reaches (one consent for exactly those), a new workflow saved by its name alone, and *Use
// here* reading the plan of the choice for a draft that is the version it was saved as. In English and 中文, both themes, at the main
// area's wide, half and narrow widths (80, 39 and 25rem). A `play` presses what a person would.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const planned = async (): Promise<WorkflowUseAnswer> => ({
  done: false, plan: true, refused: null, message: 'engine would follow it.', changed: true, reach: [], expected: 'use-aaaa',
  lowers: ['Your review stays your part.'], places: [{ repository: 'engine', was: 'Current', now: 'Docs release v2', lowers: [] }],
});

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor/Save',
  component: WorkflowEditor,
  args: {
    list: { ...LIST, use: ['plan'] }, palette: PALETTE, error: null, roles: ROLES, shown: null, onShow: nothing, judged: APPEND, judging: false, onJudge: nothing,
    saving: false, onSave: async (): Promise<WorkflowSaveAnswer> => HOLDS, using: false,
    onUse: async (): Promise<WorkflowUseAnswer> => ({ done: true, refused: null, message: null, changed: true, reach: [] }), onPlan: planned,
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: MERGE_GRAPH, id: 'docs-release', name: 'Docs release', base: 2 },
  },
  render: (args) => <WorkflowEditor {...args} judgedOf={args.judgedOf ?? (args.startFrom ? judgedFor(args.startFrom.graph) : null)} />,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

const press = (canvasElement: HTMLElement, name: RegExp) => {
  const button = [...canvasElement.querySelectorAll<HTMLElement>('button')].find((each) => name.test(each.textContent ?? ''));
  button?.click();
  return button;
};
const settle = () => new Promise((resolve) => setTimeout(resolve, 120));

const review: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(Review and save|审阅并保存)$/);
  await settle();
};

/** **Review and save, an append**: the version it starts from, the two places a save reaches and one consent for exactly those. */
export const ReviewAppend: Story = { play: review };
export const ReviewAppendHalf: Story = { play: review, decorators: [storyWidths.half] };
export const ReviewAppendNarrow: Story = { play: review, decorators: [storyWidths.narrow] };
export const ReviewAppendChinese: Story = { play: review, decorators: [storyWidths.wide, chinese] };
export const ReviewAppendChineseHalf: Story = { play: review, decorators: [storyWidths.half, chinese] };
export const ReviewAppendChineseNarrow: Story = { play: review, decorators: [storyWidths.narrow, chinese] };
export const ReviewAppendDark: Story = { play: review, decorators: [storyWidths.wide, dark] };
export const ReviewAppendDarkHalf: Story = { play: review, decorators: [storyWidths.half, dark] };
export const ReviewAppendChineseDarkNarrow: Story = { play: review, decorators: [storyWidths.narrow, chinese, dark] };

/** **A new workflow**: its name is all that is asked, and nothing is authorized. */
export const ReviewNew: Story = {
  play: review,
  args: { judged: HOLDS, startFrom: { graph: MERGE_GRAPH, name: 'Merge workflow' } },
};
export const ReviewNewNarrow: Story = { ...ReviewNew, decorators: [storyWidths.narrow] };
export const ReviewNewChineseHalf: Story = { ...ReviewNew, decorators: [storyWidths.half, chinese] };
export const ReviewNewDark: Story = { ...ReviewNew, decorators: [storyWidths.wide, dark] };

/** **Waiting on the judgement**: the draft is not known to hold, so there is nothing to review yet. */
export const NotJudged: Story = { args: { judged: null } };
export const NotJudgedChineseNarrow: Story = { ...NotJudged, decorators: [storyWidths.narrow, chinese] };

const use: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(Use here…|在此使用…)$/);
  await settle();
};

/** **Use here**, for a draft that is the saved version: the plan of the choice is read, and *Use here* waits on its token. */
export const UseHere: Story = { args: { judged: HOLDS }, play: use };
export const UseHereHalf: Story = { ...UseHere, decorators: [storyWidths.half] };
export const UseHereNarrow: Story = { ...UseHere, decorators: [storyWidths.narrow] };
export const UseHereChineseNarrow: Story = { ...UseHere, decorators: [storyWidths.narrow, chinese] };
export const UseHereDark: Story = { ...UseHere, decorators: [storyWidths.wide, dark] };

/** A driver that announces no plan: *Use here* is today's press. */
export const UseHereUnannounced: Story = { args: { judged: HOLDS, list: LIST, onPlan: undefined } };
