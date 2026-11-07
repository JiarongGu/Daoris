import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { HelperAskRow } from '../waits/HelperAsks';
import { BringInAgent } from './BringInAgent';
import { CoWorkers } from './CoWorkers';
import { MemberThread } from './MemberThread';

// The head's *Co-workers* with its presses, *Bring in another agent…*, a member's thread
// with *Back to lead*, and talking to a member as asking again: *Ask again…* and *Pass to the lead*, in both languages.

const row = (over: Partial<HelperAskRow> & Pick<HelperAskRow, 'id'>): HelperAskRow => ({
  question: 'Why does the list flicker?', agent: 'codex-acp', askedBy: 'person', at: '2026-11-06T09:00:00Z', state: null, code: null,
  helper: null, startedOn: null, ...over,
});

const nameOf = (agent: string) => (agent === 'codex-acp' ? 'Codex' : agent);

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const memberLine = (id: string) => within(document.querySelector(`[data-member="${id}"]`) as HTMLElement);

describe("the head's Co-workers", () => {
  it('draws the lead, each member by its state and where its answer went, and opens its thread', async () => {
    const onOpen = vi.fn();
    render(
      <CoWorkers
        session="s1"
        lead="Claude Code"
        helpers={{
          nameOf,
          onOpen,
          asks: [
            row({ id: 'a1', state: 'started', helper: 'h1', startedOn: 'codex-acp' }),
            row({ id: 'a2', state: 'answered', helper: 'h2', startedOn: 'codex-acp' }),
            row({ id: 'a3', state: 'answered', helper: 'h3', startedOn: 'codex-acp', to: 'person', kept: true }),
          ],
        }}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Co-workers' })).toBeTruthy();
    expect(screen.getByText('Lead · Claude Code')).toBeTruthy();
    expect(memberLine('a1').getByRole('button', { name: 'Codex · helper · working' })).toBeTruthy();
    expect(memberLine('a2').getByText('Codex · helper · answered · goes to the lead')).toBeTruthy();
    expect(memberLine('a3').getByText('Codex · helper · answered · to you')).toBeTruthy();
    await userEvent.click(memberLine('a1').getByRole('button'));
    expect(onOpen).toHaveBeenCalledWith('h1');
  });

  it('offers Pass to the lead only on an answer kept for the person, and says a refusal where it was pressed', async () => {
    const onPass = vi.fn((_ask: string, answered: { refused: (s: string) => void }) => answered.refused('It has not answered yet.'));
    render(
      <CoWorkers
        session="s1"
        lead="Claude Code"
        onPass={onPass}
        helpers={{
          nameOf,
          asks: [
            row({ id: 'a2', state: 'answered', helper: 'h2' }),
            row({ id: 'a3', state: 'answered', helper: 'h3', to: 'person', kept: true }),
            row({ id: 'a4', state: 'answered', helper: 'h4', to: 'person', passed: true }),
          ],
        }}
      />,
    );

    expect(memberLine('a2').queryByRole('button', { name: 'Pass to the lead' })).toBeNull();
    expect(memberLine('a4').getByText('Codex · helper · answered · passed to the lead')).toBeTruthy();
    await userEvent.click(memberLine('a3').getByRole('button', { name: 'Pass to the lead' }));
    expect(onPass.mock.calls[0]![0]).toBe('a3');
    expect(memberLine('a3').getByRole('alert').textContent).toBe('It has not answered yet.');
  });

  it("keeps an agent's ask that waits on the person's yes with its presses and marks its wait's section", () => {
    render(
      <CoWorkers
        session="s1"
        lead="Claude Code"
        helpers={{ nameOf, onApprove: vi.fn(), onDecline: vi.fn(), asks: [row({ id: 'w1', askedBy: 'agent', state: 'waiting', agent: null })] }}
      />,
    );

    const section = document.querySelector('[data-coworkers]')!;
    expect(section.hasAttribute('data-wait-section')).toBe(true);
    expect(screen.getByText('Why does the list flicker?')).toBeTruthy();
  });

  it('draws nothing for a session alone with no door to bring one in', () => {
    const { container } = render(<CoWorkers session="s1" lead="Claude Code" helpers={{ nameOf, asks: [] }} />);
    expect(container.innerHTML).toBe('');
  });

  it('says its words in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(
      <CoWorkers
        session="s1"
        lead="Claude Code"
        onPass={vi.fn()}
        helpers={{ nameOf, asks: [row({ id: 'a3', state: 'answered', helper: 'h3', to: 'person', kept: true })] }}
      />,
    );

    expect(screen.getByRole('heading', { name: '同事' })).toBeTruthy();
    expect(screen.getByText('主导 · Claude Code')).toBeTruthy();
    expect(screen.getByRole('button', { name: '转给主导' })).toBeTruthy();
    expect(memberLine('a3').getByText('Codex · 助手 · 已答复 · 给你')).toBeTruthy();
  });
});

describe('Bring in another agent…', () => {
  it("lists the structures' doors in order, opens the chosen one, and shows one the rules turn off with its reason", async () => {
    const question = vi.fn();
    const onClose = vi.fn();
    render(
      <BringInAgent
        onClose={onClose}
        choices={[
          { door: 'opinion', off: 'Second opinions are off for this repository.' },
          { door: 'question', onChoose: question },
          { door: 'piece' },
        ]}
      />,
    );

    const doors = [...document.querySelectorAll('[data-door]')].map((door) => door.getAttribute('data-door'));
    expect(doors).toEqual(['question', 'opinion']);
    expect((screen.getByRole('button', { name: 'Second opinion' }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText('Second opinions are off for this repository.')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Answer a question' }));
    expect(question).toHaveBeenCalledOnce();
    expect(onClose).toHaveBeenCalled();
  });
});

describe("a member's thread", () => {
  it('while it works says it takes one turn, with Back to lead and no Ask again', async () => {
    const onBack = vi.fn();
    render(<MemberThread lead="Fix the loader" ask={row({ id: 'a1', state: 'started', helper: 'h1' })} working onBack={onBack} onAskAgain={vi.fn()} />);

    expect(screen.getByText('Working for Fix the loader')).toBeTruthy();
    expect(screen.getByText('A co-worker takes one turn; ask it again once it has answered.')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Ask again…' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Back to lead' }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('once answered to the person says so, passes it, and asks again for the person unless the lead is chosen', async () => {
    const onPass = vi.fn((answered: { done: () => void }) => answered.done());
    const onAskAgain = vi.fn((_asked: unknown, answered: { done: () => void }) => answered.done());
    render(
      <MemberThread
        lead="Fix the loader"
        ask={row({ id: 'a3', state: 'answered', helper: 'h3', to: 'person', kept: true })}
        working={false}
        onPass={onPass}
        onAskAgain={onAskAgain}
      />,
    );

    expect(screen.getByText('Its answer came to you; it reaches Fix the loader only when you pass it to the lead.')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Pass to the lead' }));
    expect(onPass).toHaveBeenCalledOnce();
    await userEvent.click(screen.getByRole('button', { name: 'Ask again…' }));
    await userEvent.type(screen.getByLabelText('Your question'), 'And on resize?');
    await userEvent.click(screen.getByRole('button', { name: 'Ask' }));
    expect(onAskAgain.mock.calls[0]![0]).toEqual({ question: 'And on resize?', forLead: false });
    expect(screen.queryByLabelText('Your question')).toBeNull();
  });

  it('says where an answer to the lead went, in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<MemberThread lead="修加载器" ask={row({ id: 'a2', state: 'answered', helper: 'h2' })} working={false} onBack={vi.fn()} />);

    expect(screen.getByText('为「修加载器」工作')).toBeTruthy();
    expect(screen.getByText('它的回答作为「修加载器」的下一轮送达。')).toBeTruthy();
    expect(screen.getByRole('button', { name: '返回主导' })).toBeTruthy();
  });
});
