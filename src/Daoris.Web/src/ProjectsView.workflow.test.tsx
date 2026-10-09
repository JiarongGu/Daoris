import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Workflow tab in SHELL mode (WORKFLOW1b; D157 point 12, the workflow design §6.1): a repository's page and a
// workspace's ask this machine's driver for their Current over DAORIS.DRIVER's `WORKFLOW_CURRENT`, only while the tab
// shows, and each door opens the Setup that sets a step. Over a mocked bridge, as the shell-attached half is held
// (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { DRIVER_STATE, respond, show, WIRING } from './test/shellHarness';
import { chooseRepository, chooseWorkspace, ProjectsView, repositoryMain } from './test/projectsView';
import { FULL, WORKSPACE } from './workflow/fixtures';

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

/** The workspace's rules as the driver's state answers them, so its Setup opens its defaults where a rule is set there. */
const STATE = {
  ...DRIVER_STATE,
  workspaceLandings: [{ workspace: 'aurora', form: 'branch', pattern: 'work/{quest}' }],
  workspaceReviews: [],
  workspaceOpinions: [],
};

const asked = (type: string) => invoke.mock.calls.filter(([, called]) => called === type);

describe('the Workflow tab', () => {
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

  const answering = (current: (payload: { repository?: string; workspace?: string }) => unknown) =>
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: Record<string, string> }) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (type === 'WORKFLOW_CURRENT') {
        const answer = current(options?.payload ?? {});
        if (answer instanceof Error) throw answer;
        return answer;
      }
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      if (type === 'HARNESSES') return { harnesses: [] };
      if (type === 'ACCOUNTS') return { agents: [] };
      if (type === 'STARTS') return { starts: [] };
      if (type === 'PLUGINS') return { plugins: [] };
      if (type === 'RULES') return { defaults: [], scopes: [] };
      if (type === 'ACROSS') return { repositories: [] };
      return STATE;
    });

  it("asks for a repository's Current only once its tab shows, and draws it", async () => {
    answering(() => FULL);
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    expect(asked('WORKFLOW_CURRENT')).toEqual([]);

    await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));

    expect(await within(repositoryMain()).findByRole('heading', { name: 'Current workflow' })).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_CURRENT', expect.objectContaining({ payload: { repository: 'engine' } }));
    expect(within(repositoryMain()).getByRole('list', { name: 'Before it lands' })).toBeInTheDocument();
  });

  it("opens the workspace's page at its Setup from a step its workspace set", async () => {
    answering(() => FULL);
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));

    const opinion = await waitFor(() => within(repositoryMain()).getAllByRole('listitem')
      .find((item) => item.getAttribute('data-step') === 'opinion')!);
    await userEvent.click(within(opinion).getByRole('button', { name: 'Open workspace Setup' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
    expect(within(repositoryMain()).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
    expect(await within(repositoryMain()).findByRole('button', { name: 'Defaults' })).toHaveAttribute('aria-expanded', 'true');
  });

  it("says a refusal in the driver's words, in the tab's place", async () => {
    answering(() => new Error('the driver is still coming up — try again in a moment.'));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');
    await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));

    expect(await within(repositoryMain()).findByText('the driver is still coming up — try again in a moment.')).toBeInTheDocument();
  });

  it("draws a workspace's Current, opens its own Setup from a step, and a repository with its own rules at its own", async () => {
    answering((payload) => (payload.workspace ? WORKSPACE : FULL));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseWorkspace('aurora');

    await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));
    expect(await within(repositoryMain()).findByText(/How work moves in each repository of aurora/)).toBeInTheDocument();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_CURRENT', expect.objectContaining({ payload: { workspace: 'aurora' } }));

    // A repository that sets its own rules opens at its own Workflow tab.
    await userEvent.click(within(repositoryMain()).getByRole('button', { name: "Open engine's workflow" }));
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
    expect(within(repositoryMain()).getByRole('tab', { name: 'Workflow' })).toHaveAttribute('aria-selected', 'true');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_CURRENT', expect.objectContaining({ payload: { repository: 'engine' } })));
  });

  it("opens a workspace's Setup at its defaults from a step its rules set", async () => {
    answering(() => WORKSPACE);
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseWorkspace('aurora');
    await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));

    const landing = await waitFor(() => within(repositoryMain()).getAllByRole('listitem')
      .find((item) => item.getAttribute('data-step') === 'landing')!);
    await userEvent.click(within(landing).getByRole('button', { name: 'Change in Setup' }));

    expect(within(repositoryMain()).getByRole('tab', { name: 'Setup' })).toHaveAttribute('aria-selected', 'true');
    expect(await within(repositoryMain()).findByRole('button', { name: 'Defaults' })).toHaveAttribute('aria-expanded', 'true');
  });
});
