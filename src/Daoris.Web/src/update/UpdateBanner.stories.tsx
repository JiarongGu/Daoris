import type { Meta, StoryObj } from '@storybook/react-vite';
import { UpdateBanner, type UpdateState } from './UpdateBanner';

// The install's update (UPDATE1, D139), in the shape `DAORIS.UPDATE` · `STATE` answers: each state the banner draws, and
// the outcome said once at the start after a swap.

const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };

const state = (extra: Partial<UpdateState>): UpdateState => ({
  state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null, ...extra,
});

const meta = {
  title: 'Shell/UpdateBanner',
  component: UpdateBanner,
  args: { onSay: () => {}, onDismiss: () => {} },
} satisfies Meta<typeof UpdateBanner>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Draining: Story = { args: { update: state({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 2, turns: 1 }) } };

export const Waiting: Story = { args: { update: state({ state: 'waiting', staged: STAGED, mode: 'not-now' }) } };

export const Applying: Story = { args: { update: state({ state: 'applying', staged: STAGED, mode: 'now' }) } };

export const Refused: Story = {
  args: { update: state({ state: 'refused', staged: STAGED, problem: { code: 'host', message: 'this install carries its HTTP host.' } }) },
};

export const Updated: Story = {
  args: { update: state({ outcome: { phase: 'installed', build: STAGED.id, version: '0.0.1', commit: 'abc1234' } }) },
};

// SWAP2c: the two codes SWAP2 added, said in the banner's words; `detail` is the journal's English, which they replace.
export const RolledBackByAFailedMove: Story = {
  args: {
    update: state({
      outcome: {
        phase: 'rolled-back', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'move',
        detail: 'a move the swap had to make failed (the target already exists); nothing was changed.',
      },
    }),
  },
};

export const RefusedByTheLaunchersError: Story = {
  args: {
    update: state({
      outcome: {
        phase: 'refused', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'error',
        detail: 'the launcher met an error before it could swap.',
      },
    }),
  },
};

export const RolledBackWhileANewerIsStaged: Story = {
  args: {
    update: state({
      state: 'draining', staged: { ...STAGED, commit: 'def5678' }, mode: 'when-idle', driven: 1,
      outcome: { phase: 'rolled-back', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'exited' },
    }),
  },
};
