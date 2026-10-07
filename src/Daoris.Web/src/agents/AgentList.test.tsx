import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { AgentStrip } from './AgentList';
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
