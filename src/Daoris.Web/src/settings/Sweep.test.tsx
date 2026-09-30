import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import { SweepList, type LandedBranch, type SweepBranch } from './Sweep';

// WSR3 (D88): every session branch with what it holds, listed first — then one press removes those
// whose work is on a branch of the person's, or that hold nothing, and only those it listed to go.

const branch = (extra: Partial<SweepBranch> & Pick<SweepBranch, 'branch' | 'kind'>): SweepBranch => ({
  repository: 'engine', workspace: 'aurora', hasTree: true, commits: 0, removable: false, ...extra,
});

const BRANCHES: SweepBranch[] = [
  branch({ branch: 'daoris/s-empty', kind: 'empty', where: 'main', removable: true }),
  branch({ branch: 'daoris/s-landed', kind: 'landed', where: 'feature/0fda18-fix', removable: true }),
  branch({ branch: 'daoris/s-alone', kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work\ne4f5a6b more work' }),
  branch({ branch: 'daoris/s-dirty', kind: 'dirty' }),
  branch({ repository: 'game', branch: 'daoris/s-busy', kind: 'in-use' }),
];

const draw = (props: Partial<Parameters<typeof SweepList>[0]> = {}) => {
  const onClean = vi.fn();
  render(
    <Tooltip.Provider>
      <SweepList branches={BRANCHES} onLook={vi.fn()} onClean={onClean} {...props} />
    </Tooltip.Provider>,
  );
  return onClean;
};

describe('the session branches card', () => {
  it('lists each branch with what it holds, and whether it goes', () => {
    draw();

    const row = (name: string) => screen.getByRole('listitem', { name });
    expect(within(row('daoris/s-empty')).getByText('goes')).toBeInTheDocument();
    expect(within(row('daoris/s-landed')).getByText('feature/0fda18-fix', { selector: 'code' })).toBeInTheDocument();
    expect(within(row('daoris/s-alone')).getByText('kept')).toBeInTheDocument();
    expect(within(row('daoris/s-alone')).getByText(/2 commits no branch of yours holds/)).toBeInTheDocument();
    expect(within(row('daoris/s-alone')).getByText(/a1b2c3d the work/)).toBeInTheDocument();
    expect(within(row('daoris/s-dirty')).getByText(/uncommitted/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'game' })).getByText(/still running or waiting/)).toBeInTheDocument();
  });

  it('removes only what it listed to go, on one press', async () => {
    const onClean = draw();

    await userEvent.click(screen.getByRole('button', { name: 'Clean up 2 branches' }));

    expect(onClean).toHaveBeenCalledWith(['engine:daoris/s-empty', 'engine:daoris/s-landed']);
  });

  it('has nothing to press when nothing would go, and says when there are no branches at all', () => {
    draw({ branches: [BRANCHES[2]!] });
    expect(screen.getByRole('button', { name: 'Clean up 0 branches' })).toBeDisabled();
  });

  it('names a machine with no session branch', () => {
    draw({ branches: [] });
    expect(screen.getByText(/No session branches/)).toBeInTheDocument();
  });
});

// WSR5a: the branches landings made, in a group of their own — each goes once every file it changed reads
// on the line as it left it, or it is inside another that does — and by the same press.

const landed = (extra: Partial<LandedBranch> & Pick<LandedBranch, 'branch' | 'kind'>): LandedBranch => ({
  repository: 'engine', workspace: 'aurora', files: [], commits: 1, removable: false, ...extra,
});

const LANDED: LandedBranch[] = [
  landed({ branch: 'feature/q2-second', kind: 'on-line', where: 'origin/main', removable: true }),
  landed({ branch: 'feature/q1-first', kind: 'inside', where: 'feature/q2-second', removable: true }),
  landed({ branch: 'feature/q3-third', kind: 'differs', where: 'main', files: ['shared.txt', 'b.txt'] }),
  landed({ repository: 'game', branch: 'feature/q4-fourth', kind: 'checked-out' }),
  landed({ repository: 'game', branch: 'feature/q5-fifth', kind: 'ahead-of-remote', commits: 2 }),
  landed({ repository: 'game', branch: 'feature/q6-sixth', kind: 'leaned-on', where: 'daoris/s-1' }),
];

describe('the landed branches in the session branches card', () => {
  it('lists each branch a landing made in its own group, with what it holds', () => {
    draw({ landed: LANDED });

    const group = screen.getByRole('region', { name: 'Branches landings made' });
    const row = (name: string) => within(group).getByRole('listitem', { name });
    expect(within(row('feature/q2-second')).getByText('goes')).toBeInTheDocument();
    expect(within(row('feature/q2-second')).getByText('origin/main', { selector: 'code' })).toBeInTheDocument();
    expect(within(row('feature/q1-first')).getByText('feature/q2-second', { selector: 'code' })).toBeInTheDocument();
    expect(within(row('feature/q3-third')).getByText('kept')).toBeInTheDocument();
    expect(within(row('feature/q3-third')).getByText(/2 files still differ on the line/)).toBeInTheDocument();
    expect(within(row('feature/q3-third')).getByText('shared.txt, b.txt')).toBeInTheDocument();
    expect(within(row('feature/q4-fourth')).getByText(/Checked out/)).toBeInTheDocument();
    expect(within(row('feature/q5-fifth')).getByText(/2 commits its remote does not have/)).toBeInTheDocument();
    expect(within(row('feature/q6-sixth')).getByText('daoris/s-1', { selector: 'code' })).toBeInTheDocument();
    // Grouped by repository within its own group.
    expect(within(group).getByRole('group', { name: 'game' })).toBeInTheDocument();
  });

  it('removes what both groups listed to go, on the one press', async () => {
    const onClean = draw({ landed: LANDED });

    await userEvent.click(screen.getByRole('button', { name: 'Clean up 4 branches' }));

    expect(onClean).toHaveBeenCalledWith([
      'engine:daoris/s-empty', 'engine:daoris/s-landed', 'engine:feature/q2-second', 'engine:feature/q1-first',
    ]);
  });

  it('draws no group where no landing made a branch', () => {
    draw({ landed: [] });
    expect(screen.queryByRole('region', { name: 'Branches landings made' })).not.toBeInTheDocument();
  });
});
