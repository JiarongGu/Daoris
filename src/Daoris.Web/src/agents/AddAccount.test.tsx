import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ReactElement } from 'react';
import { cleanup, render as rtlRender, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../locales/i18n';
import { byTool, type ToolDoor } from './tools';
import { type AgentActs, AgentPage } from './AgentPage';
import { NameAccount } from './AddAccount';
import { CLAUDE_USE, DOORS, READ } from './agentsFixtures';

// The add flow's last step says what an account left unnamed is called (ACCTUX1, the 2026-10-07 second opinion): the name the
// page gives it, which for a fresh id is who signed in or a key's handle, never the id it said before, which the page shows
// only where nothing else names it. Since POOL1c it asks only that name (D197 point 4; the account-pool design §4.1).

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onReadOne: nothing, onReadNow: nothing, onSignInNew: nothing, onAddKey: nothing, onReplaceKey: nothing, onSignIn: nothing, onTryNow: nothing,
  onRemove: nothing, onRename: nothing, onAddedAnswer: nothing, onPooledSaid: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
  scope: { onOrder: nothing, onUse: nothing },
  rules: { onSwitchDefault: nothing, onRemove: nothing, onAdd: nothing, onAnswer: nothing },
};

const CLAUDE = DOORS.find((door) => door.harness === 'claude-code')!;
const FRESH = 'acct-5e1f0a2b';

/** Claude Code's page at the add flow's last step, the new account kept as `profile` and who signed in as `who`. */
function asking(profile: NonNullable<ToolDoor['profiles']>[number], who: string | null, acts: AgentActs = ACTS) {
  const [tool] = byTool([{ ...CLAUDE, profiles: [...CLAUDE.profiles!, profile] }]);
  return render(
    <AgentPage tool={tool!} use={CLAUDE_USE} rules={null} usage={[]} acts={acts} added={{ account: profile.name, who }} />,
  );
}

/**
 * POOL1c (D197 point 4; the account-pool design §4.1): the step says the account joined Daoris's accounts, asks its name, and
 * offers one press, *Done*; nothing about where it runs, so nothing *Not now* could lose, and no *This machine's list*.
 */
describe('a new account’s last step', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('says who signed in and that it joined Daoris’s accounts, asks its name, and offers Done alone', async () => {
    await i18n.changeLanguage('en');
    asking({ name: FRESH, home: `h/${FRESH}`, login: 'in', account: 'spare@example.invalid', read: READ }, 'spare@example.invalid');
    const step = screen.getByRole('region', { name: 'Add an account' });
    expect(step.textContent).toContain('Signed in as spare@example.invalid.');
    expect(step.textContent).toContain("spare@example.invalid joined Daoris's accounts: every workspace's work may run on it.");
    expect(screen.getByRole('textbox')).toBeTruthy();
    expect(screen.getAllByRole('button').filter((button) => step.contains(button)).map((button) => button.textContent)).toEqual(['Done']);
    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(step.textContent).not.toMatch(/this machine|Not now|Let it run work in/i);
  });

  it('hands the name typed to Done, and the terminal’s rename as its twin', async () => {
    await i18n.changeLanguage('en');
    const onDone = vi.fn();
    render(
      <NameAccount agent="claude-code" account={FRESH} title="Add an account" signedIn="spare@example.invalid" offered="" fallback="spare@example.invalid" onDone={onDone} />,
    );
    expect(document.body.textContent).not.toContain('daoris agent profile');
    await userEvent.type(screen.getByRole('textbox'), 'spare');
    expect(document.body.textContent).toContain('daoris agent profile rename claude-code acct-5e1f0a2b spare');
    expect(document.body.textContent).not.toContain('profile join');
    await userEvent.click(screen.getByRole('button', { name: 'Done' }));
    expect(onDone).toHaveBeenCalledWith({ name: 'spare' });
  });

  it('says a key added and the same sentence, and says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(
      <NameAccount agent="claude-code" account={FRESH} title="添加账户" lead="已添加 API 密钥 …wxyz。" offered="" fallback="API 密钥 …wxyz" onDone={nothing} />,
    );
    expect(screen.getByText('API 密钥 …wxyz 已加入 Daoris 的账户，所有工作区的工作都可以在它上面运行。')).toBeTruthy();
    expect(screen.getByRole('button', { name: '完成' })).toBeTruthy();
    expect(document.body.textContent).not.toMatch(/本机/);
  });
});

describe('an account left unnamed (ACCTUX1)', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it('is called who signed in, which the hint says, and a terminal types its id', async () => {
    await i18n.changeLanguage('en');
    asking({ name: FRESH, home: `h/${FRESH}`, login: 'in', account: 'spare@example.invalid', read: READ }, 'spare@example.invalid');
    expect(screen.getByText(
      `One word a terminal can type. Left empty, it is called spare@example.invalid, and a terminal types its id, ${FRESH}.`)).toBeTruthy();
    expect(screen.getByRole('textbox').getAttribute('placeholder')).toBe('spare@example.invalid');
  });

  it('is called its key’s handle where it is a key', async () => {
    await i18n.changeLanguage('en');
    asking({ name: FRESH, home: `h/${FRESH}`, login: 'in', key: '…wxyz', read: READ }, null);
    expect(screen.getByText(`One word a terminal can type. Left empty, it is called API key …wxyz, and a terminal types its id, ${FRESH}.`))
      .toBeTruthy();
  });

  it('is called its id where nothing else names it', async () => {
    await i18n.changeLanguage('en');
    asking({ name: FRESH, home: `h/${FRESH}`, login: 'in', read: READ }, null);
    expect(screen.getByText(`One word a terminal can type. Left empty, it is called ${FRESH}.`)).toBeTruthy();
  });

  it('says the same in 中文', async () => {
    await i18n.changeLanguage('zh');
    asking({ name: FRESH, home: `h/${FRESH}`, login: 'in', account: 'spare@example.invalid', read: READ }, 'spare@example.invalid');
    expect(screen.getByText(`终端里能输入的一个词。留空则称为 spare@example.invalid，终端里输入它的 ID ${FRESH}。`)).toBeTruthy();
  });
});
