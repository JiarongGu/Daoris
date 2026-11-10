import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

// What Ask Daoris pressed for the person in full access, with no card: a group in Notifications beside the steward's line, each press
// in the window's own words for its route, its target, time and outcome, a press to open its conversation and, for a pause, *Resume*.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke),
  useShenoraEvent: () => undefined,
}));

import i18n from '../locales/i18n';
import { standingOf } from '../bridge/person';
import { type HelpPressNow, type StandingConfirmationNow, helpReverse } from '../person/standing';
import { HelpPressLine } from './HelpPressLine';
import { NotificationsStanding } from './NotificationsStanding';

const press = (id: string, route: string, more: Partial<HelpPressNow> = {}): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route, session: 's1a2b3c4', scope: null, target: null, at: '2026-11-10T10:00:00Z', outcome: 'done', ...more,
});
/** This run's presses, newest first, as the shell sends them. */
const PRESSES: HelpPressNow[] = [
  press('h2', 'SET_HOLD', { target: 'engine', at: '2026-11-10T10:02:00Z', outcome: 'failed' }),
  press('h1', 'WORK_PAUSE', { scope: 'quest', target: 'q1' }),
];
const STANDING: StandingConfirmationNow = { holds: [], holders: [], stewardActs: [], stewardDesk: [], stewardLands: [], helpPresses: PRESSES };

const group = () => within(screen.getByRole('region', { name: 'What Ask Daoris pressed' }));
const rows = () => within(screen.getByRole('list', { name: 'Presses Ask Daoris made' })).getAllByRole('listitem');

describe("Notifications' group of what Ask Daoris pressed", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('draws nothing where Ask Daoris pressed nothing this run', () => {
    render(<HelpPressLine standing={{ ...STANDING, helpPresses: [] }} onOpen={vi.fn()} onReverse={vi.fn()} />);
    expect(document.querySelector('[data-help-press-line]')).toBeNull();
  });

  it('reads the presses field by field and leaves out one it cannot read', () => {
    expect(standingOf({ helpPresses: [{ id: 'a', module: 'DAORIS.DRIVER', route: 'WORK_PAUSE', session: 's1', at: '2026-11-10T10:00:00Z', outcome: 'done', target: 'q1', scope: 'quest' }, { id: 'b' }, 'x'] })?.helpPresses)
      .toEqual([{ id: 'a', module: 'DAORIS.DRIVER', route: 'WORK_PAUSE', session: 's1', at: '2026-11-10T10:00:00Z', outcome: 'done', target: 'q1', scope: 'quest' }]);
    expect(standingOf({})?.helpPresses).toEqual([]);
  });

  it('offers a reverse press only for a pause that was done', () => {
    expect(helpReverse(PRESSES[1]!)).toBe('resume');
    expect(helpReverse({ ...PRESSES[1]!, outcome: 'failed' })).toBeNull();
    expect(helpReverse(PRESSES[0]!)).toBeNull();
  });

  it.each([
    ['en', 'Ask Daoris pressed 2 acts for you in full access, with no card, since Daoris started', 'Show what Ask Daoris pressed',
      ['hold a repository\'s work, or let it go', 'pause the work on this machine'], ['Not done', 'Done'], 'Open the conversation', 'Resume'],
    ['zh', '自 Daoris 启动以来，Ask Daoris 已在完全访问下为你直接执行 2 项操作，未经确认卡', '显示 Ask Daoris 代按的操作',
      ['暂停一个仓库的工作，或将其放开', '暂缓本机上的这项工作'], ['未完成', '已完成'], '打开该对话', '恢复'],
  ])('lists each press with its words, outcome, conversation and reverse press (%s)', async (language, said, show, words, outcomes, open, resume) => {
    await i18n.changeLanguage(language);
    const onOpen = vi.fn();
    const onReverse = vi.fn();
    render(<HelpPressLine standing={STANDING} onOpen={onOpen} onReverse={onReverse} />);
    const region = within(screen.getByRole('region'));
    expect(region.getByText(said)).toBeInTheDocument();
    await userEvent.click(region.getByRole('button', { name: show }));

    const list = screen.getAllByRole('listitem');
    expect(list.map((row) => row.querySelector('span span')?.textContent)).toEqual(words);
    expect(list.map((row) => row.querySelector('[data-help-outcome]')?.textContent)).toEqual(outcomes);
    // Both rows open their conversation; only the done pause has a reverse.
    expect(list.map((row) => within(row).queryAllByRole('button').length)).toEqual([1, 2]);
    await userEvent.click(within(list[1]!).getByText(open));
    expect(onOpen).toHaveBeenCalledWith(PRESSES[1]);
    await userEvent.click(within(list[1]!).getByText(resume));
    expect(onReverse).toHaveBeenCalledWith(PRESSES[1]);
    expect(screen.getByRole('region').textContent).not.toMatch(/\{\{|undefined/);
  });

  it("as Notifications holds it, opens Ask Daoris's conversation and sends a pause's resume as the work pages do", async () => {
    invoke.mockImplementation(async () => ({ drivable: [], holds: [], trees: [], running: [], standingConfirmation: STANDING }));
    const onOpenHelp = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <NotificationsStanding notify={() => {}} onOpenHelp={onOpenHelp} />
      </QueryClientProvider>);

    await userEvent.click(await screen.findByRole('button', { name: 'Show what Ask Daoris pressed' }));
    const pause = rows()[1]!;
    await userEvent.click(within(pause).getByRole('button', { name: 'Open the conversation that pressed: pause the work on this machine' }));
    expect(onOpenHelp).toHaveBeenCalledTimes(1);
    await userEvent.click(within(pause).getByRole('button', { name: 'Take back: pause the work on this machine' }));
    await vi.waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'WORK_RESUME').map(([, , request]) => request?.payload))
      .toEqual([{ quest: 'q1' }]));
    expect(group().getByText('Ask Daoris pressed 2 acts for you in full access, with no card, since Daoris started')).toBeInTheDocument();
  });
});
