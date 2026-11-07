import { type ReactNode, useEffect } from 'react';
import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { registerAskFill } from '../../help/askFill';
import { RELEASE_GRAPH as SHIPPED_RELEASE } from '../graphFixtures';
import type { WorkflowSaveAnswer } from './answers';
import { HOLDS, judgedFor, LIST, PALETTE, REFUSED, REFUSED_GRAPH, ROLES } from './editFixtures';
import { WorkflowEditor } from './WorkflowEditor';

// The driver's judgement on the editor's chart, drawn from props: a refusal at its step (declined's border and
// its words) and at its way (a ⚠ chip, its sentence in the source's foot), the head's *n to put right* with a door, *Ask why* and *Fix it*
// to each where the window has Ask Daoris; a refusal inside the folded holds opening them; and the two states in which the last answer is
// no longer the draft's, an edit before the new answer and a request that failed. In English and 中文, both themes, at the main area's
// wide, half and narrow widths (80, 39 and 25rem).

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** A window with Ask Daoris in it, so the head offers *Ask why* and *Fix it*. */
function WithAsk({ children }: { children: ReactNode }) {
  useEffect(() => registerAskFill(nothing), []);
  return <>{children}</>;
}
const asking: Decorator = (Story) => <WithAsk><Story /></WithAsk>;

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor/Judgement',
  component: WorkflowEditor,
  args: {
    list: LIST, palette: PALETTE, error: null, roles: ROLES, shown: null, onShow: nothing, judged: REFUSED, judging: false, onJudge: nothing,
    saving: false, onSave: async () => HOLDS, using: false, onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: REFUSED_GRAPH, name: 'Release workflow' },
  },
  render: (args) => <WorkflowEditor {...args} judgedOf={args.judgedOf ?? (args.startFrom ? judgedFor(args.startFrom.graph) : null)} />,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

/** **A role refused at its step and a way back at its way**, and a rule this page has no words for in the head alone; no Ask Daoris here. */
export const Refused: Story = {};

/** **With Ask Daoris in the window**: *Ask why* and *Fix it* beside each refusal in the head. */
export const RefusedAsking: Story = { decorators: [storyWidths.wide, asking] };
export const RefusedAskingHalf: Story = { decorators: [storyWidths.half, asking] };
export const RefusedAskingNarrow: Story = { decorators: [storyWidths.narrow, asking] };
export const RefusedAskingChinese: Story = { decorators: [storyWidths.wide, chinese, asking] };
export const RefusedAskingChineseHalf: Story = { decorators: [storyWidths.half, chinese, asking] };
export const RefusedAskingChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese, asking] };
export const RefusedAskingDark: Story = { decorators: [storyWidths.wide, dark, asking] };
export const RefusedAskingDarkHalf: Story = { decorators: [storyWidths.half, dark, asking] };
export const RefusedAskingChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark, asking] };

const INSIDE_HOLDS: WorkflowSaveAnswer = {
  ...HOLDS, done: false, refused: 'refused', version: null,
  problems: [{ code: 'unreachable', wire: null, node: 'content', words: 'no way from the start reaches `content`.' }],
};

/** **A refusal inside the holds**: the folded group opens on the step it names, and stays open. */
export const InsideHolds: Story = { args: { judged: INSIDE_HOLDS, startFrom: { graph: SHIPPED_RELEASE, name: 'Release workflow' } }, decorators: [storyWidths.wide, asking] };
export const InsideHoldsHalf: Story = { ...InsideHolds, decorators: [storyWidths.half, asking] };
export const InsideHoldsChineseNarrow: Story = { ...InsideHolds, decorators: [storyWidths.narrow, chinese, asking] };

/** **An edit since the answer**: the refusals went with the draft they judged; the head says it is judging, and the chart is unmarked. */
export const EditedSinceTheAnswer: Story = { args: { judgedOf: 'g1-0000000000000000', judging: true } };
export const EditedSinceTheAnswerChineseNarrow: Story = { ...EditedSinceTheAnswer, decorators: [storyWidths.narrow, chinese] };

/** **A request that failed**: the draft is not known to hold, and nothing is marked. */
export const CouldNotBeJudged: Story = { args: { judged: null, judgeFailed: judgedFor(REFUSED_GRAPH) } };
export const CouldNotBeJudgedChineseDark: Story = { ...CouldNotBeJudged, decorators: [storyWidths.half, chinese, dark] };
