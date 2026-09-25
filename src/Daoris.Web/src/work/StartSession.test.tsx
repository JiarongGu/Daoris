import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { StartSession } from './StartSession';

/**
 * Starting a session (SURF4d): which accounts are offered follows the harness chosen (REV3 web-work
 * F7). The form was handed the DEFAULT harness's accounts only, so choosing another harness offered
 * accounts that harness does not have — and the spawn then refused a name it had never heard of.
 */
describe('starting a session', () => {
  afterEach(() => cleanup());

  const ACCOUNTS = {
    'claude-code': [{ name: 'personal', login: 'in' as const }],
    codex: [{ name: 'team', login: 'in' as const, account: 'team@example.com' }],
  };

  function show(onStart = vi.fn()) {
    render(
      <Tooltip.Provider>
        <StartSession
          repositories={['engine']}
          harnesses={['claude-code', 'codex']}
          defaultHarness="claude-code"
          accounts={ACCOUNTS}
          onStart={onStart}
        />
      </Tooltip.Provider>,
    );
    return onStart;
  }

  // The platform's own select (UX5 U5): a combobox that opens a listbox, never the OS's control.
  // Opened from the keyboard, as AiJobs' tests do: in jsdom a pointer-opened select opened in the
  // first test of the file and in no test after it.
  const open = async (field: string) => {
    screen.getByRole('combobox', { name: field }).focus();
    await userEvent.keyboard('{Enter}');
    return screen.findAllByRole('option');
  };
  const choose = async (field: string, option: string) => {
    await open(field);
    await userEvent.click(await screen.findByRole('option', { name: option }));
  };
  const accountNames = async () => {
    const options = await open('account');
    const names = options.map((option) => option.textContent);
    await userEvent.keyboard('{Escape}');
    return names;
  };

  it('offers the accounts of the harness chosen, and the default harness\'s when none is', async () => {
    show();
    expect(await accountNames()).toEqual(['whatever this machine already decided', 'personal']);

    await choose('agent tool', 'codex');
    expect(await accountNames()).toEqual(['whatever this machine already decided', 'team@example.com']);
  });

  it('forgets an account chosen for another harness when the harness changes', async () => {
    const onStart = show();
    await choose('account', 'personal');
    await choose('agent tool', 'codex');

    await userEvent.click(screen.getByRole('button', { name: /start/i }));

    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ adapter: 'codex', profile: undefined }));
  });

  /**
   * UX5 U5: three native selects and a native checkbox sat beside `ui.tsx`'s own, so this one form
   * wore the OS's controls in a palette that draws its own (platform language §4). The default
   * choices mean "leave it to the driver", and they stay choosable after another was chosen.
   */
  it('wears the platform\'s own controls, and a default can be chosen back', async () => {
    const onStart = show();
    // Radix keeps a hidden native select for the form, which nobody sees; a VISIBLE one is the defect.
    expect(document.querySelector('select:not([aria-hidden="true"]), input[type="checkbox"]:not([aria-hidden="true"])')).toBeNull();

    await choose('agent tool', 'codex');
    await choose('agent tool', 'this machine\'s default');
    await userEvent.click(screen.getByRole('checkbox', { name: /working tree of its own/ }));
    await userEvent.click(screen.getByRole('button', { name: /start/i }));

    expect(onStart).toHaveBeenCalledWith({
      repository: 'engine', adapter: undefined, profile: undefined, ownTree: true,
    });
  });
});
