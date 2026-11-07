import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { code } from '../../test/code';
import { type HelpProposal, ProposalCard } from '../ProposalCard';

// The card of a supervisor is worded from the catalogue, not carried as the driver's English sentence.

const GATEKEEPER: HelpProposal = {
  id: 'p31', kind: 'role', describe: 'Set the supervisor `gatekeeper` (Gatekeeper): it decides allow, landing in workspace `w1`, held by a terminal.',
  terminal: 'daoris role set gatekeeper --name "Gatekeeper" --scope workspace:w1 --decides allow,landing --acts allow.once --holder terminal --window 10',
  why: 'the person wants allows decided here',
  supervisor: {
    id: 'gatekeeper', names: ['Gatekeeper', '守门人'], scope: { kind: 'workspace', id: 'w1' },
    hold: { kind: 'supervisor', acts: ['allow.once'], holder: 'terminal', window: 10, decides: ['allow', 'landing'] },
    before: null,
  },
};

const GENERIC: HelpProposal = {
  ...GATEKEEPER, id: 'p32',
  supervisor: {
    id: 'wide', names: ['Wide'], scope: { kind: 'machine', id: null },
    hold: { kind: 'supervisor', acts: [], holder: 'rules', window: 1, decides: [] },
    before: { kind: 'supervisor', acts: ['question.answer'], holder: 'terminal', window: 5, decides: [] },
  },
};

describe('the card of a supervisor', () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it("words its kinds, holder, acts and window from the catalogue in English, not the driver's sentence", () => {
    render(<ul><ProposalCard proposal={GATEKEEPER} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Supervisor: Gatekeeper · 守门人, for Workspace w1')).toBeInTheDocument();
    expect(screen.queryByText(/it decides allow, landing/)).toBeNull();
    expect(screen.getByText('Not set here yet')).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After Apply' }));
    expect(after.getByText(
      'Decides: Allows and Landings · Held by a terminal · allow a call once · a decision is held 10 minutes for your overrule',
    )).toBeInTheDocument();
    expect(screen.getByText(code(GATEKEEPER.terminal))).toBeInTheDocument();
  });

  it('says a generic supervisor as every decision, and what it was before', () => {
    render(<ul><ProposalCard proposal={GENERIC} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    const now = within(screen.getByRole('region', { name: 'Now' }));
    expect(now.getByText(
      'Decides: Every decision no other supervisor here takes · Held by a terminal · answer a question · a decision is held 5 minutes for your overrule',
    )).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After Apply' }));
    expect(after.getByText(
      'Decides: Every decision no other supervisor here takes · Held by your rules, then you · no acts · a decision is held 1 minute for your overrule',
    )).toBeInTheDocument();
  });

  it('words it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<ul><ProposalCard proposal={GATEKEEPER} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('主管：Gatekeeper · 守门人，适用于工作区 w1')).toBeInTheDocument();
    expect(screen.queryByText(/it decides allow/)).toBeNull();
    const after = within(screen.getByRole('region', { name: '应用之后' }));
    expect(after.getByText('决定：允许和落地 · 担任者：终端 · 允许这一次调用 · 决定会保留 10 分钟，供你否决')).toBeInTheDocument();
  });
});
