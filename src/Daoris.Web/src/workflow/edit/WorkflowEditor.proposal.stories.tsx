import { type ReactNode, useEffect } from 'react';
import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { registerAskFill } from '../../help/askFill';
import { MERGE_GRAPH, RELEASE_GRAPH } from '../graphFixtures';
import { HOLDS, judgedFor, LIST, PALETTE, ROLES } from './editFixtures';
import { proposalOf, RELEASE_PROPOSAL } from './proposalFixtures';
import type { ProposalShown } from './proposalTake';
import { WorkflowEditor } from './WorkflowEditor';

// Ask Daoris's proposal on the editor's chart, drawn from props: the release workflow proposed against the
// Merge workflow open in the editor, its new steps and ways drawn beside the draft and marked in words, the bar naming the agent and the
// model that made it, and its changes one by one; the same proposal after the person took away a way it renumbers, two of its changes
// refused with *Ask again*; the Merge workflow proposed against the release workflow, its steps to remove drawn dashed; and a proposal
// against a draft this window no longer holds. In English and 中文, both themes, at the main area's wide, half and narrow widths (80, 39
// and 25rem). The measured-box check draws them in a browser: no edge through a box, no chip over one.

const nothing = () => {};
const pressed = async () => ({});

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** A window with Ask Daoris in it, so a change the draft no longer fits offers *Ask again*. */
function WithAsk({ children }: { children: ReactNode }) {
  useEffect(() => registerAskFill(nothing), []);
  return <>{children}</>;
}
const asking: Decorator = (Story) => <WithAsk><Story /></WithAsk>;

const MAKER = { agent: 'Claude Code', models: ['claude-opus-5-5'] };

const RELEASE: ProposalShown = { ...RELEASE_PROPOSAL, why: 'You asked for a release after the line: find it, record it and copy it.', problems: 0, maker: MAKER };

const MERGE: ProposalShown = {
  ...proposalOf(RELEASE_GRAPH, MERGE_GRAPH), why: 'You asked for the Merge workflow back: no checks and no release.', problems: 0, maker: MAKER,
};

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor/Proposal',
  component: WorkflowEditor,
  args: {
    list: LIST, palette: PALETTE, error: null, roles: ROLES, shown: null, onShow: nothing, judged: HOLDS, judging: false, onJudge: nothing,
    saving: false, onSave: async () => HOLDS, using: false, onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: MERGE_GRAPH, name: 'Merge workflow' },
    proposal: RELEASE, onSettleProposal: pressed, onDismissProposal: pressed,
  },
  render: (args) => <WorkflowEditor {...args} judgedOf={args.judgedOf ?? (args.startFrom ? judgedFor(args.startFrom.graph) : null)} />,
  decorators: [storyWidths.wide, asking],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

/** **Fresh**: the new steps beside the draft's, each marked *new*, a moved way to remove and new, and every change still to decide. */
export const Proposal: Story = {};
export const ProposalHalf: Story = { decorators: [storyWidths.half, asking] };
export const ProposalNarrow: Story = { decorators: [storyWidths.narrow, asking] };
export const ProposalChinese: Story = { decorators: [storyWidths.wide, chinese, asking] };
export const ProposalChineseHalf: Story = { decorators: [storyWidths.half, chinese, asking] };
export const ProposalChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese, asking] };
export const ProposalDark: Story = { decorators: [storyWidths.wide, dark, asking] };
export const ProposalDarkHalf: Story = { decorators: [storyWidths.half, dark, asking] };
export const ProposalChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark, asking] };

const WITHOUT_C = { ...MERGE_GRAPH, wires: MERGE_GRAPH.wires.filter((wire) => wire.id !== 'c') };

/** **A stale change**: the person took away the landing's way to the line, so the two changes that touch it say so, with *Ask again*. */
export const Stale: Story = { args: { startFrom: { graph: WITHOUT_C, name: 'Merge workflow' } } };
export const StaleHalf: Story = { ...Stale, decorators: [storyWidths.half, asking] };
export const StaleChineseNarrow: Story = { ...Stale, decorators: [storyWidths.narrow, chinese, asking] };
export const StaleDark: Story = { ...Stale, decorators: [storyWidths.wide, dark, asking] };

/** **Steps to remove**: the checks and the release's three steps drawn dashed and marked *to remove*, their ways with them. */
export const Removes: Story = { args: { proposal: MERGE, startFrom: { graph: RELEASE_GRAPH, name: 'Release workflow' } } };
export const RemovesHalf: Story = { ...Removes, decorators: [storyWidths.half, asking] };
export const RemovesChineseDark: Story = { ...Removes, decorators: [storyWidths.wide, chinese, dark, asking] };

/** **A draft this window no longer holds**: nothing of it can be taken; *Ask again* and *Not now*. */
export const UnknownBase: Story = { args: { proposal: { ...RELEASE, base: null } } };
export const UnknownBaseChineseNarrow: Story = { ...UnknownBase, decorators: [storyWidths.narrow, chinese, asking] };
