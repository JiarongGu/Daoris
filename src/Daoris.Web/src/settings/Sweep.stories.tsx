import type { Meta, StoryObj } from '@storybook/react-vite';
import { SweepList, type SweepBranch } from './Sweep';

// Session branches (WSR3, D88), in the shape the driver's SWEEP_PLAN answer takes: every kind a
// branch can be, and a machine with none.

const branch = (extra: Partial<SweepBranch> & Pick<SweepBranch, 'branch' | 'kind'>): SweepBranch => ({
  repository: 'engine', workspace: 'aurora', hasTree: true, commits: 0, removable: false, ...extra,
});

const meta = {
  title: 'Settings/Sweep',
  component: SweepList,
  args: { onLook: () => {}, onClean: () => {} },
} satisfies Meta<typeof SweepList>;

export default meta;
type Story = StoryObj<typeof meta>;

export const EveryKind: Story = {
  args: {
    branches: [
      branch({ branch: 'daoris/s-1f2e3d4c', kind: 'empty', where: 'main', removable: true }),
      branch({ branch: 'daoris/s-5a6b7c8d', kind: 'landed', where: 'feature/0fda18-fix-the-api-gap', removable: true }),
      branch({ branch: 'daoris/s-9e0f1a2b', kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work\ne4f5a6b more work' }),
      branch({ branch: 'daoris/s-3c4d5e6f', kind: 'dirty', detail: '3 path(s) uncommitted' }),
      branch({ repository: 'game', branch: 'daoris/s-7a8b9c0d', kind: 'in-use' }),
    ],
  },
};

export const Cleaning: Story = { args: { ...EveryKind.args, busy: true } };

export const Asking: Story = { args: { branches: undefined } };

export const NoneHere: Story = { args: { branches: [] } };
