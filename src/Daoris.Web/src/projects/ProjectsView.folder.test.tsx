import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { waitFor, within } from '@testing-library/react';

// A folder registered inside a repository has no git of its own: its page draws Details, Files and Workflow and asks git nothing.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => ({
  ...(await import('../test/sliceShell')).mockedShell(invoke, { notifyReady }),
  useShenoraEvent: () => undefined,
}));

import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { chooseRepository, ProjectsView } from '../test/projectsView';

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora', root: 'C:/somewhere/engine' },
  {
    repository: 'notes', adopted: false, registered: true, owns: [], accepts: [], packs: [], entries: 0, workspace: 'aurora',
    root: 'C:/somewhere/engine/notes', kind: 'folder',
  },
];

const asked = (type: string) => invoke.mock.calls.filter(([, called]) => called === type);

describe("a folder repository's page", () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(REGISTRY) : respond(url);
    }));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module === 'DAORIS.REMOTES') return WIRING;
      if (module === 'DAORIS.LOG') return { lines: [] };
      if (type === 'LINES') return { lines: [], landings: [], reviews: [], opinions: [] };
      return DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    window.localStorage.clear();
  });

  it('draws Details, Files and Workflow alone, and asks git for no branch, history or change', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('notes');

    await waitFor(() => expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['Details', 'Files', 'Workflow']));
    for (const type of ['GIT_BRANCHES', 'GIT_GRAPH', 'GIT_CHANGES']) expect(asked(type)).toEqual([]);
  });

  it('draws a git checkout beside it with its Branches and History tabs', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await waitFor(() => expect(within(page).getAllByRole('tab').map((tab) => tab.textContent)).toContain('Branches'));
  });
});
