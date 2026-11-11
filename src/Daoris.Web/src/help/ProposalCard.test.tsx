import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type HelpProposal, ProposalCard } from './ProposalCard';

// A screen Ask Daoris proposes, and the person's two presses.

const GO: HelpProposal = {
  id: 'p5', kind: 'go', describe: 'Open Settings → Get started at step 2, Ask Daoris\'s agent.', terminal: '',
  why: 'the person asked where to name its agent',
};

const press = async (proposal: HelpProposal) => {
  const onApply = vi.fn();
  const onDismiss = vi.fn();
  render(<ul><ProposalCard proposal={proposal} onApply={onApply} onDismiss={onDismiss} /></ul>);
  await userEvent.click(screen.getByRole('button', { name: 'Go there' }));
  await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
  expect(onApply).toHaveBeenCalledWith(proposal.id);
  expect(onDismiss).toHaveBeenCalledWith(proposal.id);
};

describe('a go card', () => {
  it('names the place, says why, carries no command since it changes nothing, and its Apply is a go', async () => {
    await press(GO);

    expect(screen.getByText('Ask Daoris suggests a screen')).toBeInTheDocument();
    expect(screen.getByText(GO.describe)).toBeInTheDocument();
    expect(screen.getByText('Why: the person asked where to name its agent')).toBeInTheDocument();
    expect(screen.getByText(/changes nothing/)).toBeInTheDocument();
    expect(screen.queryByText(/the same at a terminal/)).not.toBeInTheDocument();
  });

  it('does neither press while one is on its way', () => {
    render(<ul><ProposalCard proposal={GO} pending onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByRole('button', { name: 'Go there' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Dismiss' })).toBeDisabled();
  });

  it('a go to a taken quest says it is taken, and its second press is a dismiss, no act on the quest', async () => {
    const taken: HelpProposal = {
      id: 'g6t7k8n9', kind: 'go', terminal: '', why: 'the person asked where their quest went',
      describe: 'Open Quests → quest `#q2taken0` “Stream the tiles”, taken.',
    };
    await press(taken);

    expect(screen.getByText(/, taken\.$/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Not now' })).not.toBeInTheDocument();
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Go there', 'Dismiss']);
  });

  it('speaks 中文, the driver\'s sentence left as it said it', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={GO} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍建议打开一个界面')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '前往' })).toBeInTheDocument();
      // The go card's dismiss takes it out of the list, as a notice's does.
      expect(screen.getByRole('button', { name: '移除' })).toBeInTheDocument();
      expect(screen.getByText(GO.describe)).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
