import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import { describe, expect, it, vi } from 'vitest';
import i18n from '../i18n';
import type { Answered } from '../work/InlineConfirm';
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

// LAND3b (D102's LAND3 note): a failed or superseded attempt's commits are on no branch of the person's, so no clean-up
// takes its branch. Beside each row the driver says `discardable`, the discard is offered, as the terminal's list prints
// `trees remove … --force` beside it; it asks once, naming the branch and that its commits go, and only then presses.

describe('discarding a failed attempt\'s branch from the session branches card', () => {
  const FAILED: SweepBranch[] = [
    branch({ branch: 'daoris/s-gone', hasTree: false, kind: 'unlanded', commits: 2, detail: 'a1b2c3d the work', discardable: true }),
    branch({ branch: 'daoris/s-here', kind: 'unlanded', commits: 1, discardable: true }),
    branch({ branch: 'daoris/s-unsure', hasTree: false, kind: 'unlanded', commits: 0 }),
    branch({ branch: 'daoris/s-empty', kind: 'empty', where: 'main', removable: true }),
  ];

  const drawDiscard = (props: Partial<Parameters<typeof SweepList>[0]> = {}) => {
    const onDiscard = vi.fn();
    const onClean = draw({ branches: FAILED, onDiscard, ...props });
    return { onDiscard, onClean };
  };
  const row = (name: string) => screen.getByRole('listitem', { name });

  it('offers the discard beside each row the driver offers it on, and beside no other', () => {
    drawDiscard();

    expect(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' })).toBeInTheDocument();
    expect(within(row('daoris/s-here')).getByRole('button', { name: 'Discard branch…' })).toBeInTheDocument();
    expect(within(row('daoris/s-unsure')).queryByRole('button', { name: 'Discard branch…' })).toBeNull();
    expect(within(row('daoris/s-empty')).queryByRole('button', { name: 'Discard branch…' })).toBeNull();
  });

  it('asks once, naming the branch, its repository and that its commits go, before it discards', async () => {
    const { onDiscard, onClean } = drawDiscard();

    await userEvent.click(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' }));
    expect(onDiscard).not.toHaveBeenCalled();
    const ask = screen.getByRole('group', { name: 'discard daoris/s-gone' });
    expect(ask).toHaveTextContent('Discards daoris/s-gone in engine, and with it 2 commits no branch of yours holds. Nothing brings them back.');
    expect(within(ask).getByText('daoris/s-gone', { selector: 'code' })).toBeInTheDocument();
    // While it asks, the first press is not offered twice.
    expect(within(row('daoris/s-gone')).queryByRole('button', { name: 'Discard branch…' })).toBeNull();

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(onDiscard).toHaveBeenCalledOnce();
    expect(onDiscard).toHaveBeenCalledWith(expect.objectContaining({ repository: 'engine', branch: 'daoris/s-gone' }), expect.anything());
    expect(onClean).not.toHaveBeenCalled();
  });

  /**
   * UXFIX2 (the second-opinion review, `Sweep.tsx:218`): the press keeps the ask open and waiting until the driver answers,
   * where it closed at once and refused in a toast. Its sentence takes the focus and describes the move; a branch the driver
   * kept is said inside it, in the driver's words; it closes once the discard lands.
   */
  it('keeps the ask open while the discard is on its way, says why the driver kept it inside, and closes once it lands', async () => {
    let answered: Answered | undefined;
    drawDiscard({ onDiscard: (_branch, told) => { answered = told; } });

    await userEvent.click(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' }));
    const ask = screen.getByRole('group', { name: 'discard daoris/s-gone' });
    const says = within(ask).getByText(/^Discards/);
    await waitFor(() => expect(says).toHaveFocus());
    expect(within(ask).getByRole('button', { name: 'Discard branch' })).toHaveAccessibleDescription(says.textContent!);

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    expect(screen.getByRole('group', { name: 'discard daoris/s-gone' })).toBeInTheDocument();
    expect(within(ask).getByRole('button', { name: 'Never mind' })).toBeDisabled();

    act(() => answered!.refused('daoris/s-gone is checked out in a tree a session uses, so it was kept.'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('daoris/s-gone is checked out in a tree a session uses, so it was kept.');

    await userEvent.click(within(ask).getByRole('button', { name: 'Discard branch' }));
    act(() => answered!.done());
    expect(screen.queryByRole('group', { name: 'discard daoris/s-gone' })).toBeNull();
  });

  it('says the tree goes too where the branch still has one', async () => {
    drawDiscard();

    await userEvent.click(within(row('daoris/s-here')).getByRole('button', { name: 'Discard branch…' }));
    expect(screen.getByRole('group', { name: 'discard daoris/s-here' }))
      .toHaveTextContent('Discards daoris/s-here in engine and its tree, and with them 1 commit no branch of yours holds. Nothing brings it back.');
  });

  it('lets the person back out, with nothing pressed, and gives the focus back to its row’s Discard branch…', async () => {
    const { onDiscard } = drawDiscard();

    const user = userEvent.setup();
    await user.click(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' }));
    await user.click(screen.getByRole('button', { name: 'Never mind' }));

    expect(onDiscard).not.toHaveBeenCalled();
    expect(screen.queryByRole('group', { name: 'discard daoris/s-gone' })).toBeNull();
    expect(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' })).toHaveFocus();

    // Escape puts it down the same way.
    await user.click(within(row('daoris/s-here')).getByRole('button', { name: 'Discard branch…' }));
    await waitFor(() => expect(within(screen.getByRole('group', { name: 'discard daoris/s-here' })).getByText(/^Discards/)).toHaveFocus());
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('group', { name: 'discard daoris/s-here' })).toBeNull();
    expect(within(row('daoris/s-here')).getByRole('button', { name: 'Discard branch…' })).toHaveFocus();
  });

  it('waits on a discard on its way, and offers none where nothing can press it', async () => {
    const { unmount } = render(
      <Tooltip.Provider>
        <SweepList branches={FAILED} onLook={vi.fn()} onClean={vi.fn()} onDiscard={vi.fn()} discarding="engine:daoris/s-gone" />
      </Tooltip.Provider>,
    );
    expect(within(row('daoris/s-gone')).getByRole('button', { name: 'Discard branch…' })).toBeDisabled();
    expect(within(row('daoris/s-here')).getByRole('button', { name: 'Discard branch…' })).toBeEnabled();
    unmount();

    draw({ branches: FAILED });
    expect(screen.queryByRole('button', { name: 'Discard branch…' })).toBeNull();
  });

  it('asks in 中文 too', async () => {
    await i18n.changeLanguage('zh');
    try {
      drawDiscard();
      await userEvent.click(within(row('daoris/s-gone')).getByRole('button', { name: '丢弃分支…' }));
      const ask = screen.getByRole('group', { name: '丢弃 daoris/s-gone' });
      expect(ask).toHaveTextContent('丢弃 engine 中的 daoris/s-gone，连同你的任何分支都没有的 2 个提交。丢弃后无法找回。');
      expect(within(ask).getByRole('button', { name: '确认丢弃分支' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

// LAND4 (D102's LAND4 note): a kept branch whose tree is still here and holds commits no branch of the person's holds says
// which session it is and how it ended, and how to land it: its page's Accept, or `daoris-driver trees land` from a terminal,
// the line the terminal's list prints beside the same row. A row the driver offers no landing beside says none.

describe('the landing a kept branch offers', () => {
  const KEPT: SweepBranch[] = [
    branch({ branch: 'daoris/s-4e6837ed', kind: 'unlanded', commits: 1, session: { id: 'f41led00', state: 'failed' }, landable: true }),
    branch({ branch: 'daoris/s-56cb4d29', kind: 'unlanded', commits: 2, session: { id: 'f1n1sh00', state: 'completed' }, landable: true }),
    branch({ branch: 'daoris/s-gone', hasTree: false, kind: 'unlanded', commits: 2, discardable: true }),
  ];
  const row = (name: string) => screen.getByRole('listitem', { name });

  it('names the session its tree is, how it ended, and how to land it', () => {
    draw({ branches: KEPT });

    expect(row('daoris/s-4e6837ed')).toHaveTextContent(
      'From session f41led00 (failed), its tree still here: its page offers Accept, which lands it, as daoris-driver trees land f41led00 does.');
    expect(row('daoris/s-56cb4d29')).toHaveTextContent('From session f1n1sh00 (completed), its tree still here');
    expect(row('daoris/s-gone')).not.toHaveTextContent('From session');
  });

  it('says it in 中文 too', async () => {
    await i18n.changeLanguage('zh');
    try {
      draw({ branches: KEPT });
      expect(row('daoris/s-4e6837ed')).toHaveTextContent(
        '来自会话 f41led00（失败），它的工作树还在：在它的页面上采纳即可将其落地，与 daoris-driver trees land f41led00 相同。');
    } finally {
      await i18n.changeLanguage('en');
    }
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

    const group = screen.getByRole('region', { name: 'Landed branches' });
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
    expect(screen.queryByRole('region', { name: 'Landed branches' })).not.toBeInTheDocument();
  });
});

// UXFIX4 (the second-opinion review, `Sweep.tsx:188,244`): a branch's row kept its three columns down to the main area's
// 400 px floor, and cut its name to one line, so a long name could not be read whole by anyone. jsdom lays nothing out, so
// what the rows rest on is held: the name a reader hears is the name the row shows, whole, and the columns follow the
// list's own width.

/** A class that cuts what it holds to one line, or hides what overflows it. */
const CUTS = /^(?:truncate|text-ellipsis|text-clip|overflow-hidden|overflow-x-hidden|whitespace-nowrap|line-clamp-\d+)$/;

/** The element a row is named by, and every class it and what it holds wear. */
function namedBy(row: HTMLElement) {
  const id = row.getAttribute('aria-labelledby');
  const shown = id ? row.ownerDocument.getElementById(id) : null;
  const classes = shown ? [shown, ...shown.querySelectorAll('*')].flatMap((each) => [...each.classList]) : [];
  return { shown, classes };
}

describe('a branch row at a narrow width', () => {
  const LONG = 'feature/0fda18-fix-the-api-gap-before-the-quarter-closes';
  const LONGER = 'daoris/s-1f2e3d4c-a-chain-step-that-carries-a-long-slug';

  it('is named by the name it shows, whole, and nothing cuts that name to a line', () => {
    draw({
      branches: [branch({ branch: LONGER, kind: 'unlanded', commits: 2, discardable: true })],
      landed: [landed({ branch: LONG, kind: 'on-line', where: 'origin/main', removable: true })],
      onDiscard: vi.fn(),
    });

    for (const name of [LONGER, LONG]) {
      const row = screen.getByRole('listitem', { name });
      const { shown, classes } = namedBy(row);
      // What a reader hears is what the row shows: one source for both, so they cannot drift.
      expect(shown).not.toBeNull();
      expect(row).toContainElement(shown);
      expect(shown!.textContent).toBe(name);
      // Shown whole: it wraps after its separators, as a path does, and is never cut with its words in a tip.
      expect(classes.filter((name) => CUTS.test(name))).toEqual([]);
      expect(shown!.querySelector('[data-copy]')).toHaveAttribute('data-copy', name);
    }
  });

  it('keeps the row named whole while its discard asks under it', async () => {
    draw({ branches: [branch({ branch: LONGER, kind: 'unlanded', commits: 2, discardable: true })], onDiscard: vi.fn() });

    await userEvent.click(within(screen.getByRole('listitem', { name: LONGER })).getByRole('button', { name: 'Discard branch…' }));

    const row = screen.getByRole('listitem', { name: LONGER });
    expect(within(row).getByRole('group', { name: `discard ${LONGER}` })).toBeInTheDocument();
    expect(namedBy(row).shown!.textContent).toBe(LONGER);
  });

  it('lays its columns out by its own list’s width, never the window’s', () => {
    draw({ landed: LANDED });

    for (const row of screen.getAllByRole('listitem')) {
      const list = row.closest('ul')!;
      const columns = [...row.classList].filter((name) => /grid-cols-/.test(name));
      expect(list).toHaveClass('@container/branches');
      // Narrow, the name beside its mark and the sentence under the name; three columns only where the list holds them.
      expect(columns).toEqual(['grid-cols-[4.5rem_minmax(0,1fr)]', '@min-[30rem]/branches:grid-cols-[4.5rem_minmax(0,16rem)_minmax(13rem,1fr)]']);
      expect([...row.classList].filter((name) => /^(?:sm|md|lg|xl|2xl|max-(?:sm|md|lg|xl|2xl)):/.test(name))).toEqual([]);
    }
  });
});
