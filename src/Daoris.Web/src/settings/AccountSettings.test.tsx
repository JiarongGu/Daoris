import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { code } from '../test/code';
import { AccountSettingsForm, AccountSettingsSummary } from './AccountSettings';
import type { AccountSettings, SettingsChoices } from '../tools';

// AGT6 (D98): an account's own model and effort, as molecules — props in, states out. The values are the
// tool's own file's and the choices the tool's own words; the form sends only what the person changed.

const CHOICES: SettingsChoices = {
  models: ['default', 'sonnet', 'opus', 'haiku', 'opus[1m]'],
  efforts: ['low', 'medium', 'high', 'xhigh'],
};

const settings = (over: Partial<AccountSettings> = {}): AccountSettings => ({
  model: null, effort: null, perModel: [], problem: null, ...over,
});

const form = (props: Partial<Parameters<typeof AccountSettingsForm>[0]> = {}) =>
  render(
    <Tooltip.Provider>
      <AccountSettingsForm
        harness="claude-code"
        account="work"
        accountLabel="someone@example.invalid"
        settings={settings()}
        choices={CHOICES}
        onSave={() => {}}
        onCancel={() => {}}
        {...props}
      />
    </Tooltip.Provider>,
  );

async function choose(name: string, option: string) {
  const user = userEvent.setup();
  screen.getByRole('combobox', { name }).focus();
  await user.keyboard('{Enter}');
  await user.click(await screen.findByRole('option', { name: option }));
}

describe("an account's own settings", () => {
  it('says what the account runs on, in the tool’s words, and the tool’s default where nothing is set', () => {
    const { rerender } = render(<AccountSettingsSummary settings={settings({ model: 'opus', effort: 'high' })} />);
    expect(screen.getByText('model opus · effort high')).toBeInTheDocument();

    rerender(<AccountSettingsSummary settings={settings()} />);
    expect(screen.getByText("the agent's own model and effort")).toBeInTheDocument();

    rerender(<AccountSettingsSummary settings={settings({ model: 'sonnet', perModel: [{ model: 'claude-opus-5', effort: 'xhigh' }] })} />);
    expect(screen.getByText("model sonnet · effort the agent's own default · 1 set per model")).toBeInTheDocument();
  });

  it('says a file it could not read in the driver’s own words', () => {
    render(<AccountSettingsSummary settings={settings({ problem: '`settings.json` could not be read (bad)' })} />);

    expect(screen.getByText('`settings.json` could not be read (bad)')).toBeInTheDocument();
  });

  it('opens on what the file says, and sends nothing when nothing changed', () => {
    form({ settings: settings({ model: 'opus', effort: 'high' }) });

    expect(screen.getByRole('combobox', { name: 'model' })).toHaveTextContent('opus');
    expect(screen.getByRole('combobox', { name: 'effort' })).toHaveTextContent('high');
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
  });

  it('sends the model and the effort the person chose, and only those', async () => {
    const onSave = vi.fn();
    form({ settings: settings({ model: 'opus', effort: 'high' }), onSave });

    await choose('model', 'sonnet');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenLastCalledWith({ model: 'sonnet' });

    await choose('effort', 'xhigh');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenLastCalledWith({ model: 'sonnet', effort: 'xhigh' });
  });

  it('clears a key by choosing the tool’s own default', async () => {
    const onSave = vi.fn();
    form({ settings: settings({ model: 'opus', effort: 'high' }), onSave });

    await choose('effort', "The agent's own default");
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenLastCalledWith({ effort: null });
  });

  it('takes a full model id in a field of its own, and opens on one the aliases do not name', async () => {
    const onSave = vi.fn();
    form({ onSave });

    await choose('model', 'Another model…');
    const field = screen.getByRole('textbox', { name: "the model's full id" });
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    await userEvent.type(field, 'claude-opus-5-5');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onSave).toHaveBeenLastCalledWith({ model: 'claude-opus-5-5' });
  });

  it('shows a model the aliases do not name as the full id it is', () => {
    form({ settings: settings({ model: 'claude-sonnet-5-5' }) });

    expect(screen.getByRole('combobox', { name: 'model' })).toHaveTextContent('Another model…');
    expect(screen.getByRole('textbox', { name: "the model's full id" })).toHaveValue('claude-sonnet-5-5');
  });

  it('offers only the efforts the settings keep, and still shows one the file holds outside them', () => {
    form({ settings: settings({ effort: 'max' }) });

    expect(screen.getByRole('combobox', { name: 'effort' })).toHaveTextContent('max');
  });

  it('sets and clears each model’s own effort by its name', async () => {
    const onSave = vi.fn();
    form({
      settings: settings({ perModel: [{ model: 'claude-opus-5', effort: 'xhigh' }, { model: 'claude-sonnet-5', effort: 'low' }] }),
      onSave,
    });

    const perModel = screen.getByRole('group', { name: 'set per model' });
    expect(within(perModel).getByText('claude-opus-5')).toBeInTheDocument();
    await choose('effort for claude-opus-5', 'medium');
    await choose('effort for claude-sonnet-5', 'Not set');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSave).toHaveBeenLastCalledWith({ perModel: { 'claude-opus-5': 'medium', 'claude-sonnet-5': null } });
  });

  /**
   * D50: whatever the screen sets, a terminal can — and the form says the command, for this account. It is the console's
   * one code span, so a line breaks only between its words and never inside `--account` (LOOK5).
   */
  it('names the terminal command that does the same, for this account', () => {
    form();

    const command = screen.getByText(code('daoris agent settings claude-code --account work model <model> effort <effort>'));
    expect([...command.children].map((word) => word.textContent)).toEqual(
      ['daoris', 'agent', 'settings', 'claude-code', '--account', 'work', 'model', '<model>', 'effort', '<effort>']);
    for (const word of command.children) expect(word).toHaveClass('inline-block', 'max-w-full');
  });

  /**
   * ACCTQUOTE1c (D125's ACCTQUOTE1 note): the command is pasted into whichever shell a person has, so an account a shell
   * would split is in double quotes, and one no spelling holds in every shell (`R&D`) is the placeholder `<account>`.
   */
  it('spells the account in the terminal command for a shell', () => {
    const { unmount } = form({ account: 'my team' });
    expect(screen.getByText(code('daoris agent settings claude-code --account "my team" model <model> effort <effort>'))).toBeInTheDocument();
    unmount();

    form({ account: 'R&D' });
    expect(screen.getByText(code('daoris agent settings claude-code --account <account> model <model> effort <effort>'))).toBeInTheDocument();
  });

  it('is taken back with cancel, and holds nothing while it saves', async () => {
    const onCancel = vi.fn();
    const { rerender } = form({ onCancel });

    await userEvent.click(screen.getByRole('button', { name: 'Never mind' }));
    expect(onCancel).toHaveBeenCalled();

    rerender(
      <Tooltip.Provider>
        <AccountSettingsForm harness="claude-code" account="work" accountLabel="work" settings={settings()} choices={CHOICES}
          busy onSave={() => {}} onCancel={() => {}} />
      </Tooltip.Provider>,
    );
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled();
    // SETTINGSWAIT1: Never mind waits with Save, so a press is not ignored by the page.
    expect(screen.getByRole('button', { name: 'Never mind' })).toBeDisabled();
  });
});
