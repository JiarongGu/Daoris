import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → This Daoris, set up from the page: *Open* sends a row's id and never a path;
// *Put on my PATH* and *Take off my PATH* say first what they do and press only on the second; *Copy a terminal line* is the page's
// clipboard and presses nothing; *Change the service's address* says the current value and the new one first; each refusal is data, said in
// the catalogue's words, in both languages. The shell here is a stand-in: no account, PATH or file manager is touched.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import i18n from '../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { SPACE, WIRED } from './wiring/wiringFixtures';
import { terminalLine } from './wiring/terminalLine';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };
const BIN = 'C:\\Daoris\\app\\bin';
const INSTALLED = { ...WIRED, terminal: { ...WIRED.terminal, bin: BIN, onAccountPath: false } };

const ROUTES = ['SYSTEM_OPEN_FOLDER', 'SYSTEM_PATH', 'SYSTEM_SERVICE_ADDRESS'];

/** The shell: the wiring as given, and each set-up press answering `answer`. */
const shell = (wiring: unknown = INSTALLED, answer: unknown = { done: true, changed: true }) =>
  async (module: string, type: string) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return wiring;
    if (type === 'SYSTEM_SPACE') return SPACE;
    if (ROUTES.includes(type)) return answer;
    return DRIVER_STATE;
  };

const card = async (name: RegExp) => within(await screen.findByRole('region', { name }));
const terminal = () => card(/^(Terminal|终端)$/);
const service = () => card(/^(Service|服务)$/);
const sent = (route: string) => invoke.mock.calls.filter(([, type]) => type === route).map(([, , request]) => request?.payload);

describe('This Daoris → set up from the page', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('opens a folder by the id the read gave, never a path', async () => {
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const folders = await card(/^(Folders|文件夹)$/);

    await userEvent.click((await folders.findAllByRole('button', { name: 'Open folder' }))[0]!);

    await vi.waitFor(() => expect(sent('SYSTEM_OPEN_FOLDER')).toEqual([{ folder: 'home' }]));
    for (const payload of sent('SYSTEM_OPEN_FOLDER')) expect(JSON.stringify(payload)).not.toMatch(/[\\/]/);
  });

  it('says a folder that did not open in the catalogue\'s words', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(shell(INSTALLED, { done: false, changed: false, why: 'FOLDER_NOT_OPENED', message: 'English.' }));
    show(<SettingsView notify={notify} section="wiring" />);

    await userEvent.click((await (await card(/^(Folders|文件夹)$/)).findAllByRole('button', { name: 'Open folder' }))[0]!);

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith('The folder could not be opened: it is not there yet, or the file manager failed.'));
  });

  it('says first what Put on my PATH will do, and sends nothing until the second press', async () => {
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    await userEvent.click(await card.findByRole('button', { name: 'Put on my PATH…' }));
    const ask = within(card.getByRole('group', { name: 'Put this install on your PATH?' }));
    expect(ask.getByText(/Your account's PATH only, never the machine's/)).toBeTruthy();
    expect(ask.getByText(BIN)).toBeTruthy();
    expect(ask.getByText('Terminals opened after it find daoris.')).toBeTruthy();
    expect(ask.getByText(/Another daoris comes first and still will/)).toBeTruthy();
    expect(sent('SYSTEM_PATH')).toEqual([]);

    await userEvent.click(ask.getByRole('button', { name: 'Put on my PATH' }));
    await vi.waitFor(() => expect(sent('SYSTEM_PATH')).toEqual([{ on: true }]));
  });

  it('offers Remove from my PATH where it is on, saying first that it removes exactly that folder', async () => {
    invoke.mockImplementation(shell({ ...INSTALLED, terminal: { ...INSTALLED.terminal, onAccountPath: true } }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    await userEvent.click(await card.findByRole('button', { name: 'Remove from my PATH…' }));
    const ask = within(card.getByRole('group', { name: 'Remove this install from your PATH?' }));
    expect(ask.getByText("Removes exactly this folder from your account's PATH:")).toBeTruthy();
    await userEvent.click(ask.getByRole('button', { name: 'Remove from my PATH' }));

    await vi.waitFor(() => expect(sent('SYSTEM_PATH')).toEqual([{ on: false }]));
  });

  it('offers no PATH press in a development run or where the PATH was not read, and says why', async () => {
    invoke.mockImplementation(shell({ ...INSTALLED, terminal: { ...INSTALLED.terminal, bin: null, onAccountPath: null } }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    expect(await card.findByText('A development run has no launchers to put on a PATH')).toBeTruthy();
    expect(card.queryByRole('button', { name: /my PATH/ })).toBeNull();
  });

  it.each([
    ['PATH_NOT_INSTALL', 'A development run has no launchers, so there is nothing to put on a PATH. Nothing was changed.'],
    ['PATH_NOT_WINDOWS', 'Only Windows keeps an account PATH here. Nothing was changed.'],
    ['PATH_UNREADABLE', "Your account's PATH could not be read, so nothing was written."],
    ['PATH_NOT_WRITTEN', "Your account's PATH could not be written, so it is as it was."],
  ])('says the PATH refusal %s inside the ask and keeps it open', async (why, words) => {
    invoke.mockImplementation(shell(INSTALLED, { done: false, changed: false, why, message: 'English.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Put on my PATH…' }));
    const ask = within(card.getByRole('group', { name: 'Put this install on your PATH?' }));

    await userEvent.click(ask.getByRole('button', { name: 'Put on my PATH' }));

    expect((await ask.findByRole('alert')).textContent).toBe(words);
  });

  it('copies the terminal line for the shell chosen, and presses nothing', async () => {
    const written = vi.fn(() => Promise.resolve());
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText: written } });
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    const facts = { home: INSTALLED.folder.path, url: INSTALLED.service.url, bin: BIN };

    await userEvent.click(await card.findByRole('button', { name: 'Copy line' }));
    expect(written).toHaveBeenLastCalledWith(terminalLine('powershell', facts));
    await userEvent.click(card.getByRole('radio', { name: 'Git Bash' }));
    await userEvent.click(card.getByRole('button', { name: 'Copy line' }));
    expect(written).toHaveBeenLastCalledWith(terminalLine('bash', facts));
    for (const route of ROUTES) expect(sent(route)).toEqual([]);
  });

  it('builds the line for each terminal with the home, the address and the launchers first on the PATH', () => {
    const facts = { home: 'C:\\Teams\\me\\Daoris', url: 'http://127.0.0.1:5198', bin: BIN };

    expect(terminalLine('powershell', facts)).toBe(
      "$env:DAORIS_HOME = 'C:\\Teams\\me\\Daoris'; $env:DAORIS_SERVICE_URL = 'http://127.0.0.1:5198'; $env:PATH = 'C:\\Daoris\\app\\bin;' + $env:PATH");
    expect(terminalLine('cmd', facts)).toBe(
      'set "DAORIS_HOME=C:\\Teams\\me\\Daoris" && set "DAORIS_SERVICE_URL=http://127.0.0.1:5198" && set "PATH=C:\\Daoris\\app\\bin;%PATH%"');
    expect(terminalLine('bash', facts)).toBe(
      "export DAORIS_HOME='/c/Teams/me/Daoris'; export DAORIS_SERVICE_URL='http://127.0.0.1:5198'; export PATH='/c/Daoris/app/bin':\"$PATH\"");
    // A part with nothing to say is left out, and a quote is escaped for the shell it is in.
    expect(terminalLine('powershell', { home: "C:\\O'Neil", url: null, bin: null })).toBe("$env:DAORIS_HOME = 'C:\\O''Neil'");
    expect(terminalLine('bash', { home: "/o'neil", url: null, bin: null })).toBe("export DAORIS_HOME='/o'\\''neil'");
  });

  it("says the old address and the new first, then sets the account's variable", async () => {
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await service();

    await userEvent.click(await card.findByRole('button', { name: 'Change…' }));
    const input = card.getByRole('textbox', { name: "The service's address" });
    await userEvent.clear(input);
    await userEvent.type(input, 'http://127.0.0.1:6001');
    const ask = within(card.getByRole('group', { name: "Change the service's address?" }));
    expect(ask.getByText("Sets your account's DAORIS_SERVICE_URL from http://localhost:5177 to http://127.0.0.1:6001.")).toBeTruthy();
    expect(ask.getByText('It takes effect when Daoris starts again, and a terminal reads it too.')).toBeTruthy();
    expect(sent('SYSTEM_SERVICE_ADDRESS')).toEqual([]);

    await userEvent.click(ask.getByRole('button', { name: 'Change address' }));
    await vi.waitFor(() => expect(sent('SYSTEM_SERVICE_ADDRESS')).toEqual([{ url: 'http://127.0.0.1:6001' }]));
  });

  it('clears the address for the default with a null', async () => {
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await service();

    await userEvent.click(await card.findByRole('button', { name: 'Use the default…' }));
    await userEvent.click(within(card.getByRole('group', { name: "Change the service's address?" })).getByRole('button', { name: 'Change address' }));

    await vi.waitFor(() => expect(sent('SYSTEM_SERVICE_ADDRESS')).toEqual([{ url: null }]));
  });

  it.each([
    ['SERVICE_NOT_LOOPBACK', "The local service is sent this machine's paths, so its address must be this machine's own: localhost, 127.0.0.1 or [::1]."],
    ['SERVICE_MALFORMED', 'That is not an address: use http:// or https:// with a host and a port, and nothing else.'],
    ['SERVICE_NOT_WRITTEN', "Your account's variables could not be written, so they are as they were."],
  ])('says the address refusal %s inside the ask', async (why, words) => {
    invoke.mockImplementation(shell(INSTALLED, { done: false, changed: false, why, message: 'English.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await service();
    await userEvent.click(await card.findByRole('button', { name: 'Change…' }));
    const ask = within(card.getByRole('group', { name: "Change the service's address?" }));

    await userEvent.click(ask.getByRole('button', { name: 'Change address' }));

    expect((await ask.findByRole('alert')).textContent).toBe(words);
  });

  it('says a code a later host adds in the host\'s own sentence', async () => {
    invoke.mockImplementation(shell(INSTALLED, { done: false, changed: false, why: 'LATER', message: 'A later reason.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Put on my PATH…' }));
    const ask = within(card.getByRole('group', { name: 'Put this install on your PATH?' }));

    await userEvent.click(ask.getByRole('button', { name: 'Put on my PATH' }));

    expect((await ask.findByRole('alert')).textContent).toBe('Refused: A later reason.');
  });

  it('says the PATH ask and a refusal in Chinese', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(INSTALLED, { done: false, changed: false, why: 'PATH_UNREADABLE', message: 'English.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    await userEvent.click(await card.findByRole('button', { name: '加入我的 PATH…' }));
    const ask = within(card.getByRole('group', { name: '把本安装加入你的 PATH？' }));
    expect(ask.getByText(/只改你的账户 PATH，绝不改机器的/)).toBeTruthy();
    await userEvent.click(ask.getByRole('button', { name: '加入我的 PATH' }));
    expect((await ask.findByRole('alert')).textContent).toBe('无法读取你的账户 PATH，所以没有写入任何内容。');
  });

  it('says the address ask in Chinese', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell());
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await service();

    await userEvent.click(await card.findByRole('button', { name: '更改…' }));
    const ask = within(card.getByRole('group', { name: '更改服务地址？' }));
    expect(ask.getByText('Daoris 再次启动时生效，终端也会读取它。')).toBeTruthy();
  });
});
