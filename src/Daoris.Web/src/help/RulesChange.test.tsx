import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import { type HelpProposal, ProposalCard } from './ProposalCard';

// CONTEXTDRIVE1k: the card of a place's supervisor rules is worded from the catalogue, not carried as the driver's English sentence.

const RULES: HelpProposal = {
  id: 'p30', kind: 'role', describe: 'Supervisor, repository:daoris: none here → answers-alone at 3: hold for build.',
  terminal: 'daoris role rules --place repository:daoris --clear all --set answers-alone=3:hold@build', why: 'the person asked',
  rules: {
    place: 'repository:daoris',
    before: [],
    after: [{ count: 'answers-alone', at: 3, do: 'hold', roles: ['build'] }, { count: 'limits', at: 1, do: 'wait', upTo: 30, roles: null }],
  },
};

describe("the card of a place's supervisor rules", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage('en');
  });

  it("words the place and each rule from the catalogue in English, not the driver's sentence", () => {
    render(<ul><ProposalCard proposal={RULES} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Supervisor: its rules for Repository daoris')).toBeInTheDocument();
    expect(screen.queryByText(/answers-alone at 3/)).toBeNull();
    expect(screen.getByText('No rules of its own here')).toBeInTheDocument();
    const after = within(screen.getByRole('region', { name: 'After Apply' }));
    expect(after.getByText('Answers alone in a row, at 3: Wait for you, for build')).toBeInTheDocument();
    expect(after.getByText('It reaches its limit, at 1: Wait for it up to 30 minutes, for Every role')).toBeInTheDocument();
    expect(screen.getByText(code(RULES.terminal))).toBeInTheDocument();
  });

  it('words them in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<ul><ProposalCard proposal={RULES} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
    expect(screen.getByText('主管：仓库 daoris的规则')).toBeInTheDocument();
    expect(screen.queryByText(/answers-alone at 3/)).toBeNull();
  });
});
