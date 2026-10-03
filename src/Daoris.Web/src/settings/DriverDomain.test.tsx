import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
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
import { moment } from '../format';
import i18n from '../i18n';
import { code } from '../test/code';
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
   * TOOL4g (D125 §2.2, §6): how long an account cools when its agent names no reset, the `cooloff` `daoris driver cooloff`
   * edits. Written only when changed to a whole number of at least one, as the strikes are; an older shell that answers no
   * such field is offered no row.
   */
  it('sets the cool-off a limit naming no time takes, and offers nothing to a shell older than it', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      return type === 'SET_COOLOFF' ? { ...DRIVER_STATE, coolOff: 90 } : { ...DRIVER_STATE, coolOff: 60 };
    });
    const { unmount } = show(<SettingsView notify={notify} section="driver" />);
    const field = await screen.findByLabelText('Cool-off when no time is named');
    expect((field as HTMLInputElement).value).toBe('60');

    await userEvent.clear(field);
    await userEvent.type(field, '0');
    await userEvent.tab();
    expect(invoke.mock.calls.some(([, type]) => type === 'SET_COOLOFF')).toBe(false);
    expect((field as HTMLInputElement).value).toBe('60');

    await userEvent.clear(field);
    await userEvent.type(field, '90');
    await userEvent.tab();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_COOLOFF', { payload: { minutes: 90 } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('An account whose agent names no reset now cools 90 minutes.'));
    unmount();

    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByLabelText('Failures before a quest parks');
    expect(screen.queryByLabelText('Cool-off when no time is named')).toBeNull();
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
    expect(screen.getByText(code(/daoris driver notify on\|off/))).toBeTruthy();

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

/**
 * UPDATE1b (D139 §3, §6): the install's update in the application's own domain, read from `DAORIS.UPDATE` · `STATE` and
 * said with `SET`. The banner is the screen's other door and is gone once dismissed; this row stands: what is staged, the
 * drain and what it waits on, how the last swap ended, and the three words the terminal's `daoris-driver update` takes.
 */
describe("the driver domain: the install's update", () => {
  const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };
  const update = (extra: object) => ({
    state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, ...extra,
  });
  /** The shell: the driver's state, the update's as given, and what `SET` answers after a word. */
  const answer = (state: object, after: object = state) => async (module: string, type: string) => {
    if (module === 'DAORIS.DRIVER') return DRIVER_STATE;
    if (module === 'DAORIS.UPDATE') return type === 'SET' ? after : state;
    return WIRING;
  };
  const section = async () => within(await screen.findByRole('region', { name: 'Update' }));

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('with nothing staged, says so, names the terminal\'s door, and offers none of the three words', async () => {
    invoke.mockImplementation(answer(update({})));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText('Nothing is staged beside this install.')).toBeTruthy();
    expect(row.getByText(code(/daoris-driver update --when-idle/))).toBeTruthy();
    for (const word of ['Update when idle', 'Update now', 'Not now']) expect(row.queryByRole('button', { name: word })).toBeNull();
    // No swap to tell of: no row for one.
    expect(row.queryByText('Last update')).toBeNull();
    expect(invoke).toHaveBeenCalledWith('DAORIS.UPDATE', 'STATE');
  });

  it('while draining, names the build, its version, commit and when, what the drain waits on, and offers Update now and Not now', async () => {
    invoke.mockImplementation(answer(
      update({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 2, turns: 1 }),
      update({ state: 'waiting', staged: STAGED, mode: 'not-now' }),
    ));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(STAGED.id)).toBeTruthy();
    expect(row.getByText('0.0.1')).toBeTruthy();
    expect(row.getByText('abc1234')).toBeTruthy();
    expect(row.getByText(moment(STAGED.at))).toBeTruthy();
    expect(row.getByText('when idle')).toBeTruthy();
    expect(row.getByText(/Installs when the work running now ends; nothing new starts meanwhile\./)).toBeTruthy();
    expect(row.getByText(/Waits on 2 driven sessions and 1 conversation mid-turn\./)).toBeTruthy();
    expect(row.queryByRole('button', { name: 'Update when idle' })).toBeNull();

    await userEvent.click(row.getByRole('button', { name: 'Not now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.UPDATE', 'SET', { payload: { mode: 'not-now' } });
    // The answer is the state after the word, which the row takes at once.
    expect(await row.findByText('not now')).toBeTruthy();
  });

  it('draining with nothing left running, says it installs at the next look', async () => {
    invoke.mockImplementation(answer(update({ state: 'draining', staged: STAGED, mode: 'when-idle' })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(/Nothing runs now, so it installs at the next look\./)).toBeTruthy();
    await userEvent.click(row.getByRole('button', { name: 'Update now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.UPDATE', 'SET', { payload: { mode: 'now' } });
  });

  it('waiting after Not now, says work goes on and offers Update when idle and Update now', async () => {
    invoke.mockImplementation(answer(update({ state: 'waiting', staged: STAGED, mode: 'not-now' })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(/You said Not now: work goes on, and this build waits for your word\./)).toBeTruthy();
    expect(row.getByText('not now')).toBeTruthy();
    expect(row.queryByRole('button', { name: 'Not now' })).toBeNull();
    await userEvent.click(row.getByRole('button', { name: 'Update when idle' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.UPDATE', 'SET', { payload: { mode: 'when-idle' } });
    await userEvent.click(row.getByRole('button', { name: 'Update now' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.UPDATE', 'SET', { payload: { mode: 'now' } });
  });

  it('says a word the shell refused in the shell\'s sentence', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.UPDATE' && type === 'SET') {
        throw Object.assign(new Error('nothing is staged'), { code: 'UPDATE_NOTHING_STAGED' });
      }
      return answer(update({ state: 'waiting', staged: STAGED, mode: 'not-now' }))(module, type);
    });
    show(<SettingsView notify={notify} section="driver" />);
    const row = await section();

    await userEvent.click(await row.findByRole('button', { name: 'Update when idle' }));
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Nothing is staged beside this install, so there is nothing to install.', 'error'));
  });

  it('refused, says which check failed and that it is not tried again, and offers nothing to press', async () => {
    invoke.mockImplementation(answer(update({
      state: 'refused', staged: STAGED, problem: { code: 'hash', message: 'app/Daoris.Desktop.App.dll is not the file its manifest names.' },
    })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(/Not installed: a file is not the one its manifest names\. It is not tried again/)).toBeTruthy();
    expect(row.getByText('refused')).toBeTruthy();
    expect(row.getByText(STAGED.id)).toBeTruthy();
    expect(row.queryByRole('button')).toBeNull();
  });

  it('says how the last swap ended, installed, and still says it once the banner has put the outcome away', async () => {
    const installed = { phase: 'installed', build: STAGED.id, version: '0.0.1', commit: 'abc1234' };
    invoke.mockImplementation(answer(update({ outcome: installed })));
    const { unmount } = show(<SettingsView notify={() => {}} section="driver" />);
    let row = await section();

    expect(await row.findByText('Last update')).toBeTruthy();
    expect(row.getByText('Daoris was updated to 0.0.1 (abc1234).')).toBeTruthy();
    expect(row.getByText('installed')).toBeTruthy();
    unmount();

    // After *Dismiss*: `outcome` is put away, and `last` keeps the swap the row tells of.
    invoke.mockImplementation(answer(update({ last: installed })));
    show(<SettingsView notify={() => {}} section="driver" />);
    row = await section();
    expect(await row.findByText('Daoris was updated to 0.0.1 (abc1234).')).toBeTruthy();
  });

  it('says a swap rolled back with its reason, beside a newer build draining', async () => {
    invoke.mockImplementation(answer(update({
      state: 'draining', staged: { ...STAGED, id: '20261004T090000Z-cd34ef56', commit: 'def5678' }, mode: 'when-idle', driven: 1,
      outcome: { phase: 'rolled-back', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'exited' },
    })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(
      'Daoris 0.0.1 (abc1234) could not start, so Daoris went back to the build before it: the new build ended before it came up.',
    )).toBeTruthy();
    expect(row.getByText('rolled back')).toBeTruthy();
    expect(row.getByText('def5678')).toBeTruthy();
    expect(row.getByText(/Waits on 1 driven session\./)).toBeTruthy();
  });

  it('says a swap the launcher refused with its check', async () => {
    invoke.mockImplementation(answer(update({
      outcome: { phase: 'refused', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'missing' },
    })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(
      'The staged Daoris 0.0.1 (abc1234) was refused before anything was replaced: a file its manifest names is missing.',
    )).toBeTruthy();
    expect(row.getByText('refused')).toBeTruthy();
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(answer(update({ state: 'draining', staged: STAGED, mode: 'when-idle', driven: 1, turns: 2 })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = within(await screen.findByRole('region', { name: '更新' }));

    expect(await row.findByText('暂存的构建')).toBeTruthy();
    expect(row.getByText(/还在等 1 个驱动的会话和 2 个对话的当前一轮结束。/)).toBeTruthy();
    expect(row.getByRole('button', { name: '立即更新' })).toBeTruthy();
    expect(row.getByRole('button', { name: '暂不更新' })).toBeTruthy();
  });
});
