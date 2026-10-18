import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type AgentChoice, AgentChip } from './AgentChip';

/**
 * The agent and account chip both *New conversation* and *Go on with another agent…* draw (MIXAGENT1b-i, D163): the agents
 * offered, then the chosen agent's accounts in the scope's order. The start leaves both to the driver; the hand-over names the
 * agent, so it has no default row.
 */
const ACCOUNTS = {
  'claude-code': [{ name: 'personal', login: 'in' as const }],
  codex: [
    { name: 'team', login: 'in' as const, account: 'team@example.com' },
    { name: 'spare', login: 'in' as const },
    { name: 'lab', login: 'in' as const },
  ],
};

function show(over: Partial<React.ComponentProps<typeof AgentChip>> = {}) {
  const onChange = vi.fn<(next: AgentChoice) => void>();
  render(
    <AgentChip
      value={{ adapter: '', profile: '' }}
      onChange={onChange}
      harnesses={['claude-code', 'codex']}
      defaultHarness="claude-code"
      names={{ 'claude-code': 'Claude Code', codex: 'Codex' }}
      accounts={ACCOUNTS}
      {...over}
    />,
  );
  return onChange;
}

async function open() {
  screen.getByRole('button', { name: /Agent and account/ }).focus();
  await userEvent.keyboard('{Enter}');
  return screen.findByRole('menu');
}

describe('the agent and account chip', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('offers the driver’s own agent first by default, and reports the agent chosen with no account', async () => {
    const onChange = show({ value: { adapter: '', profile: 'personal' } });
    const menu = await open();

    expect(within(menu).getAllByRole('menuitemradio').map((row) => row.textContent).slice(0, 3))
      .toEqual(["This machine's default: Claude Code", 'Claude Code', 'Codex']);
    await userEvent.click(within(menu).getByRole('menuitemradio', { name: 'Codex' }));

    expect(onChange).toHaveBeenCalledWith({ adapter: 'codex', profile: '' });
  });

  it('names the agent where it is the person’s to name: no default row, and only the agents handed in', async () => {
    const onChange = show({ named: true, harnesses: ['codex'], value: { adapter: 'codex', profile: '' } });
    const menu = await open();

    expect(within(menu).getAllByRole('menuitemradio').slice(0, 1).map((row) => row.textContent)).toEqual(['Codex']);
    expect(within(menu).queryByText(/default/)).toBeNull();
    await userEvent.click(within(menu).getByRole('menuitemradio', { name: /team/ }));

    expect(onChange).toHaveBeenCalledWith({ adapter: 'codex', profile: 'team' });
  });

  it('lists the scope’s accounts first, in its order, then the others', async () => {
    show({
      named: true, harnesses: ['codex'], value: { adapter: 'codex', profile: '' }, repository: 'engine',
      scopeOf: () => ({ workspace: 'aurora', list: ['lab', 'team'] }),
    });
    const menu = await open();

    const rows = within(menu).getAllByRole('menuitemradio').map((row) => row.textContent ?? '');
    expect(rows.findIndex((text) => text.startsWith('lab'))).toBeLessThan(rows.findIndex((text) => text.startsWith('team')));
    expect(rows.findIndex((text) => text.startsWith('team'))).toBeLessThan(rows.findIndex((text) => text.startsWith('spare')));
  });

  /** ACCOUNTID1b: two sign-ins of one email read alike; each says its organisation, after the email and once. */
  it('tells two accounts of one email apart by their organisations, in its rows and on the chip', async () => {
    const email = 'person@example.invalid';
    show({
      value: { adapter: 'claude-code', profile: 'acct-0000000b' },
      accounts: {
        'claude-code': [
          { name: 'acct-0000000a', login: 'in' as const, account: email, org: 'Org A' },
          { name: 'acct-0000000b', login: 'in' as const, account: email, org: 'Org B' },
          { name: 'acct-0000000d', displayName: 'work', login: 'in' as const, account: email, org: 'Org D' },
        ],
      },
    });
    expect(screen.getByRole('button', { name: /Agent and account/ }).textContent).toBe(`Claude Code · ${email} · Org B`);
    const menu = await open();

    const rows = within(menu).getAllByRole('menuitemradio').map((row) => row.textContent);
    expect(rows.slice(-3)).toEqual([`${email} · Org A`, `${email} · Org B`, `work · ${email} · Org D`]);
  });
});
