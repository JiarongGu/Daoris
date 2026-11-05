import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// A repository's Branches and History tabs in SHELL mode (UX6h1, D150 §4.2, §4.4): Git inside Repositories. Each tab asks
// this machine's driver over DAORIS.DRIVER's `GIT_BRANCHES` and `GIT_GRAPH` (GIT1d) only while it shows, asks nothing for a
// repository with no checkout here, and says a refusal by its code in its place. Over a mocked bridge, as the shell-attached
// half is held (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: () => undefined,
}));

import type { GitBranchList, GitGraphPage } from '../bridge/repositories/git';
import {
  GIT_BRANCH_PARTED, GIT_BRANCH_PARTED_PAGES, GIT_BRANCHES_ENGINE, GIT_BRANCHES_GAME, GIT_CHANGES_ENGINE, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES,
  GIT_HUNKS_BUDGET, GIT_COMMIT_PLAN_ENGINE, GIT_STASH_PLAN_ENGINE, GIT_SWITCH_PLAN_ENGINE,
} from './fixtures';
import { SHARED_BRANCH, SHARED_UNITS, traceAnswerOf } from './git/landingFixtures';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { chooseRepository, chooseWorkspace, ProjectsView, repositoryMain } from '../test/projectsView';

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

  const answering = ({ branches = () => ENGINE_LIST, graph = () => answer(PAGE), log = () => ({ lines: [] }), trace, work }: {
    branches?: (payload: Record<string, unknown>) => unknown;
    graph?: (payload: Record<string, unknown>) => unknown;
    /** What the machine log answers `LINES` with (AUTOTIDY1a). */
    log?: () => unknown;
    /** What `TRACE` and `WORK_STATE` answer (FLOWCHART1c). */
    trace?: (payload: Record<string, unknown>) => unknown;
    work?: (payload: Record<string, unknown>) => unknown;
  } = {}) =>
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (module === 'DAORIS.LOG') return log();
      const read = type === 'GIT_BRANCHES' ? branches : type === 'GIT_GRAPH' ? graph
        : type === 'TRACE' && trace ? trace : type === 'WORK_STATE' && work ? work : null;
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
    expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'History', 'Integration']);
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

  describe('over all its branches (UX6h3c)', () => {
    const tick = async () => {
      const options = within(repositoryMain()).getByRole('button', { name: 'History options' });
      options.focus();
      await userEvent.keyboard('{Enter}');
      await userEvent.click(await screen.findByRole('menuitemcheckbox', { name: 'All branches' }));
    };
    const tipsOf = (call: unknown[]) => ((call[2] as { payload: { tips: string[] } }).payload.tips);

    it("asks the graph for the line's commit then each branch's, once and in the list's order, and pages it", async () => {
      const everyCommit = [...new Set([LINE_COMMIT, ...GIT_BRANCHES_ENGINE.branches.map((branch) => branch.commit!)])];
      answering({ graph: (payload) => answer({ ...PAGE, tips: payload.tips as string[], commits: firstOf(payload), more: payload.page !== 1 }) });
      function firstOf(payload: Record<string, unknown>) { return payload.page === 1 ? GIT_HISTORY_PAGES[1] : GIT_HISTORY_PAGES[0]; }
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'History' }));
      await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });

      await tick();

      const list = await within(repositoryMain()).findByRole('list', { name: "engine's history over all its branches" });
      expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_HISTORY_PAGES[0].length);
      const call = asked('GIT_GRAPH').at(-1)!;
      expect(tipsOf(call)).toEqual(everyCommit);
      expect(tipsOf(call)[0]).toBe(LINE_COMMIT);
      expect(everyCommit.length).toBe(GIT_BRANCHES_ENGINE.branches.length);

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Show more' }));
      await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_HISTORY_ENGINE.length));
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_GRAPH', expect.objectContaining({ payload: { repository: 'engine', tips: everyCommit, page: 1, workspace: 'aurora' } }));

      // Unticked, it is the line's graph again.
      await tick();
      await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });
      expect(tipsOf(asked('GIT_GRAPH').at(-1)!)).toEqual([LINE_COMMIT]);
    });

    it('never asks with more than the read carries, and says it drew the first 256 of the branches', async () => {
      const many = Array.from({ length: 300 }, (_, index) => ({ ...GIT_BRANCHES_ENGINE.branches[6]!, name: `bulk/b${index}`, commit: `${String(index).padStart(40, '0')}` }));
      const crowded: GitBranchList = {
        repositories: [{ ...GIT_BRANCHES_ENGINE, workspace: 'aurora', branches: [...GIT_BRANCHES_ENGINE.branches, ...many] }], apart: [], sessionsUnread: false,
      };
      answering({ branches: () => crowded });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'History' }));
      await within(repositoryMain()).findByRole('list', { name: "engine's history on main" });

      await tick();

      await within(repositoryMain()).findByRole('list', { name: "engine's history over all its branches" });
      const tips = tipsOf(asked('GIT_GRAPH').at(-1)!);
      expect(tips).toHaveLength(256);
      expect(tips[0]).toBe(LINE_COMMIT);
      expect(within(repositoryMain()).getByText(/the first 256 of 314 branches/)).toBeInTheDocument();
    });
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

  /**
   * UX6h3b (the built-in Git design §2.3): a branch's name in Branches opens its page in the main area, page state of the
   * view's. The page asks `GIT_GRAPH` since the branch parted from its line, its two tips the branch's then the line's, pages
   * it with the load-more foot, and goes back to the repository's Branches. The line's own row opens History.
   */
  describe('a branch opened from Branches (UX6h3b)', () => {
    const PHYSICS = 'daoris/engine-physics-step';
    const PHYSICS_COMMIT = GIT_BRANCHES_ENGINE.branches.find((branch) => branch.name === PHYSICS)!.commit;
    const PARTED: GitGraphPage = { tips: [PHYSICS_COMMIT, LINE_COMMIT], sinceParted: true, page: 0, commits: GIT_BRANCH_PARTED_PAGES[0], more: true };
    const graph = (payload: Record<string, unknown>) => (!payload.sinceParted
      ? answer(PAGE)
      : payload.page === 1
        ? answer({ ...PARTED, page: 1, commits: GIT_BRANCH_PARTED_PAGES[1], more: false })
        : answer(PARTED));

    it('opens its page, asks its graph since it parted from its line, pages it, and goes back to Branches', async () => {
      answering({ graph });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: `Open ${PHYSICS}` }));

      expect(await within(repositoryMain()).findByRole('heading', { level: 1, name: PHYSICS })).toBeInTheDocument();
      const list = await within(repositoryMain()).findByRole('list', { name: `${PHYSICS} beside main since they parted` });
      expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_BRANCH_PARTED_PAGES[0].length);
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_GRAPH', expect.objectContaining({
        payload: { repository: 'engine', tips: [PHYSICS_COMMIT, LINE_COMMIT], sinceParted: true, workspace: 'aurora' },
      }));
      // Its standing, in the list's words.
      expect(within(repositoryMain()).getByRole('heading', { level: 1 }).closest('header')).toHaveTextContent('2 commits ahead of main, and 2 behind.');

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Show more' }));
      await waitFor(() => expect(within(list).getAllByRole('listitem')).toHaveLength(GIT_BRANCH_PARTED.length));
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_GRAPH', expect.objectContaining({
        payload: { repository: 'engine', tips: [PHYSICS_COMMIT, LINE_COMMIT], page: 1, sinceParted: true, workspace: 'aurora' },
      }));

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' }));
      expect(await within(repositoryMain()).findByRole('region', { name: "engine's branches" })).toBeInTheDocument();
      expect(within(repositoryMain()).getByRole('tab', { name: 'Branches' })).toHaveAttribute('aria-selected', 'true');
      expect(within(repositoryMain()).getByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
    });

    it("opens History from the line's own row", async () => {
      answering({ graph });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: "Open main's history" }));

      expect(await within(repositoryMain()).findByRole('list', { name: "engine's history on main" })).toBeInTheDocument();
      expect(within(repositoryMain()).getByRole('tab', { name: 'History' })).toHaveAttribute('aria-selected', 'true');
    });

    it("opens from a workspace's Branches on the repository's page, whose way back is its Branches", async () => {
      answering({ graph });
      show(<ProjectsView notify={() => {}} />);
      await chooseWorkspace('aurora');
      await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));
      const engine = await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
      await userEvent.click(within(engine).getByRole('button', { name: `Open ${PHYSICS}` }));

      expect(await within(repositoryMain()).findByRole('heading', { level: 1, name: PHYSICS })).toBeInTheDocument();
      await within(repositoryMain()).findByRole('list', { name: `${PHYSICS} beside main since they parted` });

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' }));
      expect(await within(repositoryMain()).findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
      expect(within(repositoryMain()).getByRole('tab', { name: 'Branches' })).toHaveAttribute('aria-selected', 'true');
    });

    // UX6h3d: the way back returns the focus to the press that opened the page, so a keyboard reader keeps their place.
    it("leaves the focus on the opened branch's name button when it goes back", async () => {
      answering({ graph });
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: `Open ${PHYSICS}` }));
      await within(repositoryMain()).findByRole('list', { name: `${PHYSICS} beside main since they parted` });

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' }));
      await waitFor(() => expect(within(repositoryMain()).getByRole('button', { name: `Open ${PHYSICS}` })).toHaveFocus());
    });

    it('leaves the focus on the page heading when the branch is no longer listed', async () => {
      const without = { ...ENGINE_LIST, repositories: [{ ...ENGINE_LIST.repositories[0]!, branches: GIT_BRANCHES_ENGINE.branches.filter((branch) => branch.name !== PHYSICS) }] };
      answering({ graph, branches: (payload) => (payload.repository ? without : ENGINE_LIST) });
      show(<ProjectsView notify={() => {}} />);
      await chooseWorkspace('aurora');
      await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));
      const engine = await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
      await userEvent.click(within(engine).getByRole('button', { name: `Open ${PHYSICS}` }));
      await within(repositoryMain()).findByText('No such branch here');

      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' }));
      await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
      await waitFor(() => expect(within(repositoryMain()).getByRole('heading', { level: 1, name: 'engine' })).toHaveFocus());
    });

    it('says a branch its repository no longer lists, with the way back', async () => {
      // The workspace's read lists it; the repository's own, read for the page, no longer does.
      const without = { ...ENGINE_LIST, repositories: [{ ...ENGINE_LIST.repositories[0]!, branches: GIT_BRANCHES_ENGINE.branches.filter((branch) => branch.name !== PHYSICS) }] };
      answering({ graph, branches: (payload) => (payload.repository ? without : ENGINE_LIST) });
      show(<ProjectsView notify={() => {}} />);
      await chooseWorkspace('aurora');
      await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));
      const engine = await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
      await userEvent.click(within(engine).getByRole('button', { name: `Open ${PHYSICS}` }));

      expect(await within(repositoryMain()).findByText('No such branch here')).toBeInTheDocument();
      expect(asked('GIT_GRAPH').filter(([, , request]) => request.payload.sinceParted)).toEqual([]);
      expect(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' })).toBeInTheDocument();
    });
  });

  /**
   * FLOWCHART1c (the flowchart design §2.7, §10's trap): a landed branch's page draws how it landed, read once its section opens:
   * the trace of the session its landing record names, never a unit's executor, and the units of work the branch carries, by the
   * branch. A door into it (the work's milestone, a run's landing step) opens the page with the section open.
   */
  describe('how a landed branch landed (FLOWCHART1c)', () => {
    const LANDED = 'review/q6-asset-cache';
    // The landing record names `s-21` (the list's fixture); a unit of the branch's names another executor, which is never read by.
    const landed = { ...SHARED_BRANCH, branch: LANDED, session: 's-21' };
    const reads = {
      trace: (payload: Record<string, unknown>) => (payload.id === 's-21' ? traceAnswerOf('s-21', landed) : { chain: null, unread: [] }),
      work: () => ({ units: [...SHARED_UNITS, { ...SHARED_UNITS[0]!, unit: 'u-elsewhere', repository: 'game' }], recovery: [], unread: [], held: [] }),
    };

    it('reads it only once opened, by the session its landing names and its units by the branch, and draws it', async () => {
      answering(reads);
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: `Open ${LANDED}` }));
      const fold = await within(repositoryMain()).findByRole('button', { name: /^How it landed/ });
      expect(asked('TRACE')).toEqual([]);
      expect(asked('WORK_STATE')).toEqual([]);

      await userEvent.click(fold);
      const flow = await within(repositoryMain()).findByRole('list', { name: `How ${LANDED} landed` });
      expect(asked('TRACE').map(([, , request]) => request.payload)).toEqual([{ kind: 'session', id: 's-21' }]);
      expect(asked('WORK_STATE').map(([, , request]) => request.payload)).toEqual([{ unit: LANDED }]);
      expect(within(flow).getByRole('listitem', { name: /^Landed on review\/q6-asset-cache/ })).toBeInTheDocument();
      // The units are this repository's: the end says three, not the fourth another repository's unit named.
      expect(within(flow).getByRole('listitem', { name: /^End/ }).querySelectorAll('p')).toHaveLength(3);
    });

    it('offers nothing of it on a branch that is no landing\'s', async () => {
      answering(reads);
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Open daoris/engine-physics-step' }));
      await within(repositoryMain()).findByRole('heading', { level: 1, name: 'daoris/engine-physics-step' });
      expect(within(repositoryMain()).queryByRole('button', { name: /^How it landed/ })).toBeNull();
    });

    it('opens on a door\'s word with the section open, and lets the word go', async () => {
      answering(reads);
      const onBranchOpened = vi.fn();
      show(<ProjectsView notify={() => {}} door="engine" branchAsked={{ repository: 'engine', branch: LANDED }} onBranchOpened={onBranchOpened} />);
      expect(await screen.findByRole('heading', { level: 1, name: LANDED }, { timeout: 4000 })).toBeInTheDocument();
      expect(await within(repositoryMain()).findByRole('list', { name: `How ${LANDED} landed` })).toBeInTheDocument();
      expect(within(repositoryMain()).getByRole('button', { name: /^How it landed/ })).toHaveAttribute('aria-expanded', 'true');
      expect(onBranchOpened).toHaveBeenCalled();
      // Its way back is its repository's Branches, as a branch opened from the list.
      await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Back to Branches' }));
      expect(await within(repositoryMain()).findByRole('region', { name: "engine's branches" })).toBeInTheDocument();
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
    // Said once on the tab, not again under Updates (UX6h2b3).
    expect(screen.getAllByText(/with a checkout here holds? no branch of Daoris's/)).toHaveLength(1);
    await userEvent.click(within(repositoryMain()).getByText("1 repository with a checkout here holds no branch of Daoris's, and is not listed"));
    const apart = within(within(repositoryMain()).getByRole('group', { name: 'Repositories not listed' }));
    expect(apart.queryByLabelText('kiln')).toBeNull();
    await userEvent.click(apart.getByLabelText('game'));
    await userEvent.click(apart.getByRole('button', { name: 'Include (1)' }));

    const game = await within(repositoryMain()).findByRole('region', { name: "game's branches" });
    expect(within(game).getByRole('list', { name: 'Yours (2)' })).toBeInTheDocument();
    expect(asked('GIT_BRANCHES').map(([, , request]) => request.payload)).toEqual([{ workspace: 'aurora' }, { repository: 'game', workspace: 'aurora' }]);

    // One fold (UX6h2b3): Updates lists nothing apart of its own, and its look takes the game the fold included.
    const updates = within(screen.getByRole('region', { name: 'Updates' }));
    expect(updates.queryByText(/with a checkout here/)).toBeNull();
    await userEvent.click(updates.getByRole('button', { name: 'Look for updates' }));
    expect(asked('TREES_SYNC_PLAN').map(([, , request]) => request.payload)).toEqual([{ also: ['game'], workspace: 'aurora' }]);

    // Each group's heading is the door to its repository's page.
    await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'Open engine' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
  });

  /**
   * AUTOTIDY1a (D88): the tab shows what the look cleaned up in this workspace, read from the machine log with the two events,
   * the workspace and a week, and drawn after the repositories' groups and before the clean-up.
   */
  it("draws what was cleaned up on its own after the groups and before the clean-up, asked of the log for this workspace", async () => {
    const stamp = new Date(Date.now() - 2 * 3_600_000).toISOString();
    answering({
      log: () => ({
        lines: [
          { time: stamp, source: 'driver', level: 'info', event: 'branch.tidied', data: { repository: 'engine', workspace: 'aurora', branch: 'daoris/s-1f2e3d4c', tree: true, folder: 'trees/.tidied/aurora/engine/s-1' } },
          { time: stamp, source: 'driver', level: 'warn', event: 'branch.kept', data: { repository: 'engine', workspace: 'aurora', branch: 'daoris/s-9e0f1a2b', why: 'busy' } },
        ],
      }),
    });
    show(<ProjectsView notify={() => {}} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));

    const tidied = await within(repositoryMain()).findByRole('heading', { name: 'Cleaned up on its own' });
    const rows = within(tidied.closest('section')!).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('daoris/s-1f2e3d4c');
    expect(rows[0]).toHaveTextContent("Its tree's folder was set aside");
    expect(rows[1]).toHaveTextContent('in use');
    expect(invoke).toHaveBeenCalledWith('DAORIS.LOG', 'LINES', {
      payload: { events: ['branch.tidied', 'branch.kept'], workspace: 'aurora', since: '7d', limit: 50 },
    });
    const cleanUp = within(repositoryMain()).getByRole('heading', { name: 'Session branches' });
    expect(tidied.compareDocumentPosition(cleanUp) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('draws nothing of it where the log holds nothing', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    await chooseWorkspace('aurora');
    await userEvent.click(within(repositoryMain()).getByRole('tab', { name: 'Branches' }));
    await within(repositoryMain()).findByRole('heading', { name: 'Session branches' });
    expect(within(repositoryMain()).queryByRole('heading', { name: 'Cleaned up on its own' })).toBeNull();
  });
});

// GITAPP2b (D204, the git-writes design §2, §11): the switch *Daoris may change this checkout's files*, off by default in Setup's
// Reach, and the Changes tab beside Branches and History where it is on. The tab reads on open and on *Refresh* and writes nothing.
describe("a repository's Changes tab and the switch behind it (GITAPP2b)", () => {
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

  const switchOff = { ...DRIVER_STATE, checkoutWrites: [] as string[] };
  const switchOn = { ...DRIVER_STATE, checkoutWrites: ['engine'] };
  const answering = (
    state: Record<string, unknown> = switchOff, changes: () => unknown = () => answer({ ...GIT_CHANGES_ENGINE, readAt: new Date().toISOString() }),
    acts: Record<string, () => unknown> = {},
  ) =>
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (module === 'DAORIS.LOG') return { lines: [] };
      if (type === 'GIT_CHANGES') return changes();
      if (type in acts) return acts[type]!();
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'PLUGINS') return { plugins: [] };
      if (type === 'RULES') return { defaults: [], scopes: [] };
      if (type === 'ACROSS') return { repositories: [] };
      return state;
    });

  it('draws no Changes tab and reads nothing of the checkout while the switch is off, as it is by default', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'History', 'Integration']);
    expect(asked('GIT_CHANGES')).toEqual([]);
  });

  it('draws the Changes tab where the switch is on, reads it when it opens, and reads again on Refresh', async () => {
    answering(switchOn);
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Branches', 'History', 'Changes', 'Integration']);
    expect(asked('GIT_CHANGES')).toEqual([]);

    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));

    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });
    expect(within(region).getByRole('list', { name: 'Staged (2)' })).toBeInTheDocument();
    expect(within(region).getByRole('list', { name: 'Not staged (2)' })).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_CHANGES', expect.objectContaining({
      payload: expect.objectContaining({ repository: 'engine', workspace: 'aurora' }),
    }));
    const refresh = within(region).getByRole('button', { name: 'Refresh' });
    await waitFor(() => expect(refresh).toBeEnabled());
    const before = asked('GIT_CHANGES').length;

    await userEvent.click(refresh);

    await waitFor(() => expect(asked('GIT_CHANGES').length).toBeGreaterThan(before));
    // It only reads: no route that changes a repository is pressed.
    expect(invoke.mock.calls.map(([, called]) => called).filter((called) => /^(STAGE|UNSTAGE|COMMIT)/.test(String(called)))).toEqual([]);
  });

  // GITAPP2h: the presses of the tab reach the driver as one act each, with what the hunks were read against, and Undo takes the act.
  it('stages a file whole: reads its hunks for what to send back, presses GIT_STAGE, says it done and undoes it by its act', async () => {
    const budget = { ...GIT_HUNKS_BUDGET, hunks: [] };
    answering(switchOn, undefined, {
      GIT_HUNKS: () => answer(budget),
      GIT_STAGE: () => ({ done: true, refused: false, act: 'act-9', outcome: 'done', said: 'x' }),
      GIT_UNDO: () => ({ done: true, refused: false, act: 'act-10', outcome: 'done', said: 'x' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });
    const row = within(within(region).getByRole('list', { name: 'Not staged (2)' })).getAllByRole('listitem')[1]!;

    await userEvent.click(within(row).getByRole('button', { name: 'Stage' }));

    await waitFor(() => expect(asked('GIT_STAGE')).toHaveLength(1));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_STAGE', {
      payload: expect.objectContaining({
        repository: 'engine', workspace: 'aurora', path: 'src/budget.cs', head: budget.head, index: budget.index, source: budget.source, picks: [],
      }),
    });
    expect(await within(region).findByRole('status')).toHaveTextContent('Staged src/budget.cs.');

    await userEvent.click(within(region).getByRole('button', { name: 'Undo' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_UNDO', { payload: expect.objectContaining({ repository: 'engine', act: 'act-9' }) }));
    expect(await within(region).findByText('The index is put back as it was before that act.')).toBeInTheDocument();
  });

  it('stages a hunk from the file\'s changes, and says the driver\'s sentence where it refused', async () => {
    answering(switchOn, undefined, {
      GIT_HUNKS: () => answer(GIT_HUNKS_BUDGET),
      GIT_STAGE: () => ({ done: false, refused: true, said: '`src/budget.cs`\'s bytes changed since its hunks were read: nothing was changed.' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });
    const row = within(within(region).getByRole('list', { name: 'Not staged (2)' })).getAllByRole('listitem')[1]!;

    await userEvent.click(within(row).getByRole('button', { name: 'Show changes' }));
    await userEvent.click(await within(row).findByRole('button', { name: 'Stage this hunk' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_STAGE', {
      payload: expect.objectContaining({ path: 'src/budget.cs', index: GIT_HUNKS_BUDGET.index, picks: [{ id: 'a1b2c3d4e5f6' }] }),
    }));
    expect(await within(region).findByRole('status')).toHaveTextContent('bytes changed since its hunks were read: nothing was changed.');
    expect(within(region).queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('refuses a file whose index entry moved since the list was read, and presses nothing', async () => {
    answering(switchOn, undefined, { GIT_HUNKS: () => answer({ ...GIT_HUNKS_BUDGET, hunks: [], index: '100644 ' + '1'.repeat(40) }) });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });
    const row = within(within(region).getByRole('list', { name: 'Not staged (2)' })).getAllByRole('listitem')[1]!;

    await userEvent.click(within(row).getByRole('button', { name: 'Stage' }));

    expect(await within(region).findByRole('status')).toHaveTextContent('The file changed since this list was read: nothing was changed.');
    expect(asked('GIT_STAGE')).toEqual([]);
  });

  // GITAPP2i: the commit box reads the plan, presses with the tip and tree it showed, and says a hook's rewrite from the answer.
  it('commits the staged changes: reads the plan, presses GIT_MAKE_COMMIT with its tip and tree, and says what a hook changed', async () => {
    answering(switchOn, undefined, {
      GIT_COMMIT_PLAN: () => answer(GIT_COMMIT_PLAN_ENGINE),
      GIT_MAKE_COMMIT: () => ({ done: true, refused: false, act: 'act-11', outcome: 'done', said: 'x', notes: 'A hook changed what was committed: `a.txt` differ from what was staged.' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });

    await userEvent.type(await within(region).findByRole('textbox', { name: 'Message' }), 'Round the total');
    await userEvent.click(within(region).getByRole('button', { name: 'Commit' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_MAKE_COMMIT', {
      payload: expect.objectContaining({ repository: 'engine', workspace: 'aurora', message: 'Round the total', head: GIT_COMMIT_PLAN_ENGINE.head, tree: GIT_COMMIT_PLAN_ENGINE.tree }),
    }));
    expect(await within(region).findByRole('status')).toHaveTextContent('Committed. A hook changed what was committed');
    expect(within(region).queryByRole('button', { name: 'Undo' })).toBeNull();
    expect(within(region).getByRole('textbox', { name: 'Message' })).toHaveValue('');
  });

  it('amends through GIT_AMEND after asking for the amend\'s own plan, and says a refusal in the driver\'s sentence', async () => {
    answering(switchOn, undefined, {
      GIT_COMMIT_PLAN: () => answer(GIT_COMMIT_PLAN_ENGINE),
      GIT_AMEND: () => ({ done: false, refused: true, said: 'HEAD moved since the staged changes were read: nothing was changed.' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });

    await userEvent.click(await within(region).findByRole('checkbox', { name: 'Amend the last commit' }));
    await userEvent.click(within(region).getByRole('button', { name: 'Amend' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_COMMIT_PLAN', { payload: expect.objectContaining({ verb: 'amend' }) }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_AMEND', { payload: expect.objectContaining({ message: 'Round the budget total', head: GIT_COMMIT_PLAN_ENGINE.head }) }));
    expect(await within(region).findByRole('status')).toHaveTextContent('HEAD moved since the staged changes were read');
    expect(asked('GIT_MAKE_COMMIT')).toEqual([]);
  });

  // GITAPP2k: stash Save reads its plan and presses with the tip and the shared stash it showed.
  it('saves to the stash: reads the plan, presses GIT_STASH with the tip and the stash it showed, and names the stash saved', async () => {
    answering(switchOn, undefined, {
      GIT_STASH_PLAN: () => answer(GIT_STASH_PLAN_ENGINE),
      GIT_STASH: () => ({ done: true, refused: false, act: 'act-21', outcome: 'done', said: 'x', notes: 'Saved as stash `3fa91c0e2b7d` in `refs/stash`.' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });

    await userEvent.click(await within(region).findByRole('checkbox', { name: 'Include untracked files' }));
    await userEvent.click(within(region).getByRole('button', { name: 'Save to the stash' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_STASH', {
      payload: expect.objectContaining({ repository: 'engine', workspace: 'aurora', untracked: true, head: GIT_STASH_PLAN_ENGINE.head, stash: null }),
    }));
    expect(await within(region).findByRole('status')).toHaveTextContent('Saved as stash 3fa91c0e2b7d');
  });

  // GITAPP2l: stash Apply presses the stash's commit id, never its place on the list, with the tip the plan showed.
  it('applies a stash by its commit id with the tip the plan showed, and says the stash is kept', async () => {
    answering(switchOn, undefined, {
      GIT_STASH_PLAN: () => answer(GIT_STASH_PLAN_ENGINE),
      GIT_STASH_APPLY: () => ({ done: true, refused: false, act: 'act-22', outcome: 'done', said: 'x', notes: 'The stash is kept in `refs/stash` (`3fa91c0e2b7d`).' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });

    await userEvent.click(await within(region).findByRole('button', { name: 'Apply stash 3fa91c0e2b7d' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_STASH_APPLY', {
      payload: expect.objectContaining({ repository: 'engine', workspace: 'aurora', id: GIT_STASH_PLAN_ENGINE.entries[0]!.id, head: GIT_STASH_PLAN_ENGINE.head }),
    }));
    expect(await within(region).findByRole('status')).toHaveTextContent('The stash is kept in refs/stash');
  });

  // GITAPP2k: switch reads the plan of the branch chosen and presses with the branch HEAD named and its commit.
  it('switches: chooses a local branch, reads its plan, presses GIT_SWITCH, and says a refusal in the driver\'s sentence', async () => {
    answering(switchOn, undefined, {
      GIT_BRANCHES: () => ({ repositories: [{ ...GIT_BRANCHES_ENGINE, workspace: 'aurora' }], apart: [], sessionsUnread: false }),
      GIT_SWITCH_PLAN: () => answer(GIT_SWITCH_PLAN_ENGINE),
      GIT_SWITCH: () => ({ done: false, refused: true, said: 'tracked files are changed here: nothing was changed.' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });

    await userEvent.click(await within(region).findByRole('combobox', { name: 'Branch to switch to' }));
    await userEvent.click(await screen.findByRole('option', { name: 'try/ecs' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_SWITCH_PLAN', { payload: expect.objectContaining({ branch: 'try/ecs' }) }));
    await userEvent.click(await within(region).findByRole('button', { name: 'Switch' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_SWITCH', {
      payload: expect.objectContaining({ repository: 'engine', branch: 'try/ecs', from: 'main', head: GIT_SWITCH_PLAN_ENGINE.head }),
    }));
    expect(await within(region).findByRole('status')).toHaveTextContent('tracked files are changed here');
  });

  it("shows the read the shell held as read again until a fresh one lands, and does not offer Refresh meanwhile", async () => {
    // The shell answers what it held at once: a read made before this window asked, with its own time.
    answering(switchOn, () => answer({ ...GIT_CHANGES_ENGINE, readAt: new Date(Date.now() - 3_600_000).toISOString() }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));

    const region = await within(repositoryMain()).findByRole('region', { name: "engine's changes" });
    expect(within(region).getByText('Reading again…')).toBeInTheDocument();
    expect(within(region).getByRole('button', { name: 'Refresh' })).toBeDisabled();
  });

  it("says this machine has none for a repository with no checkout, and why git did not answer", async () => {
    answering({ ...DRIVER_STATE, checkoutWrites: ['game'] });
    show(<ProjectsView notify={() => {}} />);
    const game = await chooseRepository('game');
    await userEvent.click(within(game).getByRole('tab', { name: 'Changes' }));
    expect(await within(repositoryMain()).findByText("game's changes are read from its checkout, and this machine has none.")).toBeInTheDocument();
    expect(asked('GIT_CHANGES')).toEqual([]);
  });

  it('says the sentence of a read git did not answer, in its place', async () => {
    answering(switchOn, () => ({ ...answer(null, 'GIT_NOT_ANSWERED'), problem: 'git could not read this checkout.' }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Changes' }));
    expect(await within(repositoryMain()).findByText(/Git could not|git could not read this checkout/i)).toBeInTheDocument();
  });
});

// GITAPP2c (D204, the git-writes design §6, §9): New branch and New tag on a repository's Branches. The person names it, sees what will
// be made, and confirms; only then does the press go to the driver, which answers it as data. The undo deletes the ref at the id it made.
describe("a repository's New branch and New tag (GITAPP2c)", () => {
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

  const answering = (acts: Record<string, () => unknown> = {}) =>
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (module === 'DAORIS.LOG') return { lines: [] };
      if (type === 'GIT_BRANCHES') return ENGINE_LIST;
      if (acts[type]) return acts[type]!();
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'PLUGINS') return { plugins: [] };
      if (type === 'RULES') return { defaults: [], scopes: [] };
      if (type === 'ACROSS') return { repositories: [] };
      return DRIVER_STATE;
    });

  const openForm = async () => {
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    await within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    await userEvent.click(within(repositoryMain()).getByRole('button', { name: 'New branch or tag…' }));
    return within(repositoryMain()).getByRole('group', { name: 'New branch or tag' });
  };

  it('names what it will make and sends nothing until Confirm, then presses the create at the line commit', async () => {
    answering({ GIT_REF_CREATE: () => ({ act: 'a1', outcome: 'done', refused: false, said: 'created' }) });
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'topic');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));

    expect(within(form).getByText(/Make the branch topic at main/)).toBeInTheDocument();
    expect(asked('GIT_REF_CREATE')).toEqual([]);
    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    expect(await within(form).findByText('Made the branch topic.')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_CREATE', {
      payload: { repository: 'engine', workspace: 'aurora', kind: 'branch', name: 'topic', at: LINE_COMMIT },
    });
  });

  it('makes a lightweight tag when asked for one, and says so in the confirmation', async () => {
    answering({ GIT_REF_CREATE: () => ({ act: 'a2', outcome: 'done', refused: false, said: 'created' }) });
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    await userEvent.click(within(form).getByRole('radio', { name: 'Tag' }));
    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'v1');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));
    expect(within(form).getByText(/Make the lightweight tag v1 at main/)).toBeInTheDocument();
    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    await within(form).findByText('Made the tag v1.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_CREATE', expect.objectContaining({ payload: expect.objectContaining({ kind: 'tag', name: 'v1' }) }));
  });

  it('goes back from the confirmation without sending, and asks no name that is empty', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    expect(within(form).getByRole('button', { name: 'Create…' })).toBeDisabled();
    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'topic');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Back' }));

    expect(within(form).getByRole('textbox', { name: 'Name' })).toHaveValue('topic');
    expect(asked('GIT_REF_CREATE')).toEqual([]);
  });

  it("says a refusal in the driver's sentence and keeps the name to change", async () => {
    answering({ GIT_REF_CREATE: () => ({ refused: true, outcome: null, said: 'a branch named `topic` already exists, so nothing was created' }) });
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'topic');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    expect(await within(form).findByText(/a branch named .topic. already exists/)).toBeInTheDocument();
    expect(within(form).getByRole('textbox', { name: 'Name' })).toHaveValue('topic');
  });

  it('undoes a create at the act it made, and says the ref is gone', async () => {
    answering({
      GIT_REF_CREATE: () => ({ act: 'a1', outcome: 'done', refused: false, said: 'created' }),
      GIT_REF_UNDO: () => ({ act: 'a3', outcome: 'done', refused: false, said: 'deleted' }),
    });
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();
    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'topic');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    await userEvent.click(await within(form).findByRole('button', { name: 'Undo' }));

    expect(await within(form).findByText('Undone: the branch topic is gone.')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_UNDO', { payload: { repository: 'engine', workspace: 'aurora', act: 'a1' } });
  });

  it('says what it could not tell where the outcome is unknown, and offers no undo', async () => {
    answering({ GIT_REF_CREATE: () => ({ act: 'a4', outcome: 'unknown', refused: false, said: 'its processes did not end' }) });
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();
    await userEvent.type(within(form).getByRole('textbox', { name: 'Name' }), 'topic');
    await userEvent.click(within(form).getByRole('button', { name: 'Create…' }));
    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    expect(await within(form).findByText(/Daoris could not tell whether this finished; nothing was retried/)).toBeInTheDocument();
    expect(within(form).queryByRole('button', { name: 'Undo' })).toBeNull();
  });

  it('draws no New branch or tag for a repository with no checkout here', async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const game = await chooseRepository('game');
    await userEvent.click(within(game).getByRole('tab', { name: 'Branches' }));
    await within(repositoryMain()).findByText(/this machine has none/);
    expect(within(repositoryMain()).queryByRole('button', { name: 'New branch or tag…' })).toBeNull();
  });

  describe('Delete (GITAPP2d)', () => {
    const SPIKE = GIT_BRANCHES_ENGINE.branches.find((branch) => branch.name === 'spike/renderer')!.commit;

    const openBranches = async () => {
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      return within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    };

    it('names what goes and where its commits stay, sends nothing until Delete, then presses the delete at the commit the row showed', async () => {
      answering({ GIT_REF_DELETE: () => ({ act: 'd1', outcome: 'done', refused: false, said: 'deleted' }) });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      await userEvent.click(within(list).getByRole('button', { name: 'Delete the branch spike/renderer' }));
      const group = within(list).getByRole('group', { name: 'Delete the branch spike/renderer' });

      expect(within(group).getByText(/Delete the branch spike\/renderer \(5d2e770\)\? Its commits stay on a recovery ref/)).toBeInTheDocument();
      expect(asked('GIT_REF_DELETE')).toEqual([]);
      await userEvent.click(within(group).getByRole('button', { name: 'Delete' }));

      expect(await within(list).findByText('Deleted the branch spike/renderer. Its commits are kept for recovery.')).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_DELETE', {
        payload: { repository: 'engine', workspace: 'aurora', kind: 'branch', name: 'spike/renderer', at: SPIKE },
      });
    });

    it('keeps its Undo after the row is gone and undoes at the act the delete made', async () => {
      answering({
        GIT_REF_DELETE: () => ({ act: 'd1', outcome: 'done', refused: false, said: 'deleted' }),
        GIT_REF_UNDO: () => ({ act: 'u1', outcome: 'done', refused: false, said: 'made again' }),
      });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();
      await userEvent.click(within(list).getByRole('button', { name: 'Delete the branch fix/readme' }));
      await userEvent.click(within(within(list).getByRole('group', { name: 'Delete the branch fix/readme' })).getByRole('button', { name: 'Delete' }));

      await userEvent.click(await within(list).findByRole('button', { name: 'Undo' }));

      expect(await within(list).findByText('Undone: the branch fix/readme is back.')).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_UNDO', { payload: { repository: 'engine', workspace: 'aurora', act: 'd1' } });
    });

    it("says a refusal in the driver's sentence and sends nothing more", async () => {
      answering({ GIT_REF_DELETE: () => ({ refused: true, outcome: null, said: '`spike/renderer` is checked out in `engine-tree`, so nothing was deleted' }) });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();
      await userEvent.click(within(list).getByRole('button', { name: 'Delete the branch spike/renderer' }));
      await userEvent.click(within(within(list).getByRole('group', { name: 'Delete the branch spike/renderer' })).getByRole('button', { name: 'Delete' }));

      expect(await within(list).findByText(/is checked out in .engine-tree., so nothing was deleted/)).toBeInTheDocument();
      expect(within(list).queryByRole('button', { name: 'Undo' })).toBeNull();
    });

    it('goes back without sending, and is drawn on no line, session or landed row', async () => {
      answering();
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      await userEvent.click(within(list).getByRole('button', { name: 'Delete the branch spike/renderer' }));
      await userEvent.click(within(within(list).getByRole('group', { name: 'Delete the branch spike/renderer' })).getByRole('button', { name: 'Cancel' }));

      expect(asked('GIT_REF_DELETE')).toEqual([]);
      expect(within(list).getByRole('button', { name: 'Delete the branch spike/renderer' })).toBeInTheDocument();
      for (const held of ['main', 'daoris/engine-physics-step', 'review/q6-asset-cache']) {
        expect(within(list).queryByRole('button', { name: `Delete the branch ${held}` })).toBeNull();
      }
    });
  });

  describe('Rename (GITAPP2e)', () => {
    const SPIKE = GIT_BRANCHES_ENGINE.branches.find((branch) => branch.name === 'spike/renderer')!.commit;

    const openBranches = async () => {
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      return within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    };

    const rename = async (list: HTMLElement, to: string) => {
      await userEvent.click(within(list).getByRole('button', { name: 'Rename the branch spike/renderer' }));
      const group = within(list).getByRole('group', { name: 'Rename the branch spike/renderer' });
      await userEvent.clear(within(group).getByRole('textbox', { name: 'New name' }));
      await userEvent.type(within(group).getByRole('textbox', { name: 'New name' }), to);
      await userEvent.click(within(group).getByRole('button', { name: 'Continue' }));
      return group;
    };

    it('asks the new name, says what happens, sends nothing until Rename, then presses at the commit the row showed', async () => {
      answering({ GIT_REF_RENAME: () => ({ act: 'r1', outcome: 'done', refused: false, said: 'renamed' }) });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      const group = await rename(list, 'spike/renderer-2');

      expect(within(group).getByText(/Rename the branch spike\/renderer \(5d2e770\) to spike\/renderer-2\? Its old tip stays on a recovery ref/)).toBeInTheDocument();
      expect(asked('GIT_REF_RENAME')).toEqual([]);
      await userEvent.click(within(group).getByRole('button', { name: 'Rename' }));

      expect(await within(list).findByText(/Renamed the branch spike\/renderer to spike\/renderer-2\. spike\/renderer-2 starts a new reflog/)).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_RENAME', {
        payload: { repository: 'engine', workspace: 'aurora', kind: 'branch', name: 'spike/renderer', to: 'spike/renderer-2', at: SPIKE },
      });
    });

    it('undoes a done rename at its act', async () => {
      answering({
        GIT_REF_RENAME: () => ({ act: 'r1', outcome: 'done', refused: false, said: 'renamed' }),
        GIT_REF_UNDO: () => ({ act: 'u1', outcome: 'done', refused: false, said: 'moved back' }),
      });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();
      await userEvent.click(within(await rename(list, 'spike/renderer-2')).getByRole('button', { name: 'Rename' }));

      expect(within(list).queryByRole('button', { name: 'Finish' })).toBeNull();
      await userEvent.click(await within(list).findByRole('button', { name: 'Undo' }));

      expect(await within(list).findByText('Undone: the branch spike/renderer-2 is spike/renderer again.')).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_UNDO', { payload: { repository: 'engine', workspace: 'aurora', act: 'r1' } });
    });

    it('offers Finish and Undo for a rename that stopped before its settings moved, and Finish names the act', async () => {
      answering({
        GIT_REF_RENAME: () => ({ act: 'r1', outcome: 'partial', refused: false, said: 'part of it stands' }),
        GIT_REF_FINISH: () => ({ act: 'f1', outcome: 'done', refused: false, said: 'finished' }),
      });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();
      await userEvent.click(within(await rename(list, 'spike/renderer-2')).getByRole('button', { name: 'Rename' }));

      expect(await within(list).findByText(/but its settings \(its upstream and the like\) did not move/)).toBeInTheDocument();
      expect(within(list).getByRole('button', { name: 'Undo' })).toBeInTheDocument();
      await userEvent.click(within(list).getByRole('button', { name: 'Finish' }));

      expect(await within(list).findByText(/Renamed the branch spike\/renderer to spike\/renderer-2\. spike\/renderer-2 starts a new reflog/)).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_REF_FINISH', { payload: { repository: 'engine', workspace: 'aurora', act: 'r1' } });
      expect(within(list).queryByRole('button', { name: 'Finish' })).toBeNull();
    });

    it("says a refusal in the driver's sentence with the name kept, and leaves a stopped rename's presses to try the other", async () => {
      answering({ GIT_REF_RENAME: () => ({ refused: true, outcome: null, said: 'a branch named `spike/physics` already exists, so nothing was renamed' }) });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      await userEvent.click(within(await rename(list, 'spike/physics')).getByRole('button', { name: 'Rename' }));

      expect(await within(list).findByText(/already exists, so nothing was renamed/)).toBeInTheDocument();
      expect(within(list).getByRole('textbox', { name: 'New name' })).toHaveValue('spike/physics');
      expect(within(list).queryByRole('button', { name: 'Undo' })).toBeNull();
    });

    it('goes back without sending, will not rename to the same name, and is drawn on no line, session or landed row', async () => {
      answering();
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      await userEvent.click(within(list).getByRole('button', { name: 'Rename the branch spike/renderer' }));
      const group = within(list).getByRole('group', { name: 'Rename the branch spike/renderer' });
      expect(within(group).getByRole('button', { name: 'Continue' })).toBeDisabled();
      await userEvent.click(within(group).getByRole('button', { name: 'Cancel' }));

      expect(asked('GIT_REF_RENAME')).toEqual([]);
      for (const held of ['main', 'daoris/engine-physics-step', 'review/q6-asset-cache']) {
        expect(within(list).queryByRole('button', { name: `Rename the branch ${held}` })).toBeNull();
      }
    });
  });

  describe('Fetch origin (GITAPP2f)', () => {
    const offered = () => answer({ offered: true, reason: null, why: null, address: 'https://example.test/engine.git', command: 'git fetch' });
    const refused = (reason: string, why: string) => () => answer({ offered: false, reason, why, address: null, command: 'git fetch' });

    const openBranches = async () => {
      const page = await chooseRepository('engine');
      await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
      return within(repositoryMain()).findByRole('region', { name: "engine's branches" });
    };

    it('offers Fetch once the driver says so, names what moves, sends nothing until Fetch, then says what was made, moved and removed', async () => {
      answering({
        GIT_FETCH_PLAN: offered,
        GIT_FETCH: () => ({ act: 'f1', outcome: 'done', refused: false, said: 'act f1', notes: 'Moved `origin/main`. Removed `origin/old`, which origin no longer has.' }),
      });
      show(<ProjectsView notify={() => {}} />);
      const list = await openBranches();

      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Fetch origin…' }));
      const group = within(repositoryMain()).getByRole('group', { name: 'Fetch origin' });
      expect(within(group).getByText(/Fetch origin’s branches from https:\/\/example.test\/engine.git\? Only the origin\/… branches here move/)).toBeInTheDocument();
      expect(asked('GIT_FETCH')).toEqual([]);
      await userEvent.click(within(group).getByRole('button', { name: 'Fetch' }));

      expect(await within(group).findByText(/Fetched origin’s branches\. Moved .origin\/main.\. Removed .origin\/old./)).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_FETCH', { payload: { repository: 'engine', workspace: 'aurora' } });
      expect(list).toBeInTheDocument();
    });

    it("says a refusal in the driver's sentence, and a fetch that did nothing as nothing fetched", async () => {
      answering({ GIT_FETCH_PLAN: offered, GIT_FETCH: () => ({ refused: true, outcome: null, said: 'A hold on origin, so nothing was fetched.' }) });
      show(<ProjectsView notify={() => {}} />);
      await openBranches();
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Fetch origin…' }));
      await userEvent.click(within(within(repositoryMain()).getByRole('group', { name: 'Fetch origin' })).getByRole('button', { name: 'Fetch' }));

      expect(await within(repositoryMain()).findByText('A hold on origin, so nothing was fetched.')).toBeInTheDocument();
    });

    it('goes back without sending', async () => {
      answering({ GIT_FETCH_PLAN: offered });
      show(<ProjectsView notify={() => {}} />);
      await openBranches();
      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Fetch origin…' }));
      await userEvent.click(within(within(repositoryMain()).getByRole('group', { name: 'Fetch origin' })).getByRole('button', { name: 'Cancel' }));

      expect(asked('GIT_FETCH')).toEqual([]);
      expect(within(repositoryMain()).getByRole('button', { name: 'Fetch origin…' })).toBeInTheDocument();
    });

    it('draws nothing where the git doors are not installed, and says why only where origin is set to run another program', async () => {
      answering({ GIT_FETCH_PLAN: refused('doors', "Daoris's git doors are not installed in this Daoris") });
      show(<ProjectsView notify={() => {}} />);
      await openBranches();
      await waitFor(() => expect(asked('GIT_FETCH_PLAN')).not.toEqual([]));
      expect(within(repositoryMain()).queryByRole('button', { name: 'Fetch origin…' })).toBeNull();
      expect(within(repositoryMain()).queryByText(/Fetch is not offered here/)).toBeNull();
    });

    it('says a setting of origin that runs another program refuses it', async () => {
      answering({ GIT_FETCH_PLAN: refused('widened', '`remote.origin.uploadpack` is set, which would run another program') });
      show(<ProjectsView notify={() => {}} />);
      await openBranches();

      expect(await within(repositoryMain()).findByText(/Fetch is not offered here: .remote\.origin\.uploadpack. is set/)).toBeInTheDocument();
      expect(within(repositoryMain()).queryByRole('button', { name: 'Fetch origin…' })).toBeNull();
    });
  });
});

// GITAPP2m (D204, the git-writes design §7, §9): Edit history on a branch of the person's own. The page asks the driver's plan first
// (`plan: true`, which changes nothing) and shows the commits it takes; only Confirm sends the edit, carrying the ids the plan pinned.
// A refusal stays in the form in the driver's sentence; an unknown outcome is said and never retried.
describe("a branch's Edit history (GITAPP2m)", () => {
  const MINE = 'spike/renderer';
  const MINE_COMMIT = GIT_BRANCHES_ENGINE.branches.find((branch) => branch.name === MINE)!.commit!;
  const PLAN = {
    edit: 'rebase', branch: MINE, tip: MINE_COMMIT, onto: LINE_COMMIT, commands: [`git rebase main ${MINE}`],
    commits: [{ id: 'aa11bb22cc33dd44ee55ff6677889900aabbccdd', subject: 'Try a forward renderer' }],
  };

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

  const answering = (edit?: (payload: Record<string, unknown>) => unknown) =>
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: Record<string, unknown> }) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (module === 'DAORIS.LOG') return { lines: [] };
      if (type === 'GIT_BRANCHES') return ENGINE_LIST;
      if (type === 'GIT_GRAPH') return answer(PAGE);
      if (type === 'GIT_HISTORY_EDIT' && edit) return edit(request?.payload ?? {});
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'PLUGINS') return { plugins: [] };
      if (type === 'RULES') return { defaults: [], scopes: [] };
      if (type === 'ACROSS') return { repositories: [] };
      return DRIVER_STATE;
    });

  const openForm = async (branch = MINE) => {
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    await userEvent.click(await within(repositoryMain()).findByRole('button', { name: `Open ${branch}` }));
    await within(repositoryMain()).findByRole('heading', { level: 1, name: branch });
    await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Edit history…' }));
    return within(repositoryMain()).getByRole('group', { name: 'Edit history' });
  };

  it('asks the plan first, shows the commits it takes, and sends the edit only on Confirm with the plan pinned', async () => {
    answering((payload) => (payload.plan
      ? { refused: false, said: '', plan: PLAN }
      : { act: 'h1', outcome: 'done', refused: false, said: '', tip: 'bb22cc33dd44ee55ff6677889900aabbccddeeff', kept: `refs/daoris/discarded/${MINE}` }));
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    expect(within(form).getByRole('textbox', { name: 'Commit or branch to rebase onto' })).toHaveValue('main');
    await userEvent.click(within(form).getByRole('button', { name: 'Show the plan…' }));

    expect(await within(form).findByText(/Rebase spike\/renderer onto main/)).toBeInTheDocument();
    expect(within(form).getByText('Try a forward renderer')).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_HISTORY_EDIT', {
      payload: { repository: 'engine', workspace: 'aurora', edit: 'rebase', branch: MINE, target: 'main', plan: true },
    });
    expect(asked('GIT_HISTORY_EDIT')).toHaveLength(1);

    await userEvent.click(within(form).getByRole('button', { name: 'Confirm' }));

    expect(await within(form).findByText(/spike\/renderer now stands at bb22cc3/)).toBeInTheDocument();
    expect(within(form).getByText(/refs\/daoris\/discarded\/spike\/renderer/)).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_HISTORY_EDIT', {
      payload: {
        repository: 'engine', workspace: 'aurora', edit: 'rebase', branch: MINE, target: 'main',
        pins: { tip: PLAN.tip, onto: PLAN.onto, commits: [PLAN.commits[0]!.id] },
      },
    });
  });

  it('picks the commit typed, and says a plan the driver refuses in its sentence with the words kept', async () => {
    answering(() => ({ refused: true, said: 'aa11bb22cc33 (Fix) is a merge, and a history edit takes one line of non-merge commits, so nothing was changed' }));
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();

    await userEvent.click(within(form).getByRole('radio', { name: 'Pick' }));
    await userEvent.type(within(form).getByRole('textbox', { name: 'Commit or A..B range to pick' }), 'aa11bb2');
    await userEvent.click(within(form).getByRole('button', { name: 'Show the plan…' }));

    expect(await within(form).findByText(/is a merge, and a history edit takes one line/)).toBeInTheDocument();
    expect(within(form).getByRole('textbox', { name: 'Commit or A..B range to pick' })).toHaveValue('aa11bb2');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'GIT_HISTORY_EDIT', expect.objectContaining({
      payload: expect.objectContaining({ edit: 'pick', target: 'aa11bb2', plan: true }),
    }));
    expect(within(form).queryByRole('button', { name: 'Confirm' })).toBeNull();
  });

  it('says what it could not tell where the outcome is unknown', async () => {
    answering((payload) => (payload.plan ? { refused: false, said: '', plan: PLAN } : { act: 'h2', outcome: 'unknown', refused: false, said: 'its processes did not end' }));
    show(<ProjectsView notify={() => {}} />);
    const form = await openForm();
    await userEvent.click(within(form).getByRole('button', { name: 'Show the plan…' }));
    await userEvent.click(await within(form).findByRole('button', { name: 'Confirm' }));

    expect(await within(form).findByText(/Daoris could not tell whether this finished; nothing was retried/)).toBeInTheDocument();
  });

  it("offers no Edit history on a session's branch", async () => {
    answering();
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Branches' }));
    await userEvent.click(await within(repositoryMain()).findByRole('button', { name: 'Open daoris/engine-physics-step' }));
    await within(repositoryMain()).findByRole('heading', { level: 1, name: 'daoris/engine-physics-step' });

    expect(within(repositoryMain()).queryByRole('button', { name: 'Edit history…' })).toBeNull();
  });
});
