import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
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

  it('says its groups in Chinese', async () => {
    await i18n.changeLanguage('zh');
    view();

    expect(screen.getAllByRole('region').map((group) => group.getAttribute('aria-label'))).toEqual(['等待你确认', '等你处理']);
  });
});
