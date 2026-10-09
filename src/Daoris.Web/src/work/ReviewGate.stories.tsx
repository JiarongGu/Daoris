import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { ReactNode } from 'react';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { ReviewGate, type ReviewGateActs } from './ReviewGate';
import {
  STEP_DEPLOYED, STEP_ELSEWHERE, STEP_NOT_YET, STEP_REVIEWED, STEP_SETTING_UP, STEP_SHOWN, STEP_SHOWN_AGAIN,
} from './reviewFixtures';

// *Review in `<environment>`* (REVIEWENV1g; the review environment design §3.1–§3.3, §3.6), where *Accept…* would be while the
// gate holds: not shown yet with the intake's proposal, being set up, shown and still served, shown and no longer served,
// shown again after a not yet, not yet, work added since, unread with the driver's sentence, a deployed environment's link, a
// teammate's local set-up, and a step's own page listing every set-up. At the main area's width beside a 1546 px window and at
// 680 px, in both languages and both themes.

const nothing = () => {};
const ACTS: ReviewGateActs = { reviewed: nothing, notYet: nothing, skip: nothing, setUp: nothing, showAgain: nothing, open: nothing };

function Main({ width, children }: { width: number; children: ReactNode }) {
  return <div className="@container/main max-w-full bg-page p-4" style={{ width }}>{children}</div>;
}

const wide: Decorator = (Story) => <Main width={760}><Story /></Main>;
const narrow: Decorator = (Story) => <Main width={400}><Story /></Main>;
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof ReviewGate> = {
  title: 'Work/Review gate',
  component: ReviewGate,
  args: { environment: 'local', state: 'shown', step: STEP_SHOWN, served: true, acts: ACTS },
  decorators: [wide],
};
export default meta;

type Story = StoryObj<typeof ReviewGate>;

/** No set-up step shows it: *Set it up in `local`* and *Skip…*, with the intake's proposal beside them. */
export const NotShown: Story = {
  args: { state: 'not-shown', step: null, proposal: { choice: 'off', reason: 'Only the README changes, which has nothing to show.' } },
};

/** The same in 中文. */
export const NotShownChinese: Story = { ...NotShown, decorators: [chinese] };

/** The step's session works: only the skip, and the door to the step. */
export const BeingSetUp: Story = { args: { state: 'being-set-up', step: STEP_SETTING_UP } };

/** Shown, its tab still served: *Reviewed*, *Not yet…*, *Show it again*, *Skip…*. */
export const Shown: Story = {};

/** The same in 中文. */
export const ShownChinese: Story = { decorators: [chinese] };

/** The same in dark. */
export const ShownDark: Story = { decorators: [dark] };

/** The same in dark and 中文. */
export const ShownDarkChinese: Story = { decorators: [chinese, dark] };

/** Shown, and the person closed its tab: no longer served, and a reload there loads their own server. */
export const ShownUnserved: Story = { args: { served: false } };

/** At the main area's 400 px floor. */
export const ShownNarrow: Story = { decorators: [narrow] };

/** At the floor in 中文. */
export const ShownNarrowChinese: Story = { decorators: [chinese, narrow] };

/** *Not yet…* asking for the person's words. */
export const NotYetAsking: Story = {
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll('button')].find((button) => button.textContent?.trim() === 'Not yet…')?.click();
  },
};

/** Said not yet: its session puts it right and shows it again. */
export const NotYet: Story = { args: { state: 'not-yet', step: STEP_NOT_YET } };

/** The same in 中文. */
export const NotYetChinese: Story = { ...NotYet, decorators: [chinese] };

/** Shown again after the not yet: the newest set-up waits, the first is listed as shown before on the step's page. */
export const ShownAgain: Story = { args: { step: STEP_SHOWN_AGAIN, whole: true } };

/** Reviewed, and work added since: what was reviewed does not hold it. */
export const NotHeld: Story = { args: { state: 'not-held', step: STEP_REVIEWED } };

/** The same in 中文. */
export const NotHeldChinese: Story = { ...NotHeld, decorators: [chinese] };

/** Whether it waits could not be read: the driver's sentence beneath, and no press. */
export const Unread: Story = { args: { state: 'unread', step: null, said: 'the service did not answer (connection refused)' } };

/** A deployed environment: its address opens, and nothing is served to show again. */
export const Deployed: Story = { args: { environment: 'dev', step: STEP_DEPLOYED, served: null } };

/** A teammate's local set-up: shown on their machine, and no address here. */
export const Elsewhere: Story = { args: { step: STEP_ELSEWHERE, served: null } };

/** A set-up step's own page once reviewed: every set-up, newest first, each with its verdict. */
export const ReviewedWhole: Story = { args: { state: 'reviewed', step: STEP_REVIEWED, whole: true, acts: {} } };

/** The same in dark. */
export const ReviewedWholeDark: Story = { ...ReviewedWhole, decorators: [dark] };
