import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import {
  AHEAD, CHOICES, MACHINE_NEAR, OFFERED_AGAIN, OWN_COOLING, scopeOf, SILENT, SIX, SIX_AGENT, SIX_CHOICES, THREE,
} from './accountsFixtures';
import { AccountFactsLines, type ScopeActs, ScopeEditor, TermsLine } from './AccountUse';

// How an agent's accounts are used, as molecules (TOOL4g; D125 §2.4, §3.7, §6; D130 §9, §16.6; POOL1c, D197): props in,
// presses out, each the terminal's door's twin, which names no workspace since one set serves them all. With three accounts
// and with six: nothing counts accounts.

const acts = (): ScopeActs & { calls: unknown[][] } => {
  const calls: unknown[][] = [];
  return {
    calls,
    onOrder: (list) => { calls.push(['order', list]); },
    onUse: (change) => { calls.push(['use', change]); },
  };
};

const wrap = (node: React.ReactElement) => render(<Tooltip.Provider>{node}</Tooltip.Provider>);

/** Every terminal twin drawn, in the page's order: each hint's code span that is a command. */
const twins = () => [...document.querySelectorAll('code')]
  .map((span) => span.textContent ?? '')
  .filter((text) => text.startsWith('daoris '));

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

  /** USAGEREAD3b (D165 point 2): the agent's pace warning in its own words, never as near its limit. */
  it('says its agent’s pace warning as ahead of its pace, never as near', () => {
    wrap(<AccountFactsLines facts={AHEAD} label="home@example.invalid" onTryNow={() => {}} />);

    const line = screen.getByText(/said 3h ago: ahead of its pace on its weekly window, by its own word; 88% of its five-hour limit used/);
    expect(line.textContent).not.toContain('near');
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

describe('the terms', () => {
  it('says each account\'s own plan and terms apply', () => {
    wrap(<TermsLine />);

    expect(screen.getByText(/^Each account's own plan and terms apply\./)).toBeTruthy();
  });
});

describe('the set\'s order', () => {
  /**
   * POOL1c (D197 point 2; the account-pool design §4.2): every account of Daoris's in the order, each with up and down and no
   * *Use* switch, since every account serves every workspace; a move writes the whole order, one it did not name yet included.
   */
  it('moves each account up and down in the whole order, with no Use switch', async () => {
    const pressed = acts();
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1' })} accounts={CHOICES} acts={pressed} />);

    const list = screen.getByRole('list', { name: "Daoris's accounts, in the order they are used" });
    expect(within(list).getAllByRole('listitem').map((row) => row.textContent)).toEqual([
      expect.stringContaining('work@example.invalid'), expect.stringContaining('home@example.invalid'),
      expect.stringContaining('spare@example.invalid'),
    ]);
    expect(screen.queryByRole('checkbox')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Move up · home@example.invalid' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move up · spare@example.invalid' }));

    expect(pressed.calls).toEqual([
      ['order', ['account-2', 'account-1', 'account-3']],
      ['order', ['account-1', 'account-3', 'account-2']],
    ]);
    expect(screen.getByRole('button', { name: 'Move up · work@example.invalid' }).hasAttribute('disabled')).toBe(true);
    expect(screen.getByRole('button', { name: 'Move down · spare@example.invalid' }).hasAttribute('disabled')).toBe(true);
    // A signed-out account says so where it would be passed.
    expect(within(list).getAllByRole('listitem')[2]!.textContent).toContain('signed out');
    expect(document.body.textContent).not.toMatch(/This machine|this scope|Use off/);
  });

  /**
   * ACCTUX2: a target of 28 px at least (the platform language §6). Each arrow was its 13 px glyph and `px-1.5`, about 25 px
   * wide; the button's own floor gives its height.
   */
  it('gives each move up and down a target of 28 px at least', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1' })} accounts={CHOICES} acts={acts()} />);

    const moves = screen.getAllByRole('button', { name: /^Move (up|down) · / });
    expect(moves).toHaveLength(6);
    for (const move of moves) {
      const width = /(?:^|\s)min-w-(\d+)(?:\s|$)/.exec(move.className)?.[1];
      expect(Number(width) * 4, move.getAttribute('aria-label') ?? '').toBeGreaterThanOrEqual(28);
    }
  });

  /**
   * Where starts begin, the kept account, one a start passes, and one cooling, each said on its row; what passes it in the
   * curve's words (SWITCH1d): over its curve, its limit reached, or on usage credits.
   */
  it('marks where starts begin, the kept account, one over its curve and one cooling', () => {
    const { unmount } = wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={acts()} />);

    const rows = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(rows[0]!.textContent).toContain('starts here');
    expect(rows[0]!.textContent).toContain('cooling');
    expect(rows[1]!.textContent).toContain('over its curve');
    expect(rows[1]!.textContent).not.toContain('near its limit');
    expect(rows[2]!.textContent).toContain('kept for conversations');
    unmount();

    const passed = (by: 'refused' | 'credits') => ({ ...MACHINE_NEAR, near: [{ account: 'account-2', window: 'session', by }] });
    const reached = wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={passed('refused')} accounts={CHOICES} acts={acts()} />);
    expect(within(screen.getByRole('list')).getAllByRole('listitem')[1]!.textContent).toContain('limit reached');
    reached.unmount();
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={passed('credits')} accounts={CHOICES} acts={acts()} />);
    expect(within(screen.getByRole('list')).getAllByRole('listitem')[1]!.textContent).toContain('on usage credits');
  });

  /** USAGEREAD3b: passed is the scope's own (a limit, credits or the number), so a pace warning alone wears no pill. */
  it('marks no account passed for its agent’s pace warning alone', () => {
    const agent = { ...THREE, accounts: THREE.accounts.map((account) => (account.name === 'account-3' ? { ...AHEAD, name: 'account-3' } : account)) };
    wrap(<ScopeEditor agent={agent} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={acts()} />);

    const rows = within(screen.getByRole('list')).getAllByRole('listitem');
    expect(rows[1]!.textContent).toContain('over its curve');
    expect(rows[2]!.textContent).not.toContain('over its curve');
  });

  /**
   * POOL1c: an answer that names no order yet still draws every account of Daoris's, in the agent's order, and the one entry of
   * settings, since every account serves every workspace; nothing says *no account is used here*.
   */
  it('draws every account and the settings where the answer names no order yet', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf()} accounts={CHOICES} acts={acts()} />);

    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(3);
    expect(screen.getAllByRole('radiogroup')).toHaveLength(2);
    expect(screen.getByText('Keep for conversations')).toBeTruthy();
    expect(screen.queryByText(/No account is used here yet/)).toBeNull();
  });

  /** A hand-edited file that breaks the rule is read with the order winning, and said (D130 §3.1); a default is never said. */
  it('says a kept account the order does not hold, and no default', () => {
    const { unmount } = wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1', 'account-2'], begins: 'account-1', problem: { kind: 'keep', account: 'account-3' } })} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByText(/^The account kept for conversations, spare@example\.invalid, is not in the order/)).toBeTruthy();
    unmount();

    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1'], begins: 'account-1', problem: { kind: 'default', account: 'account-3' } })} accounts={CHOICES} acts={acts()} />);
    expect(screen.queryByText(/This scope's default|is not in its list/)).toBeNull();
  });
});

describe('how a scope\'s list is used', () => {
  /**
   * D130 §16.6 as D165 §6 amends it: *Use accounts*, *Keep for conversations* and *Switch accounts*, each one setting; the switch
   * *Smart*, *My own curve* or *Off*, as `profile use --switch` writes it (SWITCH1d). *Near at* is gone: the curve is its own.
   */
  it('changes one setting at a time, as the terminal\'s profile use does', async () => {
    const pressed = acts();
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={pressed} />);

    await userEvent.click(screen.getByRole('radio', { name: 'One by one, in order' }));
    const switched = screen.getByRole('radiogroup', { name: 'Switch accounts' });
    expect(within(switched).getByRole('radio', { name: 'My own curve' })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(within(switched).getByRole('radio', { name: 'Off' }));
    await userEvent.click(within(switched).getByRole('radio', { name: 'Smart' }));

    expect(pressed.calls).toEqual([
      ['use', { use: 'order' }],
      ['use', { switch: 'off' }],
      ['use', { switch: 'smart' }],
    ]);
    expect(screen.getByText(/^Each start goes to the account running the fewest/)).toBeTruthy();
    expect(screen.getByText(/^Your own rows, a window each; a window you leave out takes Daoris's curve\.$/)).toBeTruthy();
    expect(screen.queryByText('Near at')).toBeNull();
    expect(screen.queryByText('Switch before the limit')).toBeNull();
  });

  /**
   * SWITCH1d (the switch design §5): each window's curve in effect, drawn with each account of the list where it stands now, the
   * five-hour window first; under *Smart* no point is anyone's to edit.
   */
  it('draws each window’s curve with each account of the list on it', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={THREE.scopes[0]!} accounts={CHOICES} acts={acts()} />);

    const charts = screen.getAllByRole('img');
    expect(charts.map((chart) => chart.getAttribute('aria-label'))).toEqual([
      "The five-hour window: its curve, and each account's use now",
      "The weekly window: its curve, and each account's use now",
    ]);
    // account-2 said 88% of its five-hour window: the smart curve's 90% holds it under.
    const session = charts[0]!.querySelector('[data-account="account-2"]')!;
    expect(session.querySelector('title')!.textContent).toMatch(/^home@example\.invalid: 88% used, \d+% of the window gone$/);
    expect(charts[0]!.querySelector('[data-curve]')!.getAttribute('data-curve')).toBe('0:90 100:90');
    expect(screen.queryByRole('group', { name: /^Your rows/ })).toBeNull();
  });

  /** SWITCH1d (the switch design §5, §6): *My own curve* edits a window's points, saved as `--curve`, and clears them all. */
  it('edits a window’s own points under My own curve, and clears them as --no-curve does', async () => {
    const pressed = acts();
    // The curve in effect as the accounts' answer carries it (PUSH1e): the person's five-hour points, Daoris's week.
    const scope = {
      ...MACHINE_NEAR,
      use: { ...MACHINE_NEAR.use, switch: 'curve' as const, curve: { session: [[0, 80]] as [number, number][] } },
      curves: { ...MACHINE_NEAR.curves, session: [[0, 80]] as [number, number][] },
    };
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scope} accounts={CHOICES} acts={pressed} />);

    expect(screen.getAllByRole('img')[0]!.querySelector('[data-curve]')!.getAttribute('data-curve')).toBe('0:80 100:80');
    expect(screen.getByText("The weekly window takes Daoris's curve.")).toBeTruthy();
    const session = screen.getByRole('group', { name: 'Your rows for the five-hour window' });
    const used = within(session).getByRole('spinbutton', { name: 'Used, % · row 1' });
    await userEvent.clear(used);
    await userEvent.type(used, '85');
    await userEvent.click(within(session).getByRole('button', { name: 'Save the curve' }));
    await userEvent.click(screen.getByRole('button', { name: 'Clear my own curve' }));

    expect(pressed.calls).toEqual([
      ['use', { curve: { session: [[0, 85]] } }],
      ['use', { noCurve: true }],
    ]);
    expect(twins()).toEqual(expect.arrayContaining([
      'daoris agent profile use claude-code --curve session=0:80',
      'daoris agent profile use claude-code --curve weekly=<elapsed>:<used>,…',
      'daoris agent profile use claude-code --no-curve',
    ]));
  });

  /** D130 §16.4: with nothing said by any account of the list, the screen says what the walk does instead. */
  it('says nothing has been said yet, with six accounts', () => {
    wrap(<ScopeEditor agent={SIX_AGENT} product="Claude Code" scope={SIX_AGENT.scopes[0]!} accounts={SIX_CHOICES} acts={acts()} />);

    expect(within(screen.getByRole('list')).getAllByRole('listitem')).toHaveLength(SIX.length);
    expect(screen.getByText(/^No account has said what it has left yet\./)).toBeTruthy();
  });

  /** The order's cost is said where it is chosen (D130 §16.6). */
  it('says what one by one costs', () => {
    const scope = { ...THREE.scopes[0]!, use: { ...THREE.scopes[0]!.use, use: 'order' as const } };
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scope} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText(/one limit stops every session on that account\.$/)).toBeTruthy();
  });

  /** A set of one keeps nothing: keeping its only account would leave driven work none. */
  it('offers no kept account with one account of Daoris’s', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1'], begins: 'account-1' })} accounts={CHOICES.slice(0, 1)} acts={acts()} />);

    expect(screen.getByText(/^Keeping an account for conversations needs another account/)).toBeTruthy();
    expect(screen.queryByRole('combobox')).toBeNull();
  });

  /** D130 §6: an agent whose sessions say nothing of their limits keeps the switch, and says it waits for that word. */
  it('says the switch waits for an agent that says nothing of its limits', () => {
    wrap(<ScopeEditor agent={SILENT} product="Codex" scope={SILENT.scopes[0]!} accounts={[]} acts={acts()} />);

    expect(screen.getByText("Codex's sessions here do not say how much of each window is used, so a start passes no account by its use.")).toBeTruthy();
    expect(screen.getByRole('radiogroup', { name: 'Switch accounts' })).toBeTruthy();
  });

  /**
   * SWITCH1b-iii (D165 point 2, the switch design §3): the switch says what passes an account at a start, its limit reached,
   * usage credits or its use over its curve, never the agent's own word that it is near, nor one fixed percentage.
   */
  it('says what passes an account: its limit reached, usage credits, or its use over its curve', async () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={MACHINE_NEAR} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByRole('note', { name: /^Per window, a curve says the most of it an account should have used at each moment of the window's time\. A start passes an account whose limit is reached, that draws on usage credits, or whose use is over its curve, while another account is ready/ })).toBeTruthy();
    expect(screen.getByText(/^Each start goes to [^.]+, and an account whose limit is reached, that draws on usage credits or whose use is over its curve is passed while another is ready\.$/)).toBeTruthy();
    expect(document.body.textContent).not.toMatch(/near its limit|at or over this share|Near at/);
  });
});

describe('the next start', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  /**
   * TOOL6e, the owner's case: the order's first came out of its cool-off and nothing runs, so the next start takes it; another
   * account cools; and the tool's own sign-in, which the owner had switched, carries none of these starts.
   */
  it('says which account the next start takes, why, since when it is offered again, and what holds the others', () => {
    wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={OFFERED_AGAIN.scopes[0]!} accounts={CHOICES} acts={acts()} />);

    const row = screen.getByText('Next start').closest('div.\\@container') as HTMLElement;
    expect(row).toBeTruthy();
    expect(within(row).getByText(/^The next start takes work@example\.invalid: it comes first in the list\. Its cool-off ended at .+\.$/)).toBeTruthy();
    expect(within(row).getByText(/^spare@example\.invalid is cooling until .+\.$/)).toBeTruthy();
    expect(within(row).getByText(/^Claude Code's own sign-in, the account it uses at your terminal, carries none of these starts/)).toBeTruthy();
    // Its terminal twin: `profile use` names the steps, since the CLI reads no session record to name the account; no workspace.
    expect(row.textContent).toContain('daoris agent profile use claude-code');
    expect(row.textContent).not.toContain('--workspace');
  });

  /** A test double's own home, where the reason is `own`, draws no next start: nothing else says it either. */
  it('says the tool\'s own sign-in once, and its wait without claiming it carries none', () => {
    const { unmount } = wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ next: { account: null, reason: 'own', others: [] } })} accounts={CHOICES} acts={acts()} />);
    expect(screen.queryByText('Next start')).toBeNull();
    unmount();

    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ next: { account: null, reason: 'waits', when: OWN_COOLING.until, others: [{ account: null, hold: 'cooling', until: OWN_COOLING.until }] } })} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByText(/^No account here is ready, so the next start waits until .+\.$/)).toBeTruthy();
    expect(screen.getByText(/^the tool's own sign-in is cooling until .+\.$/)).toBeTruthy();
    expect(screen.queryByText(/carries none of these starts/)).toBeNull();
  });

  /** D175 point 2 and OWNSPACE1b settlement 4: no Daoris account signed in is its own line, apart from a wait. */
  it('says no Daoris account is signed in, when the reason arrives', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ next: { account: null, reason: 'noAccount', others: [] } })} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByText('No Daoris account is signed in for it; its starts wait until you sign in.')).toBeTruthy();
  });

  /** A set whose every account is held waits, and its own-sign-in sentence stands: switching that would not help. */
  it('says a list\'s wait with the sentence that the tool\'s own sign-in carries none of its starts', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1'], begins: 'account-1', next: { account: null, reason: 'waits', when: OWN_COOLING.until, others: [{ account: 'account-1', hold: 'cooling', until: OWN_COOLING.until }] } })} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText(/^No account here is ready, so the next start waits until .+\.$/)).toBeTruthy();
    expect(screen.getByText(/^work@example\.invalid is cooling until .+\.$/)).toBeTruthy();
    expect(screen.getByText(/carries none of these starts/)).toBeTruthy();
  });

  /**
   * SWITCH1b-iii (the switch design §3): the walk's steps say what goes last, the least over first, and step 5 by the most
   * room under the curve, or by the week's pace with the switch off; one by one says what goes last, with no switch named.
   */
  it('says the walk by the curve, and by the week\'s pace with the switch off', async () => {
    const set = OFFERED_AGAIN.scopes[0]!;
    const { unmount } = wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={set} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByRole('note', { name: /^Each start walks this list: .+ whose use is over its curve goes last, the least over first; .+, then the one with the most room under its curve, / })).toBeTruthy();
    unmount();

    const paced = wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={{ ...set, use: { ...set.use, early: false, switch: 'off' } }} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByRole('note', { name: /^Each start walks this list: .+ whose use is over Daoris's curve goes last, the least over first; .+, then the one furthest behind its week's pace, / })).toBeTruthy();
    paced.unmount();

    // POOL1c (D197 point 2): one by one begins at the order's first, since the set has no default beside it.
    wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={{ ...set, use: { ...set.use, use: 'order' } }} accounts={CHOICES} acts={acts()} />);
    expect(screen.getByRole('note', { name: /^Each start runs on the first ready account in the order, begun at its first; an account whose limit is reached, that draws on usage credits or whose use is over its curve goes last\./ })).toBeTruthy();
  });

  /** An answer from a shell older than TOOL6e has no `next`: the row is not drawn. */
  it('draws no row where the answer says nothing of the next start', () => {
    wrap(<ScopeEditor agent={THREE} product="Claude Code" scope={scopeOf({ list: ['account-1'], begins: 'account-1' })} accounts={CHOICES} acts={acts()} />);

    expect(screen.queryByText('Next start')).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={OFFERED_AGAIN.scopes[0]!} accounts={CHOICES} acts={acts()} />);

    expect(screen.getByText('下一次启动')).toBeTruthy();
    expect(screen.getByText(/^下一次启动使用 work@example\.invalid：.+。它的冷却在 .+ 结束。$/)).toBeTruthy();
    expect(document.body.textContent).not.toMatch(/本机/);
    expect(screen.getByText(/^Claude Code 自己的登录/)).toBeTruthy();
  });

  /** An account's row says since when it is offered again, where its cool-off ended within the day. */
  it('says on an account\'s row since when it is offered again', () => {
    wrap(<AccountFactsLines facts={OFFERED_AGAIN.accounts[0]!} label="work@example.invalid" onTryNow={() => {}} />);

    expect(screen.getByText(/^0 of Daoris's sessions running · offered again since .+ · nothing said yet/)).toBeTruthy();
  });
});

/**
 * ACCTQUOTE1c (D125's ACCTQUOTE1 note), POOL1c (D197 point 6): each hint is a command a person pastes into whichever shell they
 * have, and names no workspace, since one set of Daoris's accounts serves them all and the terminal refuses `--workspace`.
 */
describe('the set\'s terminal twins', () => {
  it('names no workspace in any twin', () => {
    wrap(<ScopeEditor agent={OFFERED_AGAIN} product="Claude Code" scope={OFFERED_AGAIN.scopes[0]!} accounts={CHOICES} acts={acts()} />);
    expect(twins()).toEqual([
      'daoris agent profile use claude-code',
      'daoris agent profile order claude-code <account>…',
      'daoris agent profile use claude-code goal|order',
      'daoris agent profile use claude-code --keep <account>|--no-keep',
      'daoris agent profile use claude-code --switch smart|curve|off',
    ]);
  });
});
