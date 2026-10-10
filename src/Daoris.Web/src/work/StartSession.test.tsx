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
    const options = await open('Account');
    const names = options.map((option) => option.textContent);
    await userEvent.keyboard('{Escape}');
    return names;
  };

  it('offers the accounts of the harness chosen, and the default harness\'s when none is', async () => {
    show();
    expect(await accountNames()).toEqual(['As already set', 'personal']);

    await choose('Agent', 'codex');
    // The account's name leads, and who signed in sits beside it where the two differ (D152 §4.2).
    expect(await accountNames()).toEqual(['As already set', 'team · team@example.com']);
  });

  it('forgets an account chosen for another harness when the harness changes', async () => {
    const onStart = show();
    await choose('Account', 'personal');
    await choose('Agent', 'codex');

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

    await choose('Agent', 'codex');
    await choose('Agent', 'This machine\'s default: claude-code');
    await userEvent.click(screen.getByRole('checkbox', { name: /tree of its own/ }));
    await userEvent.click(screen.getByRole('button', { name: /start/i }));

    expect(onStart).toHaveBeenCalledWith({
      repository: 'engine', adapter: undefined, profile: undefined, ownTree: true,
    });
  });

  /**
   * UX5 U67, seen on the window: the choice listed `claude-code` and `claude-code-acp`, where Settings
   * names the same ways in by the tool and the door, and *this machine's default* said nothing of
   * what it is. The names are the page's to give; the value is still the id the driver takes.
   */
  it('names each way in as Settings does, and says what this machine’s default is', async () => {
    const onStart = vi.fn();
    render(
      <Tooltip.Provider>
        <StartSession
          repositories={['engine']}
          harnesses={['claude-code', 'claude-code-acp']}
          defaultHarness="claude-code"
          labels={{
            'claude-code': 'Claude Code — direct (claude-code)',
            'claude-code-acp': 'Claude Code — protocol (claude-code-acp)',
          }}
          accounts={{}}
          onStart={onStart}
        />
      </Tooltip.Provider>,
    );

    expect((await open('Agent')).map((option) => option.textContent)).toEqual([
      "This machine's default: Claude Code — direct (claude-code)",
      'Claude Code — direct (claude-code)',
      'Claude Code — protocol (claude-code-acp)',
    ]);
    await userEvent.click(screen.getByRole('option', { name: 'Claude Code — protocol (claude-code-acp)' }));
    await userEvent.click(screen.getByRole('button', { name: /start/i }));
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ adapter: 'claude-code-acp' }));
  });

  /**
   * UX5 U68, seen on the window: the form opened on a repository a parked conversation held, said
   * nothing of it, and the start was refused. It opens on one nothing works in, marks a busy one,
   * and says what lets a busy one start after all: a working tree of its own.
   */
  it('opens where nothing is working, marks a busy repository, and says a tree of its own starts beside it', async () => {
    render(
      <Tooltip.Provider>
        <StartSession repositories={['engine', 'game']} busy={['engine']} harnesses={['claude-code']} accounts={{}} onStart={vi.fn()} />
      </Tooltip.Provider>,
    );

    expect(screen.getByRole('combobox', { name: 'Repository' })).toHaveTextContent('game');
    expect(screen.queryByText(/has an active session/)).toBeNull();

    await choose('Repository', 'engine · busy');
    expect(screen.getByText(/engine has an active session in its checkout/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('checkbox', { name: /tree of its own/ }));
    expect(screen.queryByText(/has an active session/)).toBeNull();
  });

  /** U68: a refused start is said in the form it answers, whole. It was a corner toast, cut mid-sentence. */
  it('says a refused start in the form, whole', () => {
    const refusal = '`engine` already has an active session — `669b2930` (awaiting-person, a chat). '
      + 'One session per working tree: two agents in one tree corrupt each other’s git state.';
    render(
      <Tooltip.Provider>
        <StartSession repositories={['engine']} harnesses={['claude-code']} accounts={{}} refusal={refusal} onStart={vi.fn()} />
      </Tooltip.Provider>,
    );

    expect(screen.getByRole('alert')).toHaveTextContent(
      'engine already has an active session — 669b2930 (awaiting-person, a chat). One session per working tree');
    // Its code marks drawn as code, as the toast drew them, never as backticks.
    expect(screen.getByText('669b2930').tagName).toBe('CODE');
  });

  /**
   * D130 §3.2 (TOOL4m's rest, built with UX6e): the picker lists the accounts the repository's workspace's list holds first,
   * in the list's order, and every other account under *not in its list*, so a pick outside it is never made by accident.
   */
  it('lists the workspace’s accounts first, and every other under not in its list', async () => {
    render(
      <Tooltip.Provider>
        <StartSession
          repositories={['engine']}
          harnesses={['claude-code']}
          defaultHarness="claude-code"
          accounts={{
            'claude-code': [
              { name: 'account-1', login: 'in', account: 'you@work.example' },
              { name: 'account-2', login: 'in', account: 'you@home.example' },
              { name: 'account-3', login: 'out', account: 'spare@example.invalid' },
            ],
          }}
          scopeOf={(repository, harness) => (repository === 'engine' && harness === 'claude-code'
            ? { workspace: 'work', list: ['account-2', 'account-1'] }
            : null)}
          onStart={vi.fn()}
        />
      </Tooltip.Provider>,
    );

    const names = (await open('Account')).map((option) => option.textContent);
    expect(names).toEqual([
      'As already set', 'account-2 · you@home.example', 'account-1 · you@work.example', 'account-3 · spare@example.invalid (signed out)',
    ]);
    expect(screen.getByRole('group', { name: "Not in work's list" })).toHaveTextContent('spare@example.invalid');
  });

  /**
   * ACCTNAME1 (D152 §4.2, D125's ACCT2 note): the picker names each account as its row on the agent's page leads with it: the
   * person's name, a key's handle, who signed in for a fresh id nobody named, else the id; and who signed in beside it where
   * the two differ. The value is still the id, which the spawn takes.
   */
  it('names each account by the person’s name, who signed in beside it, and starts on its id', async () => {
    const onStart = vi.fn();
    render(
      <Tooltip.Provider>
        <StartSession
          repositories={['engine']}
          harnesses={['claude-code']}
          defaultHarness="claude-code"
          accounts={{
            'claude-code': [
              { name: 'acct-3f9c1a2b', displayName: 'work', login: 'in', account: 'you@work.example' },
              { name: 'acct-77aa00ff', displayName: null, login: 'out', account: 'spare@example.invalid' },
              { name: 'acct-0badc0de', login: 'in', key: 'sk-…a1b2' },
              // ACCTUX1b: a key read signed out is a key refused, never an account to sign in.
              { name: 'acct-5e5e5e5e', login: 'out', key: 'sk-…c3d4' },
            ],
          }}
          onStart={onStart}
        />
      </Tooltip.Provider>,
    );

    expect((await open('Account')).map((option) => option.textContent)).toEqual([
      'As already set', 'work · you@work.example', 'spare@example.invalid (signed out)', 'API key sk-…a1b2',
      'API key sk-…c3d4 (key refused)',
    ]);
    await userEvent.click(screen.getByRole('option', { name: 'work · you@work.example' }));
    await userEvent.click(screen.getByRole('button', { name: /start/i }));
    expect(onStart).toHaveBeenCalledWith(expect.objectContaining({ profile: 'acct-3f9c1a2b' }));
    expect(screen.queryByText(/acct-3f9c1a2b/)).toBeNull();
  });
});
