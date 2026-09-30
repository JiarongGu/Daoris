import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Driver domain in SHELL mode: where this machine's Daoris lives and the driver's dials over
// `driver.json`. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';

describe('the driver domain', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => WIRING);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /**
   * Where this machine's Daoris lives (D63), and what the start did about it. The notice rides the
   * STATE rather than only the one-time event, because the page subscribes after the host answers
   * and a toast raised before that reached nobody — measured on the first migrated start.
   */
  it('shows where this machine\'s Daoris lives, and what establishing it did', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? {
        ...DRIVER_STATE,
        home: 'D:/somewhere/Daoris/data',
        homeNotice: 'Daoris home: D:/somewhere/Daoris/data — moved in from D:/somewhere/.daoris: driver.json.',
        hostNotice: 'the host at http://localhost:5177 was already running and serves a different page than this install carries.',
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByText('D:/somewhere/Daoris/data')).toBeTruthy();
    expect(screen.getByText(/moved in from/)).toBeTruthy();
    // The adopted host's page is a standing fact and gets a standing line, not only a toast.
    expect(screen.getByText(/serves a different page/)).toBeTruthy();
  });

  it('says nothing about the home on a shell that has never heard of one', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByLabelText('Park a quest after this many failed sessions');

    // What is absent is the path a newer shell answers.
    expect(screen.queryByText(/somewhere\/Daoris\/data/)).toBeNull();
    expect(screen.queryByText(/moved in from/)).toBeNull();
  });

  /**
   * A setting is a row (2026-09-23): the control sits beside its label, the terminal's door is the
   * one-line hint, and the paragraph that motivated the setting is on the glyph rather than on the
   * page — so the first control is where the eye lands rather than a screen below the title.
   */
  it('lays each setting out as a row, with its why one hover away rather than on the page', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true, strikes: 3 } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByRole('checkbox', { checked: true });

    // The number dial is reachable by its label, and the why is a note, not a paragraph.
    expect(screen.getByLabelText('Park a quest after this many failed sessions')).toBeTruthy();
    expect(screen.getByRole('note', { name: /Nobody should have to watch a driver/ })).toBeTruthy();
    expect(screen.queryByText(/Nobody should have to watch a driver/)).toBeNull();
  });

  /**
   * REV3: passing through the field was a write. `Number(null)` and `Number('')` are both 0, which is
   * the one value that means "never park" — and a keyboard user tabbing through the card sent it, with
   * a toast that read like a confirmation.
   */
  it('writes the strikes only when the person changed them — focus, blur and a cleared box write nothing', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, strikes: 3 } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    const field = await screen.findByLabelText('Park a quest after this many failed sessions');

    await userEvent.click(field);
    await userEvent.tab();
    await userEvent.clear(field);
    await userEvent.tab();
    await userEvent.clear(field);
    await userEvent.type(field, '3');
    await userEvent.tab();
    expect(invoke.mock.calls.some(([, type]) => type === 'SET_STRIKES')).toBe(false);
    expect((field as HTMLInputElement).value).toBe('3');

    await userEvent.clear(field);
    await userEvent.type(field, '5');
    await userEvent.tab();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STRIKES', { payload: { strikes: 5 } });
  });

  /**
   * Notifications (SURF5b): the desktop's door onto the same `driver.json` field
   * `daoris driver notify on|off` edits. It is ON until somebody says otherwise — a driver nobody
   * has to watch is the point of one — and the surface names the other door so a person on a
   * machine they reach over ssh does not go looking for a second setting.
   */
  it('the notification switch reads the machine and writes the same file a terminal does', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true } : WIRING));

    show(<SettingsView notify={() => {}} section="driver" />);

    // Found by its loaded STATE rather than by its label, because the switch renders before the
    // machine has answered and its default is on — a bare label query would pass either way.
    const check = await screen.findByRole('checkbox', { checked: true });
    expect(screen.getByText(/daoris driver notify on\|off/)).toBeTruthy();

    await userEvent.click(check);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_NOTIFY', { payload: { notify: false } });
  });

  it('a machine that has turned notifications off says so', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: false } : WIRING));

    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByRole('checkbox', { checked: false })).toBeTruthy();
  });
});
