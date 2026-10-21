import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import type { AskMessages, Notice } from '../bridge/notices';
import type { Confirmation } from '../person/Confirmations';
import { NotificationsView } from './NotificationsView';

// *Notifications* (NOTIFY2b, D196 point 3, the notification design §2): a view of the regions, the presses waiting for the person's
// confirmation first, each as its whole card, then *What needs you*; each group headed by its name and its count, foldable, its fold
// this window's own; a group holding nothing not drawn, and with nothing in any, *Nothing needs you* and the sessions working.

const PRESS: Confirmation = {
  source: 'pipe', id: 'c1', module: 'DAORIS.DRIVER', route: 'STOP_SESSION',
  fields: [{ name: '/id', type: 'string', value: '9b1e0d2a' }], named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }],
  asker: { process: 'bash', pid: 4120 }, expires: '2026-10-21T09:10:00Z',
};
const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict", fields: [], asker: null,
  expires: '2026-10-21T09:04:00Z',
};

const view = (props: Partial<Parameters<typeof NotificationsView>[0]> = {}) => {
  const onVisible = vi.fn();
  const drawn = render(
    <NotificationsView
      presses={[PRESS, SERVICE]}
      needs={2}
      working={1}
      attention={<p>the rows of What needs you</p>}
      onAnswer={async () => ({ answered: true })}
      onVisible={onVisible}
      {...props}
    />);
  return { ...drawn, onVisible };
};

describe('Notifications', () => {
  beforeEach(() => { window.sessionStorage.clear(); });
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('lists the presses first, each as its card, then What needs you, each group with its count', () => {
    view();

    const groups = screen.getAllByRole('region');
    expect(groups.map((group) => group.getAttribute('aria-label'))).toEqual(['Waiting for your confirmation', 'What needs you']);
    expect(within(groups[0]!).getByText('2')).toBeInTheDocument();
    expect(within(groups[0]!).getAllByRole('group')).toHaveLength(2);
    expect(within(groups[1]!).getByText('2')).toBeInTheDocument();
    expect(within(groups[1]!).getByText('the rows of What needs you')).toBeInTheDocument();
  });

  it('draws no group that holds nothing, and with nothing in any says nothing needs you and what is working', () => {
    const { rerender } = view({ presses: [] });
    expect(screen.getAllByRole('region').map((group) => group.getAttribute('aria-label'))).toEqual(['What needs you']);

    rerender(
      <NotificationsView presses={[]} needs={0} working={2} attention={<p>rows</p>} onAnswer={async () => ({ answered: true })} />);
    expect(screen.queryAllByRole('region')).toHaveLength(0);
    expect(screen.getByText('Nothing needs you')).toBeInTheDocument();
    expect(screen.getByText('2 sessions working')).toBeInTheDocument();
  });

  it("folds a group by its head, its cards drawn no more, and keeps the fold as this window's own", async () => {
    const { unmount } = view();

    await userEvent.click(screen.getByRole('button', { name: /Waiting for your confirmation/ }));
    expect(screen.getByRole('button', { name: /Waiting for your confirmation/ })).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryAllByRole('group')).toHaveLength(0);
    unmount();

    view();
    expect(screen.queryAllByRole('group')).toHaveLength(0);
    await userEvent.click(screen.getByRole('button', { name: /Waiting for your confirmation/ }));
    expect(screen.getAllByRole('group')).toHaveLength(2);
  });

  it('shows a group a door asked for, unfolded and brought into view, and a press by its card', async () => {
    const scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled;
    window.sessionStorage.setItem('daoris.notifications.folded', JSON.stringify(['needs']));
    const { rerender } = view({ at: { group: 'needs', n: 1 } });
    expect(screen.getByText('the rows of What needs you')).toBeInTheDocument();
    expect(scrolled).toHaveBeenCalled();

    scrolled.mockClear();
    rerender(
      <NotificationsView presses={[PRESS]} needs={2} working={1} attention={<p>rows</p>} at={{ press: 'c1', n: 2 }}
        onAnswer={async () => ({ answered: true })} />);
    expect(scrolled).toHaveBeenCalled();
    expect(scrolled.mock.contexts[0]).toBe(document.getElementById('confirmation-c1'));
  });

  it('tells the window it is shown while drawn, and no more once it goes', () => {
    const { onVisible, unmount } = view();
    expect(onVisible).toHaveBeenLastCalledWith(true);
    unmount();
    expect(onVisible).toHaveBeenLastCalledWith(false);
  });

  it('lists Done and failed last, counting its unread, folding as the others do, and opens at it (NOTIFY2f)', async () => {
    const notices: Notice[] = [
      { id: 'ended-s1', kind: 'ended', at: '2026-10-21T09:00:00.000Z', read: false, session: 's1', repository: 'Engine', state: 'failed' },
      { id: 'ended-s2', kind: 'ended', at: '2026-10-21T08:00:00.000Z', read: true, session: 's2', repository: 'Engine', state: 'completed' },
    ];
    const onDismiss = vi.fn();
    view({ notices, onDismiss });

    const groups = screen.getAllByRole('region');
    expect(groups.map((group) => group.getAttribute('aria-label'))).toEqual(['Waiting for your confirmation', 'What needs you', 'Done and failed']);
    expect(within(groups[2]!).getByRole('button', { name: /Done and failed/ })).toHaveTextContent('1');
    expect(within(groups[2]!).getAllByRole('listitem')).toHaveLength(2);
    await userEvent.click(within(groups[2]!).getAllByRole('button', { name: 'Dismiss' })[1]!);
    expect(onDismiss).toHaveBeenCalledWith(notices[1]);

    await userEvent.click(within(groups[2]!).getByRole('button', { name: /Done and failed/ }));
    expect(within(screen.getAllByRole('region')[2]!).queryAllByRole('listitem')).toHaveLength(0);
    expect(JSON.parse(window.sessionStorage.getItem('daoris.notifications.folded')!)).toEqual(['done']);
  });

  it('says nothing needs you above what ended, which waits on nobody', () => {
    view({
      presses: [], needs: 0, working: 0,
      notices: [{ id: 'expired-p1', kind: 'expired', at: '2026-10-21T09:00:00.000Z', read: true, route: 'SESSION_INPUT' }],
    });

    expect(screen.getByText('Nothing needs you')).toBeInTheDocument();
    expect(screen.getAllByRole('region').map((group) => group.getAttribute('aria-label'))).toEqual(['Done and failed']);
  });

  /** NOTIFY2g (§2.2's third group, §2.3): one item per ask with anything unread, a teammate's among them, and each unread answer. */
  describe('Messages', () => {
    const NEWEST = { id: 'm2', at: '2026-10-21T09:20:00.000Z', who: 'agent' as const, repository: 'db', line: 'Which column holds the totals?', level: 'urgent' as const, toEveryone: true };
    const ASKS: AskMessages[] = [
      { ask: 'a1', workspace: 'default', teammate: false, unread: 2, newest: NEWEST },
      { ask: '7e6f51', workspace: 'default', teammate: true, unread: 1, newest: { ...NEWEST, id: 'm9', at: '2026-10-21T09:10:00.000Z', who: 'teammate', level: 'ordinary' } },
      { ask: 'a3', workspace: 'default', teammate: false, unread: 0, newest: { ...NEWEST, id: 'm1' } },
    ];
    const ANSWERED: Notice[] = [
      { id: 'answered-c1', kind: 'answered', at: '2026-10-21T09:30:00.000Z', read: false, session: 'c1', repository: 'game' },
      { id: 'answered-c2', kind: 'answered', at: '2026-10-21T09:00:00.000Z', read: true, session: 'c2', repository: 'web' },
    ];
    const messages = (props: Partial<Parameters<typeof NotificationsView>[0]> = {}) => {
      const acts = { onOpenAsk: vi.fn(), onMarkAsk: vi.fn(), onOpenAnswer: vi.fn(), onRead: vi.fn() };
      const drawn = view({
        messages: ASKS, answered: ANSWERED, messageNames: { ask: (id) => (id === 'a1' ? 'Export the feed as CSV' : null) }, ...acts, ...props,
      });
      return { ...drawn, ...acts };
    };
    const group = () => screen.getByRole('region', { name: 'Messages' });

    it('lists Messages third, headed by its unread, one item per ask with anything unread and each unread answer', () => {
      messages({ notices: [{ id: 'ended-s1', kind: 'ended', at: '2026-10-21T09:00:00.000Z', read: false, repository: 'Engine', state: 'failed' }] });

      expect(screen.getAllByRole('region').map((each) => each.getAttribute('aria-label')))
        .toEqual(['Waiting for your confirmation', 'What needs you', 'Messages', 'Done and failed']);
      expect(within(group()).getByRole('button', { name: /^Messages/ })).toHaveTextContent('4');
      const items = within(group()).getAllByRole('listitem');
      expect(items.map((item) => item.querySelector('[data-message-name]')!.textContent)).toEqual([
        'A conversation in game answered you', 'Export the feed as CSV', 'A teammate’s ask #7e6f51',
      ]);
      expect(items[1]).toHaveTextContent('2 unread');
      expect(items[1]).toHaveTextContent('db');
      expect(items[1]).toHaveTextContent('Which column holds the totals?');
      expect(items[2]).toHaveTextContent('A teammate');
      // Being listed reads nothing.
      expect(screen.queryByText('Nothing unread.')).toBeNull();
    });

    it("opens an ask's channel, and marks it read through its newest message", async () => {
      const { onOpenAsk, onMarkAsk, onRead } = messages();
      expect(onRead).not.toHaveBeenCalled();

      const item = within(group()).getAllByRole('listitem')[1]!;
      await userEvent.click(within(item).getByRole('button', { name: 'Open' }));
      expect(onOpenAsk).toHaveBeenCalledWith(ASKS[0]);
      await userEvent.click(within(item).getByRole('button', { name: 'Mark read' }));
      expect(onMarkAsk).toHaveBeenCalledWith(ASKS[0]);
      expect(onRead).not.toHaveBeenCalled();
    });

    it("opens an answer's conversation and reads it, or reads it by Mark read", async () => {
      const { onOpenAnswer, onRead } = messages();

      const item = within(group()).getAllByRole('listitem')[0]!;
      await userEvent.click(within(item).getByRole('button', { name: 'Open' }));
      expect(onRead).toHaveBeenLastCalledWith(['answered-c1']);
      expect(onOpenAnswer).toHaveBeenCalledWith(ANSWERED[0]);
      onRead.mockClear();
      await userEvent.click(within(item).getByRole('button', { name: 'Mark read' }));
      expect(onRead).toHaveBeenLastCalledWith(['answered-c1']);
    });

    it('marks every ask and answer it lists by Mark all read', async () => {
      const { onMarkAsk, onRead } = messages();

      await userEvent.click(within(group()).getByRole('button', { name: 'Mark all read' }));
      expect(onMarkAsk.mock.calls.map(([entry]) => entry.ask)).toEqual(['a1', '7e6f51']);
      expect(onRead).toHaveBeenCalledWith(['answered-c1']);
    });

    it('draws no group with nothing unread, and says nothing is unread where nothing needs you either', () => {
      messages({ presses: [], needs: 0, messages: ASKS.map((entry) => ({ ...entry, unread: 0 })), answered: [ANSWERED[1]!] });

      expect(screen.queryByRole('region', { name: 'Messages' })).toBeNull();
      expect(screen.getByText('Nothing needs you')).toBeInTheDocument();
      expect(screen.getByText('Nothing unread.')).toBeInTheDocument();
    });

    it('opens at Messages from a door', () => {
      const scrolled = vi.fn();
      Element.prototype.scrollIntoView = scrolled;
      window.sessionStorage.setItem('daoris.notifications.folded', JSON.stringify(['messages']));
      messages({ at: { group: 'messages', n: 1 } });

      expect(within(group()).getAllByRole('listitem')).toHaveLength(3);
      expect(scrolled.mock.contexts[0]).toBe(document.getElementById('notifications-messages'));
    });

    it('says Messages in Chinese, what an agent wrote as written', async () => {
      await i18n.changeLanguage('zh');
      messages();

      const zh = screen.getByRole('region', { name: '消息' });
      const items = within(zh).getAllByRole('listitem');
      expect(items[0]!.querySelector('[data-message-name]')!.textContent).toBe('game 的一个对话回复了你');
      expect(items[1]).toHaveTextContent('2 条未读');
      expect(items[1]).toHaveTextContent('Which column holds the totals?');
      expect(within(zh).getByRole('button', { name: '全部标为已读' })).toBeInTheDocument();
      expect(within(items[1]!).getByRole('button', { name: '标为已读' })).toBeInTheDocument();
    });
  });

  it('says its groups in Chinese', async () => {
    await i18n.changeLanguage('zh');
    view({ notices: [{ id: 'ended-s1', kind: 'ended', at: '2026-10-21T09:00:00.000Z', read: false, repository: 'Engine', state: 'failed' }] });

    expect(screen.getAllByRole('region').map((group) => group.getAttribute('aria-label'))).toEqual(['等待你确认', '等你处理', '完成与出错']);
  });
});
