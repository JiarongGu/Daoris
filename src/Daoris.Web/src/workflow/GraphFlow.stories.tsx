import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Meta, StoryObj } from '@storybook/react-vite';
import { Flow } from '../design/Flow';
import { atWidths, inDark, inLight } from '../design/flowFrames';
import { chinese } from '../design/storyLanguage';
import { MERGE_GRAPH, RELEASE_GRAPH } from './graphFixtures';
import { drawnFromJson, graphFlow } from './graphFlow';

// One drawing of a workflow graph (SETUPUX1n; the Setup design's §2.3): the shipped release workflow and the merge workflow, always
// in sections and never left to right, at 80rem and 39rem top to bottom and at 25rem as the rail, in both languages and both themes.
// The measured-box check draws these in a rendered browser: no edge through a card, no crossing.

/** The frames' widths: 80, 39 and 25rem. */
const REM = [1280, 624, 400];

function Drawn({ graph, open = false }: { graph: unknown; open?: boolean }) {
  const { t, i18n } = useTranslation();
  const [opened, setOpened] = useState(open);
  const flow = graphFlow(t, drawnFromJson(t, i18n, graph), { label: t('workflow.edit.drawing'), holds: { open: opened, toggle: () => setOpened((was) => !was) } });
  return <Flow label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track} />;
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
