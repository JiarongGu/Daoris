import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { AgentRules, type AgentRulesState, type RuleProposal } from './AgentRules';

// A widening a supervisor allowed shows who allowed it, and the person takes it back as they remove their own rule.

const RULES: AgentRulesState = {
  path: 'C:/somewhere/data/permissions.json',
  defaults: [],
  scopes: [{ scope: 'repository', name: 'engine', allow: ['Bash(npm run build:*)'], ask: [], deny: [] }],
};

const ALLOWED: RuleProposal = {
  id: 'p0000003', state: 'accepted', action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(npm run build:*)',
  why: 'It builds.', session: 's1a2b3c4', proposed: '2026-11-08T10:00:00Z', settledBy: 'the supervisor', settledRole: 'allow-keeper',
};

const show = async () => {
  const props = { rules: { ...RULES, proposals: [ALLOWED] }, circles: ['default'], repositories: ['engine'], onSwitchDefault: vi.fn(), onRemove: vi.fn(), onAdd: vi.fn(), onAnswer: vi.fn() };
  render(<Tooltip.Provider><AgentRules {...props} /></Tooltip.Provider>);
  await userEvent.click(screen.getByRole('button', { name: /Earlier proposals|更早的提议/ }));
  return props;
};

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('an allow a supervisor gave', () => {
  it('says which supervisor allowed it and is taken back by removing the rule from its scope', async () => {
    const props = await show();

    const row = screen.getByRole('listitem', { name: 'proposal #p0000003' });
    expect(within(row).getByText(/allowed by the supervisor allow-keeper/)).toBeTruthy();
    await userEvent.click(within(row).getByRole('button', { name: 'Take back the allow of Bash(npm run build:*)' }));

    expect(props.onRemove).toHaveBeenCalledWith({ scope: 'repository', name: 'engine', rule: 'Bash(npm run build:*)' });
  });

  it('says it in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    await show();

    const row = screen.getByRole('listitem', { name: /p0000003/ });
    expect(within(row).getByText(/由监督者 allow-keeper 允许/)).toBeTruthy();
    expect(within(row).getByRole('button', { name: '收回对 Bash(npm run build:*) 的允许' })).toBeTruthy();
  });

  it('offers no take-back for what the person settled or a supervisor declined', async () => {
    const props = { rules: { ...RULES, proposals: [{ ...ALLOWED, settledBy: 'the person', settledRole: undefined }] }, circles: ['default'], repositories: ['engine'], onSwitchDefault: vi.fn(), onRemove: vi.fn(), onAdd: vi.fn(), onAnswer: vi.fn() };
    render(<Tooltip.Provider><AgentRules {...props} /></Tooltip.Provider>);
    await userEvent.click(screen.getByRole('button', { name: 'Earlier proposals (1)' }));

    expect(screen.queryByRole('button', { name: /Take back/ })).toBeNull();
  });
});
