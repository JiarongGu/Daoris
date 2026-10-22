import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// ZOOM1 (the UI/UX design §3.11), the way the window holds it: the whole window over a mocked shell. Ctrl+wheel was the engine's
// own zoom, which scaled the title strip, its menus and the window controls with the content and said no level (the owner's
// report, 2026-10-22). Now the keys, the wheel and View's rows step the content as an editor's do, the level is said as it
// changes and kept on the status bar while it is not 100%, pressed back to 100%, and kept for the machine.

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

import '../locales/i18n';
import { App } from './App';
import { AT_START } from '../help/setupGuide';
import { WorkspaceScopeProvider } from '../data/scope';
import { FRAME_HELD } from '../design/ui';
import { setZoom, ZOOM_KEY } from '../design/zoom';

function start() {
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical', note: '' });
    if (url.startsWith('/api/sync')) return Response.json({ workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] });
    return Response.json([]);
  }));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return { path: 'remotes.json', fromEnvironment: false, remotes: [] };
    if (module === 'DAORIS.DRIVER' && type === 'STATE') {
      return { drivable: [], holds: [], trees: [], running: [], notify: false, strikes: 3, forgiven: {} };
    }
    return {};
  });
  window.localStorage.setItem(AT_START, 'off');
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

const root = () => document.documentElement;
const press = (key: string, code: string, shiftKey = false) => fireEvent.keyDown(window, { key, code, ctrlKey: true, shiftKey });
const status = () => screen.getByRole('contentinfo');

describe("the window's zoom, on the window", () => {
  beforeEach(() => {
    window.localStorage.removeItem(ZOOM_KEY);
    setZoom(100);
  });
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
    setZoom(100);
  });

  it('zooms in by Ctrl+= and Ctrl++, says the level, keeps it on the status bar and for the machine', async () => {
    start();
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    expect(within(status()).queryByRole('button', { name: 'zoom' })).toBeNull();

    press('=', 'Equal');
    await waitFor(() => expect(root().style.fontSize).toBe('110%'));
    expect(screen.getAllByRole('status').some((each) => each.textContent === 'zoom 110%')).toBe(true);
    press('+', 'Equal', true);
    await waitFor(() => expect(root().style.fontSize).toBe('125%'));
    expect(within(status()).getByRole('button', { name: 'zoom' })).toHaveTextContent('125%');
    expect(window.localStorage.getItem(ZOOM_KEY)).toBe('125');
  });

  it('zooms out by Ctrl+- and back to 100% by Ctrl+0, which forgets the level', async () => {
    start();
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    press('-', 'Minus');
    await waitFor(() => expect(root().style.fontSize).toBe('90%'));
    press('0', 'Digit0');
    await waitFor(() => expect(root().style.fontSize).toBe(''));
    expect(window.localStorage.getItem(ZOOM_KEY)).toBeNull();
    expect(within(status()).queryByRole('button', { name: 'zoom' })).toBeNull();
    // The reset is said too.
    expect(screen.getAllByRole('status').some((each) => each.textContent === 'zoom 100%')).toBe(true);
  });

  it('zooms in by Ctrl+wheel over the content, and goes back to 100% from the status bar', async () => {
    start();
    const heading = await screen.findByRole('heading', { level: 1, name: 'Overview' });
    fireEvent.wheel(heading, { deltaY: -100, ctrlKey: true });
    await waitFor(() => expect(root().style.fontSize).toBe('110%'));

    await userEvent.click(within(status()).getByRole('button', { name: 'zoom' }));
    await waitFor(() => expect(root().style.fontSize).toBe(''));
  });

  it("zooms from View's rows", async () => {
    start();
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    // A menu opens from the keyboard in jsdom (see `AppMenu.test`).
    const user = userEvent.setup();
    screen.getByRole('button', { name: 'View' }).focus();
    await user.keyboard('{Enter}');
    const zoomIn = await screen.findByRole('menuitem', { name: /^Zoom in/ });
    // The menu bar's menu keeps the frame's size (ZOOM1).
    expect(zoomIn.closest(`.${FRAME_HELD}`)).not.toBeNull();
    await user.click(zoomIn);
    await waitFor(() => expect(root().style.fontSize).toBe('110%'));
  });

  /**
   * The frame keeps its size at every level: it wears the level's inverse, so its size is the root's times that, one at each.
   * jsdom lays nothing out; `measured/zoom.spec.ts` measures the bars in Chromium.
   */
  it('keeps the title strip, the activity bar and the status bar at their size at every level', async () => {
    const { container } = start();
    await screen.findByRole('heading', { level: 1, name: 'Overview' });
    const frame = [container.querySelector('header'), screen.getByRole('navigation', { name: 'Views' }), status()];
    for (const part of frame) expect(part).toHaveClass(FRAME_HELD);

    for (const [key, code] of [['=', 'Equal'], ['=', 'Equal'], ['=', 'Equal'], ['0', 'Digit0'], ['-', 'Minus'], ['-', 'Minus']]) {
      press(key!, code!);
      const scale = (Number.parseFloat(root().style.fontSize || '100') / 100) * Number(root().style.getPropertyValue('--frame-zoom') || '1');
      expect(scale).toBeCloseTo(1, 10);
    }
  });
});
