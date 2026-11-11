import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { type TerminalActsNow, type TerminalPress, terminalActsOf } from '../person/terminalActs';
import { TerminalTierLine } from './TerminalTierLine';

// The terminal's tier in Notifications: while a terminal's presses run as the person's with no card, who turned that on and when, a
// one-press *Back to ask*, and each press it made with what it named, the command it is, its admission and its end.

const press = (id: string, route: string, more: Partial<TerminalPress> = {}): TerminalPress => ({
  id, module: 'DAORIS.DRIVER', route, scope: null, target: null, command: null, at: '2026-11-11T10:00:00Z', outcome: 'done', code: null, ended: null, ...more,
});
const ACTS: TerminalActsNow = {
  changes: [{ tier: 'act', at: '2026-11-11T09:00:00Z', by: 'ask-daoris', session: 'h1a2b3c4' }],
  presses: [
    press('t3', 'SET_HOLD', { scope: 'repository', target: 'engine', outcome: 'interrupted' }),
    press('t2', 'SESSION_ARCHIVE', { outcome: 'failed', code: 'driver-refused' }),
    press('t1', 'WORK_PAUSE', { scope: 'quest', target: 'q1', command: 'daoris quest pause q1' }),
  ],
};

describe("Notifications' line of the terminal's tier", () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('reads the record field by field and leaves out what it cannot read', () => {
    expect(terminalActsOf({
      changes: [{ tier: 'act', at: '2026-11-11T09:00:00Z', by: 'window' }, { tier: 'always', at: 'x', by: 'window' }],
      presses: [{ id: 'a', module: 'DAORIS.DRIVER', route: 'WORK_PAUSE', at: '2026-11-11T10:00:00Z', outcome: 'unknown', scope: 'quest', target: 'q1' }, { id: 'b' }],
    })).toEqual({
      changes: [{ tier: 'act', at: '2026-11-11T09:00:00Z', by: 'window', session: null }],
      presses: [press('a', 'WORK_PAUSE', { at: '2026-11-11T10:00:00Z', outcome: 'unknown', scope: 'quest', target: 'q1' })],
    });
    expect(terminalActsOf(undefined)).toEqual({ changes: [], presses: [] });
  });

  it('draws nothing under ask where a terminal pressed nothing', () => {
    render(<TerminalTierLine tier="ask" acts={{ changes: [], presses: [] }} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(document.querySelector('[data-terminal-tier-line]')).toBeNull();
  });

  it.each([
    ['en', "A terminal's presses", 'A press from a terminal on this machine runs at once as yours, with no card.', 'Turned on by Ask Daoris, with no card', 'Back to ask',
      'Presses a terminal made as yours', 'Interrupted: Daoris stopped while it ran, so whether it was done is not known', 'Not done (driver-refused)', 'Done'],
    ['zh', '终端的操作', '本机终端发来的操作会直接以你的名义执行，不经确认卡。', '由 Ask Daoris 未经确认卡开启', '改回询问',
      '终端以你的名义执行的操作', '已中断：运行中 Daoris 停止了，无法确定是否已完成', '未完成（driver-refused）', '已完成'],
  ])('says who turned act on, offers Back to ask and lists each press with how it ended (%s)', async (language, label, act, by, back, list, interrupted, failed, done) => {
    await i18n.changeLanguage(language);
    const onBackToAsk = vi.fn();
    const onOpen = vi.fn();
    render(<TerminalTierLine tier="act" acts={ACTS} onBackToAsk={onBackToAsk} onOpen={onOpen} defaultOpen />);

    const line = within(screen.getByRole('region', { name: label }));
    expect(line.getByText(act)).toBeInTheDocument();
    expect(line.getByText(new RegExp(by))).toBeInTheDocument();
    await userEvent.click(line.getByRole('button', { name: back }));
    expect(onBackToAsk).toHaveBeenCalledTimes(1);

    const rows = within(line.getByRole('list', { name: list })).getAllByRole('listitem');
    expect(rows.map((row) => row.querySelector('[data-terminal-outcome]')?.textContent)).toEqual([interrupted, failed, done]);
    expect(within(rows[2]!).getByText('daoris quest pause q1')).toBeInTheDocument();
    await userEvent.click(within(rows[2]!).getByRole('button', { name: /q1/ }));
    expect(onOpen).toHaveBeenCalledWith(ACTS.presses[2]);
    expect(within(rows[1]!).queryAllByRole('button')).toHaveLength(0);
    expect(document.body.textContent).not.toMatch(/\{\{|undefined/);
  });

  it.each([
    ['window', 'Turned on by you, in this window'],
    ['card', "Turned on by you, on a terminal's card"],
    ['session', 'Turned on by the session s9, as its own'],
    ['terminal', 'Turned on by a terminal, with no card'],
  ] as const)('names who turned it on: %s', (by, said) => {
    render(<TerminalTierLine tier="act" acts={{ changes: [{ tier: 'act', at: '2026-11-11T09:00:00Z', by, session: by === 'session' ? 's9' : null }], presses: [] }} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(screen.getByText(new RegExp(said))).toBeInTheDocument();
    expect(screen.queryByRole('list')).toBeNull();
  });

  it('says act was set outside the record where its newest change is not act, and under ask says who set it back', async () => {
    const { unmount } = render(<TerminalTierLine tier="act" acts={{ changes: [{ tier: 'ask', at: '2026-11-11T09:00:00Z', by: 'window', session: null }], presses: [] }} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(screen.getByText(/Turned on outside Daoris: no change to it is recorded/)).toBeInTheDocument();
    unmount();

    render(<TerminalTierLine tier="ask" acts={{ changes: [{ tier: 'ask', at: '2026-11-11T11:00:00Z', by: 'alone', session: null }], presses: ACTS.presses }} onBackToAsk={vi.fn()} onOpen={vi.fn()} />);
    expect(screen.getByText('A press from a terminal on this machine waits for your Confirm.')).toBeInTheDocument();
    expect(screen.getByText(/Back to ask by a terminal, while Daoris was not running/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Back to ask' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Show what a terminal pressed' }));
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
  });
});
