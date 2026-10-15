import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { type HelperAskRow, HelperAsks, openAsks } from './HelperAsks';

// MIXAGENT2b (D163 points 2 and 3; the mixed-agents design §3, §5): the helpers a session asked for, on its page, each its question
// as written, who asked and where the driver's look left it; the person's yes to an agent's ask that waits on it, and a door to
// each helper once started. A molecule: every state is reached by passing it.

const row = (over: Partial<HelperAskRow> & Pick<HelperAskRow, 'id'>): HelperAskRow => ({
  question: 'Which test covers the loader?', agent: null, askedBy: 'agent', at: '2026-10-16T09:00:00Z', state: null, code: null,
  helper: null, startedOn: null, ...over,
});

const NAMES: Record<string, string> = { 'codex-acp': 'Codex', 'claude-code': 'Claude Code' };
const nameOf = (agent: string) => NAMES[agent] ?? agent;

afterEach(cleanup);

const lineOf = (question: string) => within(screen.getByText(question).closest('li')!);

describe('the helpers a session asked for', () => {
  it('says each ask as written, who asked and where it stands', () => {
    render(
      <HelperAsks
        nameOf={nameOf}
        asks={[
          row({ id: 'a1', question: 'Mine, not looked at yet.', askedBy: 'person', agent: 'codex-acp' }),
          row({ id: 'a2', question: 'Waits on a slot.', state: 'waiting', code: 'cap' }),
          row({ id: 'a3', question: 'Waits on an agent.', state: 'waiting', code: 'cooling' }),
          row({ id: 'a4', question: 'Being answered.', state: 'started', helper: 'h1', startedOn: 'codex-acp' }),
          row({ id: 'a5', question: 'Answered.', state: 'answered', helper: 'h2', startedOn: 'claude-code' }),
          row({ id: 'a6', question: 'Refused.', state: 'refused', code: 'too-many' }),
          row({ id: 'a7', question: 'Refused by a word this page does not know.', state: 'refused', code: 'newer-code' }),
          row({ id: 'a8', question: 'Approved.', state: 'approved' }),
        ]}
      />,
    );

    expect(lineOf('Mine, not looked at yet.').getByText('You asked · of Codex · starts at the driver’s next look')).toBeInTheDocument();
    expect(lineOf('Waits on a slot.').getByText('The agent asked · waits for a running session to end')).toBeInTheDocument();
    expect(lineOf('Waits on an agent.').getByText('The agent asked · waits for an agent that can take it')).toBeInTheDocument();
    expect(lineOf('Being answered.').getByText('The agent asked · Codex is answering')).toBeInTheDocument();
    expect(lineOf('Answered.').getByText('The agent asked · Claude Code answered: its answer is this session’s next turn')).toBeInTheDocument();
    expect(lineOf('Refused.').getByText('The agent asked · no helper answered: that session already has two helpers at work')).toBeInTheDocument();
    expect(lineOf('Refused by a word this page does not know.').getByText('The agent asked · no helper answered: newer-code')).toBeInTheDocument();
    expect(lineOf('Approved.').getByText('The agent asked · you said yes · starts at the driver’s next look')).toBeInTheDocument();
  });

  it('offers the person’s yes only on an agent’s ask that waits on it, and tells the press how it ended', async () => {
    const approve = vi.fn();
    render(
      <HelperAsks
        nameOf={nameOf}
        onApprove={approve}
        asks={[row({ id: 'a1', question: 'Is the cap right?', state: 'waiting' }), row({ id: 'a2', question: 'Not yet looked at.' }),
          row({ id: 'a3', question: 'Mine.', askedBy: 'person' })]}
      />,
    );

    expect(lineOf('Is the cap right?').getByText('The agent asked · waits for your yes')).toBeInTheDocument();
    expect(lineOf('Mine.').queryByRole('button')).toBeNull();
    await userEvent.click(lineOf('Is the cap right?').getByRole('button', { name: 'Let it ask' }));
    expect(approve).toHaveBeenCalledWith('a1', expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));

    // Refused, the sentence stands beside the press, which may be pressed again.
    const [, answered] = approve.mock.calls[0]!;
    answered.refused('It waits on cap, not on your yes.');
    expect(await lineOf('Is the cap right?').findByText('It waits on cap, not on your yes.')).toBeInTheDocument();
    // An agent's ask the look has not read yet waits on the yes all the same, under the rule's `ask`.
    expect(lineOf('Not yet looked at.').getByRole('button', { name: 'Let it ask' })).toBeInTheDocument();
  });

  /**
   * HELPERNO1 (D163 point 3): the person's no beside the yes, on an agent's ask the driver's look holds waiting on their word. One
   * the look has not read yet takes the yes only, since a no there could cross a start under the rule's `on`.
   */
  it('offers the person’s no beside the yes on an agent’s ask that waits on their word, and tells the press how it ended', async () => {
    const decline = vi.fn();
    render(
      <HelperAsks
        nameOf={nameOf}
        onApprove={vi.fn()}
        onDecline={decline}
        asks={[row({ id: 'a1', question: 'Is the cap right?', state: 'waiting' }), row({ id: 'a2', question: 'Not yet looked at.' }),
          row({ id: 'a3', question: 'Mine.', askedBy: 'person' })]}
      />,
    );

    expect(lineOf('Not yet looked at.').getByRole('button', { name: 'Let it ask' })).toBeInTheDocument();
    expect(lineOf('Not yet looked at.').queryByRole('button', { name: 'Not now' })).toBeNull();
    expect(lineOf('Mine.').queryByRole('button')).toBeNull();
    await userEvent.click(lineOf('Is the cap right?').getByRole('button', { name: 'Not now' }));
    expect(decline).toHaveBeenCalledWith('a1', expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
    // On its way, both presses wait for it.
    expect(lineOf('Is the cap right?').getByRole('button', { name: 'Let it ask' })).toBeDisabled();

    const [, answered] = decline.mock.calls[0]!;
    answered.refused('It has your yes already: the driver starts it at its next look.');
    expect(await lineOf('Is the cap right?').findByText('It has your yes already: the driver starts it at its next look.')).toBeInTheDocument();
    expect(lineOf('Is the cap right?').getByRole('button', { name: 'Not now' })).toBeEnabled();
  });

  it('says a declined ask as the person’s no', () => {
    render(<HelperAsks nameOf={nameOf} asks={[row({ id: 'a1', question: 'Declined.', state: 'refused', code: 'declined' })]} />);
    expect(lineOf('Declined.').getByText('The agent asked · no helper answered: you said no to it')).toBeInTheDocument();
  });

  /** A settled ask is said where it settled: the answer in the conversation, a refusal in the timeline. */
  it('keeps on the page only the asks still on their way', () => {
    const asks = ['waiting', 'approved', 'started', 'answered', 'refused', null].map((state, at) => row({ id: `a${at}`, state }));
    expect(openAsks(asks).map((each) => each.state)).toEqual(['waiting', 'approved', 'started', null]);
  });

  it('opens each helper once it started', async () => {
    const open = vi.fn();
    render(<HelperAsks nameOf={nameOf} onOpen={open} asks={[row({ id: 'a1', state: 'answered', helper: 'h1', startedOn: 'codex-acp' })]} />);

    await userEvent.click(screen.getByRole('button', { name: 'Open helper' }));
    expect(open).toHaveBeenCalledWith('h1');
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<HelperAsks nameOf={nameOf} onApprove={vi.fn()} onDecline={vi.fn()} asks={[row({ id: 'a1', question: '缓存上限对吗？', state: 'waiting' })]} />);
      expect(lineOf('缓存上限对吗？').getByText('智能体问的 · 等你同意')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '让它问' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: '暂不' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
