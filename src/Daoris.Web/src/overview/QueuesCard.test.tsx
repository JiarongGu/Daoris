import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { readQueueRows } from './queuePlaces';
import { QueuesCard } from './QueuesCard';

// The machine's queues as `QUEUES` answers them: an ordinary check and an urgent one, and two starts behind the spent cap.
// The card composes each place with the page's twin of the comparator, and the press sets a class through the person's route.

const ROWS = readQueueRows({
  rows: [
    { queue: 'checks', item: 'a', class: 'normal', stage: 'first-checks', arrived: '2026-11-26T10:00:00Z', order: 0, source: 'unset', repository: 'Daoris', code: 'slot', values: { running: '2', slots: '2' }, reason: 'Waits for a check slot: 2 of 2 run' },
    { queue: 'checks', item: 'u', class: 'urgent', stage: 'first-checks', arrived: '2026-11-26T10:20:00Z', order: 1, source: 'quest', by: 'the person', why: 'the fix', repository: 'Daoris', code: 'slot', values: { running: '2', slots: '2' }, reason: 'Waits for a check slot: 2 of 2 run' },
    { queue: 'starts', item: 'q2', class: 'high', stage: 'new-start', arrived: '2026-11-26T09:00:00Z', order: 1, source: 'ask', by: 'session s-1', why: 'the release', ask: 'cafee7', repository: 'engine', code: 'cap', verdict: 'AtCapacity', values: { cap: '2' }, reason: 'The cap is spent.' },
    { queue: 'starts', item: 'q1', class: 'high', stage: 'resume', arrived: '2026-11-26T11:00:00Z', order: 0, source: 'unset', agedFrom: 'normal', repository: 'engine', code: 'cap', verdict: 'AtCapacity', values: { cap: '2' }, reason: 'The cap is spent.' },
  ],
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

/** The item's line whose words hold `has`, read whole: a line's ids are code within it. */
const line = (has: string): string => screen.getAllByRole('listitem').map((each) => each.textContent ?? '').find((text) => text.includes(has))!;

describe('the queues card', () => {
  it('gives each item its place, its class and where it comes from, its stage, why it waits and what is ahead', () => {
    render(<QueuesCard rows={ROWS} onSet={() => {}} />);

    const checks = within(screen.getByRole('region', { name: 'Checks waiting for a slot' })).getAllByRole('listitem').map((each) => each.textContent);
    expect(checks[0]).toMatch(/^1 of 2the checks of #uurgent \(set by you: the fix\)first checks/);
    expect(checks[0]).toContain('Every check slot runs: 2 of 2.');
    expect(checks[1]).toMatch(/^2 of 2the checks of #a/);
    expect(checks[1]).toContain('Ahead of it: the checks of #u, urgent (set by you: the fix), first checks');

    // A resume before a new start of its class, whatever their arrivals.
    expect(line('#q1')).toMatch(/^1 of 2quest #q1high \(aged from normal\)a session going on/);
    expect(line('#q1')).toContain('The concurrency cap (2) is spent');
    expect(line('#q2')).toMatch(/^2 of 2quest #q2high \(from its ask #cafee7, set by session s-1: the release\)a new start/);
  });

  it('says the same places in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<QueuesCard rows={ROWS} onSet={() => {}} />);

    expect(line('#u 的检查')).toMatch(/^第 1 位，共 2 位#u 的检查加急（由你设置：the fix）首次检查/);
    expect(line('#a 的检查')).toContain('排在它前面的：#u 的检查，加急（由你设置：the fix），首次检查');
    expect(line('委托 #q1')).toMatch(/^第 1 位，共 2 位委托 #q1高（由常规因久等而提升）会话继续/);
  });

  it('sets a class with why through its press, and says the driver\'s refusal in its words', async () => {
    const onSet = vi.fn();
    render(<QueuesCard rows={ROWS} onSet={onSet} />);

    await userEvent.click(screen.getByRole('button', { name: 'Set the priority of the checks of #a' }));
    await userEvent.click(screen.getByRole('radio', { name: 'urgent' }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Why' }), 'it unblocks the release');
    await userEvent.type(screen.getByRole('textbox', { name: 'Ends' }), '2h');
    await userEvent.click(screen.getByRole('button', { name: 'Set' }));

    expect(onSet).toHaveBeenCalledTimes(1);
    const [change, answered] = onSet.mock.calls[0]!;
    expect(change).toEqual({ quest: 'a', class: 'urgent', why: 'it unblocks the release', until: '2h' });
    answered.refused('not set: the workspace games holds 2 urgent at once, its cap of 2.');
    expect(await screen.findByText('not set: the workspace games holds 2 urgent at once, its cap of 2.')).toBeTruthy();
  });

  it('says nothing waits, and draws placeholders before the first reading', () => {
    const { unmount } = render(<QueuesCard rows={[]} onSet={() => {}} />);
    expect(screen.getByText('Nothing waits in the machine\'s queues.')).toBeTruthy();
    unmount();

    render(<QueuesCard rows={undefined} onSet={() => {}} />);
    expect(screen.queryByText('Nothing waits in the machine\'s queues.')).toBeNull();
  });

  it('says the driver\'s refusal of the read where its rows would be', () => {
    render(<QueuesCard rows={undefined} refused="Daoris is coming up." onSet={() => {}} />);
    expect(screen.getByRole('alert').textContent).toBe('Daoris is coming up.');
  });
});
