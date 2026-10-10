import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { ContextMenus } from '../menus/ContextMenu';
import { menuActs, rightClick } from '../test/contextMenu';
import { byTool } from '../tools';
import { AgentList, AgentStrip } from './AgentList';
import { agentRows } from './agents';
import { CLAUDE_USE, CODEX_USE, TOOLS } from './agentsFixtures';

// The Agents list closed to its strip (D118 §3a; ACCTUX2): each agent a mark a person tells from its neighbours, where the
// first character alone drew Claude Code and Codex both `C`.

describe('the agent strip', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(cleanup);

  it('draws each agent by a mark of its own, named in words', () => {
    const rows = agentRows(TOOLS, { agents: [CLAUDE_USE, CODEX_USE] });
    render(<Tooltip.Provider><AgentStrip rows={rows} chosen="claude-code" onChoose={() => {}} /></Tooltip.Provider>);

    const claude = screen.getByRole('button', { name: /^Claude Code · / });
    const codex = screen.getByRole('button', { name: /^Codex · / });
    expect(claude).toHaveTextContent(/^CC$/);
    expect(codex).toHaveTextContent(/^Co$/);
    const faces = screen.getAllByRole('button').map((mark) => mark.textContent);
    expect(new Set(faces).size).toBe(faces.length);
  });
});

// INSTALLDOOR1: where Daoris has no installer for an agent, its row offers *How to install*, which opens its page and runs none.
describe('the agent list, an agent Daoris has no installer for', () => {
  beforeEach(() => i18n.changeLanguage('en'));
  afterEach(cleanup);

  const rows = (installs?: boolean) => agentRows(byTool([
    { harness: 'forge', product: 'Forge', maker: 'Acme', present: false, profiles: [], ...(installs === undefined ? {} : { installs }) },
  ]), undefined);

  it('offers How to install in the Install slot, and pressing it only chooses the agent', async () => {
    const onChoose = vi.fn();
    const onInstall = vi.fn();
    render(<AgentList rows={rows(false)} chosen={null} onChoose={onChoose} onInstall={onInstall} />);

    expect(screen.queryByRole('button', { name: 'Install Forge' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'How to install Forge' }));
    expect(onChoose).toHaveBeenCalledWith('forge');
    expect(onInstall).not.toHaveBeenCalled();
  });

  it('offers How to install in the row’s menu too, and Install where an installer is, an absent field reading as one', async () => {
    render(<ContextMenus doors={{ copy: vi.fn() }} />);
    const { unmount } = render(<AgentList rows={rows(false)} chosen={null} onChoose={() => {}} onInstall={() => {}} />);
    rightClick(screen.getByText('Forge'));
    const acts = await menuActs('Actions for Forge');
    expect(acts).toContain('How to install');
    expect(acts).not.toContain('Install');
    unmount();

    render(<AgentList rows={rows()} chosen={null} onChoose={() => {}} onInstall={() => {}} />);
    expect(screen.getByRole('button', { name: 'Install Forge' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'How to install Forge' })).toBeNull();
  });
});
