import type { Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { UpdateSection, type UpdateStanding } from './Update';

// The install's update in Settings (UPDATE1b, D139 §3, §6), in the shape `DAORIS.UPDATE` · `STATE` answers: nothing
// staged, the drain and what it waits on, waiting after Not now, installing, a refusal, and how the last swap ended,
// light and dark.

const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };

const standing = (extra: Partial<UpdateStanding>): UpdateStanding => ({
  state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, ...extra,
});

const DRAINING = standing({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 2, turns: 1 });
const INSTALLED = standing({ outcome: { phase: 'installed', build: STAGED.id, version: '0.0.1', commit: 'abc1234' } });

const meta = {
  title: 'Settings/Update',
  component: UpdateSection,
  args: { onSay: () => {} },
  // A width between the side bar's two states, as the Driver domain's main area has.
  decorators: [(Story) => <div className="w-[46rem] max-w-full"><Story /></div>],
} satisfies Meta<typeof UpdateSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/** A workspace build, or an install with nothing staged beside it. */
export const NothingStaged: Story = { args: { update: standing({}) } };

/** Staged and installed when idle: nothing new starts, and what still runs is counted. */
export const Draining: Story = { args: { update: DRAINING }, decorators: [(Story) => <InTheme theme="light"><Story /></InTheme>] };

/** The same, in dark. */
export const DrainingDark: Story = { args: { update: DRAINING }, decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** Draining with nothing left running: it installs at the next look. */
export const DrainingIdle: Story = { args: { update: standing({ state: 'draining', staged: STAGED, mode: 'when-idle' }) } };

/** The person said Not now: work goes on and the build waits for their word. */
export const WaitingAfterNotNow: Story = { args: { update: standing({ state: 'waiting', staged: STAGED, mode: 'not-now' }) } };

/** A word on its way: the words held until the shell answers. */
export const Saying: Story = { args: { update: DRAINING, busy: true } };

/** Checked, the launcher started, and Daoris closing for it. */
export const Applying: Story = { args: { update: standing({ state: 'applying', staged: STAGED, mode: 'now' }) } };

/** The application's check refused it: nothing replaced, and it is not tried again. */
export const Refused: Story = {
  args: {
    update: standing({ state: 'refused', staged: STAGED, problem: { code: 'host', message: 'this install carries its HTTP host.' } }),
  },
};

/** The start after a swap that installed. */
export const Installed: Story = { args: { update: INSTALLED }, decorators: [(Story) => <InTheme theme="light"><Story /></InTheme>] };

/** The same, in dark. */
export const InstalledDark: Story = { args: { update: INSTALLED }, decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>] };

/** A swap rolled back with its reason, while a newer build drains. */
export const RolledBackWhileANewerDrains: Story = {
  args: {
    update: standing({
      state: 'draining', staged: { ...STAGED, id: '20261004T090000Z-cd34ef56', commit: 'def5678', at: '2026-10-04T09:00:00Z' },
      mode: 'when-idle', driven: 1,
      outcome: { phase: 'rolled-back', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'exited' },
    }),
  },
  decorators: [(Story) => <InTheme theme="dark"><Story /></InTheme>],
};

/** The launcher's check refused the build before anything moved. */
export const RefusedAtSwap: Story = {
  args: { update: standing({ outcome: { phase: 'refused', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'missing' } }) },
};

/** After the banner's Dismiss, once `STATE` keeps the last swap as `last`. */
export const AfterDismissal: Story = {
  args: { update: standing({ last: { phase: 'installed', build: STAGED.id, version: '0.0.1', commit: 'abc1234' } }) },
};

/** A narrow main area: the row stacks its words under its label. */
export const Narrow: Story = {
  args: { update: DRAINING },
  decorators: [(Story) => <div className="w-[22rem] max-w-full"><Story /></div>],
};
