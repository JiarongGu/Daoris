import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { SettingsView } from './SettingsView';

// Settings in a BROWSER: no shell mocked, so no machine answers. `shell.test.tsx` holds the desktop.

function view(section: Parameters<typeof SettingsView>[0]['section']) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <SettingsView notify={() => {}} section={section} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

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
  afterEach(() => vi.unstubAllGlobals());

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
      .toEqual(['Get started', 'Appearance', "Daoris's own AI", 'Workspace']);
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
    expect(screen.getByText(/the desktop's Get started shows them/)).toBeTruthy();
    expect(screen.queryByRole('checkbox', { name: "Don't open at start" })).toBeNull();
  });
});
