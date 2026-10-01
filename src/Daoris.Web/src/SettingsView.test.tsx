import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { type SettingsSection, SettingsView } from './SettingsView';

// Settings in a BROWSER: no shell mocked, so no machine answers. `shell.test.tsx` holds the desktop.

function view(section: Parameters<typeof SettingsView>[0]['section'], node = <SettingsView notify={() => {}} section={section} />) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        {node}
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** Settings as the application holds it: the domain chosen in its list is the one it shows. */
function Held({ initial = 'appearance' }: { initial?: SettingsSection }) {
  const [section, setSection] = useState<SettingsSection>(initial);
  return <SettingsView notify={() => {}} section={section} onSection={setSection} />;
}

const widen = (width: number) => act(() => {
  Object.defineProperty(window, 'innerWidth', { configurable: true, value: width });
  window.dispatchEvent(new Event('resize'));
});

const listPane = () => document.querySelector<HTMLElement>('[data-region="list"]');

describe('SettingsView in a browser', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical only', note: '' });
      if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
      // Get started's reading counts what waits on the person, for the starters it shares a reading with.
      if (url.startsWith('/api/sessions')) return Response.json([]);
      throw new Error(`unstubbed request: ${url}`);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
    widen(1024);
  });

  const REGISTRY = [
    { repository: 'engine', adopted: true, registered: true, owns: [], accepts: [], packs: [], workspace: 'aurora' },
    { repository: 'game', adopted: true, registered: true, owns: [], accepts: [], packs: [], workspace: 'aurora' },
    { repository: 'tools', adopted: false, registered: true, owns: [], accepts: [], packs: [], workspace: 'forge' },
  ];

  /**
   * D75 §3: the workspace is always named, and its domain says what each one holds. That is what a
   * browser may know too, so the domain is offered there, holding the list and none of the wiring.
   */
  it('lists every workspace with the repositories it holds, in a browser too', async () => {
    view('workspace');

    const aurora = await screen.findByRole('listitem', { name: 'aurora' });
    expect(within(aurora).getByText('2 repositories')).toBeTruthy();
    expect(within(aurora).getByText('engine · game')).toBeTruthy();
    expect(within(screen.getByRole('listitem', { name: 'forge' })).getByText('1 repository')).toBeTruthy();
    // None of the machine's wiring reaches a browser.
    expect(screen.queryByRole('button', { name: 'Wire a workspace' })).toBeNull();
  });

  /**
   * A browser is never offered a machine's domain (D47 §4, D75): not a disabled one, none. A domain it
   * was asked for that it cannot show, one a shell remembered, opens on Appearance.
   */
  it('offers only what a browser may know, and opens a machine domain on Appearance', async () => {
    view('agents');

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getAllByRole('button').map((button) => button.textContent))
      .toEqual(['Setup', 'Appearance', "AI features", 'Workspace']);
    expect(within(domains).getByRole('button', { name: 'Appearance' })).toHaveAttribute('aria-current', 'page');
    expect(await screen.findByText('Theme')).toBeTruthy();
  });

  /**
   * SETUP1a (D97): Get started leads the list, a browser's too, holding what a browser can know — the
   * registry — and saying the rest is the desktop's (D47 §4). Nothing else of a machine is asked for.
   */
  it('opens Get started first in the list, with only the step a browser can know', async () => {
    view('start');

    const steps = await screen.findByRole('list', { name: 'setup steps' });
    expect(within(steps).getAllByRole('listitem').map((item) => item.getAttribute('aria-label')))
      .toEqual(['3. A workspace and its repositories']);
    expect(within(steps).getByText('done')).toBeTruthy();
    expect(screen.getByText(/the desktop's Setup shows them/)).toBeTruthy();
    expect(screen.queryByRole('checkbox', { name: "Don't open at start" })).toBeNull();
  });
});

/**
 * FRAME1g (D118 §2, §3a; audit ST1–ST3, ST10): Settings' domains are its list pane, 176–320 px and 176 to
 * start, where they were a fixed 11 rem column drawn inside the page; the domain is the main area, named by
 * its header. A browser keeps the list and the main area (D118 §4).
 */
describe("Settings' list pane and main area", () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/status')) return Response.json({ semantic: false, tier: 'lexical only', note: '' });
      if (url.startsWith('/api/registry')) return Response.json([]);
      if (url.startsWith('/api/sessions')) return Response.json([]);
      throw new Error(`unstubbed request: ${url}`);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.clear();
    widen(1024);
  });

  it('draws its domains as its list pane beside the main area, which the domain shown fills', async () => {
    view('appearance');

    const list = listPane()!;
    expect(list).toHaveAttribute('data-list-mode', 'open');
    expect(within(list).getByRole('navigation', { name: 'Settings domains' })).toBeInTheDocument();
    // Its header names the list, and Settings makes nothing, so there is no ＋.
    expect(within(list).getByText('Settings')).toBeInTheDocument();
    expect(within(list).getAllByRole('button').map((button) => button.getAttribute('aria-label') ?? button.textContent))
      .toEqual(['Hide the settings list', 'Setup', 'Appearance', 'AI features', 'Workspace']);

    // The domain is the main area, and its header names it: a card alone in it need not.
    const main = screen.getByRole('main');
    expect(within(main).getByRole('heading', { level: 1, name: 'Appearance' })).toBeInTheDocument();
    expect(await within(main).findByRole('radiogroup', { name: 'Theme' })).toBeInTheDocument();
    expect(within(main).queryByRole('navigation', { name: 'Settings domains' })).toBeNull();
  });

  it('is resized within its own bounds, 176 to 320 px, from 176', () => {
    view('appearance');

    const edge = screen.getByRole('separator', { name: 'settings list width' });
    expect(edge).toHaveAttribute('aria-valuenow', '176');
    expect(edge).toHaveAttribute('aria-valuemin', '176');
    expect(edge).toHaveAttribute('aria-valuemax', '320');
  });

  /** D47 §4: absent, never disabled, and said where the absence is — beneath the domains a browser is offered. */
  it('says beneath its domains that a machine\'s own settings are on the desktop', () => {
    view('appearance');

    expect(within(listPane()!).getByText("A machine's own settings are on the desktop, where the machine is.")).toBeInTheDocument();
  });

  /**
   * ST10: below a 768 px viewport the list stacked above the domain at the column's full width, ten rows over
   * the domain on a shell. It never stacks now: at 680 px it stays beside the domain where there is room.
   */
  it('stays beside the domain at a 680 px window, never above it', () => {
    widen(680);
    view('appearance');

    expect(listPane()).toHaveAttribute('data-list-mode', 'open');
    expect(screen.getByRole('main').contains(screen.getByRole('navigation', { name: 'Settings domains' }))).toBe(false);
  });

  /**
   * D118 §3a: where the domain would fall below its 400 px floor beside it, the list is a strip by itself,
   * and its open lays the list over the main area — Settings' list is its only way in, so a strip that
   * offered no open would leave the view out of reach. A choice closes it.
   */
  it('is a strip where there is no room, opens laid over the domain, and closes on a choice', async () => {
    widen(600);
    view('appearance', <Held />);

    expect(listPane()).toHaveAttribute('data-list-mode', 'strip');
    expect(screen.queryByRole('navigation', { name: 'Settings domains' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Appearance' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Show the settings list' }));
    const over = screen.getByRole('region', { name: 'Settings' });
    await userEvent.click(within(over).getByRole('button', { name: 'Workspace' }));

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Settings' })).toBeNull());
    expect(screen.getByRole('heading', { level: 1, name: 'Workspace' })).toBeInTheDocument();
    expect(listPane()).toHaveAttribute('data-list-mode', 'strip');
  });

  it('closes to its strip as Settings\' own, and opens from it again', async () => {
    view('appearance');

    await userEvent.click(screen.getByRole('button', { name: 'Hide the settings list' }));
    expect(listPane()).toHaveAttribute('data-list-mode', 'strip');
    expect(window.localStorage.getItem('daoris.list.settings.closed')).toBe('1');

    await userEvent.click(screen.getByRole('button', { name: 'Show the settings list' }));
    expect(listPane()).toHaveAttribute('data-list-mode', 'open');
    expect(window.localStorage.getItem('daoris.list.settings.closed')).toBeNull();
  });

  /**
   * A domain chosen opens at its top: the main area is drawn anew for it, so the next domain does not open
   * at the scroll the last one was left at.
   */
  it('opens a domain chosen at its top, in a main area drawn anew', async () => {
    view('appearance', <Held />);

    const before = screen.getByRole('main');
    await userEvent.click(screen.getByRole('button', { name: 'AI features' }));

    expect(screen.getByRole('heading', { level: 1, name: 'AI features' })).toBeInTheDocument();
    expect(screen.getByRole('main')).not.toBe(before);
  });
});
