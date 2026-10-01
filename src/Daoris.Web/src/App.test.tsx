import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { App } from './App';
import { keys, useRefreshIndex, useRegistry } from './queries';
import { WorkspaceScopeProvider } from './scope';

// The shell in BROWSER mode — no bridge, so nothing shell-only renders — over a stubbed service that
// holds two circles. The switcher is global chrome (platform language §2: global state lives in
// exactly one place), so it is the shell's to test, not any view's.

const STATUS = { semantic: false, tier: 'lexical', note: 'no embedder configured' };
const REPOSITORIES = [
  { name: 'engine', total: 3, local: 3, canonical: 0, workspace: 'default' },
  { name: 'studio', total: 1, local: 1, canonical: 0, workspace: 'aurora' },
];
const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 3, workspace: 'default' },
  { repository: 'studio', adopted: true, registered: true, summary: 'the studio', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

/** What Overview's band is made of, when the fixtures below hand it something to show. */
let QUESTS: unknown[] = [];
let SESSIONS: unknown[] = [];
let ASKS: unknown[] = [];
/** Where a circle stands with its remote (SYNC6b) — no remote, unless a test wires one. */
let SYNC: unknown = { workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] };

let fetchMock: ReturnType<typeof vi.fn>;

function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json(STATUS);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/sync')) return Response.json(SYNC);
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  throw new Error(`unstubbed request: ${url}`);
}

function shell(initial: string | null = null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={initial}>
          <App />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const requested = () => fetchMock.mock.calls.map((call) => String(call[0]));

/**
 * REV3: the service names a registered repository whose checkout is not where the registry says — "a
 * repository that quietly stops contributing looks exactly like one with nothing to say" — and the
 * toast read only the count. It says which, as the error it is. Pressed with `fireEvent`, not
 * `userEvent`: the latter's hover opened the refresh button's tooltip, which outlived the render and
 * made the scope test that ran after it miss its options.
 */
describe('a refresh, said whole', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) =>
      (String(input) === '/api/refresh' && init?.method === 'POST'
        ? Response.json({ entries: 3, repositories: 1, withheld: 0, absent: ['studio'] })
        : respond(String(input))));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => vi.unstubAllGlobals());

  it('a refresh that could not find a registered checkout says which', async () => {
    shell();

    fireEvent.click(await screen.findByRole('button', { name: 'Refresh the index' }));

    expect(await screen.findByText(/Indexed 3 entries/)).toBeTruthy();
    expect(await screen.findByText(/Not found where the registry says: studio/)).toBeTruthy();
    // Absent is never zero (SEM3b): a refresh the semantic half did not run in says nothing of embedding.
    expect(screen.queryByText(/Embedded|vectors/)).toBeNull();
  });

  /**
   * SEM3b (D123): the service embeds every part of a long entry, in pieces the deployment's window
   * bounds, and its refresh answer says what that made. The notice says it beside the count, the
   * figures grouped as the reader's language groups them.
   */
  it('a refresh the semantic half ran in says what it embedded, at what window, and how many it split', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
      (String(input) === '/api/refresh' && init?.method === 'POST'
        ? Response.json({
          entries: 636, repositories: 3, withheld: 0,
          embedded: { entries: 636, pieces: 1267, split: 322, window: 2000 },
        })
        : respond(String(input))));
    shell();

    fireEvent.click(await screen.findByRole('button', { name: 'Refresh the index' }));

    expect(await screen.findByText(
      'Indexed 636 entries from 3 repositories. Embedded 636 entries as 1,267 vectors of at most 2,000 '
      + 'characters; 322 longer than that were split.')).toBeTruthy();
  });

  it('a refresh that split nothing says none was, rather than a zero', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
      (String(input) === '/api/refresh' && init?.method === 'POST'
        ? Response.json({
          entries: 2, repositories: 1, withheld: 0, embedded: { entries: 2, pieces: 2, split: 0, window: 8000 },
        })
        : respond(String(input))));
    shell();

    fireEvent.click(await screen.findByRole('button', { name: 'Refresh the index' }));

    expect(await screen.findByText(/at most 8,000 characters; none was longer, so none was split\.$/)).toBeTruthy();
  });
});

describe('the shell in a browser, over two workspaces', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    // A menu opens Settings at a domain, and the domain is remembered per viewer (D75); a door chooses Quests' item.
    window.localStorage.removeItem('daoris.settings');
    window.localStorage.removeItem('daoris.list.quests.chosen');
  });

  /**
   * REV3: a refresh invalidated EVERY query — the tick-written answers too, whose "fetch" is an empty
   * list, so each refresh wiped *why a quest is sitting* and the trust holds until the next tick.
   */
  it('a refresh asks again what the index feeds, and leaves what the tick wrote alone', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) =>
      (String(input) === '/api/refresh' && init?.method === 'POST'
        ? Response.json({ entries: 3, repositories: 1, withheld: 0 })
        : respond(String(input))));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(keys.considered, [{ quest: 'q1', verdict: 'sitting' }]);
    client.setQueryData(keys.repositories(null), REPOSITORIES);
    const { result } = renderHook(() => useRefreshIndex(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    await act(async () => { await result.current.mutateAsync(); });

    expect(client.getQueryState(keys.considered)?.isInvalidated).toBe(false);
    expect(client.getQueryState(keys.repositories(null))?.isInvalidated).toBe(true);
  });

  /**
   * REV3 web-rest F9: Settings is the machine's, whatever the window is scoped to. Its choices — a
   * rule's scope, an account's workspace default, what a start runs on — came from the SCOPED registry,
   * so a window scoped to one circle offered only that circle's workspaces and repositories.
   */
  it('reads the whole registry for a machine-wide choice, whatever the window is scoped to', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useRegistry('machine'), {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>
          <WorkspaceScopeProvider initial="aurora">{children}</WorkspaceScopeProvider>
        </QueryClientProvider>
      ),
    });

    await waitFor(() => expect(result.current.data).toHaveLength(2));
    expect(requested().filter((url) => url.startsWith('/api/registry'))).toEqual(['/api/registry']);
  });

  it('offers the scope in the app strip, stating that it spans every workspace', async () => {
    shell();
    const scope = await screen.findByRole('combobox', { name: 'workspace' });
    expect(scope).toHaveTextContent('Every workspace · 2');
    // Nothing chosen: the page asks the doors for everything, and says so above rather than picking.
    for (const url of requested()) expect(url).not.toContain('workspace=');
  });

  it('choosing a workspace scopes every door the shell and its views ask', async () => {
    shell();
    await userEvent.click(await screen.findByRole('combobox', { name: 'workspace' }));
    await userEvent.click(await screen.findByRole('option', { name: 'aurora' }));

    await waitFor(() => {
      const urls = requested();
      // The badge's quests, the foot's count, the landing view's registry — all scoped, none mixed.
      expect(urls.some((url) => url.startsWith('/api/quests') && url.includes('workspace=aurora'))).toBe(true);
      expect(urls.some((url) => url.startsWith('/api/repositories') && url.includes('workspace=aurora'))).toBe(true);
      expect(urls.some((url) => url.startsWith('/api/registry') && url.includes('workspace=aurora'))).toBe(true);
    });
    expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('aurora');
  });

  /**
   * D75: the Workspace menu is the scope's second door. It lists every workspace with what it holds,
   * ticks the scope, and choosing one scopes the window as the switcher does.
   */
  /**
   * D75 §3: the workspace is named whatever the machine holds. Seen on the installed window with none
   * registered: the top bar and the status bar both said "every workspace", of nothing, and the
   * switcher that would have said more is absent below two by WSP5's rule. The rule still hides the
   * control; it no longer hides the fact.
   */
  const withRegistry = (rows: unknown[]) => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(rows) : respond(url);
    });
    vi.stubGlobal('fetch', fetchMock);
  };

  it('says there is no workspace yet in the top bar and the status bar, with none registered', async () => {
    withRegistry([]);
    shell();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Commands (Ctrl+K)' })).toHaveTextContent('no workspace yet'));
    expect(screen.getByRole('contentinfo')).toHaveTextContent('no workspace yet');
    expect(screen.queryByRole('combobox', { name: 'workspace' })).toBeNull();
  });

  /**
   * 🔴 Seen on the install once retiring emptied the index (POLISH5): the repositories card was its
   * heading over a footnote about adopted dots, with nothing for the dots to mark.
   */
  it('says the index holds nothing, rather than a footnote under an empty chart', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL) =>
      String(input).startsWith('/api/repositories') ? Response.json([]) : respond(String(input)));
    shell();

    expect(await screen.findByText('The index holds nothing yet')).toBeInTheDocument();
    expect(screen.queryByText(/adopted — it carries its own declaration/)).toBeNull();
  });

  it('names the one workspace there is, with no switcher to choose it', async () => {
    withRegistry([REGISTRY[1]]);
    shell();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Commands (Ctrl+K)' })).toHaveTextContent('aurora'));
    expect(screen.getByRole('contentinfo')).toHaveTextContent('aurora');
    expect(screen.queryByRole('combobox', { name: 'workspace' })).toBeNull();
  });

  /** A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway (see `AppMenu.test`). */
  const openMenu = async (name: string) => {
    const user = userEvent.setup();
    (await screen.findByRole('button', { name })).focus();
    await user.keyboard('{Enter}');
    return user;
  };

  it('the Workspace menu lists each workspace, ticks the scope, and choosing one scopes the window', async () => {
    shell();
    await screen.findByRole('combobox', { name: 'workspace' });
    const user = await openMenu('Workspace');
    const every = await screen.findByRole('menuitem', { name: /Every workspace · 2/ });
    expect(every.querySelector('svg')).not.toBeNull();
    // A browser is offered the list, and none of the machine's acts.
    expect(screen.queryByRole('menuitem', { name: /Add repository/ })).toBeNull();

    await user.click(screen.getByRole('menuitem', { name: /aurora/ }));
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('aurora'));
  });

  /** D75: a menu item opens its own domain of Settings; a browser's Agents menu holds only what it may know. */
  it('the Agents menu opens Settings at the domain it names', async () => {
    shell();
    const user = await openMenu('Agents');
    await screen.findByRole('menuitem', { name: "AI features" });
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(["AI features"]);

    await user.click(screen.getByRole('menuitem', { name: "AI features" }));
    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getByRole('button', { name: "AI features" })).toHaveAttribute('aria-current', 'page');
  });

  /**
   * SETUP1a (D97): the Daoris menu's *Set up Daoris* opens Get started — a browser's too, holding the one
   * step a browser can know, and saying the rest is the desktop's.
   */
  it('the Daoris menu sets Daoris up, opening Get started', async () => {
    shell();
    const user = await openMenu('Daoris');

    await user.click(await screen.findByRole('menuitem', { name: 'Setup' }));
    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getByRole('button', { name: 'Setup' })).toHaveAttribute('aria-current', 'page');
    const steps = await screen.findByRole('list', { name: 'setup steps' });
    expect(within(steps).getByRole('listitem', { name: '3. A workspace and its repositories' })).toHaveTextContent('done');
  });

  /** REV3 web-rest F14: a window is the shell's to open, so a browser's View menu offers none. */
  it('the View menu offers a browser the palette and no window', async () => {
    shell();
    await openMenu('View');
    await screen.findByRole('menuitem', { name: 'Commands' });
    expect(screen.queryByRole('menuitem', { name: 'Monitor window' })).toBeNull();
  });

  /**
   * SYNC6b: where the chosen circle stands comes from this machine's host, so a browser here reads it
   * — and a conflict in it opens that quest. What a browser does not get is *Sync now*: the pass is
   * the shell's.
   */
  it('shows where a wired circle stands, opens a conflicted quest, and offers no Sync now in a browser', async () => {
    SYNC = {
      workspace: 'aurora', wired: true, ahead: 2, behind: [], conflicts: ['q1a2b3'],
      synced: '2026-09-24T10:00:00Z', tried: '2026-09-24T10:00:00Z', problem: null,
    };
    QUESTS = [{
      id: 'q1a2b3', from: 'engine', to: 'studio', title: 'Lost the race', body: 'why', status: 'Taken',
      filed: '2026-09-24T09:00:00Z', updated: '2026-09-24T10:00:00Z', workspace: 'aurora',
      conflicts: [{ machine: 'm2', attempted: 'Taken', note: 'the other machine', at: '2026-09-24T10:00:00Z' }],
    }];
    try {
      shell('aurora');
      const user = userEvent.setup();
      const item = await screen.findByRole('button', { name: 'sync' });
      expect(requested()).toContain('/api/sync?workspace=aurora');

      item.focus();
      await user.keyboard('{Enter}');
      expect(screen.queryByRole('menuitem', { name: /Sync now/ })).toBeNull();
      await user.keyboard('{Enter}');

      // Its page, in Quests' main area (FRAME1d): a record is the main area, never a drawer.
      await screen.findByRole('heading', { level: 1, name: 'Lost the race' });
      expect(within(screen.getByRole('main')).getByRole('region', { name: 'Conflicts' })).toBeInTheDocument();
    } finally {
      SYNC = { workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] };
      QUESTS = [];
    }
  });

  it('a remembered workspace that no longer exists falls back to every, out loud', async () => {
    shell('gone');
    await waitFor(() => {
      expect(screen.getByRole('combobox', { name: 'workspace' })).toHaveTextContent('Every workspace · 2');
    });
    // …and the queries settle on every circle, not on a scope nobody holds any more.
    await waitFor(() => {
      const latest = requested().filter((url) => url.startsWith('/api/registry')).at(-1);
      expect(latest).not.toContain('workspace=');
    });
  });
});

/**
 * One navigation (D66), in a browser. Sessions' centre is a stream and its rows carry tree paths,
 * and neither may leave the machine that produced them (D47 §4) — so the view is absent from the bar
 * rather than disabled, and a remembered `sessions` cannot resurrect it. Settings is there: a browser
 * has appearance to set.
 */
describe('the views, in a browser', () => {
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    window.localStorage.removeItem('daoris.view');
  });

  it('offers no Sessions and no mode switch — and does offer Settings', async () => {
    shell();
    const bar = await screen.findByRole('navigation', { name: 'Views' });

    expect(within(bar).queryByRole('button', { name: 'Sessions' })).toBeNull();
    expect(screen.queryByRole('group', { name: /mode/i })).toBeNull();
    expect(within(bar).getByRole('button', { name: 'Settings' })).toBeInTheDocument();
  });

  /**
   * 🔴 Seen on the window (PERM1): Settings grew a second scrollbar. `sr-only` text is absolutely
   * positioned, and with no positioned ancestor inside the scroll column its containing block was
   * the viewport — so a screen-reader label 3,000px down the column stretched the whole document.
   * jsdom lays nothing out, so this holds the one property that prevents it.
   */
  it('makes the scroll column the containing block for what is positioned inside it', async () => {
    shell();
    await screen.findByRole('navigation', { name: 'Views' });

    expect(screen.getByRole('main')).toHaveClass('relative');
  });

  /**
   * UX5 U59: content did not follow the window. The column was capped at 72rem, so a maximized window left every view but Sessions a third
   * empty. Content follows the window; prose keeps its own measure (`Prose`), a form its own size.
   */
  it('lets every view follow the window, capping no content column', async () => {
    shell();
    await screen.findByRole('navigation', { name: 'Views' });

    const main = screen.getByRole('main');
    for (const element of [main, ...main.querySelectorAll(':scope > *')]) {
      expect(element.getAttribute('class') ?? '').not.toMatch(/\bmax-w-/);
    }
    // And prose keeps its measure: uncapped, the repositories' key ran 121 characters a line.
    expect(await screen.findByText(/adopted — it carries its own declaration/)).toHaveClass('max-w-prose');
  });

  /**
   * D118 §4, amending DOCK1a: a browser keeps the view's list and its main area, and never the side bar or
   * the panel. Overview has no list, so its strip holds no list toggle either: absent, never disabled.
   */
  it('draws the view in its main area, with no side bar, no panel, and no list toggle where the view has no list', async () => {
    shell();
    await screen.findByRole('heading', { name: 'Overview' });

    expect(screen.getByRole('main')).toHaveClass('@container/main');
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();
    expect(screen.queryByRole('region', { name: 'the panel' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^show or hide/ })).toBeNull();
    expect(document.querySelector('[data-region="list"]')).toBeNull();
  });

  /** FRAME1d (D118 §4): Quests is the first view a browser draws with its list, beside its main area. */
  it("keeps Quests' list and its main area, with the list's toggle alone on its strip", async () => {
    shell();
    await userEvent.click(within(await screen.findByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Quests' }));

    expect(await screen.findByRole('complementary', { name: 'Quests' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'show or hide the quest list (Ctrl+B)' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByText('Choose a quest or an ask')).toBeInTheDocument();
    expect(screen.queryByRole('complementary', { name: 'right side bar' })).toBeNull();
    expect(screen.queryByRole('button', { name: /show or hide the panel/ })).toBeNull();
  });

  /** FRAME1e (D118 §4): Repositories is a browser's list too, and it makes nothing there, since adding is a shell's. */
  it("keeps Repositories' list and its main area, and offers a browser nothing to add or import", async () => {
    shell();
    await userEvent.click(within(await screen.findByRole('navigation', { name: 'Views' })).getByRole('button', { name: 'Repositories' }));

    const list = await screen.findByRole('complementary', { name: 'Repositories' });
    expect(await within(list).findByRole('heading', { name: 'Adopted (2)' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'show or hide the repository list (Ctrl+B)' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByText('Choose a repository')).toBeInTheDocument();
    // Adding and importing touch machine paths (D48 §7): the list's ＋ and its ⋯ are absent, never disabled.
    expect(within(list).queryByRole('button', { name: 'Add repository' })).toBeNull();
    expect(within(list).queryByRole('button', { name: 'More actions' })).toBeNull();
  });

  /**
   * FRAME1f (D118 §2, §4): Search's list is the box, *local only* and the hits, and Convergence's the similarity and the
   * findings, each beside its main area in a browser too; each makes nothing, so neither list has a ＋. Neither asks the
   * service anything until it is in front: a comparison over a real index takes seconds.
   */
  it("keeps Search's and Convergence's lists and their main areas, asking nothing until each is in front", async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/convergence')) return Response.json([]);
      return respond(url);
    });
    shell();
    const views = await screen.findByRole('navigation', { name: 'Views' });
    expect(requested().some((url) => url.startsWith('/api/convergence') || url.startsWith('/api/search'))).toBe(false);

    await userEvent.click(within(views).getByRole('button', { name: 'Search' }));
    const results = await screen.findByRole('complementary', { name: 'Search' });
    expect(within(results).getByRole('searchbox', { name: 'search knowledge' })).toHaveFocus();
    expect(within(results).getByRole('checkbox', { name: "Each repository's own only" })).toBeChecked();
    expect(screen.getByRole('button', { name: 'show or hide the result list (Ctrl+B)' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByText('Choose a result')).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();

    await userEvent.click(within(views).getByRole('button', { name: 'Convergence' }));
    const findings = await screen.findByRole('complementary', { name: 'Convergence' });
    expect(await within(findings).findByText('Nothing converges at 0.75 or above')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'show or hide the finding list (Ctrl+B)' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByText('Choose a finding')).toBeInTheDocument();
    expect(within(findings).queryByRole('button', { name: /^New|^Add/ })).toBeNull();
  });

  it('falls back to Overview when the browser remembers a view this deployment does not have', async () => {
    window.localStorage.setItem('daoris.view', 'sessions');
    shell();

    // The landing view, not an empty Sessions.
    expect(await screen.findByRole('heading', { name: 'Overview' })).toBeInTheDocument();
    expect(screen.queryByLabelText('sessions')).toBeNull();
  });

  /** In a browser, Settings is appearance and nothing of a machine (D47 §4). */
  it('opens Settings on appearance alone', async () => {
    shell();
    await userEvent.click(await screen.findByRole('button', { name: 'Settings' }));

    expect(await screen.findByRole('radiogroup', { name: 'Theme' })).toBeInTheDocument();
    expect(screen.getByRole('radiogroup', { name: 'Language' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'This machine' })).toBeNull();
  });

  /** Ambient truth is true on every view, so the bar belongs to the application. */
  it('carries the status bar, saying plainly that this machine has no driver', async () => {
    shell();

    const bar = await screen.findByLabelText('state of this machine');
    expect(bar).toHaveTextContent('none here');
    // The remote question cannot be asked without the map, so it is not answered either.
    expect(bar).not.toHaveTextContent('remote');
  });
});

/**
 * D118 §3i: every door into a view names the item it opens, through the application's one opener, and the
 * view's list remembers it as its chosen item (§3f) — a quest's, an ask's, a domain's. Since FRAME1d Quests
 * shows its chosen item in its main area, so a quest's record and an ask's open there, never in a drawer.
 */
describe('every door names its item', () => {
  const QUEST = {
    id: '5e7a11', from: 'game', to: 'engine', title: 'Read the media field names from config',
    body: 'the names are hard-coded.', status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
  };
  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    QUESTS = [];
    ASKS = [];
    SYNC = { workspace: 'default', wired: false, ahead: 0, behind: [], conflicts: [] };
    window.localStorage.clear();
  });

  it("Overview's outstanding row opens its quest, and Quests' list has it chosen", async () => {
    QUESTS = [QUEST];
    shell();
    await userEvent.click(await screen.findByRole('button', { name: /Read the media field names from config/ }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Read the media field names from config' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('5e7a11');
    // Chosen in the list beside it, which a browser keeps (D118 §4).
    const list = screen.getByRole('complementary', { name: 'Quests' });
    expect(within(list).getByRole('button', { name: /Read the media field names from config/ })).toHaveAttribute('aria-current', 'true');
  });

  it("What needs you's ask row opens the ask, chosen as an ask", async () => {
    ASKS = [{
      id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.', state: 'Proposed',
      tier: 'declarations', asked: '2026-09-21T08:00:00Z', updated: '2026-09-21T08:00:00Z',
      links: [], attachments: [], quests: [], proposal: [{ repository: 'engine', score: 3, matched: ['frame'] }],
    }];
    shell();
    await userEvent.click(await screen.findByRole('button', { name: /Cap the hydration per frame\./ }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Cap the hydration per frame.' })).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('ask:7c1e9a04b2d5');
  });

  it("the status bar's conflict opens its quest, chosen", async () => {
    SYNC = {
      workspace: 'aurora', wired: true, ahead: 0, behind: [], conflicts: ['5e7a11'],
      synced: '2026-09-24T10:00:00Z', tried: '2026-09-24T10:00:00Z', problem: null,
    };
    QUESTS = [{ ...QUEST, workspace: 'aurora' }];
    shell('aurora');
    const user = userEvent.setup();
    (await screen.findByRole('button', { name: 'sync' })).focus();
    await user.keyboard('{Enter}');
    await user.keyboard('{Enter}');

    expect(await screen.findByRole('heading', { level: 1, name: 'Read the media field names from config' })).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('5e7a11');
  });

  /**
   * FRAME1e: a repository's page has a door to its code map, one level into the Map (MAP3a), and the list remembers the
   * repository. The Map's own place opens the workspace, never a code map a door left there.
   */
  it("a repository's page opens its code map on the Map, which its own place leaves for the workspace", async () => {
    fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/code-map/')) {
        return Response.json({
          repository: 'engine', file: 'docs/code-map.json', problem: null,
          modules: [{ id: 'renderer', path: 'src/renderer', summary: 'draws frames' }], dependencies: [],
        });
      }
      if (url.startsWith('/api/convergence')) return Response.json([]);
      return respond(url);
    });
    vi.stubGlobal('fetch', fetchMock);
    shell();
    const views = await screen.findByRole('navigation', { name: 'Views' });
    await userEvent.click(within(views).getByRole('button', { name: 'Repositories' }));
    const list = await screen.findByRole('complementary', { name: 'Repositories' });
    await userEvent.click(within(await within(list).findByRole('listitem', { name: 'engine' })).getByRole('button'));
    expect(window.localStorage.getItem('daoris.list.projects.chosen')).toBe('engine');

    await userEvent.click(await within(screen.getByRole('main')).findByRole('button', { name: 'Open code map' }));
    expect(await screen.findByRole('heading', { name: 'engine: code map' })).toBeInTheDocument();

    await userEvent.click(within(views).getByRole('button', { name: 'Overview' }));
    await userEvent.click(within(views).getByRole('button', { name: 'Map' }));
    expect(await screen.findByRole('heading', { name: 'Map' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'engine: code map' })).toBeNull();
  });

  it("the status bar's tier opens Settings at Daoris's own AI, chosen in its list", async () => {
    shell();
    await userEvent.click(await screen.findByRole('button', { name: 'recall' }));

    const domains = await screen.findByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getByRole('button', { name: 'AI features' })).toHaveAttribute('aria-current', 'page');
    expect(window.localStorage.getItem('daoris.settings')).toBe('ai');
  });
});

/**
 * *What needs you* (design §4). It is service data, so a browser sees it — knowing is the half
 * that travels — but the doors into Work are not offered where Work does not exist.
 */
describe('the attention band', () => {
  beforeEach(() => {
    QUESTS = [];
    SESSIONS = [];
    fetchMock = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
    vi.stubGlobal('fetch', fetchMock);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    QUESTS = [];
    SESSIONS = [];
    // A door chooses Quests' item, which is remembered per viewer (D118 §3f).
    window.localStorage.removeItem('daoris.list.quests.chosen');
  });

  it('is absent entirely when nothing is waiting — an always-there all-clear is not read', async () => {
    shell();
    await screen.findByRole('heading', { name: 'Overview' });

    expect(screen.queryByText('What needs you')).toBeNull();
  });

  it('names a parked session and a quest nobody here can take', async () => {
    SESSIONS = [{
      id: 'p4rk3d00', quest: null, repository: 'engine', adapter: 'stub', kind: 'chat',
      state: 'awaiting-person', note: 'two ways forward.',
      created: '2026-09-21T09:00:00Z', updated: '2026-09-21T10:00:00Z',
    }];
    QUESTS = [{
      id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget',
      body: 'a per-frame cap.', status: 'Open',
      filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
    }];

    shell();

    expect(await screen.findByText('What needs you')).toBeInTheDocument();
    expect(screen.getByText('parked at a checkpoint')).toBeInTheDocument();
    expect(screen.getByText('nobody here can take this')).toBeInTheDocument();
    // The category the design names third is not invented: SURF6's viewed mark is not kept, so
    // nothing records looking — and the band says that, not that it waits on review (POLISH4).
    expect(screen.getByText(/nothing yet keeps a record of what you have looked at/)).toBeInTheDocument();
  });

  /**
   * 🔴 A badge counts what its place holds (UX5 U20, the owner's choice). The band's count is
   * Overview's, beside the band that lists it; it rode the Sessions icon, which held a sixth of it.
   */
  it("puts the band's count on Overview's icon, in the waiting hue", async () => {
    SESSIONS = [{
      id: 'p4rk3d00', quest: null, repository: 'engine', adapter: 'stub', kind: 'chat',
      state: 'awaiting-person', note: 'two ways forward.',
      created: '2026-09-21T09:00:00Z', updated: '2026-09-21T10:00:00Z',
    }];
    QUESTS = [{
      id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget',
      body: 'a per-frame cap.', status: 'Open',
      filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
    }];
    shell();

    await screen.findByText('What needs you');
    const bar = screen.getByRole('navigation', { name: 'Views' });
    const overview = within(bar).getByRole('button', { name: 'Overview' });
    await waitFor(() => expect(within(overview).getByText('2')).toBeInTheDocument());
    expect(within(overview).getByText('2').className).toContain('text-st-open');
  });

  /**
   * 🔴 UX5 U26: an outstanding row went to Quests and opened nothing, so the person pressed a quest
   * and had to find it again in the list. Platform language §5: *outstanding rows open the quest*,
   * as the band's quest rows already did — on its page in Quests' main area since FRAME1d.
   */
  it('opens an outstanding quest on its own page, not only its view', async () => {
    QUESTS = [{
      id: '5e7a11', from: 'game', to: 'engine', title: 'Read the media field names from config',
      body: 'the names are hard-coded.', status: 'Open',
      filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
    }];
    shell();

    // Its receiver is registered, so the band does not list it: this button is the outstanding row.
    await userEvent.click(await screen.findByRole('button', { name: /Read the media field names from config/ }));

    await screen.findByRole('heading', { level: 1, name: 'Read the media field names from config' });
    expect(within(screen.getByRole('main')).getByText('#5e7a11')).toBeInTheDocument();
  });

  /**
   * A door opens something, or it is not a door (platform language §4). A browser has no Sessions, so
   * a parked row is text there; a quest nobody can take opens its own page, which a browser has.
   */
  it('makes a row a door only where its destination exists', async () => {
    SESSIONS = [{
      id: 'p4rk3d00', quest: null, repository: 'engine', adapter: 'stub', kind: 'chat',
      state: 'awaiting-person', note: 'two ways forward.',
      created: '2026-09-21T09:00:00Z', updated: '2026-09-21T10:00:00Z',
    }];
    QUESTS = [{
      id: '7a82cc', from: 'engine', to: 'retired', title: 'Expose a streaming budget',
      body: 'a per-frame cap.', status: 'Open',
      filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
    }];
    shell();

    const band = await screen.findByRole('region', { name: 'What needs you' });
    expect(within(band).queryByRole('button', { name: /two ways forward/ })).toBeNull();
    expect(within(band).getByText('two ways forward.')).toBeInTheDocument();

    await userEvent.click(within(band).getByRole('button', { name: /Expose a streaming budget/ }));
    await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' });
    expect(within(screen.getByRole('main')).getByText('#7a82cc')).toBeInTheDocument();
  });

  /**
   * INT4d: an ask waits on a person — a proposal only a person publishes, or an intake that parked
   * asking — and its row is a door into the ask's record in Quests, where the answer is. The parked
   * intake is a session awaiting a person too, and is counted once: as the ask.
   */
  it('names each ask waiting on a person once, and opens its record where it is answered', async () => {
    const proposed = {
      id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.\n\nThe trace is attached.',
      state: 'Proposed', tier: 'declarations', asked: '2026-09-21T08:00:00Z', updated: '2026-09-21T08:00:00Z',
      links: [], attachments: [], quests: [],
      proposal: [{ repository: 'engine', score: 3, matched: ['frame', 'hydration'] }],
    };
    ASKS = [
      proposed,
      { ...proposed, id: '0b9f3c21aa77', sentence: 'Tidy the release notes.', proposal: [], intake: 'i9n8t7k6' },
    ];
    SESSIONS = [{
      id: 'i9n8t7k6', quest: null, repository: 'ask #0b9f3c21aa77', adapter: 'stub', kind: 'chat',
      state: 'awaiting-person', ask: '0b9f3c21aa77', note: 'published nothing: it asks you rather than guess.',
      created: '2026-09-21T09:00:00Z', updated: '2026-09-21T09:30:00Z',
    }];
    try {
      shell();

      expect(await screen.findByText('proposed, not yet published')).toBeInTheDocument();
      expect(screen.getByText('its intake asked you')).toBeInTheDocument();
      expect(screen.queryByText('parked at a checkpoint')).toBeNull();

      await userEvent.click(screen.getByRole('button', { name: /Cap the hydration per frame\./ }));
      await screen.findByRole('heading', { level: 1, name: 'Cap the hydration per frame.' });
      expect(within(screen.getByRole('main')).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
    } finally {
      ASKS = [];
    }
  });
});
