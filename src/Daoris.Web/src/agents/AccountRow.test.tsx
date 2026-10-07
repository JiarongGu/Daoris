import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import type { ReactElement } from 'react';
import { cleanup, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { byTool, type ToolDoor } from '../tools';
import { AccountRow } from './AccountRow';
import { type AgentActs, AgentPage } from './AgentPage';
import { accountState } from './agents';
import { CLAUDE_USE, DOORS, READ } from './agentsFixtures';

// An account's row as a person reads it (ACCTUX1, the 2026-10-07 second opinion's account section): what its agent last said
// is a reading, not metadata, so it is set at the row's own size, its shares in the ink and its times in the soft ink; a key
// its provider refused says so, with the act that repairs it; and a name that is its identity is said once.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onReadOne: nothing, onSignInNew: nothing, onAddKey: nothing, onSignIn: nothing, onTryNow: nothing, onDefault: nothing,
  onRemove: nothing, onRename: nothing, onJoin: nothing, onAddedAnswer: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
  scope: { onOrder: nothing, onUse: nothing, onInherit: nothing },
  rules: { onSwitchDefault: nothing, onRemove: nothing, onAdd: nothing, onAnswer: nothing },
};

const CLAUDE = DOORS.find((door) => door.harness === 'claude-code')!;
const HOME = 'C:/somewhere/data/harnesses/claude-code/';

/** Claude Code's page with these accounts, each in this machine's list so each holds work. */
function page(profiles: NonNullable<ToolDoor['profiles']>) {
  const [tool] = byTool([{ ...CLAUDE, machineDefault: null, workspaceDefaults: [], profiles }]);
  const use = {
    ...CLAUDE_USE,
    accounts: profiles.map((profile) => ({ name: profile.name, running: 0 })),
    scopes: [{ ...CLAUDE_USE.scopes[0]!, default: null, begins: profiles[0]!.name, list: profiles.map((profile) => profile.name) }],
  };
  return render(<AgentPage tool={tool!} use={use} rules={null} usage={[]} workspaces={['work']} acts={ACTS} />);
}

describe('an account’s row (ACCTUX1)', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(cleanup);

  /** Measured by the review at 3.38:1 in light as `text-meta text-ink-faint`: the readings a person acts on, said faintest. */
  it('sets what its agent last said at the row’s size, its shares in the ink and its times in the soft ink', () => {
    render(
      <ul>
        <AccountRow
          name="spare"
          state={accountState({ login: 'in', read: READ }, null, true)}
          runs="work"
          menu={[]}
          said={[
            { text: '88% of its five-hour limit used', tone: 'reading' },
            { text: ', ', tone: 'join' },
            { text: 'resets 15:00', tone: 'when' },
          ]}
        />
      </ul>,
    );
    const share = screen.getByText('88% of its five-hour limit used');
    const line = share.parentElement!;
    expect(line.className).toContain('text-small');
    expect(line.className).not.toMatch(/text-meta|text-ink-faint/);
    expect(share.className).toContain('text-ink');
    expect(share.className).not.toContain('text-ink-soft');
    expect(screen.getByText('resets 15:00').className).toContain('text-ink-soft');
  });

  it('says a key its provider refused as Key refused, with Add an API key, which opens the key’s field', async () => {
    page([{ name: 'acct-0000abcd', home: `${HOME}acct-0000abcd`, login: 'out', key: '…abcd', read: READ }]);

    const row = screen.getByRole('listitem', { name: 'API key …abcd' });
    expect(within(row).getByText('key refused')).toBeTruthy();
    expect(within(row).queryByText('unchecked')).toBeNull();
    expect(within(row).queryByRole('button', { name: /Sign in/ })).toBeNull();
    await userEvent.click(within(row).getByRole('button', { name: 'Add an API key in place of API key …abcd' }));
    expect(screen.getByLabelText('API key for Claude Code')).toBeTruthy();
  });

  /** ACCTNAME1's page half: the owner named each account by its email, so the row says that email once. */
  it('says a name that is its identity once', () => {
    page([{
      name: 'Gmail', displayName: 'You@Example.invalid', home: `${HOME}Gmail`, login: 'in', account: 'you@example.invalid', read: READ,
    }]);

    const row = screen.getByRole('listitem', { name: 'You@Example.invalid' });
    expect(within(row).getAllByText(/you@example\.invalid/i)).toHaveLength(1);
  });
});

/** A Tailwind spacing class's size in px, on the 4 px step: `min-h-7` is 28. */
const px = (className: string, prefix: string) => {
  const step = new RegExp(`(?:^|\\s)${prefix}-(\\d+(?:\\.\\d+)?)(?:\\s|$)`).exec(className)?.[1];
  return step === undefined ? undefined : Number(step) * 4;
};

// ACCTUX2, the second opinion's account section: the account view without a pointer. Who signed in sat truncated in a tip on
// a span no key reaches; why a row cools, or why its key is unchecked or refused, was a tip on hover alone; the columns'
// names were hidden from a reader with nothing on the rows in their place; and two presses were under the 28 px floor.
describe('an account’s row without a pointer (ACCTUX2)', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(cleanup);

  it('says who signed in whole, wrapping, never cut into a tip', () => {
    render(
      <ul>
        <AccountRow
          name="spare"
          who="a.rather.long.identity.for.a.narrow.row@example.invalid"
          state={accountState({ login: 'in', read: READ }, null, true)}
          runs="work"
          menu={[]}
        />
      </ul>,
    );
    const who = screen.getByText('a.rather.long.identity.for.a.narrow.row@example.invalid');
    expect(who).not.toHaveClass('truncate');
    expect(who.className).toContain('[overflow-wrap:anywhere]');
    // A tip's trigger wears its state; the identity is text, not a trigger.
    expect(who).not.toHaveAttribute('data-state');
  });

  it('names each fact on its row for a reader: its state, what it runs for, and what runs on it now', () => {
    render(
      <ul>
        <AccountRow
          name="spare"
          state={accountState({ login: 'in', read: READ }, null, true)}
          runs="work · forge"
          current="1 session"
          menu={[]}
        />
      </ul>,
    );
    const row = screen.getByRole('listitem', { name: 'spare' });
    for (const [label, value] of [['State', 'signed in'], ['Runs for', 'work · forge'], ['Now', '1 session']] as const) {
      const named = within(row).getByText(label);
      expect(named, label).toHaveClass('sr-only');
      expect(named.parentElement!.textContent, label).toContain(value);
    }
  });

  /** Whether the hold is the agent's reset or Daoris's default was the time's tip alone. */
  it('says why an account cools behind a press a keyboard reaches, in words', async () => {
    const until = new Date(Date.now() + 48 * 60_000).toISOString();
    const cooling = { until, stated: false, window: null, seen: READ, assumedZone: false, notBelieved: false };
    render(
      <ul>
        <AccountRow name="reserve" state={accountState({ login: 'in', read: READ }, { cooling }, true)} runs="work" menu={[]} />
      </ul>,
    );
    const row = screen.getByRole('listitem', { name: 'reserve' });
    expect(within(row).queryByText(/Daoris's default: the agent named no time/)).toBeNull();

    const about = within(row).getByRole('button', { name: "About reserve's state" });
    expect(about).toHaveAttribute('aria-expanded', 'false');
    expect(px(about.className, 'h')).toBeGreaterThanOrEqual(28);
    expect(px(about.className, 'w')).toBeGreaterThanOrEqual(28);
    about.focus();
    await userEvent.keyboard('{Enter}');

    expect(about).toHaveAttribute('aria-expanded', 'true');
    const why = within(row).getByText(/^Cooling until .+ · Daoris's default: the agent named no time$/);
    expect(about.getAttribute('aria-controls')).toBe(why.id);
    await userEvent.keyboard('{Enter}');
    expect(within(row).queryByText(/Daoris's default: the agent named no time/)).toBeNull();
  });

  it('says what repairs a refused key, and why a key reads unchecked, behind the same press', async () => {
    page([
      { name: 'acct-0000abcd', home: `${HOME}acct-0000abcd`, login: 'out', key: '…abcd', read: READ },
      { name: 'acct-0000wxyz', home: `${HOME}acct-0000wxyz`, login: 'in', key: '…wxyz', read: READ },
    ]);

    const refused = screen.getByRole('listitem', { name: 'API key …abcd' });
    await userEvent.click(within(refused).getByRole('button', { name: "About API key …abcd's state" }));
    expect(within(refused).getByText(/^Its provider refused this key/)).toBeTruthy();
    const unchecked = screen.getByRole('listitem', { name: 'API key …wxyz' });
    await userEvent.click(within(unchecked).getByRole('button', { name: "About API key …wxyz's state" }));
    expect(within(unchecked).getByText(/^The tool says signed in for any key/)).toBeTruthy();
  });

  it('offers no such press where the state needs no explanation', () => {
    render(<ul><AccountRow name="spare" state={accountState({ login: 'in', read: READ }, null, true)} runs="work" menu={[]} /></ul>);
    expect(screen.queryByRole('button', { name: /state$/ })).toBeNull();
  });

  /** The platform language §6: a target of 28 px at least. The terms' line was its text's height, about 16 px. */
  it('gives the terms’ line a target of 28 px at least', () => {
    page([{ name: 'account-1', home: `${HOME}account-1`, login: 'in', account: 'you@example.invalid', read: READ }]);
    const terms = screen.getByRole('button', { name: "Each account's own plan and terms" });
    expect(px(terms.className, 'min-h')).toBeGreaterThanOrEqual(28);
  });
});
