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

  const accountNames = () =>
    [...(screen.getByLabelText('account') as HTMLSelectElement).options].map((option) => option.value).filter(Boolean);

  it('offers the accounts of the harness chosen, and the default harness\'s when none is', async () => {
    show();
    expect(accountNames()).toEqual(['personal']);

    await userEvent.selectOptions(screen.getByLabelText('agent tool'), 'codex');
    expect(accountNames()).toEqual(['team']);
  });

  it('forgets an account chosen for another harness when the harness changes', async () => {
    const onStart = show();
    await userEvent.selectOptions(screen.getByLabelText('account'), 'personal');
    await userEvent.selectOptions(screen.getByLabelText('agent tool'), 'codex');

    await userEvent.click(screen.getByRole('button', { name: /start/i }));

    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ adapter: 'codex', profile: undefined }));
  });
});
