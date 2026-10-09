import { act, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { fireEvent } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

// SQUASHTIDY1f (D102's SQUASHTIDY1b and SQUASHTIDY1f notes): the review of a session whose work the line holds by content, a
// squash-merged pull request's, offers no *Accept*, which the landing door now refuses, but says where the work is in the
// head's clause and offers the head's discard in its place, from the same pieces and through the same press. Where the tree
// holds what no commit does, it says why the tree stays, and the review's own Discard, which asks twice, stays the door.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import i18n from '../i18n';
import { DiffPane } from './DiffPane';
import type { Answered } from './InlineConfirm';
import type { DiscardOffer } from './groups';

const FILES = [{ path: 'src/report/compare.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main', source: 'workspace' };
const KEPT = 'refs/daoris/discarded/daoris/s-4e6837ed';
const HELD: DiscardOffer = {
  branch: 'daoris/s-4e6837ed',
  tree: 's-4e6837ed',
  says: 'Its work is on `main` by content (a squash merge).',
  keeps: KEPT,
  keptAt: `Its commits stay at \`${KEPT}\` until you delete that ref; \`git branch daoris/s-4e6837ed ${KEPT}\` brings the branch back.`,
};
const STAYS: DiscardOffer = {
  ...HELD,
  says: 'Its work is on `main` by content (a squash merge). Its tree stays: it holds 1 ignored path(s) your checkout does not have: local.db, which a discard would destroy.',
  keeps: null,
  keptAt: null,
};

const asked = (type: string) => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === type);

function answer() {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return PLAN;
    return {};
  });
}

function pane(discards: DiscardOffer | null, onDiscardTree?: (answered: Answered) => void) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} discards={discards} onDiscardTree={onDiscardTree} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe.each(['en', 'zh'])('the review of a session whose work the line holds by content, in %s', (language) => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('says where its work is where Accept would be, and its discard asks once naming the ref, then presses as the head does', async () => {
    await i18n.changeLanguage(language);
    answer();
    const onDiscardTree = vi.fn();
    const { container } = pane(HELD, onDiscardTree);

    expect(await screen.findByText('src/report/compare.ts')).toBeInTheDocument();
    // The driver's sentence, shown as it is in either language: it is content, as the review's own sentence is.
    expect(container).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(screen.queryByRole('button', { name: i18n.t('work.review.accept') })).toBeNull();
    // One discard: the head's, in Accept's place; the review's own two-step one would say the same unforced.
    expect(screen.queryByRole('button', { name: i18n.t('work.review.discard') })).toBeNull();
    expect(screen.getByRole('button', { name: i18n.t('work.review.sendBack') })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: i18n.t('settings.sweep.discard') }));
    expect(onDiscardTree).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: i18n.t('settings.sweep.discardTitle', { branch: HELD.branch }) });
    expect(ask).toHaveTextContent(`Its commits stay at ${KEPT} until you delete that ref; git branch daoris/s-4e6837ed ${KEPT} brings the branch back.`);
    fireEvent.click(within(ask).getByRole('button', { name: i18n.t('settings.sweep.discardMeanIt') }));
    expect(onDiscardTree).toHaveBeenCalledOnce();
    act(() => (onDiscardTree.mock.calls[0]![0] as Answered).done());
    expect(screen.queryByRole('group', { name: i18n.t('settings.sweep.discardTitle', { branch: HELD.branch }) })).toBeNull();

    // The press is its owner's, never the pane's own: no landing is planned or pressed, and no discard sent from here.
    expect(asked('LANDING')).toHaveLength(0);
    expect(asked('LAND_SESSION_TREE')).toHaveLength(0);
    expect(asked('DISCARD_SESSION_TREE')).toHaveLength(0);
  });

  it('says a refusal inside the ask', async () => {
    await i18n.changeLanguage(language);
    answer();
    const onDiscardTree = vi.fn();
    pane(HELD, onDiscardTree);

    fireEvent.click(await screen.findByRole('button', { name: i18n.t('settings.sweep.discard') }));
    const ask = screen.getByRole('group', { name: i18n.t('settings.sweep.discardTitle', { branch: HELD.branch }) });
    fireEvent.click(within(ask).getByRole('button', { name: i18n.t('settings.sweep.discardMeanIt') }));
    act(() => (onDiscardTree.mock.calls[0]![0] as Answered).refused('the tree at s-4e6837ed has uncommitted work — 1 path(s).'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('the tree at s-4e6837ed has uncommitted work — 1 path(s).');
  });

  it('says why a tree that holds what no commit does stays, and keeps the review’s own Discard as the door', async () => {
    await i18n.changeLanguage(language);
    answer();
    const { container } = pane(STAYS, vi.fn());

    expect(await screen.findByText('src/report/compare.ts')).toBeInTheDocument();
    expect(container).toHaveTextContent('Its tree stays: it holds 1 ignored path(s) your checkout does not have: local.db');
    expect(screen.queryByRole('button', { name: i18n.t('work.review.accept') })).toBeNull();
    expect(screen.queryByRole('button', { name: i18n.t('settings.sweep.discard') })).toBeNull();
    expect(screen.getByRole('button', { name: i18n.t('work.review.discard') })).toBeInTheDocument();
  });

  /**
   * Both: the landing door asks the content proof before the review's gate, since a review of work already on the line lands
   * nothing, so where a review's gate (REVIEWENV1g) would also hold the work the discard stands, and the plan is never asked.
   */
  it('offers the discard, not the review’s gate, where the review would also hold the work', async () => {
    await i18n.changeLanguage(language);
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return DIFF;
      if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
      if (type === 'LANDING') return { ...PLAN, review: { state: 'shown', environment: 'staging-preview', level: 'set-up-step', quest: 'q2' } };
      return {};
    });
    const { container } = pane(HELD, vi.fn());

    expect(await screen.findByRole('button', { name: i18n.t('settings.sweep.discard') })).toBeInTheDocument();
    expect(container).toHaveTextContent('Its work is on main by content (a squash merge).');
    expect(container).not.toHaveTextContent('staging-preview');
    expect(screen.queryByRole('button', { name: i18n.t('work.review.accept') })).toBeNull();
    expect(asked('LANDING')).toHaveLength(0);
  });

  it('offers Accept as before where nothing holds the work by content', async () => {
    await i18n.changeLanguage(language);
    answer();
    pane(null);

    expect(await screen.findByRole('button', { name: i18n.t('work.review.accept') })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: i18n.t('settings.sweep.discard') })).toBeNull();
    await waitFor(() => expect(asked('LANDING')).toHaveLength(1));
  });
});
