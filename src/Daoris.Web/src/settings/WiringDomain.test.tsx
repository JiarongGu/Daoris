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
import { clockOf, dayOf, size } from '../locales/format';
import { code } from '../test/code';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import type { SystemSpace, SystemWiring } from '../bridge/system';
import {
  DEVELOPMENT, DOWNLOADS, MEASURING, SPACE, SPACE_COUNTING, SPACE_FREED, SPACE_PLAN, SPACE_RUNNING, SPACE_SET, SPACE_STOPPED,
  SPACE_UNCOUNTED, WIRED,
} from './wiring/wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };

/**
 * The shell: the wiring as given for `SYSTEM_WIRING` (a function may ask the payload), the disk space for `SYSTEM_SPACE` (KEEP1c), the
 * driver's state for the rest.
 */
const shell = (wiring: SystemWiring | ((payload?: { measure?: boolean }) => SystemWiring), space: SystemSpace = SPACE) =>
  async (module: string, type: string, request?: { payload?: { measure?: boolean } }) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return typeof wiring === 'function' ? wiring(request?.payload) : wiring;
    if (type === 'SYSTEM_SPACE') return space;
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

  // OWNSPACE2 (D175 point 3, D88): what Daoris's sessions left in the person's own tool home, listed and nothing more (the press is OWNSPACE2e).
  it('lists the folders Daoris left in the person\'s own tool home, read-only, with the records that name them', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    for (const each of WIRED.toolHome!.folders) expect(card.getByText(each.name)).toBeTruthy();
    expect(card.getByText('2.3 MB')).toBeTruthy();
    expect(card.getByText('D:/somewhere/person/.claude')).toBeTruthy();
    // Why each is Daoris's (the records that name it) and what would keep it; a folder a conversation may go on in says so.
    expect(card.getByText('named by q-1-s-1a2b3c4d')).toBeTruthy();
    expect(card.getByText('named by help-1, help-2')).toBeTruthy();
    expect(card.getByText('still in use: a conversation that began here goes on here')).toBeTruthy();
    expect(card.getByText(/named by CLAUDE_CONFIG_DIR|its usual place/)).toBeTruthy();
    // Read-only: they can be removed by hand, and nothing here is a press that sends or changes anything.
    expect(card.getByText(/does not remove these yet\. You can delete one by hand/)).toBeTruthy();
    expect(card.queryAllByRole('button')).toEqual([]);
    expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_TOOL_CLEAR')).toBe(false);
  });

  it('says Daoris\'s session records could not be read, and lists nothing, when there is no record of use', async () => {
    invoke.mockImplementation(shell({ ...WIRED, toolHome: { ...WIRED.toolHome!, why: 'records-unread', folders: [] } }));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    expect(card.getByText(/session records could not be read/)).toBeTruthy();
    expect(card.queryByText(WIRED.toolHome!.folders[0]!.name)).toBeNull();
  });

  it('lists nothing and says why where the home named is not the person\'s own', async () => {
    const refused = { ...WIRED.toolHome!, path: null, pathWhy: 'inside-daoris', from: 'process' as const, why: 'no-tool-home', folders: [] };
    invoke.mockImplementation(shell({ ...WIRED, toolHome: refused }));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    expect(card.getByText("Inside Daoris's own folder")).toBeTruthy();
    expect(card.getByText(/No own home is named here/)).toBeTruthy();
  });

  it('says there is nothing left where the tool home holds none of Daoris\'s', async () => {
    invoke.mockImplementation(shell(DEVELOPMENT));
    open();
    const card = within(await screen.findByRole('region', { name: 'Left in your own tool home' }));

    expect(card.getByText(/nothing of Daoris's is left/i)).toBeTruthy();
    expect(card.queryAllByRole('button')).toEqual([]);
  });

  // CACHECLEAR1 (D88's shape): the Downloads row lists the agents' and tools' downloaded versions with what keeps each, then one
  // press removes those nothing uses; the driver judges each again, and what it kept at the press is said.
  const withDownloads = (cleared?: (versions: unknown) => unknown) =>
    async (module: string, type: string, request?: { payload?: { measure?: boolean; versions?: unknown } }) => {
      if (module === 'DAORIS.DRIVER' && type === 'SYSTEM_DOWNLOADS') return DOWNLOADS;
      if (module === 'DAORIS.DRIVER' && type === 'SYSTEM_DOWNLOADS_CLEAR') return cleared?.(request?.payload?.versions);
      return shell(WIRED)(module, type, request);
    };

  it('lists the downloads only once the row is opened, each with its size and what keeps it', async () => {
    invoke.mockImplementation(withDownloads());
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));
    expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_DOWNLOADS')).toBe(false);

    await userEvent.click(folders.getByRole('button', { name: 'Clear unused downloads…' }));
    const list = within(await screen.findByRole('region', { name: 'Clear downloads' }));

    expect(await list.findByText('claude-code 2.0.1')).toBeTruthy();
    expect(list.getAllByText('nothing uses it')).toHaveLength(2);
    expect(list.getByText('kept: pinned on this machine')).toBeTruthy();
    expect(list.getByText('kept: pinned by the games workspace')).toBeTruthy();
    expect(list.getByText('kept: downloaded within the hour, so a pin may be on its way')).toBeTruthy();
    expect(list.getByText('kept: Tools runs this version')).toBeTruthy();
    expect(list.getByRole('button', { name: /^Remove 2 unused versions \(.+\)$/ })).toBeTruthy();
    expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_DOWNLOADS_CLEAR')).toBe(false);
  });

  // KEEP1b (D195, the cleanup design §9): the row says each version's date by the rule, and a version a running session was handed is
  // kept; one no pass has found unused yet has no date.
  it('says the rule, when each unused version goes by it, and keeps what a running session was handed', async () => {
    invoke.mockImplementation(withDownloads());
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));
    await userEvent.click(folders.getByRole('button', { name: 'Clear unused downloads…' }));
    const list = within(await screen.findByRole('region', { name: 'Clear downloads' }));

    expect(await list.findByText('A version nothing uses goes by itself 7 days after Daoris first finds it unused.')).toBeTruthy();
    expect(list.getAllByText(/^goes by itself on /)).toHaveLength(1);
    expect(list.getByText(`goes by itself on ${clockOf('2026-10-22T09:00:00.000+00:00')}`)).toBeTruthy();
    expect(list.getByText('kept: a running session was handed it')).toBeTruthy();
  });

  it('removes only the rows nothing keeps, and says what the driver kept at the press', async () => {
    const asked = vi.fn();
    invoke.mockImplementation(withDownloads((versions) => {
      asked(versions);
      return { cleared: [
        { kind: 'agent', name: 'claude-code', version: '2.0.1', unfinished: false, outcome: 'removed', bytes: 120_000_000 },
        { kind: 'tool', name: 'gh', version: '2.61.0', unfinished: false, outcome: 'kept', bytes: 30_000_000, keeps: 'in-use', by: [] },
      ] };
    }));
    open();
    const folders = within(await screen.findByRole('region', { name: 'Folders' }));
    await userEvent.click(folders.getByRole('button', { name: 'Clear unused downloads…' }));
    const list = within(await screen.findByRole('region', { name: 'Clear downloads' }));

    await userEvent.click(await list.findByRole('button', { name: /^Remove 2 unused versions/ }));

    await vi.waitFor(() => expect(asked).toHaveBeenCalledWith([
      { kind: 'agent', name: 'claude-code', version: '2.0.1', unfinished: false },
      { kind: 'tool', name: 'gh', version: '2.61.0', unfinished: false },
    ]));
    expect(await list.findByText(/^Removed 1 version \(.+\)\.$/)).toBeTruthy();
    expect(list.getByText(/kept: a program is running from it$/).textContent).toBe('gh 2.61.0 · kept: a program is running from it');
  });

  it('lists and presses in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(withDownloads(() => ({ cleared: [] })));
    open();
    const folders = within(await screen.findByRole('region', { name: '文件夹' }));
    await userEvent.click(folders.getByRole('button', { name: '清除未使用的下载内容…' }));
    const list = within(await screen.findByRole('region', { name: '清除下载内容' }));

    expect((await list.findAllByText('没有任何地方使用')).length).toBe(2);
    expect(list.getByText('保留：本机固定了这个版本')).toBeTruthy();
    expect(list.getByText('保留：工作区 games 固定了这个版本')).toBeTruthy();
    expect(list.getAllByText('智能体').length).toBeGreaterThan(0);
    // KEEP1b: the rule, the date it goes and the running session's keep, in 中文.
    expect(list.getByText('没有任何地方使用的版本，会在 Daoris 首次发现它未被使用的 7 天后自动移除。')).toBeTruthy();
    expect(list.getByText(`将于 ${clockOf('2026-10-22T09:00:00.000+00:00', new Date(), 'zh')} 自动移除`)).toBeTruthy();
    expect(list.getByText('保留：一个正在运行的会话使用了它')).toBeTruthy();
    await userEvent.click(list.getByRole('button', { name: /^移除 2 个未使用的版本/ }));
    expect(await list.findByText(/^已移除 0 个版本/)).toBeTruthy();
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

  // KEEP1c (D195 point 5, the cleanup design §4.1, §4.2): the Disk space card, each kind's size, rule, next and last clean.
  it('shows the disk space after the folders: each kind with its size, rule, next and last, and counts nothing on the open', async () => {
    invoke.mockImplementation(shell(WIRED));
    open();

    const card = await screen.findByRole('region', { name: 'Disk space' });
    const regions = screen.getAllByRole('region').map((region) => region.getAttribute('aria-label'));
    expect(regions.indexOf('Disk space')).toBe(regions.indexOf('Folders') + 1);
    const scope = within(card);
    expect(scope.getByText(`${size(143.5 * 1024 ** 3)} of ${size(1024 ** 4)}`)).toBeTruthy();
    expect(scope.getByText('Set-aside trees')).toBeTruthy();
    expect(scope.getByText('kept 14 days; its package folders go an hour after the move')).toBeTruthy();
    expect(scope.getByText(
      `next: 3 folders, ${size(0.6 * 1024 ** 3)} on ${dayOf('2026-10-29T09:00:00.000+00:00')} · `
      + `last: ${clockOf('2026-10-16T08:10:00.000+00:00')}, 2 removed, ${size(1.1 * 1024 ** 3)}`,
    )).toBeTruthy();
    expect(scope.getByText('an unused version goes 7 days after it was found unused')).toBeTruthy();
    expect(scope.getByText('kept 30 days')).toBeTruthy();
    expect(scope.getByText('kept until you clear it')).toBeTruthy();
    expect(scope.getByText('cleaned up once its work is on the line')).toBeTruthy();
    expect(scope.getByText("the browser's own limit")).toBeTruthy();
    // What an earlier build left goes only by the person's press, which frees space, listed first (KEEP1e).
    expect(scope.getByText('removed only by your press')).toBeTruthy();
    expect(scope.getByRole('button', { name: 'Free up space…' })).toBeTruthy();
    // KEEP1d: the rules the person sets carry their editor and their terminal twin, history's age among them (KEEP1g).
    expect(scope.getAllByRole('button', { name: 'Change…' })).toHaveLength(4);
    expect(card.textContent).toContain('daoris driver keep history …');
    expect(card.textContent).toContain('daoris driver keep set-aside …');
    await vi.waitFor(() => expect(invoke.mock.calls.some(([, type]) => type === 'SYSTEM_SPACE')).toBe(true));
    expect(invoke.mock.calls.filter(([, type, request]) => type === 'SYSTEM_SPACE' && request?.payload)).toEqual([]);
  });

  it("counts one kind from its Count, and every kind from the card's Measure again", async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_UNCOUNTED));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    expect(card.getAllByText('not counted yet')).toHaveLength(9);

    await userEvent.click(card.getAllByRole('button', { name: 'Count' })[0]!);
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYSTEM_SPACE', { payload: { measure: ['set-aside'] } }));
    await userEvent.click(card.getByRole('button', { name: 'Measure again' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYSTEM_SPACE', { payload: { measure: true } }));
  });

  it("says a count under way, one that stopped short, a last it could not read and a terminal's run", async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_COUNTING));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect(card.getAllByText(/Counting…/).length).toBeGreaterThanOrEqual(2);
    expect(card.getByText(`at least ${size(2.1 * 1024 ** 3)}`)).toBeTruthy();
    expect(card.getByText(/more files than one count reads/)).toBeTruthy();
    expect(card.getByText(/last: not known, since its record could not be read/)).toBeTruthy();
    expect(card.getByText(`A run from a terminal is under way, since ${clockOf('2026-10-16T10:40:00.000+00:00')}.`)).toBeTruthy();
    expect((card.getByRole('button', { name: 'Measure again' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('says a run a crash cut short until the next run ends', async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_STOPPED));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect(card.getByText(`The last run, from the look, stopped before its end; it began ${clockOf('2026-10-16T03:00:00.000+00:00')}.`)).toBeTruthy();
  });

  /** The shell with Free up space's two forms (KEEP1e): its list, and a press answered as `started` says. */
  const freeing = (started: object = { run: 'r1', joined: false }, space: SystemSpace = SPACE) =>
    async (module: string, type: string, request?: { payload?: { measure?: boolean; plan?: boolean } }) => {
      if (module === 'DAORIS.DRIVER' && type === 'SYSTEM_SPACE_FREE') return request?.payload?.plan ? SPACE_PLAN : started;
      return shell(WIRED, space)(module, type, request);
    };

  const freeCalls = () => invoke.mock.calls.filter(([, type]) => type === 'SYSTEM_SPACE_FREE').map(([, , request]) => request?.payload);

  it('lists what goes now and what could go early under its head, and presses exactly what goes now', async () => {
    invoke.mockImplementation(freeing());
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    await userEvent.click(card.getByRole('button', { name: 'Free up space…' }));
    const list = within(await card.findByRole('region', { name: 'Free up space…' }));

    expect(await list.findByText('What goes now, by your rules')).toBeTruthy();
    expect(list.getByText(`3 items, ${size(0.6 * 1024 ** 3 + 4 * 1024 ** 2)}`)).toBeTruthy();
    expect(list.getByText(`2 folders past their days, ${size(0.6 * 1024 ** 3)}`)).toBeTruthy();
    // The log's days by the person's rule (KEEP1d), so the line names no number of days.
    expect(list.getByText(`1 file past its days, ${size(4 * 1024 ** 2)}`)).toBeTruthy();
    expect(list.getByText('What could go early, by your word')).toBeTruthy();
    expect(list.getByText('2 folders before their date · 1 holds files of its own')).toBeTruthy();
    expect(list.getByText('kept while the browser runs')).toBeTruthy();
    expect(list.getByText('2 folders · webview2, browser/profile')).toBeTruthy();
    expect(list.getByText("With their own press: Clear history… on a workspace's page · Clean up… on its Branches.")).toBeTruthy();
    expect(freeCalls()).toEqual([{ plan: true }]);

    await userEvent.click(list.getByRole('button', { name: 'Remove 3 now' }));

    await vi.waitFor(() => expect(freeCalls()).toHaveLength(2));
    expect(freeCalls()[1]).toEqual({
      units: SPACE_PLAN.now.map((unit) => ({ kind: unit.kind, id: unit.id, why: unit.why, own: false })),
      early: [],
    });
    // Started: the run's line says the rest, and the list closes.
    await vi.waitFor(() => expect(card.queryByRole('region', { name: 'Free up space…' })).toBeNull());
  });

  it('says an early group first in its own words, then sends only that group', async () => {
    invoke.mockImplementation(freeing());
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    await userEvent.click(card.getByRole('button', { name: 'Free up space…' }));
    const list = within(await card.findByRole('region', { name: 'Free up space…' }));
    await list.findByText('What could go early, by your word');

    await userEvent.click(list.getAllByRole('button', { name: 'Remove…' })[0]!);
    expect(list.getByText(/These folders go whole, before their date/)).toBeTruthy();
    expect(freeCalls()).toHaveLength(1);
    await userEvent.click(list.getByRole('button', { name: 'Remove 2' }));

    await vi.waitFor(() => expect(freeCalls()).toHaveLength(2));
    expect(freeCalls()[1]).toEqual({
      units: [],
      early: SPACE_PLAN.early[0]!.units.map((unit) => ({ kind: unit.kind, id: unit.id, why: unit.why, own: unit.own === true })),
    });
  });

  it('says a press refused while another run holds the keeping where it was pressed, the list kept open', async () => {
    invoke.mockImplementation(freeing({
      run: null, joined: false, why: 'held', message: 'held', held: { pid: 7, since: '2026-10-16T10:40:00.000+00:00', door: 'terminal', gone: false },
    }));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    await userEvent.click(card.getByRole('button', { name: 'Free up space…' }));
    const list = within(await card.findByRole('region', { name: 'Free up space…' }));
    await userEvent.click(await list.findByRole('button', { name: 'Remove 3 now' }));

    expect((await list.findByRole('alert')).textContent).toBe(
      `A run from a terminal has held Daoris's keeping since ${clockOf('2026-10-16T10:40:00.000+00:00')}, so nothing was removed. Press again once it ends.`);
    expect(card.getByRole('region', { name: 'Free up space…' })).toBeTruthy();
  });

  it("says a run's kind under way, as its own line and never as a run held elsewhere, the press held meanwhile", async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_RUNNING));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect(card.getByText(`Freeing space: Machine log, 2 of 2, since ${clockOf('2026-10-16T10:43:00.000+00:00')}`)).toBeTruthy();
    expect(card.queryByText(/is under way, since/)).toBeNull();
    expect((card.getByRole('button', { name: 'Free up space…' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('says what an ended run removed and what it kept, by reason', async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_FREED));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect(card.getByText(`Freed ${size(0.4 * 1024 ** 3 + 4 * 1024 ** 2)} at ${clockOf('2026-10-16T10:44:00.000+00:00')}: removed 2 of 3.`)).toBeTruthy();
    expect(card.getByText('1 kept: changed since the list')).toBeTruthy();
    expect((card.getByRole('button', { name: 'Free up space…' }) as HTMLButtonElement).disabled).toBe(false);
  });

  it('holds the press while a run from elsewhere holds the keeping', async () => {
    invoke.mockImplementation(shell(WIRED, SPACE_COUNTING));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    expect((card.getByRole('button', { name: 'Free up space…' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it('lists and says the run in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(freeing(undefined, SPACE_FREED));
    open();
    const card = within(await screen.findByRole('region', { name: '磁盘空间' }));

    expect(card.getByText(`已于 ${clockOf('2026-10-16T10:44:00.000+00:00')} 释放 ${size(0.4 * 1024 ** 3 + 4 * 1024 ** 2)}：3 项中移除了 2 项。`)).toBeTruthy();
    expect(card.getByText('保留 1 项：列出后已有变化')).toBeTruthy();
    await userEvent.click(card.getByRole('button', { name: '释放空间…' }));
    const list = within(await card.findByRole('region', { name: '释放空间…' }));
    expect(await list.findByText('按你的规则现在移除的')).toBeTruthy();
    expect(list.getByText('可由你提前移除的')).toBeTruthy();
    expect(list.getByRole('button', { name: '现在移除 3 项' })).toBeTruthy();
  });

  it("opens the downloads' list under its own row", async () => {
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { measure?: boolean } }) => (
      type === 'SYSTEM_DOWNLOADS' ? DOWNLOADS : shell(WIRED)(module, type, request)));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));

    await userEvent.click(card.getByRole('button', { name: 'Clear unused downloads…' }));

    expect(await card.findByRole('region', { name: 'Clear downloads' })).toBeTruthy();
    expect(within(screen.getByRole('region', { name: 'Folders' })).queryByRole('region', { name: 'Clear downloads' })).toBeNull();
  });

  // KEEP1d (the cleanup design §4.1, §4.2): each rule the person sets, from its row's Change…, inline under the row.
  /** The shell answering SET_KEEP as `keep` answers it; every other route as before. */
  const keeping = (keep: (payload: Record<string, unknown>) => unknown, space: SystemSpace = SPACE) =>
    async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => (
      type === 'SET_KEEP' ? keep(request!.payload!) : shell(WIRED, space)(module, type, request as { payload?: { measure?: boolean } }));
  const row = (card: ReturnType<typeof within>, label: string) =>
    within(card.getByText(label).closest('[data-setting-label]')!.parentElement!);

  it("sets the set-aside folders' days and keeps them whole from the row's editor, which closes once it is saved", async () => {
    invoke.mockImplementation(keeping(() => DRIVER_STATE));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    const aside = row(card, 'Set-aside trees');

    await userEvent.click(aside.getByRole('button', { name: 'Change…' }));
    const days = aside.getByRole('spinbutton', { name: 'Days kept' });
    expect((days as HTMLInputElement).value).toBe('14');
    expect(aside.getByText('1 to 90 days')).toBeTruthy();
    expect(aside.queryByRole('checkbox', { name: 'Until I remove it' })).toBeNull();
    await userEvent.clear(days);
    await userEvent.type(days, '30');
    await userEvent.click(aside.getByRole('checkbox', { name: 'Keep each folder whole' }));
    await userEvent.click(aside.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'set-aside', days: 30, whole: true } }));
    await vi.waitFor(() => expect(aside.queryByRole('button', { name: 'Save' })).toBeNull());
    // The default is the rule it holds: nothing to take back.
    await userEvent.click(aside.getByRole('button', { name: 'Change…' }));
    expect((aside.getByRole('button', { name: 'The default' }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("sets the downloads to stay until the person removes them, and a rule back to its default", async () => {
    invoke.mockImplementation(keeping(() => DRIVER_STATE, SPACE_SET));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    expect(card.getByText('kept 30 days; each folder kept whole')).toBeTruthy();
    expect(card.getByText('kept until you remove it')).toBeTruthy();
    expect(card.getByText('kept 14 days')).toBeTruthy();

    const downloads = row(card, 'Downloaded versions');
    await userEvent.click(downloads.getByRole('button', { name: 'Change…' }));
    expect((downloads.getByRole('checkbox', { name: 'Until I remove it' }) as HTMLElement).getAttribute('data-state')).toBe('checked');
    expect((downloads.getByRole('spinbutton', { name: 'Days kept' }) as HTMLInputElement).disabled).toBe(true);
    await userEvent.click(downloads.getByRole('button', { name: 'Save' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'downloads', never: true } }));

    const logs = row(card, 'Machine log');
    await userEvent.click(logs.getByRole('button', { name: 'Change…' }));
    await userEvent.click(logs.getByRole('button', { name: 'The default' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'logs', default: true } }));
  });

  // KEEP1g (D195 point 7, decided by the owner): finished history by an age the person sets, the press alone by default.
  it('sets an age for finished history, saying what it clears, and takes it back to the press alone', async () => {
    invoke.mockImplementation(keeping(() => DRIVER_STATE));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    const history = row(card, 'Conversations and records');

    await userEvent.click(history.getByRole('button', { name: 'Change…' }));
    expect((history.getByRole('checkbox', { name: 'Until I clear it' }) as HTMLElement).getAttribute('data-state')).toBe('checked');
    expect((history.getByRole('spinbutton', { name: 'Days kept' }) as HTMLInputElement).disabled).toBe(true);
    expect(history.getByText('30 to 3650 days')).toBeTruthy();
    expect(history.queryByText(/Nothing brings it back/)).toBeNull();
    await userEvent.click(history.getByRole('checkbox', { name: 'Until I clear it' }));
    expect(history.getByText(/older than this is cleared from this machine, its words included/)).toBeTruthy();
    await userEvent.type(history.getByRole('spinbutton', { name: 'Days kept' }), '365');
    await userEvent.click(history.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'history', days: 365 } }));
  });

  it('says an age set for finished history and its last clear', async () => {
    invoke.mockImplementation(keeping(() => DRIVER_STATE, SPACE_SET));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    const history = row(card, 'Conversations and records');

    expect(history.getByText('cleared 365 days after its last move')).toBeTruthy();
    expect(history.getByText(`last: ${clockOf('2026-10-16T08:12:00.000+00:00')}, 4 removed, ${size(12 * 1024 ** 2)}`)).toBeTruthy();
    await userEvent.click(history.getByRole('button', { name: 'Change…' }));
    await userEvent.click(history.getByRole('checkbox', { name: 'Until I clear it' }));
    await userEvent.click(history.getByRole('button', { name: 'Save' }));
    await vi.waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'history', never: true } }));
  });

  it('says an age for finished history in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(keeping(() => DRIVER_STATE, SPACE_SET));
    open();
    const card = within(await screen.findByRole('region', { name: '磁盘空间' }));
    expect(card.getByText('最后一次变动 365 天后清除')).toBeTruthy();
    const history = row(card, '会话与记录');
    await userEvent.click(history.getByRole('button', { name: '更改…' }));
    expect(history.getByRole('checkbox', { name: '保留到我清除为止' })).toBeTruthy();
  });

  it("says the driver's refusal of a value out of its bounds under the editor, which stays open, and a value that is no number", async () => {
    invoke.mockImplementation(keeping(() => {
      throw Object.assign(new Error('KEEP_OUT_OF_RANGE'), { code: 'KEEP_OUT_OF_RANGE', parameters: { kind: 'logs', min: '7', max: '30' } });
    }));
    open();
    const card = within(await screen.findByRole('region', { name: 'Disk space' }));
    const logs = row(card, 'Machine log');

    await userEvent.click(logs.getByRole('button', { name: 'Change…' }));
    const days = logs.getByRole('spinbutton', { name: 'Days kept' });
    await userEvent.clear(days);
    await userEvent.click(logs.getByRole('button', { name: 'Save' }));
    expect(logs.getByRole('alert').textContent).toBe('Name a whole number of days.');
    expect(invoke.mock.calls.some(([, type]) => type === 'SET_KEEP')).toBe(false);

    await userEvent.type(days, '60');
    await userEvent.click(logs.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => expect(logs.getByRole('alert').textContent)
      .toBe('This rule keeps 7 to 30 days; that value is outside it, so nothing was written.'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_KEEP', { payload: { kind: 'logs', days: 60 } });
    expect(logs.getByRole('button', { name: 'Save' })).toBeTruthy();
  });

  it('sets a rule and says its refusal in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(keeping(() => {
      throw Object.assign(new Error('KEEP_OUT_OF_RANGE'), { code: 'KEEP_OUT_OF_RANGE', parameters: { kind: 'set-aside', min: '1', max: '90' } });
    }));
    open();
    const card = within(await screen.findByRole('region', { name: '磁盘空间' }));
    const aside = row(card, '另行存放的工作树');

    await userEvent.click(aside.getByRole('button', { name: '更改…' }));
    expect(aside.getByText('1 到 90 天')).toBeTruthy();
    expect(aside.getByRole('checkbox', { name: '完整保留每个文件夹' })).toBeTruthy();
    const days = aside.getByRole('spinbutton', { name: '保留天数' });
    await userEvent.clear(days);
    await userEvent.type(days, '120');
    await userEvent.click(aside.getByRole('button', { name: '保存' }));

    await vi.waitFor(() => expect(aside.getByRole('alert').textContent).toBe('此规则可保留 1 到 90 天；该值超出范围，未写入任何内容。'));
  });

  it('says in its card that a shell older than it answers no disk space', async () => {
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { measure?: boolean } }) => {
      if (module === 'DAORIS.DRIVER' && type === 'SYSTEM_SPACE') throw Object.assign(new Error('NO_ROUTE'), { code: 'NO_ROUTE' });
      return shell(WIRED)(module, type, request);
    });
    open();

    const card = await screen.findByRole('region', { name: 'Disk space' });
    expect(await within(card).findByText('This window answers no disk space: it is older than this page.')).toBeTruthy();
    expect(screen.getByRole('region', { name: 'Folders' })).toBeTruthy();
  });

  it('shows the disk space in 中文', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(WIRED));
    open();

    const card = within(await screen.findByRole('region', { name: '磁盘空间' }));
    expect(card.getByText('另行存放的工作树')).toBeTruthy();
    expect(card.getByText('保留 14 天；其依赖包文件夹在移走一小时后移除')).toBeTruthy();
    expect(card.getByText('保留到你清除为止')).toBeTruthy();
    expect(card.getByText(`共 ${size(1024 ** 4)}，可用 ${size(143.5 * 1024 ** 3)}`)).toBeTruthy();
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
