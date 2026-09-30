import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Logs domain in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { SettingsView } from '../SettingsView';
import { DRIVER_STATE, respond, serviceCalls, show } from '../test/shellHarness';

/**
 * Settings → Logs (LOG1c, D94): the machine log read back over `DAORIS.LOG`, with the filters the
 * terminal's `daoris-driver logs` takes applied by the shell, and the folder opened by the shell. None
 * of it goes over the service: the log is the machine's (D47 §4).
 */
describe('the logs domain', () => {
  const READING = {
    folder: 'C:/somewhere/data/logs',
    lines: [
      { time: '2026-09-30T11:30:00.000Z', source: 'desktop', level: 'error', event: 'page.error', data: { where: 'window' } },
      { time: '2026-09-30T09:00:00.000Z', source: 'desktop', level: 'info', event: 'view.opened', data: { view: 'sessions' } },
    ],
    total: 2,
    counts: { info: 1, warn: 0, error: 1 },
    events: ['page.error', 'view.opened'],
    skipped: 0,
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.LOG') return DRIVER_STATE;
      return type === 'OPEN_FOLDER' ? { opened: true, folder: READING.folder } : READING;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('is a domain of a desktop, reading the last day of every source from the machine, and nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);

    expect(await within(screen.getByRole('navigation', { name: 'Settings domains' })).findByRole('button', { name: 'Logs' }))
      .toHaveAttribute('aria-current', 'page');
    expect(await screen.findByRole('listitem', { name: 'page.error' })).toBeInTheDocument();
    expect(screen.getByText('C:/somewhere/data/logs')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'LINES', { payload: { since: '1d', limit: 200 } });
    expect(serviceCalls()).toEqual([]);
  });

  it('asks the shell again with each filter, as the terminal would take the flag', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);
    await screen.findByRole('listitem', { name: 'page.error' });

    await userEvent.click(screen.getByRole('radio', { name: 'errors' }));
    await waitFor(() => expect(invoke).toHaveBeenLastCalledWith(
      'DAORIS.LOG', 'LINES', { payload: { since: '1d', level: 'error', limit: 200 } }));

    await userEvent.click(screen.getByRole('radio', { name: 'last 7 days' }));
    await waitFor(() => expect(invoke).toHaveBeenLastCalledWith(
      'DAORIS.LOG', 'LINES', { payload: { since: '7d', level: 'error', limit: 200 } }));
  });

  it('opens the folder through the shell, naming no path', async () => {
    show(<SettingsView notify={() => {}} section="logs" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Open the folder' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'OPEN_FOLDER', {});
  });

  it('says where the log is when the shell could not open a folder', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.LOG') return DRIVER_STATE;
      return type === 'OPEN_FOLDER' ? { opened: false, folder: READING.folder } : READING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="logs" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Open the folder' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('This window cannot open a folder. The log is in C:/somewhere/data/logs.'));
  });
});
