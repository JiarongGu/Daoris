import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// A repository's Branches and History tabs in SHELL mode (UX6h1, D150 §4.2, §4.4): Git inside Repositories. Each tab asks
// this machine's driver over DAORIS.DRIVER's `GIT_BRANCHES` and `GIT_GRAPH` (GIT1d) only while it shows, asks nothing for a
// repository with no checkout here, and says a refusal by its code in its place. Over a mocked bridge, as the shell-attached
// half is held (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import type { GitBranchList, GitGraphPage } from './bridge/git';
import { GIT_BRANCHES_ENGINE, GIT_BRANCHES_GAME, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES } from './projects/fixtures';
import { DRIVER_STATE, respond, show, WIRING } from './test/shellHarness';
import { chooseRepository, chooseWorkspace, ProjectsView, repositoryMain } from './test/projectsView';

// The engine has a checkout here; the game is a teammate's, with none.
const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora', root: 'C:/somewhere/engine' },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

// The fixtures held to the bridge's shapes, as the driver answers them.
const ENGINE_LIST: GitBranchList = { repositories: [{ ...GIT_BRANCHES_ENGINE, workspace: 'aurora' }], apart: [], sessionsUnread: false };
const LINE_COMMIT = GIT_BRANCHES_ENGINE.line.commit!;
const PAGE: GitGraphPage = { tips: [LINE_COMMIT], sinceParted: false, page: 0, commits: GIT_HISTORY_ENGINE, more: false };
const answer = <T,>(value: T | null, refusal: string | null = null) => ({
  value, refusal, problem: refusal ? 'git said no' : null, commands: ['git log --graph --date-order main'], remembered: false,
});

const asked = (type: string) => invoke.mock.calls.filter(([, called]) => called === type);

describe("a repository's Branches and History tabs", () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRY) : respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  const answering = ({ branches = () => ENGINE_LIST, graph = () => answer(PAGE) }: {
    branches?: (payload: Record<string, unknown>) => unknown;
    graph?: (payload: Record<string, unknown>) => unknown;
  } = {}) =>
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      const read = type === 'GIT_BRANCHES' ? branches : type === 'GIT_GRAPH' ? graph : null;
      if (read) {
        const answered = read(request?.payload ?? {});
        if (answered instanceof Error) throw answered;
        return answered;
      }
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'PLUGINS') return { plugins: [] };
      if (type === 'RULES') return { defaults: [], scopes: [] };
      if (type === 'ACROSS') return { repositories: [] };
      return DRIVER_STATE;
    });

  it('asks for its branches only once Branches shows, for it alone, and lists them', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'History', 'Workflow', 'Setup']);
    expect(asked('GIT_BRANCHES')).toEqual([]);

    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));

    const list = await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    // Two sessions', three landed and the first five of yours (UX6h2a); the line is the list's head.
    expect(within(list).getAllByRole('listitem')).toHaveLength(10);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_BRANCHES', expect.objectContaining({ payload: { repository: 'engine', workspace: 'aurora' } }));
    expect(asked('GIT_GRAPH')).toEqual([]);
  });

  it("names a session's branch by its session's title, joined on the page", async () => {
    // The branch's session is one the service lists, on a quest it lists: the rail's rule names it by the quest.
    const [line, physics, ...rest] = GIT_BRANCHES_ENGINE.branches;
    const named = { ...physics!, session: { ...physics!.session!, session: 's1a2b3c4', quest: 'abc123' } };
    const repositories = [{ ...ENGINE_LIST.repositories[0]!, branches: [line!, named, ...rest] }];
    answering({ branches: () => ({ ...ENGINE_LIST, repositories }) });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));

    const sessions = await within(repositoryMain()).findByRole('list', { name: "Sessions' (2)" });
    await waitFor(() => expect(within(sessions).getAllByRole('listitem')[0]).toHaveTextContent('Expose a streaming budget'));
    expect(within(repositoryMain()).queryByText(/The session records could not be read/)).toBeNull();
  });

  it('says where the session records went unread: the list says it beside the entries, and the tab reaches it', async () => {
    answering({ branches: () => ({ ...ENGINE_LIST, sessionsUnread: true }) });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    expect(await within(repositoryMain()).findByText('The session records could not be read, so no branch here is named by its session.'))
      .toBeInTheDocument();
  });

  it("draws its line's history from the line's commit, and remembers the tab for the view", async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'History' }));

    const list = await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });
    expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_HISTORY_ENGINE.length);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_GRAPH', expect.objectContaining({ payload: { repository: 'engine', tips: [LINE_COMMIT], workspace: 'aurora' } }));
    expect(within(repositoryMain()).getByRole('tab', { name: 'History' })).toHaveAttribute('aria-selected', 'true');
    expect(window.localStorage.getItem('daoris.list.projects.tab')).toContain('history');
  });

  describe('paging its graph (UX6h3a)', () => {
    const [firstPage, secondPage] = GIT_HISTORY_PAGES;
    const paged = (payload: Record<string, unknown>) =>
      payload.page === 1 ? answer({ ...PAGE, page: 1, commits: secondPage, more: false }) : answer({ ...PAGE, commits: firstPage, more: true });

    it('lists the first page with its foot, asks the next with page 1 on Show more, and accumulates the rows', async () => {
      answering({ graph: paged });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'History' }));

      const list = await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });
      expect(within(list).getAllByRole('listitem')).toHaveLength(firstPage.length);
      expect(within(repositoryMain()).getByText('3 commits listed')).toBeInTheDocument();
      expect(asked('GIT_GRAPH')).toHaveLength(1);

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Show more' }));

      await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_HISTORY_ENGINE.length));
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_GRAPH', expect.objectContaining({ payload: { repository: 'engine', tips: [LINE_COMMIT], page: 1, workspace: 'aurora' } }));
      // The last page took the foot, and the focus is on the first row it brought.
      expect(within(repositoryMain()).queryByRole('button', { name: 'Show more' })).toBeNull();
      expect(document.activeElement).toBe(within(list).getAllByRole('listitem')[firstPage.length]);
    });

    it("says the next page's refusal in the foot, keeps the rows, and asks the same page again on another press", async () => {
      let refuse = true;
      answering({ graph: (payload) => (payload.page === 1 && refuse ? answer(null, 'GIT_NOT_ANSWERED') : paged(payload)) });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'History' }));
      const list = await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });
      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Show more' }));

      expect(await within(repositoryMain()).findByText(/^Git could not answer for this\./)).toBeInTheDocument();
      expect(within(list).getAllByRole('listitem')).toHaveLength(firstPage.length);

      refuse = false;
      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Show more' }));
      await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_HISTORY_ENGINE.length));
    });
  });

  it('says a repository with no checkout here has none, and asks Git nothing', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('game');

    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    expect(await within(repositoryMain()).findByText("game's branches are read from its checkout, and this machine has none.")).toBeInTheDocument();
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'History' }));
    expect(await within(repositoryMain()).findByText("game's history is read from its checkout, and this machine has none.")).toBeInTheDocument();
    expect(asked('GIT_BRANCHES')).toEqual([]);
    expect(asked('GIT_GRAPH')).toEqual([]);
  });

  it('says no checkout where the list holds none for it, and asks for no graph', async () => {
    answering({ branches: () => ({ repositories: [], apart: [], sessionsUnread: false }) });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    expect(await within(repositoryMain()).findByText('No checkout on this machine')).toBeInTheDocument();

    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'History' }));
    expect(await within(repositoryMain()).findByText("engine's history is read from its checkout, and this machine has none.")).toBeInTheDocument();
    expect(asked('GIT_GRAPH')).toEqual([]);
  });

  it('says no checkout where the graph refuses with that code', async () => {
    answering({ graph: () => answer(null, 'GIT_NO_CHECKOUT') });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'History' }));

    expect(await within(repositoryMain()).findByText("engine's history is read from its checkout, and this machine has none.")).toBeInTheDocument();
    expect(asked('GIT_GRAPH')).toHaveLength(1);
  });

  it('words a refusal by its code, in the tab, never the sentence git gave', async () => {
    answering({ graph: () => answer(null, 'GIT_NOT_ANSWERED') });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'History' }));

    expect(await within(repositoryMain()).findByText(/^Git could not answer for this\./)).toBeInTheDocument();
    expect(within(repositoryMain()).queryByText('git said no')).toBeNull();
  });

  it("says its line has no commit here, and asks for no graph", async () => {
    answering({ branches: () => ({ ...ENGINE_LIST, repositories: [{ ...ENGINE_LIST.repositories[0]!, line: { ...GIT_BRANCHES_ENGINE.line, commit: null } }] }) });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'History' }));

    expect(await within(repositoryMain()).findByText('No history yet')).toBeInTheDocument();
    expect(asked('GIT_GRAPH')).toEqual([]);
  });

  it("says a read that failed in the driver's words, in the tab's place", async () => {
    answering({ branches: () => new Error('the driver is still coming up — try again in a moment.') });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));

    expect(await within(repositoryMain()).findByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();
    await waitFor(() => expect(asked('GIT_BRANCHES').length).toBeGreaterThan(0));
  });

  /**
   * UX6h2b2 (the built-in Git design §2.2, D112): a workspace's Branches tab lists each of its repositories' branches as a
   * group above the clean-up and the look, read once for the workspace, a session's branch named by its title in its group.
   * The game holds no branch of Daoris's: it is apart and folded, read alone once the person includes it. A row of another
   * workspace's is filtered out again, as BRSCOPE1's rows are.
   */
  it("lists each repository's branches as a group above the clean-up, and reads one apart only once it is included", async () => {
    const [line, physics, ...rest] = GIT_BRANCHES_ENGINE.branches;
    const named = { ...physics!, session: { ...physics!.session!, session: 's1a2b3c4', quest: 'abc123' } };
    answering({
      branches: (payload) => (payload.repository === 'game'
        ? { repositories: [{ ...GIT_BRANCHES_GAME, workspace: 'aurora' }], apart: [], sessionsUnread: false }
        : {
            repositories: [{ ...GIT_BRANCHES_ENGINE, workspace: 'aurora', branches: [line!, named, ...rest] }],
            apart: [{ repository: 'game', workspace: 'aurora', holds: false }, { repository: 'kiln', workspace: 'forge', holds: false }],
            sessionsUnread: false,
          }),
    });
    show(<ProjectsView notify={() => {}} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    const engine = await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    expect(asked('GIT_BRANCHES').map(([, , request]) => request.payload)).toEqual([{ workspace: 'aurora' }]);
    await waitFor(() => expect(within(engine).getByRole('list', { name: "Sessions' (2)" })).toHaveTextContent('Expose a streaming budget'));
    // Above the clean-up and the look, which keep their place.
    const cleanUp = within(repositoryMain()).getByRole('heading', { name: 'Session branches' });
    expect(engine.compareDocumentPosition(cleanUp) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    // The game is apart, and the other workspace's row is not this tab's.
    await userEvent.click(within(repositoryMain()).getByText("1 repository with a checkout here holds no branch of Daoris's, and is not listed"));
    const apart = within(within(repositoryMain()).getByRole('group', { name: 'Repositories not listed' }));
    expect(apart.queryByLabelText('kiln')).toBeNull();
    await userEvent.click(apart.getByLabelText('game'));
    await userEvent.click(apart.getByRole('button', { name: 'Include (1)' }));

    const game = await within(repositoryMain()).findByRole('region', { name: "game's branches" });
    expect(within(game).getByRole('list', { name: 'Yours (2)' })).toBeInTheDocument();
    expect(asked('GIT_BRANCHES').map(([, , request]) => request.payload)).toEqual([{ workspace: 'aurora' }, { repository: 'game', workspace: 'aurora' }]);

    // Each group's heading is the door to its repository's page.
    await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Open engine' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
  });
});
