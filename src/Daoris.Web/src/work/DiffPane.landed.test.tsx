import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEW2 (D113): the review of a landed session, over a mocked bridge. A landing that tidied its tree away is read
// from its landed branch and says so; its acts are the hand-off where one applies and nothing that acts on a tree
// that is gone; a branch gone since is said plainly. The review's other acts are WorkFrame.test's.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../i18n';
import { DiffPane } from './DiffPane';

const LANDED = {
  branch: 'feature/0fda18-fix-the-api-gap', repository: 'engine', line: 'main',
  landedAt: '2026-10-01T09:30:00.0000000+00:00', plugin: null, pushed: false, pullRequest: null,
  state: 'standing', asLanded: true, reads: null, removed: null, detail: null,
};

const FILES = [{ path: 'src/api/gap.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }];

/** What the host answers once the landing tidied the tree: the landed branch's changes, from where its work grew from. */
const FROM_BRANCH = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'branch', landed: LANDED };

const asked = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

function answer(diff: unknown, handOff: unknown = { session: 's1a2b3c4', branch: null }) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') {
      if (diff instanceof Error) throw diff;
      return diff;
    }
    if (type === 'HANDOFF_PLAN') return handOff;
    if (type === 'LANDING') return { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-api-gap', source: 'workspace' };
    return {};
  });
}

function pane(onSendBack = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session="s1a2b3c4" hasTree onSendBack={onSendBack} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return onSendBack;
}

describe('the review of a landed session (REVIEW2)', () => {
  afterEach(() => {
    invoke.mockReset();
  });

  /**
   * The installed window's case: the landing tidied the tree. The review says where the work landed and reads its
   * changes from that branch; accepting, sending back and discarding a tree that is gone are not offered, and
   * the landing's plan is never asked.
   */
  it('reads a tidied landing from its branch, says where it landed, and offers nothing that acts on the tree', async () => {
    answer(FROM_BRANCH);
    pane();

    expect(await screen.findByText('src/api/gap.ts')).toBeTruthy();
    const note = screen.getByRole('region', { name: 'where this work landed' });
    expect(note.textContent).toMatch(/landed on feature\/0fda18-fix-the-api-gap/);
    expect(note.textContent).toMatch(/read from that branch in engine's own checkout/);
    expect(screen.getByText(/since 01234567/)).toBeTruthy();
    await waitFor(() => expect(asked('HANDOFF_PLAN')).toHaveLength(1));
    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'send it back…' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
    expect(asked('LANDING')).toHaveLength(0);
  });

  /** The hand-off stays where one applies (WSR5b): the landed branch, handed to the rule's plugin. */
  it('offers the hand-off of the landed branch, and only that', async () => {
    answer(FROM_BRANCH, { session: 's1a2b3c4', branch: LANDED.branch, repository: 'engine', plugin: 'example.lands', problem: null, commits: 1 });
    pane();

    expect(await screen.findByRole('button', { name: 'hand it to example.lands' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
  });

  /** The pull request a plugin opened is one press away, in the note. */
  it('links the pull request a plugin opened', async () => {
    answer({ ...FROM_BRANCH, landed: { ...LANDED, plugin: 'example.lands', pushed: true, pullRequest: 'https://example.test/org/engine/pull/7' } });
    pane();

    expect(await screen.findByRole('link', { name: 'open the pull request' })).toHaveAttribute('href', 'https://example.test/org/engine/pull/7');
  });

  /** A branch gone since: said plainly, with whether its work reads on the line — never "nothing landed". */
  it('says a landed branch gone since and whether its work reads on the line, rather than that nothing landed', async () => {
    answer({
      ...FROM_BRANCH, base: '', files: [],
      landed: {
        ...LANDED, state: 'gone',
        removed: { kind: 'on-line', where: 'origin/main', at: '2026-10-01T11:00:00Z' },
        reads: { kind: 'on-line', where: 'main', files: [], detail: null },
      },
    });
    pane();

    const note = await screen.findByRole('region', { name: 'where this work landed' });
    expect(note.textContent).toMatch(/That branch is gone from engine now\. The clean-up removed it once its work read on origin\/main\. Its work reads on main\./);
    expect(screen.queryByText('Nothing landed')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });

  /** No tidy: the tree is still here and its landed branch stands. The tree's changes; no accepting again; the tree may go. */
  it('keeps discarding a tree still here, and offers no second landing while its branch stands', async () => {
    answer({ ...FROM_BRANCH, base: 'abc1234567890', source: 'tree' });
    pane();

    expect(await screen.findByRole('button', { name: 'discard the tree' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'where this work landed' }).textContent).toMatch(/not accepted again/);
    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'send it back…' })).toBeNull();
    expect(asked('LANDING')).toHaveLength(0);
  });

  /**
   * The tree is still here and its landed branch went (WSR6 replayed what it did since): the review is the tree's
   * again, says where it landed before, and every act is back.
   */
  it('gives a tree that carried on after its landing its acts back once the landed branch is gone', async () => {
    answer({ ...FROM_BRANCH, base: 'abc1234567890', source: 'tree', landed: { ...LANDED, state: 'gone', asLanded: false } });
    const onSendBack = pane();

    expect(await screen.findByRole('button', { name: 'accept' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'discard the tree' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'send it back…' })).toBeTruthy();
    expect(screen.getByRole('region', { name: 'where this work landed' }).textContent).toMatch(/That branch is gone from engine now/);
    await waitFor(() => expect(asked('LANDING')).toHaveLength(1));
    expect(onSendBack).not.toHaveBeenCalled();
  });

  /** A tree gone with no landing recorded (a tidy after a merge, a discard): the host's sentence, and no act on a tree. */
  it('offers no act on a tree the host says is gone', async () => {
    answer(Object.assign(new Error('fallback'), { code: 'SESSION_TREE_GONE', parameters: { session: 's1a2b3c4' } }));
    pane();

    expect(await screen.findByText(/This session's tree is gone from this machine/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'accept' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'discard the tree' })).toBeNull();
    // The work was not landed here, so sending it back is still a door.
    expect(screen.getByRole('button', { name: 'send it back…' })).toBeTruthy();
    expect(asked('LANDING')).toHaveLength(0);
  });
});
