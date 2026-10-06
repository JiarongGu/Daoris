import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// A workspace's *Kept on this machine* in SHELL mode (HIST1e, D153; the history-clearing design §2.4, §5, §6.1): its Details
// asks this machine's driver what the home keeps of its finished work, and *Clear history…* sends over DAORIS.DRIVER
// exactly the units its first press listed. Over a mocked bridge, as the shell-attached half is held
// (`docs/2026-09-19-frontend-architecture.md` §4).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { DRIVER_STATE, respond, show, WIRING } from './test/shellHarness';
import { chooseWorkspace, ProjectsView, repositoryMain } from './test/projectsView';
import { WORKSPACE_CLEARED, WORKSPACE_EMPTY, WORKSPACE_PLAN } from './work/historyFixtures';

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
];

describe("a workspace's history on its Details", () => {
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

  const answering = (answers: Record<string, unknown>) => invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return WIRING;
    if (type in answers) {
      const answer = answers[type];
      if (answer instanceof Error) throw answer;
      return answer;
    }
    if (type === 'HARNESSES') return { harnesses: [] };
    if (type === 'ACCOUNTS') return { agents: [] };
    if (type === 'STARTS') return { starts: [] };
    return DRIVER_STATE;
  });

  it('reads what the home keeps, lists the clear, and sends exactly the units it listed', async () => {
    answering({ HISTORY_PLAN: WORKSPACE_PLAN, HISTORY_CLEAR: WORKSPACE_CLEARED });
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    const page = await chooseWorkspace('aurora');

    const kept = await within(page).findByRole('region', { name: 'Kept on this machine' });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_PLAN', expect.objectContaining({ payload: { workspace: 'aurora' } }));
    expect(kept).toHaveTextContent('11 closed quests, 2 asks, 14 sessions, 1 copy of a teammate’s record: 23.6 MB.');

    await userEvent.click(within(kept).getByRole('button', { name: 'Clear history…' }));
    const ask = within(kept).getByRole('group', { name: 'clear from this machine' });
    expect(within(ask).getByRole('list', { name: 'What stays' })).toHaveTextContent('Ask #b2c3d4 is waiting for you to publish or close it');
    await userEvent.click(within(ask).getByRole('button', { name: 'Clear 3' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_CLEAR', expect.objectContaining({
      payload: {
        workspace: 'aurora',
        units: [{ kind: 'ask', id: 'a1b2c3' }, { kind: 'quest', id: '0c1d2e' }, { kind: 'quest', id: '3f4a5b' }],
      },
    })));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Cleared 2 of 3 from aurora, 14.6 MB. 1 changed since the list and was kept.', 'ok'));
    // The reading is asked again, so the page says what is kept now.
    await waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'HISTORY_PLAN').length).toBeGreaterThan(1));
    expect(within(repositoryMain()).queryByRole('group', { name: 'clear from this machine' })).toBeNull();
  });

  it('opens Branches from a reason the clean-up frees, and a kept quest’s page through the application’s door', async () => {
    answering({ HISTORY_PLAN: WORKSPACE_PLAN, SWEEP_PLAN: { branches: [], landed: [] } });
    const onOpenQuest = vi.fn();
    show(<ProjectsView notify={() => {}} onOpenQuest={onOpenQuest} />);
    const page = await chooseWorkspace('aurora');

    const kept = await within(page).findByRole('region', { name: 'Kept on this machine' });
    await userEvent.click(within(kept).getByRole('button', { name: 'Clear history…' }));
    await userEvent.click(within(within(kept).getByRole('listitem', { name: 'Quest #9f0a1b' })).getByRole('button', { name: 'Open #9f0a1b' }));
    expect(onOpenQuest).toHaveBeenCalledWith('9f0a1b');

    await userEvent.click(within(kept).getByRole('button', { name: 'Never mind' }));
    await userEvent.click(within(kept).getByRole('button', { name: 'Open branches' }));
    expect(within(repositoryMain()).getByRole('tab', { name: 'Branches' })).toHaveAttribute('aria-selected', 'true');
  });

  it('says nothing finished is kept, with no press, on a workspace with nothing closed', async () => {
    answering({ HISTORY_PLAN: WORKSPACE_EMPTY });
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseWorkspace('aurora');

    const kept = await within(page).findByRole('region', { name: 'Kept on this machine' });
    expect(kept).toHaveTextContent('Nothing finished is kept here.');
    expect(within(kept).queryByRole('button', { name: 'Clear history…' })).toBeNull();
  });

  it('leaves the section out where the host answers no plan', async () => {
    answering({});
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseWorkspace('aurora');
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HISTORY_PLAN', expect.anything()));
    expect(within(page).queryByRole('region', { name: 'Kept on this machine' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'aurora' })).toBeInTheDocument();
  });
});
