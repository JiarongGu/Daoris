import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import { HandOverAsk } from './HandOverAsk';

// MIXAGENT1b-iii (D163): the hand-over's ask lists the offer's candidates, other tools, first, and still lets the person choose
// anything the roster has; with no choice made it opens on the first of them.
const roster = {
  harnesses: ['claude-code', 'claude-code-acp', 'codex'],
  names: { 'claude-code': 'Claude Code', 'claude-code-acp': 'Claude Code (protocol)', codex: 'Codex' },
  accounts: { 'claude-code': [], 'claude-code-acp': [], codex: [] },
};

afterEach(cleanup);

describe('HandOverAsk', () => {
  it('opens on the first other tool when the offer names them first, leaving the session’s own tool’s other door a choice', () => {
    render(<HandOverAsk from="claude-code-acp" first={['codex']} roster={roster} onHandOver={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Agent and account/ }).textContent).toContain('Codex');
  });

  it('opens on the first other agent in the roster’s order where none is named first', () => {
    render(<HandOverAsk from="claude-code-acp" roster={roster} onHandOver={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('button', { name: /Agent and account/ }).textContent).toContain('Claude Code');
  });
});
