import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEWENV1g (D154 point 7; the review environment design §3.1): the session's review says the work waits for the person's
// review where *Accept* would be, and offers no *Accept* the landing door would refuse; where nothing waits, it is as it was.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../../locales/i18n';
import type { Session } from '../../data/api';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'src/report/compare.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main', source: 'workspace' };

function answer(landing: unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return landing;
    return {};
  });
}

function pane() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe("the review's foot while a review's gate holds the work", () => {
  afterEach(() => { invoke.mockReset(); });

  it.each(['kind-paths', 'cannot-start', 'unread'])('shows the workflow %s hold instead of Accept', async (state) => {
    answer({ ...PLAN, workflow: { state, holds: true, says: 'Holds: choose a saved workflow in the terminal.' } });
    pane();
    expect(await screen.findByRole('region', { name: 'Workflow before it lands' })).toHaveTextContent('choose a saved workflow');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });

  it('says it waits for the review in its environment, in the gate\'s state, and offers no Accept', async () => {
    answer({ ...PLAN, review: { state: 'shown', environment: 'local', level: 'set-up-step', quest: 'q2' } });
    pane();

    expect(await screen.findByText(/Waits for your review in/)).toHaveTextContent('Waits for your review in local · shown');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toBeInTheDocument();
  });

  it('offers Accept as before where the gate lets the work go', async () => {
    answer(PLAN);
    pane();

    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByText(/Waits for your review/)).toBeNull();
  });
});

/**
 * UXFIX2b2b: the review's own discard asks twice. The unforced press's refusal names what would go and arms the ask, which
 * says it; the forced press stays open and waiting until the driver answers, says its own refusal inside itself, and closes
 * once the tree went, the focus back on the press drawn again.
 */
describe('the review’s forced discard', () => {
  afterEach(() => { invoke.mockReset(); });

  const NAMES = 'The tree holds 2 uncommitted path(s), which a discard would destroy.';
  const discards = () => invoke.mock.calls.filter(([, type]) => type === 'DISCARD_SESSION_TREE').map(([, , options]) => options.payload);

  function drive(forced: Array<{ done: boolean; message: string } | Error>) {
    const queue = [...forced];
    invoke.mockImplementation(async (_module: string, type: string, options?: { payload?: { force?: boolean } }) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return PLAN;
      if (type === 'DISCARD_SESSION_TREE') {
        if (!options?.payload?.force) return { done: false, message: NAMES };
        const next = queue.shift()!;
        if (next instanceof Error) throw next;
        return next;
      }
      return {};
    });
  }

  const armed = async () => {
    pane();
    await userEvent.click(await screen.findByRole('button', { name: 'Discard tree' }));
    return await screen.findByRole('group', { name: 'discard this tree' });
  };

  it('arms an ask that says what would go, and the first press stays a ghost until then', async () => {
    drive([]);
    const ask = await armed();

    expect(ask).toHaveTextContent(NAMES);
    // Said once: the ask holds the sentence, and the foot does not repeat it.
    expect(screen.getAllByText(NAMES)).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();
    expect(within(ask).getByRole('button', { name: 'Discard anyway' }).className).toContain('text-ink-danger');
    expect(discards()).toEqual([{ id: 's1a2b3c4' }]);
  });

  it('says a refused forced discard inside the ask, and lets it be pressed again', async () => {
    drive([{ done: false, message: 'The tree is busy.' }, new Error('The driver is not running.')]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    expect(await within(ask).findByRole('alert')).toHaveTextContent('The tree is busy.');
    expect(discards()[1]).toEqual({ id: 's1a2b3c4', force: true });

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    await waitFor(() => expect(within(ask).getByRole('alert')).toHaveTextContent('The driver is not running.'));
    expect(screen.getByRole('group', { name: 'discard this tree' })).toBeInTheDocument();
  });

  it('closes once the tree went, says so in the foot, and gives the focus back to the press drawn again', async () => {
    drive([{ done: true, message: 'Discarded the tree.' }]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard anyway' }));
    await waitFor(() => expect(screen.queryByRole('group', { name: 'discard this tree' })).toBeNull());
    expect(screen.getByText('Discarded the tree.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toHaveFocus();
  });

  it('puts the ask down on Never mind, forcing nothing, and returns the focus', async () => {
    drive([]);
    const ask = await armed();

    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'discard this tree' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Discard tree' })).toHaveFocus();
    expect(discards()).toEqual([{ id: 's1a2b3c4' }]);
  });
});

/**
 * REVIEWDIRTY1-b (D164 point 4): a session tree's uncommitted work is read beside what it committed, under its own heading;
 * "Nothing landed" is said only where both are empty, and Accept's refusal is said before the press.
 */
describe('the review of a tree holding uncommitted work', () => {
  afterEach(() => { invoke.mockReset(); });

  const UNCOMMITTED = {
    files: [
      { path: 'src/report/draft.ts', status: 'modified', added: 2, removed: 1, patch: '@@ -1 +1,2 @@\n-x\n+y\n+z' },
      { path: 'assets/dump.bin', status: 'added', added: null, removed: null, patch: '' },
    ],
    truncated: false,
  };

  function drive(diff: Record<string, unknown>, plan: Record<string, unknown> = PLAN) {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return { ...DIFF, ...diff };
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return plan;
      return {};
    });
  }

  it('lists them under their own heading after the committed files, with the same rows and patch', async () => {
    drive({ uncommitted: UNCOMMITTED });
    pane();

    expect(await screen.findByText('src/report/compare.ts')).toBeInTheDocument();
    expect(screen.getByText('Not committed yet · 2 files')).toBeInTheDocument();
    const draft = screen.getByText('src/report/draft.ts');
    expect(screen.getByText('src/report/compare.ts').compareDocumentPosition(draft) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    await userEvent.click(draft);
    expect(await screen.findByText('z')).toBeInTheDocument();
  });

  it('says why an uncommitted file has no patch, and its truncated line', async () => {
    drive({ uncommitted: { ...UNCOMMITTED, truncated: true } });
    pane();

    await userEvent.click(await screen.findByText('assets/dump.bin'));
    expect(await screen.findByText(/too large, or not text/)).toBeInTheDocument();
    expect(screen.getByText(/More uncommitted files than are listed/)).toBeInTheDocument();
  });

  it('says the session committed nothing yet, and never "Nothing landed", where only uncommitted work exists', async () => {
    // Nothing committed answers a tree review with no files and an empty base: not a refusal.
    drive({ files: [], base: '', uncommitted: UNCOMMITTED });
    pane();

    expect(await screen.findByText('Not committed yet · 2 files')).toBeInTheDocument();
    expect(screen.getByText(/committed nothing yet/)).toBeInTheDocument();
    expect(screen.queryByText('Nothing landed')).toBeNull();
  });

  it('says before the press that Accept will refuse until the files are committed, and leaves Accept pressable', async () => {
    drive({ uncommitted: UNCOMMITTED }, { ...PLAN, uncommitted: 5 });
    pane();

    expect(await screen.findByText(/Accept will refuse until the 5 uncommitted files are committed/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept' })).toBeEnabled();
  });

  it('says nothing of it where the plan counts none', async () => {
    drive({}, { ...PLAN, uncommitted: 0 });
    pane();
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByText(/uncommitted files are committed/)).toBeNull();
    expect(screen.queryByText(/Not committed yet/)).toBeNull();
  });
});

/**
 * REVIEWBASE1 (D164's note): the review measures from the newest commit the work shares with the line, and says in its head how
 * far the latest line the host knows is past the work. A tree that holds nothing beyond the line says so, never "Nothing landed".
 */
describe('the review against the latest line', () => {
  afterEach(() => { invoke.mockReset(); });

  function drive(diff: Record<string, unknown>) {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return { ...DIFF, ...diff };
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return PLAN;
      return {};
    });
  }

  it('says in its head that the line holds commits the work does not', async () => {
    drive({ behind: { line: 'main', commits: 3, ahead: 1 } });
    pane();

    expect(await screen.findByText(/has 3 commits this work does not: it is not based on the latest/)).toBeInTheDocument();
    expect(screen.getByText('src/report/compare.ts')).toBeInTheDocument();
  });

  it('says nothing of the line where the work is based on its latest', async () => {
    drive({ behind: { line: 'main', commits: 0, ahead: 1 } });
    pane();

    expect(await screen.findByText('src/report/compare.ts')).toBeInTheDocument();
    expect(screen.queryByText(/not based on the latest/)).toBeNull();
  });

  it('says a tree that holds nothing beyond the line holds nothing beyond it, never "Nothing landed"', async () => {
    drive({ files: [], behind: { line: 'main', commits: 0, ahead: 0 } });
    pane();

    expect(await screen.findByText('Nothing beyond the line')).toBeInTheDocument();
    expect(screen.getByText(/holds nothing/)).toHaveTextContent('Its tree holds nothing main does not, so there is no diff to read.');
    expect(screen.queryByText('Nothing landed')).toBeNull();
  });

  it('says "Nothing landed" as before where the tree holds commits beyond the line and none of its own', async () => {
    // A chain's step that made no commit yet: measured from its parent's tip, past the line.
    drive({ files: [], behind: { line: 'main', commits: 0, ahead: 2 } });
    pane();

    expect(await screen.findByText('Nothing landed')).toBeInTheDocument();
    expect(screen.queryByText('Nothing beyond the line')).toBeNull();
  });
});

/**
 * REVIEWBASE4 (D164's note): a tree that holds nothing beyond the line has nothing to land. Its review says so where *Accept*
 * would be, offers neither *Accept* nor *Send back…*, asks no landing plan, and leads with *Discard tree*. Its head names the
 * branch the tree stands on once the review answers, over the record's guess, and none where the tree is detached.
 */
describe('the review of a tree with nothing beyond the line', () => {
  afterEach(() => { invoke.mockReset(); });

  const RECORD: Session = {
    id: 's1a2b3c4',
    quest: 'abc123',
    repository: 'portal-ui',
    adapter: 'claude-code',
    state: 'completed',
    kind: 'driven',
    created: '2026-10-03T08:00:00Z',
    updated: '2026-10-03T09:00:00Z',
    workspace: 'default',
    tree: 'C:/somewhere/.daoris/trees/default/portal-ui/s-aeefe837',
    baseCommit: '0fda18c2b7e4a9d1',
  };
  // The seen case: the rule lands on a branch, so the pane once said Accept would put the work on one.
  const BRANCH_PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/aeefe837-fix-the-gap', source: 'workspace' };
  const EVEN = { files: [], base: '9c41e07d2b5a8f13', behind: { line: 'main', commits: 0, ahead: 0 } };
  const landings = () => invoke.mock.calls.filter(([, type]) => type === 'LANDING');

  function drive(diff: Record<string, unknown>) {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return { ...DIFF, ...diff };
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return BRANCH_PLAN;
      return {};
    });
  }

  function attended() {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <DiffPane session="s1a2b3c4" record={RECORD} title="Fix the gap" hasTree onSendBack={vi.fn()} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
  }

  it('says there is nothing to land where Accept would be, and offers neither Accept nor Send back', async () => {
    drive(EVEN);
    attended();

    expect(await screen.findByText('Nothing beyond the line')).toBeInTheDocument();
    expect(screen.getByText(/nothing to land/)).toHaveTextContent('There is nothing to land: everything its tree holds is on main already.');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Send back…' })).toBeNull();
    expect(screen.queryByText(/Accepting puts this work/)).toBeNull();
    // Nothing would land, so no plan is asked for: `useLanding` is handed no session.
    expect(landings()).toHaveLength(0);
  });

  it('leads with Discard tree', async () => {
    drive(EVEN);
    attended();

    await screen.findByText('Nothing beyond the line');
    expect(screen.getByRole('button', { name: 'Discard tree' }).className).toContain('bg-accent');
  });

  it.each([
    ['holds commits beyond the line', { files: [], behind: { line: 'main', commits: 0, ahead: 2 } }],
    ['holds uncommitted work', {
      files: [],
      behind: { line: 'main', commits: 0, ahead: 0 },
      uncommitted: { files: [{ path: 'src/draft.ts', status: 'modified', added: 1, removed: 0, patch: '@@ -1 +1 @@\n+x' }], truncated: false },
    }],
    ['was not measured against its line', { files: [], behind: null }],
  ])('offers Accept as before where the tree %s', async (_case, diff) => {
    drive(diff);
    attended();

    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send back…' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Discard tree' }).className).toContain('bg-transparent');
    expect(screen.queryByText(/nothing to land/)).toBeNull();
  });

  it("names the branch its tree stands on once the review answers, over the record's guess", async () => {
    drive({ treeBranch: 'feature/TK-9-gap' });
    attended();

    expect(await screen.findByText('feature/TK-9-gap')).toBeInTheDocument();
    expect(screen.queryByText('daoris/s-aeefe837')).toBeNull();
  });

  it('names no branch for a detached tree, and says it is detached at the commit the line holds', async () => {
    drive({ ...EVEN, treeBranch: null });
    attended();

    expect(await screen.findByText('detached HEAD at 9c41e07d')).toBeInTheDocument();
    expect(screen.queryByText('daoris/s-aeefe837')).toBeNull();
    expect(screen.queryByText(/^since /)).toBeNull();
  });

  it("keeps the record's branch where the answer names none, as a host older than REVIEWBASE4 answers", async () => {
    drive({});
    attended();

    expect(await screen.findByText('src/report/compare.ts')).toBeInTheDocument();
    expect(screen.getByText('daoris/s-aeefe837')).toBeInTheDocument();
    expect(screen.queryByText(/detached HEAD/)).toBeNull();
  });
});
