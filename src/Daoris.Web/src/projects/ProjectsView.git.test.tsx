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

import type { GitBranchList, GitGraphPage } from '../bridge/repositories/git';
import {
  GIT_BRANCH_PARTED, GIT_BRANCH_PARTED_PAGES, GIT_BRANCHES_ENGINE, GIT_BRANCHES_GAME, GIT_HISTORY_ENGINE, GIT_HISTORY_PAGES,
} from './fixtures';
import { SHARED_BRANCH, SHARED_DUE, SHARED_UNITS, traceAnswerOf } from './git/landingFixtures';
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
      trace: (payload: Record<string, unknown>) => (payload.id === 's-21' ? traceAnswerOf('s-21', landed, SHARED_DUE) : { chain: null, unread: [] }),
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
