import type { Meta, StoryObj } from '@storybook/react-vite';
import { readMachine } from '../help/machine';
import { setupSteps } from '../help/setup';
import { GetStarted } from './GetStarted';

// The setup guide (SETUP1a, D97), from the machines a first start meets: nothing at all, halfway with
// Ask Daoris's agent named, everything required done, and a browser, which knows only the registry.

const IN = { harness: 'claude-code', present: true, product: 'Claude Code', ownLogin: 'in' };

const meta = {
  title: 'Settings/GetStarted',
  component: GetStarted,
  args: {
    steps: setupSteps(readMachine({ attached: true, registry: [], driver: { drivable: [] }, harnesses: [] })),
    helper: null,
    atStart: true,
    onAtStart: () => {},
    onGo: () => {},
    onAsk: () => {},
  },
} satisfies Meta<typeof GetStarted>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Fresh: Story = {};

export const Halfway: Story = {
  args: {
    helper: 'claude-code-acp',
    steps: setupSteps(readMachine({
      attached: true,
      registry: [{ repository: 'engine' }, { repository: 'game' }],
      driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp' },
      harnesses: [IN],
      lines: [{ repository: 'engine', workspace: 'aurora', source: 'none' }],
      landings: [{ repository: 'engine', workspace: 'aurora', source: 'default', form: 'merge' }],
    })),
  },
};

export const Done: Story = {
  args: {
    helper: 'claude-code-acp',
    atStart: false,
    steps: setupSteps(readMachine({
      attached: true,
      registry: [{ repository: 'engine' }],
      driver: { drivable: ['engine'], helperAdapter: 'claude-code-acp' },
      harnesses: [IN],
      lines: [{ repository: 'engine', workspace: 'aurora', branch: 'develop', source: 'workspace' }],
      landings: [{ repository: 'engine', workspace: 'aurora', source: 'workspace', form: 'branch', pattern: 'feature/{quest}-{slug}' }],
    })),
  },
};

export const InABrowser: Story = {
  args: {
    steps: setupSteps(readMachine({ attached: false, registry: [] })),
    atStart: undefined,
    onAtStart: undefined,
    onAsk: undefined,
  },
};
