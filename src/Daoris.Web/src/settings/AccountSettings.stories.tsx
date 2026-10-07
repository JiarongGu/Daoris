import type { Meta, StoryObj } from '@storybook/react-vite';
import type { AccountSettings } from '../tools';
import { AccountSettingsForm, AccountSettingsSummary } from './AccountSettings';

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

const ROWS: [label: string, settings: AccountSettings][] = [
  ['work@example.invalid', { model: 'opus[1m]', effort: 'high', perModel: [], problem: null }],
  ['home@example.invalid', { model: null, effort: null, perModel: [], problem: '`settings.json` could not be read (Unexpected token } in JSON at position 41)' }],
];

/**
 * The line each account's row carries on the agent's page: what is set, and a file the driver could not read, its
 * sentence verbatim in the danger ink a red word wears (UXFIX5b), never the declined fill's colour.
 */
export const TheSummaryLines: Story = {
  render: () => (
    <ul className="m-0 list-none p-0">
      {ROWS.map(([label, settings]) => (
        <li key={label} className="flex flex-wrap items-center gap-x-3 gap-y-1 border-t border-line py-1.5 first:border-t-0">
          <span className="min-w-0 text-body text-ink">{label}</span>
          <AccountSettingsSummary settings={settings} />
        </li>
      ))}
    </ul>
  ),
};
