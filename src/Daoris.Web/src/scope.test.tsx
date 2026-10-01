import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { WorkspaceScopeProvider } from './scope';
import { QuestsView } from './test/questsView';

// The claim WSP5 makes (workspace design §4): every cross-repository answer is scoped to ONE workspace
// per query. Held at the query layer, so a view that never mentions workspaces still asks for one —
// and, just as deliberately, a page with nothing chosen names none, because the door never invents a
// default and the page states the scope it spans instead.

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json([]);
  if (url.startsWith('/api/quests')) return Response.json([]);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/asks')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

let fetchMock: ReturnType<typeof vi.fn>;

function view(node: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>{node}</Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const requested = () => fetchMock.mock.calls.map((call) => String(call[0]));

/** The view has asked its three doors — the moment the URLs can be judged. */
const asked = () => waitFor(() => {
  for (const door of ['/api/quests', '/api/registry', '/api/sessions']) {
    expect(requested().some((url) => url.startsWith(door)), `${door} was asked`).toBe(true);
  }
});

describe('the workspace scope', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('a chosen workspace rides every cross-repository query the view makes', async () => {
    view(
      <WorkspaceScopeProvider initial="aurora">
        <QuestsView notify={() => {}} />
      </WorkspaceScopeProvider>,
    );
    await asked();

    for (const door of ['/api/quests', '/api/registry', '/api/sessions']) {
      expect(requested().find((url) => url.startsWith(door))).toContain('workspace=aurora');
    }
  });

  it('nothing chosen names no workspace — the door spans every circle, and the page says so elsewhere', async () => {
    view(<QuestsView notify={() => {}} />);
    await asked();

    for (const url of requested()) expect(url).not.toContain('workspace=');
  });

  /**
   * WINDOW1: a detached session read the scope once, at open, so the main window choosing another
   * workspace never reached it — its queries kept asking for the circle it opened in. The browser
   * tells every OTHER document of a storage write; the provider hears it and asks again.
   */
  it('follows a workspace another window chose', async () => {
    window.localStorage.removeItem('daoris.workspace');
    view(
      <WorkspaceScopeProvider>
        <QuestsView notify={() => {}} />
      </WorkspaceScopeProvider>,
    );
    await asked();
    fetchMock.mockClear();

    window.localStorage.setItem('daoris.workspace', 'aurora');
    window.dispatchEvent(new StorageEvent('storage', { key: 'daoris.workspace', newValue: 'aurora' }));

    await waitFor(() => expect(requested().find((url) => url.startsWith('/api/quests'))).toContain('workspace=aurora'));
    window.localStorage.removeItem('daoris.workspace');
  });

  it('a workspace name travels encoded, so a circle called 工作区 reaches the door intact', async () => {
    view(
      <WorkspaceScopeProvider initial="工作区">
        <QuestsView notify={() => {}} />
      </WorkspaceScopeProvider>,
    );
    await asked();

    expect(requested().find((url) => url.startsWith('/api/quests'))).toContain(
      `workspace=${encodeURIComponent('工作区')}`);
  });
});
