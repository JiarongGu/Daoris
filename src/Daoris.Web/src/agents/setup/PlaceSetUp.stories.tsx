import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { KEPT_ROW, planOf, SETUP_PRESETS } from './placeFixtures';
import { askOf, deliveriesOf, freshDraft, type SetUpDraft } from './placePlan';
import { PlaceSetUp } from './PlaceSetUp';

// A place's Set up…: how it lands (Keep, the deliveries by id, Not driven), adoption with
// what it builds, driving, the refusals under their parts with their doors, the plan's line and *Review the plan*, at the main area's 80, 39 and
// 25 rem, in 中文 and in dark. The review is `PlaceReview.stories.tsx`.

const nothing = () => {};

const doors = () => nothing;

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const draftOf = (patch: Partial<SetUpDraft> = {}): SetUpDraft => ({ ...freshDraft(['engine']), preset: 'merge-after-accept', adoption: true, builds: 'service', ...patch });

const plannedFor = (draft: SetUpDraft) => ({ draft, plan: planOf(askOf(draft)) });

const meta: Meta<typeof PlaceSetUp> = {
  title: 'Agents/PlaceSetUp',
  component: PlaceSetUp,
  args: {
    place: 'engine', kind: 'repository', choices: [], deliveries: deliveriesOf(SETUP_PRESETS), doorFor: doors, ...plannedFor(draftOf()),
    reading: false, stale: false, problem: null, applying: false, holding: false, holds: {},
    onChange: nothing, onApply: nothing, onHold: nothing, onClose: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof PlaceSetUp>;

export const Plan: Story = {};
export const PlanHalf: Story = { decorators: [storyWidths.half] };
export const PlanNarrow: Story = { decorators: [storyWidths.narrow] };

export const PlanChinese: Story = { decorators: [chinese] };
export const PlanChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const PlanChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const PlanDark: Story = { decorators: [dark] };
export const PlanDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const PlanDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** The suggestion on its way: the plan has not answered. */
export const Reading: Story = { args: { plan: null, reading: true } };

/** A refusal said in place, and no Apply. */
export const Refused: Story = { args: plannedFor(draftOf({ repositories: ['site'], preset: 'pull-request' })) };
export const RefusedNarrow: Story = { ...Refused, decorators: [storyWidths.narrow] };
export const RefusedChinese: Story = { ...Refused, decorators: [chinese] };

/** Each refusal under the part it is about, a door beside the ones whose cause is put right elsewhere, and the plan's line jumping to each part. */
export const RefusedWithDoors: Story = {
  args: plannedFor(draftOf({ repositories: ['site'], preset: 'branch-to-push', opinion: true, look: true, adoption: true, drive: false })),
};
export const RefusedWithDoorsHalf: Story = { ...RefusedWithDoors, decorators: [storyWidths.half] };
export const RefusedWithDoorsNarrow: Story = { ...RefusedWithDoors, decorators: [storyWidths.narrow] };
export const RefusedWithDoorsChinese: Story = { ...RefusedWithDoors, decorators: [chinese] };
export const RefusedWithDoorsChineseNarrow: Story = { ...RefusedWithDoors, decorators: [storyWidths.narrow, chinese] };
export const RefusedWithDoorsDark: Story = { ...RefusedWithDoors, decorators: [dark] };

/** Keep how it lands, suggested where the landing is chosen or a saved workflow is followed, saying what it follows. */
export const Keep: Story = { args: { ...plannedFor(draftOf({ preset: 'keep', adoption: false })), plan: planOf(askOf(draftOf({ preset: 'keep', adoption: false })), { engine: KEPT_ROW }) } };
export const KeepHalf: Story = { ...Keep, decorators: [storyWidths.half] };
export const KeepNarrow: Story = { ...Keep, decorators: [storyWidths.narrow] };
export const KeepChinese: Story = { ...Keep, decorators: [chinese] };
export const KeepDark: Story = { ...Keep, decorators: [dark] };

/** The suggestion with its why worded by code and the driver's sentence beneath. */
export const SuggestedWhy: Story = { args: plannedFor(draftOf({ repositories: ['site'], preset: 'pull-request', adoption: false })) };
export const SuggestedWhyNarrow: Story = { ...SuggestedWhy, decorators: [storyWidths.narrow] };
export const SuggestedWhyChinese: Story = { ...SuggestedWhy, decorators: [chinese] };

/** A workspace whose repositories suggest different things: nothing is preselected, and each choice names the repositories it suits. */
export const Mixed: Story = {
  args: {
    place: 'aurora', kind: 'workspace',
    choices: [{ repository: 'engine', driven: true }, { repository: 'site', driven: true }],
    ...plannedFor(draftOf({ repositories: ['engine', 'site'], preset: null, adoption: null })),
  },
};
export const MixedHalf: Story = { ...Mixed, decorators: [storyWidths.half] };
export const MixedNarrow: Story = { ...Mixed, decorators: [storyWidths.narrow] };
export const MixedChinese: Story = { ...Mixed, decorators: [chinese] };
export const MixedDark: Story = { ...Mixed, decorators: [dark] };

/** Driving captioned from the plan's own sides, repository by repository. */
export const DrivingCaption: Story = {
  args: {
    place: 'aurora', kind: 'workspace',
    choices: [{ repository: 'engine', driven: true }, { repository: 'site', driven: false }],
    ...plannedFor(draftOf({ repositories: ['engine', 'site'], preset: 'keep', adoption: true, drive: false })),
  },
};
export const DrivingCaptionNarrow: Story = { ...DrivingCaption, decorators: [storyWidths.narrow] };
export const DrivingCaptionChinese: Story = { ...DrivingCaption, decorators: [chinese] };

/** A workspace's whole list of repositories: All, Driven, Not driven or None, a count and a search. */
export const Chooser: Story = {
  args: {
    place: 'aurora', kind: 'workspace',
    choices: [
      { repository: 'engine', driven: true }, { repository: 'site', driven: true },
      ...Array.from({ length: 41 }, (_, at) => ({ repository: `archive-${at + 1}`, driven: false })),
    ],
    ...plannedFor(draftOf({ repositories: ['engine', 'site'], preset: 'branch-to-push' })),
  },
};
export const ChooserHalf: Story = { ...Chooser, decorators: [storyWidths.half] };
export const ChooserNarrow: Story = { ...Chooser, decorators: [storyWidths.narrow] };
export const ChooserChinese: Story = { ...Chooser, decorators: [chinese] };
export const ChooserDark: Story = { ...Chooser, decorators: [dark] };

/** Not driven takes no second opinion, look or driving. */
export const NotDriven: Story = { args: plannedFor(draftOf({ preset: 'not-driven' })) };

/** A workspace's several repositories in one plan, each change said once. */
export const Workspace: Story = {
  args: {
    place: 'aurora', kind: 'workspace',
    choices: [{ repository: 'engine', driven: true }, { repository: 'site', driven: false }, { repository: 'archive', driven: false }],
    ...plannedFor(draftOf({ repositories: ['engine', 'site'], preset: 'branch-to-push' })),
  },
};
export const WorkspaceNarrow: Story = { ...Workspace, decorators: [storyWidths.narrow] };
export const WorkspaceChinese: Story = { ...Workspace, decorators: [chinese] };

/** The driver could not be asked. */
export const Unavailable: Story = { args: { plan: null, problem: 'The driver is still coming up — try again in a moment.' } };
