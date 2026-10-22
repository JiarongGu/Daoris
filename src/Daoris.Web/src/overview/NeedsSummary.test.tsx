import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AttentionRead } from '../bridge/attention';
import type { AskMessages, Notice } from '../bridge/notices';
import i18n from '../locales/i18n';
import type { Confirmation } from '../person/Confirmations';
import type { PushedAttention } from '../work/attention/attention';
import { NeedsSummary } from './NeedsSummary';

// Overview's summary of what needs the person (NOTIFY2b, D196 point 5, the notification design §4): a line per group with its count
// and its oldest wait, each a door into *Notifications* at its group, and no row listed; with nothing waiting, today's one line; in a
// plain browser, that this is the app's.

const NOW = new Date('2026-10-21T12:00:00Z');
const ago = (minutes: number) => new Date(NOW.getTime() - minutes * 60_000).toISOString();

const ROWS: PushedAttention[] = [
  { kind: 'parked', id: 's1', since: ago(130), where: 'game' },
  { kind: 'go-ahead', id: 'g1', since: ago(60), where: 'game' },
  { kind: 'proposal', id: 'a1', since: ago(24 * 60), where: 'default' },
  { kind: 'review', id: 'r1', since: null, where: '' },
  // A press's own row is its card's: counted by the presses' line, never twice.
  { kind: 'confirmation', id: 'c1', since: ago(1), where: '' },
];
const KNOWN: AttentionRead = { state: 'known', rows: ROWS, working: 2, running: 2, resyncing: false };
const PRESS: Confirmation = {
  source: 'pipe', id: 'c1', route: 'STOP_SESSION', fields: [], shown: true, expires: new Date(NOW.getTime() + 100_000).toISOString(),
};

describe("Overview's summary of what needs you", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
  });
  afterEach(async () => {
    vi.useRealTimers();
    await i18n.changeLanguage('en');
  });

  it('says a line per group with its count and its oldest wait, and lists no row', () => {
    render(<NeedsSummary attention={KNOWN} presses={[PRESS]} onNotifications={() => {}} />);

    const summary = screen.getByRole('region', { name: 'What needs you' });
    const lines = within(summary).getAllByRole('button');
    expect(lines.map((line) => line.textContent)).toEqual([
      'Waiting for your confirmation1refused by itself in 1m 40s',
      'Holding work2the oldest 2h 10m',
      'Waiting for your word1the oldest 1d 0h',
      'Ready for you1',
    ]);
    expect(within(summary).queryByRole('listitem')).toBeNull();
  });

  /** GOAHEADSTALE1: a stale go-ahead holds no session's act, so it is counted with what waits for the person's word, where it is listed. */
  it('counts a stale go-ahead with what waits for your word, as Notifications lists it', () => {
    const stale: PushedAttention = { kind: 'go-ahead', id: 'g2', since: ago(3 * 24 * 60), where: 'game', stale: { since: ago(2 * 24 * 60) } };
    render(<NeedsSummary attention={{ ...KNOWN, rows: [...ROWS, stale] }} presses={[PRESS]} onNotifications={() => {}} />);

    const lines = within(screen.getByRole('region', { name: 'What needs you' })).getAllByRole('button');
    expect(lines.map((line) => line.textContent)).toEqual([
      'Waiting for your confirmation1refused by itself in 1m 40s',
      'Holding work2the oldest 2h 10m',
      'Waiting for your word2the oldest 3d 0h',
      'Ready for you1',
    ]);
  });

  it('opens Notifications at its group from each line', async () => {
    const onNotifications = vi.fn();
    vi.useRealTimers();
    render(<NeedsSummary attention={KNOWN} presses={[PRESS]} onNotifications={onNotifications} />);

    await userEvent.click(screen.getByRole('button', { name: /^Waiting for your confirmation/ }));
    expect(onNotifications).toHaveBeenLastCalledWith('presses');
    await userEvent.click(screen.getByRole('button', { name: /^Holding work/ }));
    expect(onNotifications).toHaveBeenLastCalledWith('needs');
  });

  it('says nothing needs you, and the sessions working, with nothing waiting', () => {
    render(<NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} onNotifications={() => {}} />);

    const summary = screen.getByRole('region', { name: 'What needs you' });
    expect(summary).toHaveTextContent('Nothing needs you');
    expect(summary).toHaveTextContent('2 sessions working');
    expect(within(summary).queryByRole('button')).toBeNull();
  });

  it('says Done and failed after what waits, its notices and how many are unread, a door to its group (NOTIFY2f)', async () => {
    const onNotifications = vi.fn();
    const notices: Notice[] = [
      { id: 'ended-s1', kind: 'ended', at: ago(5), read: false, repository: 'Engine', state: 'failed' },
      { id: 'ended-s2', kind: 'ended', at: ago(50), read: true, repository: 'Engine', state: 'completed' },
    ];
    const { rerender } = render(<NeedsSummary attention={KNOWN} presses={[]} notices={notices} onNotifications={onNotifications} />);

    const lines = within(screen.getByRole('region', { name: 'What needs you' })).getAllByRole('button');
    expect(lines.at(-1)!.textContent).toBe('Done and failed21 unread');
    vi.useRealTimers();
    await userEvent.click(lines.at(-1)!);
    expect(onNotifications).toHaveBeenLastCalledWith('done');

    // With nothing waiting, its line stands alone, and every one read says no count of unread.
    rerender(<NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} notices={[notices[1]!]} onNotifications={onNotifications} />);
    expect(within(screen.getByRole('region', { name: 'What needs you' })).getAllByRole('button').map((line) => line.textContent))
      .toEqual(['Done and failed1']);

    await i18n.changeLanguage('zh');
    rerender(<NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} notices={notices} onNotifications={onNotifications} />);
    expect(within(screen.getByRole('region', { name: '等你处理' })).getAllByRole('button').map((line) => line.textContent))
      .toEqual(['完成与出错21 条未读']);
  });

  it('says Messages before Done and failed, its items and how many are unread, a door to its group (NOTIFY2g)', async () => {
    const onNotifications = vi.fn();
    const messages: AskMessages[] = [
      { ask: 'a1', workspace: 'default', teammate: false, unread: 3 },
      { ask: '7e6f51', workspace: 'default', teammate: true, unread: 1 },
      { ask: 'a2', workspace: 'default', teammate: false, unread: 0 },
    ];
    const answered: Notice[] = [
      { id: 'answered-c1', kind: 'answered', at: ago(2), read: false, session: 'c1', repository: 'game' },
      { id: 'answered-c2', kind: 'answered', at: ago(9), read: true, session: 'c2', repository: 'game' },
    ];
    const notices: Notice[] = [{ id: 'ended-s1', kind: 'ended', at: ago(5), read: false, repository: 'Engine', state: 'failed' }];
    const { rerender } = render(
      <NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} messages={messages} answered={answered} notices={notices} onNotifications={onNotifications} />);

    const lines = () => within(screen.getByRole('region', { name: /What needs you|等你处理/ })).getAllByRole('button');
    expect(lines().map((line) => line.textContent)).toEqual(['Messages35 unread', 'Done and failed11 unread']);
    vi.useRealTimers();
    await userEvent.click(lines()[0]!);
    expect(onNotifications).toHaveBeenLastCalledWith('messages');

    // Nothing unread: no line, and with nothing waiting either, today's one line.
    rerender(<NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} messages={[messages[2]!]} answered={[answered[1]!]} onNotifications={onNotifications} />);
    expect(screen.getByRole('region', { name: 'What needs you' })).toHaveTextContent('Nothing needs you');

    await i18n.changeLanguage('zh');
    rerender(<NeedsSummary attention={{ ...KNOWN, rows: [] }} presses={[]} messages={messages} answered={answered} onNotifications={onNotifications} />);
    expect(lines().map((line) => line.textContent)).toEqual(['消息35 条未读']);
  });

  it("says in a plain browser that it is the app's", () => {
    render(<NeedsSummary attention={{ state: 'app' }} presses={[]} />);

    expect(screen.getByRole('region', { name: 'What needs you' })).toHaveTextContent(
      'What needs you is judged by the Daoris app: open the app to see what waits for your word.');
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<NeedsSummary attention={KNOWN} presses={[PRESS]} onNotifications={() => {}} />);

    const summary = screen.getByRole('region', { name: '等你处理' });
    expect(within(summary).getAllByRole('button').map((line) => line.textContent)).toEqual([
      '等待你确认11 分 40 秒后自动拒绝',
      '阻塞工作2最早的已等 2 小时 10 分',
      '等你发话1最早的已等 1 天 0 小时',
      '待你审阅1',
    ]);
  });
});
