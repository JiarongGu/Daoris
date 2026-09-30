import type { Meta, StoryObj } from '@storybook/react-vite';
import { SessionOptions } from './SessionOptions';

// One conversation's model and effort (AGT6b, D98), in the shape the driver answers SESSION_OPTIONS
// with: the agent's own names and values, and nothing of Daoris's.

const MODEL = {
  id: 'model', name: 'Model', category: 'model', current: 'default',
  choices: [
    { value: 'default', name: 'Default (recommended)' },
    { value: 'sonnet', name: 'Sonnet' },
    { value: 'haiku', name: 'Haiku' },
  ],
};

const EFFORT = {
  id: 'effort', name: 'Effort', category: 'thought_level', current: 'high',
  choices: [{ value: 'low', name: 'Low' }, { value: 'medium', name: 'Medium' }, { value: 'high', name: 'High' }, { value: 'max', name: 'Max' }],
};

const meta: Meta<typeof SessionOptions> = {
  title: 'Work/SessionOptions',
  component: SessionOptions,
  args: { options: [MODEL, EFFORT], onChange: () => {} },
  decorators: [(Story) => <div className="flex p-4"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof SessionOptions>;

/** An agent that offers both, as Claude Code's protocol door does. */
export const ModelAndEffort: Story = {};

/** A model that takes no effort: the agent offers the model alone. */
export const ModelAlone: Story = { args: { options: [{ ...MODEL, current: 'haiku' }] } };

/** A change on its way to the agent: nothing more is chosen until it answers. */
export const Changing: Story = { args: { busy: true } };
