import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → This Daoris (WIRING1c-i, the this-Daoris design §3.1-3.3, §7): the folders, the service and the loop of one
// install, read from the shell's `SYSTEM_WIRING`; it shows and sets nothing. A shell older than the page answers the route
// `NO_ROUTE`, and the page says so.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import i18n from '../locales/i18n';
import { code } from '../test/code';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import type { SystemWiring } from '../bridge/system';
import { DEVELOPMENT, MEASURING, WIRED } from './wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

/** The shell: the wiring as given for `SYSTEM_WIRING` (a function may ask the payload), the driver's state for the rest. */
const shell = (wiring: SystemWiring | ((payload?: { measure?: boolean }) => SystemWiring)) =>
  async (module: string, type: string, request?: { payload?: { measure?: boolean } }) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return typeof wiring === 'function' ? wiring(request?.payload) : wiring;
    return DRIVER_STATE;
  };

const open = (onGo = vi.fn()) => {
  show(<SettingsView notify={() => {}} section="wiring" onGo={onGo} />);
  return onGo;
};

describe('Settings → This Daoris', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('is a domain of its own, after AI features, and asks the sizes once when it opens', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();

    expect(await screen.findByRole('region', { name: 'Folders' })).toBeTruthy();
    // The open itself asks no measure, so it walks no folder; the ask that follows does.
    expect(invoke.mock.calls.some(([, type, request]) => type === 'SYSTEM_WIRING' && !request?.payload)).toBe(true);
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYSTEM_WIRING', { payload: { measure: true } }));
    expect(invoke.mock.calls.filter(([, , request]) => request?.payload?.measure)).toHaveLength(1);
  });

  it('shows skeleton rows until the first answer', async () => {
    invoke.mockImplementation(async (module: string, type: string) => (
      type === 'SYSTEM_WIRING' ? new Promise(() => {}) : shell(WIRED)(module, type)));
    show(<SettingsView notify={() => {}} section="wiring" />);

    await vi.waitFor(() => expect(document.querySelector('[aria-busy="true"]')).toBeTruthy());
    expect(screen.queryByRole('region', { name: 'Folders' })).toBeNull();
  });

  it('shows the Daoris home with how it stands to the account, and each folder with its size and when it was counted', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));

    expect(folders.getByText('D:/somewhere/Daoris')).toBeTruthy();
    expect(folders.getByText('installed')).toBeTruthy();
    expect(folders.getByText("A terminal's daoris reads this folder.")).toBeTruthy();
    for (const name of ['Program', 'Update', 'Your settings and records', 'Sign-ins', 'Trees and kept files', 'Downloads', 'While running', 'Machine log']) {
      expect(folders.getByText(name)).toBeTruthy();
    }
    // Sizes only through format.ts: 310,000,000 bytes is 295.6 MB, 812 bytes stays bytes.
    expect(folders.getByText('295.6 MB')).toBeTruthy();
    expect(folders.getByText('812 B')).toBeTruthy();
    // A folder not there yet says so and shows no size.
    expect(folders.getByText('Not there yet')).toBeTruthy();
    expect(folders.getAllByText(/measured /).length).toBeGreaterThan(0);
    expect(folders.getByText(code('daoris-driver wiring'))).toBeTruthy();
  });

  it('says Measuring… for each size while a walk is under way, and holds the button until it ends', async () => {
    invoke.mockImplementation(shell(MEASURING));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));

    // Every folder that is there, and what is not Daoris's; a folder not there yet has nothing to count.
    expect(folders.getAllByText('Measuring…')).toHaveLength(MEASURING.folders.filter((each) => each.present).length + MEASURING.leftOver.length);
    expect(folders.getByRole('button', { name: 'Measure again' })).toBeDisabled();
  });

  it('asks again from Measure again, once the sizes are counted', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));
    const asked = () => invoke.mock.calls.filter(([, , request]) => request?.payload?.measure).length;
    await vi.waitFor(() => expect(asked()).toBe(1));

    await userEvent.click(await folders.findByRole('button', { name: 'Measure again' }));
    await vi.waitFor(() => expect(asked()).toBe(2));
  });

  it('puts an entry still at its old place under its folder, with why, and what is not Daoris\'s with its size', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));

    expect(folders.getByText('trees')).toBeTruthy();
    expect(folders.getByText('stays: a conversation may resume in it')).toBeTruthy();
    expect(folders.getByText('Not Daoris\'s, or an earlier build\'s')).toBeTruthy();
    expect(folders.getByText('webview2')).toBeTruthy();
    expect(folders.getByText('left by an earlier build')).toBeTruthy();
    expect(folders.getByText('7 MB')).toBeTruthy();
  });

  it('says a development run at its head, and the two notices the home carried under it', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));

    expect(screen.getAllByText('development run').length).toBeGreaterThan(0);
    expect(screen.getByText(/runs from a build folder, not from an install/)).toBeTruthy();
    expect(folders.getByText(/this install's own data folder, not D:\/somewhere\/first\/data/)).toBeTruthy();
    expect(folders.getByText(/serves a different page than this install carries/)).toBeTruthy();
    expect(folders.getByText(/names this install's data folder, which is read as this install/)).toBeTruthy();
  });

  it('shows the service: its address and source, who started it, whether it answers, its tier and version, its programs, the key and the remotes', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const service = within(await screen.findByRole('region', { name: 'Service' }));

    expect(service.getByText('http://localhost:5177')).toBeTruthy();
    expect(service.getByText('the default address')).toBeTruthy();
    expect(service.getByText('this window')).toBeTruthy();
    expect(service.getByText('answering')).toBeTruthy();
    expect(service.getByText('semantic')).toBeTruthy();
    expect(service.getByText('0.21.0')).toBeTruthy();
    expect(service.getByText('D:/somewhere/Daoris/app/daoris-http/Daoris.Service.Http')).toBeTruthy();
    expect(service.getAllByText('beside the application')).toHaveLength(2);
    expect(service.getByText('proved')).toBeTruthy();
    // The workspace's remote: its address, the key's audit prefix and never the key, and where it was read.
    expect(service.getByText('aurora')).toBeTruthy();
    expect(service.getByText('https://aurora.example.com')).toBeTruthy();
    expect(service.getByText('key dk_abcd1234')).toBeTruthy();
    expect(service.getByText('from a file')).toBeTruthy();
  });

  it('leads the service with why it is not answering, and says each fact it could not read in its own words', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const service = within(await screen.findByRole('region', { name: 'Service' }));

    expect(service.getByText('not answering')).toBeTruthy();
    expect(service.getByText("Another install's service answered")).toBeTruthy();
    expect(service.getByText('adopted')).toBeTruthy();
    expect(service.getAllByText('Not found')).toHaveLength(2);
    expect(service.getAllByText('The service is not answering').length).toBeGreaterThan(0);
  });

  it('shows the loop: who holds the home, when it last looked, what holds its starts, how often it looks; with the door to Driver', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const loop = within(await screen.findByRole('region', { name: 'Driver loop' }));

    expect(loop.getByText('This window')).toBeTruthy();
    expect(loop.getByText(/^process 4242, since /)).toBeTruthy();
    expect(loop.getByText('Nothing is held')).toBeTruthy();
    expect(loop.getByText('30 seconds')).toBeTruthy();
    await userEvent.click(loop.getAllByRole('button', { name: 'Open Driver' })[0]!);
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'driver' });
  });

  it('names a terminal that holds the home, and a drain for an update with its holds', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const loop = within(await screen.findByRole('region', { name: 'Driver loop' }));

    expect(loop.getByText("A terminal's daoris-driver drive")).toBeTruthy();
    expect(loop.getByText('Draining for an update: nothing new starts.')).toBeTruthy();
    expect(loop.getByText('2 held by driver.json')).toBeTruthy();
  });

  it('says nobody when no one holds the home, and starting until the loop is up', async () => {
    invoke.mockImplementation(shell({
      ...WIRED, loop: { ...WIRED.loop, holder: null, looked: null, lookedWhy: 'starting' },
    }));
    open();
    const loop = within(await screen.findByRole('region', { name: 'Driver loop' }));

    expect(loop.getByText('Nobody')).toBeTruthy();
    expect(loop.getByText('Starting')).toBeTruthy();
  });

  it('lists each settings file with what it holds, its size, whether it reads and what overrides it, and never its contents', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const files = within(await screen.findByRole('region', { name: 'Settings files' }));

    expect(files.getByText('driver.json')).toBeTruthy();
    expect(files.getByText("The driver's dials: its pace, the starts it holds and its limits.")).toBeTruthy();
    expect(files.getAllByText('1.2 KB').length).toBeGreaterThan(0);
    expect(files.getAllByText('reads').length).toBeGreaterThan(0);
    expect(files.getByText('named by DAORIS_HARNESS_CONFIG')).toBeTruthy();
    // A file that does not parse says what follows from it; one not there yet says so.
    expect(files.getByText('cannot be read')).toBeTruthy();
    expect(files.getByText(/The screens that edit it refuse until it is fixed/)).toBeTruthy();
    expect(files.getByText('Not there yet')).toBeTruthy();
    // The records are folded away, with no door.
    expect(files.queryByText('usage.json')).toBeNull();
    await userEvent.click(files.getByRole('button', { name: 'Records' }));
    expect(files.getByText('usage.json')).toBeTruthy();
    expect(files.getByText('knowledge.db')).toBeTruthy();
  });

  it('opens where a file is edited by a door, and never a file', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const files = within(await screen.findByRole('region', { name: 'Settings files' }));

    await userEvent.click(files.getByRole('button', { name: 'Open Tools' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'tools' });
    await userEvent.click(files.getAllByRole('button', { name: 'Open Driver' })[0]!);
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'driver' });
    await userEvent.click(files.getByRole('button', { name: 'Open Agents' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'agents' });
    await userEvent.click(files.getByRole('button', { name: 'Open Browser' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'browser' });
  });

  it('says a development run\'s files that could not be read or are not there, in their own words', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const files = within(await screen.findByRole('region', { name: 'Settings files' }));

    expect(files.getByText('Could not be read')).toBeTruthy();
    expect(files.getByText('plugins.json')).toBeTruthy();
  });

  it('shows each variable that is set with its value and where it came from, a key as its prefix and never its value', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const variables = within(await screen.findByRole('region', { name: 'Variables' }));

    expect(variables.getByText('DAORIS_HOME')).toBeTruthy();
    expect(variables.getByText('D:/somewhere/Daoris')).toBeTruthy();
    expect(variables.getByText('set by Daoris for this window')).toBeTruthy();
    // Where the window's value differs from the account's, both.
    expect(variables.getByText("Your account's: D:/somewhere/first/data")).toBeTruthy();
    expect(variables.getByText('inherited from your account')).toBeTruthy();
    expect(variables.getByText('DAORIS_SERVICE_KEY')).toBeTruthy();
    expect(variables.getByText('set, key dk_abcd1234')).toBeTruthy();
    expect(variables.queryByText('DAORIS_REMOTE_KEY')).toBeNull();
  });

  it('folds the variables that are not set away, each with what is used instead', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const variables = within(await screen.findByRole('region', { name: 'Variables' }));

    expect(variables.queryByText('DAORIS_EMBED_MODEL')).toBeNull();
    await userEvent.click(variables.getByRole('button', { name: '3 not set' }));
    expect(variables.getByText('DAORIS_EMBED_MODEL')).toBeTruthy();
    expect(variables.getByText('used instead: keyword search only')).toBeTruthy();
    expect(variables.getByText('used instead: 2000')).toBeTruthy();
    expect(variables.getByText('DAORIS_REMOTE_KEY')).toBeTruthy();
  });

  it('says an adopted service\'s variables are its own, and a value set for this start apart from the account\'s', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const variables = within(await screen.findByRole('region', { name: 'Variables' }));

    expect(variables.getByText('set for this start')).toBeTruthy();
    expect(variables.getByText("Your account's: http://localhost:6000")).toBeTruthy();
    await userEvent.click(variables.getByRole('button', { name: '1 not set' }));
    expect(variables.getByText('DAORIS_KNOWLEDGE_ROOT')).toBeTruthy();
    expect(variables.getByText('The service was already running; it knows its own')).toBeTruthy();
  });

  it('shows the versions, the one not read in its own words with its door, and the update in one line', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const versions = within(await screen.findByRole('region', { name: 'Versions' }));

    expect(versions.getByText('0.21.0 (abc1234)')).toBeTruthy();
    expect(versions.getByText('Shenora 0.21.1')).toBeTruthy();
    expect(versions.getByText('10.0.1')).toBeTruthy();
    expect(versions.getByText('Settings → Tools says it')).toBeTruthy();
    expect(await versions.findByText('Nothing is staged beside this install.')).toBeTruthy();
    await userEvent.click(versions.getByRole('button', { name: 'Open Tools' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'tools' });
    await userEvent.click(versions.getByRole('button', { name: 'Open Driver' }));
    expect(onGo).toHaveBeenLastCalledWith({ view: 'settings', section: 'driver' });
  });

  it('says what a development run does not carry, and that it is not updated', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const versions = within(await screen.findByRole('region', { name: 'Versions' }));

    expect(versions.getAllByText('A development run has none')).toHaveLength(2);
    expect(versions.getByText('A development run is not updated.')).toBeTruthy();
    expect(versions.getByText('Only the window knows')).toBeTruthy();
  });

  it('shows the terminal: the launchers, the PATH, what a new terminal runs, the address file and the account\'s two variables', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const terminal = within(await screen.findByRole('region', { name: 'Terminal' }));

    expect(terminal.getByText('daoris-driver.cmd')).toBeTruthy();
    expect(terminal.getByText('missing')).toBeTruthy();
    expect(terminal.getByText("On your account's PATH")).toBeTruthy();
    expect(terminal.getByText("This install's")).toBeTruthy();
    expect(terminal.getByText('Another')).toBeTruthy();
    expect(terminal.getByText('D:/somewhere/else/bin/daoris-driver.cmd')).toBeTruthy();
    // The address file's, and the account's DAORIS_SERVICE_URL, which a terminal reads first.
    expect(terminal.getAllByText('http://localhost:5177')).toHaveLength(2);
    expect(terminal.getByText(/^process 4242, since /)).toBeTruthy();
    expect(terminal.getByText('taken')).toBeTruthy();
    expect(terminal.getByText('D:/somewhere/first/data')).toBeTruthy();
  });

  it('says a development run has no launchers, and why a terminal does not take the address file', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const terminal = within(await screen.findByRole('region', { name: 'Terminal' }));

    expect(terminal.getAllByText('A development run has none').length).toBeGreaterThan(0);
    expect(terminal.getByText('Its process has ended')).toBeTruthy();
    expect(terminal.getByText('not taken')).toBeTruthy();
    expect(terminal.getAllByText('None').length).toBe(2);
  });

  it('shows the knowledge store: its file and where that was named, its size, the family root and the tier, with a door to AI features', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const knowledge = within(await screen.findByRole('region', { name: 'Knowledge store' }));

    expect(knowledge.getByText('D:/somewhere/Daoris/data/knowledge.db')).toBeTruthy();
    expect(knowledge.getByText("in the Daoris home's data folder")).toBeTruthy();
    expect(knowledge.getByText(/MB$/)).toBeTruthy();
    expect(knowledge.getByText('D:/somewhere/family')).toBeTruthy();
    expect(knowledge.getByText('semantic')).toBeTruthy();
    await userEvent.click(knowledge.getByRole('button', { name: 'Open AI features' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'ai' });
  });

  it('says why the knowledge store has no root or size, and where its file was named', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const knowledge = within(await screen.findByRole('region', { name: 'Knowledge store' }));

    expect(knowledge.getByText('from the variable DAORIS_KNOWLEDGE_DB')).toBeTruthy();
    expect(knowledge.getByText('The service was already running; it knows its own')).toBeTruthy();
    expect(knowledge.getByText('Not there')).toBeTruthy();
  });

  it('shows the logs: the folder, its size, the days it keeps, its newest day and the engine\'s log, with doors', async () => {
    invoke.mockImplementation(shell(WIRED));
    const onGo = open();
    const logs = within(await screen.findByRole('region', { name: 'Logs' }));

    expect(logs.getByText('D:/somewhere/Daoris/logs')).toBeTruthy();
    expect(logs.getByText('93.8 KB')).toBeTruthy();
    expect(logs.getByText('30 days')).toBeTruthy();
    expect(logs.getByText('present')).toBeTruthy();
    expect(logs.getByRole('button', { name: 'Open the folder' })).toBeTruthy();
    await userEvent.click(logs.getByRole('button', { name: 'Open Machine log' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'settings', section: 'logs' });
  });

  it('says the logs\' size is not measured and the engine\'s log is not there', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const logs = within(await screen.findByRole('region', { name: 'Logs' }));

    expect(logs.getByText('Not measured yet')).toBeTruthy();
    expect(logs.getByText('Not there')).toBeTruthy();
    expect(logs.getByText('not there')).toBeTruthy();
  });

  it('offers no press that opens, copies or changes anything: the page shows', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    await screen.findByRole('region', { name: 'Folders' });

    const pressed = screen.getAllByRole('button').map((button) => button.textContent);
    expect(pressed.filter((name) => /^(Open folder|Copy terminal line|Put on my PATH|Change address)/.test(name ?? ''))).toEqual([]);
  });

  // OWNSPACE2 (D175 point 3, D88): what Daoris's sessions left in the person's own tool home, listed and removed by the press.
  it('lists the folders Daoris left in the person\'s own tool home, read-only until the press', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    for (const each of WIRED.toolHome!.folders) expect(card.getByText(each.name)).toBeTruthy();
    expect(card.getByText('2.3 MB')).toBeTruthy();
    expect(card.getByText('D:/somewhere/person/.claude')).toBeTruthy();
    // A folder a conversation may go on in is listed and says it stays; it is never offered to move.
    expect(card.getByText('stays: a conversation that began here goes on here')).toBeTruthy();
    expect(card.getByText(/named by CLAUDE_CONFIG_DIR|its usual place/)).toBeTruthy();
    expect(card.getByRole('button', { name: 'Move these aside' })).toBeTruthy();
    expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_TOOL_CLEAR')).toBe(false);
  });

  it('moves aside exactly the folders listed as free to go, by name and identity, after an ask, and reads the wiring again', async () => {
    const free = WIRED.toolHome!.folders.filter((each) => each.keeps === null);
    const answered = { cleared: free.map((each) => ({ name: each.name, bytes: each.bytes, to: 'D:/somewhere/Daoris/trees/.tidied/tool-home/projects/x-20261016T120000000Z' })), kept: [] };
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { measure?: boolean } }) =>
      (type === 'SYSTEM_TOOL_CLEAR' ? answered : shell(WIRED)(module, type, request)));
    show(<SettingsView notify={notify} section="wiring" onGo={vi.fn()} />);
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    await userEvent.click(card.getByRole('button', { name: 'Move these aside' }));
    // The ask says it is moved, for how long, and that nothing else is touched; nothing is sent until it is confirmed.
    expect(card.getByText(/for 14 days/i)).toBeTruthy();
    expect(card.getByText(/your own work is not touched/i)).toBeTruthy();
    expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_TOOL_CLEAR')).toBe(false);
    const wirings = () => invoke.mock.calls.filter(([, type]) => type === 'SYSTEM_WIRING').length;
    const before = wirings();
    await userEvent.click(card.getByRole('button', { name: 'Move them aside' }));

    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYSTEM_TOOL_CLEAR', {
      payload: { folders: free.map((each) => ({ name: each.name, id: each.id })) },
    }));
    await vi.waitFor(() => expect(wirings()).toBeGreaterThan(before));
    // It says where they went and for how long, never that they were deleted.
    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/Moved aside: 1, to .*projects\. Daoris keeps them 14 days/)));
  });

  it('says a folder the driver kept, and why', async () => {
    const notify = vi.fn();
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { measure?: boolean } }) => (
      type === 'SYSTEM_TOOL_CLEAR'
        ? { cleared: [], kept: [{ name: WIRED.toolHome!.folders[0]!.name, why: 'in-use' }] }
        : shell(WIRED)(module, type, request)));
    show(<SettingsView notify={notify} section="wiring" onGo={vi.fn()} />);
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    await userEvent.click(card.getByRole('button', { name: 'Move these aside' }));
    await userEvent.click(card.getByRole('button', { name: 'Move them aside' }));

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/stayed: 1.*written to lately/i)));
  });

  it('lists nothing and says why where the home named is not the person\'s own, and offers no press', async () => {
    const refused = { ...WIRED.toolHome!, path: null, pathWhy: 'inside-daoris', from: 'process' as const, why: 'no-tool-home', folders: [] };
    invoke.mockImplementation(shell({ ...WIRED, toolHome: refused }));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    expect(card.getByText("Inside Daoris's own folder")).toBeTruthy();
    expect(card.getByText(/No own home is named here/)).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Move these aside' })).toBeNull();
  });

  it('says there is nothing left, and offers no press, where the tool home holds none of Daoris\'s', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    expect(card.getByText(/nothing of Daoris's is left/i)).toBeTruthy();
    expect(card.queryByRole('button', { name: 'Move these aside' })).toBeNull();
  });

  it('draws no card from a shell older than the list', async () => {
    invoke.mockImplementation(shell({ ...WIRED, toolHome: undefined }));
    open();
    await screen.findByRole('region', { name: 'Folders' });

    expect(screen.queryByRole('region', { name: 'Left in your own tool home' })).toBeNull();
  });

  it('a shell older than the page: one line saying so, and no card', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.DRIVER' && type === 'SYSTEM_WIRING') throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
      return module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING;
    });
    open();

    expect(await screen.findByText(/This window is older than this page/)).toBeTruthy();
    expect(screen.queryByRole('region', { name: 'Folders' })).toBeNull();
  });

  it('speaks 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();

    expect(await screen.findByRole('region', { name: '文件夹' })).toBeTruthy();
    expect(screen.getByRole('region', { name: '服务' })).toBeTruthy();
    expect(screen.getByRole('region', { name: '驱动循环' })).toBeTruthy();
    expect(screen.getAllByText('开发运行').length).toBeGreaterThan(0);
    for (const name of ['设置文件', '环境变量', '版本', '终端', '知识库', '日志', '留在你自己工具主目录里']) expect(screen.getByRole('region', { name })).toBeTruthy();
  });
});
