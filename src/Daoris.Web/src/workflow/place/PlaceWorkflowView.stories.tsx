import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { LIST, PALETTE } from '../edit/editFixtures';
import { FULL, GATES_CHANGED, INTEGRATION_ACCEPTED, INTEGRATION_CHANGED, INTEGRATION_UNACCEPTED, NOTHING_SET, START } from '../fixtures';
import { PlaceWorkflowView } from './PlaceWorkflowView';

// A repository place's Workflow (AGENTSPLACE1h; the Agents-place design §3, §5), every state the design names drawn from props: the route and
// chart with Gates waiting on both choices, Gates settled, Change… and Edit steps… open, the answer on its way and refused, at the main
// area's 80, 39 and 25 rem, in 中文 and in dark.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const editor = {
  list: LIST, palette: PALETTE, error: null, roles: [], shown: null, onShow: nothing, judged: null, judging: false, onJudge: nothing,
  saving: false, onSave: async () => { throw new Error('a story saves nothing'); }, using: false,
  onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
};

const meta: Meta<typeof PlaceWorkflowView> = {
  title: 'Workflow/PlaceWorkflowView',
  component: PlaceWorkflowView,
  args: {
    name: 'engine',
    current: { ...FULL, start: { ...GATES_CHANGED, integration: INTEGRATION_CHANGED } },
    doors: { workspaceSetup: nothing },
    onAcceptGates: (answered) => answered.done(),
    onAcceptIntegration: (_request, answered) => answered.done(),
    onVerify: (_ask, answered) => answered.done(),
    editor,
  },
  // A place's page in the Agents place's main area, at the frame's wide width.
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof PlaceWorkflowView>;

/**
 * **Both acceptances waiting**: the landed route's gates changed since they were accepted (verification stops) and the integration
 * declaration changed too (integration goes on under the last accepted blob). Each declaration is drawn once, in Gates, with its two choices.
 */
export const GatesWaiting: Story = {};
export const GatesWaitingHalf: Story = { decorators: [storyWidths.half] };
export const GatesWaitingNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, at each width. */
export const GatesWaitingChinese: Story = { decorators: [chinese] };
export const GatesWaitingChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const GatesWaitingChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** The same in dark, at each width. */
export const GatesWaitingDark: Story = { decorators: [dark] };
export const GatesWaitingDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const GatesWaitingDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** **Settled**: both declarations accepted as the tip holds them; the choices name their standing and offer no press but the withdrawal. */
export const GatesSettled: Story = { args: { current: { ...FULL, start: { ...START, integration: INTEGRATION_ACCEPTED } } } };
export const GatesSettledNarrowChineseDark: Story = { ...GatesSettled, decorators: [storyWidths.narrow, chinese, dark] };

/** **Integration never accepted** beside accepted landed gates: one choice waits. */
export const OneChoiceWaiting: Story = { args: { current: { ...FULL, start: { ...START, integration: INTEGRATION_UNACCEPTED } } } };

/** **Nothing asks**: no landed route verifies the work, and the line declares no integration. */
export const NothingToAccept: Story = { args: { current: NOTHING_SET } };

/** The answer on its way, and refused in the driver's sentence. */
export const Reading: Story = { args: { current: undefined, reading: true } };
export const Refused: Story = {
  args: { current: undefined, refusal: 'the driver is still coming up — its service is not answering yet. A moment.' },
};

/** No editor handed (a shell before the step editor): the steps' section says the terminal's way alone. */
export const WithoutTheEditor: Story = { args: { editor: undefined } };
