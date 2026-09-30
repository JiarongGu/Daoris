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
    expect(within(panel).getByText(/Name an agent under AI features/)).toBeInTheDocument();
  });

  it('goes to the screen that fixes a starter, and closes', async () => {
    const onGo = vi.fn();
    const onClose = vi.fn();
    render(<AskPanel starters={LACKING} helper={null} onGo={onGo} onClose={onClose} />);

    await userEvent.click(screen.getByRole('button', { name: "Open AI features" }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'ai' });
    await userEvent.click(screen.getByRole('button', { name: 'Close Ask Daoris' }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  /** In the right dock it is the dock's tab: no frame of its own, no title and no close — the dock has those. */
  it('drops its own frame, title and close when a dock holds it, and keeps starting again', () => {
    render(
      <AskPanel
        framed={false}
        starters={[]}
        helper="claude-code-acp"
        conversation={{ body: <p>the conversation</p>, composer: <p>the box</p>, ended: false, onNew: vi.fn() }}
        onGo={vi.fn()}
        onClose={vi.fn()}
      />,
    );

    expect(screen.queryByRole('complementary')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Close Ask Daoris' })).toBeNull();
    expect(screen.getByRole('button', { name: 'New conversation' })).toBeInTheDocument();
    expect(screen.getByText('the conversation')).toBeInTheDocument();
    expect(screen.getByText('the box')).toBeInTheDocument();
  });

  /** On every other view it is the one right region, resized by its left edge and remembered by the caller. */
  it('is resized by its left edge where it stands alone', async () => {
    const onResize = vi.fn();
    render(<AskPanel starters={[]} helper={null} width={420} range={{ min: 320, max: 800 }} onResize={onResize} onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByRole('complementary', { name: 'Ask Daoris' })).toHaveStyle({ width: '420px' });
    expect(screen.getByRole('separator', { name: 'resize Ask Daoris' })).toBeInTheDocument();
  });

  /**
   * SETUP1a (D97 §2): the starters lead to the setup guide while its required steps are not all done —
   * what is missing in order, beside what is missing now.
   */
  it('leads to the setup guide while setup is not done, and not after', async () => {
    const onGo = vi.fn();
    const { rerender } = render(
      <AskPanel starters={LACKING} helper={null} setup={{ done: 2, of: 5 }} onGo={onGo} onClose={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Set up Daoris step by step: 2 of 5 done' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'start' });

    rerender(<AskPanel starters={LACKING} helper={null} setup={{ done: 5, of: 5 }} onGo={onGo} onClose={vi.fn()} />);
    expect(screen.queryByRole('button', { name: /set up Daoris/ })).toBeNull();
  });

  it('says so when the machine lacks nothing, and names the agent it runs on', () => {
    render(<AskPanel starters={[]} helper="claude-code-acp" onGo={vi.fn()} onClose={vi.fn()} />);

    expect(screen.getByText(/lacks nothing Ask Daoris knows to look for/)).toBeInTheDocument();
    expect(screen.getByText(/runs on claude-code-acp/)).toBeInTheDocument();
  });
});
