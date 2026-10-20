import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../../locales/i18n';
import { HandOverAgentAsk, type HandOverAskRow } from './HandOverAgentAsk';

// MIXAGENT1c (D163 point 3; the mixed-agents design §2, §5): a session's agent's own ask to hand it over, on its page. Its note is the
// agent's claim, shown as written and said to be its claim; where the look holds it waiting on the person's word, *Hand over to …*
// and *Not now*, each told how it ended, a refusal said beside them in the page's words.

const row = (over: Partial<HandOverAskRow> = {}): HandOverAskRow => ({
  id: 'a1b2c3d4', note: 'Near my limit.\nParserTests are left.', at: '2026-10-20T09:00:00Z', state: 'waiting', code: null,
  agent: 'codex-acp', ...over,
});

const nameOf = (agent: string) => (agent === 'codex-acp' ? 'Codex' : agent);

describe('an agent’s ask to hand its session over', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('shows its note as the agent wrote it, as its claim, with the yes and the no where it waits on the person', async () => {
    const onApprove = vi.fn();
    const onDecline = vi.fn();
    render(<HandOverAgentAsk ask={row()} nameOf={nameOf} onApprove={onApprove} onDecline={onDecline} />);

    expect(screen.getByText(/ParserTests are left\./)).toBeInTheDocument();
    expect(screen.getByText(/its own claim/)).toBeInTheDocument();
    expect(screen.getByText(/would go on with Codex/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Hand over to Codex' }));
    expect(onApprove).toHaveBeenCalledWith('a1b2c3d4', expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
    onApprove.mock.calls[0][1].done();
    await userEvent.click(screen.getByRole('button', { name: 'Not now' }));
    expect(onDecline).toHaveBeenCalledWith('a1b2c3d4', expect.anything());
  });

  it('says a refusal beside the presses', async () => {
    render(<HandOverAgentAsk ask={row()} nameOf={nameOf} onApprove={(_, answered) => answered.refused('It is past your word.')} />);

    await userEvent.click(screen.getByRole('button', { name: 'Hand over to Codex' }));

    expect(screen.getByText('It is past your word.')).toBeInTheDocument();
  });

  it('offers no press where it waits on nothing of the person’s, and nothing at all once it is settled', () => {
    const { rerender, container } = render(<HandOverAgentAsk ask={row({ state: 'approved' })} nameOf={nameOf} onApprove={vi.fn()} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText(/You said yes: it goes on with Codex/)).toBeInTheDocument();

    rerender(<HandOverAgentAsk ask={row({ state: null, agent: null })} nameOf={nameOf} onApprove={vi.fn()} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText(/by your rule for hand-overs/)).toBeInTheDocument();

    rerender(<HandOverAgentAsk ask={row({ state: 'taken' })} nameOf={nameOf} onApprove={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<HandOverAgentAsk ask={row()} nameOf={nameOf} onApprove={vi.fn()} onDecline={vi.fn()} />);

    expect(screen.getByRole('button', { name: '转交给 Codex' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '暂不' })).toBeInTheDocument();
  });
});
