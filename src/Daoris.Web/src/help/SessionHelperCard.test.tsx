import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type HelpProposal, ProposalCard } from './ProposalCard';
import { sessionHelperDoor } from './sessionHelperDoor';

// COWORKUX1: the helper and pass doors of a `session` proposal, drawn from the command the driver wrote for them.
const HELPER: HelpProposal = {
  id: 'h1', kind: 'session', why: 'the person wants a second look',
  describe: 'Ask agent `codex` a question about session `s1`.',
  terminal: 'daoris session helper s1 "does the retry loop stop?" --agent codex --after a7 --to-me',
};
const PASS: HelpProposal = {
  id: 'h2', kind: 'session', why: 'the answer is good', describe: 'Pass the answer to ask `#a7` to session `s1`.',
  terminal: 'daoris session pass a7',
};

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('a session helper or pass proposal', () => {
  it('reads the question, agent, earlier ask and whose answer it is from the command', () => {
    expect(sessionHelperDoor(HELPER.terminal)).toEqual({
      door: 'helper', session: 's1', question: 'does the retry loop stop?', agent: 'codex', after: 'a7', toMe: true,
    });
    expect(sessionHelperDoor('daoris session helper s1 "why \\"x\\"?"')).toMatchObject({ question: 'why \\"x\\"?', agent: null, after: null, toMe: false });
    expect(sessionHelperDoor(PASS.terminal)).toEqual({ door: 'pass', ask: 'a7' });
    expect(sessionHelperDoor('daoris session archive s1 s2')).toBeNull();
  });

  it('draws the helper ask with its own title and lines, and applies on the press', async () => {
    const onApply = vi.fn();
    render(<ul><ProposalCard proposal={HELPER} onApply={onApply} onDismiss={vi.fn()} /></ul>);

    expect(screen.getByText('Ask Daoris proposes asking another agent')).toBeInTheDocument();
    expect(screen.getByText(/does the retry loop stop\?/)).toBeInTheDocument();
    expect(screen.getByText(/Agent:/)).toHaveTextContent('codex');
    expect(screen.getByText(/Asks again after:/)).toHaveTextContent('#a7');
    expect(screen.getByText(/Its answer:/)).toHaveTextContent('comes to you first');
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));
    expect(onApply).toHaveBeenCalledWith('h1');
  });

  it('draws the pass in Chinese, and leaves out the agent line it was not given', async () => {
    await i18n.changeLanguage('zh');
    const plain = { ...HELPER, terminal: 'daoris session helper s1 "q?"' };
    const onApply = vi.fn();
    const { unmount } = render(<ul><ProposalCard proposal={plain} onApply={onApply} onDismiss={vi.fn()} /></ul>);
    expect(screen.getByText('问道衍提议请另一个智能体帮忙')).toBeInTheDocument();
    expect(screen.getByText(/智能体:/)).toHaveTextContent('由道衍挑选');
    expect(screen.queryByText(/接着这一问再问/)).toBeNull();
    unmount();

    render(<ul><ProposalCard proposal={PASS} onApply={onApply} onDismiss={vi.fn()} /></ul>);
    expect(screen.getByText('问道衍提议把答复转给主导')).toBeInTheDocument();
    expect(screen.getByText(/答复所属的提问:/)).toHaveTextContent('#a7');
    await userEvent.click(screen.getByRole('button', { name: '应用' }));
    expect(onApply).toHaveBeenCalledWith('h2');
  });
});
