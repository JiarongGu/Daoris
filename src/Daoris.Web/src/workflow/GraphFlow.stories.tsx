import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { FlowChart } from './chart/FlowChart';
import { MERGE_GRAPH, RELEASE_GRAPH, TWO_OUTCOMES_GRAPH } from './graphFixtures';
import { drawnFromJson, graphFlow } from './graphFlow';

// One drawing of a workflow graph (SETUPUX1n; the Setup design's §2.3), through the chart's components (FLOWCHART2a): the shipped release
// workflow and the merge workflow, always in sections and never left to right, at 80rem and 39rem top to bottom and at 25rem as the rail,
// in both languages and both themes. The measured-box check draws these in a rendered browser: no edge through a card, no crossing, and
// each decision's route leaving its outcome's port.

/** The frames' widths: 80, 39 and 25rem. */
const REM = [1280, 624, 400];

function Drawn({ graph, open = false, selected }: { graph: unknown; open?: boolean; selected?: string | null }) {
  const { t, i18n } = useTranslation();
  const [opened, setOpened] = useState(open);
  const flow = graphFlow(t, drawnFromJson(t, i18n, graph), { label: t('workflow.edit.drawing'), holds: { open: opened, toggle: () => setOpened((was) => !was) } });
  return (
    <FlowChart
      label={flow.label}
      nodes={flow.nodes}
      edges={flow.edges}
      entries={flow.entries}
      sections={flow.sections}
      track={flow.track}
      {...(selected === undefined ? {} : { selected })}
    />
  );
}

const meta: Meta<typeof Drawn> = { title: 'Workflow/GraphFlow', component: Drawn, args: { graph: RELEASE_GRAPH } };
export default meta;

type Story = StoryObj<typeof Drawn>;

const light = [atWidths(REM), inLight];

/** **The release workflow**: the holds one group, your review with its way back, the landing, the line flagged where the work is done, and the release found, recorded and copied, its *none* going nowhere. */
export const Release: Story = { decorators: light };
export const ReleaseDark: Story = { decorators: [atWidths(REM), inDark] };
export const ReleaseChinese: Story = { decorators: [chinese, atWidths(REM), inLight] };
export const ReleaseChineseDark: Story = { decorators: [chinese, atWidths(REM), inDark] };

/** **The holds opened**: the group's three steps drawn, the first saying how to fold them. */
export const ReleaseHoldsOpen: Story = { args: { open: true }, decorators: light };

/** **The merge workflow**: one chain, the line its end, with nothing after it. */
export const Merge: Story = { args: { graph: MERGE_GRAPH }, decorators: light };
export const MergeChinese: Story = { args: { graph: MERGE_GRAPH }, decorators: [chinese, atWidths(REM), inLight] };

/** **Two outcomes reaching one step**: each leaves its own port and stays a way of its own, the third ending at a ◎. */
export const TwoOutcomes: Story = { args: { graph: TWO_OUTCOMES_GRAPH }, decorators: light };
export const TwoOutcomesChinese: Story = { args: { graph: TWO_OUTCOMES_GRAPH }, decorators: [chinese, atWidths(REM), inLight] };
export const TwoOutcomesDark: Story = { args: { graph: TWO_OUTCOMES_GRAPH }, decorators: [atWidths(REM), inDark] };

/** **A step selected**: the data wires of the step that records the release are drawn, dotted, outside the nodes at the right; nothing moves. */
export const ReleaseData: Story = { args: { selected: 'release' }, decorators: light };
export const ReleaseDataChinese: Story = { args: { selected: 'release' }, decorators: [chinese, atWidths(REM), inLight] };
/** The same flow with nothing selected: the lanes are kept free, the wires not drawn. */
export const ReleaseDataUnselected: Story = { args: { selected: null }, decorators: light };
