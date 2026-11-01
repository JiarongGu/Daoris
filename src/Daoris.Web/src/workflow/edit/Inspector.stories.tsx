import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useTranslation } from 'react-i18next';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { MERGE_GRAPH, RELEASE_GRAPH } from '../graphFixtures';
import { PALETTE, ROLES } from './editFixtures';
import { stepTitle } from './editWords';
import { draftOf, type GraphDraft, setLandMethod, setOption } from './graphDraft';
import { Inspector } from './Inspector';

// The inspector's options, tail and inputs (SETUPUX1r; D221 §2.3): a landing's options in the page's words with the names under Details, the
// tail its method brought, a pattern's preview, the inputs proposed from the step before, and an input two steps could feed, which asks. In
// English and 中文, both themes, at the main area's wide, half and narrow widths (80, 39 and 25rem).

const nothing = () => {};
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The merge workflow's landing made a pull request on a branch named by a pattern: its tail brought in the one change. */
const HOSTED: GraphDraft = setOption(setLandMethod(PALETTE, draftOf(MERGE_GRAPH), 'landing', 'pull-request').draft, 'landing', 'pattern', 'feature/{quest}-{slug}');

/** The release workflow with the copy's input left unfed: the record's and the find's both make it. */
const ASKING: GraphDraft = (() => {
  const draft = draftOf(RELEASE_GRAPH);
  return { ...draft, wires: draft.wires.filter((wire) => wire.kind !== 'data' || wire.to !== 'publish') };
})();

function Shown({ draft, step, lines }: { draft: GraphDraft; step: string; lines?: string[] }) {
  const { t, i18n } = useTranslation();
  const node = draft.nodes.find((each) => each.id === step)!;
  const titleOf = (id: string) => {
    const found = draft.nodes.find((each) => each.id === id);
    return found ? stepTitle(t, i18n, PALETTE, found) : id;
  };
  return (
    <div className="grid min-w-0 gap-3 rounded-card border border-line bg-raised p-3">
      <h4 className="m-0 text-body font-semibold text-ink">{titleOf(step)}</h4>
      <Inspector
        palette={PALETTE} draft={draft} roles={ROLES} lines={lines} node={node} titleOf={titleOf} problems={{ atStep: [], atWire: () => [] }}
        confirming={null} onConfirm={nothing}
        acts={{ onChange: nothing, onAfter: nothing, onMove: nothing, onDuplicate: nothing, onCopy: nothing, onRemove: nothing,
          onTarget: nothing, onAdd: nothing, onPick: nothing, onNew: nothing, onInsert: nothing }}
      />
    </div>
  );
}

const meta: Meta<typeof Shown> = {
  title: 'Workflow/Inspector',
  component: Shown,
  args: { draft: HOSTED, step: 'landing', lines: ['main'] },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Shown>;

/** **A landing**: its method in words and the tail it brought, the pattern's preview, the line chosen from the place's. */
export const Landing: Story = {};
export const LandingHalf: Story = { decorators: [storyWidths.half] };
export const LandingNarrow: Story = { decorators: [storyWidths.narrow] };
export const LandingChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const LandingChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const LandingChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const LandingDark: Story = { decorators: [storyWidths.wide, dark] };
export const LandingDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const LandingChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** **An input proposed**: the record takes the find's release, drawn under *Carries*. */
export const Proposed: Story = { args: { draft: draftOf(RELEASE_GRAPH), step: 'release' } };
export const ProposedHalf: Story = { ...Proposed, decorators: [storyWidths.half, chinese] };

/** **An input that asks**: two steps make what the copy takes, and neither is chosen for it. */
export const Asking: Story = { args: { draft: ASKING, step: 'publish' } };
export const AskingHalf: Story = { ...Asking, decorators: [storyWidths.half] };
export const AskingChineseNarrow: Story = { ...Asking, decorators: [storyWidths.narrow, chinese] };
export const AskingDark: Story = { ...Asking, decorators: [storyWidths.wide, dark] };
export const AskingChineseDarkNarrow: Story = { ...Asking, decorators: [storyWidths.narrow, chinese, dark] };
