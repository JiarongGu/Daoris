import { describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { type HelpProposal, ProposalCard } from './ProposalCard';

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

// HELP6: every door built since, a card of its own kind — each with Apply (or its kind's word for it) and
// Not now, since every one is the person's press.

const UPDATE: HelpProposal = {
  id: 'p2', kind: 'agent',
  describe: 'Update `claude-code-acp`: move its pin from 0.84.0 to the newest release, installed before the pin moves.',
  terminal: 'daoris agent update claude-code-acp',
  why: 'the person asked for the newest',
};

const ACCOUNT: HelpProposal = {
  id: 'p3', kind: 'account',
  describe: 'Set `claude-code` account `work`\'s model to `opus` and its effort to `high`.',
  terminal: 'daoris agent settings claude-code --account work model opus effort high',
  why: 'the person wants it to think harder',
};

const DELETE: HelpProposal = {
  id: 'p4', kind: 'delete',
  describe: 'Delete ask `#a1b2c3d4` “a test ask”, with the quest it became: `#q1a2b3c4` “Cap the chunk budget”.',
  terminal: 'daoris-driver ask --delete a1b2c3d4',
  why: 'it was a test',
};

const GO: HelpProposal = {
  id: 'p5', kind: 'go', describe: 'Open Settings → Get started at step 2, Daoris\'s own agent.', terminal: '',
  why: 'the person asked where to name its agent',
};

const press = async (proposal: HelpProposal, apply: string) => {
  const onApply = vi.fn();
  const onDismiss = vi.fn();
  render(<ul><ProposalCard proposal={proposal} onApply={onApply} onDismiss={onDismiss} /></ul>);
  await userEvent.click(screen.getByRole('button', { name: apply }));
  await userEvent.click(screen.getByRole('button', { name: 'not now' }));
  expect(onApply).toHaveBeenCalledWith(proposal.id);
  expect(onDismiss).toHaveBeenCalledWith(proposal.id);
};

describe('the kinds that reach every door', () => {
  it('an agent\'s update says what moves and the command, with Apply and Not now', async () => {
    await press(UPDATE, 'apply');

    expect(screen.getByText('Ask Daoris proposes')).toBeInTheDocument();
    expect(screen.getByText('claude-code-acp', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(UPDATE.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('an account\'s model and effort say the values in the tool\'s own words, with Apply and Not now', async () => {
    await press(ACCOUNT, 'apply');

    expect(screen.getByText('opus', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText('high', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(ACCOUNT.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('a delete says what goes and that it cannot be undone, and its Apply is a delete', async () => {
    await press(DELETE, 'delete');

    expect(screen.getByText('Ask Daoris proposes a delete')).toBeInTheDocument();
    expect(screen.getByText('#q1a2b3c4', { selector: 'code' })).toBeInTheDocument();
    expect(screen.getByText(/cannot be undone/)).toBeInTheDocument();
    expect(screen.getByText(DELETE.terminal, { selector: 'code' })).toBeInTheDocument();
  });

  it('a go names the place, carries no command since it changes nothing, and its Apply is a go', async () => {
    await press(GO, 'go there');

    expect(screen.getByText('Ask Daoris suggests a screen')).toBeInTheDocument();
    expect(screen.getByText(GO.describe)).toBeInTheDocument();
    expect(screen.getByText(/changes nothing/)).toBeInTheDocument();
    expect(screen.queryByText(/the same at a terminal/)).not.toBeInTheDocument();
  });

  it('speaks 中文 for every kind, the driver\'s sentence left as it said it', async () => {
    const { default: i18n } = await import('../i18n');
    await i18n.changeLanguage('zh');
    try {
      render(<ul><ProposalCard proposal={DELETE} onApply={vi.fn()} onDismiss={vi.fn()} /><ProposalCard proposal={GO} onApply={vi.fn()} onDismiss={vi.fn()} /></ul>);
      expect(screen.getByText('问道衍提议删除')).toBeInTheDocument();
      expect(screen.getByText('问道衍建议打开一个界面')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '删除' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '前往' })).toBeInTheDocument();
      expect(screen.getByText(GO.describe)).toBeInTheDocument();
    } finally {
      cleanup();
      await i18n.changeLanguage('en');
    }
  });
});
