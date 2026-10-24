import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';

// The standing confirmation's line in *Notifications* (TERMTRUST1a, D201 point 7; the terminal-trust design §6.2): at the head of the
// presses' group while it is on, naming the presses granted and only those, since when and how many it covered, with **Turn off**,
// which sends none, and *Open This Daoris*. A line, not a row: no group's count and never the bell's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import type { Confirmation } from '../person/Confirmations';
import type { StandingConfirmationNow } from '../person/standing';
import { NotificationsStanding } from './NotificationsStanding';
import { NotificationsView } from './NotificationsView';
import { StandingLine } from './StandingLine';

const SINCE = '2026-10-23T09:30:00Z';
const ON: StandingConfirmationNow = {
  offered: true, on: true, presses: ['stop', 'finish'], built: ['stop', 'finish', 'hold', 'resume'], since: SINCE, unread: null,
  unwritten: false, used: 3, held: [], appointmentsOffered: false, appointments: [], supervisorOffered: false, stewardOffered: false, holders: [],
};
const OFF: StandingConfirmationNow = { ...ON, on: false, presses: [], since: null, used: 0 };

const PRESS: Confirmation = {
  source: 'pipe', id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_DELETE', fields: [{ name: '/id', type: 'string', value: 's1' }],
  named: [], asker: null, expires: '2026-10-23T09:40:00Z',
};

describe("Notifications' standing confirmation line", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('names the granted presses only, since when and how many it covered, with Turn off and Open This Daoris', async () => {
    const onTurnOff = vi.fn();
    const onOpen = vi.fn();
    render(<StandingLine standing={ON} onTurnOff={onTurnOff} onOpen={onOpen} />);

    expect(screen.getByText(`These presses from a terminal run without asking you: stop a session and finish a session that waits on you, without your words. Standing confirmation since ${moment(SINCE)}, used 3 times since Daoris started.`))
      .toBeInTheDocument();
    expect(screen.queryByText(/hold a repository|resume a repository/)).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Turn off' }));
    await userEvent.click(screen.getByRole('button', { name: 'Open This Daoris' }));
    expect(onTurnOff).toHaveBeenCalledTimes(1);
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('is drawn only while it is on', () => {
    const { container, rerender } = render(<StandingLine standing={OFF} onTurnOff={() => {}} onOpen={() => {}} />);
    expect(container.innerHTML).toBe('');
    rerender(<StandingLine standing={null} onTurnOff={() => {}} onOpen={() => {}} />);
    expect(container.innerHTML).toBe('');
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<StandingLine standing={{ ...ON, used: 1 }} onTurnOff={() => {}} onOpen={() => {}} />);

    expect(screen.getByText(`终端的这些操作无需问你即可执行：停止会话和完成一个在等你的会话（不附你的话）。自 ${moment(SINCE, 'zh')} 起长期确认，本次启动以来使用 1 次。`))
      .toBeInTheDocument();
    expect(screen.getByRole('button', { name: '停用' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '打开本机' })).toBeInTheDocument();
  });

  it('heads the presses’ group, and counts toward no group, with a press waiting or none', () => {
    const line = <StandingLine standing={ON} onTurnOff={() => {}} onOpen={() => {}} />;
    const props = { needs: 0, working: 0, attention: <p>rows</p>, onAnswer: async () => ({ answered: true as const }), standing: line };
    const { rerender } = render(<NotificationsView presses={[PRESS]} {...props} />);

    const regions = screen.getAllByRole('region');
    expect(regions.map((region) => region.getAttribute('aria-label'))).toEqual(['Standing confirmation', 'Waiting for your confirmation']);
    // The presses' group counts its one press, never the line.
    expect(within(regions[1]!).getByText('1')).toBeInTheDocument();

    // Nothing waiting: the line is still there to turn it off, and nothing needs the person.
    rerender(<NotificationsView presses={[]} {...props} />);
    expect(screen.getAllByRole('region').map((region) => region.getAttribute('aria-label'))).toEqual(['Standing confirmation']);
    expect(screen.getByText('Nothing needs you')).toBeInTheDocument();
  });

  it("as Notifications holds it, reads the driver's state and Turn off sends no presses", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STATE'
      ? { drivable: [], holds: [], trees: [], running: [], standingConfirmation: ON }
      : type === 'SET_STANDING_CONFIRMATION' ? { done: true } : null));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <NotificationsStanding notify={() => {}} onOpen={() => {}} />
      </QueryClientProvider>);

    await userEvent.click(await screen.findByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STANDING_CONFIRMATION', { payload: { presses: [] } }));
  });

  it("says a Turn off refused under the line, in the catalogue's words", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'STATE'
      ? { drivable: [], holds: [], trees: [], running: [], standingConfirmation: ON }
      : type === 'SET_STANDING_CONFIRMATION' ? { done: false, why: 'window-only', message: 'No.' } : null));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <NotificationsStanding notify={() => {}} onOpen={() => {}} />
      </QueryClientProvider>);

    await userEvent.click(await screen.findByRole('button', { name: 'Turn off' }));
    expect((await screen.findByRole('alert')).textContent).toBe("Only Daoris's window grants or revokes the standing confirmation.");
  });
});
