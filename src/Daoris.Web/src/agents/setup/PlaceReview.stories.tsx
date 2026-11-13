import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { APPLIED, MOVED, planOf } from './placeFixtures';
import { askOf, freshDraft, type PlaceRepositoryPlan, type SetUpDraft } from './placePlan';
import { PlaceReview } from './PlaceReview';

// A place's Set up…, the review: what changes, what stops it, what it starts, your part, the work it releases
// with what holding did per repository, the acceptances, the terminal line and one Apply, at the main area's 80, 39 and 25 rem, in 中文 and in
// dark. Two columns at 80 rem and one, in the same order, below it. The measured-box check (`measured/review.spec.ts`) reads where Apply sits.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const draftOf = (patch: Partial<SetUpDraft> = {}): SetUpDraft => ({
  ...freshDraft(['engine', 'archive']), preset: 'integrated', adoption: true, builds: 'service', drive: true, review: true, ...patch,
});

/** What the driver says of reach and of a line it could not read, beside the changes. */
const MIX: Record<string, Partial<PlaceRepositoryPlan>> = {
  engine: { reach: [{ repository: 'engine', cannots: [], noGates: false, said: 'A change here reaches `api`, which has no gates of its own.' }] },
  archive: { unread: 'its remote did not answer.' },
};

const plannedFor = (draft: SetUpDraft, mix: Record<string, Partial<PlaceRepositoryPlan>> = MIX) => ({ draft, plan: planOf(askOf(draft), mix) });

const meta: Meta<typeof PlaceReview> = {
  title: 'Agents/PlaceReview',
  component: PlaceReview,
  args: {
    place: 'aurora', kind: 'workspace', ...plannedFor(draftOf()),
    reading: false, stale: false, problem: null, applying: false, holding: false, holds: {},
    onBack: nothing, onLeaveOut: nothing, onAccept: nothing, onApply: nothing, onHold: nothing, onOpenQuest: nothing, onClose: nothing,
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof PlaceReview>;

export const Review: Story = {};
export const ReviewHalf: Story = { decorators: [storyWidths.half] };
export const ReviewNarrow: Story = { decorators: [storyWidths.narrow] };

export const ReviewChinese: Story = { decorators: [chinese] };
export const ReviewChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ReviewChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const ReviewDark: Story = { decorators: [dark] };
export const ReviewDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ReviewDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** A repository's own review: no repositories named beside each change. */
export const Repository: Story = { args: { place: 'engine', kind: 'repository', ...plannedFor(draftOf({ repositories: ['engine'] })) } };
export const RepositoryHalf: Story = { ...Repository, decorators: [storyWidths.half] };

/** What stops the plan, whole, with a link back to its part and a change of selection; no Apply. */
export const Stopped: Story = { args: plannedFor(draftOf({ repositories: ['engine', 'site'], preset: 'pull-request', opinion: true }), {}) };
export const StoppedHalf: Story = { ...Stopped, decorators: [storyWidths.half] };
export const StoppedNarrow: Story = { ...Stopped, decorators: [storyWidths.narrow] };
export const StoppedChinese: Story = { ...Stopped, decorators: [chinese] };
export const StoppedDark: Story = { ...Stopped, decorators: [dark] };

/** The last plan stays, marked updating, while the next is read; Apply waits. */
export const Updating: Story = { args: { reading: true, stale: true } };
export const UpdatingNarrow: Story = { ...Updating, decorators: [storyWidths.narrow] };
export const UpdatingChinese: Story = { ...Updating, decorators: [chinese] };

/** One repository held, one the driver refused, each said beside the work it released. */
export const PartialHold: Story = {
  args: {
    ...plannedFor(draftOf({ repositories: ['engine', 'site'], preset: 'branch-to-push', adoption: false }), { site: { released: [{ quest: 'q-20', title: 'Fix the login' }] } }),
    holds: { engine: { held: true }, site: { held: false, said: 'the driver refused the hold: site is already held by the steward.' } },
  },
};
export const PartialHoldNarrow: Story = { ...PartialHold, decorators: [storyWidths.narrow] };
export const PartialHoldChinese: Story = { ...PartialHold, decorators: [chinese] };

/** A tick the page clears because the declaration changes after it is ticked. */
export const ClearedTick: Story = {
  args: plannedFor(draftOf({
    repositories: ['engine'],
    drive: false,
    cleared: [{ repository: 'engine', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }],
  })),
};
export const ClearedTickNarrow: Story = { ...ClearedTick, decorators: [storyWidths.narrow] };

/** An acceptance ticked at the blob read. */
export const Ticked: Story = {
  args: plannedFor(draftOf({
    repositories: ['engine'],
    drive: false,
    accept: [{ repository: 'engine', line: 'main', blob: '3b18e512dba79e4c8300dd08aeb37f8e728b8dad' }],
  })),
};

/** The plan moves after it is read: the driver's sentence, and nothing written. */
export const Moved: Story = { args: plannedFor(draftOf({ result: MOVED })) };
export const MovedNarrow: Story = { ...Moved, decorators: [storyWidths.narrow] };

/** Flow 1's end: configuration applied, adoption pending, each repository where it stands, and the door to the adoption quest. */
export const Applied: Story = {
  args: {
    place: 'archive',
    kind: 'repository',
    ...plannedFor(draftOf({
      repositories: ['archive'],
      preset: 'not-driven',
      drive: false,
      result: { ...APPLIED, operations: [{ id: 'quest:archive', kind: 'quest', repository: 'archive', said: 'the set-up quest asked of `archive`', outcome: 'done', quest: 'q-77' }] },
      ended: [{ state: 'adoptionOnly', repositories: ['archive'] }],
    }), {}),
  },
};
export const AppliedHalf: Story = { ...Applied, decorators: [storyWidths.half] };
export const AppliedNarrow: Story = { ...Applied, decorators: [storyWidths.narrow] };
export const AppliedChinese: Story = { ...Applied, decorators: [chinese] };
export const AppliedDark: Story = { ...Applied, decorators: [dark] };

/** Keyboard: focus rests on Apply, whose ring shows. */
export const ApplyFocused: Story = {
  args: plannedFor(draftOf({ repositories: ['engine'], drive: false })),
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLButtonElement>('[data-review-apply]')?.focus();
  },
};
export const ApplyFocusedNarrow: Story = { ...ApplyFocused, decorators: [storyWidths.narrow] };

/** Keyboard: focus rests on the first acceptance's tick. */
export const TickFocused: Story = {
  args: plannedFor(draftOf({ repositories: ['engine'], drive: false })),
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLElement>('[data-acceptance] [role="checkbox"]')?.focus();
  },
};

/** The driver could not be asked. */
export const Unavailable: Story = { args: { plan: null, problem: 'The driver is still coming up — try again in a moment.' } };
