import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactNode, useRef } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Notice } from '../bridge/notices';
import i18n from '../locales/i18n';
import { PressArea } from '../person/Confirmations';
import { watchScreen } from '../test/onScreen';
import { DoneAndFailed } from './DoneAndFailed';

// *Done and failed* (NOTIFY2f, D196 point 6, the notification design §2.2's fourth group, §2.3, §2.4): what the person need not answer,
// newest first, each one line in the reader's language with its age, the unread marked; read once it is shown in a window in front;
// *Open*, *Dismiss* and *Mark all read*. jsdom lays nothing out, so each box is the test's, and an `IntersectionObserver` stand-in wakes
// the judgement as the window would.

const NOW = Date.parse('2026-10-22T09:00:00Z');
const ENDED: Notice = { id: 'ended-s1', kind: 'ended', at: '2026-10-22T08:55:00.000Z', read: false, session: 's1', repository: 'Engine', state: 'failed', workspace: 'games' };
const EXPIRED: Notice = { id: 'expired-p1', kind: 'expired', at: '2026-10-22T08:58:00.000Z', read: false, route: 'SESSION_INPUT' };
const DONE: Notice = { id: 'ended-s2', kind: 'ended', at: '2026-10-22T08:00:00.000Z', read: true, session: 's2', repository: 'Engine', state: 'completed' };

describe('Done and failed', () => {
  type Box = { top: number; height: number };
  const layout: { area: Box; row: Box } = { area: { top: 0, height: 400 }, row: { top: 20, height: 60 } };
  const rect = ({ top, height }: Box) => ({ top, bottom: top + height, height, left: 0, right: 300, width: 300, x: 0, y: top, toJSON: () => ({}) });
  let screenStandIn: ReturnType<typeof watchScreen>;
  let focused: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    layout.area = { top: 0, height: 400 };
    layout.row = { top: 20, height: 60 };
    screenStandIn = watchScreen();
    focused = vi.spyOn(document, 'hasFocus').mockReturnValue(true);
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function box(this: Element) {
      if (this.hasAttribute('data-area')) return rect(layout.area) as DOMRect;
      if (this.hasAttribute('data-notice')) return rect(layout.row) as DOMRect;
      return rect({ top: 0, height: 0 }) as DOMRect;
    });
  });
  afterEach(async () => {
    vi.restoreAllMocks();
    screenStandIn.restore();
    await i18n.changeLanguage('en');
  });

  function Area({ children }: { children: ReactNode }) {
    const area = useRef<HTMLDivElement>(null);
    return <div data-area="" ref={area}><PressArea.Provider value={area}>{children}</PressArea.Provider></div>;
  }
  const drawn = (notices: Notice[]) => {
    const onRead = vi.fn();
    const onOpen = vi.fn();
    const onDismiss = vi.fn();
    const view = render(
      <Area><DoneAndFailed notices={notices} onRead={onRead} onOpen={onOpen} onDismiss={onDismiss} now={NOW} /></Area>);
    return { ...view, onRead, onOpen, onDismiss };
  };

  it("says each notice in one line with its age, the unread marked, and never a raw state or a route's name", () => {
    layout.row = { top: 500, height: 60 };
    drawn([EXPIRED, ENDED, DONE]);

    const rows = screen.getAllByRole('listitem');
    expect(rows.map((row) => row.getAttribute('data-notice'))).toEqual(['expired-p1', 'ended-s1', 'ended-s2']);
    expect(within(rows[0]!).getByText(/A terminal's press to say words to a session.*went unanswered/)).toBeInTheDocument();
    expect(within(rows[1]!).getByText('A session in Engine ended: failed')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('5m ago')).toBeInTheDocument();
    expect(within(rows[1]!).getByRole('img', { name: 'Unread' })).toBeInTheDocument();
    expect(within(rows[2]!).queryByRole('img', { name: 'Unread' })).not.toBeInTheDocument();
    expect(screen.queryByText(/SESSION_INPUT/)).not.toBeInTheDocument();
  });

  it('reads a notice once it is shown in a focused window, never while scrolled out or behind, and never one already read', () => {
    layout.row = { top: 500, height: 60 };
    focused.mockReturnValue(false);
    const { onRead } = drawn([ENDED, DONE]);
    expect(onRead).not.toHaveBeenCalled();

    focused.mockReturnValue(true);
    act(() => { window.dispatchEvent(new Event('focus')); });
    expect(onRead).not.toHaveBeenCalled();

    layout.row = { top: 20, height: 60 };
    screenStandIn.show(true);
    expect(onRead).toHaveBeenCalledTimes(1);
    expect(onRead).toHaveBeenCalledWith(['ended-s1']);
    screenStandIn.show(true);
    expect(onRead).toHaveBeenCalledTimes(1);
  });

  it('opens its session, reading it, dismisses it, and reads every unread one by Mark all read', async () => {
    layout.row = { top: 500, height: 60 };
    const { onRead, onOpen, onDismiss } = drawn([EXPIRED, ENDED, DONE]);
    const rows = screen.getAllByRole('listitem');

    // A press's notice names no session: it has no door.
    expect(within(rows[0]!).queryByRole('button', { name: 'Open' })).not.toBeInTheDocument();
    await userEvent.click(within(rows[1]!).getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenCalledWith(ENDED);
    expect(onRead).toHaveBeenLastCalledWith(['ended-s1']);

    await userEvent.click(within(rows[2]!).getByRole('button', { name: 'Dismiss' }));
    expect(onDismiss).toHaveBeenCalledWith(DONE);

    await userEvent.click(screen.getByRole('button', { name: 'Mark all read' }));
    expect(onRead).toHaveBeenLastCalledWith(['expired-p1', 'ended-s1']);
  });

  it('offers no Mark all read with nothing unread', () => {
    drawn([DONE]);
    expect(screen.queryByRole('button', { name: 'Mark all read' })).not.toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    layout.row = { top: 500, height: 60 };
    drawn([EXPIRED, ENDED]);

    const rows = screen.getAllByRole('listitem');
    expect(within(rows[0]!).getByText(/一个终端请求.*无人回答，已超时/)).toBeInTheDocument();
    expect(within(rows[1]!).getByText('Engine 的一个会话已结束：失败')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '全部标为已读' })).toBeInTheDocument();
    expect(within(rows[1]!).getByRole('button', { name: '移除' })).toBeInTheDocument();
  });

  /**
   * NOTIFY2h (D196 point 7, the notification design §5.1): a look's news is said by its code in the reader's language, never the
   * driver's English line; it opens its session where it names one; a code a later build says reads as something ended.
   */
  it.each([
    ['en', 'A session was stopped: another machine took its quest first', 'A meeting was cut short: the driver running it stopped', 'Something ended'],
    ['zh', '一个会话已停止：另一台机器先接下了它的委托', '一次会议被中断：运行它的驱动已停止', '有事项已结束'],
  ])("says a look's news by its code in %s", async (language, lost, cut, other) => {
    await i18n.changeLanguage(language);
    layout.row = { top: 500, height: 60 };
    const LOST: Notice = { id: 'look-lost-claim-s3', kind: 'look', code: 'lost-claim', at: '2026-10-22T08:59:00.000Z', read: false, session: 's3', quest: 'q3' };
    const CUT: Notice = { id: 'look-meeting-cut-m1', kind: 'look', code: 'meeting-cut', at: '2026-10-22T08:58:00.000Z', read: false };
    const LATER: Notice = { id: 'look-later-x', kind: 'look', code: 'a-later-code', at: '2026-10-22T08:57:00.000Z', read: false };
    const { onOpen } = drawn([LOST, CUT, LATER]);

    const rows = screen.getAllByRole('listitem');
    expect(within(rows[0]!).getByText(lost)).toBeInTheDocument();
    expect(within(rows[1]!).getByText(cut)).toBeInTheDocument();
    expect(within(rows[2]!).getByText(other)).toBeInTheDocument();
    expect(screen.queryByText(/lost-claim|meeting-cut|stop  session/)).not.toBeInTheDocument();
    // A meeting's names no session: it has no door; a stopped session's opens it.
    expect(within(rows[1]!).queryByRole('button', { name: language === 'en' ? 'Open' : '打开' })).not.toBeInTheDocument();
    await userEvent.click(within(rows[0]!).getByRole('button', { name: language === 'en' ? 'Open' : '打开' }));
    expect(onOpen).toHaveBeenCalledWith(LOST);
  });

  /**
   * NOTIFY2h2 (D196's NOTIFY2h note): the news its deeper producers class, each said by its code in the reader's language; a reopen whose
   * tree is gone names its session and opens it, a plugin stopped as unsound names none.
   */
  it.each([
    ['en', 'A reopened quest\'s session was told nothing: its tree is gone, so no work is made again',
      'A plugin stopped after exiting three times within an hour: it starts again once you try it again',
      'An ask for a helper was refused: the session that asked it has left this machine'],
    ['zh', '一个被重新打开的委托的会话未被告知：它的工作树已不在，所以不会重做这项工作',
      '一个插件在一小时内退出三次后已停止：你再试一次后它才会重新启动',
      '一个请助手回答的请求被拒绝：提出它的会话已离开本机'],
  ])("says the deeper producers' news by its code in %s", async (language, tree, plugin, helper) => {
    await i18n.changeLanguage(language);
    layout.row = { top: 500, height: 60 };
    const TREE: Notice = { id: 'look-reopen-tree-gone-q4', kind: 'look', code: 'reopen-tree-gone', at: '2026-10-22T08:59:00.000Z', read: false, session: 's4', quest: 'q4' };
    const PLUGIN: Notice = { id: 'look-plugin-unsound-acme', kind: 'look', code: 'plugin-unsound', at: '2026-10-22T08:58:00.000Z', read: false };
    const HELPER: Notice = { id: 'look-helper-asker-gone-h1', kind: 'look', code: 'helper-asker-gone', at: '2026-10-22T08:57:00.000Z', read: false };
    const { onOpen } = drawn([TREE, PLUGIN, HELPER]);

    const rows = screen.getAllByRole('listitem');
    expect(within(rows[0]!).getByText(tree)).toBeInTheDocument();
    expect(within(rows[1]!).getByText(plugin)).toBeInTheDocument();
    expect(within(rows[2]!).getByText(helper)).toBeInTheDocument();
    expect(screen.queryByText(/tree-gone|plugin-unsound|asker-gone|reopen {2}/)).not.toBeInTheDocument();
    expect(within(rows[1]!).queryByRole('button', { name: language === 'en' ? 'Open' : '打开' })).not.toBeInTheDocument();
    await userEvent.click(within(rows[0]!).getByRole('button', { name: language === 'en' ? 'Open' : '打开' }));
    expect(onOpen).toHaveBeenCalledWith(TREE);
  });
});
