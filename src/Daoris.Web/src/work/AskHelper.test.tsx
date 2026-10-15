import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AskHelper } from './AskHelper';

// MIXAGENT2b (D163 point 2; the mixed-agents design §3): *Ask another agent…*'s ask under the page header, the person's own door
// with the tool's fields: the question, and the agent, left to Daoris unless the person names one.

const roster = {
  harnesses: ['claude-code', 'codex-acp'],
  names: { 'claude-code': 'Claude Code', 'codex-acp': 'Codex' },
};

afterEach(cleanup);

describe('AskHelper', () => {
  it('says what a helper does, and waits for a question before it asks', async () => {
    const ask = vi.fn();
    render(<AskHelper from="claude-code" roster={roster} onAsk={ask} onClose={vi.fn()} />);

    expect(screen.getByText(/Another agent reads this session’s work and answers your question/)).toBeInTheDocument();
    const move = screen.getByRole('button', { name: 'Ask' });
    expect(move).toBeDisabled();

    await userEvent.type(screen.getByRole('textbox', { name: 'Your question' }), '  Which test covers the loader?  ');
    await userEvent.click(move);
    expect(ask).toHaveBeenCalledWith({ question: 'Which test covers the loader?', agent: null }, expect.objectContaining({ done: expect.any(Function) }));
  });

  it('names the agent the person chose, never the session’s own', async () => {
    const ask = vi.fn();
    render(<AskHelper from="claude-code" roster={roster} onAsk={ask} onClose={vi.fn()} />);

    await userEvent.click(screen.getByRole('combobox', { name: 'Which agent' }));
    expect(screen.queryByRole('option', { name: 'Claude Code' })).toBeNull();
    await userEvent.click(screen.getByRole('option', { name: 'Codex' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Your question' }), 'Is the cap right?');
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }));

    expect(ask).toHaveBeenCalledWith({ question: 'Is the cap right?', agent: 'codex-acp' }, expect.anything());
  });

  it('says a refusal inside the ask and keeps the question', async () => {
    const ask = vi.fn((_: unknown, answered: { refused: (sentence: string) => void }) => answered.refused('A helper cannot ask for a helper.'));
    render(<AskHelper from="claude-code" roster={roster} onAsk={ask} onClose={vi.fn()} />);

    await userEvent.type(screen.getByRole('textbox', { name: 'Your question' }), 'Why?');
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }));

    expect(await screen.findByText('A helper cannot ask for a helper.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Your question' })).toHaveValue('Why?');
  });
});
