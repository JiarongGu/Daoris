import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { SyncSection, type LinePull, type RebaseBranch, type SyncPlan } from './Sync';
import type { LandedBranch } from './Sweep';

// WSR6 (D109): after a pull request merges — each line pulled by a fast-forward, the branches still at work
// replayed onto it, the landed branches whose work reached it deleted. Listed first, done by one press, and the
// list is asked for, never fetched on its own.

const pull = (extra: Partial<LinePull> & Pick<LinePull, 'kind'>): LinePull => ({
  repository: 'engine', workspace: 'aurora', line: 'main', commits: 0, moves: false, ...extra,
});

const rebase = (extra: Partial<RebaseBranch> & Pick<RebaseBranch, 'branch' | 'kind'>): RebaseBranch => ({
  repository: 'engine', workspace: 'aurora', landed: false, onto: 'main', commits: 0, replays: false, ...extra,
});

const landed = (extra: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'engine', workspace: 'aurora', files: [], commits: 1, removable: false, ...extra,
});

const PLAN: SyncPlan = {
  lines: [
    pull({ kind: 'fast-forward', commits: 1, moves: true }),
    pull({ repository: 'game', kind: 'dirty' }),
  ],
  rebases: [
    rebase({ branch: 'daoris/s-step', kind: 'replay', commits: 1, cutBy: 'record', grewFrom: 'daoris/s-parent', replays: true }),
    rebase({ branch: 'feature/q2-second', kind: 'replay', landed: true, commits: 1, cutBy: 'record', replays: true }),
    rebase({ branch: 'daoris/s-busy', kind: 'in-use' }),
    rebase({ branch: 'feature/q3-open', kind: 'pushed', landed: true }),
    rebase({ branch: 'daoris/s-waits', kind: 'grew-from-unlanded', grewFrom: 'feature/q3-open' }),
  ],
  deletes: [
    landed({ branch: 'feature/q1-first', kind: 'on-line', where: 'origin/main', removable: true }),
  ],
};

const draw = (props: Partial<Parameters<typeof SyncSection>[0]> = {}) => {
  const onSync = vi.fn();
  const onLook = vi.fn();
  render(
    <Tooltip.Provider>
      <SyncSection plan={PLAN} onLook={onLook} onSync={onSync} {...props} />
    </Tooltip.Provider>,
  );
  return { onSync, onLook };
};

describe('bringing repositories up to date', () => {
  it('is asked for: before a look it lists nothing, and says the look fetches while nothing of yours moves', async () => {
    const { onLook } = draw({ plan: undefined });

    expect(screen.queryByRole('listitem')).toBeNull();
    expect(screen.getByText(/never pushes/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Look for updates' }));
    expect(onLook).toHaveBeenCalledOnce();
  });

  it('lists each line, each branch and each landed branch with what the press would do', () => {
    draw();

    const row = (name: string) => screen.getByRole('listitem', { name });
    const engine = within(screen.getByRole('region', { name: 'engine' }));
    expect(within(engine.getByRole('listitem', { name: 'main' })).getByText('moves')).toBeInTheDocument();
    expect(within(engine.getByRole('listitem', { name: 'main' })).getByText(/Fast-forwards 1 commit to/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'game' })).getByText(/uncommitted work, so it stays/)).toBeInTheDocument();
    expect(within(row('daoris/s-step')).getByText(/Replays 1 commit of its own onto/)).toBeInTheDocument();
    expect(within(row('daoris/s-step')).getByText('daoris/s-parent', { selector: 'code' })).toBeInTheDocument();
    expect(within(row('feature/q2-second')).getByText("a landing's")).toBeInTheDocument();
    expect(within(row('daoris/s-busy')).getByText('stays')).toBeInTheDocument();
    expect(within(row('feature/q3-open')).getByText(/force push/)).toBeInTheDocument();
    expect(within(row('daoris/s-waits')).getByText(/has not reached the line yet/)).toBeInTheDocument();
    expect(within(row('feature/q1-first')).getByText('goes')).toBeInTheDocument();
  });

  it('does only what it listed, on one press', async () => {
    const { onSync } = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Bring up to date: 4 changes' }));

    expect(onSync).toHaveBeenCalledWith(['engine:main', 'engine:daoris/s-step', 'engine:feature/q2-second', 'engine:feature/q1-first']);
  });

  it('says so when everything is up to date, and has nothing to press', () => {
    draw({ plan: { lines: [pull({ kind: 'up-to-date' })], rebases: [rebase({ branch: 'daoris/s-done', kind: 'up-to-date' })], deletes: [] } });

    expect(screen.getByText('Everything here is up to date.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Bring up to date/ })).toBeDisabled();
  });

  it('names a fetch that did not happen, in git\'s words', () => {
    draw({ plan: { lines: [pull({ kind: 'up-to-date', fetch: 'there is no `origin` remote here' })], rebases: [], deletes: [] } });

    expect(screen.getByText(/Not fetched/)).toBeInTheDocument();
    expect(screen.getByText('origin', { selector: 'code' })).toBeInTheDocument();
  });

  it('holds both presses while one is under way', () => {
    draw({ busy: true });

    expect(screen.getByRole('button', { name: /Bring up to date/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Look again' })).toBeDisabled();
  });
});
