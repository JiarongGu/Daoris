import type { Meta, StoryObj } from '@storybook/react-vite';
import { AccountSettingsForm } from './AccountSettings';

// An account's own model and effort (AGT6, D98), in the shapes the driver's roster answers with: the
// account's file as read, and the choices the tool itself offers.

const CHOICES = {
  models: ['default', 'sonnet', 'opus', 'haiku', 'fable', 'best', 'sonnet[1m]', 'opus[1m]', 'fable[1m]', 'opusplan'],
  efforts: ['low', 'medium', 'high', 'xhigh'],
};

const meta: Meta<typeof AccountSettingsForm> = {
  title: 'Settings/AccountSettings',
  component: AccountSettingsForm,
  args: {
    harness: 'claude-code',
    account: 'account-1',
    accountLabel: 'someone@example.invalid',
    settings: { model: null, effort: null, perModel: [], problem: null },
    choices: CHOICES,
    onSave: () => {},
    onCancel: () => {},
  },
  decorators: [(Story) => <div className="max-w-4xl"><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof AccountSettingsForm>;

/** A fresh account: no settings file, so the tool's own model and effort. */
export const TheToolsOwn: Story = {};

/** An alias and an effort, and one model with an effort of its own that the tool reads first. */
export const SetWithOnePerModel: Story = {
  args: {
    settings: { model: 'opus[1m]', effort: 'high', perModel: [{ model: 'claude-opus-5', effort: 'xhigh' }], problem: null },
  },
};

/** A full model id the aliases do not name, shown in its own field. */
export const AFullModelId: Story = {
  args: { settings: { model: 'claude-sonnet-5-5', effort: 'medium', perModel: [], problem: null } },
};
