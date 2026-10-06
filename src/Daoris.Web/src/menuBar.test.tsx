import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// UX7a (D152): the menu bar a person reaches for, on the whole window over a mocked shell — the seven menus, the keys
// the table gives them, the record in front acted on through its own owner, and the bar reached from the keyboard.

const { invoke, notifyReady } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
}));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import './i18n';
import { App } from './App';
import { AT_START } from './setupGuide';
import { WorkspaceScopeProvider } from './scope';

/** A conversation working now: its header offers its stop and its own window. */
const WORKING = {
  id: 's1a2b3c4', quest: null, repository: 'engine', adapter: 'claude-code', kind: 'chat', state: 'working',
  created: '2026-10-05T00:00:00Z', updated: '2026-10-05T00:01:00Z', workspace: 'default',
};

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) {
    return Response.json([{ repository: 'engine', workspace: 'default', registered: true, adopted: true, owns: [], accepts: [], packs: [] }]);
  }
  if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
  if (url.startsWith('/api/sessions')) return Response.json(url.includes('daoris%3Ahelp') ? [] : [WORKING]);
  if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
  return Response.json([]);
}

/** The window on a view, as a relaunch lands on it, over a machine whose driver answers. */
function start(view: 'sessions' | 'overview' = 'overview') {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: ['engine'], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    if (module === 'DAORIS.TERMINAL' && type === 'OPEN') return { id: 't1', shell: 'pwsh', cwd: '' };
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
  if (view === 'sessions') {
    window.localStorage.setItem('daoris.view', 'sessions');
    window.localStorage.setItem('daoris.attending', WORKING.id);
  }
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const menuBar = async () => {
  const bar = await screen.findByRole('navigation', { name: 'Menu bar' });
  // The driver answers after the first render: the bar is the desktop's once its Terminal is there.
  await within(bar).findByRole('button', { name: 'Terminal' });
  return bar;
};
const places = () => screen.getByRole('navigation', { name: 'Views' });

/** A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway (see `AppMenu.test`). */
async function openMenu(name: string) {
  const user = userEvent.setup();
  within(await menuBar()).getByRole('button', { name }).focus();
  await user.keyboard('{Enter}');
  return user;
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  invoke.mockReset();
  window.localStorage.clear();
});

describe('the menu bar on the desktop (D152 §1)', () => {
  it('holds seven menus, VS Code\'s less Selection with Workspace first', async () => {
    start();
    expect(within(await menuBar()).getAllByRole('button').map((button) => button.textContent)).toEqual(
      ['Workspace', 'Edit', 'View', 'Go', 'Run', 'Terminal', 'Help']);
  });

  it('prints every place\'s key under Go, and the key goes there', async () => {
    start();
    await openMenu('Go');
    expect(await screen.findByRole('menuitem', { name: /^Sessions/ })).toHaveTextContent('Ctrl+2');
    expect(screen.getByRole('menuitem', { name: /^Plugins/ })).toHaveTextContent('Ctrl+8');
    fireEvent.keyDown(document.activeElement!, { key: 'Escape' });

    // The bar's place says its key too, in its tip (the design §7: a position is shown in the menu and the bar's tip).
    expect(within(places()).getByRole('button', { name: /^Quests/ })).toHaveAttribute('aria-keyshortcuts', 'Ctrl+3');
    expect(within(places()).getByRole('button', { name: 'Settings' })).toHaveAttribute('aria-keyshortcuts', 'Ctrl+,');
    expect(fireEvent.keyDown(window, { key: '3', code: 'Digit3', ctrlKey: true })).toBe(false);
    await waitFor(() => expect(within(places()).getByRole('button', { name: /^Quests/ })).toHaveAttribute('aria-current', 'page'));
  });
});

describe('the bar from the keyboard (the design §3.3)', () => {
  it('shows each menu\'s letter while Alt is held, and Alt with a letter opens its menu', async () => {
    start();
    const bar = await menuBar();
    // 🔴 The focus on the page first: jsdom fires a window blur when the focus leaves the document itself for the menu,
    // which a menu reads as the window losing the focus, and closes on (a browser fires none).
    within(places()).getByRole('button', { name: /^Overview/ }).focus();
    fireEvent.keyDown(window, { key: 'Alt', code: 'AltLeft', altKey: true });
    expect(bar.querySelectorAll('u')).toHaveLength(7);
    fireEvent.keyDown(window, { key: 'r', code: 'KeyR', altKey: true });
    await waitFor(() => expect(document.querySelectorAll('[role="menu"]').length).toBe(1));
    expect(within(document.querySelector<HTMLElement>('[role="menu"]')!).getByText(/^Start a session/)).toBeInTheDocument();
    expect(document.querySelector('[role="menu"]')).toHaveTextContent('Ctrl+Shift+N');
    fireEvent.keyUp(window, { key: 'Alt', code: 'AltLeft' });
    expect(bar.querySelectorAll('u')).toHaveLength(0);
  });

  it('puts the focus on the bar for Alt pressed and let go alone, and for F10', async () => {
    start();
    const bar = await menuBar();
    fireEvent.keyDown(window, { key: 'Alt', code: 'AltLeft', altKey: true });
    fireEvent.keyUp(window, { key: 'Alt', code: 'AltLeft' });
    expect(within(bar).getByRole('button', { name: 'Workspace' })).toHaveFocus();

    act(() => { (document.activeElement as HTMLElement).blur(); });
    fireEvent.keyDown(window, { key: 'F10', code: 'F10' });
    expect(within(bar).getByRole('button', { name: 'Workspace' })).toHaveFocus();
  });

  it('leaves Alt with another key alone: Ctrl+Alt+B is the side bar\'s', async () => {
    start();
    const bar = await menuBar();
    fireEvent.keyDown(window, { key: 'Alt', code: 'AltLeft', altKey: true });
    fireEvent.keyDown(window, { key: 'b', code: 'KeyB', altKey: true, ctrlKey: true });
    fireEvent.keyUp(window, { key: 'Alt', code: 'AltLeft' });
    expect(within(bar).getByRole('button', { name: 'Workspace' })).not.toHaveFocus();
  });
});

/**
 * UX7a2 (D152's UX7a note, the design §3.3): the seven names need about 590 CSS px of strip beside the command center's glyph
 * and the window's buttons, so below that they fold into one menu, and the keys reach it as they reach the bar.
 */
describe('a narrow window\'s menu bar', () => {
  const atWidth = (width: number) => act(() => {
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
    window.dispatchEvent(new Event('resize'));
  });
  afterEach(() => atWidth(1024));

  it('folds the seven menus into one at 560 px, and unfolds them at 680', async () => {
    atWidth(560);
    start();
    const bar = await screen.findByRole('navigation', { name: 'Menu bar' });
    await waitFor(() => expect(within(bar).getAllByRole('button').map((button) => button.getAttribute('aria-label')))
      .toEqual(['Menu']));

    atWidth(680);
    expect(within(await menuBar()).getAllByRole('button').map((button) => button.textContent)).toEqual(
      ['Workspace', 'Edit', 'View', 'Go', 'Run', 'Terminal', 'Help']);
  });

  it('opens the menu Alt names inside the fold, and puts the focus on the fold for Alt alone and F10', async () => {
    atWidth(560);
    start();
    // The driver answers after the first render: the strip is the desktop's once its browser door is there.
    await screen.findByRole('button', { name: "Open Daoris's browser" });
    const fold = within(screen.getByRole('navigation', { name: 'Menu bar' })).getByRole('button', { name: 'Menu' });

    fireEvent.keyDown(window, { key: 'Alt', code: 'AltLeft', altKey: true });
    fireEvent.keyUp(window, { key: 'Alt', code: 'AltLeft' });
    expect(fold).toHaveFocus();
    act(() => { fold.blur(); });
    fireEvent.keyDown(window, { key: 'F10', code: 'F10' });
    expect(fold).toHaveFocus();
    act(() => { fold.blur(); });

    within(places()).getByRole('button', { name: /^Overview/ }).focus();
    fireEvent.keyDown(window, { key: 'r', code: 'KeyR', altKey: true });
    const run = await screen.findByRole('menuitem', { name: 'Run' });
    expect(run).toHaveAttribute('aria-expanded', 'true');
    expect(await screen.findByRole('menuitem', { name: /^Start a session/ })).toHaveTextContent('Ctrl+Shift+N');
    expect(screen.getByRole('menuitem', { name: 'Terminal' })).toBeInTheDocument();
  });
});

describe('the keys (D152 §3.4)', () => {
  it('opens Settings on Ctrl+, and the palette on Ctrl+Shift+P, grouped by menu', async () => {
    start();
    await menuBar();
    expect(fireEvent.keyDown(window, { key: 'P', code: 'KeyP', ctrlKey: true, shiftKey: true })).toBe(false);
    const palette = await screen.findByRole('dialog', { name: 'Commands' });
    expect(within(palette).getByRole('option', { name: /Go to: Quests/ })).toHaveTextContent('Go');
    expect(within(palette).getByRole('option', { name: /^New ask/ })).toHaveTextContent('Workspace');
    fireEvent.keyDown(within(palette).getByRole('combobox'), { key: 'Escape' });

    fireEvent.keyDown(window, { key: ',', code: 'Comma', ctrlKey: true });
    expect(await screen.findByRole('navigation', { name: 'Settings domains' })).toBeInTheDocument();
  });

  it('leaves Ctrl+K and Ctrl+F to a field, and takes Ctrl+N in one', async () => {
    start();
    await menuBar();
    const field = document.createElement('input');
    document.body.append(field);
    field.focus();
    expect(fireEvent.keyDown(field, { key: 'k', code: 'KeyK', ctrlKey: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'f', code: 'KeyF', ctrlKey: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'n', code: 'KeyN', ctrlKey: true })).toBe(false);
    expect(await screen.findByRole('dialog', { name: 'Ask the workspace' })).toBeInTheDocument();
    field.remove();
  });

  it('opens a new terminal on Ctrl+Shift+`, and gives Stop… no key', async () => {
    start();
    await menuBar();
    fireEvent.keyDown(window, { key: '~', code: 'Backquote', ctrlKey: true, shiftKey: true });
    await waitFor(() => expect(invoke.mock.calls.some(([module, type]) => module === 'DAORIS.TERMINAL' && type === 'OPEN')).toBe(true));

    await openMenu('Run');
    expect(await screen.findByRole('menuitem', { name: 'Stop…' })).not.toHaveTextContent(/Ctrl|Alt/);
  });
});

describe('the record in front (D152 §2)', () => {
  it('enables This session\'s acts as its header offers them, and Stop… asks under the header as its button does', async () => {
    start('sessions');
    await screen.findByRole('button', { name: /^Stop…/ });
    const user = await openMenu('Run');

    expect(await screen.findByText('This session')).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: 'Stop…' })).not.toHaveAttribute('aria-disabled');
    expect(screen.getByRole('menuitem', { name: 'Open in its own window' })).not.toHaveAttribute('aria-disabled');
    // This session's *Try again*, then This quest's.
    expect(screen.getAllByRole('menuitem', { name: 'Try again' })[0]).toHaveAttribute('aria-disabled', 'true');
    // No quest is open on Quests, so This quest's acts keep their place, off.
    expect(screen.getByRole('menuitem', { name: 'Take' })).toHaveAttribute('aria-disabled', 'true');

    await user.click(screen.getByRole('menuitem', { name: 'Stop…' }));
    expect(await screen.findByRole('button', { name: 'Stop session' })).toBeInTheDocument();
  });

  it('copies the record\'s id from Edit › Copy ID, as its page\'s own Copy does', async () => {
    start('sessions');
    await screen.findByRole('button', { name: /^Stop…/ });
    const user = await openMenu('Edit');
    await waitFor(() => expect(document.querySelectorAll('[role="menu"]').length).toBe(1));
    const copy = within(document.querySelector<HTMLElement>('[role="menu"]')!).getByText('Copy ID').closest('[role="menuitem"]')!;
    expect(copy).not.toHaveAttribute('aria-disabled');
    await user.click(copy);
    // user-event's own clipboard stands in for the window's from `setup()` on.
    await waitFor(async () => expect(await navigator.clipboard.readText()).toBe(WORKING.id));
    expect(await screen.findByText(`Copied ${WORKING.id}.`)).toBeInTheDocument();
  });
});

describe('Help › Update', () => {
  it('brings the update card into view after the shell answers, and clears the anchor for a later Settings visit', async () => {
    const scroll = vi.spyOn(HTMLElement.prototype, 'scrollIntoView');
    try {
      start();
      const user = await openMenu('Help');
      await user.click(await screen.findByRole('menuitem', { name: 'Update' }));
      await waitFor(() => expect(scroll.mock.instances).toContain(document.getElementById('settings-update')));
      expect(document.getElementById('settings-update')).not.toBeNull();
      const calls = scroll.mock.calls.length;
      await user.click(within(places()).getByRole('button', { name: /^Overview/ }));
      fireEvent.keyDown(window, { key: ',', code: 'Comma', ctrlKey: true });
      await screen.findByRole('navigation', { name: 'Settings domains' });
      expect(scroll).toHaveBeenCalledTimes(calls);
    } finally {
      scroll.mockRestore();
    }
  });
});

describe('Help › Keyboard shortcuts', () => {
  it('opens a drawer listing every key by menu', async () => {
    start();
    const user = await openMenu('Help');
    await user.click(await screen.findByRole('menuitem', { name: 'Keyboard shortcuts' }));
    const drawer = await screen.findByRole('dialog', { name: 'Keyboard shortcuts' });
    expect(within(drawer).getByRole('region', { name: 'Terminal' })).toHaveTextContent('Ctrl+`');
    expect(within(drawer).getByRole('region', { name: 'Go' })).toHaveTextContent('Ctrl+1');
  });
});
