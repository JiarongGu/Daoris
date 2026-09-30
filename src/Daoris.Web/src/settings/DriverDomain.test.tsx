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

  /**
   * LEFT1 then LEFT2 (D105): a terminal's daoris reads the folder the account's DAORIS_HOME names. The hint says
   * that is this folder only when the state's `homeAccount` says the account names it. An install that overrode
   * an inherited home says which folder a terminal still reads in its notice, and the hint points there; a home
   * named for this start alone has no notice (D105 respects it without one), so the hint says it itself. The
   * field decides, never the notice's English: the host's sentence is passed through untranslated.
   */
  it('says a terminal reads the same folder only when the state says the account names it', async () => {
    const home = 'D:/somewhere/second/data';
    const overridden = `Daoris home: ${home} — this install's own data folder, not D:/somewhere/first/data, which `
      + "DAORIS_HOME names for your account; that variable is left as it is, so a terminal's daoris still reads "
      + 'D:/somewhere/first/data until you change it.';
    // Renders the row, and answers which of the three things the hint says of a terminal.
    const hint = async (homeAccount: string | undefined, homeNotice: string | null) => {
      invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
        ? { ...DRIVER_STATE, home, homeNotice, ...(homeAccount === undefined ? {} : { homeAccount }) }
        : WIRING));
      const shown = show(<SettingsView notify={() => {}} section="driver" />);
      await screen.findByText(home);
      if (homeNotice) expect(screen.getByText(homeNotice)).toBeTruthy();
      // The rest of the hint is there either way.
      expect(screen.getByRole('note', { name: /Every file on this page lives under it/ })).toBeTruthy();
      const said = screen.queryByRole('note', { name: /a terminal's daoris reads the same folder/ }) ? 'same'
        : screen.queryByRole('note', { name: /The line below says which folder a terminal's daoris reads/ }) ? 'below'
          : screen.queryByRole('note', { name: /named for this start alone.*does not read this folder/ }) ? 'this start'
            : 'nothing';
      shown.unmount();
      return said;
    };

    expect(await hint('same', null)).toBe('same');
    expect(await hint('overridden', overridden)).toBe('below');
    // 🔴 The case the notice could not tell: DAORIS_HOME set for this start alone, and no notice at all.
    expect(await hint('this-start', null)).toBe('this start');
    // The field decides, whatever the notice says: the override's words under a home the account names.
    expect(await hint('same', overridden)).toBe('same');
    expect(await hint('same', `Daoris home: ${home} — moved in from D:/somewhere/.daoris: driver.json.`)).toBe('same');
    // A shell older than the field says what it always said.
    expect(await hint(undefined, null)).toBe('same');
  });

  it('says nothing about the home on a shell that has never heard of one', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByLabelText('Failures before a quest parks');

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
    expect(screen.getByLabelText('Failures before a quest parks')).toBeTruthy();
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
    const field = await screen.findByLabelText('Failures before a quest parks');

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
