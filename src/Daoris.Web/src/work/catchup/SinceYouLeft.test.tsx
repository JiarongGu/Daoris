import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { elapsed } from '../../locales/format';
import { toTurns } from '../conversation/conversation';
import { lifetimesIn } from '../beside/besides';
import { digestOf } from './digest';
import { SinceYouLeft } from './SinceYouLeft';
import { DRIVEN, DRIVEN_MARK } from './catchup.fixtures';

// The row at the mark: each line in the reader's words, a jump to its first row, and the agent's last words quoted as its own.

const NOW = Date.parse('2026-11-19T12:10:30Z');

const draw = (onJump = vi.fn()) => {
  const digest = digestOf({
    turns: toTurns(DRIVEN).turns, part: 's-driven', mark: DRIVEN_MARK, lifetimes: lifetimesIn(DRIVEN),
    steps: { passed: 2, failed: ['release'] }, waiting: 1,
  })!;
  render(<SinceYouLeft digest={digest} read={DRIVEN_MARK.read} now={NOW} onJump={onJump} />);
  return onJump;
};

describe('Since you left', () => {
  it('says how long ago the person left and each line of what happened', () => {
    draw();
    const row = screen.getByRole('region', { name: 'What happened since you left' });

    expect(row.textContent).toContain(`Since you left · ${elapsed(DRIVEN_MARK.read, new Date(NOW).toISOString())}`);
    for (const said of ['1 turn', '1 command', 'subagents: 2 started, 2 ended · 1 started by another subagent','background tasks: 1 started, 1 ended', '1 step passed',
      'failed: release', '1 waiting on you', 'Its last words (the agent\'s):', 'The refresh now checks expiry first.']) {
      expect(row.textContent).toContain(said);
    }
  });

  it('jumps to the first row each line names, and draws a line with nowhere to go as no press', () => {
    const onJump = draw();
    const row = screen.getByRole('region', { name: 'What happened since you left' });

    fireEvent.click(within(row).getByRole('button', { name: /subagents: 2 started/ }));
    fireEvent.click(within(row).getByRole('button', { name: /The refresh now checks expiry first/ }));
    expect(onJump.mock.calls).toEqual([['e2'], ['e10']]);
    expect(within(row).queryByRole('button', { name: /step passed/ })).toBeNull();
  });

  it('reads in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      draw();
      const row = screen.getByRole('region', { name: '你离开之后发生了什么' });
      expect(row.textContent).toContain('子智能体：启动 2 个，结束 2 个');
      expect(row.textContent).toContain('1 项等你处理');
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
