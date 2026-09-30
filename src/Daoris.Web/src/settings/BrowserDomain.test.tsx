import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Browser domain in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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
 * Daoris's browser on Settings (CHR5, CHR7): the favorites it shows on its bookmarks bar and the
 * extensions setting, over the files `daoris browser` edits (D50). A machine domain: the bridge, never
 * the service.
 */
describe('the browser domain', () => {
  const BROWSER = {
    favoritesPath: 'C:/somewhere/data/browser/favorites.json',
    favorites: [{ url: 'https://site.example/board', title: 'Board' }],
    favoritesProblem: null,
    settingsPath: 'C:/somewhere/data/browser/settings.json',
    extensions: 'offer',
    browser: 'daoris',
    edgeFound: true,
    edgeProfile: 'C:/somewhere/data/browser/edge',
    settingsProblem: null,
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? BROWSER : DRIVER_STATE));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the favorites and the extensions setting from the machine, and nothing over the service', async () => {
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText('Board')).toBeTruthy();
    expect(screen.getByText('https://site.example/board')).toBeTruthy();
    expect(screen.getByText('C:/somewhere/data/browser/favorites.json')).toBeTruthy();
    expect(screen.getByRole('radio', { name: 'offer' }).getAttribute('aria-checked')).toBe('true');
    expect(screen.getByText(/next time Daoris's browser starts/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'STATE', {});
    expect(serviceCalls()).toEqual([]);
  });

  it('keeping and removing a page land as the verbs a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);
    await screen.findByText('Board');

    await userEvent.type(screen.getByPlaceholderText('https://…'), 'site.example/new');
    await userEvent.type(screen.getByPlaceholderText("the page's host"), 'New');
    await userEvent.click(screen.getByRole('button', { name: 'keep it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'ADD_FAVORITE', { payload: { address: 'site.example/new', title: 'New' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('New is a favorite.'));

    await userEvent.click(screen.getByRole('button', { name: 'remove' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'REMOVE_FAVORITE', { payload: { address: 'https://site.example/board' } });
  });

  it('refusing other software\'s extensions says it holds from the next start', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    await userEvent.click(await screen.findByRole('radio', { name: 'refuse' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_EXTENSIONS', { payload: { extensions: 'refuse' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      "Other software's extensions will be refused from the browser's next start."));
  });

  /** BRW12: the person's Edge, as an option, saying what it brings before it is chosen. */
  it('choosing Edge lands as the verb a terminal has, and says what Edge brings', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    expect(await screen.findByRole('radio', { name: "Daoris's own" })).toHaveAttribute('aria-checked', 'true');
    await userEvent.click(screen.getByRole('radio', { name: 'your Edge' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_BROWSER', { payload: { browser: 'edge' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('your Edge, from the next time the browser is opened.'));
  });

  /** BRW7: where the page's links open — set here or by `daoris browser links`, and holding at once. */
  it("sending links to Daoris's browser lands as the verb a terminal has, and says it holds at once", async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="browser" />);

    // A shell that sends no `links` is the system's, as a link always was.
    expect(await screen.findByRole('radio', { name: "the system's browser" })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByText(/daoris browser links system\|daoris/)).toBeTruthy();
    await userEvent.click(screen.getByRole('radio', { name: "Daoris's browser" }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.BROWSER', 'SET_LINKS', { payload: { links: 'daoris' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith("Links on the page open in Daoris's browser, from the next click."));
  });

  /** BRW8: whose hands are on it, in its own domain too — each session a door into Sessions. */
  it('names who is driving it, each opening its session, and says so when nobody is', async () => {
    const onAttend = vi.fn();
    const { unmount } = show(
      <SettingsView
        notify={() => {}}
        section="browser"
        browserDrivers={[{ id: 's1a2b3c4', name: 'engine · Read the ticket' }, { id: 'c0ffee00', name: 'game · conversation' }]}
        onAttend={onAttend}
      />,
    );

    expect(await screen.findByText('Driving it now')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'open game · conversation' }));
    expect(onAttend).toHaveBeenCalledWith('c0ffee00');
    unmount();

    show(<SettingsView notify={() => {}} section="browser" browserDrivers={[]} />);
    expect(await screen.findByText('No session is driving it.')).toBeTruthy();
  });

  it('with Edge chosen, says its profile, its account and its default profile, and whose favorites these are', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? { ...BROWSER, browser: 'edge' } : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/C:\/somewhere\/data\/browser\/edge.*Microsoft account.*default Edge profile cannot be driven/)).toBeTruthy();
    expect(screen.getByText(/Edge keeps its own/)).toBeTruthy();
  });

  it('a machine with no Edge says so before it is chosen', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER' ? { ...BROWSER, edgeFound: false } : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/No Edge is installed on this machine/)).toBeTruthy();
  });

  it('a file that could not be read is said, with where it is, and no list is guessed', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.BROWSER'
      ? { ...BROWSER, favorites: [], favoritesProblem: 'C:/somewhere/data/browser/favorites.json is not a JSON object' }
      : DRIVER_STATE));
    show(<SettingsView notify={() => {}} section="browser" />);

    expect(await screen.findByText(/could not be read: .*is not a JSON object/)).toBeTruthy();
    expect(screen.queryByText('No favorites yet.')).toBeNull();
  });
});
