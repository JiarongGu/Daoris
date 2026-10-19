import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { useScope, WorkspaceScopeProvider, type WindowStore } from './scope';
import { QuestsView } from '../test/questsView';

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

/** A window's own storage, its `sessionStorage`: a second document's, which no other window reads. */
function ownStorage(): WindowStore {
  const kept = new Map<string, string>();
  return {
    read: (key) => kept.get(key) ?? null,
    write: (key, value) => {
      if (value === null) kept.delete(key);
      else kept.set(key, value);
    },
  };
}

/**
 * One window of the instance, by its address (`?window=main%3A2`, `?window=session%3A…`, or none for the first main window)
 * and its own storage, which a reload keeps: what its scope is, a choice in it, and a window it opens.
 */
function windowOf(search: string, own = ownStorage(), inside?: React.ReactElement) {
  const held: { scope?: ReturnType<typeof useScope> } = {};
  function Probe() {
    held.scope = useScope();
    return inside ?? null;
  }
  const page = view(<WorkspaceScopeProvider search={search} own={own}><Probe /></WorkspaceScopeProvider>);
  return {
    own,
    get workspace() {
      return held.scope!.workspace;
    },
    choose: (workspace: string | null) => act(() => held.scope!.setWorkspace(workspace)),
    opens: (name: string, workspace?: string) => act(() => held.scope!.opens(name, workspace)),
    close: () => page.unmount(),
  };
}

// The browser tells every OTHER document of a write to the storage they share. One test document plays every window, so
// each key written there is told after the act, as the browser would tell the others; the writer hearing its own changes
// nothing a real one would not.
let written: string[] = [];

function hearWrites() {
  written = [];
  const set = Storage.prototype.setItem;
  const remove = Storage.prototype.removeItem;
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(function (this: Storage, key: string, value: string) {
    if (this === window.localStorage) written.push(key);
    set.call(this, key, value);
  });
  vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(function (this: Storage, key: string) {
    if (this === window.localStorage) written.push(key);
    remove.call(this, key);
  });
}

/** Every write told to every window, until the windows that follow another have nothing more to write. */
async function told() {
  for (let round = 0; round < 5 && written.length > 0; round += 1) {
    const keys = [...new Set(written)];
    written = [];
    await act(async () => {
      for (const key of keys) window.dispatchEvent(new StorageEvent('storage', { key }));
    });
  }
}

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
   * tells every OTHER document of a storage write; the provider hears it and asks again. Since MAINWINDOWS3 it hears
   * the window that opened it, and no other.
   */
  it('a detached session follows a workspace the window that opened it chose, into every query it makes', async () => {
    window.localStorage.clear();
    hearWrites();
    const main = windowOf('');
    main.opens('session:abc12345');
    windowOf('?window=session%3Aabc12345', ownStorage(), <QuestsView notify={() => {}} />);
    await asked();
    fetchMock.mockClear();

    main.choose('aurora');
    await told();

    await waitFor(() => expect(requested().find((url) => url.startsWith('/api/quests'))).toContain('workspace=aurora'));
    vi.restoreAllMocks();
    window.localStorage.clear();
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

/**
 * MAINWINDOWS3 (D171's note): the owner wants two main windows of one instance, one on their work's workspace and one on
 * `games`. The scope was one shared key every document followed, so a choice in one main window moved them all. Each main
 * window now holds its own, a new one starts from the last chosen anywhere, and a detached window follows the window that
 * opened it, or its session's own workspace.
 */
describe('each main window holds its own workspace scope', () => {
  beforeEach(() => {
    window.localStorage.clear();
    hearWrites();
  });
  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it('two main windows choosing different workspaces never follow each other', async () => {
    const first = windowOf('');
    const second = windowOf('?window=main%3A2&host=http%3A%2F%2Flocalhost%3A5177');

    first.choose('work');
    await told();
    second.choose('games');
    await told();

    expect(first.workspace).toBe('work');
    expect(second.workspace).toBe('games');
  });

  it('a reload keeps the window\'s own scope, every workspace included, whatever another chose since', async () => {
    const first = windowOf('');
    first.choose('work');
    await told();
    first.close();
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();

    expect(windowOf('', first.own).workspace).toBe('work');

    second.choose(null);
    await told();
    const third = windowOf('?window=main%3A3');
    third.choose('games');
    await told();
    second.close();
    expect(windowOf('?window=main%3A2', second.own).workspace).toBeNull();
  });

  it('a new main window starts from the last scope chosen anywhere, then keeps its own', async () => {
    const first = windowOf('');
    first.choose('work');
    await told();
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();

    const third = windowOf('?window=main%3A3');
    expect(third.workspace).toBe('games');

    third.choose('studio');
    await told();
    expect([first.workspace, second.workspace, third.workspace]).toEqual(['work', 'games', 'studio']);
  });

  it('a detached session window follows the main window it was opened from, never another', async () => {
    const first = windowOf('');
    first.choose('work');
    await told();
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();

    second.opens('session:abc12345');
    const detached = windowOf('?window=session%3Aabc12345');
    expect(detached.workspace).toBe('games');

    first.choose('studio');
    await told();
    expect(detached.workspace).toBe('games');

    second.choose('work');
    await told();
    expect(detached.workspace).toBe('work');
  });

  it('a detached session whose session names its workspace holds that workspace', async () => {
    const first = windowOf('');
    first.choose('games');
    await told();

    first.opens('session:abc12345', 'studio');
    const detached = windowOf('?window=session%3Aabc12345');
    expect(detached.workspace).toBe('studio');

    first.choose('work');
    await told();
    expect(detached.workspace).toBe('studio');
  });

  it('a monitor follows the window that opened it, and a session detached from the monitor follows the monitor', async () => {
    const first = windowOf('');
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();
    second.opens('monitor');
    const monitor = windowOf('?window=monitor');
    monitor.opens('session:abc12345');
    const detached = windowOf('?window=session%3Aabc12345');
    expect([monitor.workspace, detached.workspace]).toEqual(['games', 'games']);

    first.choose('work');
    await told();
    expect([monitor.workspace, detached.workspace]).toEqual(['games', 'games']);

    second.choose('studio');
    await told();
    expect([monitor.workspace, detached.workspace]).toEqual(['studio', 'studio']);
  });

  it('a window opened again from another main window follows that one from then on', async () => {
    const first = windowOf('');
    first.choose('work');
    await told();
    first.opens('session:abc12345');
    const detached = windowOf('?window=session%3Aabc12345');
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();
    expect(detached.workspace).toBe('work');

    // One window per name: the second press brings it forward, now the second main window's.
    second.opens('session:abc12345');
    await told();
    expect(detached.workspace).toBe('games');
    first.choose('studio');
    await told();
    expect(detached.workspace).toBe('games');
  });

  it('a detached window nothing handed follows the first main window', async () => {
    const first = windowOf('');
    first.choose('work');
    await told();
    const second = windowOf('?window=main%3A2');
    second.choose('games');
    await told();

    const detached = windowOf('?window=session%3Aabc12345');
    expect(detached.workspace).toBe('work');
    first.choose('studio');
    await told();
    expect(detached.workspace).toBe('studio');
  });
});
