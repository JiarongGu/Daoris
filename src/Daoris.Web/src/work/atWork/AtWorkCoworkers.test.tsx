import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { AtWorkView, WaitingOnLine } from './AtWorkView';
import { COWORKERS, LEAD_WAITING_ON_HELPER, LEAD_WAITING_ON_LANE } from './atWork.fixtures';
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
    expect(screen.getByText('协作者')).toBeTruthy();
    expect(container.querySelector('[data-coworker="helper:h8"]')?.textContent).toContain('Codex，作为助手');
    expect(container.querySelector('[data-coworker="helper:h8"]')?.textContent).toContain('已回答 · 已收下');
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
      expect(onOpen).toHaveBeenCalledOnce();
      head.unmount();
    }
    expect(answerOf(LEAD_WAITING_ON_HELPER).exchanges).toHaveLength(1);
  });

  it('COMMS1i draws nothing where the line waits on nothing it asked', () => {
    const { container } = render(<WaitingOnLine answer={{ ...answerOf(COWORKERS), exchanges: [] }} onOpen={() => {}} />);
    expect(container.textContent).toBe('');
  });
});
