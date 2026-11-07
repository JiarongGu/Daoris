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

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { SettingsView } from './SettingsView';

/** The page has drawn and the driver's state has been asked and answered: what the answer leaves out is then known to be absent. */
const driverRead = async () => {
  await screen.findByRole('checkbox', { name: 'Announce what needs you or ended' });
  await vi.waitFor(() => expect(invoke.mock.calls.some(([, type, options]) =>
    type === 'STATE_SLICE' && (options as { payload?: { slice?: string } } | undefined)?.payload?.slice === 'driver')).toBe(true));
  await new Promise((resolve) => setTimeout(resolve, 50));
};
import { moment } from '../locales/format';
import i18n from '../locales/i18n';
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
   * Where this machine's Daoris lives, and what the start did about it. The notice rides the
   * STATE rather than only the one-time event, because the page subscribes after the host answers
   * and a toast raised before that reached nobody — measured on the first migrated start.
   */
  it('shows where this machine\'s Daoris lives, and what establishing it did', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? {
        ...DRIVER_STATE,
        home: 'D:/somewhere/Daoris/data',
      }
      : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByText('D:/somewhere/Daoris/data')).toBeTruthy();
  });

  /**
   * This machine's sessions' access, the default for every workspace and repository that sets none of
   * its own, the same rows as a repository's Setup, each set as `daoris place access` with no repository or workspace sets it.
   */
  it("sets this machine's sessions' access, full access only through its ask", async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? { ...DRIVER_STATE, machineAccess: { full: false, pauses: [{ pause: 'goAheads', value: 'never' }] } }
      : WIRING));
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="driver" />);
    const access = within(await screen.findByRole('region', { name: "Sessions' access" }));
    const row = (label: string) => access.getAllByText(label, { selector: 'span' })[0]!.closest('[data-setting-row]') as HTMLElement;

    // Its lead paragraph is its title's ⓘ (ACCESSASK1, the UI/UX design §3.3): the reason one press away, the rows first.
    expect(access.getByRole('heading', { name: /Sessions' access/ })).toContainElement(
      access.getByRole('note', { name: /This machine's default, for every workspace and repository that sets none of its own/ }));
    expect(access.queryByText(/This machine's default/, { selector: 'p' })).toBeNull();
    expect(row('Go-aheads')).toHaveTextContent('No go-ahead is asked, save one the quest or your words demand. Set for this machine.');
    expect(row('Permission prompts')).toHaveTextContent("Daoris's default: nothing is set.");
    expect(within(row('Permission prompts')).getByRole('button', { name: 'Set for this machine' })).toBeTruthy();
    expect(within(row('Go-aheads')).getByText(code('daoris place access goAheads=named|never'))).toBeTruthy();

    await userEvent.click(within(row('Go-aheads')).getByRole('button', { name: 'Clear' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACCESS', { payload: { machine: true, access: { goAheads: null } } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Go-aheads for this machine comes from what stands above it again.'));

    await vi.waitFor(() => expect(access.getByRole('checkbox', { name: 'Full access' })).toBeEnabled());
    await userEvent.click(access.getByRole('checkbox', { name: 'Full access' }));
    expect(access.getByRole('group', { name: 'Turn on full access for this machine' })).toHaveTextContent(
      'wherever a workspace or a repository sets none of its own');
    await userEvent.click(access.getByRole('button', { name: 'Enable full access' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_ACCESS', { payload: { machine: true, access: 'full' } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Sessions on this machine now start with full access.'));
  });

  // A pause's change the driver refused is said under its row, the editor open, no toast.
  it("says a refused change to this machine's access under its row, never a toast", async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ACCESS') throw new Error('The driver could not write driver.json.');
      return { ...DRIVER_STATE, machineAccess: { full: false, pauses: [] } };
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="driver" />);
    const access = within(await screen.findByRole('region', { name: "Sessions' access" }));
    const row = access.getAllByText('Permission prompts', { selector: 'span' })[0]!.closest('[data-setting-row]') as HTMLElement;

    await userEvent.click(within(row).getByRole('button', { name: 'Set for this machine' }));
    await userEvent.click(within(row).getByRole('radio', { name: 'Asking mode' }));

    expect(await within(row).findByRole('alert')).toHaveTextContent('The driver could not write driver.json.');
    expect(within(row).getByRole('radiogroup')).toBeTruthy();
    expect(notify).not.toHaveBeenCalled();
  });

  // The access card carries this machine's git access beside its pauses, each class's change this
  // machine's, as `daoris git access --machine` sets it.
  it("sets this machine's git access in its access card", async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? { ...DRIVER_STATE, machineAccess: { full: false, pauses: [] }, machineGitAccess: [{ class: 'write', value: 'ask' }] }
      : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    const access = within(await screen.findByRole('region', { name: "Sessions' access" }));

    expect(access.getByText('Write', { selector: 'span' })).toBeTruthy();
    await userEvent.click(within(access.getByRole('radiogroup', { name: 'Write for this machine' })).getByRole('radio', { name: 'deny' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_GIT_ACCESS', { payload: { machine: true, access: { write: 'deny' } } });
  });

  it("offers no sessions' access where the answer lacks it", async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, home: 'D:/somewhere/Daoris/data' } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    expect(await screen.findByText('D:/somewhere/Daoris/data')).toBeTruthy();
    expect(screen.queryByRole('region', { name: "Sessions' access" })).toBeNull();
  });

  /**
   * Settings holds no Plugins domain, and where plugins are looked for is a fact
   * of the Daoris home, so it is a row under the home, read-only, with the door to the Plugins place beside its path.
   */
  it('names the plugins folder under the home, with its door to the Plugins place', async () => {
    invoke.mockImplementation(async (module: string, type: string) => (module !== 'DAORIS.DRIVER' ? WIRING
      : type === 'PLUGINS' ? { folder: 'D:/somewhere/Daoris/data/plugins', plugins: [] }
        : { ...DRIVER_STATE, home: 'D:/somewhere/Daoris/data' }));
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="driver" onGo={onGo} />);

    expect(await screen.findByText('D:/somewhere/Daoris/data/plugins')).toBeTruthy();
    // Under the home, whose fact it is.
    const labels = screen.getAllByText(/^(Daoris home|Plugins folder)$/).map((label) => label.textContent);
    expect(labels).toEqual(['Daoris home', 'Plugins folder']);
    expect(screen.getByText(code('daoris plugin list'))).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Open Plugins' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'plugins' });
  });

  /**
   * The Plugins section lists each installed plugin from the catalogue the Plugins place
   * reads, with what it holds set, and its door opens that plugin's page, where its settings are changed.
   */
  it('lists each installed plugin with what it holds set, its door opening its page', async () => {
    invoke.mockImplementation(async (module: string, type: string) => (module !== 'DAORIS.DRIVER' ? WIRING
      : type === 'PLUGINS'
        ? {
          folder: 'D:/somewhere/Daoris/data/plugins',
          plugins: [{
            id: 'in-app-browser', name: 'In-app browser', version: '1.0.0', description: '', enabled: true, problem: null,
            harnesses: [], points: [], running: false, folder: 'D:/somewhere/Daoris/data/plugins/in-app-browser', data: '',
            helpServers: [{ name: 'browser', allow: ['browser_snapshot'], writes: ['browser_click'], state: 'approved', accepted: ['browser_click'] }],
          }],
        }
        : { ...DRIVER_STATE, home: 'D:/somewhere/Daoris/data' }));
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="driver" onGo={onGo} />);

    const plugins = within(await screen.findByRole('region', { name: 'Installed plugins' }));
    expect(await plugins.findByText('In-app browser')).toBeTruthy();
    expect(plugins.getByText('approved for Ask Daoris')).toBeTruthy();

    await userEvent.click(plugins.getByRole('button', { name: "Open In-app browser's page" }));
    expect(onGo).toHaveBeenCalledWith({ view: 'plugins', item: 'in-app-browser' });
  });

  /**
   * Where the home lives moved to Settings → This Daoris, and Driver keeps a row whose
   * control is the door to it.
   */
  it('keeps the Daoris home as a door to This Daoris', async () => {
    const home = 'D:/somewhere/Daoris/data';
    const driver = { ...DRIVER_STATE, home };
    invoke.mockImplementation(async (module: string, type: string) => (module !== 'DAORIS.DRIVER' ? WIRING
      : type === 'SYSTEM_WIRING' ? { schema: 1 } : driver));
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="driver" onGo={onGo} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Open This Daoris' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'wiring' });
    // The notices went to the page the door opens.
    expect(screen.queryByText(/moved in from/)).toBeNull();
  });

  it('says nothing about the home on a shell that has never heard of one', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await driverRead();

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
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true, cap: 2 } : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await screen.findByRole('checkbox', { checked: true });

    // The number dial is reachable by its label, and the why is a note, not a paragraph.
    expect(await screen.findByLabelText('Sessions at once on this machine')).toBeTruthy();
    expect(screen.getByRole('note', { name: /Nobody should have to watch a driver/ })).toBeTruthy();
    expect(screen.queryByText(/Nobody should have to watch a driver/)).toBeNull();
  });

  /**
   * How many sessions Daoris starts at once, the `cap` `daoris settings cap` edits. Written
   * only when changed to a whole number of at least one; an answer with no such field is offered no row.
   */
  it('sets the cap of sessions Daoris starts, and offers nothing where the answer lacks it', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      return type === 'SET_CAP' ? { ...DRIVER_STATE, cap: 4 } : { ...DRIVER_STATE, cap: 2 };
    });
    const { unmount } = show(<SettingsView notify={notify} section="driver" />);
    const field = await screen.findByLabelText('Sessions at once on this machine');
    expect((field as HTMLInputElement).value).toBe('2');

    await userEvent.clear(field);
    await userEvent.type(field, '0');
    await userEvent.tab();
    expect(invoke.mock.calls.some(([, type]) => type === 'SET_CAP')).toBe(false);
    expect((field as HTMLInputElement).value).toBe('2');

    await userEvent.clear(field);
    await userEvent.type(field, '4');
    await userEvent.tab();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_CAP', { payload: { cap: 4 } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Daoris now starts at most 4 sessions at once on this machine.'));
    unmount();

    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await driverRead();
    expect(screen.queryByLabelText('Sessions at once on this machine')).toBeNull();
  });

  /**
   * How long an account cools when its agent names no reset, the `cooloff` `daoris settings cooloff`
   * edits. Written only when changed to a whole number of at least one; an answer with no such field is offered no
   * row.
   */
  it('sets the cool-off a limit naming no time takes, and offers nothing where the answer lacks it', async () => {
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
    await driverRead();
    expect(screen.queryByLabelText('Cool-off when no time is named')).toBeNull();
  });

  /**
   * `quietMinutes`, the one quiet setting, the `quiet` `daoris settings quiet` edits: written only when changed to whole minutes
   * from 5 to 120; an answer with no such field is offered no row.
   */
  it('sets how long a session may be quiet in its bounds, and offers nothing where the answer lacks it', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      return type === 'SET_QUIET' ? { ...DRIVER_STATE, quiet: 25 } : { ...DRIVER_STATE, quiet: 10 };
    });
    const { unmount } = show(<SettingsView notify={notify} section="driver" />);
    const field = await screen.findByLabelText('Look in on a quiet session after');
    expect((field as HTMLInputElement).value).toBe('10');

    for (const out of ['4', '121']) {
      await userEvent.clear(field);
      await userEvent.type(field, out);
      await userEvent.tab();
      expect(invoke.mock.calls.some(([, type]) => type === 'SET_QUIET')).toBe(false);
      expect((field as HTMLInputElement).value).toBe('10');
    }

    await userEvent.clear(field);
    await userEvent.type(field, '25');
    await userEvent.tab();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_QUIET', { payload: { minutes: 25 } });
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('Daoris now looks in on a session quiet for 25 minutes.'));
    unmount();

    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} section="driver" />);
    await driverRead();
    expect(screen.queryByLabelText('Look in on a quiet session after')).toBeNull();
  });

  /**
   * Notifications: the desktop's door onto the same `driver.json` field
   * `daoris settings notify on|off` edits. It is ON until somebody says otherwise — a driver nobody
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
    expect(screen.getByText(code(/daoris settings notify on\|off/))).toBeTruthy();

    await userEvent.click(check);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_NOTIFY', { payload: { notify: false } });
  });

  it('a machine that has turned notifications off says so', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: false } : WIRING));

    show(<SettingsView notify={() => {}} section="driver" />);

    expect(await screen.findByRole('checkbox', { checked: false })).toBeTruthy();
  });

  /**
   * The switch says what it governs, beside its value. It said *when a session parks*
   * while it also governed an ending and an answer, and a held tool call joins them; a terminal's press is announced either way,
   * since its terminal waits on the person's answer, and Notifications lists everything whatever it says.
   */
  it.each([
    ['en', true, 'Announce what needs you or ended',
      'On: a park, a held tool call, an answer to your words and an ending you did not ask for are each said, in the window you are at or by a system notification while Daoris is behind.'],
    ['en', false, 'Announce what needs you or ended', 'Off: none of these is said, and Notifications still lists each one.'],
    ['zh', true, '提醒等你处理和已结束的事',
      '开启：会话挂起、等你回答的工具调用、对你的话的回复，以及不是你要求的结束，都会在你所在的窗口里提醒你；Daoris 在后面时发系统通知。'],
    ['zh', false, '提醒等你处理和已结束的事', '关闭：这些都不会提醒，通知里仍会列出每一项。'],
  ] as const)('in %s, the switch says what it governs while it is %s', async (language, on, label, state) => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: on } : WIRING));
    await i18n.changeLanguage(language);
    try {
      show(<SettingsView notify={() => {}} section="driver" />);

      expect(await screen.findByRole('checkbox', { checked: on, name: label })).toBeTruthy();
      expect(screen.getByText(state)).toBeTruthy();
      expect(screen.getByText(language === 'en'
        ? "A terminal's press for your confirmation is said either way: its terminal waits on your answer."
        : '终端请你确认的请求无论开关如何都会提醒：它的终端在等你的回答。')).toBeTruthy();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

/**
 * The install's update in the application's own domain, read from `DAORIS.UPDATE` · `STATE` and
 * said with `SET`. The banner is the screen's other door and is gone once dismissed; this row stands: what is staged, the
 * drain and what it waits on, how the last swap ended, and the three words the terminal's `daoris update` takes.
 */
describe("the driver domain: the install's update", () => {
  const STAGED = { id: '20261003T120000Z-ab12cd34', version: '0.0.1', commit: 'abc1234', at: '2026-10-03T12:00:00Z' };
  const update = (extra: object) => ({
    state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null, ...extra,
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
    expect(row.getByText(code(/daoris update --when-idle/))).toBeTruthy();
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

  it('refused, says which check failed, and offers Update now alone, which checks it again', async () => {
    invoke.mockImplementation(answer(update({
      state: 'refused', staged: STAGED, problem: { code: 'hash', message: 'app/Daoris.Desktop.App.dll is not the file its manifest names.' },
    })));
    show(<SettingsView notify={() => {}} section="driver" />);
    const row = await section();

    expect(await row.findByText(/Not installed: a file is not the one its manifest names\. Update now checks it again/)).toBeTruthy();
    expect(row.getByText('refused')).toBeTruthy();
    expect(row.getByText(STAGED.id)).toBeTruthy();
    expect(row.getAllByRole('button').map((button) => button.textContent)).toEqual(['Update now']);
  });

  it('says how the last swap ended, installed, and still says it once the banner has put the outcome away', async () => {
    const installed = { phase: 'installed', build: STAGED.id, version: '0.0.1', commit: 'abc1234' };
    // The start after the swap: the banner's once-said `outcome`, and `last`, the journal's record, which the row reads.
    invoke.mockImplementation(answer(update({ outcome: installed, last: installed })));
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
      last: { phase: 'rolled-back', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'exited' },
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
      last: { phase: 'refused', build: STAGED.id, version: '0.0.1', commit: 'abc1234', reason: 'missing' },
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
