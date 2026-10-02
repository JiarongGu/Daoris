import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import { USE_DEFAULTS } from './accounts';
import {
  CHOICES, MACHINE_NEAR, OWN_COOLING, scopeOf, SILENT, SIX, SIX_AGENT, SIX_CHOICES, THREE,
} from './accountsFixtures';
import { AccountFactsLines, OwnSignInLine, type ScopeActs, ScopeEditor, TermsLine, WorkspaceScope } from './AccountUse';

// How an agent's accounts are used, as molecules (TOOL4g; D125 §2.4, §3.7, §6; D130 §3.2, §9, §16.6): props in, presses out,
// each the terminal's door's twin. With three accounts and with six: nothing counts accounts.

const acts = (): ScopeActs & { calls: unknown[][] } => {
  const calls: unknown[][] = [];
  return {
    calls,
    onOrder: (workspace, list) => calls.push(['order', workspace, list]),
    onUse: (workspace, change) => calls.push(['use', workspace, change]),
    onInherit: (workspace) => calls.push(['inherit', workspace]),
  };
};

const wrap = (node: React.ReactElement) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

describe('an account\'s facts', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  /** D125 §2.4: its cool-off, how long and why, with *Try now*; the sessions running on it; and its learned week. */
  it('says a cool-off with Try now, the sessions running on it, and its learned week', async () => {
    const onTryNow = vi.fn();
    wrap(<AccountFactsLines facts={THREE.accounts[0]!} label="work@example.invalid" onTryNow={onTryNow} />);

    expect(screen.getByText('cooling')).toBeTruthy();
    expect(screen.getByText(/^Cooling until .+ · in 3h \d+m · the agent said so$/)).toBeTruthy();
    expect(screen.getByText(/0 of Daoris's sessions running · nothing said yet · its week resets /)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'try work@example.invalid now' }));
    expect(onTryNow).toHaveBeenCalledOnce();
  });

  /** D130 §5.2: what its agent last said, each window's use and reset, and how long ago; no cool-off, no Try now. */
  it('says what its agent last said, and offers no Try now where nothing cools', () => {
    wrap(<AccountFactsLines facts={THREE.accounts[1]!} label="home@example.invalid" onTryNow={() => {}} />);

    expect(screen.getByText(/2 of Daoris's sessions running · said 3h ago: 88% of its five-hour limit used, resets .+; 14% of its weekly limit used/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /now$/ })).toBeNull();
  });

  /** Absent is never zero (D57): a count Daoris could not read is said not at all. */
  it('says no count where none was read', () => {
    wrap(<AccountFactsLines facts={{ name: 'account-9' }} label="account-9" onTryNow={() => {}} />);

    expect(screen.getByText('nothing said yet')).toBeTruthy();
    expect(screen.queryByText(/sessions running/)).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    wrap(<AccountFactsLines facts={THREE.accounts[1]!} label="home@example.invalid" onTryNow={() => {}} />);

    expect(screen.getByText(/Daoris 的 2 个会话正在运行 · .+报告：5 小时上限已用 88%/)).toBeTruthy();
  });
});

describe('the tool\'s own sign-in, and the terms', () => {
  /** D125 §3.7: the line while starts run on it, with who and its cool-off, and the press that gives Daoris an account. */
  it('says who it is and that it cools, with Sign in to another account', async () => {
    const onSignIn = vi.fn();
    wrap(<OwnSignInLine who="someone@example.invalid" cooling={OWN_COOLING} signsIn onSignIn={onSignIn} />);

    expect(screen.getByText(/^Sessions run on your own sign-in, someone@example\.invalid: .+ It is cooling until .+look again\.$/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Sign in to another account' }));
    expect(onSignIn).toHaveBeenCalledOnce();
  });

  it('offers no sign-in a tool has none of', () => {
    wrap(<OwnSignInLine signsIn={false} onSignIn={() => {}} />);

    expect(screen.getByText(/^Sessions run on your own sign-in: /)).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says each account\'s own plan and terms apply', () => {
    wrap(<TermsLine />);

    expect(screen.getByText(/^Each account's own plan and terms apply\./)).toBeTruthy();
  });
});

describe('a scope\'s list', () => {
  /** D130 §9: *Use* on each account's row; on appends last, off takes it out, as the terminal names a list. */
  it('turns Use on last and off where it was, and moves a used account up and down', async () => {
    const pressed = acts();
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1' })} accounts={CHOICES} acts={pressed} />);

    const list = screen.getByRole('list', { name: 'This machine: accounts in the order they are used' });
    expect(within(list).getAllByRole('listitem').map((row) => row.textContent)).toEqual([
      expect.stringContaining('work@example.invalid'), expect.stringContaining('home@example.invalid'),
      expect.stringContaining('spare@example.invalid'),
    ]);
    await userEvent.click(screen.getByRole('checkbox', { name: 'use spare@example.invalid for This machine' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'use home@example.invalid for This machine' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move up · home@example.invalid' }));

    expect(pressed.calls).toEqual([
      ['order', null, ['account-1', 'account-2', 'account-3']],
      ['order', null, ['account-1']],
      ['order', null, ['account-2', 'account-1']],
    ]);
    expect(screen.getByRole('button', { name: 'Move up · work@example.invalid' }).hasAttribute('disabled')).toBe(true);
    expect(screen.getByRole('button', { name: 'Move down · home@example.invalid' }).hasAttribute('disabled')).toBe(true);
    // A signed-out account says so where it would be passed.
    expect(within(list).getAllByRole('listitem')[2]!.textContent).toContain('not signed in');
  });

  /** What the terminal refuses, the screen does not offer: the default's Use off is disabled, and its tip says why. */
  it('does not offer Use off on the scope\'s default or its kept account', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByRole('checkbox', { name: 'use work@example.invalid for This machine' }).hasAttribute('disabled')).toBe(true);
    expect(screen.getByRole('checkbox', { name: 'use spare@example.invalid for This machine' }).hasAttribute('disabled')).toBe(true);
    expect(screen.getByRole('checkbox', { name: 'use home@example.invalid for This machine' }).hasAttribute('disabled')).toBe(false);
  });

  /** Where starts begin, the kept account, one near by the scope's own near, and one cooling, each said on its row. */
  it('marks where starts begin, the kept account, one near its limit and one cooling', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={acts()} />);

    const rows = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(rows[0]!.textContent).toContain('starts here');
    expect(rows[0]!.textContent).toContain('cooling');
    expect(rows[1]!.textContent).toContain('near its limit');
    expect(rows[2]!.textContent).toContain('kept for conversations');
  });

  /** A scope with no list has no settings: it says where its starts run, and the first Use gives it a list. */
  it('says where starts run while the scope has no list, and offers no settings', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf()} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText(/^No account is used here yet, so starts run on the tool's own sign-in\./)).toBeTruthy();
    expect(screen.queryByRole('radiogroup')).toBeNull();
    expect(screen.queryByText('Keep for conversations')).toBeNull();
  });

  /** A hand-edited file that breaks the rule is read with the list winning, and said (D130 §3.1). */
  it('says a default outside its list', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ default: 'account-3', list: ['account-1'], begins: 'account-1', problem: { kind: 'default', account: 'account-3' } })} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText(/^This scope's default, spare@example\.invalid, is not in its list/)).toBeTruthy();
  });
});

describe('how a scope\'s list is used', () => {
  /** D130 §16.6: *Use accounts*, *Keep for conversations*, *Switch before the limit* and its *near*, each one setting. */
  it('changes one setting at a time, as the terminal\'s profile use does', async () => {
    const pressed = acts();
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={pressed} />);

    await userEvent.click(screen.getByRole('radio', { name: 'One by one, in order' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'Switch before the limit · This machine' }));
    const near = screen.getByRole('spinbutton', { name: 'Near at · This machine' });
    await userEvent.clear(near);
    await userEvent.type(near, '40');
    await userEvent.tab();
    await userEvent.clear(near);
    await userEvent.type(near, '80');
    await userEvent.tab();

    expect(pressed.calls).toEqual([
      ['use', null, { use: 'order' }],
      ['use', null, { early: false }],
      ['use', null, { near: 80 }],
    ]);
    expect(screen.getByText(/^Each start goes to the account running the fewest/)).toBeTruthy();
  });

  /** D130 §16.4: with nothing said by any account of the list, the screen says what the walk does instead. */
  it('says nothing has been said yet, with six accounts', () => {
    wrap(<ScopeEditor agent={SIX_AGENT} product="Claude Code" scope={SIX_AGENT.scopes[0]!} accounts={SIX_CHOICES} acts={acts()} />);

    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(SIX.length);
    expect(screen.getByText(/^No account has said what it has left yet\./)).toBeTruthy();
  });

  /** The order's cost is said where it is chosen (D130 §16.6). */
  it('says what one by one costs', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={THREE.scopes[1]!} accounts={CHOICES} workspace="work" acts={acts()} />);

    expect(screen.getByText(/one limit stops every session on that account\.$/)).toBeTruthy();
  });

  /** A list of one keeps nothing: keeping its only account would leave driven work none. */
  it('offers no kept account with one account in the list', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1'], begins: 'account-1' })} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText(/^Keeping an account for conversations needs another account/)).toBeTruthy();
    expect(screen.queryByRole('combobox')).toBeNull();
  });

  /** D130 §6: an agent whose sessions say nothing of their limits keeps the switch, and says it waits for that word. */
  it('says the switch waits for an agent that says nothing of its limits', () => {
    wrap(<ScopeEditor agent={SILENT} product="Codex" scope={SILENT.scopes[0]!} accounts={[]} acts={acts()} />);

    expect(screen.getByText("Codex's sessions here do not say how near their limits are, so this waits for that word.")).toBeTruthy();
    expect(screen.getByRole('checkbox', { name: 'Switch before the limit · This machine' })).toBeTruthy();
  });
});

describe('a workspace\'s scope', () => {
  /** D130 §3.2: on this machine's accounts, it says which; its own accounts open the list, which the first Use writes. */
  it('says which accounts it runs on, and opens a list of its own on the press', async () => {
    const pressed = acts();
    wrap(<WorkspaceScope agent={THREE} product="Claude Code" workspace="lab" scope={null} machine={THREE.scopes[0]!} accounts={CHOICES} acts={pressed} />);

    expect(screen.getByText('Its starts run on this machine\'s accounts: work@example.invalid, home@example.invalid, spare@example.invalid.')).toBeTruthy();
    await userEvent.click(screen.getByRole('radio', { name: 'Its own accounts' }));
    await userEvent.click(screen.getByRole('checkbox', { name: 'use home@example.invalid for Workspace lab' }));

    expect(pressed.calls).toEqual([['order', 'lab', ['account-2']]]);
  });

  /** Back to this machine's accounts clears its own default, list and settings, so the first press asks. */
  it('asks once before returning to this machine\'s accounts', async () => {
    const pressed = acts();
    wrap(<WorkspaceScope agent={THREE} product="Claude Code" workspace="work" scope={THREE.scopes[1]!} machine={THREE.scopes[0]!} accounts={CHOICES} acts={pressed} />);

    await userEvent.click(screen.getByRole('radio', { name: "This machine's accounts" }));
    expect(pressed.calls).toEqual([]);
    expect(screen.getByText(/^This clears work's own default, list and settings for claude-code/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: "Use this machine's accounts" }));

    expect(pressed.calls).toEqual([['inherit', 'work']]);
  });

  /** A workspace on this machine's accounts where the machine has none says the tool's own sign-in, or the default. */
  it('says the tool\'s own sign-in, or this machine\'s default, where the machine has no list', () => {
    const { unmount } = wrap(<WorkspaceScope agent={THREE} product="Claude Code" workspace="lab" scope={null} machine={scopeOf()} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByText("Its starts run on the tool's own sign-in, as this machine's do.")).toBeTruthy();
    unmount();

    wrap(<WorkspaceScope agent={THREE} product="Claude Code" workspace="lab" scope={null} machine={scopeOf({ default: 'account-2', begins: 'account-2', use: USE_DEFAULTS })} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByText("Its starts run on this machine's default, home@example.invalid.")).toBeTruthy();
  });
});
