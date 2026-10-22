import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → This Daoris → Terminal's standing confirmation (TERMTRUST1a, D201; the terminal-trust design §6.1): the row read from the
// driver's state, Off or On with the presses granted and only those, a record not honoured said under it, not offered yet with no
// *Turn on…*; *Turn on…* asks once under the row with each press ticked, and sends only the ticked ones; *Turn off* asks nothing and
// sends none.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { SPACE, WIRED } from './wiring/wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };
const BUILT = ['stop', 'finish', 'hold', 'resume'];
const SINCE = '2026-10-23T09:30:00Z';

/** The shell, its driver's state carrying `standing` as `standingConfirmation` (left out where undefined, as the bridge leaves a null). */
const shell = (standing?: Record<string, unknown>, answer: unknown = { done: true }) =>
  async (module: string, type: string) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return WIRED;
    if (type === 'SYSTEM_SPACE') return SPACE;
    if (type === 'SET_STANDING_CONFIRMATION') return answer;
    return standing === undefined ? DRIVER_STATE : { ...DRIVER_STATE, standingConfirmation: standing };
  };

const OFF = { offered: true, on: false, presses: [], built: BUILT, unwritten: false, used: 0, held: [] };
const ON = { ...OFF, on: true, presses: ['stop', 'hold'], since: SINCE, used: 3 };

const terminal = async () => within(await screen.findByRole('region', { name: /^(Terminal|终端)$/ }));
const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SET_STANDING_CONFIRMATION').map(([, , request]) => request?.payload);

describe('This Daoris → Terminal → Standing confirmation', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('draws no row where the shell says none', async () => {
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(card.getByText('Address file')).toBeTruthy();
    expect(card.queryByText('Standing confirmation')).toBeNull();
  });

  it('says Off with its hint, and offers Turn on…', async () => {
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText('Standing confirmation')).toBeTruthy();
    expect(card.getByText('Some presses from a terminal run without asking you.')).toBeTruthy();
    expect(card.getByText("Off: a terminal's presses that need you wait for your confirmation")).toBeTruthy();
    expect(card.getByRole('button', { name: 'Turn on…' })).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Turn off' })).toBeNull();
  });

  it('says On since its moment with the granted presses only, and Turn off sends none and asks nothing', async () => {
    invoke.mockImplementation(shell(ON));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText(`On since ${moment(SINCE)}: stop a session and hold a repository`)).toBeTruthy();
    expect(card.queryByText(/finish a session/)).toBeNull();
    expect(card.queryByText(/resume a repository/)).toBeNull();
    expect(card.queryByRole('button', { name: 'Turn on…' })).toBeNull();

    await userEvent.click(card.getByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ presses: [] }]));
    expect(card.queryByRole('group')).toBeNull();
  });

  it("says a revoke whose record could not be written, and a refusal in the catalogue's words", async () => {
    invoke.mockImplementation(shell(ON, { done: true, why: 'unwritten', message: 'Turned off for this run.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Turn off' }));
    expect(await card.findByText('Turned off; its record could not be written, so the next start may read it as on.')).toBeTruthy();

    invoke.mockImplementation(shell(ON, { done: false, why: 'no-pipe', message: 'The driver says so.' }));
    await userEvent.click(card.getByRole('button', { name: 'Turn off' }));
    expect((await card.findByRole('alert')).textContent).toBe("This Daoris serves no terminal, so it has no standing confirmation.");

    // A code a later driver adds is said in the driver's own sentence.
    invoke.mockImplementation(shell(ON, { done: false, why: 'later-code', message: 'A later reason.' }));
    await userEvent.click(card.getByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(card.getByRole('alert').textContent).toBe('A later reason.'));
  });

  it.each([
    ['other-machine', 'Off: its record is from another machine'],
    ['unread', 'Off: its record could not be read'],
    ['newer', 'Off: its record is from a newer Daoris'],
  ])('says a record not honoured (%s) under the row', async (unread, words) => {
    invoke.mockImplementation(shell({ ...OFF, unread }));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText(words)).toBeTruthy();
  });

  it('offers no Turn on… where it is not offered yet, and says why; one on anyway is still turned off here', async () => {
    invoke.mockImplementation(shell({ ...OFF, offered: false }));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText('Not offered yet: Daoris offers it once it holds every agent it runs apart from your terminals.'))
      .toBeTruthy();
    expect(card.getByText("Off: a terminal's presses that need you wait for your confirmation")).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Turn on…' })).toBeNull();
  });

  it('still offers Turn off where it is on though not offered', async () => {
    invoke.mockImplementation(shell({ ...ON, offered: false }));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ presses: [] }]));
  });

  it('asks once under the row, each press ticked, and Turn on sends only the ticked ones', async () => {
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Turn on…' }));

    const ask = within(card.getByRole('group', { name: 'Turn on the standing confirmation?' }));
    expect(ask.getByText(/^Let a terminal on this machine do these without asking you each time\./)).toBeTruthy();
    expect(ask.getByText(/an agent you run yourself outside Daoris can do them too/)).toBeTruthy();
    const ticks = ask.getAllByRole('checkbox');
    expect(ticks).toHaveLength(4);
    for (const tick of ticks) expect(tick.getAttribute('aria-checked')).toBe('true');
    for (const words of ['stop a session', 'finish a session that waits on you, without your words', 'hold a repository',
      'resume a repository a terminal held']) {
      expect(ask.getByText(words)).toBeTruthy();
    }
    expect(ask.getByRole('button', { name: 'Never mind' })).toBeTruthy();

    await userEvent.click(ask.getByRole('checkbox', { name: 'finish a session that waits on you, without your words' }));
    await userEvent.click(ask.getByRole('checkbox', { name: 'resume a repository a terminal held' }));
    await userEvent.click(ask.getByRole('button', { name: 'Turn on' }));

    await vi.waitFor(() => expect(sent()).toEqual([{ presses: ['stop', 'hold'] }]));
    await vi.waitFor(() => expect(card.queryByRole('group', { name: 'Turn on the standing confirmation?' })).toBeNull());
  });

  it('keeps the ask open with a refusal said inside it, and cannot be pressed with nothing ticked', async () => {
    invoke.mockImplementation(shell(OFF, { done: false, why: 'not-offered', message: 'Not yet.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Turn on…' }));
    const ask = within(card.getByRole('group', { name: 'Turn on the standing confirmation?' }));

    await userEvent.click(ask.getByRole('button', { name: 'Turn on' }));
    expect((await ask.findByRole('alert')).textContent).toBe('This Daoris does not offer the standing confirmation yet.');

    for (const tick of ask.getAllByRole('checkbox')) await userEvent.click(tick);
    expect((ask.getByRole('button', { name: 'Turn on' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('says the row and its ask in Chinese', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    expect(await card.findByText('长期确认')).toBeTruthy();
    expect(card.getByText('终端的部分操作无需问你即可执行。')).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: '启用…' }));
    const ask = within(card.getByRole('group', { name: '启用长期确认？' }));
    expect(ask.getByText(/^允许本机的终端无需每次问你即可执行这些操作。/)).toBeTruthy();
    for (const words of ['停止会话', '完成一个在等你的会话（不附你的话）', '暂停一个仓库', '恢复由终端暂停的仓库']) {
      expect(ask.getByText(words)).toBeTruthy();
    }
    expect(ask.getByRole('button', { name: '启用' })).toBeTruthy();
    expect(ask.getByRole('button', { name: '取消' })).toBeTruthy();
  });

  it('says On in Chinese with the granted presses only', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(ON));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    expect(await card.findByText(`自 ${moment(SINCE, 'zh')} 起启用：停止会话和暂停一个仓库`)).toBeTruthy();
    expect(card.getByRole('button', { name: '停用' })).toBeTruthy();
  });
});
