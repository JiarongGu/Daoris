import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import { AskPanel } from './AskPanel';
import { starters } from './starters';

// HELP1d (D89): Ask Daoris's panel — what the machine lacks, each with its door and its command.

const LACKING = starters({
  repositories: ['engine'], drivable: [], tools: [], waiting: 1, unnamedLines: [], helper: null,
});

describe('Ask Daoris', () => {
  it('lists what the machine lacks, each with the command that does the same', () => {
    render(<AskPanel starters={LACKING} helper={null} onGo={vi.fn()} onClose={vi.fn()} />);

    const panel = screen.getByRole('complementary', { name: 'Ask Daoris' });
    expect(within(panel).getByText('1 session is waiting on you.')).toBeInTheDocument();
    expect(within(panel).getByText(/No repository is driven on this machine/)).toBeInTheDocument();
    expect(within(panel).getByText('daoris driver drive <repository>', { selector: 'code' })).toBeInTheDocument();
    expect(within(panel).getByText(/Name an agent under Daoris's own AI/)).toBeInTheDocument();
  });

  it('goes to the screen that fixes a starter, and closes', async () => {
    const onGo = vi.fn();
    const onClose = vi.fn();
    render(<AskPanel starters={LACKING} helper={null} onGo={onGo} onClose={onClose} />);

    await userEvent.click(screen.getByRole('button', { name: "open Daoris's own AI" }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'ai' });
    await userEvent.click(screen.getByRole('button', { name: 'close Ask Daoris' }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('says so when the machine lacks nothing, and names the agent it runs on', () => {
    render(<AskPanel starters={[]} helper="claude-code-acp" onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByText(/lacks nothing Ask Daoris knows to look for/)).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp/)).toBeInTheDocument();
  });
});
