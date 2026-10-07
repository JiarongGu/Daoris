import { afterEach, describe, expect, it } from 'vitest';
import type { ReactElement } from 'react';
import { cleanup, render as rtlRender, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { byTool, type ToolDoor } from '../tools';
import { type AgentActs, AgentPage } from './AgentPage';
import { CLAUDE_USE, DOORS, READ } from './agentsFixtures';

// The add flow's last step says what an account left unnamed is called (ACCTUX1, the 2026-10-07 second opinion): the name the
// page gives it, which for a fresh id is who signed in or a key's handle, never the id it said before, which the page shows
// only where nothing else names it.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);
const nothing = () => {};
const ACTS: AgentActs = {
  onReadAgain: nothing, onReadOne: nothing, onSignInNew: nothing, onAddKey: nothing, onSignIn: nothing, onTryNow: nothing, onDefault: nothing,
  onRemove: nothing, onRename: nothing, onJoin: nothing, onAddedAnswer: nothing, onSaveSettings: nothing, onDoor: nothing, onPin: nothing,
  scope: { onOrder: nothing, onUse: nothing, onInherit: nothing },
  rules: { onSwitchDefault: nothing, onRemove: nothing, onAdd: nothing, onAnswer: nothing },
};

const CLAUDE = DOORS.find((door) => door.harness === 'claude-code')!;
const FRESH = 'acct-5e1f0a2b';

/** Claude Code's page at the add flow's last step, the new account kept as `profile` and who signed in as `who`. */
function asking(profile: NonNullable<ToolDoor['profiles']>[number], who: string | null) {
  const [tool] = byTool([{ ...CLAUDE, profiles: [...CLAUDE.profiles!, profile] }]);
  return render(
    <AgentPage tool={tool!} use={CLAUDE_USE} rules={null} usage={[]} workspaces={['work']} acts={ACTS} added={{ account: profile.name, who }} />,
  );
}

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
