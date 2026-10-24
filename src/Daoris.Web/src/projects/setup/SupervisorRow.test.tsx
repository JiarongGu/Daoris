import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { afterEach, describe, expect, it, vi } from 'vitest';

// The supervisor's row in a repository's Setup → Driving (SUPERVISOR1a-ii, D208; the supervisor design §5, §6.4): not offered yet in
// this build; off, *Appoint…*; appointed, since when and how many answers, *Appoint again…* and *Turn off*; found as Daoris started,
// dormant. *Appoint* and *Appoint again* open the one ask naming the scope found, and nothing is sent before its *Appoint*. The key is
// never on the page: no answer the page receives holds one, and nothing is written to the clipboard.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => undefined,
}));

import { useSetAppointment } from '../../bridge/shell';
import i18n from '../../locales/i18n';
import { moment } from '../../locales/format';
import type { AppointmentNow } from '../../person/standing';
import { SupervisorRow, type SupervisorSetup } from './SupervisorRow';

const SINCE = '2026-10-24T14:02:00Z';
const LIVE: AppointmentNow = {
  role: 'supervisor', repository: 'engine', holder: 'terminal', since: SINCE, dormant: false, answers: 3, scope: 'repository:engine', holderId: 'h1',
  acts: [], setup: false,
  workspace: null, window: null,
};
const FOUND: AppointmentNow = { ...LIVE, dormant: true, answers: 0, holderId: null };

const setup = (overrides: Partial<SupervisorSetup> = {}): SupervisorSetup =>
  ({ offered: true, appointment: null, onAppoint: vi.fn(), onTurnOff: vi.fn(), ...overrides });

describe("Setup's supervisor row", () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says it is not offered yet in a build that does not offer it, and offers no Appoint', () => {
    render(<SupervisorRow repository="engine" supervisor={setup({ offered: false })} />);
    expect(screen.getByText(/Not offered yet/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Appoint…' })).toBeNull();
  });

  it('opens the ask naming its scope with its plain sentences, and appoints only on its own Appoint', async () => {
    const onAppoint = vi.fn();
    render(<SupervisorRow repository="engine" supervisor={setup({ onAppoint })} />);
    expect(screen.getByText('Not appointed: every question here waits for you.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    expect(onAppoint).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'appoint the supervisor for engine' });
    expect(within(ask).getByText('The supervisor for engine, on this machine, until Daoris restarts.')).toBeInTheDocument();
    expect(within(ask).getByText(/never copy the key out of it/)).toBeInTheDocument();
    expect(within(ask).getByText(/press Appoint again, and Daoris starts the session again in a new terminal/)).toBeInTheDocument();
    expect(within(ask).getByText(/It starts signed out: sign in once in its tab/)).toBeInTheDocument();
    // No holder lives: no choice of terminal is asked.
    expect(within(ask).queryByRole('radio')).toBeNull();
    expect(within(ask).getByText(/It never answers what is yours: a go-ahead, a sign-in/)).toBeInTheDocument();

    await userEvent.click(within(ask).getByRole('button', { name: 'Appoint' }));
    expect(onAppoint).toHaveBeenCalledTimes(1);
  });

  it('says an appointment since when and its answers this run, with Appoint again and Turn off', async () => {
    const onTurnOff = vi.fn();
    render(<SupervisorRow repository="engine" supervisor={setup({ appointment: LIVE, started: true, onTurnOff })} />);
    expect(screen.getByText(`Appointed since ${moment(SINCE)}, held by the terminal Daoris started at your appointment · 3 answers this run`))
      .toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent("Opened the supervisor's terminal in Daoris's terminal panel, running Daoris's own Claude Code");
    await userEvent.click(screen.getByRole('button', { name: 'Turn off' }));
    expect(onTurnOff).toHaveBeenCalledTimes(1);
  });

  it('says a found appointment dormant, never how it was written, and Appoint again opens the same ask naming the scope found', async () => {
    const onAppoint = vi.fn();
    render(<SupervisorRow repository="engine" supervisor={setup({ appointment: FOUND, onAppoint })} />);
    expect(screen.getByText('Found as Daoris started: dormant until you press Appoint again.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Appoint again…' }));
    // Nothing is installed before its Appoint: the press opened the ask and sent nothing.
    expect(onAppoint).not.toHaveBeenCalled();
    expect(screen.getByRole('group', { name: 'appoint the supervisor for engine' })).toBeInTheDocument();
  });

  // STEWARDROLE1a-i (D209 §2.2): while a holder lives, the ask offers adding the role to its terminal or a terminal of its own, neither
  // preselected, and Appoint waits for the choice; the choice is sent as the appointment's `share`.
  it('offers adding the role to the living terminal or one of its own, neither preselected, and sends the choice', async () => {
    const onAppoint = vi.fn();
    const holders = [{ id: 'h1', since: SINCE, roles: ['steward'] }];
    render(<SupervisorRow repository="engine" supervisor={setup({ onAppoint, holders })} />);
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    const ask = screen.getByRole('group', { name: 'appoint the supervisor for engine' });
    expect(within(ask).getByText(/reads the questions and messages of the agents it supervises while it can also drive, stop and hand over/))
      .toBeInTheDocument();
    const add = within(ask).getByRole('radio', { name: `Add to the terminal Daoris started at ${moment(SINCE)}` });
    const own = within(ask).getByRole('radio', { name: 'A terminal of its own' });
    expect(add).not.toBeChecked();
    expect(own).not.toBeChecked();
    expect(within(ask).getByRole('button', { name: 'Appoint' })).toBeDisabled();

    await userEvent.click(add);
    await userEvent.click(within(ask).getByRole('button', { name: 'Appoint' }));
    expect(onAppoint).toHaveBeenCalledWith(expect.anything(), 'h1', 5);
  });

  it('sends a terminal of its own as the share', async () => {
    invoke.mockResolvedValue({ done: true });
    function Pressed() {
      const appoint = useSetAppointment();
      return (
        <SupervisorRow
          repository="engine"
          supervisor={setup({
            holders: [{ id: 'h1', since: SINCE, roles: ['steward'] }],
            onAppoint: (answered, share) => appoint.mutate({ role: 'supervisor', repository: 'engine', on: true, share }, {
              onSuccess: () => answered.done(),
            }),
          })}
        />
      );
    }

    render(<QueryClientProvider client={new QueryClient()}><Pressed /></QueryClientProvider>);
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    await userEvent.click(screen.getByRole('radio', { name: 'A terminal of its own' }));
    await userEvent.click(screen.getByRole('button', { name: 'Appoint' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SET_APPOINTMENT', { payload: { role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: true, share: 'own' } }));
  });

  // SUPERVISOR1e (D208 §5, §6.3, §12): the overrule window the owner sets in the ask, 1 to 60 minutes, five unset; never 0.
  it('asks the overrule window, five unset, holds Appoint outside 1 to 60, and sends the window set', async () => {
    invoke.mockResolvedValue({ done: true });
    function Pressed() {
      const appoint = useSetAppointment();
      return (
        <SupervisorRow
          repository="engine"
          supervisor={setup({
            onAppoint: (answered, share, window) => appoint.mutate({ role: 'supervisor', repository: 'engine', on: true, share, window }, {
              onSuccess: () => answered.done(),
            }),
          })}
        />
      );
    }

    render(<QueryClientProvider client={new QueryClient()}><Pressed /></QueryClientProvider>);
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    const ask = screen.getByRole('group', { name: 'appoint the supervisor for engine' });
    const window = within(ask).getByRole('spinbutton', { name: 'Overrule window' });
    expect(window).toHaveValue(5);
    expect(within(ask).getByText(/held for the window you set here for your overrule/)).toBeInTheDocument();
    const appoint = within(ask).getByRole('button', { name: 'Appoint' });

    for (const refused of ['0', '61', '2.5', '']) {
      await userEvent.clear(window);
      if (refused) await userEvent.type(window, refused);
      expect(appoint).toBeDisabled();
      expect(within(ask).getByText('A whole number of minutes from 1 to 60.')).toBeInTheDocument();
    }

    await userEvent.clear(window);
    await userEvent.type(window, '30');
    expect(appoint).toBeEnabled();
    await userEvent.click(appoint);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SET_APPOINTMENT', { payload: { role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: true, window: 30 } }));
  });

  it('says the window an appointment set, and the ask opens on it', async () => {
    render(<SupervisorRow repository="engine" supervisor={setup({ appointment: { ...LIVE, window: 12 } })} />);
    expect(screen.getByText('Each answer is held 12 minutes for your overrule.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Appoint again…' }));
    expect(screen.getByRole('spinbutton', { name: 'Overrule window' })).toHaveValue(12);
  });

  // SUPERVISOR1e (§5): a workspace's appointment, every repository of it here and one added later, sent as `workspace:<name>`.
  it("appoints for a workspace, naming its repositories and those added later, and sends the workspace's scope", async () => {
    invoke.mockResolvedValue({ done: true });
    function Pressed() {
      const appoint = useSetAppointment();
      return (
        <SupervisorRow
          workspace="games"
          repositories={['engine', 'tools']}
          supervisor={setup({
            onAppoint: (answered, share, window) => appoint.mutate({ role: 'supervisor', workspace: 'games', on: true, share, window }, {
              onSuccess: () => answered.done(),
            }),
          })}
        />
      );
    }

    render(<QueryClientProvider client={new QueryClient()}><Pressed /></QueryClientProvider>);
    expect(screen.getByText(/in any repository of games on your behalf/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    const ask = screen.getByRole('group', { name: 'appoint the supervisor for games' });
    expect(within(ask).getByText(
      "The supervisor for every repository of games on this machine (engine and tools), and any added to it later, until Daoris restarts. A repository's own appointment wins over this one there.",
    )).toBeInTheDocument();
    await userEvent.click(within(ask).getByRole('button', { name: 'Appoint' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SET_APPOINTMENT', { payload: { role: 'supervisor', scope: 'workspace:games', holder: 'terminal', on: true, window: 5 } }));
  });

  it("says a repository its workspace's appointment covers, and that one made here wins over it", async () => {
    const games: AppointmentNow = { ...LIVE, repository: '', scope: 'workspace:games', workspace: 'games', window: 20 };
    const { rerender } = render(<SupervisorRow repository="engine" supervisor={setup({ covering: games })} />);
    expect(screen.getByText("Answered under games's appointment: an appointment here wins over it for this repository.")).toBeInTheDocument();
    expect(screen.getByText('Each answer is held 20 minutes for your overrule.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    expect(screen.getByText(/this one wins over it for this repository, and an answer of it still held here is dropped/)).toBeInTheDocument();

    // The repository's own appointment wins: the workspace's is no longer said here.
    rerender(<SupervisorRow repository="engine" supervisor={setup({ appointment: LIVE, covering: games })} />);
    expect(screen.queryByText(/Answered under games's appointment/)).toBeNull();
    expect(screen.queryByText(/held 20 minutes/)).toBeNull();
  });

  it('says the workspace, the cover and the window in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const games: AppointmentNow = { ...LIVE, repository: '', scope: 'workspace:games', workspace: 'games', window: 1 };
    const { unmount } = render(<SupervisorRow repository="engine" open supervisor={setup({ covering: games })} />);
    expect(screen.getByText('按 games 的任命回答：在这里任命，会在这个仓库取代它。')).toBeInTheDocument();
    expect(screen.getByText('每个回答保留 1 分钟供你否决。')).toBeInTheDocument();
    expect(screen.getByRole('spinbutton', { name: '否决时限' })).toHaveValue(5);
    unmount();
    render(<SupervisorRow workspace="games" repositories={[]} open supervisor={setup()} />);
    expect(screen.getByRole('group', { name: '为 games 任命主管' })).toBeInTheDocument();
    expect(screen.getByText(/本机上 games 每个仓库的主管（目前还没有仓库）/)).toBeInTheDocument();
  });

  it('says the choice of terminal in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<SupervisorRow repository="engine" open supervisor={setup({ holders: [{ id: 'h1', since: SINCE, roles: ['steward'] }] })} />);
    expect(screen.getByRole('radio', { name: `加入 ${moment(SINCE)} 启动的终端` })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '单独的终端' })).toBeInTheDocument();
  });

  it('says it in Chinese with the glossary’s words', async () => {
    await i18n.changeLanguage('zh');
    render(<SupervisorRow repository="engine" supervisor={setup({ appointment: FOUND })} />);
    expect(screen.getByText('主管')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '再次任命…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '停用' })).toBeInTheDocument();
  });

  it('sends the appointment through the window, and no answer the page receives holds a key, nor is anything written to the clipboard', async () => {
    const key = '5e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a15e17e1a1';
    const received: unknown[] = [];
    invoke.mockImplementation(async (_module: string, type: string) => {
      const answer = type === 'SET_APPOINTMENT' ? { done: true } : null;
      received.push(answer);
      return answer;
    });
    const writeText = vi.fn();
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    function Pressed() {
      const appoint = useSetAppointment();

      return (
        <SupervisorRow
          repository="engine"
          supervisor={setup({
            onAppoint: (answered) => appoint.mutate({ role: 'supervisor', repository: 'engine', on: true }, {
              onSuccess: () => answered.done(),
            }),
          })}
        />
      );
    }

    render(<QueryClientProvider client={new QueryClient()}><Pressed /></QueryClientProvider>);
    await userEvent.click(screen.getByRole('button', { name: 'Appoint…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Appoint' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'SET_APPOINTMENT', { payload: { role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: true } }));
    expect(JSON.stringify(received)).not.toContain(key);
    expect(JSON.stringify(received)).not.toMatch(/[0-9a-f]{64}/);
    expect(writeText).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toMatch(/[0-9a-f]{64}/);
  });
});
