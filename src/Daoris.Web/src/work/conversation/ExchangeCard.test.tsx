import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { toTurns } from './conversation';
import { ConversationView } from './ConversationView';
import { exchangesOf } from './exchanges';
import { ANSWER, askedEvents, askRow, nameOf, QUESTION } from './exchanges.fixtures';

/**
 * COMMS1h (the comms design §7): a message is drawn whole once, at its home. An agent's ask for a helper is its card at the
 * `helper_ask` call, never a tool card folded in a run; its answer folds into the card, and the turn that took it holds one line that
 * opens the card, never the answer drawn again.
 */
describe('COMMS1h the exchange card', () => {
  const draw = (answered: boolean, state: string | null, extra: Partial<Parameters<typeof ConversationView>[0]> = {}) => {
    const { turns } = toTurns(askedEvents(answered));
    const exchanges = exchangesOf(turns, [askRow(state)], nameOf);
    return render(
      <Tooltip.Provider>
        <ConversationView turns={turns} exchanges={exchanges} {...extra} />
      </Tooltip.Provider>,
    );
  };

  it('COMMS1h joins the ask to its call and its answer by the helper’s session, never by words', () => {
    const { turns } = toTurns(askedEvents(true));
    const { cards, takenAt } = exchangesOf(turns, [askRow('answered')], nameOf);
    const card = Object.values(cards)[0]!;
    expect(card).toMatchObject({ id: 'helper:h7', call: 'e2', agent: 'Codex', question: QUESTION, answer: { key: 'e5', text: ANSWER } });
    expect(takenAt).toEqual({ e5: 'e2' });
    // Another helper's answer is no answer of this ask; a person's ask has no call to be drawn at.
    expect(exchangesOf(turns, [askRow('answered', 'helper-9')], nameOf).takenAt).toEqual({});
    expect(exchangesOf(turns, [{ ...askRow('answered'), askedBy: 'person' }], nameOf).cards).toEqual({});
  });

  it('COMMS1h draws the ask as its card where it stands, and says where the answer goes', () => {
    const { container } = draw(false, 'started');
    const card = container.querySelector('[data-exchange="helper:h7"]')!;
    expect(card.textContent).toContain('Asked Codex, as helper');
    expect(card.querySelector('[data-exchange-state]')?.textContent).toMatch(/· reading$/);
    expect(card.textContent).toContain(QUESTION);
    expect(card.textContent).toContain('Its answer comes back to this session as its next turn.');
    // Never a tool card of its own beside it.
    expect(screen.queryByText('helper_ask')).toBeNull();
  });

  it('COMMS1h folds the answer into the card, and the turn that took it is one line to the card', async () => {
    const onShow = vi.fn();
    const { container } = draw(true, 'answered', { onShow });
    const card = container.querySelector('[data-exchange="helper:h7"]')!;
    expect(card.querySelector('[data-exchange-state]')?.textContent).toMatch(/answered .+ · taken as the next turn$/);
    expect(card.querySelector('[data-exchange-answer]')?.textContent).toBe(ANSWER);
    expect(card.textContent).toContain('Codex’s answer · a claim to check, not your words');
    // The answer's words are drawn once, in the card; the taking turn says where they are.
    expect(container.textContent!.split('The lock is released').length - 1).toBe(1);
    expect(screen.queryByText('A helper’s answer, in Daoris’s words')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Began with Codex’s answer' }));
    expect(onShow).toHaveBeenCalledWith('e2');
  });

  it('COMMS1h opens the helper’s conversation from its card, and the keyboard reaches the card’s doors in order', async () => {
    const onOpenHelper = vi.fn();
    draw(true, 'answered', { onOpenHelper, onShow: () => {} });
    const user = userEvent.setup();
    const order: string[] = [];
    for (let at = 0; at < 6; at += 1) {
      await user.tab();
      order.push(document.activeElement?.textContent ?? '');
    }
    // The card's doors in reading order, then the turn that took its answer (a turn's meter between them is the first turn's own).
    const showAll = order.indexOf('Show all');
    expect(showAll).toBeGreaterThanOrEqual(0);
    expect(order[showAll + 1]).toBe('Open its conversation');
    expect(order.indexOf('Began with Codex’s answer')).toBeGreaterThan(showAll + 1);
    await user.click(screen.getByRole('button', { name: 'Open its conversation' }));
    expect(onOpenHelper).toHaveBeenCalledWith('helper-7');
  });

  it('COMMS1h says the card in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      const { container } = draw(true, 'answered', { onShow: () => {} });
      const card = container.querySelector('[data-exchange="helper:h7"]')!;
      expect(card.textContent).toContain('已请 Codex 作为助手');
      expect(card.querySelector('[data-exchange-state]')?.textContent).toMatch(/已回答 · 作为下一轮收下$/);
      expect(screen.getByRole('button', { name: '以 Codex 的回答开始' })).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  it('COMMS1h draws the answer whole where it was taken when the page holds no call to fold it into', () => {
    const { turns } = toTurns(askedEvents(true).filter((event) => event.kind !== 'tool'));
    const exchanges = exchangesOf(turns, [askRow('answered')], nameOf);
    const { container } = render(<Tooltip.Provider><ConversationView turns={turns} exchanges={exchanges} /></Tooltip.Provider>);
    expect(container.querySelector('[data-exchange]')).toBeNull();
    expect(container.textContent).toContain('The lock is released');
  });
});
