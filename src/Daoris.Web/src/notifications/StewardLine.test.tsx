import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

// The steward's line in *Notifications* (STEWARDROLE1b, D209; the steward design §5.3, §5.4, §8): *Steward appointed · n acts since
// Daoris started · Turn off*, opening to the run's acts newest first, each its act in words, its target, its time and the mark *By the
// steward, on your behalf*, and only while it stands, the owner's own reverse press: *Stop driving* for a drive, *Drive* for an undrive,
// *Trees off* for trees, *Resume* for a hold, *Hold* for a resume, *Try again* for a stop; none for a finish or a Try again. A stop whose
// quest still waits on a Try again is said as *Stopped by the steward*, with its press. A line, not a row: no notice per act, no count.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../locales/i18n';
import { clockOf } from '../locales/format';
import type { AppointmentNow, DeskRequestNow, StandingConfirmationNow, StewardActNow } from '../person/standing';
import { NotificationsStanding } from './NotificationsStanding';
import { StewardLine } from './StewardLine';

const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];
const STEWARD: AppointmentNow = {
  role: 'steward', repository: '', holder: 'terminal', since: '2026-10-25T09:00:00Z', dormant: false, answers: 0, scope: 'machine',
  holderId: 'h1', acts: OFFERED, setup: false, workspace: '', window: 5,
};
const act = (id: string, kind: string, at: string, more: Partial<StewardActNow> = {}): StewardActNow => ({
  id, act: kind, repository: null, quest: null, session: null, at, standing: true, waiting: false, ...more,
});
/** This run's acts, newest first, as the shell sends them. */
const ACTS: StewardActNow[] = [
  act('a8', 'retry', '2026-10-25T10:08:00Z', { quest: 'q3' }),
  act('a7', 'finish', '2026-10-25T10:07:00Z', { quest: 'q2', session: 's2' }),
  act('a6', 'stop', '2026-10-25T10:06:00Z', { quest: 'q1', session: 's1', waiting: true }),
  act('a5', 'resume', '2026-10-25T10:05:00Z', { repository: 'atlas' }),
  act('a4', 'hold', '2026-10-25T10:04:00Z', { repository: 'atlas', standing: false }),
  act('a3', 'trees', '2026-10-25T10:03:00Z', { repository: 'engine' }),
  act('a2', 'undrive', '2026-10-25T10:02:00Z', { repository: 'atlas' }),
  act('a1', 'drive', '2026-10-25T10:01:00Z', { repository: 'engine' }),
];
const STANDING: StandingConfirmationNow = {
  offered: false, on: false, presses: [], built: [], since: null, unread: null, unwritten: false, used: 0, held: [],
  appointmentsOffered: true, supervisorOffered: false, stewardOffered: true, appointments: [STEWARD], holders: [],
  stewardActsOffered: OFFERED, stewardActs: ACTS, stewardDesk: [],
};
/** The steward's desk this run, newest first (STEWARDROLE1c): one waiting, one declined with a reason, one answered by an act. */
const DESK: DeskRequestNow[] = [
  {
    id: 'r3', act: 'retry', target: '#q3', why: 'the outage passed <b>now</b>', filed: '2026-10-25T10:09:00Z', state: 'open', closed: null,
    answeredBy: null, reason: null,
  },
  {
    id: 'r2', act: 'hold', target: 'atlas', why: 'atlas is mid-release', filed: '2026-10-25T10:05:00Z', state: 'declined',
    closed: '2026-10-25T10:06:00Z', answeredBy: null, reason: 'the owner holds atlas during a release',
  },
  {
    id: 'r1', act: 'trees', target: 'engine', why: 'engine needs trees', filed: '2026-10-25T10:02:00Z', state: 'answered',
    closed: '2026-10-25T10:03:00Z', answeredBy: 'a3', reason: null,
  },
];
const desk = () => within(screen.getByRole('list', { name: "The steward's desk" })).getAllByRole('listitem');

const line = () => within(screen.getByRole('region', { name: 'The steward' }));
const acts = () => within(screen.getByRole('list', { name: "The steward's acts" })).getAllByRole('listitem');

describe("Notifications' steward line", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('draws nothing where the build does not offer the steward, or where none is appointed and it made no act', () => {
    const { rerender } = render(<StewardLine standing={{ ...STANDING, stewardOffered: false }} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    expect(document.querySelector('[data-steward-line]')).toBeNull();
    rerender(<StewardLine standing={{ ...STANDING, appointments: [], stewardActs: [] }} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    expect(document.querySelector('[data-steward-line]')).toBeNull();
    rerender(<StewardLine standing={null} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    expect(document.querySelector('[data-steward-line]')).toBeNull();
  });

  it('says the appointment and its acts since Daoris started, and Turn off turns it off at once', async () => {
    const onTurnOff = vi.fn();
    render(<StewardLine standing={STANDING} onTurnOff={onTurnOff} onReverse={vi.fn()} />);
    expect(line().getByText('Steward appointed · 8 acts since Daoris started')).toBeInTheDocument();
    // Folded until opened: the acts are one press away.
    expect(screen.queryByRole('list', { name: "The steward's acts" })).toBeNull();
    await userEvent.click(line().getByRole('button', { name: 'Turn off' }));
    expect(onTurnOff).toHaveBeenCalledTimes(1);
  });

  it('opens to the acts newest first, each in words with its target, time and mark', async () => {
    render(<StewardLine standing={STANDING} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    await userEvent.click(line().getByRole('button', { name: "Show the steward's acts" }));

    const rows = acts();
    expect(rows.map((row) => row.querySelector('[data-steward-act]')?.textContent)).toEqual([
      'Tried again', 'Finished', 'Stopped', 'Resumed', 'Held', 'Gave trees', 'Stopped driving', 'Drove',
    ]);
    expect(within(rows[0]!).getByText('#q3')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('session s2 of #q2')).toBeInTheDocument();
    expect(within(rows[7]!).getByText('engine')).toBeInTheDocument();
    expect(within(rows[7]!).getByText(clockOf('2026-10-25T10:01:00Z'))).toBeInTheDocument();
    for (const row of rows) expect(within(row).getByText('By the steward, on your behalf')).toBeInTheDocument();
  });

  it('offers the owner’s reverse press only while an act stands, and none for a finish or a Try again', async () => {
    const onReverse = vi.fn();
    render(<StewardLine standing={STANDING} onTurnOff={vi.fn()} onReverse={onReverse} />);
    await userEvent.click(line().getByRole('button', { name: "Show the steward's acts" }));

    const rows = acts();
    const presses = rows.map((row) => within(row).queryAllByRole('button').map((button) => button.textContent));
    expect(presses).toEqual([[], [], ['Try again'], ['Hold'], [], ['Trees off'], ['Drive'], ['Stop driving']]);

    await userEvent.click(within(rows[7]!).getByRole('button', { name: 'Stop driving' }));
    expect(onReverse).toHaveBeenCalledWith(ACTS[7]);
  });

  it('says a stop whose quest waits on a Try again as Stopped by the steward, with its press', async () => {
    const onReverse = vi.fn();
    render(<StewardLine standing={STANDING} onTurnOff={vi.fn()} onReverse={onReverse} />);
    const waiting = within(line().getByRole('list', { name: 'Stopped by the steward' }));
    expect(waiting.getByText('session s1 of #q1')).toBeInTheDocument();
    await userEvent.click(waiting.getByRole('button', { name: 'Try again' }));
    expect(onReverse).toHaveBeenCalledWith(ACTS[2]);
  });

  it('says it all in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<StewardLine standing={STANDING} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    const region = within(screen.getByRole('region', { name: '管家' }));
    expect(region.getByText('已任命管家 · 自 Daoris 启动以来 8 项操作')).toBeInTheDocument();
    expect(region.getByRole('list', { name: '管家已停止' })).toBeInTheDocument();
    await userEvent.click(region.getByRole('button', { name: '显示管家的操作' }));
    expect(region.getAllByText('管家代你')).toHaveLength(8);
    expect(region.getByRole('button', { name: '停止驱动' })).toBeInTheDocument();
    expect(region.getAllByRole('button', { name: '重试' })).toHaveLength(2);
    expect(region.getByRole('button', { name: '停用' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: '管家' }).textContent).not.toMatch(/这台机器|渠道|总管|任务|\{\{|undefined/);
  });

  // STEWARDROLE1c (D209 §6.2): the desk under the line, folded until opened, each request its caller's words as written, marked as
  // granting nothing, and how the steward closed it; it offers the owner no press.
  it("says the steward's desk under the line, each request as its caller wrote it and how the steward closed it", async () => {
    render(<StewardLine standing={{ ...STANDING, stewardDesk: DESK }} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    expect(line().getByText("The steward's desk · 1 waiting")).toBeInTheDocument();
    expect(screen.queryByRole('list', { name: "The steward's desk" })).toBeNull();
    await userEvent.click(line().getByRole('button', { name: "Show the steward's desk" }));

    const rows = desk();
    expect(rows).toHaveLength(3);
    expect(within(rows[0]!).getByText('Try again')).toBeInTheDocument();
    expect(within(rows[0]!).getByText('#q3')).toBeInTheDocument();
    expect(within(rows[0]!).getByText(clockOf('2026-10-25T10:09:00Z'))).toBeInTheDocument();
    // The caller's words as written, never read as markup.
    expect(rows[0]!.querySelector('[data-desk-why]')?.textContent).toBe('the outage passed <b>now</b>');
    expect(rows[0]!.querySelector('b')).toBeNull();
    expect(within(rows[0]!).getByText('Waiting for the steward')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('Declined by the steward')).toBeInTheDocument();
    expect(within(rows[1]!).getByText("The steward's reason: the owner holds atlas during a release")).toBeInTheDocument();
    expect(within(rows[2]!).getByText('Done by the steward')).toBeInTheDocument();
    for (const row of rows) {
      expect(within(row).getByText('Words of whoever filed it, which grant nothing')).toBeInTheDocument();
      expect(within(row).queryAllByRole('button')).toEqual([]);
    }
  });

  it('draws the line for the desk alone, where none is appointed and the steward made no act', () => {
    render(<StewardLine standing={{ ...STANDING, appointments: [], stewardActs: [], stewardDesk: DESK }} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    expect(line().getByText("The steward's desk · 1 waiting")).toBeInTheDocument();
    expect(line().queryByRole('button', { name: 'Turn off' })).toBeNull();
  });

  it('says the desk in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<StewardLine standing={{ ...STANDING, stewardDesk: DESK }} onTurnOff={vi.fn()} onReverse={vi.fn()} />);
    const region = within(screen.getByRole('region', { name: '管家' }));
    expect(region.getByText('管家的待办 · 1 项等管家处理')).toBeInTheDocument();
    await userEvent.click(region.getByRole('button', { name: '显示管家的待办' }));
    const rows = within(region.getByRole('list', { name: '管家的待办' })).getAllByRole('listitem');
    expect(within(rows[0]!).getByText('重试')).toBeInTheDocument();
    expect(within(rows[0]!).getByText('等管家处理')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('管家已谢绝')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('管家的理由：the owner holds atlas during a release')).toBeInTheDocument();
    expect(within(rows[2]!).getByText('管家已办')).toBeInTheDocument();
    expect(region.getAllByText('提出者自己的话，不授予任何权限')).toHaveLength(3);
    expect(screen.getByRole('region', { name: '管家' }).textContent).not.toMatch(/这台机器|渠道|总管|任务|请求|拒绝|\{\{|undefined/);
  });

  it("as Notifications holds it, sends each reverse press as the owner's ordinary route, and Turn off as the appointment's", async () => {
    // A change answers the driver's whole state, as the shell's does, its standing confirmation in it.
    const state = { drivable: [], holds: [], trees: [], running: [], standingConfirmation: STANDING };
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SET_APPOINTMENT' ? { done: true } : state));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <NotificationsStanding notify={() => {}} onOpen={() => {}} />
      </QueryClientProvider>);

    await userEvent.click(await screen.findByRole('button', { name: "Show the steward's acts" }));
    const rows = acts();
    for (const [row, press] of [[7, 'Stop driving'], [6, 'Drive'], [5, 'Trees off'], [3, 'Hold'], [2, 'Try again']] as const) {
      await userEvent.click(within(rows[row]!).getByRole('button', { name: press }));
    }
    await userEvent.click(screen.getByRole('button', { name: 'Turn off' }));
    const sent = () => invoke.mock.calls.filter(([, type]) => type !== 'STATE').map(([, type, request]) => [type, request?.payload]);
    await vi.waitFor(() => expect(sent()).toEqual([
      ['SET_DRIVABLE', { repository: 'engine', drivable: false }],
      ['SET_DRIVABLE', { repository: 'atlas', drivable: true }],
      ['SET_TREES', { repository: 'engine', ownTree: false }],
      ['SET_HOLD', { repository: 'atlas', held: true }],
      ['RETRY_QUEST', { quest: 'q1' }],
      ['SET_APPOINTMENT', { role: 'steward', scope: 'machine', holder: 'terminal', on: false }],
    ]));
  });

  it('sends Resume for a standing hold', async () => {
    const state = {
      drivable: [], holds: [], trees: [], running: [],
      standingConfirmation: { ...STANDING, stewardActs: [act('h', 'hold', '2026-10-25T10:04:00Z', { repository: 'atlas' })] },
    };
    invoke.mockImplementation(async () => state);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <NotificationsStanding notify={() => {}} onOpen={() => {}} />
      </QueryClientProvider>);
    await userEvent.click(await screen.findByRole('button', { name: "Show the steward's acts" }));
    await userEvent.click(screen.getByRole('button', { name: 'Resume' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', { payload: { repository: 'atlas', held: false } }));
  });
});
