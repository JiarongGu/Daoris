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
