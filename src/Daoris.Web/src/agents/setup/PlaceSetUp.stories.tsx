import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { APPLIED, MOVED, planOf } from './placeFixtures';
import { askOf, freshDraft, type SetUpDraft } from './placePlan';
import { PlaceSetUp } from './PlaceSetUp';

// A place's Set up… (AGENTSPLACE1j; the Agents-place design §4): delivery, adoption with what it builds, general driving, the plan read from
// the driver and one Apply, at the main area's 80, 39 and 25 rem, in 中文 and in dark.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const draftOf = (patch: Partial<SetUpDraft> = {}): SetUpDraft => ({ ...freshDraft(['engine']), preset: 'merge-after-accept', adoption: true, builds: 'service', ...patch });

const plannedFor = (draft: SetUpDraft) => ({ draft, plan: planOf(askOf(draft)) });

const meta: Meta<typeof PlaceSetUp> = {
  title: 'Agents/PlaceSetUp',
  component: PlaceSetUp,
  args: {
    place: 'engine', kind: 'repository', choices: [], ...plannedFor(draftOf()), reading: false, stale: false, problem: null, applying: false, holding: false,
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

/** The integrated delivery offers the declaration's acceptance, unchosen. */
export const Acceptance: Story = { args: plannedFor(draftOf({ preset: 'integrated', adoption: false })) };
export const AcceptanceNarrow: Story = { ...Acceptance, decorators: [storyWidths.narrow] };
export const AcceptanceChinese: Story = { ...Acceptance, decorators: [chinese] };
export const AcceptanceDark: Story = { ...Acceptance, decorators: [dark] };

/** General driving, with the queued work it releases and the offer to hold it. */
export const Driving: Story = { args: plannedFor(draftOf({ preset: 'branch-to-push', drive: true })) };
export const DrivingNarrow: Story = { ...Driving, decorators: [storyWidths.narrow] };

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

/** The plan moved since it was read, and what a done apply said. */
export const Moved: Story = { args: plannedFor(draftOf({ preset: 'integrated', result: MOVED })) };
export const Applied: Story = { args: plannedFor(draftOf({ result: APPLIED })) };
export const AppliedNarrow: Story = { ...Applied, decorators: [storyWidths.narrow] };

/** The driver could not be asked. */
export const Unavailable: Story = { args: { plan: null, problem: 'The driver is still coming up — try again in a moment.' } };
