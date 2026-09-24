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
      .toEqual(['Appearance', "Daoris's own AI", 'Workspace']);
    expect(within(domains).getByRole('button', { name: 'Appearance' })).toHaveAttribute('aria-current', 'page');
    expect(await screen.findByText('Theme')).toBeTruthy();
  });
});
