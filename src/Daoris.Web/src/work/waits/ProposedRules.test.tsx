import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { ProposedRules } from './ProposedRules';

// PROPOSEHERE1b (D168 point 2, D74): the rules a session proposed, answered where the person talks to it. *Allow…* asks once,
// since it widens what agents may do; *Decline* is direct. Props only: the frame holds the answer and its pending.

const proposal = (over: Partial<RuleProposal> = {}): RuleProposal => ({
  id: 'r1', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(git branch -D:*)',
  why: 'Auto mode refused the branch delete you asked for.', session: 's1a2b3c4', proposed: '2026-10-15T10:20:00Z', ...over,
});

const answered = { done: expect.any(Function), refused: expect.any(Function) };

describe('the rules a session proposed', () => {
  it('shows each with what it would change and why, Allow… and Decline', () => {
    render(<ProposedRules proposals={[proposal()]} onAnswer={vi.fn()} />);
    const item = screen.getByRole('listitem');
    expect(item).toHaveTextContent('allow Bash(git branch -D:*) for repository engine');
    expect(item).toHaveTextContent('Auto mode refused the branch delete you asked for.');
    expect(within(item).getByRole('button', { name: 'Allow…' })).toBeInTheDocument();
    expect(within(item).getByRole('button', { name: 'Decline' })).toBeInTheDocument();
  });

  it('declines with one press', async () => {
    const onAnswer = vi.fn();
    render(<ProposedRules proposals={[proposal()]} onAnswer={onAnswer} />);
    await userEvent.click(screen.getByRole('button', { name: 'Decline' }));
    expect(onAnswer).toHaveBeenCalledWith(proposal(), false, answered);
  });

  it('asks once before allowing, saying what it widens and that the session is told', async () => {
    const onAnswer = vi.fn((_proposal: RuleProposal, _accept: boolean, told: { done: () => void }) => told.done());
    render(<ProposedRules proposals={[proposal()]} onAnswer={onAnswer} />);

    await userEvent.click(screen.getByRole('button', { name: 'Allow…' }));
    expect(onAnswer).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'Allow…' });
    expect(ask).toHaveTextContent('It widens what agents may do: allow Bash(git branch -D:*) for repository engine.');
    expect(ask).toHaveTextContent('Once it is allowed, this session is told so and goes on.');

    await userEvent.click(within(ask).getByRole('button', { name: 'Allow' }));
    expect(onAnswer).toHaveBeenCalledWith(proposal(), true, answered);
    expect(screen.queryByRole('group', { name: 'Allow…' })).toBeNull();
  });

  it('says on the card where the session takes no words, so allowing it does not tell it', async () => {
    render(<ProposedRules proposals={[proposal()]} onAnswer={vi.fn()} heard={false} />);
    expect(screen.getByRole('listitem')).toHaveTextContent('It takes no words now, so it is not told; a session started from now on is handed the rule.');
    await userEvent.click(screen.getByRole('button', { name: 'Allow…' }));
    expect(screen.getByRole('group', { name: 'Allow…' })).not.toHaveTextContent('this session is told so');
  });

  it('says a refused decline beside it, and keeps both presses', async () => {
    render(<ProposedRules proposals={[proposal()]} onAnswer={(_p, _a, told) => told.refused('No proposal r1 waits.')} />);
    await userEvent.click(screen.getByRole('button', { name: 'Decline' }));
    expect(screen.getByRole('alert')).toHaveTextContent('No proposal r1 waits.');
    expect(screen.getByRole('button', { name: 'Decline' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Allow…' })).toBeEnabled();
  });

  it('opens its ask where the frame holds it, and holds both presses while an answer is on its way', async () => {
    const onOpen = vi.fn();
    const props = { proposals: [proposal()], onAnswer: vi.fn() };
    const { rerender } = render(<ProposedRules {...props} ask={{ open: null, onOpen }} />);
    await userEvent.click(screen.getByRole('button', { name: 'Allow…' }));
    expect(onOpen).toHaveBeenCalledWith('r1');
    expect(screen.queryByRole('group', { name: 'Allow…' })).toBeNull();

    rerender(<ProposedRules {...props} ask={{ open: 'r1', onOpen }} />);
    expect(screen.getByRole('group', { name: 'Allow…' })).toBeInTheDocument();

    rerender(<ProposedRules {...props} ask={{ open: null, onOpen }} busy />);
    expect(screen.getByRole('button', { name: 'Allow…' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Decline' })).toBeDisabled();
  });

  it('leads to its call in the conversation where one was found, and is outlined where a block led to it', async () => {
    const r2 = proposal({ id: 'r2', rule: 'Bash(git worktree remove:*)' });
    const inConversation = { r1: vi.fn() };
    render(<ProposedRules proposals={[proposal(), r2]} onAnswer={vi.fn()} inConversation={inConversation} marked="r2" />);
    const [first, second] = screen.getAllByRole('listitem');
    await userEvent.click(within(first!).getByRole('button', { name: 'In the conversation' }));
    expect(inConversation.r1).toHaveBeenCalledTimes(1);
    expect(within(second!).queryByRole('button', { name: 'In the conversation' })).toBeNull();
    expect(second!.className).toContain('outline-accent');
    expect(second).toHaveAttribute('data-rule', 'r2');
  });

  it('offers no press where nothing can answer it', () => {
    render(<ProposedRules proposals={[proposal()]} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('speaks the active catalogue', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<ProposedRules proposals={[proposal()]} onAnswer={vi.fn()} heard={false} />);
      expect(screen.getByRole('button', { name: '允许…' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '谢绝' })).toBeInTheDocument();
      expect(screen.getByRole('listitem')).toHaveTextContent('它现在不接收消息');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
