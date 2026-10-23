import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import { LimitWaitLine } from './LimitWaitLine';
import { limitWaitSaid } from './limitWait';

// LIMITRESUME1d (D206 §3.7): the line a waiting limited line shows, its words and its presses, in en and zh.

const UNTIL = '2026-10-24T11:02:00Z';
const t = i18n.t.bind(i18n) as (key: string, options?: Record<string, unknown>) => string;
const lines = () => limitWaitSaid(t, { until: UNTIL, account: 'work', own: true, stated: true, notLifted: 'session' }, (account) => account ?? '');

describe('a line waiting for its limit', () => {
  afterEach(() => i18n.changeLanguage('en'));

  it('says when it resumes, how, and a reset that did not lift it, with its presses, each pressed once', async () => {
    const pressed = { goOn: vi.fn(), tryNow: vi.fn(), pause: vi.fn() };
    render(<LimitWaitLine lines={lines()} onGoOnNow={pressed.goOn} onTryNow={pressed.tryNow} onPause={pressed.pause} />);

    const line = screen.getByRole('group', { name: 'Waiting for a limit' });
    expect(line).toHaveTextContent(`Waiting for a limit to reset: it resumes by itself at ${moment(UNTIL)}, on work.`);
    expect(line).toHaveTextContent('It goes on in its own conversation.');
    expect(line).toHaveTextContent('Its last reset did not lift the limit: session.');
    await userEvent.click(screen.getByRole('button', { name: 'Go on now with…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Try now' }));
    await userEvent.click(screen.getByRole('button', { name: i18n.t('work.act.pauseQuest') }));
    expect([pressed.goOn, pressed.tryNow, pressed.pause].map((each) => each.mock.calls.length)).toEqual([1, 1, 1]);
    // A conversation's presses are not a driven line's.
    expect(screen.queryByRole('button', { name: 'Stop waiting' })).toBeNull();
  });

  it('offers a conversation that has ended Stop waiting, never a pause, and says a refusal', () => {
    render(<LimitWaitLine lines={lines()} onStopWaiting={() => {}} refusal="conversation `c1` runs here: stop it to end its wait." />);

    expect(screen.getByRole('button', { name: 'Stop waiting' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: i18n.t('work.act.pauseQuest') })).toBeNull();
    expect(screen.getByRole('alert')).toHaveTextContent('conversation c1 runs here: stop it to end its wait.');
  });

  it('says it all in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<LimitWaitLine lines={lines()} onGoOnNow={() => {}} onStopWaiting={() => {}} />);

    const line = screen.getByRole('group', { name: '等待额度' });
    expect(line).toHaveTextContent(`正在等待额度重置：将于 ${moment(UNTIL)} 在 work 上自动继续。`);
    expect(line).toHaveTextContent('它将在原对话中继续。');
    expect(line).toHaveTextContent('上次重置未解除额度限制：session。');
    expect(screen.getByRole('button', { name: '改用其他选择继续…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '停止等待' })).toBeInTheDocument();
    // Never 恢复 for an account that is ready again, nor 任务 for a quest.
    expect(line.textContent).not.toMatch(/恢复|任务/);
  });
});
