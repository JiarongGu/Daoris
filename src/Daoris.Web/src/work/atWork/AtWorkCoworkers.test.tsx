import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { AtWorkView, WaitingOnLine } from './AtWorkView';
import { COWORKERS, LEAD_WAITING_ON_HELPER, LEAD_WAITING_ON_LANE, REVIEWERS } from './atWork.fixtures';
import type { AtWorkA } from './types';

/**
 * COMMS1i (the comms design §6, §7): *At work* reads the line's requests. Those that hold are its *Waiting on*, which the head draws
 * from the same read in the same words; the helpers, partners and successors it asked are its co-workers, each with where it stands,
 * what Daoris saw of its delivery apart from its outcome, and a door to the session that took it up. A source not read is said.
 */
afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const answerOf = (held: typeof COWORKERS) => (held as { answer: AtWorkA }).answer;
const NAMES: Record<string, string> = { 'codex-acp': 'Codex' };
const nameOf = (agent: string) => NAMES[agent] ?? agent;

describe('COMMS1i co-workers', () => {
  it('COMMS1i lists each co-worker in its state, its delivery apart, and opens the session that took it up', async () => {
    const onOpen = vi.fn();
    const { container } = render(<AtWorkView held={COWORKERS} nameOf={nameOf} onOpen={onOpen} />);
    const row = (id: string) => container.querySelector(`[data-coworker="${id}"]`)?.textContent ?? '';

    expect(row('helper:h5')).toContain('a helper');
    expect(row('helper:h5')).toContain('asked');
    expect(row('helper:h6')).toContain('Codex, as helper');
    expect(row('helper:h6')).toContain('starting');
    expect(row('helper:h8')).toContain('answered · taken');
    expect(row('piece:p1')).toContain('claude-code, as partner');
    expect(row('piece:p1')).toContain('refused');
    expect(row('piece:p1')).not.toContain('not reached');
    expect(row('hand-over:o1')).toContain('an agent to go on with it');
    expect(row('hand-over:o1')).toContain('starting · delivery not certain');
    expect(row('hand-over:o1')).toContain('asked by an earlier part');
    expect(screen.getByText('Not read: its pieces. What the line waits on may be more.')).toBeTruthy();

    await userEvent.click(within(container.querySelector('[data-coworker="helper:h7"]') as HTMLElement).getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenCalledWith('h-session');
    // A request nobody took up yet has no session to open.
    expect(within(container.querySelector('[data-coworker="helper:h5"]') as HTMLElement).queryByRole('button')).toBeNull();
  });

  it('COMMS1i says the co-workers in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const { container } = render(<AtWorkView held={COWORKERS} nameOf={nameOf} />);
    expect(screen.getByText('同事')).toBeTruthy();
    expect(container.querySelector('[data-coworker="helper:h8"]')?.textContent).toContain('Codex，作为助手');
    expect(container.querySelector('[data-coworker="helper:h8"]')?.textContent).toContain('已回答 · 已收下');
  });

  it('COMMS1i lists the line’s reviewers among its co-workers, the one reading what the line waits on, in English and Chinese', async () => {
    const view = render(<AtWorkView held={REVIEWERS} nameOf={nameOf} />);
    const row = (id: string) => view.container.querySelector(`[data-coworker="${id}"]`)?.textContent ?? '';
    expect(row('opinion:o1')).toContain('Codex, as reviewer');
    expect(row('opinion:o1')).toContain('asked');
    expect(row('opinion:o0')).toContain('claude-code, as reviewer');
    expect(row('opinion:o0')).toContain('reading ended');
    expect(row('opinion:o2')).toContain('a reviewer');
    expect(row('opinion:o2')).toContain('declined');
    expect(view.container.querySelector('[data-wait-line]')?.textContent).toContain('Codex, as reviewer');
    view.unmount();

    await i18n.changeLanguage('zh');
    const zh = render(<AtWorkView held={REVIEWERS} nameOf={nameOf} />);
    expect(zh.container.querySelector('[data-coworker="opinion:o1"]')?.textContent).toContain('Codex，作为阅读者');
    expect(zh.container.querySelector('[data-coworker="opinion:o0"]')?.textContent).toContain('阅读已结束');
    expect(zh.container.querySelector('[data-coworker="opinion:o2"]')?.textContent).toContain('一位阅读者');
  });

  it('COMMS1i draws no co-workers for a line that asked none, and a quest is no co-worker', () => {
    render(<AtWorkView held={LEAD_WAITING_ON_LANE} />);
    expect(screen.queryByText('Co-workers')).toBeNull();
  });
});

describe('COMMS1i the head’s Waiting on', () => {
  it('COMMS1i is At work’s line, the same words from the same read, with a door to At work', async () => {
    for (const held of [LEAD_WAITING_ON_LANE, LEAD_WAITING_ON_HELPER, COWORKERS]) {
      const view = render(<AtWorkView held={held} nameOf={nameOf} />);
      const inView = view.container.querySelector('[data-wait-line]')?.textContent;
      view.unmount();
      const onOpen = vi.fn();
      const head = render(<WaitingOnLine answer={answerOf(held)} nameOf={nameOf} onOpen={onOpen} />);
      const line = head.container.querySelector('[data-waiting-on]')!;
      expect(line.textContent).toBe(`${inView}Open At work`);
      await userEvent.click(within(line as HTMLElement).getByRole('button', { name: 'Open At work' }));
      // The door opens At work at the first request the line names.
      expect(onOpen).toHaveBeenCalledOnce();
      expect(onOpen).toHaveBeenCalledWith(answerOf(held).exchanges!.find((exchange) => exchange.holds)!.id);
      head.unmount();
    }
    expect(answerOf(LEAD_WAITING_ON_HELPER).exchanges).toHaveLength(1);
  });

  it('COMMS1i draws nothing where the line waits on nothing it asked', () => {
    const { container } = render(<WaitingOnLine answer={{ ...answerOf(COWORKERS), exchanges: [] }} onOpen={() => {}} />);
    expect(container.textContent).toBe('');
  });
});

/**
 * COMMS1i (the comms design §7): the head's *Waiting on* opens *At work* at the exchange. The request's row takes the focus,
 * outlined, a co-worker's or a quest's; a request with no row of its own leads to the view's *Waiting on* line.
 */
describe('COMMS1i At work opened at the request the head named', () => {
  const QUEST = { id: 'q7', title: 'Draw the policy chip', status: 'Taken', lanes: ['core'], taker: { untaken: true } };
  const withQuest = answerOfHeld(LEAD_WAITING_ON_LANE, { quests: [QUEST] as AtWorkA['quests'] });

  function answerOfHeld(held: typeof COWORKERS, extra: Partial<AtWorkA>) {
    return { ...(held as { answer: AtWorkA }), answer: { ...answerOf(held), ...extra } } as typeof COWORKERS;
  }

  it('COMMS1i focuses and outlines the co-worker the line waits on', () => {
    const { container } = render(<AtWorkView held={COWORKERS} nameOf={nameOf} seek={{ exchange: 'helper:h7', n: 1 }} />);
    const row = container.querySelector('[data-exchange="helper:h7"]') as HTMLElement;
    expect(row).toHaveFocus();
    expect(row.className).toContain('outline-accent');
    expect((container.querySelector('[data-exchange="helper:h8"]') as HTMLElement).className).not.toContain('outline-accent');
  });

  it('COMMS1i focuses the quest the line waits on among its quests', () => {
    const { container } = render(<AtWorkView held={withQuest} seek={{ exchange: 'quest:q7', n: 1 }} />);
    expect(container.querySelector('[data-exchange="quest:q7"]')).toHaveFocus();
  });

  it('COMMS1i leads to the Waiting on line where the request has no row on screen, and seeks again on a second press', () => {
    const view = render(<AtWorkView held={LEAD_WAITING_ON_LANE} seek={{ exchange: 'quest:q7', n: 1 }} />);
    expect(view.container.querySelector('[data-wait-line]')).toHaveFocus();
    (document.activeElement as HTMLElement).blur();
    view.rerender(<AtWorkView held={LEAD_WAITING_ON_LANE} seek={{ exchange: 'quest:q7', n: 2 }} />);
    expect(view.container.querySelector('[data-wait-line]')).toHaveFocus();
  });
});
