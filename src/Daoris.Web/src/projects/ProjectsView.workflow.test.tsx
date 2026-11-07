import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Workflow tab in SHELL mode: a repository's page and a
// workspace's ask this machine's driver for their Current over DAORIS.DRIVER's `WORKFLOW_CURRENT`, only while the tab
// shows, and each door opens the Setup that sets a step. Over a mocked bridge, as the shell-attached half is held
// (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: () => undefined,
}));

import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { chooseRepository, chooseWorkspace, ProjectsView, repositoryMain } from '../test/projectsView';
import { FULL, WORKSPACE } from '../workflow/fixtures';

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

/** The workspace's rules as the driver's state answers them, so its Setup opens its defaults where a rule is set there. */
const STATE = {
  ...DRIVER_STATE,
  workspaceLines: [{ workspace: 'aurora', branch: 'develop' }],
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

  // A repository's Workflow is the Agents place's page (the Workflow section), so the tab is not drawn, its Current is not
  // asked here, and a workspace's door to a repository's own workflow opens that place.
  describe('the place holds the Workflow', () => {
    it("draws no Workflow tab on a repository's page and asks no Current for it", async () => {
      answering(() => FULL);
      show(<ProjectsView notify={() => {}} />);
      const page = await chooseRepository('engine');

      expect(within(page).queryByRole('tab', { name: 'Workflow' })).toBeNull();
      expect(asked('WORKFLOW_CURRENT')).toEqual([]);
    });

    it("opens the repository's place, not its tab, from a workspace's door to a repository with rules of its own", async () => {
      const onOpenSetup = vi.fn();
      answering((payload) => (payload.workspace ? WORKSPACE : FULL));
      show(<ProjectsView notify={() => {}} onOpenSetup={onOpenSetup} />);
      const page = await chooseWorkspace('aurora');
      await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));

      await userEvent.click(await within(repositoryMain()).findByRole('button', { name: "Open engine's workflow" }));

      expect(onOpenSetup).toHaveBeenCalledWith('engine');
      expect(asked('WORKFLOW_CURRENT').filter(([, , options]) => options?.payload?.repository === 'engine')).toEqual([]);
    });

    // The defaults a step of the workspace's Current names are the place's Workflow rows, so the door asks the application, not the tab.
    it("opens the workspace's defaults at their new home from a step its rules set, and keeps the tab on Workflow", async () => {
      const onOpenWorkspaceSetup = vi.fn();
      answering(() => WORKSPACE);
      show(<ProjectsView notify={() => {}} onOpenWorkspaceSetup={onOpenWorkspaceSetup} />);
      const page = await chooseWorkspace('aurora');
      await userEvent.click(within(page).getByRole('tab', { name: 'Workflow' }));
      const landing = await waitFor(() => within(repositoryMain()).getAllByRole('listitem')
        .find((item) => item.getAttribute('data-step') === 'landing')!);
      await userEvent.click(within(landing).getByRole('button', { name: 'Change in Setup' }));

      expect(onOpenWorkspaceSetup).toHaveBeenCalledWith('aurora', 'defaults');
      expect(within(repositoryMain()).getByRole('tab', { name: 'Workflow' })).toHaveAttribute('aria-selected', 'true');
    });
  });
});
