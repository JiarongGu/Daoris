import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { ProposalCard } from './ProposalCard';

// HELP1c (D89): a change Ask Daoris proposes, and the person's two presses.

const LANDING = {
  id: 'p1a2b3c4', kind: 'setting' as const,
  describe: 'Land workspace `work`\'s accepted work on a branch `feature/{quest}-{slug}`.',
  terminal: 'daoris driver landing --workspace work branch feature/{quest}-{slug}',
  why: 'the person asked for feature branches',
};

describe('a proposal of Ask Daoris\'s', () => {
  it('says what it changes, the command that does the same, and why', () => {
    render(<ul><ProposalCard proposal={LANDING} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText('feature/{quest}-{slug}', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(LANDING.terminal, { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('Why: the person asked for feature branches')).toBeInTheDocument();
  });

  it('is applied or not by the person, and does neither while a press is on its way', async () => {
    const onApply = vi.fn();
    const onDismiss = vi.fn();
    const { rerender } = render(<ul><ProposalCard proposal={LANDING} onApply={onApply} onDismiss={onDismiss} /></ul>);

    await userEvent.click(screen.getByRole('button', { name: 'apply' }));
    await userEvent.click(screen.getByRole('button', { name: 'not now' }));
    expect(onApply).toHaveBeenCalledWith('p1a2b3c4');
    expect(onDismiss).toHaveBeenCalledWith('p1a2b3c4');

    rerender(<ul><ProposalCard proposal={LANDING} pending onApply={onApply} onDismiss={onDismiss} /></ul>);
    expect(screen.getByRole('button', { name: 'apply' })).toBeDisabled();
  });
});
